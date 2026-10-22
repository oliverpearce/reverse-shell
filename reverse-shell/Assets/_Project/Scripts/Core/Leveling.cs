using System;
using UnityEngine;

namespace Combat
{
    /// <summary>A party member's persistent progress: level, XP toward the next level, and current HP.</summary>
    [Serializable]
    public class MemberProgress
    {
        public int Level = 1;
        /// <summary>XP earned toward the next level (resets on level up).</summary>
        public int Xp;
        /// <summary>Current HP; negative means full.</summary>
        public int HP = -1;

        public MemberProgress Clone()
        {
            return new MemberProgress { Level = Level, Xp = Xp, HP = HP };
        }
    }

    /// <summary>XP curve and per-level stat growth. Pure rules, no scene or UI knowledge.</summary>
    public static class Leveling
    {
        public const int MaxLevel = 50;

        /// <summary>XP needed to go from this level to the next: 100, 200, 300...</summary>
        public static int XpToNext(int level)
        {
            return 100 * Mathf.Max(1, level);
        }

        /// <summary>Base stat multiplier at a level: 1 at level 1, plus growth per level after that.</summary>
        public static float StatScale(int level, float growthPerLevel)
        {
            return 1f + Mathf.Max(0f, growthPerLevel) * (Mathf.Clamp(level, 1, MaxLevel) - 1);
        }

        /// <summary>Whether level growth applies to this stat (the core stats; crit stays flat).</summary>
        public static bool Grows(StatType stat)
        {
            return stat == StatType.MaxHP || stat == StatType.Attack || stat == StatType.PhysicalDefense || stat == StatType.SpecialDefense;
        }

        public static int MaxHP(CharacterDefinition member, int level)
        {
            return Mathf.Max(1, Mathf.RoundToInt(member.stats.maxHP * StatScale(level, member.growthPerLevel)));
        }

        /// <summary>
        /// Adds XP and levels up as many times as it pays for. A member who was hurt keeps their missing HP,
        /// so a level up raises current HP by the same amount as max HP. Returns the number of levels gained.
        /// </summary>
        public static int AddXp(CharacterDefinition member, MemberProgress progress, int gained)
        {
            if (gained <= 0 || progress.Level >= MaxLevel) return 0;
            int startLevel = progress.Level;
            int oldMax = MaxHP(member, startLevel);

            progress.Xp += gained;
            while (progress.Level < MaxLevel && progress.Xp >= XpToNext(progress.Level))
            {
                progress.Xp -= XpToNext(progress.Level);
                progress.Level++;
            }
            if (progress.Level >= MaxLevel) progress.Xp = 0;

            if (progress.HP >= 0) progress.HP += MaxHP(member, progress.Level) - oldMax;
            return progress.Level - startLevel;
        }
    }
}
