using System.Collections.Generic;
using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>Pack Mode's races: one per pack, with its own set, clock, streak and so close.</summary>
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
        public void EachPack_IsItsOwnRace()
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
            Assert.IsFalse(PackModeRun.Of(arcPack).Started, "only the selected pack races");
            PackModeRun.Select(arcPack);
            PackModeRun.OnPackOpened(arcPack);
            Assert.IsTrue(PackModeRun.Of(arcPack).Started, "its first pack starts its clock");
            Assert.AreEqual(1, PackModeRun.Of(arcPack).Packs);

            var first = PackModeRun.OnReveal(Common(arc1), arcPack, roster);
            Assert.IsTrue(first.NewCard);
            Assert.IsNull(first.Completed);
            var again = PackModeRun.OnReveal(Common(arc1), arcPack, roster);
            Assert.IsFalse(again.NewCard);
            Assert.AreEqual(0, again.Streak, "a repeat breaks the streak");
            var basicCard = PackModeRun.OnReveal(Common(phys), arcPack, roster);
            Assert.IsFalse(basicCard.NewCard, "another set's card from this pack doesn't count");
            Assert.IsFalse(PackModeRun.Has(physPack, phys));
            var soul = PackModeRun.OnReveal(new CardPacks.Pull { Tier = CardPacks.Tier.Soul }, arcPack, roster);
            Assert.IsFalse(soul.NewCard, "Soul cards don't count for a set");

            var done = PackModeRun.OnReveal(Common(arc2), arcPack, roster);
            Assert.AreSame(arcPack, done.Completed, "the last missing card completes the set");
            Assert.IsTrue(PackModeRun.Of(arcPack).Finished);
            Assert.AreSame(arcPack, PackModeRun.JustFinished);
            Assert.IsFalse(PackModeRun.Of(physPack).Started, "the other pack's race is untouched");
        }

        [Test]
        public void SwitchingTabs_PausesTheClock_AndRestartClearsTheRace()
        {
            var arcPack = new PackKind { id = "TestArc", pool = Resonance.Arc };
            var physPack = new PackKind { id = "TestBasic", pool = Resonance.Basic };
            PackModeRun.Select(arcPack);
            PackModeRun.OnPackOpened(arcPack);
            var arc = PackModeRun.Of(arcPack);
            Assert.GreaterOrEqual(arc.RunningSince, 0f, "running while selected");
            PackModeRun.Select(physPack);
            Assert.Less(arc.RunningSince, 0f, "paused once another tab is picked");
            PackModeRun.Select(arcPack);
            Assert.GreaterOrEqual(arc.RunningSince, 0f, "and carries on when picked again");

            PackModeRun.Restart(arcPack, new List<CharacterDefinition>());
            Assert.IsFalse(PackModeRun.Of(arcPack).Started);
            Assert.AreEqual(0, PackModeRun.Of(arcPack).Packs);
        }

        static CardPacks.Pull Common(CardDefinition card) { return new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = card }; }

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
