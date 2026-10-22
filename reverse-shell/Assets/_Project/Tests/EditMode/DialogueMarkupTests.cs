using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    public class DialogueMarkupTests
    {
        [Test]
        public void Tags_AreStripped_AndStyleTheirCharacters()
        {
            var line = DialogueMarkup.Parse("a{wave}bc{/wave}{shake}d{/shake}{red}e{/c}f", 1f);

            Assert.AreEqual("abcdef", line.Text);
            Assert.AreEqual(6, line.Glyphs.Length);
            Assert.IsFalse(line.Glyphs[0].Wave);
            Assert.IsTrue(line.Glyphs[1].Wave && line.Glyphs[2].Wave);
            Assert.IsTrue(line.Glyphs[3].Shake);
            Assert.IsTrue(line.Glyphs[4].HasColor);
            Assert.IsFalse(line.Glyphs[5].HasColor, "{/c} closes the color");
        }

        [Test]
        public void Pauses_Punctuation_AndSpeed_ChangeTiming()
        {
            var plain = DialogueMarkup.Parse("ab", 1f);
            var paused = DialogueMarkup.Parse("a{p=0.5}b", 1f);
            Assert.AreEqual(plain.Glyphs[1].Delay + 0.5f, paused.Glyphs[1].Delay, 1e-4f);

            var sentence = DialogueMarkup.Parse("a.b", 1f);
            Assert.Greater(sentence.Glyphs[2].Delay, plain.Glyphs[1].Delay, "a beat after a full stop");
            var ellipsis = DialogueMarkup.Parse("a..b", 1f);
            Assert.AreEqual(plain.Glyphs[1].Delay, ellipsis.Glyphs[1].Delay, 1e-4f, "no beat inside '...'");

            var fast = DialogueMarkup.Parse("ab", 2f);
            Assert.AreEqual(plain.Glyphs[1].Delay / 2f, fast.Glyphs[1].Delay, 1e-4f);
        }

        [Test]
        public void UnknownTags_AreDropped_AndUnclosedBracesStay()
        {
            Assert.AreEqual("hi", DialogueMarkup.Parse("{nonsense}hi", 1f).Text);
            Assert.AreEqual("a{b", DialogueMarkup.Parse("a{b", 1f).Text);
        }
    }
}
