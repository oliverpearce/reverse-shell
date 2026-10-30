using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// How one world looks (Track C), as an asset in Resources/Themes (Theme_&lt;id&gt;), so it's tuned without code:
    /// its light, ambient and haze, how big the pixels are, and which nature-kit models stand in for each kind of map
    /// prop. An area picks its theme by id (<see cref="AreaAsset.theme"/>); an area with no theme keeps the old look.
    /// </summary>
    [CreateAssetMenu(menuName = "Reverse Shell/World Theme", fileName = "Theme_New")]
    public class WorldTheme : ScriptableObject
    {
        /// <summary>The map props a theme can dress with kit models: the old tiles (Tree T ... Weed, the pavement's
        /// weeds) and Track C's own (Pine p, DeadTree d, TwistedTree k, Mushroom m, Fern f, Rock o, Pebbles e, Path _),
        /// which have defaults in <see cref="NatureKit"/> a theme can override. New kinds go on the end (saved as numbers).</summary>
        public enum PropKind { Tree, Bush, Rubble, Tuft, Flower, Weed, Pine, DeadTree, TwistedTree, Mushroom, Fern, Rock, Pebbles, Path }

        /// <summary>One kind of prop: the models it picks from (by the cell, so the map always looks the same), how big
        /// they stand and a tint.</summary>
        [System.Serializable]
        public class PropSet
        {
            public PropKind kind;
            [Tooltip("Model names in Resources/NatureKit/Models (e.g. CommonTree_1).")]
            public List<string> models = new List<string>();
            [Tooltip("Height in tiles; each model is scaled to it (and kept within Max width).")]
            public float height = 3f;
            [Tooltip("Widest it may be, in tiles.")]
            public float maxWidth = 2.5f;
            [Tooltip("Up to this much bigger or smaller, at random per cell (0.2 = ±20%).")]
            [Range(0f, 0.5f)] public float sizeJitter = 0.15f;
            public Color tint = Color.white;
        }

        [Header("Light")]
        public Color sunColor = new Color(1f, 0.93f, 0.78f);
        public float sunIntensity = 1.9f;
        [Tooltip("Sun angle: x = height (degrees down), y = compass turn.")]
        public Vector2 sunAngles = new Vector2(58f, -30f);
        [Range(0f, 1f)] public float shadowStrength = 0.8f;
        [Tooltip("Dappled leaf light (a cookie on the sun), its size in tiles; 0 = plain sunlight.")]
        public float dappleSize = 9f;
        [Tooltip("The dapple drifts as if the canopy sways.")]
        public bool canopySway = true;

        [Header("Ambient and haze")]
        public Color ambientSky = new Color(0.34f, 0.4f, 0.44f);
        public Color ambientEquator = new Color(0.27f, 0.3f, 0.25f);
        public Color ambientGround = new Color(0.13f, 0.14f, 0.11f);
        public Color fogColor = new Color(0.36f, 0.41f, 0.38f);
        public float fogDensity = 0.028f;

        [Header("Ruins")]
        [Tooltip("The ruin atlas in Resources/Tiles (e.g. ruins_frozen, from Tools/TileForge/ruins_atlas.py --theme); blank = the forest ruins.")]
        public string atlas = "";

        [Header("Battle background")]
        public Color battleSkyTop = new Color(0.52f, 0.66f, 0.8f);
        public Color battleSkyHorizon = new Color(0.88f, 0.88f, 0.8f);
        [Tooltip("Leaf colours from shadow to sunlit highlight (4).")]
        public Color[] battleFoliage =
        {
            new Color(0.14f, 0.27f, 0.16f), new Color(0.21f, 0.42f, 0.2f), new Color(0.33f, 0.57f, 0.25f), new Color(0.55f, 0.75f, 0.34f)
        };
        [Tooltip("The drifting flecks' colour (snow, embers, ash, spores); clear = green leaves.")]
        public Color battleDrift = new Color(0f, 0f, 0f, 0f);
        [Tooltip("1 = the drift falls, -1 = it rises (embers).")]
        public float battleDriftDirection = 1f;

        [Header("Pixel look")]
        [Tooltip("Draw the world as pixel art: the screen is this many pixels tall (0 = full resolution).")]
        public int pixelHeight = 360;
        [Tooltip("How much the leaves and grass sway (0 = still).")]
        [Range(0f, 2f)] public float wind = 1f;

        [Header("Colours")]
        [Tooltip("The grass models' colour (the kit's grass texture is white).")]
        public Color grassColor = new Color(0.5f, 0.68f, 0.28f);
        [Tooltip("The bushes' and twisted trees' leaf colour (their texture is grey).")]
        public Color leafColor = new Color(0.38f, 0.58f, 0.26f);
        [Tooltip("Recolour ALL leaves, grass and flowers (their greyscale copies times this); clear = the kit's colours. Frozen: ice-blue glass.")]
        public Color foliageRecolor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Recolour all bark the same way; clear = the kit's bark.")]
        public Color barkRecolor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("How much recoloured foliage glows in its own colour (frost).")]
        [Range(0f, 1f)] public float foliageGlow = 0f;
        [Tooltip("A bright edge on recoloured foliage and bark, so it reads as glass or ice; clear = off.")]
        public Color foliageRim = new Color(0f, 0f, 0f, 0f);
        [Tooltip("What recoloured foliage and bark shade into where the sun doesn't reach (frozen: light blue); white = plain shading.")]
        public Color foliageShade = Color.white;

        [Header("Props")]
        public List<PropSet> props = new List<PropSet>();
        [Tooltip("A world before the fall (the office): no weeds in the floor and no vines on the walls.")]
        public bool clean;

        /// <summary>One air effect around the player (<see cref="WorldEffects.Air"/>).</summary>
        [System.Serializable]
        public class Effect
        {
            public WorldEffects.Air kind;
            [Tooltip("1 = the usual amount; Graphics: Fast halves it.")]
            public float density = 1f;
            public Color color = Color.white;
        }

        [Header("Effects")]
        public List<Effect> effects = new List<Effect>();
        [Tooltip("Ice crystals' glow (tile i).")]
        public Color crystalColor = new Color(0.6f, 0.9f, 1f);
        [Tooltip("Light shafts' colour (tile l).")]
        public Color shaftColor = new Color(1f, 0.95f, 0.8f);
        [Tooltip("Specimen capsules' liquid (tile u).")]
        public Color capsuleColor = new Color(0.45f, 1f, 0.55f);
        [Tooltip("The colour of a portal that leads into this world (the Sandbox's chapter portals).")]
        public Color portalColor = new Color(0.4f, 0.9f, 1f);

        /// <summary>The props of <paramref name="kind"/>, or null when this theme has no models for it (the old
        /// builder is used instead).</summary>
        public PropSet Props(PropKind kind)
        {
            var set = props.Find(p => p != null && p.kind == kind);
            return set != null && set.models.Count > 0 ? set : null;
        }

        static readonly Dictionary<string, WorldTheme> loaded = new Dictionary<string, WorldTheme>();

        /// <summary>The theme with this id (Resources/Themes/Theme_&lt;id&gt;), or null.</summary>
        public static WorldTheme Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            WorldTheme theme;
            if (!loaded.TryGetValue(id, out theme) || theme == null)
                loaded[id] = theme = Resources.Load<WorldTheme>("Themes/Theme_" + id);
            return theme;
        }
    }
}
