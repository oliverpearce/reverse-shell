using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Combat/Combat Config", fileName = "CombatConfig")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Hand & Actions")]
        [Min(1)] public int handSize = 7;
        [Min(1)] public int actionPointsPerRound = 3;

        [Header("Moxie")]
        [Min(1)] public int moxieCap = 5;
        public int moxiePerCast = 1;
        public int moxiePerMerge = 1;

        [Header("Damage")]
        [Tooltip("Mitigation = DEF / (DEF + defenseScale).")]
        public float defenseScale = 600f;
        public float advantageDamage = 1.3f;
        public float advantageCritBonus = 0.15f;
        public float disadvantageDamage = 0.8f;
        [Tooltip("Prism is neutral in every matchup but gets this much extra crit chance (0.1 = +10%).")]
        public float prismCritBonus = 0.1f;

        [Header("Card levels (1-20, see GAME_BIBLE: Game balance and systems)")]
        [Tooltip("A card's main hit (or heal) is this x its level, as a multiple of ATK. 0.25: Lv 4 = 100%, Lv 8 = 200%.")]
        public float powerPerLevel = 0.25f;
        [Tooltip("Hitting every enemy (or ally) at once does this share of a single-target hit.")]
        [Range(0f, 1f)] public float spreadShare = 0.6f;
        [Tooltip("Levels a merge adds: rank II, and rank III in all.")]
        public int rankIILevels = 2, rankIIILevels = 5;
        [Tooltip("Levels each copy upgrade (from packs) adds.")]
        public int levelsPerUpgrade = 2;
        [Tooltip("Levels a boss's cards get.")]
        public int bossCardLevels = 3;
        [Min(1)] public int maxCardLevel = 20;

        static CombatConfig defaultConfig;

        public static CombatConfig Default
        {
            get
            {
                if (defaultConfig == null)
                {
                    defaultConfig = CreateInstance<CombatConfig>();
                    defaultConfig.name = "CombatConfig (Default)";
                }
                return defaultConfig;
            }
        }
    }
}
