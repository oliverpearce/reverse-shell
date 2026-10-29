using System.Collections.Generic;
using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>Chapter packs (the card sheet's Packs tab): the resonance / Basic split and the rarity tiers.</summary>
    public class PackKindTests
    {
        CardDefinition Card(string name, Resonance pool, CardRarity rarity)
        {
            var c = TestFactory.Card(name);
            c.pool = pool;
            c.rarity = rarity;
            return c;
        }

        List<CharacterDefinition> Roster(params CardDefinition[] cards)
        {
            return new List<CharacterDefinition> { TestFactory.Character("Hero", Resonance.Basic, 100f, null, cards) };
        }

        static PackKind Kind(Resonance pool, float share, float common, float rare, float epic)
        {
            return new PackKind { id = "Test", pool = pool, cardsPerPack = 3, poolShare = share, commonShare = common,
                rareShare = rare, epicShare = epic, legendaryShare = 0f, soulChance = 0f, arcanaChance = 0f, blankChance = 0f };
        }

        [Test]
        public void Legendary_OnlyComesWhenRolled_NotAsAFallback()
        {
            var common = Card("Common", Resonance.Cinder, CardRarity.Common);
            var legendary = Card("Legendary", Resonance.Cinder, CardRarity.Legendary);
            var roster = Roster(common, legendary);
            var rng = new System.Random(5);
            for (int i = 0; i < 50; i++)
                foreach (var pull in CardPacks.Roll(roster, rng, Kind(Resonance.Cinder, 1f, 0f, 0f, 1f)))
                    Assert.AreNotEqual(legendary, pull.Card, "100% Epic with no Epic cards: never a Legendary");
            var always = Kind(Resonance.Cinder, 1f, 0f, 0f, 0f);
            always.legendaryShare = 1f;
            Assert.AreEqual(legendary, CardPacks.Roll(roster, rng, always)[0].Card, "100% Legendary: the Legendary first");
        }

        [Test]
        public void PoolShare_PicksResonanceOrBasic_AndNeverOtherResonances()
        {
            var arc = Card("Arc1", Resonance.Arc, CardRarity.Common);
            var arc2 = Card("Arc2", Resonance.Arc, CardRarity.Common);
            var phys = Card("Phys", Resonance.Basic, CardRarity.Common);
            var root = Card("Root", Resonance.Root, CardRarity.Common);
            var roster = Roster(arc, arc2, phys, root);
            var rng = new System.Random(7);
            for (int i = 0; i < 100; i++)
                foreach (var pull in CardPacks.Roll(roster, rng, Kind(Resonance.Arc, 1f, 1f, 0f, 0f)))
                    if (pull.Tier == CardPacks.Tier.Common)
                    {
                        Assert.AreNotEqual(root, pull.Card, "another resonance's card never comes");
                    }
            var onlyBasic = CardPacks.Roll(roster, rng, Kind(Resonance.Arc, 0f, 1f, 0f, 0f));
            Assert.AreEqual(phys, onlyBasic[0].Card, "0% resonance: Basic first");
        }

        [Test]
        public void RarityTiers_AreRespected_WhenTheTierHasCards()
        {
            var common = Card("Common", Resonance.Cinder, CardRarity.Common);
            var epic = Card("Epic", Resonance.Cinder, CardRarity.Epic);
            var roster = Roster(common, epic);
            var rng = new System.Random(3);
            for (int i = 0; i < 50; i++)
            {
                var pulls = CardPacks.Roll(roster, rng, Kind(Resonance.Cinder, 1f, 0f, 0f, 1f));
                Assert.AreEqual(epic, pulls[0].Card, "100% Epic: the Epic card first");
            }
        }

        [Test]
        public void OldSavesPackCount_BecomesAreaPacks()
        {
            WorldSession.PackQueue.Clear();
            WorldSession.AddPacks("", 2);
            WorldSession.AddPacks("Arc", 1);
            Assert.AreEqual(3, WorldSession.Packs);
            Assert.AreEqual("", WorldSession.TakePack(), "oldest first");
            WorldSession.TakePack();
            Assert.AreEqual("Arc", WorldSession.TakePack());
            Assert.IsNull(WorldSession.TakePack());
        }
    }
}
