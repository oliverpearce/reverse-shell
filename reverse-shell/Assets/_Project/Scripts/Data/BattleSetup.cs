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
        public List<CharacterDefinition> party = new List<CharacterDefinition>();
        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
        [Tooltip("Painted background for this fight (match the area it happens in).")]
        public BattleBackdropDefinition backdrop;
        [Tooltip("0 = random every time.")]
        public int seed;
    }
}
