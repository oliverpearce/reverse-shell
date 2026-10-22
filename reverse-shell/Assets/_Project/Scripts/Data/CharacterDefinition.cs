using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>A playable character: stats plus the cards they contribute to the hand.</summary>
    [CreateAssetMenu(menuName = "Combat/Character", fileName = "Char_New")]
    public class CharacterDefinition : UnitDefinition
    {
        [Tooltip("The character's class / archetype, e.g. Cantor, Bruiser, Medic. Shown under their name.")]
        public string archetype;

        [Tooltip("Incantation cards dealt into the hand (Reverse: 1999 characters have two).")]
        public List<CardDefinition> incantations = new List<CardDefinition>();

        [Tooltip("Ultimate card, spawned into the hand when Moxie reaches the cap.")]
        public CardDefinition ritual;
    }
}
