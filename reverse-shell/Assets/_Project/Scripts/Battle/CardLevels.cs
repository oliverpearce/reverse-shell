using UnityEngine;

namespace Combat
{
    /// <summary>
    /// The card level curve (GAME_BIBLE, "Game balance and systems"). Every card has a level from 1 to 20
    /// (<see cref="CardDefinition.level"/>); in a fight it goes up with copy upgrades (+2 each), merges (+2 at rank II,
    /// +5 at rank III) and on a boss (+3). Its main hit or heal is <see cref="CombatConfig.powerPerLevel"/> x level, as
    /// a multiple of ATK (less when it hits everyone, <see cref="CombatConfig.spreadShare"/>). The multipliers on the
    /// card's effects only set the shape: each one's share of the rank's biggest one (so a 1.4 + 0.5 card does its
    /// level's power plus 36% of it).
    /// </summary>
    public static class CardLevels
    {
        static CombatConfig Config { get { return CombatConfig.Default; } }

        /// <summary>The card being described, so effect text can show its levelled numbers (null = the raw shape).</summary>
        public static CardInstance Describing;

        /// <summary>The card's level in this fight: its own, plus copy upgrades, merges and the boss bonus.</summary>
        public static int Of(CardInstance card)
        {
            if (card == null || card.Definition == null) return 1;
            var c = Config;
            int level = card.Definition.level;
            if (card.Owner != null)
            {
                level += c.levelsPerUpgrade * card.Owner.UpgradesOf(card.Definition);
                if (card.Owner.IsBoss) level += c.bossCardLevels;
            }
            if (!card.IsRitual) level += card.Rank >= 3 ? c.rankIIILevels : card.Rank == 2 ? c.rankIILevels : 0;
            return Mathf.Clamp(level, 1, c.maxCardLevel);
        }

        /// <summary>The main hit's multiple of ATK at a level (0.25 x level by default).</summary>
        public static float Power(int level)
        {
            return Config.powerPerLevel * Mathf.Max(1, level);
        }

        /// <summary>A damage effect's real multiplier on <paramref name="card"/> (null card: the authored value).</summary>
        public static float Damage(CardInstance card, DamageEffect effect)
        {
            if (card == null || card.RankData == null) return effect.multiplier;
            float main = 0f;
            foreach (var e in card.RankData.effects)
            {
                var d = e as DamageEffect;
                if (d != null) main = Mathf.Max(main, d.multiplier);
            }
            return Scaled(card, effect.multiplier, main, effect.target);
        }

        /// <summary>A heal effect's real multiplier on <paramref name="card"/> (null card: the authored value).</summary>
        public static float Heal(CardInstance card, HealEffect effect)
        {
            if (card == null || card.RankData == null) return effect.multiplier;
            float main = 0f;
            foreach (var e in card.RankData.effects)
            {
                var h = e as HealEffect;
                if (h != null) main = Mathf.Max(main, h.multiplier);
            }
            return Scaled(card, effect.multiplier, main, effect.target);
        }

        static float Scaled(CardInstance card, float multiplier, float main, EffectTarget target)
        {
            if (main <= 0f) return 0f;
            float power = Power(Of(card)) * multiplier / main;
            return HitsAll(card, target) ? power * Config.spreadShare : power;
        }

        static bool HitsAll(CardInstance card, EffectTarget target)
        {
            if (target == EffectTarget.AllEnemies || target == EffectTarget.AllAllies) return true;
            if (target != EffectTarget.CardTargets) return false;
            var mode = card.Definition.targetMode;
            return mode == TargetMode.AllEnemies || mode == TargetMode.AllAllies;
        }
    }
}
