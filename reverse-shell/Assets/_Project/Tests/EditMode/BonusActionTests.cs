using System.Collections.Generic;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>The Break estimate (found weaknesses only) and the bonus action earned by Staggering an enemy.</summary>
    public class BonusActionTests
    {
        const int Seed = 12345;

        [SetUp]
        public void Reset() { WeaknessBook.Clear(); }

        [TearDown]
        public void Restore() { WeaknessBook.Clear(); }

        static CardDefinition Physical(string name)
        {
            var card = TestFactory.Card(name);
            card.attackType = AttackType.Physical;
            card.breakValue = 1;
            return card;
        }

        static EnemyDefinition Foe(string name, int breakPoints, Weakness weak)
        {
            var e = TestFactory.Enemy(name, resonance: Resonance.Basic, skills: new EnemySkill { card = TestFactory.Card("Bite"), weight = 1f });
            e.breakPoints = breakPoints;
            e.weaknesses = weak;
            return e;
        }

        static BattleController Battle(params EnemyDefinition[] enemies)
        {
            var hero = TestFactory.Character("Hero", Resonance.Basic, 100f, null, TestFactory.Card("HeroStrike"));
            var battle = new BattleController(new[] { hero }, enemies, CombatConfig.Default, Seed) { BonusActionsOn = true };
            battle.StartBattle();
            return battle;
        }

        static CardInstance[] Deal(BattleController battle, params string[] names)
        {
            var cards = new List<CardInstance>();
            foreach (var name in names) cards.Add(new CardInstance(Physical(name), battle.State.Party[0], 1));
            battle.SetHand(new List<CardInstance>(cards));
            return cards.ToArray();
        }

        [Test]
        public void Stagger_PausesForABonusCard_ThenTheRoundEnds()
        {
            var battle = Battle(Foe("Dummy", 1, Weakness.Physical));
            var cards = Deal(battle, "Breaker", "Follow");
            battle.QueueCard(cards[0]);
            battle.EndPlanning();

            Assert.AreEqual(BattlePhase.Bonus, battle.State.Phase);
            Assert.AreEqual(battle.State.Party[0], battle.BonusActor);
            Assert.AreEqual(1, battle.State.Round);

            int hp = battle.State.Enemies[0].HP;
            Assert.IsTrue(battle.PlayBonusCard(cards[1]));
            Assert.Less(battle.State.Enemies[0].HP, hp);
            Assert.AreEqual(BattlePhase.Planning, battle.State.Phase);
            Assert.AreEqual(2, battle.State.Round);
        }

        [Test]
        public void BonusStagger_DoesNotChain()
        {
            var battle = Battle(Foe("A", 1, Weakness.Physical), Foe("B", 1, Weakness.Physical));
            var cards = Deal(battle, "Breaker", "Follow", "Spare");
            battle.SelectEnemy(battle.State.Enemies[0]);
            battle.QueueCard(cards[0]);
            battle.EndPlanning();

            battle.SelectEnemy(battle.State.Enemies[1]);
            Assert.IsTrue(battle.PlayBonusCard(cards[1]));
            Assert.IsTrue(battle.State.Enemies[1].Staggered);
            Assert.AreEqual(BattlePhase.Planning, battle.State.Phase);
        }

        [Test]
        public void Skip_EndsTheRound()
        {
            var battle = Battle(Foe("Dummy", 1, Weakness.Physical));
            var cards = Deal(battle, "Breaker", "Follow");
            battle.QueueCard(cards[0]);
            battle.EndPlanning();
            battle.SkipBonus();
            Assert.AreEqual(BattlePhase.Planning, battle.State.Phase);
            Assert.AreEqual(2, battle.State.Round);
        }

        [Test]
        public void NoCardLeft_NoBonusPhase()
        {
            var battle = Battle(Foe("Dummy", 1, Weakness.Physical));
            var cards = Deal(battle, "Breaker");
            battle.QueueCard(cards[0]);
            battle.EndPlanning();
            Assert.AreEqual(BattlePhase.Planning, battle.State.Phase);
        }

        [Test]
        public void Estimate_HidesUnfoundWeaknesses_AndNeverLeaksThem()
        {
            var weak = Battle(Foe("Weak", 3, Weakness.Physical));
            var notWeak = Battle(Foe("Tough", 3, Weakness.Arc));
            var a = weak.PreviewBreak(Deal(weak, "Strike")[0])[0];
            var b = notWeak.PreviewBreak(Deal(notWeak, "Strike")[0])[0];

            Assert.IsTrue(a.Unknown);
            Assert.IsTrue(b.Unknown);
            Assert.AreEqual(3, a.After, "an unfound weakness shows no Break damage");
            Assert.AreEqual(b.After, a.After, "the same as a card that really does nothing");
        }

        [Test]
        public void Estimate_CountsFoundWeaknesses_AndQueuedCards()
        {
            var enemy = Foe("Found", 2, Weakness.Physical);
            WeaknessBook.Learn(enemy.name, Weakness.Physical);
            var battle = Battle(enemy);
            var cards = Deal(battle, "First", "Second");

            var one = battle.PreviewBreak(cards[0])[0];
            Assert.IsFalse(one.Unknown);
            Assert.AreEqual(1, one.After);
            Assert.IsFalse(one.WouldStagger);

            battle.QueueCard(cards[0]);
            var two = battle.PreviewBreak(cards[1])[0];
            Assert.AreEqual(0, two.After);
            Assert.IsTrue(two.WouldStagger);
            Assert.AreEqual(2, battle.State.Enemies[0].Break, "a preview changes nothing");
        }
    }
}
