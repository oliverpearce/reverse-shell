using System.Collections.Generic;

namespace Combat
{
    /// <summary>What a party member brings to a fight besides their level: the cards in their deck and fitted Parts.</summary>
    public class Loadout
    {
        /// <summary>Incantation cards dealt into the hand. Empty = the character's default incantations.</summary>
        public List<CardDefinition> Cards = new List<CardDefinition>();
        public List<PartDefinition> Parts = new List<PartDefinition>();
    }
}
