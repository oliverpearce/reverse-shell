using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Internal slots on the opened character card: CHIP and CORE. The numbers are fixed (they index the saved slot
    /// list), so weapon / armor slots (0, 1) can come back later without breaking saves.
    /// </summary>
    public enum PartSlot { Chip = 2, Core = 3 }

    /// <summary>An internal part a robot can have fitted: one per slot, each giving stat modifiers for every fight.</summary>
    [CreateAssetMenu(menuName = "Combat/Part", fileName = "Part_New")]
    public class PartDefinition : ScriptableObject
    {
        public string displayName;
        public PartSlot slot;
        public Color color = Color.white;
        [TextArea] public string description;
        public List<StatModifier> modifiers = new List<StatModifier>();
        [Tooltip("The modifiers only apply while the wearer's element is the one below (e.g. the Resonant Core while Electric).")]
        public bool elementOnly;
        public Afflatus element;

        /// <summary>Whether the modifiers count for a wearer of this element.</summary>
        public bool AppliesTo(Afflatus wearer) { return !elementOnly || wearer == element; }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
        }
    }
}
