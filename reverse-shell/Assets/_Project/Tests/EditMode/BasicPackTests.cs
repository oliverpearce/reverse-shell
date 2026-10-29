using System.Collections.Generic;
using Game.World;
using NUnit.Framework;
using UnityEditor;

namespace Combat.Tests
{
    /// <summary>The real Basic pack (PackRates) on the real protagonist (Char_MC): only Basic cards, and all of them.</summary>
    public class BasicPackTests
    {
        static CharacterDefinition Mc()
        {
            var mc = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Content/Characters/Char_MC.asset");
            Assert.IsNotNull(mc, "Char_MC.asset");
            return mc;
        }

        [Test]
        public void BasicPack_GivesOnlyBasicCards_AndEveryOneOfTheSet()
        {
            var basic = PackRates.Current.Pack("Basic");
            Assert.IsNotNull(basic, "a Basic pack (re-import the card sheet if this fails)");
            Assert.AreEqual(Resonance.Basic, basic.pool);
            var roster = new List<CharacterDefinition> { Mc() };
            var set = PackModeRun.SetOf(basic, roster);
            Assert.Greater(set.Count, 0, "the Basic set has cards");

            var seen = new HashSet<CardDefinition>();
            var rng = new System.Random(7);
            for (int i = 0; i < 2000; i++)
                foreach (var pull in CardPacks.Roll(roster, rng, basic))
                {
                    if (pull.Tier != CardPacks.Tier.Common) continue;
                    Assert.AreEqual(Resonance.Basic, pull.Card.pool, pull.Card.name + " came from the Basic pack");
                    seen.Add(pull.Card);
                }
            foreach (var card in set) Assert.IsTrue(seen.Contains(card), card.name + " never came from the Basic pack");
        }

        [Test]
        public void OldPhysicalId_StillFindsTheBasicPack()
        {
            Assert.AreSame(PackRates.Current.Pack("Basic"), PackRates.Current.Pack("Physical"));
        }
    }
}
