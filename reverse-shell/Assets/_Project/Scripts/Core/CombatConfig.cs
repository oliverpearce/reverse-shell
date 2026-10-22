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
