using System;

namespace Combat.View
{
    /// <summary>
    /// CARDS > TRANSLATION
    /// Whether card words can be read. Without the language chip (Chapter One) card names are runes everywhere, and a
    /// zoomed card's text is runes too except its numbers and the colours of its statuses. The game decides
    /// (<see cref="Check"/>, set by Game.World's CardCollection): the chip, the Sandbox, Pack Mode and the title
    /// screen's Cards book all read. Unset (tests, tools), everything reads.
    /// </summary>
    public static class CardTranslation
    {
        public static Func<bool> Check;
        /// <summary>The numbers chip: card numbers (text, live numbers, status turns and stacks) read as digits.</summary>
        public static Func<bool> NumbersCheck;
        /// <summary>The colours chip: card text and status names in their colours (gold numbers, coloured statuses, the
        /// kind line's colour).</summary>
        public static Func<bool> ColoursCheck;

        public static bool On { get { return Check == null || Check(); } }
        public static bool Numbers { get { return NumbersCheck == null || NumbersCheck(); } }
        public static bool Colours { get { return ColoursCheck == null || ColoursCheck(); } }

        /// <summary>Digits hidden: each becomes "?" (before the numbers chip).</summary>
        public static string HideDigits(string s)
        {
            if (string.IsNullOrEmpty(s) || Numbers) return s;
            var chars = s.ToCharArray();
            bool inTag = false;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '<') inTag = true;
                else if (chars[i] == '>') inTag = false;
                else if (!inTag && char.IsDigit(chars[i])) chars[i] = '?';
            }
            return new string(chars);
        }

        /// <summary>Rich-text colour tags taken out (before the colours chip).</summary>
        public static string PlainColours(string s)
        {
            if (string.IsNullOrEmpty(s) || Colours) return s;
            return System.Text.RegularExpressions.Regex.Replace(s, "</?color[^>]*>", "");
        }
    }
}
