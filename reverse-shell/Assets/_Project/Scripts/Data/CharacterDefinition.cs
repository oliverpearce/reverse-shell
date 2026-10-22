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

        [Tooltip("Base HP, ATK and DEF grow by this fraction per level after level 1 (0.08 = +8% per level).")]
        [Range(0f, 0.25f)] public float growthPerLevel = 0.08f;
    }
}
