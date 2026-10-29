using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Enemies' innate cards: a Basic card in their move list, played some levels up.</summary>
    public class EnemyInnateTests
    {
        [Test]
        public void InnateCard_IsPicked_AndPlaysItsLevelsUp()
        {
            var strike = TestFactory.Card("Strike");
            strike.level = 4;
            var def = TestFactory.Enemy("Biter", resonance: Resonance.Basic);
            def.innate = strike;
            def.innateLevels = 3;
            def.innateWeight = 1f;
            var enemy = TestFactory.Unit(def, Team.Enemy);

            var intent = EnemyAI.ChooseIntent(enemy, new System.Random(1), 5);
            Assert.AreSame(strike, intent.Definition, "with no other skills, the innate is the move");
            Assert.AreEqual(4 + 3, CardLevels.Of(intent), "played Innate Levels higher");

            var hero = TestFactory.Unit(TestFactory.Character("Hero", Resonance.Basic, 100f, null, strike), Team.Player);
            Assert.AreEqual(4, CardLevels.Of(new CardInstance(strike, hero, 1)), "the same card for the party has no bonus");
        }
    }
}
