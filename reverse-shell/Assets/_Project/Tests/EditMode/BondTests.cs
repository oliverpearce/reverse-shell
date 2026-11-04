using System.Collections.Generic;
using System.Linq;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Bonds (how party members join) and the rules Erin's battle tutorial relies on.</summary>
    public class BondTests
    {
        [SetUp]
        public void Reset()
        {
            SaveGame.Restore(null);   // a clean session
            WeaknessBook.Clear();
        }

        [TearDown]
        public void Clean()
        {
            SaveGame.Restore(null);
            WeaknessBook.AlwaysKnown = Weakness.None;
            WeaknessBook.Clear();
        }

        [Test]
        public void Points_MakeRanks_UpToFive()
        {
            const string erin = "Char_Erin";
            Assert.AreEqual(0, Bonds.Rank(erin));
            Assert.AreEqual(0, Bonds.AddPoints(erin, 2), "2 points: not a rank yet");
            Assert.AreEqual(1, Bonds.AddPoints(erin, 1), "the 3rd point is rank 1");
            Assert.AreEqual(2, Bonds.AddPoints(erin, 3));
            Assert.IsFalse(Bonds.Joined(erin));
            Assert.AreEqual(Bonds.SoulRank, Bonds.AddPoints(erin, 3));
            Assert.IsTrue(Bonds.Joined(erin), "rank 3: the Soul Card");
            Bonds.AddPoints(erin, 3);
            Assert.AreEqual(Bonds.MaxRank, Bonds.AddPoints(erin, 3));
            Assert.AreEqual(0, Bonds.AddPoints(erin, 3), "nothing past the last rank");
            Assert.AreEqual(Bonds.MaxRank, Bonds.Rank(erin));
        }

        [Test]
        public void Scenes_UnlockWithTheStory_ThenWaitForPoints()
        {
            var d = Bonds.Erin;
            Assert.AreEqual(Bonds.Next.Nothing, Bonds.WhatsNext(d), "not awake yet");
            StoryState.Set(Chapter1.Awake);
            Assert.AreEqual(Bonds.Next.Scene, Bonds.WhatsNext(d));
            StoryState.Set("bond.erin.1");   // the scene was played...
            Bonds.AddPoints(d.Character, 2);  // ...with a so-so answer
            Assert.AreEqual(Bonds.Next.Hangout, Bonds.WhatsNext(d), "short of rank 1: spend time together");
            Bonds.AddPoints(d.Character, 1);
            Assert.AreEqual(1, Bonds.Rank(d.Character));
            Assert.AreEqual(Bonds.Next.Nothing, Bonds.WhatsNext(d), "rank 2 waits for the binder");
        }

        [Test]
        public void Recruit_JoinsTheFormation_AndLeaveTakesThemOut()
        {
            WorldSession.Members.Add("Char_A3");
            WorldSession.Formation[0] = "Char_A3";
            Assert.IsTrue(WorldSession.Recruit("Char_Erin"));
            Assert.IsTrue(WorldSession.Members.Contains("Char_Erin"));
            Assert.AreEqual("Char_Erin", WorldSession.Formation[1]);
            Assert.IsFalse(WorldSession.Recruit("Char_Erin"), "already in");

            WorldSession.Leave("Char_Erin");
            Assert.IsFalse(WorldSession.Members.Contains("Char_Erin"));
            Assert.AreEqual("", WorldSession.Formation[1]);
        }

        [Test]
        public void Bonds_SaveAndLoad()
        {
            Bonds.AddPoints("Char_Erin", 3);
            Bonds.AddPoints("Char_Erin", 2);
            var json = JsonUtility.ToJson(SaveGame.Capture());
            SaveGame.Restore(null);
            Assert.AreEqual(0, Bonds.Rank("Char_Erin"));
            SaveGame.Restore(JsonUtility.FromJson<SaveGame.Data>(json));
            Assert.AreEqual(1, Bonds.Rank("Char_Erin"));
            Assert.AreEqual(2, WorldSession.Bonds["Char_Erin"].Points);
        }

        [Test]
        public void Tutorial_TapMergeCast_BreaksTheMachine()
        {
            // The tutorial's flow on the real battle controller (no UI): two fighters (2 AP), a set hand, the machine's
            // Break set to a Rank I + a Rank II Strike. Tap, move into a merge, Go; cast the Rank II card, Go: BREAK.
            var strike = TestFactory.Card("Strike");
            strike.attackType = AttackType.Physical;
            strike.breakValue = 1;
            var spark = TestFactory.Card("Spark");
            var a3 = TestFactory.Character("Hero", Resonance.Basic, 100f, null, strike);
            var erin = TestFactory.Character("Erin", Resonance.Basic, 100f, null, spark);
            var e = TestFactory.Enemy("Hound", resonance: Resonance.Basic, skills: new EnemySkill { card = TestFactory.Card("Bite"), weight = 1f });
            e.breakPoints = 10;
            e.weaknesses = Weakness.Physical;
            var battle = new BattleController(new[] { a3, erin }, new[] { e }, CombatConfig.Default, 7);
            battle.StartBattle();
            var hero = battle.State.Party[0];
            var friend = battle.State.Party[1];
            var enemy = battle.State.Enemies[0];
            enemy.Break = BreakRules.Value(strike, 1) + BreakRules.Value(strike, 2);
            Assert.AreEqual(2, battle.State.ActionPoints, "one AP per fighter");

            var first = new CardInstance(strike, hero, 1);
            var b = new CardInstance(strike, hero, 1);
            var c = new CardInstance(strike, hero, 1);
            battle.SetHand(new List<CardInstance> { first, new CardInstance(spark, friend, 1), b, new CardInstance(spark, friend, 1), c });

            Assert.IsTrue(battle.QueueCard(first), "step 1: tap the Physical card");
            Assert.AreEqual(4, battle.State.Hand.Count, "taking it out merges nothing (the others are apart)");
            Assert.IsTrue(battle.MoveCard(c, 2), "step 2: move it beside its match");
            var merged = battle.State.Hand.Cards.FirstOrDefault(x => x.Definition == strike && x.Rank == 2);
            Assert.IsNotNull(merged, "they merge into Rank II");
            Assert.AreEqual(0, battle.State.ActionPoints);

            battle.EndPlanning();   // the Rank I Strike lands
            Assert.AreEqual(BreakRules.Value(strike, 2), enemy.Break);
            Assert.IsFalse(enemy.Staggered);

            // New draws can merge with it, so (like the tutorial) the cast round deals the Rank II card it needs.
            var rank2 = battle.State.Hand.Cards.FirstOrDefault(x => x.Definition == strike && x.Rank == 2)
                        ?? new CardInstance(strike, hero, 2);
            battle.SetHand(new List<CardInstance> { rank2, new CardInstance(spark, friend, 1) });
            Assert.IsTrue(battle.QueueCard(rank2), "step 3: cast the Rank II card");
            battle.EndPlanning();
            Assert.IsTrue(enemy.Staggered, "BREAK");
            Assert.IsTrue(battle.Events.OfType<StaggeredEvent>().Any(x => x.Unit == enemy));
        }
    }
}
