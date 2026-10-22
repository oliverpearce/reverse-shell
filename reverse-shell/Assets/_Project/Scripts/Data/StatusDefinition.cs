using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>A buff or debuff. Durations count down at the end of each round.</summary>
    [CreateAssetMenu(menuName = "Combat/Status", fileName = "Status_New")]
    public class StatusDefinition : ScriptableObject
    {
        public string displayName;
        [Tooltip("2-3 letters shown on the status chip.")]
        public string shortLabel = "ST";
        public Color color = Color.cyan;
        public Sprite icon;
        public bool isDebuff;

        public StackRule stackRule = StackRule.AddStacks;
        [Min(1)] public int maxStacks = 1;

        [Tooltip("Applied once per stack.")]
        public List<StatModifier> modifiers = new List<StatModifier>();

        [Tooltip("Fraction of max HP lost at round end, per stack (0.05 = 5%).")]
        public float damagePerRoundPercentMaxHP;
        [Tooltip("Fraction of max HP restored at round end, per stack.")]
        public float healPerRoundPercentMaxHP;
        [Tooltip("Unit skips its actions while this is active.")]
        public bool preventsAction;

        [TextArea] public string description;

        [Tooltip("Optional verb for card text: 'poison' reads 'poison 1 enemy (x1, 2 turns)'. " +
                 "Empty reads 'give 1 enemy <name> (2 turns)'.")]
        public string verb;

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
        }
    }
}
