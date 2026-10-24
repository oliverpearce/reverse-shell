using System.Collections.Generic;
using System.Linq;
using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    public class FormationTests
    {
        List<CharacterDefinition> roster;

        [SetUp]
        public void SetUp()
        {
            roster = new List<CharacterDefinition>
            {
                TestFactory.Character("A"), TestFactory.Character("B"), TestFactory.Character("C"), TestFactory.Character("D"),
            };
            WorldSession.Members.Clear();
            WorldSession.PartyOrder.Clear();
            for (int i = 0; i < WorldSession.FormationSlots; i++) WorldSession.Formation[i] = "";
        }

        [TearDown]
        public void TearDown()
        {
            WorldSession.Members.Clear();
            for (int i = 0; i < WorldSession.FormationSlots; i++) WorldSession.Formation[i] = "";
        }

        [Test]
        public void EmptyFormation_FillsThreeActiveThenBackup()
        {
            Assert.AreEqual(new[] { "A", "B", "C" }, WorldSession.FormationActive(roster).Select(m => m.name).ToArray());
            Assert.AreEqual("D", WorldSession.FormationBackup(roster).name);
        }

        [Test]
        public void Assign_SwapsWithWhereverTheMemberWas()
        {
            WorldSession.EnsureFormation(roster);
            WorldSession.Assign(0, roster[3], roster);   // D (the backup) leads; A goes to the backup slot
            Assert.AreEqual("D", WorldSession.Formation[0]);
            Assert.AreEqual("A", WorldSession.Formation[WorldSession.BackupSlot]);
        }

        [Test]
        public void ClearSlot_NeverLeavesNoActiveMember()
        {
            WorldSession.Members.Add("A");
            WorldSession.EnsureFormation(roster);         // only A has joined
            Assert.IsFalse(WorldSession.ClearSlot(0, roster), "the last active member can't be removed");
            Assert.AreEqual("A", WorldSession.Formation[0]);
        }

        [Test]
        public void Recruits_FillEmptySlots()
        {
            WorldSession.Members.Add("A");
            WorldSession.EnsureFormation(roster);
            WorldSession.RecruitAll(roster);
            Assert.AreEqual(3, WorldSession.FormationActive(roster).Count);
            Assert.AreEqual("D", WorldSession.FormationBackup(roster).name);
        }

        [Test]
        public void EnemyLevel_ScalesFromLevelFive_AndBossHasTripleHP()
        {
            var enemy = TestFactory.Enemy("E", hp: 1000);
            var unit = new BattleUnit(enemy, Team.Enemy);
            Assert.AreEqual(BattleUnit.DefaultEnemyLevel, unit.Level);
            Assert.AreEqual(1000, unit.MaxHP, "Lv 5 = the definition's stats");
            unit.SetEnemyLevel(20, false);
            Assert.Greater(unit.MaxHP, 1000);
            int lv20 = unit.MaxHP;
            unit.SetEnemyLevel(20, true);
            Assert.AreEqual(lv20 * 3, unit.MaxHP, 1);
            unit.SetEnemyLevel(150, false);
            Assert.AreEqual(BattleUnit.MaxEnemyLevel, unit.Level, "capped at 99");
        }

        [Test]
        public void QueuedCard_KeepsTheTargetChosenWhenItWasPlayed()
        {
            var strike = TestFactory.Card("Strike");
            var hero = TestFactory.Character("Hero", incantations: new[] { strike });
            var a = TestFactory.Enemy("EnemyA");
            var b = TestFactory.Enemy("EnemyB");
            var battle = new BattleController(new[] { hero }, new[] { a, b }, CombatConfig.Default, 7);
            battle.StartBattle();
            var second = battle.State.Enemies[1];
            battle.SelectEnemy(second);
            var card = battle.State.Hand[0];
            Assert.IsTrue(battle.QueueCard(card));
            Assert.AreSame(second, card.Target);
            battle.SelectEnemy(battle.State.Enemies[0]);   // moving the cursor later doesn't change it
            Assert.AreSame(second, card.Target);
        }
    }
}
