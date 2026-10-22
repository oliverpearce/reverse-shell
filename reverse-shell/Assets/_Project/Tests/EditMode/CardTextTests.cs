using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public class CardTextTests
    {
        static CardDefinition VirusSpike(out StatusDefinition poison)
        {
            poison = ScriptableObject.CreateInstance<StatusDefinition>();
            poison.displayName = "Poison";
            poison.maxStacks = 3;
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.displayName = "Virus Spike";
            card.ranks = new List<CardRank>
            {
                new CardRank { effects = new List<CardEffect> { new DamageEffect { multiplier = 1.2f }, new ApplyStatusEffect { status = poison, stacks = 1, duration = 2 } } },
                new CardRank { effects = new List<CardEffect> { new DamageEffect { multiplier = 1.8f }, new ApplyStatusEffect { status = poison, stacks = 2, duration = 2 } } },
                new CardRank { effects = new List<CardEffect> { new DamageEffect { multiplier = 2.8f }, new ApplyStatusEffect { status = poison, stacks = 3, duration = 3 } } },
            };
            return card;
        }

        [Test]
        public void AllRanks_MergesChangingValues()
        {
            StatusDefinition poison;
            var text = CardText.AllRanks(VirusSpike(out poison));

            StringAssert.Contains("Deal (120% I / 180% II / 280% III) ATK damage and give 1 enemy Poison", text);
            StringAssert.Contains("(x1 I / x2 II / x3 III)", text);
            StringAssert.Contains("(2 I / 2 II / 3 III) turns", text);
        }

        [Test]
        public void AllRanks_KeepsUnchangedValuesPlain_AndLabelsRankOnlyEffects()
        {
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ranks = new List<CardRank>
            {
                new CardRank { effects = new List<CardEffect> { new HealEffect { multiplier = 1f } } },
                new CardRank { effects = new List<CardEffect> { new HealEffect { multiplier = 1f } } },
                new CardRank { effects = new List<CardEffect> { new HealEffect { multiplier = 1f }, new GainMoxieEffect { amount = 1 } } },
            };

            var text = CardText.AllRanks(card);

            StringAssert.Contains("for 100% of ATK", text);
            StringAssert.Contains("III only: Self gains 1 Ult.", text);
        }

        [Test]
        public void ForRank_DescribesThatRankOnly()
        {
            StatusDefinition poison;
            var card = VirusSpike(out poison);
            Assert.AreEqual("Deal 180% ATK damage and give 1 enemy Poison (x2, 2 turns).", CardText.ForRank(card, 2));
        }

        [Test]
        public void SameTargetEffects_ShareOneSentence_UsingTheStatusVerb()
        {
            StatusDefinition poison;
            var card = VirusSpike(out poison);
            poison.verb = "poison";
            Assert.AreEqual("Deal 120% ATK damage and poison 1 enemy (x1, 2 turns).", CardText.ForRank(card, 1));
        }

        [Test]
        public void DifferentTargets_StaySeparateSentences()
        {
            var buff = ScriptableObject.CreateInstance<StatusDefinition>();
            buff.displayName = "ATK Up";
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ranks = new List<CardRank>
            {
                new CardRank { effects = new List<CardEffect>
                {
                    new DamageEffect { multiplier = 6f, hits = 2 },
                    new ApplyStatusEffect { target = EffectTarget.AllAllies, status = buff, duration = 2 }
                } },
            };
            Assert.AreEqual("Deal 600% ATK damage to 1 enemy over 2 hits. Give all allies ATK Up (2 turns).", CardText.ForRank(card, 1));
        }
    }
}
