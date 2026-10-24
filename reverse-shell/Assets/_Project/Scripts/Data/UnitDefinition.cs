using UnityEngine;

namespace Combat
{
    /// <summary>Shared data for anything that can stand on the battlefield.</summary>
    public abstract class UnitDefinition : ScriptableObject
    {
        public string displayName;
        public Color color = Color.gray;
        [Tooltip("Optional. When empty, a colored placeholder with the name's initial is shown.")]
        public Sprite portrait;
        [Tooltip("Animated pixel sprite (idle, walk, slash, spellcast, hurt). Takes priority over Sprite Sheet.")]
        public SpriteAnimationSet animations;
        [Tooltip("Legacy: pixel-art walk sheet in RPG Maker layout: columns = walk frames, rows = down, left, right, up. " +
                 "Shown in battle (facing the enemy) and used for overworld characters.")]
        public Texture2D spriteSheet;
        [Min(1)] public int sheetColumns = 3;
        [Min(1)] public int sheetRows = 4;
        public Afflatus afflatus;
        public DamageType damageType;
        public Stats stats = new Stats();

        /// <summary>
        /// Runtime names that replace an asset's display name, keyed by asset name (the protagonist's chosen name
        /// replaces "MC"). Never saved into the asset.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> NameOverrides =
            new System.Collections.Generic.Dictionary<string, string>();

        public string DisplayName
        {
            get
            {
                string over;
                if (NameOverrides.TryGetValue(name, out over) && !string.IsNullOrEmpty(over)) return over;
                return string.IsNullOrEmpty(displayName) ? name : displayName;
            }
        }
    }
}
