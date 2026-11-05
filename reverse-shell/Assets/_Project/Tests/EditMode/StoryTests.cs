using System.Collections.Generic;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public class StoryTests
    {
        [SetUp]
        public void SetUp() { SaveGame.Restore(null); }

        [TearDown]
        public void TearDown() { SaveGame.Restore(null); }

        [Test]
        public void ThoughtLines_HaveNoSpeaker_AndAreTinted()
        {
            var blocks = new Dictionary<string, List<StoryText.Entry>>();
            StoryText.Parse("[t]\nThought | Light.\nErin | Hi.", blocks);
            Assert.AreEqual(StoryText.Thought, blocks["t"][0].Speaker);

            StoryState.SetName("Wren");
            foreach (var key in new[] { "awakening", "erin.vanished", "memory.after", "ending" })
                foreach (var line in StoryText.Lines(key, s => DialogueVoice.Default))
                {
                    Assert.AreNotEqual("Wren", line.Speaker, key + ": the protagonist thinks, they don't get a name plate");
                    Assert.AreNotEqual(StoryText.Thought, line.Speaker, key);
                }
            var light = StoryText.Lines("awakening", s => DialogueVoice.Default).Find(l => l.Text.Contains("Light."));
            Assert.IsNotNull(light);
            Assert.AreEqual("", light.Speaker);
            StringAssert.StartsWith("{c=" + StoryText.ThoughtColor + "}", light.Text);
        }

        [Test]
        public void Objective_FollowsTheFlags()
        {
            Assert.AreEqual("", Chapter1.CurrentObjective(), "nothing before waking");
            StoryState.Set(Chapter1.Awake);
            StringAssert.Contains("Explore", Chapter1.CurrentObjective());
            StoryState.Set(Chapter1.Scanner);
            StringAssert.Contains("scanner", Chapter1.CurrentObjective());
            StoryState.Set(Chapter1.CollectorMet);
            StringAssert.Contains("Collector", Chapter1.CurrentObjective());
            StoryState.Set(Chapter1.CollectorBeaten);
            StringAssert.Contains("Scan", Chapter1.CurrentObjective());
            StoryState.Set(Chapter1.Route);
            StoryState.Set(Chapter1.Tunnel);
            StoryState.Set(Chapter1.DragonSeen);
            StringAssert.Contains("guardian", Chapter1.CurrentObjective());
            StoryState.Set(Chapter1.DragonBeaten);
            StringAssert.Contains("Soul Card", Chapter1.CurrentObjective());
            StoryState.Set(Chapter1.ResonanceArc);
            StoryState.Set(Chapter1.Complete);
            StringAssert.Contains("complete", Chapter1.CurrentObjective());
        }

        [Test]
        public void Allows_ChecksRequiredAndBlockedFlags()
        {
            StoryState.Set("a");
            Assert.IsTrue(StoryState.Allows(new[] { "a" }, null));
            Assert.IsFalse(StoryState.Allows(new[] { "a", "b" }, null));
            Assert.IsFalse(StoryState.Allows(null, new[] { "a" }));
            Assert.IsTrue(StoryState.Allows(null, null));
        }

        [Test]
        public void SaveRoundTrip_KeepsFlagsNameAndResonance()
        {
            StoryState.Set(Chapter1.Scanner);
            StoryState.SetName("Wren");
            WorldSession.UnlockResonance(Resonance.Arc);
            WorldSession.SetResonance(Resonance.Arc);
            WorldSession.CurrentArea = WorldAreas.Tunnel;
            var json = JsonUtility.ToJson(SaveGame.Capture());

            SaveGame.Restore(null);
            Assert.IsFalse(StoryState.Has(Chapter1.Scanner));

            SaveGame.Restore(JsonUtility.FromJson<SaveGame.Data>(json));
            Assert.IsTrue(StoryState.Has(Chapter1.Scanner));
            Assert.AreEqual("Wren", StoryState.PlayerName);
            Assert.IsTrue(WorldSession.UnlockedResonances.Contains(Resonance.Arc));
            Assert.AreEqual(Resonance.Arc, WorldSession.ActiveResonance);
            Assert.AreEqual(WorldAreas.Tunnel, WorldSession.CurrentArea);
        }

        [Test]
        public void ChosenName_ReplacesTheProtagonistsDisplayName()
        {
            var a3 = TestFactory.Character(StoryState.ProtagonistAsset);
            StoryState.SetName("Wren");
            Assert.AreEqual("Wren", a3.DisplayName);
            StoryState.SetName("");
            Assert.AreEqual(StoryState.ProtagonistAsset, a3.DisplayName);
        }

        static CharacterDefinition Protagonist(out CardDefinition physical, out CardDefinition electric, out CardDefinition ult)
        {
            physical = TestFactory.Card("Strike");
            electric = TestFactory.Card("Arc");
            ult = TestFactory.Card("Thunder", 5f, ritual: true);
            var a3 = TestFactory.Character("Hero", Resonance.Arc, 100f, null, physical);
            a3.resonances = new List<ResonanceKit>
            {
                new ResonanceKit { element = Resonance.Basic, cards = new List<CardDefinition> { physical } },
                new ResonanceKit { element = Resonance.Arc, cards = new List<CardDefinition> { electric }, ultimate = ult },
            };
            return a3;
        }

        [Test]
        public void Resonance_StartsBasic_ElectricNeedsUnlocking_AndSwapsCardsElementAndUlt()
        {
            CardDefinition physical, electric, ult;
            var a3 = Protagonist(out physical, out electric, out ult);

            Assert.AreEqual(new[] { Resonance.Basic }, WorldSession.AvailableResonances(a3).ToArray());
            WorldSession.SetResonance(Resonance.Arc);   // not unlocked yet: still Basic
            Assert.AreEqual(Resonance.Basic, WorldSession.ElementOf(a3));
            CollectionAssert.AreEqual(new[] { physical }, WorldSession.DeckOf(a3));

            WorldSession.UnlockResonance(Resonance.Arc);
            Assert.AreEqual(Resonance.Arc, WorldSession.ElementOf(a3));
            CollectionAssert.AreEqual(new[] { electric }, WorldSession.DeckOf(a3));
            Assert.AreSame(ult, WorldSession.UltimateOf(a3));

            var unit = new BattleUnit(a3, Team.Player);
            unit.ApplyLoadout(WorldSession.LoadoutsFor(new[] { a3 })[a3]);
            Assert.AreEqual(Resonance.Arc, unit.Resonance);
            Assert.AreSame(ult, unit.Ultimate);
        }

        [Test]
        public void SandboxMode_LendsElectric_AndUnlocksTheResonanceMenu()
        {
            CardDefinition physical, electric, ult;
            var a3 = Protagonist(out physical, out electric, out ult);
            WorldSession.CurrentArea = WorldAreas.City;
            Assert.IsFalse(WorldSession.ResonanceMenuUnlocked);

            WorldSession.CurrentArea = WorldAreas.Sandbox;
            Assert.IsTrue(WorldSession.ResonanceMenuUnlocked);
            Assert.AreEqual(new[] { Resonance.Basic, Resonance.Arc }, WorldSession.AvailableResonances(a3).ToArray());

            WorldSession.CurrentArea = WorldAreas.City;
            StoryState.Set(Chapter1.Complete);
            Assert.IsTrue(WorldSession.ResonanceMenuUnlocked);
        }

        [Test]
        public void Resonance_SetsTheColour_ElectricYellow_BasicOwn()
        {
            CardDefinition physical, electric, ult;
            var a3 = Protagonist(out physical, out electric, out ult);
            a3.color = Color.white;
            Assert.AreEqual(Color.white, WorldSession.ColorOf(a3));

            WorldSession.UnlockResonance(Resonance.Arc);
            WorldSession.SetResonance(Resonance.Arc);
            var yellow = ResonanceInfo.Color(Resonance.Arc);
            Assert.Greater(yellow.r, yellow.b + 0.5f, "Electric reads as yellow");
            Assert.AreEqual(yellow, WorldSession.ColorOf(a3));

            var unit = new BattleUnit(a3, Team.Player);
            unit.ApplyLoadout(WorldSession.LoadoutsFor(new[] { a3 })[a3]);
            Assert.AreEqual(yellow, unit.Color);
        }

        [Test]
        public void Basic_IsNoResonanceWeakness()
        {
            Assert.AreEqual(Weakness.None, BreakRules.Of(Resonance.Basic));
            foreach (Resonance other in System.Enum.GetValues(typeof(Resonance)))
                if (other != Resonance.Basic) Assert.AreNotEqual(Weakness.None, BreakRules.Of(other));
        }

        [Test]
        public void Stunned_StopsAUnitActing()
        {
            var stunned = ScriptableObject.CreateInstance<StatusDefinition>();
            stunned.preventsAction = true;
            var unit = new BattleUnit(TestFactory.Enemy("E"), Team.Enemy);
            Assert.IsFalse(unit.IsStunned);
            unit.Statuses.Add(new StatusInstance(stunned, null, 1, 1));
            Assert.IsTrue(unit.IsStunned);
        }

        [Test]
        public void PositionAndAccessPoint_SurviveASave_AndContinuePutsYouBack()
        {
            WorldSession.CurrentArea = WorldAreas.Tunnel;
            WorldSession.PlayerSpot = new WorldSession.Spot { area = WorldAreas.Tunnel, position = new Vector3(5f, 0f, -9f), facing = 1 };
            WorldSession.LastAccessPoint = new WorldSession.Spot { area = WorldAreas.City, position = new Vector3(20f, 0f, -15f), facing = 3 };
            var json = JsonUtility.ToJson(SaveGame.Capture());
            SaveGame.Restore(null);
            WorldSession.HasReturnPoint = false;
            Assert.IsNull(WorldSession.LastAccessPoint);
            SaveGame.Restore(JsonUtility.FromJson<SaveGame.Data>(json));
            Assert.AreEqual(WorldAreas.Tunnel, WorldSession.CurrentArea);
            Assert.IsTrue(WorldSession.HasReturnPoint, "Continue starts where you saved");
            Assert.AreEqual(new Vector3(5f, 0f, -9f), WorldSession.ReturnPosition);
            Assert.AreEqual(1, WorldSession.ReturnFacing);
            Assert.AreEqual(WorldAreas.City, WorldSession.LastAccessPoint.area);
            Assert.AreEqual(new Vector3(20f, 0f, -15f), WorldSession.LastAccessPoint.position);
        }

        [Test]
        public void ASaveMadeWhileTravelling_StartsAtTheAreaSpawn()
        {
            WorldSession.CurrentArea = WorldAreas.Chamber;
            WorldSession.PlayerSpot = null;
            var json = JsonUtility.ToJson(SaveGame.Capture());
            WorldSession.HasReturnPoint = false;
            SaveGame.Restore(JsonUtility.FromJson<SaveGame.Data>(json));
            Assert.IsFalse(WorldSession.HasReturnPoint);
        }

        [Test]
        public void RespawnPatrols_ClearsBeatenMachines()
        {
            WorldSession.DefeatedEncounters.Add("a");
            WorldSession.DefeatedEncounters.Add("b");
            Assert.AreEqual(2, WorldSession.RespawnPatrols());
            Assert.AreEqual(0, WorldSession.DefeatedEncounters.Count);
        }

        [Test]
        public void StoryText_FillsIns()
        {
            Assert.AreEqual("You got the Ult Chip.", StoryText.Fill("You got the {item}.", "item", "Ult Chip"));
            Assert.AreEqual("{gold}x{/c}", StoryText.Fill("{gold}x{/c}", "item", "y"), "markup is left alone");
        }

        [Test]
        public void SandboxReturnPoint_SurvivesASave()
        {
            WorldSession.CurrentArea = WorldAreas.Sandbox;
            WorldSession.HasSandboxReturn = true;
            WorldSession.SandboxReturnArea = WorldAreas.Tunnel;
            WorldSession.SandboxReturnPosition = new Vector3(3f, 0f, -7f);
            WorldSession.SandboxReturnFacing = 2;
            var json = JsonUtility.ToJson(SaveGame.Capture());
            SaveGame.Restore(null);
            Assert.IsFalse(WorldSession.HasSandboxReturn);
            Assert.IsFalse(WorldSession.InSandbox);
            SaveGame.Restore(JsonUtility.FromJson<SaveGame.Data>(json));
            Assert.IsTrue(WorldSession.InSandbox);
            Assert.IsTrue(WorldSession.HasSandboxReturn);
            Assert.AreEqual(WorldAreas.Tunnel, WorldSession.SandboxReturnArea);
            Assert.AreEqual(new Vector3(3f, 0f, -7f), WorldSession.SandboxReturnPosition);
            Assert.AreEqual(2, WorldSession.SandboxReturnFacing);
        }

        [Test]
        public void StoryText_ParsesBlocksSpeakersAndComments()
        {
            var blocks = new Dictionary<string, List<StoryText.Entry>>();
            StoryText.Parse("# a comment\n[intro]\nNarration here.\nCollector | Stop there.\n\n[other]\nOne\n", blocks);
            Assert.AreEqual(2, blocks.Count);
            Assert.AreEqual(2, blocks["intro"].Count);
            Assert.AreEqual("", blocks["intro"][0].Speaker);
            Assert.AreEqual("Narration here.", blocks["intro"][0].Text);
            Assert.AreEqual("Collector", blocks["intro"][1].Speaker);
            Assert.AreEqual("Stop there.", blocks["intro"][1].Text);
        }

        [Test]
        public void StoryText_HasEveryKeyTheGameUses()
        {
            StoryText.Reload();
            string[] keys =
            {
                "prologue.recording", "prologue.tagline", "prologue.procedure", "prologue.name", "prologue.name.question",
                "awakening", "binder.pickup", "pod", "chip.pickup", "chip.colours.pickup", "chip.numbers.pickup", "memory.2", "memory.3", "memory.after",
                "record.city.4", "record.city.5", "record.city.6", "record.city.7", "record.tunnel.4", "npc.small_machine",
                "scanner.pickup", "scan.jammed", "scan.trace", "scan.none.hint", "scan.none", "route.open",
                "collector.intro", "collector.question", "collector.options", "collector.answer.1", "collector.answer.2",
                "collector.final", "collector.again", "collector.win", "guardian.intro", "guardian.again", "guardian.win",
                "pedestal.empty", "pedestal.blocked", "absorb", "absorb.banner", "absorb.core", "ending", "endcard",
                // World.txt
                "objective.explore", "objective.scanner", "objective.collector", "objective.scan", "objective.route",
                "objective.tunnel", "objective.guardian", "objective.card", "objective.leave", "objective.complete",
                "battle.fled", "battle.lost", "access.rest", "access.respawn", "item.found", "chest.empty",
                "beacon.none", "beacon.ask", "beacon.options", "beacon.sent", "sandbox.joins", "sandbox.joins.many",
                "sandbox.trader", "sandbox.scout", "sandbox.wanderer",
            };
            foreach (var key in keys) Assert.IsTrue(StoryText.Has(key), "Resources/Story is missing [" + key + "]");
            Assert.AreEqual(2, StoryText.Texts("collector.options").Length, "the Collector's question has two answers");
        }

        [Test]
        public void DialoguePager_SplitsLongLinesAndCarriesColours()
        {
            var pages = DialoguePager.Split("{gold}one two three four five six{/c}", plain => plain.Length <= 14);
            Assert.Greater(pages.Count, 1);
            foreach (var page in pages)
            {
                StringAssert.StartsWith("{gold}", page);
                Assert.LessOrEqual(DialoguePager.Plain(page).Length, 14);
            }
        }

        /// <summary>Every key the code asks UI.txt for (any dotted key on a line with UIText.Get) is in the file.</summary>
        [Test]
        public void UIText_EveryKeyInCodeExists()
        {
            Combat.View.UIText.Reload();
            var key = new System.Text.RegularExpressions.Regex("\"([a-z_]+(?:\\.[a-z_]+)+)\"");
            var scripts = System.IO.Path.Combine(Application.dataPath, "_Project/Scripts");
            int checkedKeys = 0;
            foreach (var file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
                foreach (var line in System.IO.File.ReadAllLines(file))
                {
                    if (!line.Contains("UIText.Get(")) continue;
                    foreach (System.Text.RegularExpressions.Match m in key.Matches(line))
                    {
                        Assert.IsTrue(Combat.View.UIText.Has(m.Groups[1].Value),
                            System.IO.Path.GetFileName(file) + " asks for " + m.Groups[1].Value + ", which UI.txt doesn't have");
                        checkedKeys++;
                    }
                }
            Assert.Greater(checkedKeys, 50, "found the UIText calls");
        }

        [Test]
        public void UIText_ParsesKeysNewlinesAndColours()
        {
            var lines = new Dictionary<string, string>();
            Combat.View.UIText.Parse("# comment\nmenu.back = Back\nbattle.card_moved = CARD\\nMOVED\nx.y = {gold}Hi{/c} <size=40>a</size>\n", lines);
            Assert.AreEqual("Back", lines["menu.back"]);
            Assert.AreEqual("CARD\nMOVED", lines["battle.card_moved"]);
            StringAssert.StartsWith("<color=#", lines["x.y"]);
            StringAssert.EndsWith("</color> <size=40>a</size>", lines["x.y"]);
        }
    }
}
