using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Enemies' innate cards: a Basic card in their move list, played some levels up.</summary>
    public class EnemyInnateTests
    {
        static CardDefinition PoisonCard(bool ult = false)
        {
            var poison = UnityEngine.ScriptableObject.CreateInstance<StatusDefinition>();
            poison.displayName = "Poison";
            poison.maxStacks = 2;
            poison.damagePerRoundPercentMaxHP = 0.03f;
            poison.castCostPercentMaxHP = 0.05f;
            poison.lifesteal = 0.5f;
            var card = TestFactory.Card("Spore", 0.01f, ult);
            foreach (var rank in card.ranks) rank.effects.Add(new ApplyStatusEffect { status = poison, duration = 2 });
            return card;
        }

        [Test]
        public void PoisonCard_CostsHp_ThenLifesteals()
        {
            var card = PoisonCard();
            var hero = TestFactory.Character("Hero", Resonance.Basic, 1f, null, card);
            var foe = TestFactory.Enemy("Foe", hp: 50000, resonance: Resonance.Basic,
                skills: new EnemySkill { card = TestFactory.Card("Poke", 0.01f), weight = 1f });
            var battle = new BattleController(new[] { hero }, new[] { foe }, CombatConfig.Default, 7);
            battle.PartyCantFall = true;
            battle.StartBattle();
            var unit = battle.State.Party[0];
            var played = new CardInstance(card, unit, 1);
            battle.SetHand(new System.Collections.Generic.List<CardInstance> { played });
            Assert.IsTrue(battle.QueueCard(played));
            battle.EndPlanning();
            bool paid = false, healed = false;
            foreach (var e in battle.Events)
            {
                var hit = e as DamageEvent;
                if (hit != null && hit.Source == unit && hit.Target == unit && hit.Amount == 50) paid = true;
                var heal = e as HealEvent;
                if (heal != null && heal.Source == unit && heal.Target == unit && heal.Amount > 0) healed = true;
            }
            Assert.IsTrue(paid, "5% of 1000 max HP");
            Assert.IsTrue(healed, "the tick heals whoever applied it");

            unit.HP = 1;
            var again = new CardInstance(card, unit, 1);
            battle.SetHand(new System.Collections.Generic.List<CardInstance> { again });
            Assert.IsTrue(battle.QueueCard(again));
            battle.EndPlanning();
            Assert.IsTrue(unit.IsAlive, "the cost never knocks you out");
        }

        [Test]
        public void PoisonCost_ShowsOnTheCard_AndUltsAreFree()
        {
            var card = PoisonCard();
            StringAssert.Contains("Costs 5% max HP.", CardText.ForRank(card, 1));
            StringAssert.Contains("Costs 5% max HP.", CardText.AllRanks(card));
            var ult = PoisonCard(true);
            Assert.AreEqual(0f, CardLevels.HpCostShare(ult, ult.GetRank(1)));
            StringAssert.DoesNotContain("Costs", CardText.ForRank(ult, 1));
        }

        [Test]
        public void Enemy_ChargesSoulPoints_ThenUsesItsUlt()
        {
            var hero = TestFactory.Character("Hero", Resonance.Basic, 1f, null, TestFactory.Card("Strike"));
            var foe = TestFactory.Enemy("Foe", hp: 50000, resonance: Resonance.Basic,
                skills: new EnemySkill { card = TestFactory.Card("Poke", 0.01f), weight = 1f });
            foe.ritual = TestFactory.Card("Slam", 0.01f, true);
            foe.soulPointsToUlt = 2;
            var battle = new BattleController(new[] { hero }, new[] { foe }, CombatConfig.Default, 7);
            battle.StartBattle();
            var unit = battle.State.Enemies[0];
            Assert.IsFalse(unit.Intent.IsRitual);
            battle.EndPlanning();
            Assert.AreEqual(1, unit.SoulPoints, "one SP per move");
            Assert.IsFalse(unit.Intent.IsRitual);
            battle.EndPlanning();
            Assert.AreEqual(2, unit.SoulPoints);
            Assert.IsTrue(unit.Intent.IsRitual, "full SP: its Ult is the next move, shown as its intent");
            battle.EndPlanning();
            Assert.AreEqual(0, unit.SoulPoints, "using the Ult spends the SP");
        }

        [Test]
        public void EveryEnemy_HasAnInnateAndASoulCard()
        {
            var catalog = EnemyCatalog.Get();
            Assert.IsNotNull(catalog);
            foreach (var enemy in catalog.enemies)
            {
                Assert.IsNotNull(enemy.innate, enemy.name + " innate");
                Assert.IsNotNull(enemy.ritual, enemy.name + " Soul card");
            }
        }

        [Test]
        public void InnateCard_IsPicked_AndPlaysItsLevelsUp()
        {
            var strike = TestFactory.Card("Strike");
            strike.level = 4;
            var def = TestFactory.Enemy("Biter", resonance: Resonance.Basic);
            def.innate = strike;
            def.innateLevels = 3;
            def.innateWeight = 1f;
            var enemy = TestFactory.Unit(def, Team.Enemy);

            var intent = EnemyAI.ChooseIntent(enemy, new System.Random(1), 5);
            Assert.AreSame(strike, intent.Definition, "with no other skills, the innate is the move");
            Assert.AreEqual(4 + 3, CardLevels.Of(intent), "played Innate Levels higher");

            var hero = TestFactory.Unit(TestFactory.Character("Hero", Resonance.Basic, 100f, null, strike), Team.Player);
            Assert.AreEqual(4, CardLevels.Of(new CardInstance(strike, hero, 1)), "the same card for the party has no bonus");
        }
    }
}
