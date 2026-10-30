using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Break and Stagger (bible v0.8 §5, §13): weakness hits, rank scaling, Stagger's bonus window and the
    /// end-of-turn order.</summary>
    public class BreakTests
    {
        const int Seed = 12345;

        [SetUp]
        public void Reset()
        {
            WeaknessBook.Clear();
        }

        [TearDown]
        public void Restore()
        {
            WeaknessBook.AlwaysKnown = Weakness.None;
            WeaknessBook.Clear();
        }

        static CardDefinition Physical(string name, int breakValue = 1)
        {
            var card = TestFactory.Card(name);
            card.attackType = AttackType.Physical;
            card.breakValue = breakValue;
            return card;
        }

        static BattleController Battle(int breakPoints, Weakness weak, out BattleUnit hero, out BattleUnit enemy)
        {
            // Basic everything and no crits, so the numbers are exact; the enemy has a move so it shows an intent.
            var e = TestFactory.Enemy("Dummy", resonance: Resonance.Basic, skills: new EnemySkill { card = TestFactory.Card("Bite"), weight = 1f });
            e.breakPoints = breakPoints;
            e.weaknesses = weak;
            var h = TestFactory.Character("Hero", Resonance.Basic, 100f, null, TestFactory.Card("HeroStrike"));
            var battle = new BattleController(new[] { h }, new[] { e }, CombatConfig.Default, Seed);
            battle.StartBattle();
            hero = battle.State.Party[0];
            enemy = battle.State.Enemies[0];
            return battle;
        }

        static void Hit(BattleController battle, BattleUnit hero, BattleUnit enemy, CardDefinition card, int rank = 1)
        {
            var instance = new CardInstance(card, hero, rank);
            var ctx = new EffectContext(battle, hero, instance, new List<BattleUnit> { enemy });
            foreach (var effect in instance.RankData.effects) effect.Apply(ctx);
        }

        [Test]
        public void OnlyWeaknessHits_DealBreak()
        {
            BattleUnit hero, enemy;
            var battle = Battle(3, Weakness.Physical, out hero, out enemy);

            Hit(battle, hero, enemy, TestFactory.Card("Spell"));   // Incantation, Basic pool: not a weakness
            Assert.AreEqual(3, enemy.Break);

            Hit(battle, hero, enemy, Physical("Strike"));
            Assert.AreEqual(2, enemy.Break);
        }

        [Test]
        public void ResonanceWeakness_CountsTheCardsPool()
        {
            BattleUnit hero, enemy;
            var battle = Battle(3, Weakness.Arc, out hero, out enemy);
            var bolt = TestFactory.Card("Bolt");
            bolt.pool = Resonance.Arc;

            Hit(battle, hero, enemy, Physical("Strike"));
            Assert.AreEqual(3, enemy.Break, "not weak to Physical");
            Hit(battle, hero, enemy, bolt);
            Assert.AreEqual(2, enemy.Break);
        }

        [Test]
        public void Merges_MultiplyTheBreakValue()
        {
            var rates = PackRates.Current;
            var card = Physical("Strike", 2);
            Assert.AreEqual(2, BreakRules.Value(card, 1));
            Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(2 * rates.breakRankII)), BreakRules.Value(card, 2));
            Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(2 * rates.breakRankIII)), BreakRules.Value(card, 3));
        }

        [Test]
        public void MultiHitCard_BreaksOncePerTarget()
        {
            BattleUnit hero, enemy;
            var battle = Battle(5, Weakness.Physical, out hero, out enemy);
            var flurry = Physical("Flurry");
            ((DamageEffect)flurry.ranks[0].effects[0]).hits = 3;

            Hit(battle, hero, enemy, flurry);
            Assert.AreEqual(4, enemy.Break);
        }

        [Test]
        public void Stagger_CancelsIntent_AndOnlyLaterHitsGetTheBonus()
        {
            BattleUnit hero, enemy;
            var battle = Battle(2, Weakness.Physical, out hero, out enemy);
            Assert.IsNotNull(enemy.Intent);
            int start = enemy.HP;
            var strike = Physical("Strike");

            Hit(battle, hero, enemy, strike);   // 100, Break 1
            Hit(battle, hero, enemy, strike);   // 100, Break 0: this hit breaks it, so no bonus
            Assert.IsTrue(enemy.Staggered);
            Assert.IsNull(enemy.Intent, "Stagger cancels the intent");
            Assert.AreEqual(start - 200, enemy.HP);
            Assert.IsTrue(battle.Events.OfType<StaggeredEvent>().Any(e => e.Unit == enemy));

            Hit(battle, hero, enemy, strike);
            int bonus = Mathf.RoundToInt(100 * BreakRules.StaggerMultiplier);
            Assert.AreEqual(start - 200 - bonus, enemy.HP, "+50% while Staggered");
            Assert.IsTrue(battle.Events.OfType<DamageEvent>().Last().StaggerBonus);
        }

        [Test]
        public void DoT_GetsTheBonus_StaggerLastsItsTurns_ThenBreakRefills()
        {
            BattleUnit hero, enemy;
            var battle = Battle(1, Weakness.Physical, out hero, out enemy);
            var burn = TestFactory.Status("Burn");
            burn.isDebuff = true;
            burn.damagePerRoundSourceATK = 0.12f;

            Hit(battle, hero, enemy, Physical("Strike"));
            Assert.IsTrue(enemy.Staggered);
            battle.ApplyStatus(hero, enemy, burn, 2, 1);
            int before = enemy.HP;

            battle.EndPlanning();   // nothing queued; the enemy has no move left; round end; next round

            Assert.AreEqual(before - Mathf.RoundToInt(12 * BreakRules.StaggerMultiplier), enemy.HP, "Burn ticks with the Stagger bonus");
            int turns = Mathf.Max(1, PackRates.Current.staggerTurns);
            for (int round = 2; round <= turns; round++)
            {
                Assert.IsTrue(enemy.Staggered, "still Staggered in round " + round);
                Assert.IsNull(enemy.Intent, "a Staggered enemy has no move");
                Assert.AreEqual(0, enemy.Break);
                battle.EndPlanning();
            }
            Assert.IsFalse(enemy.Staggered, "Stagger wears off after its turns");
            Assert.AreEqual(enemy.MaxBreak, enemy.Break, "then the Break value refills");
            Assert.IsNotNull(enemy.Intent, "and it acts again");
        }

        [Test]
        public void WeaknessHit_RevealsIt()
        {
            WeaknessBook.AlwaysKnown = Weakness.None;
            BattleUnit hero, enemy;
            var battle = Battle(3, Weakness.Physical | Weakness.Cryo, out hero, out enemy);
            Assert.AreEqual(Weakness.None, WeaknessBook.Known(enemy.Enemy));

            Hit(battle, hero, enemy, Physical("Strike"));

            Assert.AreEqual(Weakness.Physical, WeaknessBook.Known(enemy.Enemy), "Cryo is still unknown");
            Assert.IsTrue(battle.Events.OfType<WeaknessRevealedEvent>().Any(e => e.Weakness == Weakness.Physical));
        }

        [Test]
        public void PartyMembers_CantBeBroken()
        {
            BattleUnit hero, enemy;
            Battle(3, Weakness.Physical, out hero, out enemy);
            Assert.IsFalse(hero.Breakable);
            Assert.AreEqual(Weakness.None, hero.Weaknesses);
        }
    }
}
