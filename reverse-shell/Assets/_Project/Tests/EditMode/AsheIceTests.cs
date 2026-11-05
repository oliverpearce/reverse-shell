using System.Collections.Generic;
using System.Linq;
using Combat.View;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Ashe's ice kit (AddWeaknessEffect, Frozen bonus), the tinted protagonist sprite and the idle desync.</summary>
    public class AsheIceTests
    {
        [SetUp]
        public void Reset() { WeaknessBook.Clear(); }

        [TearDown]
        public void Clean() { WeaknessBook.Clear(); }

        static CardDefinition CryoCard(string name, params CardEffect[] effects)
        {
            var card = TestFactory.Card(name);
            card.pool = Resonance.Cryo;
            card.attackType = AttackType.Incantation;
            card.breakValue = 2;
            card.ranks = new List<CardRank>();
            for (int i = 0; i < 3; i++) card.ranks.Add(new CardRank { effects = new List<CardEffect>(effects) });
            return card;
        }

        static BattleController Fight(CardDefinition card, out BattleUnit ashe, out BattleUnit enemy)
        {
            var hero = TestFactory.Character("Ashe", Resonance.Cryo, 100f, null, card);
            var e = TestFactory.Enemy("Target", resonance: Resonance.Basic, skills: new EnemySkill { card = TestFactory.Card("Poke", 0.01f), weight = 1f });
            e.breakPoints = 6;
            e.weaknesses = Weakness.Physical;   // not weak to Cryo on its own
            var battle = new BattleController(new[] { hero }, new[] { e }, CombatConfig.Default, 3);
            battle.StartBattle();
            ashe = battle.State.Party[0];
            enemy = battle.State.Enemies[0];
            return battle;
        }

        [Test]
        public void AbsoluteZero_MakesTheTargetWeakToCryo_SoTheSameHitBreaksIt()
        {
            var zero = CryoCard("Absolute Zero", new AddWeaknessEffect { weakness = Weakness.Cryo, turns = 2 }, new DamageEffect { multiplier = 1f });
            BattleUnit ashe, enemy;
            var battle = Fight(zero, out ashe, out enemy);
            Assert.AreEqual(Weakness.None, BreakRules.Matches(zero, enemy), "not weak to Cryo before");
            int before = enemy.Break;
            battle.SetHand(new List<CardInstance> { new CardInstance(zero, ashe, 1) });
            Assert.IsTrue(battle.QueueCard(battle.State.Hand.Cards[0]));
            battle.EndPlanning();
            Assert.IsTrue((enemy.Weaknesses & Weakness.Cryo) != 0, "weak to Cryo for a while");
            Assert.Less(enemy.Break, before, "its Cryo hit broke into the new weakness");
            Assert.IsTrue(battle.Events.OfType<WeaknessRevealedEvent>().Any(x => x.Unit == enemy && x.Weakness == Weakness.Cryo));
        }

        [Test]
        public void AddedWeakness_WearsOff()
        {
            BattleUnit ashe, enemy;
            var battle = Fight(TestFactory.Card("Strike"), out ashe, out enemy);
            battle.AddWeakness(enemy, Weakness.Cryo, 2);
            Assert.IsTrue((enemy.Weaknesses & Weakness.Cryo) != 0);
            battle.EndPlanning();   // round 1 ends
            Assert.IsTrue((enemy.Weaknesses & Weakness.Cryo) != 0, "2 turns: still weak next round");
            battle.EndPlanning();   // round 2 ends
            Assert.AreEqual(Weakness.None, enemy.Weaknesses & Weakness.Cryo, "worn off");
            Assert.IsTrue((enemy.Weaknesses & Weakness.Physical) != 0, "its own weakness stays");
        }

        [Test]
        public void FrozenBonus_HitsHarderOnTheFrozen()
        {
            var frozen = TestFactory.Status("Frozen");
            var storm = new DamageEffect { multiplier = 1f, bonusVs = frozen, bonusPerStack = 0.5f };
            var card = CryoCard("Leng Storm", storm);
            BattleUnit ashe, enemy;
            var battle = Fight(card, out ashe, out enemy);
            int hp0 = enemy.HP;
            new DamageEffect { multiplier = 1f }.Apply(new EffectContext(battle, ashe, new CardInstance(card, ashe, 1), new List<BattleUnit> { enemy }));
            int plain = hp0 - enemy.HP;
            battle.ApplyStatus(ashe, enemy, frozen, 2, 1);
            int hp1 = enemy.HP;
            storm.Apply(new EffectContext(battle, ashe, new CardInstance(card, ashe, 1), new List<BattleUnit> { enemy }));
            Assert.Greater(hp1 - enemy.HP, plain, "+50% on a Frozen target");
        }

        [Test]
        public void TintPixel_PaintsAtTheSameBrightness()
        {
            var yellow = new Color(1f, 0.84f, 0.18f);
            var mid = SpriteAnimationSet.TintPixel(new Color32(0x8b, 0x93, 0xaf, 255), yellow);
            Assert.Greater(mid.r, mid.b, "takes the tint's hue");
            var white = SpriteAnimationSet.TintPixel(new Color32(255, 255, 255, 200), Color.white);
            Assert.AreEqual(255, white.r);
            Assert.AreEqual(200, white.a, "alpha kept");
        }

        [Test]
        public void Runes_AreTheLettersThemselves_SoTheyTranslate()
        {
            Assert.AreEqual("frost lash", RuneText.Letters("Frost Lash"), "one SGA rune per letter, like the enchanting table");
            Assert.AreEqual(RuneText.Letters("Strike"), RuneText.Letters("strike"));
            Assert.AreEqual("cafe", RuneText.Letters("Café"), "accents dropped");
        }

        [Test]
        public void RandomIdlePhase_StaysInsideOneLoop()
        {
            var clip = new SpriteAnimationClip { fps = 5f, loop = true };
            for (int i = 0; i < 50; i++)
            {
                float t = UISpriteAnimator.RandomTime(clip, 6);
                Assert.GreaterOrEqual(t, 0f);
                Assert.LessOrEqual(t, 6f / 5f);
            }
            Assert.AreEqual(0f, UISpriteAnimator.RandomTime(clip, 1));
        }
    }
}
