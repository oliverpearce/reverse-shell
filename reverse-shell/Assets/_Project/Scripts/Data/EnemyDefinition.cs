using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Combat/Enemy", fileName = "Enemy_New")]
    public class EnemyDefinition : UnitDefinition
    {
        [Tooltip("Skills the AI picks from each round, weighted.")]
        public List<EnemySkill> skills = new List<EnemySkill>();

        [Tooltip("XP each party member earns when this enemy is beaten.")]
        [Min(0)] public int xpReward = 50;
    }

    [Serializable]
    public class EnemySkill
    {
        public CardDefinition card;
        [Range(1, 3)] public int rank = 1;
        [Min(0f)] public float weight = 1f;
    }
}
