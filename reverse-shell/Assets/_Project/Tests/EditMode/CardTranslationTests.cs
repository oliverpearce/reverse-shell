using Combat.View;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>The numbers and colours chips: before each, card text hides its digits / colour tags.</summary>
    public class CardTranslationTests
    {
        [TearDown]
        public void TearDown()
        {
            CardTranslation.NumbersCheck = null;
            CardTranslation.ColoursCheck = null;
        }

        [Test]
        public void Digits_AreHidden_UntilTheNumbersChip_ButNotInsideTags()
        {
            CardTranslation.NumbersCheck = () => false;
            Assert.AreEqual("Deal <size=30>???%</size> ATK", CardTranslation.HideDigits("Deal <size=30>125%</size> ATK"));
            CardTranslation.NumbersCheck = () => true;
            Assert.AreEqual("Deal 125%", CardTranslation.HideDigits("Deal 125%"));
        }

        [Test]
        public void ColourTags_AreTakenOut_UntilTheColoursChip()
        {
            CardTranslation.ColoursCheck = () => false;
            Assert.AreEqual("Burn all enemies.", CardTranslation.PlainColours("<color=#FF7F38>Burn</color> all enemies."));
            CardTranslation.ColoursCheck = () => true;
            Assert.AreEqual("<color=#FF7F38>Burn</color>", CardTranslation.PlainColours("<color=#FF7F38>Burn</color>"));
        }
    }
}
