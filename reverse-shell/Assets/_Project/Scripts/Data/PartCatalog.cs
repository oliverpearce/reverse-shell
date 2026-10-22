using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>Every Part in the game, so saves can store a Part by asset name and find it again.</summary>
    [CreateAssetMenu(menuName = "Combat/Part Catalog", fileName = "PartCatalog")]
    public class PartCatalog : ScriptableObject
    {
        public List<PartDefinition> parts = new List<PartDefinition>();
        [Tooltip("Parts a new game starts with in the inventory.")]
        public List<PartDefinition> starterParts = new List<PartDefinition>();

        public PartDefinition Find(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            return parts.Find(p => p != null && p.name == assetName);
        }
    }
}
