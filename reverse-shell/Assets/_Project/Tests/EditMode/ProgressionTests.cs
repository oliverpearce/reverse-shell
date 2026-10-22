using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    public class ProgressionTests
    {
        CharacterDefinition hero;

        [SetUp]
        public void SetUp()
        {
            hero = TestFactory.Character("Hero");
            hero.growthPerLevel = 0.1f;
        }

        [Test]
        public void XpCurve_AndStatScale()
        {
            Assert.AreEqual(100, Leveling.XpToNext(1));
            Assert.AreEqual(300, Leveling.XpToNext(3));
            Assert.AreEqual(1f, Leveling.StatScale(1, 0.1f), 1e-5f);
            Assert.AreEqual(1.4f, Leveling.StatScale(5, 0.1f), 1e-5f);
            Assert.AreEqual(1400, Leveling.MaxHP(hero, 5));
        }

        [Test]
        public void AddXp_CanGainSeveralLevels_AndKeepsMissingHP()
        {
            var progress = new MemberProgress { Level = 1, Xp = 50, HP = 700 }; // 300 HP missing at Lv 1
            int gained = Leveling.AddXp(hero, progress, 260);                 // 310 total: Lv 2 (-100), Lv 3 (-200), 10 left

            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, progress.Level);
            Assert.AreEqual(10, progress.Xp);
            Assert.AreEqual(Leveling.MaxHP(hero, 3) - 300, progress.HP);
        }

        [Test]
        public void AddXp_FullHPStaysFull()
        {
            var progress = new MemberProgress();
            Leveling.AddXp(hero, progress, 100);
            Assert.AreEqual(2, progress.Level);
            Assert.AreEqual(-1, progress.HP);
        }

        [Test]
        public void LeveledUnit_HasScaledStats()
        {
            var unit = TestFactory.Unit(hero, Team.Player);
            unit.SetLevel(3);
            Assert.AreEqual(1200, unit.MaxHP);
            Assert.AreEqual(1200, unit.HP);
            Assert.AreEqual(120f, unit.GetStat(StatType.Attack), 1e-3f);
            Assert.AreEqual(0f, unit.GetStat(StatType.CritRate), 1e-5f, "crit doesn't grow");
        }

        [Test]
        public void SaveData_RoundTripsThroughJson()
        {
            WorldSession.Party.Clear();
            WorldSession.DefeatedEncounters.Clear();
            WorldSession.Party["Char_2B"] = new MemberProgress { Level = 4, Xp = 35, HP = 1234 };
            WorldSession.DefeatedEncounters.Add("Overworld/encounter_22_2");

            string json = UnityEngine.JsonUtility.ToJson(SaveGame.Capture());
            WorldSession.Party.Clear();
            WorldSession.DefeatedEncounters.Clear();
            SaveGame.Restore(UnityEngine.JsonUtility.FromJson<SaveGame.Data>(json));

            var p = WorldSession.Party["Char_2B"];
            Assert.AreEqual(4, p.Level);
            Assert.AreEqual(35, p.Xp);
            Assert.AreEqual(1234, p.HP);
            Assert.IsTrue(WorldSession.DefeatedEncounters.Contains("Overworld/encounter_22_2"));

            WorldSession.Party.Clear();
            WorldSession.DefeatedEncounters.Clear();
        }
    }
}
