using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public class CombatRulesTests
    {
        [TestCase(Afflatus.Volt, Afflatus.Fire)]
        [TestCase(Afflatus.Fire, Afflatus.Toxin)]
        [TestCase(Afflatus.Toxin, Afflatus.Earth)]
        [TestCase(Afflatus.Earth, Afflatus.Volt)]
        [TestCase(Afflatus.Light, Afflatus.Void)]
        [TestCase(Afflatus.Void, Afflatus.Light)]
        public void Afflatus_Advantage(Afflatus attacker, Afflatus defender)
        {
            Assert.AreEqual(AfflatusRelation.Advantage, AfflatusChart.Relation(attacker, defender));
        }

        [Test]
        public void Afflatus_ReverseIsDisadvantage_AndUnrelatedIsNeutral()
        {
            Assert.AreEqual(AfflatusRelation.Disadvantage, AfflatusChart.Relation(Afflatus.Fire, Afflatus.Volt));
            Assert.AreEqual(AfflatusRelation.Neutral, AfflatusChart.Relation(Afflatus.Volt, Afflatus.Toxin));
            Assert.AreEqual(AfflatusRelation.Neutral, AfflatusChart.Relation(Afflatus.Volt, Afflatus.Light));
        }

        [Test]
        public void Damage_BaseFormula_NoDefense()
        {
            var atk = TestFactory.Unit(TestFactory.Character("A", Afflatus.Volt, 100f), Team.Player);
            var def = TestFactory.Unit(TestFactory.Enemy("E", afflatus: Afflatus.Light), Team.Enemy);

            var r = DamageCalculator.Compute(atk, def, 1.5f, false, CombatConfig.Default, null);

            Assert.AreEqual(150, r.Amount);
            Assert.IsFalse(r.Crit);
        }

        [Test]
        public void Damage_AfflatusAdvantage_DealsMore_DisadvantageLess()
        {
            var cfg = CombatConfig.Default;
            var star = TestFactory.Unit(TestFactory.Character("Star", Afflatus.Volt, 100f), Team.Player);
            var mineral = TestFactory.Unit(TestFactory.Enemy("Mineral", afflatus: Afflatus.Fire), Team.Enemy);
            var plant = TestFactory.Unit(TestFactory.Enemy("Plant", afflatus: Afflatus.Earth), Team.Enemy);

            Assert.AreEqual(Mathf.RoundToInt(100 * cfg.advantageDamage), DamageCalculator.Compute(star, mineral, 1f, false, cfg, null).Amount);
            Assert.AreEqual(Mathf.RoundToInt(100 * cfg.disadvantageDamage), DamageCalculator.Compute(star, plant, 1f, false, cfg, null).Amount);
        }

        [Test]
        public void Damage_DefenseMitigates()
        {
            var cfg = CombatConfig.Default;
            var atk = TestFactory.Unit(TestFactory.Character("A", Afflatus.Volt, 100f), Team.Player);
            var tank = TestFactory.Unit(TestFactory.Enemy("Tank", afflatus: Afflatus.Light, defense: cfg.defenseScale), Team.Enemy);

            Assert.AreEqual(50, DamageCalculator.Compute(atk, tank, 1f, false, cfg, null).Amount);
        }

        [Test]
        public void Damage_IncantationMightOnlyBoostsIncantations()
        {
            var cfg = CombatConfig.Default;
            var atk = TestFactory.Unit(TestFactory.Character("A", Afflatus.Volt, 100f), Team.Player);
            var def = TestFactory.Unit(TestFactory.Enemy("E", afflatus: Afflatus.Light), Team.Enemy);
            var might = TestFactory.Status("Might", new StatModifier(StatType.IncantationMight, ModifierKind.Flat, 0.2f));
            atk.Statuses.Add(new StatusInstance(might, atk, 2, 1));

            Assert.AreEqual(120, DamageCalculator.Compute(atk, def, 1f, false, cfg, null).Amount);
            Assert.AreEqual(100, DamageCalculator.Compute(atk, def, 1f, true, cfg, null).Amount);
        }

        [Test]
        public void Stat_PercentModifiersScaleWithStacks()
        {
            var unit = TestFactory.Unit(TestFactory.Character("A", attack: 100f), Team.Player);
            var atkUp = TestFactory.Status("AtkUp", new StatModifier(StatType.Attack, ModifierKind.Percent, 0.1f));
            unit.Statuses.Add(new StatusInstance(atkUp, unit, 2, 3));

            Assert.AreEqual(130f, unit.GetStat(StatType.Attack), 0.001f);
        }
    }
}
