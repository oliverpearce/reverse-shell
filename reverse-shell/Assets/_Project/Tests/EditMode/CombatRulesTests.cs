using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public class CombatRulesTests
    {
        [TestCase("Volt", Resonance.Arc)]
        [TestCase("None", Resonance.Basic)]
        [TestCase("Void", Resonance.Basic)]
        [TestCase("Cryo", Resonance.Cryo)]
        public void Resonance_ReadsOldAndNewSaveNames(string saved, Resonance expected)
        {
            Resonance parsed;
            Assert.IsTrue(ResonanceInfo.TryParse(saved, out parsed));
            Assert.AreEqual(expected, parsed);
        }

        [Test]
        public void Damage_BaseFormula_NoDefense()
        {
            var atk = TestFactory.Unit(TestFactory.Character("A", Resonance.Arc, 100f), Team.Player);
            var def = TestFactory.Unit(TestFactory.Enemy("E", resonance: Resonance.Prism), Team.Enemy);

            var r = DamageCalculator.Compute(atk, def, 1.5f, false, CombatConfig.Default, null);

            Assert.AreEqual(150, r.Amount);
            Assert.IsFalse(r.Crit);
        }

        [Test]
        public void Damage_NoResonanceWheel_StaggerAddsTheBonus()
        {
            var cfg = CombatConfig.Default;
            var star = TestFactory.Unit(TestFactory.Character("Star", Resonance.Arc, 100f), Team.Player);
            var mineral = TestFactory.Unit(TestFactory.Enemy("Mineral", resonance: Resonance.Cinder), Team.Enemy);
            var plant = TestFactory.Unit(TestFactory.Enemy("Plant", resonance: Resonance.Root), Team.Enemy);

            Assert.AreEqual(100, DamageCalculator.Compute(star, mineral, 1f, false, cfg, null).Amount, "Arc gets nothing extra on Cinder");
            Assert.AreEqual(100, DamageCalculator.Compute(star, plant, 1f, false, cfg, null).Amount, "nor less on Root");
            plant.StaggerTurns = 1;
            Assert.AreEqual(Mathf.RoundToInt(100 * BreakRules.StaggerMultiplier), DamageCalculator.Compute(star, plant, 1f, false, cfg, null).Amount);
        }

        [Test]
        public void Damage_DefenseMitigates()
        {
            var cfg = CombatConfig.Default;
            var atk = TestFactory.Unit(TestFactory.Character("A", Resonance.Arc, 100f), Team.Player);
            var tank = TestFactory.Unit(TestFactory.Enemy("Tank", resonance: Resonance.Prism, defense: cfg.defenseScale), Team.Enemy);

            Assert.AreEqual(50, DamageCalculator.Compute(atk, tank, 1f, false, cfg, null).Amount);
        }

        [Test]
        public void Damage_IncantationMightOnlyBoostsIncantations()
        {
            var cfg = CombatConfig.Default;
            var atk = TestFactory.Unit(TestFactory.Character("A", Resonance.Arc, 100f), Team.Player);
            var def = TestFactory.Unit(TestFactory.Enemy("E", resonance: Resonance.Prism), Team.Enemy);
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
