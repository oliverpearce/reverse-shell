using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// A party member's persistent progress: level, XP toward the next level, current HP, and their loadout
    /// (deck cards and fitted Parts, stored by asset name so the save file can hold them).
    /// </summary>
    [Serializable]
    public class MemberProgress
    {
        public int Level = 1;
        /// <summary>XP earned toward the next level (resets on level up).</summary>
        public int Xp;
        /// <summary>Current HP; negative means full.</summary>
        public int HP = -1;
        /// <summary>Incantation cards in the deck, by asset name. Empty = the character's defaults.</summary>
        public List<string> Cards = new List<string>();
        /// <summary>Fitted Parts by slot (index = <see cref="PartSlot"/>), by asset name; "" = empty slot.</summary>
        public List<string> Parts = new List<string>();
        /// <summary>Soul level from Soul cards pulled in packs (0 to PackRates.maxSoulLevel): more Ult power.</summary>
        public int SoulLevel;
        /// <summary>The cards chosen for the deck's extra slots (asset names; "" = an empty slot), once the player has
        /// edited them (<see cref="ExtrasChosen"/>); until then the default applies (CardCollection.Extras).</summary>
        public List<string> ExtraCards = new List<string>();
        public bool ExtrasChosen;

        public MemberProgress Clone()
        {
            return new MemberProgress
            {
                Level = Level, Xp = Xp, HP = HP,
                Cards = Cards != null ? new List<string>(Cards) : new List<string>(),
                Parts = Parts != null ? new List<string>(Parts) : new List<string>(),
                SoulLevel = SoulLevel,
                ExtraCards = ExtraCards != null ? new List<string>(ExtraCards) : new List<string>(),
                ExtrasChosen = ExtrasChosen,
            };
        }
    }

    /// <summary>XP curve and per-level stat growth. Pure rules, no scene or UI knowledge.</summary>
    public static class Leveling
    {
        public const int MaxLevel = 60;   // the story's last enemies are ~Lv 90, beatable around Lv 50-60

        /// <summary>
        /// How full a stat bar is for the reference character (A3) at the level cap: the bars keep 10% headroom,
        /// so even a maxed character doesn't fill them (Parts and later growth can push past).
        /// </summary>
        public const float BarFillAtCap = 0.9f;

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

        /// <summary>
        /// The value that fills a stat bar completely: the reference character's stat at the level cap, divided by
        /// <see cref="BarFillAtCap"/>. Using one reference keeps every member's bars comparable.
        /// </summary>
        public static float BarMax(CharacterDefinition reference, StatType stat)
        {
            if (reference == null) return 1f;
            float value = reference.stats.Get(stat);
            if (Grows(stat)) value *= StatScale(MaxLevel, reference.growthPerLevel);
            return Mathf.Max(0.0001f, value / BarFillAtCap);
        }

        public static int MaxHP(CharacterDefinition member, int level)
        {
            return Mathf.Max(1, Mathf.RoundToInt(member.stats.maxHP * StatScale(level, member.growthPerLevel)));
        }

        /// <summary>Max HP with fitted Parts' HP modifiers on top (the same formula as <see cref="BattleUnit.GetStat"/>).</summary>
        public static int MaxHP(CharacterDefinition member, int level, IEnumerable<PartDefinition> parts)
        {
            float flat = 0f, percent = 0f;
            if (parts != null)
                foreach (var part in parts)
                {
                    if (part == null) continue;
                    foreach (var mod in part.modifiers)
                    {
                        if (mod.stat != StatType.MaxHP) continue;
                        if (mod.kind == ModifierKind.Flat) flat += mod.value;
                        else percent += mod.value;
                    }
                }
            float baseHP = member.stats.maxHP * StatScale(level, member.growthPerLevel);
            return Mathf.Max(1, Mathf.RoundToInt((baseHP + flat) * (1f + percent)));
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
