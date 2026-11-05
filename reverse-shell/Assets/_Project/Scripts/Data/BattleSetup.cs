using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Everything needed to start a fight. An RPG layer can build one at runtime with
    /// ScriptableObject.CreateInstance and hand it to BattleBootstrap / BattleController.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Battle Setup", fileName = "Battle_New")]
    public class BattleSetup : ScriptableObject
    {
        public CombatConfig config;
        [Tooltip("The active members (up to 3, the first leads).")]
        public List<CharacterDefinition> party = new List<CharacterDefinition>();
        [Tooltip("Waits in reserve and steps in when an active member falls (optional).")]
        public CharacterDefinition backup;
        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
        [Tooltip("Painted background for this fight (match the area it happens in).")]
        public BattleBackdropDefinition backdrop;
        [Tooltip("0 = random every time.")]
        public int seed;

        [Header("Enemy strength")]
        [Tooltip("Enemy level: their stats are their Lv 5 values scaled up or down (1-99).")]
        [Range(1, 99)] public int enemyLevel = BattleUnit.DefaultEnemyLevel;
        [Tooltip("Boss: 3x HP, 1.25x ATK, a bigger sprite and a BOSS tag.")]
        public bool boss;
        [Tooltip("Nobody on the player's side can drop below 1 HP (the Collector, Chapter One's first real fight).")]
        public bool partyCantFall;
    }
}
