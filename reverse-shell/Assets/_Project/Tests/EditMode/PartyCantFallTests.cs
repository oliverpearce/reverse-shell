using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>BattleController.PartyCantFall (the Collector fight): party hits leave at least 1 HP; enemies still fall.</summary>
    public class PartyCantFallTests
    {
        const int Seed = 12345;

        BattleController Start(bool cantFall)
        {
            var hero = TestFactory.Character("Hero", incantations: new[] { TestFactory.Card("Strike") });
            var battle = new BattleController(new[] { hero }, new[] { TestFactory.Enemy("Brute") }, CombatConfig.Default, Seed);
            battle.PartyCantFall = cantFall;
            battle.StartBattle();
            return battle;
        }

        [Test]
        public void ProtectedMember_HoldsOnAtOneHp()
        {
            var battle = Start(true);
            var hero = battle.State.Party[0];
            var enemy = battle.State.Enemies[0];
            hero.HP = 5;
            battle.DealDamage(enemy, hero, 999f, false);
            Assert.AreEqual(1, hero.HP);
            Assert.IsTrue(hero.IsAlive);
            battle.DealDamage(enemy, hero, 999f, false);
            Assert.AreEqual(1, hero.HP, "still 1 after another big hit");
        }

        [Test]
        public void WithoutTheRule_TheMemberFalls()
        {
            var battle = Start(false);
            var hero = battle.State.Party[0];
            hero.HP = 5;
            battle.DealDamage(battle.State.Enemies[0], hero, 999f, false);
            Assert.AreEqual(0, hero.HP);
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void Enemies_StillFall()
        {
            var battle = Start(true);
            var enemy = battle.State.Enemies[0];
            enemy.HP = 5;
            battle.DealDamage(battle.State.Party[0], enemy, 999f, false);
            Assert.AreEqual(0, enemy.HP);
        }
    }
}
