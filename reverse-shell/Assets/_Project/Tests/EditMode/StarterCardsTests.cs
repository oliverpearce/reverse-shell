using System.Collections.Generic;
using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>What a new game owns: the innate card in use and the starter cards (which fill the Alt slots); other innates come from packs.</summary>
    public class StarterCardsTests
    {
        [Test]
        public void CurrentInnateAndStarters_AreOwned_OtherInnatesComeFromPacks()
        {
            WorldSession.Party.Clear();
            WorldSession.CardCopies.Clear();
            var physicalMain = TestFactory.Card("EdgeStrike");
            var arcMain = TestFactory.Card("ArcEdge");
            var arcOther = TestFactory.Card("ChainBolt");
            var strike = TestFactory.Card("Strike");
            var guard = TestFactory.Card("Guard");
            var hero = TestFactory.Character("Hero", Resonance.Basic, 100f, null, physicalMain);
            hero.cardPool = new List<CardDefinition> { physicalMain, strike, guard };
            hero.resonances = new List<ResonanceKit>
            {
                new ResonanceKit { element = Resonance.Basic, cards = new List<CardDefinition> { physicalMain } },
                new ResonanceKit { element = Resonance.Arc, cards = new List<CardDefinition> { arcMain, arcOther } },
            };
            hero.starterCards = new List<CardDefinition> { strike, guard };
            var roster = new List<CharacterDefinition> { hero };

            Assert.IsTrue(CardCollection.Owned(physicalMain, roster), "the innate card in use is owned");
            Assert.IsFalse(CardCollection.Owned(arcMain, roster), "another resonance's innate comes from packs");
            Assert.IsTrue(CardCollection.Owned(strike, roster), "starter cards are owned");
            Assert.IsFalse(CardCollection.Owned(arcOther, roster), "other cards come from packs");
            CollectionAssert.AreEqual(new[] { strike, guard }, CardCollection.Extras(hero), "the starters fill the Alt slots");
        }
    
        [Test]
        public void ACard_IsOnlyEverInOneDeck_TakingItMovesIt_AndInnatesStayPut()
        {
            WorldSession.Party.Clear();
            WorldSession.CardCopies.Clear();
            var strike = TestFactory.Card("Strike");
            var guard = TestFactory.Card("Guard");
            var aMain = TestFactory.Card("AMain");
            var bMain = TestFactory.Card("BMain");
            var a = TestFactory.Character("UnitA", Resonance.Basic, 100f, null, aMain);
            var b = TestFactory.Character("UnitB", Resonance.Basic, 100f, null, bMain);
            a.starterCards = new List<CardDefinition> { strike, guard };
            var roster = new List<CharacterDefinition> { a, b };

            CardCollection.SetExtra(b, 0, strike, roster);
            Assert.IsFalse(CardCollection.Extras(a).Contains(strike), "taking it out of A's deck");
            CollectionAssert.AreEqual(new[] { strike }, CardCollection.Extras(b));
            Assert.AreEqual(1, CardCollection.EquippedBy(strike, roster).Count, "one deck at a time");

            CardCollection.AddCopy(aMain);
            CollectionAssert.DoesNotContain(CardCollection.Eligible(b, roster), aMain, "another unit's innate card can't be taken");
        }
    
        [Test]
        public void LoadingASave_WithACardInTwoDecks_LeavesItWithTheFirstUnit()
        {
            WorldSession.Party.Clear();
            var strike = TestFactory.Card("Strike");
            var guard = TestFactory.Card("Guard");
            var a = TestFactory.Character("UnitA", Resonance.Basic, 100f, null, TestFactory.Card("AMain"));
            var b = TestFactory.Character("UnitB", Resonance.Basic, 100f, null, TestFactory.Card("BMain"));
            a.cardPool = new List<CardDefinition> { strike, guard };
            CardCollection.Register(new[] { a, b });
            // An older save: both chose Strike.
            CardCollection.SetExtra(a, 0, strike);
            CardCollection.SetExtra(a, 1, guard);
            CardCollection.SetExtra(b, 0, strike);
            Assert.AreEqual(2, CardCollection.EquippedBy(strike, new List<CharacterDefinition> { a, b }).Count);

            CardCollection.EnforceOneDeck(new List<CharacterDefinition> { a, b });

            CollectionAssert.AreEqual(new[] { strike, guard }, CardCollection.Extras(a), "the first unit keeps its Alts");
            CollectionAssert.IsEmpty(CardCollection.Extras(b), "the second loses the shared card");
        }
    }
}
