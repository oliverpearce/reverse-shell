using System.Collections.Generic;
using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>Pack Mode's race: sets, streaks, splits and the full run.</summary>
    public class PackModeRunTests
    {
        List<PackKind> savedPacks;

        [SetUp]
        public void SetUp()
        {
            savedPacks = PackRates.Current.packs;
            PackModeRun.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PackRates.Current.packs = savedPacks;
            PackModeRun.Reset();
        }

        static CardDefinition Card(string name, Resonance pool)
        {
            var c = TestFactory.Card(name);
            c.pool = pool;
            return c;
        }

        [Test]
        public void FinishingASet_IsASplit_AndEverySet_FinishesTheRun()
        {
            var arc1 = Card("Arc1", Resonance.Arc);
            var arc2 = Card("Arc2", Resonance.Arc);
            var phys = Card("Phys", Resonance.Basic);
            var hero = TestFactory.Character("Hero", Resonance.Basic, 100f, null, phys);
            hero.resonances = new List<ResonanceKit>
            {
                new ResonanceKit { element = Resonance.Basic, cards = new List<CardDefinition> { phys } },
                new ResonanceKit { element = Resonance.Arc, cards = new List<CardDefinition> { arc1, arc2 } },
            };
            var roster = new List<CharacterDefinition> { hero };
            var arcPack = new PackKind { id = "TestArc", displayName = "Arc Pack", pool = Resonance.Arc };
            var physPack = new PackKind { id = "TestBasic", displayName = "Basic Pack", pool = Resonance.Basic };
            PackRates.Current.packs = new List<PackKind> { physPack, arcPack };

            Assert.AreEqual(2, PackModeRun.SetOf(arcPack, roster).Count);
            PackModeRun.OnPackOpened(arcPack);
            Assert.IsTrue(PackModeRun.Started, "the first pack starts the timer");

            var first = PackModeRun.OnReveal(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = arc1 }, roster);
            Assert.IsTrue(first.NewCard);
            Assert.IsNull(first.Completed);
            var again = PackModeRun.OnReveal(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = arc1 }, roster);
            Assert.IsFalse(again.NewCard);
            Assert.AreEqual(0, again.Streak, "a repeat breaks the streak");
            var done = PackModeRun.OnReveal(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = arc2 }, roster);
            Assert.AreSame(arcPack, done.Completed, "the last missing card completes the set");
            Assert.IsNotNull(PackModeRun.SplitOf(arcPack));
            Assert.IsFalse(done.RunComplete);

            var soul = PackModeRun.OnReveal(new CardPacks.Pull { Tier = CardPacks.Tier.Soul }, roster);
            Assert.IsFalse(soul.NewCard, "Soul cards don't count for a set");

            var last = PackModeRun.OnReveal(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = phys }, roster);
            Assert.AreSame(physPack, last.Completed);
            Assert.IsTrue(last.RunComplete, "every set done: the run is over");
            Assert.IsTrue(PackModeRun.RunComplete);
        }

        [Test]
        public void SoClose_ShowsOncePerSet_UntilTheRunResets()
        {
            var arc = new PackKind { id = "TestArc", pool = Resonance.Arc };
            var basic = new PackKind { id = "TestBasic", pool = Resonance.Basic };
            Assert.IsTrue(PackModeRun.NoteSoClose(arc));
            Assert.IsFalse(PackModeRun.NoteSoClose(arc), "not again after the next pack");
            Assert.IsTrue(PackModeRun.NoteSoClose(basic), "each set has its own");
            PackModeRun.Reset();
            Assert.IsTrue(PackModeRun.NoteSoClose(arc), "a new run shows it again");
        }
    }
}
