using System;
using System.Collections.Generic;

namespace Combat
{
    /// <summary>
    /// One resonance of the protagonist (bible §3, §7): the element it fights as, its innate cards (which change what
    /// they do, not just their colour) and its Ultimate. Unlocked by absorbing a guardian's Soul Card.
    /// </summary>
    [Serializable]
    public class ResonanceKit
    {
        public Resonance element = Resonance.Physical;
        public List<CardDefinition> cards = new List<CardDefinition>();
        public CardDefinition ultimate;
    }
}
