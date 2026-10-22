using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Combat.Tests
{
    public class BattleControllerTests
    {
        const int Seed = 12345;

        CardDefinition strike, ult;
        CharacterDefinition hero;
        EnemyDefinition dummy;

        [SetUp]
        public void SetUp()
        {
            strike = TestFactory.Card("Strike");
            ult = TestFactory.Card("Ult", 5f, ritual: true);
            // A single incantation means every draw is the same card, so the hand settles into rank-3s.
            hero = TestFactory.Character("Hero", ritual: ult, incantations: new[] { strike });
            dummy = TestFactory.Enemy("Dummy");
        }

        BattleController Start(EnemyDefinition enemy = null)
        {
            var battle = new BattleController(new[] { hero }, new[] { enemy ?? dummy }, CombatConfig.Default, Seed);
            battle.StartBattle();
            return battle;
        }

        [Test]
        public void StartBattle_FillsHandToCapacity_AndEntersPlanning()
        {
            var battle = Start();

            Assert.AreEqual(BattlePhase.Planning, battle.State.Phase);
            Assert.AreEqual(CombatConfig.Default.handSize, battle.State.Hand.IncantationCount);
            Assert.AreEqual(CombatConfig.Default.actionPointsPerRound, battle.State.ActionPoints);
            Assert.IsTrue(battle.Events.OfType<CardsMergedEvent>().Any(), "same-card draws should have merged");
        }

        [Test]
        public void ApplyParty_CarriesLevelAndHP_ClampsToRange_AndSkipsUnlisted()
        {
            var sidekick = TestFactory.Character("Sidekick", incantations: new[] { strike });
            var battle = new BattleController(new[] { hero, sidekick }, new[] { dummy }, CombatConfig.Default, Seed);

            battle.ApplyParty(new Dictionary<CharacterDefinition, MemberProgress> { { hero, new MemberProgress { HP = 0 } } });
            Assert.AreEqual(1, battle.State.Party[0].HP, "knocked-out members come back with 1 HP");
            Assert.AreEqual(1000, battle.State.Party[1].HP, "unlisted members start full");

            battle.ApplyParty(new Dictionary<CharacterDefinition, MemberProgress>
            {
                { hero, new MemberProgress { Level = 3, HP = 640 } },
                { sidekick, new MemberProgress { HP = 99999 } }
            });
            Assert.AreEqual(3, battle.State.Party[0].Level);
            Assert.AreEqual(Leveling.MaxHP(hero, 3), battle.State.Party[0].MaxHP, "level scales max HP");

            var snapshot = battle.PartySnapshot(null);
            Assert.AreEqual(640, snapshot[hero].HP);
            Assert.AreEqual(-1, snapshot[sidekick].HP, "full HP is stored as -1");
        }

        [Test]
        public void BattleResult_Victory_GrantsEnemyXp_AndLevelsUp()
        {
            var rewarding = TestFactory.Enemy("Rewarding");
            rewarding.xpReward = 250;
            var battle = new BattleController(new[] { hero }, new[] { rewarding }, CombatConfig.Default, Seed);
            var start = new Dictionary<CharacterDefinition, MemberProgress> { { hero, new MemberProgress { Level = 1, Xp = 0 } } };
            battle.ApplyParty(start);

            var win = BattleResult.Build(BattleOutcome.Victory, battle, start);
            Assert.AreEqual(250, win.XpGained);
            Assert.AreEqual(2, win.Party[hero].Level, "100 XP for Lv 2, then 150 of the 200 needed for Lv 3");
            Assert.AreEqual(150, win.Party[hero].Xp);
            Assert.AreEqual(1, win.LevelsGained[hero]);
            Assert.AreEqual(1, start[hero].Level, "the request's progress isn't modified");

            var fled = BattleResult.Build(BattleOutcome.Fled, battle, start);
            Assert.AreEqual(0, fled.XpGained);
            Assert.AreEqual(1, fled.Party[hero].Level);
        }

        [Test]
        public void Merges_GrantMoxie()
        {
            var battle = Start();
            int merges = battle.Events.OfType<CardsMergedEvent>().Count();
            int expected = System.Math.Min(merges * CombatConfig.Default.moxiePerMerge, CombatConfig.Default.moxieCap);

            Assert.AreEqual(expected, battle.State.Party[0].Moxie);
        }

        [Test]
        public void QueueCard_SpendsAP_AndFailsWhenOut()
        {
            var battle = Start();
            var hand = battle.State.Hand;

            for (int i = 0; i < CombatConfig.Default.actionPointsPerRound; i++)
                Assert.IsTrue(battle.QueueCard(hand[0]));

            Assert.AreEqual(0, battle.State.ActionPoints);
            Assert.IsFalse(battle.QueueCard(hand[0]));
            Assert.AreEqual(CombatConfig.Default.actionPointsPerRound, battle.State.Queue.Count);
        }

        [Test]
        public void MoxieCap_SpawnsRitual_AndQueueingItResetsMoxie()
        {
            var battle = Start();
            var unit = battle.State.Party[0];

            battle.AddMoxie(unit, CombatConfig.Default.moxieCap);
            var ritual = battle.State.Hand.Cards.FirstOrDefault(c => c.IsRitual);

            Assert.IsNotNull(ritual);
            battle.Events.Clear();
            Assert.IsTrue(battle.QueueCard(ritual));

            // Moxie resets to 0; any merges caused by the ritual leaving the hand add back on top.
            int mergesAfter = battle.Events.OfType<CardsMergedEvent>().Count();
            Assert.AreEqual(mergesAfter * CombatConfig.Default.moxiePerMerge, unit.Moxie);
            Assert.Less(unit.Moxie, CombatConfig.Default.moxieCap);
        }

        [Test]
        public void Undo_RestoresHandQueueAndAP()
        {
            var battle = Start();
            var before = battle.State.Hand.Snapshot();
            int ap = battle.State.ActionPoints;

            battle.QueueCard(battle.State.Hand[0]);
            Assert.IsTrue(battle.Undo());

            CollectionAssert.AreEqual(before, battle.State.Hand.Cards.ToList());
            Assert.AreEqual(ap, battle.State.ActionPoints);
            Assert.AreEqual(0, battle.State.Queue.Count);
        }

        [Test]
        public void EndPlanning_ResolvesQueuedCards_AndStartsNextRound()
        {
            var battle = Start();
            var enemy = battle.State.Enemies[0];
            int hp = enemy.HP;

            battle.QueueCard(battle.State.Hand[0]);
            battle.EndPlanning();

            Assert.Less(enemy.HP, hp);
            Assert.AreEqual(2, battle.State.Round);
            Assert.AreEqual(BattlePhase.Planning, battle.State.Phase);
            Assert.IsTrue(battle.Events.OfType<CardCastEvent>().Any());
        }

        [Test]
        public void KillingLastEnemy_IsVictory()
        {
            var battle = Start(TestFactory.Enemy("Weakling", hp: 1));
            bool? result = null;
            battle.BattleEnded += v => result = v;

            battle.QueueCard(battle.State.Hand[0]);
            battle.EndPlanning();

            Assert.AreEqual(BattlePhase.Victory, battle.State.Phase);
            Assert.AreEqual(true, result);
        }

        [Test]
        public void EnemyIntent_IsChosenAndExecuted()
        {
            var bite = TestFactory.Card("Bite");
            var biter = TestFactory.Enemy("Biter", skills: new[] { new EnemySkill { card = bite, weight = 1f } });
            var battle = Start(biter);
            var unit = battle.State.Party[0];

            Assert.IsNotNull(battle.State.Enemies[0].Intent);
            battle.EndPlanning();

            Assert.Less(unit.HP, unit.MaxHP);
        }

        [Test]
        public void Statuses_TickDownAtRoundEnd()
        {
            var battle = Start();
            var unit = battle.State.Party[0];
            var buff = TestFactory.Status("Buff");

            battle.ApplyStatus(unit, unit, buff, 2, 1);
            battle.EndPlanning();
            Assert.AreEqual(1, unit.FindStatus(buff).Remaining);

            battle.EndPlanning();
            Assert.IsNull(unit.FindStatus(buff));
        }
    }
}
