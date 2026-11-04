using System;
using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Bonds: how party members join (bible §2, Bonds). You talk with a companion over a few scenes; each scene ends on
    /// a choice, and the better the answer the more bond points. Every <see cref="PointsPerRank"/> points is a rank (five in
    /// all). At <see cref="SoulRank"/> your souls bond: they give you their Soul Card and join the party. Ranks after that
    /// raise their Soul level.
    ///
    /// A scene unlocks with story progress (its Requires flag). If a scene's answer fell short of the next rank, the
    /// companion offers to just spend time together (the .hangout block, <see cref="HangoutPoints"/>) until it's reached.
    /// Rank and points are saved (<see cref="WorldSession.Bonds"/>); seen scenes are story flags (bond.&lt;id&gt;.&lt;rank&gt;).
    ///
    /// Words: Resources/Story/Bonds.txt, per companion "bond.&lt;id&gt;.&lt;n&gt;" (the scene), ".ask" (the question, its last
    /// line), ".options", ".answer.&lt;k&gt;", plus ".hangout", ".idle" (nothing new yet), ".soul" (the Soul Card scene) and
    /// ".max". To add a companion: a BondDef in <see cref="All"/>, their blocks in Bonds.txt, and an NPC that calls
    /// <see cref="Play"/>.
    /// </summary>
    public static class Bonds
    {
        public const int MaxRank = 5, SoulRank = 3, PointsPerRank = 3, HangoutPoints = 1;

        public class Scene
        {
            /// <summary>Story flag that unlocks it (null = from the start).</summary>
            public string Requires;
            /// <summary>Points for each answer, in the .options order.</summary>
            public int[] OptionPoints = { 3, 2, 1 };
        }

        public class Def
        {
            /// <summary>The character asset's name ("Char_Erin").</summary>
            public string Character;
            /// <summary>The text key's id ("erin").</summary>
            public string Id;
            /// <summary>One scene per rank, in order.</summary>
            public Scene[] Scenes;
            /// <summary>After the Soul Card scene (Erin's battle tutorial).</summary>
            public Action<OverworldBuilder> OnJoined;
        }

        public static readonly Def Erin = new Def
        {
            Character = "Char_Erin",
            Id = "erin",
            Scenes = new[]
            {
                new Scene { Requires = Chapter1.Awake },
                new Scene { Requires = Chapter1.Binder },
                new Scene { Requires = Chapter1.Scanner },   // rank 3: the Soul Card, before the Collector
                // after she joins, her scenes find you (ChapterOneDirector.ErinCatchesUp): every answer completes the rank
                new Scene { Requires = Chapter1.CollectorBeaten, OptionPoints = new[] { 3, 3, 3 } },
                new Scene { Requires = Chapter1.DragonSeen, OptionPoints = new[] { 3, 3, 3 } },
            },
        };

        public static readonly Def[] All = { Erin };

        public static Def Find(string character)
        {
            foreach (var d in All) if (d.Character == character) return d;
            return null;
        }

        static WorldSession.BondProgress Progress(string character)
        {
            WorldSession.BondProgress p;
            if (!WorldSession.Bonds.TryGetValue(character, out p) || p == null)
                WorldSession.Bonds[character] = p = new WorldSession.BondProgress();
            return p;
        }

        public static int Rank(string character)
        {
            WorldSession.BondProgress p;
            return WorldSession.Bonds.TryGetValue(character, out p) && p != null ? p.Rank : 0;
        }

        public static bool Joined(string character) { return Rank(character) >= SoulRank; }

        static string SeenFlag(Def d, int rank) { return "bond." + d.Id + "." + rank; }

        public enum Next { Nothing, Scene, Hangout, Max }

        /// <summary>What talking to them does now.</summary>
        public static Next WhatsNext(Def d)
        {
            int rank = Rank(d.Character);
            if (rank >= MaxRank) return Next.Max;
            if (StoryState.Has(SeenFlag(d, rank + 1))) return Next.Hangout;   // the scene is done, the points aren't
            var scene = rank < d.Scenes.Length ? d.Scenes[rank] : null;
            if (scene == null) return Next.Nothing;
            return string.IsNullOrEmpty(scene.Requires) || StoryState.Has(scene.Requires) ? Next.Scene : Next.Nothing;
        }

        /// <summary>Adds points; returns the new rank if it went up (0 otherwise). Pure (tests use it).</summary>
        public static int AddPoints(string character, int points)
        {
            var p = Progress(character);
            if (p.Rank >= MaxRank) return 0;
            p.Points += Mathf.Max(0, points);
            if (p.Points < PointsPerRank) return 0;
            p.Points = 0;
            p.Rank++;
            return p.Rank;
        }

        /// <summary>Talk to a companion: the next scene, a hangout, or an idle line. Runs as the conversation.</summary>
        public static IEnumerator Play(OverworldBuilder b, Def d, Func<string, DialogueVoice> voiceFor)
        {
            var member = b.AllCharacters.Find(c => c != null && c.name == d.Character);
            int before = Rank(d.Character);
            var next = WhatsNext(d);
            b.SetCutscene(true);
            int gained = 0;
            if (next == Next.Scene)
            {
                string key = "bond." + d.Id + "." + (before + 1);
                yield return Talk(b, Lines(key, voiceFor, member));
                int answer = -1;
                var ask = Lines(key + ".ask", voiceFor, member);
                if (ask.Count > 1) yield return Talk(b, ask.GetRange(0, ask.Count - 1));
                if (ask.Count > 0)
                {
                    b.UI.Dialogue.Ask(ask[ask.Count - 1], StoryText.Texts(key + ".options"), i => answer = i);
                    while (answer < 0) yield return null;
                    yield return Talk(b, Lines(key + ".answer." + (answer + 1), voiceFor, member));
                }
                var pts = d.Scenes[before].OptionPoints;
                gained = answer >= 0 && answer < pts.Length ? pts[answer] : 1;
                StoryState.Set(SeenFlag(d, before + 1));
            }
            else if (next == Next.Hangout)
            {
                yield return Talk(b, Lines("bond." + d.Id + ".hangout", voiceFor, member));
                gained = HangoutPoints;
            }
            else
            {
                yield return Talk(b, Lines("bond." + d.Id + (next == Next.Max ? ".max" : ".idle"), voiceFor, member));
                b.SetCutscene(false);
                yield break;
            }

            int rank = AddPoints(d.Character, gained);
            SaveGame.Save();
            if (rank > 0)
            {
                var canvas = (RectTransform)b.UI.transform;
                yield return BondRankUp.Show(canvas, member, rank);
                if (rank == SoulRank)
                {
                    yield return Talk(b, Lines("bond." + d.Id + ".soul", voiceFor, member));
                    WorldSession.Recruit(d.Character);
                    if (member != null) CardCollection.MarkNew(CardCollection.SoulKey(member));
                    SaveGame.Save();
                    yield return BondRankUp.SoulCard(canvas, member);
                    b.SetCutscene(false);
                    b.RefreshParty();
                    if (d.OnJoined != null) d.OnJoined(b);
                    yield break;
                }
                if (rank > SoulRank && member != null)
                {
                    CardCollection.AddSoul(member);   // a deeper bond: their Soul level rises
                    SaveGame.Save();
                }
            }
            b.SetCutscene(false);
        }

        /// <summary>The block's lines, with the companion's sprite as the portrait on their own lines.</summary>
        static List<DialogueLine> Lines(string key, Func<string, DialogueVoice> voiceFor, CharacterDefinition member)
        {
            var lines = StoryText.Lines(key, voiceFor);
            var portrait = member != null ? OverworldBuilder.PortraitOf(member.Anim) : null;
            if (member != null)
                foreach (var l in lines)
                    if (l.Speaker == member.DisplayName) l.Portrait = portrait;
            return lines;
        }

        static IEnumerator Talk(OverworldBuilder b, IList<DialogueLine> lines)
        {
            if (lines == null || lines.Count == 0) yield break;
            bool done = false;
            b.UI.Talk(lines, () => done = true);
            while (!done) yield return null;
        }
    }
}
