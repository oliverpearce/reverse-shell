using System.Collections.Generic;

namespace Combat
{
    /// <summary>What a party member brings to a fight besides their level: the cards in their deck and fitted Parts.</summary>
    public class Loadout
    {
        /// <summary>Incantation cards dealt into the hand. Empty = the character's default incantations.</summary>
        public List<CardDefinition> Cards = new List<CardDefinition>();
        public List<PartDefinition> Parts = new List<PartDefinition>();
        /// <summary>The active resonance's element (null = the definition's element).</summary>
        public Resonance? Element;
        /// <summary>The active resonance's Ult card (null = the definition's ritual).</summary>
        public CardDefinition Ultimate;
        /// <summary>Copy upgrades per card (0-3, from copies owned; missing = none): +2 card levels each.</summary>
        public Dictionary<CardDefinition, int> Upgrades = new Dictionary<CardDefinition, int>();
        /// <summary>Sun / Moon / Star bound onto cards (missing = none).</summary>
        public Dictionary<CardDefinition, CardBinding> Bindings = new Dictionary<CardDefinition, CardBinding>();
        /// <summary>Soul level (0-3): extra Ult power (PackRates.ultPowerPerSoul each).</summary>
        public int SoulLevel;
    }
}
