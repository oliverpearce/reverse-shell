using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Combat
{
    [Serializable]
    public class Stats
    {
        [Min(1)] public int maxHP = 2000;
        public float attack = 200f;
        [FormerlySerializedAs("realityDefense")] public float physicalDefense = 120f;
        [FormerlySerializedAs("mentalDefense")] public float specialDefense = 120f;
        [Range(0f, 1f)] public float critRate = 0.1f;
        public float critDamage = 1.5f;

        public float Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHP: return maxHP;
                case StatType.Attack: return attack;
                case StatType.PhysicalDefense: return physicalDefense;
                case StatType.SpecialDefense: return specialDefense;
                case StatType.CritRate: return critRate;
                case StatType.CritDamage: return critDamage;
                default: return 0f;
            }
        }
    }

    [Serializable]
    public class StatModifier
    {
        public StatType stat;
        [Tooltip("Flat adds to the base value. Percent multiplies it (0.2 = +20%).\n" +
                 "For bonus stats (Might, DamageBonus, DamageTaken, CritRate) use Flat: 0.1 = +10%.")]
        public ModifierKind kind;
        public float value;

        public StatModifier() { }

        public StatModifier(StatType stat, ModifierKind kind, float value)
        {
            this.stat = stat;
            this.kind = kind;
            this.value = value;
        }
    }
}
