using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Shields, counters, cover, Burn, Chill / Frozen, interrupts and the shared power budget (bible v0.6).</summary>
    public class GuardMechanicsTests
    {
        const int Seed = 12345;

        StatusDefinition Special(string name, StatusSpecial special, StackRule rule = StackRule.Replace, int max = 1)
        {
            var s = TestFactory.Status(name);
            s.special = special;
            s.stackRule = rule;
            s.maxStacks = max;
            return s;
        }

        BattleController Battle(params CharacterDefinition[] party)
        {
            // Physical enemy: neutral and no Prism crit bonus, so the numbers are exact.
            var enemy = TestFactory.Enemy("Dummy", resonance: Resonance.Physical);
            var battle = new BattleController(party, new[] { enemy }, CombatConfig.Default, Seed);
            battle.StartBattle();
            return battle;
        }

        CharacterDefinition Hero(string name = "Hero")
        {
            return TestFactory.Character(name, Resonance.Physical, 100f, null, TestFactory.Card(name + "Strike"));
        }

        [Test]
        public void Shield_SoaksCardDamageBeforeHP_ThenBreaks()
        {
            var battle = Battle(Hero());
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            var shield = Special("Shield", StatusSpecial.Shield);

            battle.GiveShield(hero, hero, shield, 60, 1);
            battle.DealDamage(enemy, hero, 1f, false);   // 100 damage

            Assert.AreEqual(1000 - 40, hero.HP, "60 soaked, 40 through");
            Assert.IsNull(hero.FindStatus(shield), "a used-up shield is gone");
            var hit = battle.Events.OfType<DamageEvent>().Last();
            Assert.AreEqual(60, hit.Blocked);
            Assert.AreEqual(40, hit.Amount);
        }

        [Test]
        public void Counter_HitsBackOncePerTrigger_AndHarderBelowHalfHP()
        {
            var battle = Battle(Hero());
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            var counter = Special("Counter", StatusSpecial.Counter, StackRule.AddStacks, 9);
            int start = enemy.HP;

            battle.GiveCounter(hero, hero, counter, 0.5f, 1, 1f, null, 2, 1);
            battle.DealDamage(enemy, hero, 1f, false);
            Assert.AreEqual(start - 50, enemy.HP, "hits back for 0.5 x ATK");
            Assert.IsNull(hero.FindStatus(counter), "one trigger, then spent");

            battle.DealDamage(enemy, hero, 1f, false);
            Assert.AreEqual(start - 50, enemy.HP, "no counter left");

            hero.HP = 300;   // below half
            battle.GiveCounter(hero, hero, counter, 0.5f, 1, 1f, null, 2, 1);
            battle.DealDamage(enemy, hero, 0.1f, false);
            Assert.AreEqual(start - 50 - 100, enemy.HP, "doubled below half HP");
        }

        [Test]
        public void Counter_GivesItsStatusToTheAttacker()
        {
            var battle = Battle(Hero());
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            var counter = Special("Counter", StatusSpecial.Counter, StackRule.AddStacks, 9);
            var atkDown = TestFactory.Status("ATK Down");

            battle.GiveCounter(hero, hero, counter, 0.5f, 1, 0f, atkDown, 2, 1);
            battle.DealDamage(enemy, hero, 1f, false);
            Assert.IsNotNull(enemy.FindStatus(atkDown));
        }

        [Test]
        public void Cover_SendsEnemyHitsToTheCoverer()
        {
            var battle = Battle(Hero("A"), Hero("B"));
            var a = battle.State.Party[0];
            var b = battle.State.Party[1];
            var enemy = battle.State.Enemies[0];
            var covered = Special("Covered", StatusSpecial.Cover, StackRule.RefreshDuration);

            battle.ApplyStatus(b, a, covered, 1, 1);   // B covers A
            battle.DealDamage(enemy, a, 1f, false);

            Assert.AreEqual(1000, a.HP);
            Assert.AreEqual(900, b.HP);
        }

        [Test]
        public void Chill_AtMaxStacks_BecomesFrozen()
        {
            var battle = Battle(Hero());
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            var frozen = TestFactory.Status("Frozen");
            frozen.preventsAction = true;
            frozen.maxStacks = 1;
            var chill = TestFactory.Status("Chill");
            chill.stackRule = StackRule.AddStacks;
            chill.maxStacks = 3;
            chill.becomesAtMaxStacks = frozen;

            battle.ApplyStatus(hero, enemy, chill, 2, 2);
            Assert.IsFalse(enemy.IsStunned);
            battle.ApplyStatus(hero, enemy, chill, 2, 1);
            Assert.IsNull(enemy.FindStatus(chill), "the Chill turns into Frozen");
            Assert.IsTrue(enemy.IsStunned, "Frozen stops it acting");
        }

        [Test]
        public void Burn_TicksForTheSourcesATK_PerStack()
        {
            var battle = Battle(Hero());
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            var burn = TestFactory.Status("Burn");
            burn.stackRule = StackRule.AddStacks;
            burn.maxStacks = 5;
            burn.damagePerRoundSourceATK = 0.12f;
            int start = enemy.HP;

            battle.ApplyStatus(hero, enemy, burn, 2, 2);
            battle.EndPlanning();   // nothing queued: straight to round end

            Assert.AreEqual(start - 24, enemy.HP, "2 stacks x 12% of 100 ATK");
        }

        [Test]
        public void Interrupt_CancelsTheIntent()
        {
            var hit = TestFactory.Card("Bite");
            var enemyDef = TestFactory.Enemy("Biter", resonance: Resonance.Physical, skills: new EnemySkill { card = hit, weight = 1f });
            var battle = new BattleController(new[] { Hero() }, new[] { enemyDef }, CombatConfig.Default, Seed);
            battle.StartBattle();
            var enemy = battle.State.Enemies[0];
            Assert.IsNotNull(enemy.Intent);

            var stop = new InterruptEffect { target = EffectTarget.SingleEnemy, attacksOnly = true };
            var card = new CardInstance(TestFactory.Card("Jam"), battle.State.Party[0], 1);
            stop.Apply(new EffectContext(battle, battle.State.Party[0], card, new List<BattleUnit> { enemy }));

            Assert.IsNull(enemy.Intent);
            Assert.IsTrue(battle.Events.OfType<IntentChosenEvent>().Any(e => e.Unit == enemy && e.Intent == null));
        }

        [Test]
        public void PowerBudget_IsSharedAcrossHitsHealsShieldsAndCounters()
        {
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.level = 8;   // power 2.0 x ATK
            var hit = new DamageEffect { multiplier = 0.6f };
            var heal = new HealEffect { multiplier = 1f, target = EffectTarget.Self };
            card.ranks = new List<CardRank> { new CardRank { effects = new List<CardEffect> { hit, heal } } };
            var instance = new CardInstance(card, null, 1);

            Assert.AreEqual(2f, CardLevels.Of(instance, heal), 1e-4f, "the biggest gets the full power");
            Assert.AreEqual(1.2f, CardLevels.Of(instance, hit), 1e-4f, "the hit is 60% of it");
        }

        [Test]
        public void DamageBonus_PerStackOfAStatus_AndConsume()
        {
            var battle = Battle(Hero());
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            var poison = TestFactory.Status("Poison");
            poison.stackRule = StackRule.AddStacks;
            battle.ApplyStatus(hero, enemy, poison, 3, 2);

            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.level = 4;   // power 1.0 x ATK = 100
            var burst = new DamageEffect { multiplier = 1f, bonusVs = poison, bonusPerStack = 0.25f, consumeBonus = true };
            card.ranks = new List<CardRank> { new CardRank { effects = new List<CardEffect> { burst } } };
            int start = enemy.HP;
            burst.Apply(new EffectContext(battle, hero, new CardInstance(card, hero, 1), new List<BattleUnit> { enemy }));

            Assert.AreEqual(start - 150, enemy.HP, "+25% per stack x 2");
            Assert.IsNull(enemy.FindStatus(poison), "consumed");
        }
    }
}
