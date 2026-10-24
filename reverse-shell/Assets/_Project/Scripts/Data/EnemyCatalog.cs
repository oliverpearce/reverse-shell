using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Every enemy, findable by asset name at runtime (story encounters list enemies by name). Lives at
    /// Resources/Bestiary.asset so builds can load it; made by the editor content bootstrap.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Enemy Catalog", fileName = "Bestiary")]
    public class EnemyCatalog : ScriptableObject
    {
        public const string ResourcePath = "Bestiary";

        public List<EnemyDefinition> enemies = new List<EnemyDefinition>();
        /// <summary>Which version of the editor's Chapter One content made this (older ones are regenerated).</summary>
        public int contentVersion;

        public EnemyDefinition Find(string assetName)
        {
            foreach (var e in enemies) if (e != null && e.name == assetName) return e;
            return null;
        }

        static EnemyCatalog loaded;

        /// <summary>The bestiary (cached), or null if the content bootstrap hasn't made it yet.</summary>
        public static EnemyCatalog Get()
        {
            if (loaded == null) loaded = Resources.Load<EnemyCatalog>(ResourcePath);
            return loaded;
        }
    }
}
