using System;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Pack Mode as bite-sized races: each pack is its own speedrun to collect every card of its set.
    ///   A pack's set is its resonance's cards (the Basic pack's: the Basic cards); Soul cards, Sun / Moon / Star
    ///   and blank cards never count. Only cards pulled from that pack's own openings count for its set.
    ///   Each pack has its own clock, which runs only while its shelf tab is selected (switching tabs pauses it). The
    ///   clock starts with that pack's first opening and stops when its set is complete. Restart wipes the pack's
    ///   run and its set's cards from the throwaway book.
    ///   Records (per pack: fastest time and fewest packs) are kept in PlayerPrefs.
    /// Every run starts fresh when Pack Mode opens (<see cref="Reset"/>).
    /// </summary>
    public static class PackModeRun
    {
        /// <summary>One pack's race.</summary>
        public class Run
        {
            public bool Started, Finished;
            public int Packs;
            public int Streak;
            /// <summary>Seconds banked before the current stretch on the clock.</summary>
            public float Banked;
            /// <summary>When the current stretch began (-1 = the clock isn't running).</summary>
            public float RunningSince = -1f;
            public bool SoClose;
            /// <summary>The bests before this run's finish (-1 = none), for the ahead / behind colour and the delta.</summary>
            public float PrevBestTime = -1f;
            public int PrevBestPacks = -1;
            public bool TimeRecord, PacksRecord;
            public readonly HashSet<CardDefinition> Got = new HashSet<CardDefinition>();

            public float Elapsed { get { return Banked + (RunningSince >= 0f ? Time.realtimeSinceStartup - RunningSince : 0f); } }
        }

        /// <summary>What a card's reveal did: a new card, the streak of new cards in a row, the set it completed.</summary>
        public struct RevealResult
        {
            public bool NewCard;
            public int Streak;
            public PackKind Completed;
        }

        static readonly Dictionary<string, Run> runs = new Dictionary<string, Run>();
        static PackKind active;

        /// <summary>The pack whose race the shelf has selected (null: the Soul tab, which races nothing).</summary>
        public static PackKind Active { get { return active; } }

        /// <summary>Set when a race finishes; the Cards page shows its results once the pack opening closes.</summary>
        public static PackKind JustFinished;

        public static List<PackKind> Packs { get { return PackRates.Current.packs; } }

        /// <summary>Every race back to zero (Pack Mode opened).</summary>
        public static void Reset()
        {
            runs.Clear();
            active = null;
            JustFinished = null;
        }

        public static Run Of(PackKind kind)
        {
            if (kind == null) return null;
            Run run;
            if (!runs.TryGetValue(kind.id, out run)) runs[kind.id] = run = new Run();
            return run;
        }

        /// <summary>The shelf picked <paramref name="kind"/>: the old race's clock pauses, this one's carries on.</summary>
        public static void Select(PackKind kind)
        {
            if (kind == active) return;
            Pause(Of(active));
            active = kind;
            var run = Of(kind);
            if (run != null && run.Started && !run.Finished) run.RunningSince = Time.realtimeSinceStartup;
        }

        static void Pause(Run run)
        {
            if (run == null || run.RunningSince < 0f) return;
            run.Banked += Time.realtimeSinceStartup - run.RunningSince;
            run.RunningSince = -1f;
        }

        /// <summary>The active race's time (0 on the Soul tab).</summary>
        public static float Elapsed { get { var run = Of(active); return run != null ? run.Elapsed : 0f; } }

        /// <summary>A fresh run for <paramref name="kind"/>: no time, no packs, and its set's cards gone from the book.</summary>
        public static void Restart(PackKind kind, IList<CharacterDefinition> roster)
        {
            if (kind == null) return;
            runs.Remove(kind.id);
            if (JustFinished == kind) JustFinished = null;
            CardCollection.ForgetSessionCards(SetOf(kind, roster));
        }

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
            var run = Of(kind);
            int n = 0;
            if (run != null) foreach (var c in SetOf(kind, roster)) if (run.Got.Contains(c)) n++;
            return n;
        }

        /// <summary>This race has pulled <paramref name="card"/> (the book shows the others as "?").</summary>
        public static bool Has(PackKind kind, CardDefinition card) { var run = Of(kind); return run != null && run.Got.Contains(card); }

        /// <summary>True the first time this run that <paramref name="kind"/>'s set is one card short (its SO CLOSE
        /// banner shows once, not after every pack).</summary>
        public static bool NoteSoClose(PackKind kind)
        {
            var run = Of(kind);
            if (run == null || run.SoClose) return false;
            return run.SoClose = true;
        }

        /// <summary>A pack opened: if it's the active race's pack, the first one starts its clock.</summary>
        public static void OnPackOpened(PackKind kind)
        {
            if (kind == null || kind != active) return;
            var run = Of(kind);
            if (run.Finished) return;
            if (!run.Started)
            {
                run.Started = true;
                run.RunningSince = Time.realtimeSinceStartup;
            }
            run.Packs++;
        }

        /// <summary>A card from a <paramref name="kind"/> pack turned face up: counts it toward that race's set and streak.</summary>
        public static RevealResult OnReveal(CardPacks.Pull pull, PackKind kind, IList<CharacterDefinition> roster)
        {
            var result = new RevealResult();
            var run = kind == active ? Of(kind) : null;
            if (run == null || !run.Started || run.Finished) return result;
            result.Streak = run.Streak;
            if (pull == null || pull.Tier != CardPacks.Tier.Common || pull.Card == null || pull.Card.pool != kind.pool)
                return result;   // Soul / rare / blank / another set's card: the streak holds
            result.NewCard = run.Got.Add(pull.Card);
            run.Streak = result.NewCard ? run.Streak + 1 : 0;
            result.Streak = run.Streak;
            if (!result.NewCard) return result;
            var set = SetOf(kind, roster);
            if (set.Count == 0 || Have(kind, roster) < set.Count) return result;
            Pause(run);
            run.Finished = true;
            run.PrevBestTime = Records.BestTime(kind.id);
            run.PrevBestPacks = Records.BestPacks(kind.id);
            Records.Note(kind.id, run.Elapsed, run.Packs, out run.TimeRecord, out run.PacksRecord);
            result.Completed = kind;
            JustFinished = kind;
            return result;
        }

        /// <summary>The colour a race's clock reads in: gold with no best yet, green while at or under the best,
        /// red past it.</summary>
        public static Color PaceColor(PackKind kind)
        {
            var run = Of(kind);
            if (run == null) return InkStyle.GoldBright;
            float best = run.Finished ? run.PrevBestTime : Records.BestTime(kind.id);
            if (best < 0f) return run.Finished ? Ahead : InkStyle.GoldBright;
            return run.Elapsed <= best ? Ahead : Behind;
        }

        public static readonly Color Ahead = new Color(0.5f, 0.88f, 0.54f), Behind = new Color(1f, 0.48f, 0.42f);

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
            }

            const string Key = "PackMode.Records";
            static Data data;

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

            public static float BestTime(string pack) { var b = Load().splits.Find(x => x.pack == pack); return b != null ? b.time : -1f; }
            public static int BestPacks(string pack) { var b = Load().splits.Find(x => x.pack == pack); return b != null ? b.packs : -1; }

            /// <summary>A finished run: keeps whichever of its time and pack count beat the record.</summary>
            public static void Note(string pack, float time, int packs, out bool timeRecord, out bool packsRecord)
            {
                var d = Load();
                var b = d.splits.Find(x => x.pack == pack);
                if (b == null) d.splits.Add(b = new Best { pack = pack });
                timeRecord = b.time < 0f || time < b.time;
                packsRecord = b.packs < 0 || packs < b.packs;
                if (timeRecord) b.time = time;
                if (packsRecord) b.packs = packs;
                Save();
            }
        }
    }
}
