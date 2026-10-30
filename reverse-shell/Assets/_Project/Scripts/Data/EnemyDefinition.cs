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

        [Header("Break (from the card sheet's Enemies tab)")]
        [Tooltip("Break value: weakness hits take Break damage off it; at 0 the enemy is Staggered. 0 = can't be broken. " +
                 "A boss gets PackRates.bossBreakBonus more.")]
        [Min(0)] public int breakPoints = 3;
        [Tooltip("What its Break value is weak to: attack types and resonances. Every Chapter One enemy is weak to Physical.")]
        public Weakness weaknesses = Weakness.Physical;

        [Tooltip("Ult: replaces the round's skill once the enemy's Ult points are full (it gains them by acting, like the party). " +
                 "It's the Soul form of the innate card below.")]
        public CardDefinition ritual;

        [Header("Innate card (from the card sheet's Enemies tab)")]
        [Tooltip("A Basic card the enemy always has, played Innate Levels higher; the Ult above is its Soul form.")]
        public CardDefinition innate;
        [Min(0)] public int innateLevels = 3;
        [Tooltip("Its weight among the skills when the AI picks a move.")]
        [Min(0f)] public float innateWeight = 1f;
    }

    [Serializable]
    public class EnemySkill
    {
        public CardDefinition card;
        [Range(1, 3)] public int rank = 1;
        [Min(0f)] public float weight = 1f;
    }
}
