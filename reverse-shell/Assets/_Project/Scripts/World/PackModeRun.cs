using System;
using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Pack Mode as a race: collect every card of every pack's set, as fast as you can.
    ///   A pack's set is its resonance's cards (the Basic pack's: the Basic cards); Soul cards, Sun / Moon / Star
    ///   and blank cards never count. A Basic card pulled from any pack still counts for the Basic set.
    ///   The timer starts with the first pack you open (from the shelf); finishing a set is a split: the time and the
    ///   packs opened so far ("Arc ★ 4:02, 37 packs"), against your best split for that pack. All the stars is a full run.
    ///   Records (best split per pack, best full run, each by time and by packs) are kept in PlayerPrefs.
    /// The run starts fresh every time Pack Mode opens (<see cref="Reset"/>).
    /// </summary>
    public static class PackModeRun
    {
        public class Split
        {
            public float Time;
            public int Packs;
            /// <summary>The best split for this pack before this run (-1 = none), for the ahead / behind colour.</summary>
            public float PrevBest = -1f;
        }

        /// <summary>What a card's reveal did: a new card, the streak of new cards in a row, the set it completed.</summary>
        public struct RevealResult
        {
            public bool NewCard;
            public int Streak;
            public PackKind Completed;
            public bool RunComplete;
        }

        public static bool Started { get; private set; }
        public static int PacksOpened { get; private set; }
        public static string StartPack { get; private set; }
        static float startedAt, finishedAt = -1f;
        static int streak;
        static readonly HashSet<CardDefinition> got = new HashSet<CardDefinition>();
        static readonly Dictionary<string, Split> splits = new Dictionary<string, Split>();
        static readonly HashSet<string> soClose = new HashSet<string>();

        /// <summary>A fresh run: no time, no cards, no stars.</summary>
        public static void Reset()
        {
            Started = false;
            PacksOpened = 0;
            StartPack = null;
            finishedAt = -1f;
            streak = 0;
            got.Clear();
            splits.Clear();
            soClose.Clear();
        }

        /// <summary>Seconds since the first pack (frozen once every set is done).</summary>
        public static float Elapsed
        {
            get
            {
                if (!Started) return 0f;
                return (finishedAt >= 0f ? finishedAt : Time.realtimeSinceStartup) - startedAt;
            }
        }

        public static bool RunComplete { get { return finishedAt >= 0f; } }

        public static List<PackKind> Packs { get { return PackRates.Current.packs; } }

        /// <summary>The cards of <paramref name="kind"/>'s set, from the units with resonance kits (the protagonist; the
        /// Sandbox crew's own cards aren't in any pack).</summary>
        public static List<CardDefinition> SetOf(PackKind kind, IList<CharacterDefinition> roster)
        {
            var list = new List<CardDefinition>();
            if (kind == null || roster == null) return list;
            foreach (var m in KitOwners(roster))
                foreach (var c in CardCollection.AbilitiesOf(m))
                    if (c.pool == kind.pool && !list.Contains(c)) list.Add(c);
            return list;
        }

        /// <summary>The members whose cards packs give: those with resonance kits, else everyone.</summary>
        public static List<CharacterDefinition> KitOwners(IList<CharacterDefinition> roster)
        {
            var owners = new List<CharacterDefinition>();
            foreach (var m in roster) if (m != null && m.resonances.Count > 0) owners.Add(m);
            if (owners.Count == 0) foreach (var m in roster) if (m != null) owners.Add(m);
            return owners;
        }

        public static int Have(PackKind kind, IList<CharacterDefinition> roster)
        {
            int n = 0;
            foreach (var c in SetOf(kind, roster)) if (got.Contains(c)) n++;
            return n;
        }

        public static Split SplitOf(PackKind kind) { Split s; return kind != null && splits.TryGetValue(kind.id, out s) ? s : null; }

        /// <summary>True the first time this run that <paramref name="kind"/>'s set is one card short (its SO CLOSE
        /// banner shows once, not after every pack).</summary>
        public static bool NoteSoClose(PackKind kind)
        {
            return kind != null && soClose.Add(kind.id);
        }

        /// <summary>A pack opened from the shelf: the first one starts the timer.</summary>
        public static void OnPackOpened(PackKind kind)
        {
            if (!Started)
            {
                Started = true;
                startedAt = Time.realtimeSinceStartup;
                StartPack = kind != null ? kind.id : null;
            }
            PacksOpened++;
        }

        /// <summary>A card turned face up: counts it toward its set, the streak, and the splits.</summary>
        public static RevealResult OnReveal(CardPacks.Pull pull, IList<CharacterDefinition> roster)
        {
            var result = new RevealResult();
            if (pull == null || pull.Tier != CardPacks.Tier.Common || pull.Card == null)
            {
                result.Streak = streak;   // Soul / rare / blank: no card for a set, the streak holds
                return result;
            }
            result.NewCard = got.Add(pull.Card);
            streak = result.NewCard ? streak + 1 : 0;
            result.Streak = streak;
            if (!result.NewCard) return result;
            foreach (var kind in Packs)
            {
                if (kind == null || kind.pool != pull.Card.pool || splits.ContainsKey(kind.id)) continue;
                var set = SetOf(kind, roster);
                if (set.Count == 0 || Have(kind, roster) < set.Count) continue;
                splits[kind.id] = new Split { Time = Elapsed, Packs = PacksOpened, PrevBest = Records.BestSplitTime(kind.id) };
                result.Completed = kind;
                Records.NoteSplit(kind.id, Elapsed, PacksOpened);
                if (splits.Count >= Packs.Count)
                {
                    finishedAt = Time.realtimeSinceStartup;
                    result.RunComplete = true;
                    Records.NoteRun(Elapsed, PacksOpened);
                }
            }
            return result;
        }

        /// <summary>"4:02.3"</summary>
        public static string Format(float seconds)
        {
            if (seconds < 0f) return "-";
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return m + ":" + s.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>"+0:12.0" / "-0:03.5" against a best.</summary>
        public static string Delta(float now, float best)
        {
            float d = now - best;
            return (d >= 0f ? "+" : "-") + Format(Mathf.Abs(d));
        }

        // ------------------------------------------------------------------ Records (PlayerPrefs)

        public static class Records
        {
            [Serializable]
            class Best
            {
                public string pack;
                public float time = -1f;
                public int packs = -1;
            }

            [Serializable]
            class Data
            {
                public List<Best> splits = new List<Best>();
                public float runTime = -1f;
                public int runPacks = -1;
            }

            const string Key = "PackMode.Records";
            static Data data;
            /// <summary>Set by the last split / run if it beat the record (for the NEW RECORD flash).</summary>
            public static bool LastWasRecord { get; private set; }

            static Data Load()
            {
                if (data != null) return data;
                try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key, "")); }
                catch (Exception) { data = null; }
                if (data == null) data = new Data();
                return data;
            }

            static void Save()
            {
                try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Load())); PlayerPrefs.Save(); }
                catch (Exception) { }   // no storage: records just aren't kept
            }

            static Best For(string pack)
            {
                var d = Load();
                var b = d.splits.Find(x => x.pack == pack);
                if (b == null) d.splits.Add(b = new Best { pack = pack });
                return b;
            }

            public static float BestSplitTime(string pack) { var b = Load().splits.Find(x => x.pack == pack); return b != null ? b.time : -1f; }
            public static int BestSplitPacks(string pack) { var b = Load().splits.Find(x => x.pack == pack); return b != null ? b.packs : -1; }
            public static float BestRunTime { get { return Load().runTime; } }
            public static int BestRunPacks { get { return Load().runPacks; } }

            public static void NoteSplit(string pack, float time, int packs)
            {
                var b = For(pack);
                LastWasRecord = b.time < 0f || time < b.time;
                if (LastWasRecord) b.time = time;
                if (b.packs < 0 || packs < b.packs) b.packs = packs;
                Save();
            }

            public static void NoteRun(float time, int packs)
            {
                var d = Load();
                LastWasRecord = d.runTime < 0f || time < d.runTime;
                if (LastWasRecord) d.runTime = time;
                if (d.runPacks < 0 || packs < d.runPacks) d.runPacks = packs;
                Save();
            }
        }
    }
}
