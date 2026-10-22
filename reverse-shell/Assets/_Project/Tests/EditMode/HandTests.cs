using NUnit.Framework;

namespace Combat.Tests
{
    public class HandTests
    {
        CardDefinition strike, guard, ult;
        BattleUnit vesper, other;

        [SetUp]
        public void SetUp()
        {
            strike = TestFactory.Card("Strike");
            guard = TestFactory.Card("Guard");
            ult = TestFactory.Card("Ult", ritual: true);
            vesper = TestFactory.Unit(TestFactory.Character("Vesper", ritual: ult, incantations: new[] { strike, guard }), Team.Player);
            other = TestFactory.Unit(TestFactory.Character("Other", incantations: new[] { strike }), Team.Player);
        }

        CardInstance C(CardDefinition def, BattleUnit owner, int rank = 1) { return new CardInstance(def, owner, rank); }

        [Test]
        public void AdjacentIdenticalCards_MergeIntoNextRank()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper));
            hand.Add(C(strike, vesper));

            var merges = hand.ResolveMerges();

            Assert.AreEqual(1, merges.Count);
            Assert.AreEqual(1, hand.Count);
            Assert.AreEqual(2, hand[0].Rank);
        }

        [Test]
        public void ChainMerge_TwoPlusOnePlusOne_BecomesRankThree()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper, 2));
            hand.Add(C(strike, vesper));
            hand.Add(C(strike, vesper));

            var merges = hand.ResolveMerges();

            Assert.AreEqual(2, merges.Count);
            Assert.AreEqual(1, hand.Count);
            Assert.AreEqual(3, hand[0].Rank);
        }

        [Test]
        public void RankThree_DoesNotMerge()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper, 3));
            hand.Add(C(strike, vesper, 3));

            Assert.AreEqual(0, hand.ResolveMerges().Count);
            Assert.AreEqual(2, hand.Count);
        }

        [Test]
        public void DifferentRanksOrOwnersOrCards_DoNotMerge()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper, 1));
            hand.Add(C(strike, vesper, 2));
            hand.Add(C(strike, other, 2));
            hand.Add(C(guard, other, 2));

            Assert.AreEqual(0, hand.ResolveMerges().Count);
        }

        [Test]
        public void NonAdjacentCards_DoNotMerge()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper));
            hand.Add(C(guard, vesper));
            hand.Add(C(strike, vesper));

            Assert.AreEqual(0, hand.ResolveMerges().Count);
        }

        [Test]
        public void RemovingCardBetweenDuplicates_TriggersMerge()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper));
            hand.Add(C(guard, vesper));
            hand.Add(C(strike, vesper));

            hand.RemoveAt(1);

            Assert.AreEqual(1, hand.ResolveMerges().Count);
            Assert.AreEqual(2, hand[0].Rank);
        }

        [Test]
        public void MovingCard_CanSetUpMerge()
        {
            var hand = new Hand(7);
            hand.Add(C(strike, vesper));
            hand.Add(C(guard, vesper));
            hand.Add(C(strike, vesper));

            hand.Move(2, 1);

            Assert.AreEqual(1, hand.ResolveMerges().Count);
            Assert.AreEqual(2, hand.Count);
        }

        [Test]
        public void RitualCards_NeverMerge_AndDoNotUseCapacity()
        {
            var hand = new Hand(1);
            hand.Add(C(ult, vesper));
            hand.Add(C(ult, vesper));

            Assert.AreEqual(0, hand.ResolveMerges().Count);
            Assert.IsTrue(hand.HasRoomForDraw);
        }
    }
}
