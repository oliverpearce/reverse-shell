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

            StringAssert.Contains("Deal (120% I / 180% II / 280% III) ATK damage to 1 enemy.", text);
            StringAssert.Contains("Give 1 enemy Poison.", text);
        }

        [Test]
        public void Text_HasNoCommas_AndNoTurnCounts()
        {
            StatusDefinition poison;
            var card = VirusSpike(out poison);
            for (int rank = 1; rank <= 3; rank++)
            {
                string text = CardText.ForRank(card, rank);
                StringAssert.DoesNotContain(",", text);
                StringAssert.DoesNotContain("turns", text);
            }
        }

        [Test]
        public void SameEffect_ReadsTheSameAtEveryRank()
        {
            // Sap Mend: rank III adds Regeneration, but the heal keeps its wording.
            var regen = ScriptableObject.CreateInstance<StatusDefinition>();
            regen.displayName = "Regeneration";
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.targetMode = TargetMode.SingleAlly;
            card.ranks = new List<CardRank>
            {
                new CardRank { effects = new List<CardEffect> { new HealEffect { target = EffectTarget.CardTargets, multiplier = 1.4f } } },
                new CardRank { effects = new List<CardEffect>
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 3.2f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = regen, duration = 2 }
                } },
            };
            Assert.AreEqual("Heal the weakest ally for 140% of ATK.", CardText.ForRank(card, 1));
            Assert.AreEqual("Heal the weakest ally for 320% of ATK. Give the weakest ally Regeneration.", CardText.ForRank(card, 2));
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
            Assert.AreEqual("Deal 180% ATK damage to 1 enemy. Give 1 enemy Poison.", CardText.ForRank(card, 2));
        }

        [Test]
        public void StatusWithAVerb_UsesIt()
        {
            StatusDefinition poison;
            var card = VirusSpike(out poison);
            poison.verb = "poison";
            Assert.AreEqual("Deal 120% ATK damage to 1 enemy. Poison 1 enemy.", CardText.ForRank(card, 1));
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
            Assert.AreEqual("Deal 600% ATK damage to 1 enemy over 2 hits. Give all allies ATK Up.", CardText.ForRank(card, 1));
        }

        [Test]
        public void EverySentence_StartsWithACapital()
        {
            Assert.AreEqual("All allies gain Shield. Deal 1.5x damage.", CardText.Capitalized("all allies gain Shield. deal 1.5x damage."));
            Assert.AreEqual("II/III only: Give Poison.\nIII: <color=#FFD700>Heal</color> 20%.",
                CardText.Capitalized("II/III only: give Poison.\nIII: <color=#FFD700>heal</color> 20%."));
            Assert.AreEqual("120% ATK. {gold}Then{/c} more.", CardText.Capitalized("120% ATK. {gold}then{/c} more."));

            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ranks = new List<CardRank> { new CardRank { description = "self heals a little." } };
            Assert.AreEqual("Self heals a little.", CardText.ForRank(card, 1), "hand-written descriptions too");
        }
    }
}
