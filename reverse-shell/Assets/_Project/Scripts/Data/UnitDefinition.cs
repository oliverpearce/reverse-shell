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
        [Tooltip("Optional alternate sprite (Settings > Alt Art), e.g. a recolour of A3's. Empty = always Animations.")]
        public SpriteAnimationSet altAnimations;
        [Tooltip("Legacy: pixel-art walk sheet in RPG Maker layout: columns = walk frames, rows = down, left, right, up. " +
                 "Shown in battle (facing the enemy) and used for overworld characters.")]
        public Texture2D spriteSheet;
        [Min(1)] public int sheetColumns = 3;
        [Min(1)] public int sheetRows = 4;
        [UnityEngine.Serialization.FormerlySerializedAs("afflatus")]
        public Resonance resonance;
        public DamageType damageType;
        public Stats stats = new Stats();

        /// <summary>
        /// Runtime names that replace an asset's display name, keyed by asset name (the protagonist's chosen name
        /// replaces "A3"). Never saved into the asset.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> NameOverrides =
            new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>The sprite to show: the alt art when Settings > Alt Art is on and this unit has one, else Animations.
        /// Everything that draws a unit (battle, overworld, menus, Soul cards) reads this.</summary>
        public SpriteAnimationSet Anim
        {
            get
            {
                var set = altAnimations != null && Combat.View.GameSettings.AltArt ? altAnimations : animations;
                if (set != null) set = set.Current;   // the machines' Snoblin / Tech Dungeon looks
                if (set != null && AnimTint != null && set.tintColors != null && set.tintColors.Count > 0)
                {
                    var t = AnimTint(this);
                    if (t.HasValue) return set.Tinted(t.Value);
                }
                return set;
            }
        }

        /// <summary>The colour a unit's sprite is tinted in (its set's tintColors), or null for none. The world sets it:
        /// the protagonist's hair and eyes take her active resonance's colour.</summary>
        public static System.Func<UnitDefinition, Color?> AnimTint;

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
