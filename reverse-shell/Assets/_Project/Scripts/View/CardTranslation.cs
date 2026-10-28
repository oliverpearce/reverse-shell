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

        public static bool On { get { return Check == null || Check(); } }
    }
}
