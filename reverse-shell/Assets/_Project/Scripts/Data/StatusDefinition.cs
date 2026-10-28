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
        [Tooltip("Share of the source's ATK lost at round end, per stack (Burn: 0.15 = 15%).")]
        public float damagePerRoundSourceATK;
        [Tooltip("Unit skips its actions while this is active.")]
        public bool preventsAction;
        [Tooltip("Shield, Counter and Cover are run by the battle rules; the card effect that gives it sets its numbers.")]
        public StatusSpecial special;
        [Tooltip("At max stacks this status turns into another one (Chill at 3 becomes Frozen).")]
        public StatusDefinition becomesAtMaxStacks;
        [Min(1)] public int becomesTurns = 1;

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
