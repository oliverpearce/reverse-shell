using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The Stylized Nature MegaKit (Quaternius, CC0; Resources/NatureKit) placed on the map for a <see cref="WorldTheme"/>.
    /// Each model is loaded by name, scaled so it stands the theme's height (the kit's own sizes vary a lot: a twisted
    /// tree is five times a common one), turned at random by its cell, and given toon materials (the WorldToon
    /// shader) made from the kit's material names, one per texture, tint and sway, shared by every copy (GPU
    /// instanced). The grass and the bush / twisted-tree leaves are coloured by the theme (their textures are white or
    /// grey); the rest keep the kit's colours. Small props (under <see cref="ShadowHeight"/>) cast no shadows.
    /// Blocking stays the map's job (no colliders); NearbyCulling hides far-off copies like any other renderer.
    /// </summary>
    public static class NatureKit
    {
        const string ModelPath = "NatureKit/Models/", TexturePath = "NatureKit/Textures/";

        /// <summary>The kit's materials: which ones are thin cards seen from both sides and sway in the wind.</summary>
        static readonly HashSet<string> Foliage = new HashSet<string>
        {
            "Leaves", "Leaves_NormalTree", "Leaves_TwistedTree", "Leaves_Pine", "Flowers", "Grass",
        };

        /// <summary>Leaves: a faint light edge where a cluster turns away (so canopies read as clusters, not one blob),
        /// and three slight shades of the colour handed out by the cell, so neighbouring trees differ.</summary>
        static readonly Color FoliageEdge = new Color(0.2f, 0.22f, 0.12f, 1f);
        static readonly float[] FoliageShades = { 0.9f, 1f, 1.1f };

        /// <summary>Props shorter than this (tiles) don't cast shadows: grass and flowers by the hundred, for little gain.</summary>
        const float ShadowHeight = 0.9f;

        /// <summary>The nature tiles' models when the theme doesn't name its own (any world can use them).</summary>
        static readonly Dictionary<WorldTheme.PropKind, WorldTheme.PropSet> defaults = new Dictionary<WorldTheme.PropKind, WorldTheme.PropSet>
        {
            { WorldTheme.PropKind.Pine, Set(WorldTheme.PropKind.Pine, 3.6f, 2.2f, "Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5") },
            { WorldTheme.PropKind.DeadTree, Set(WorldTheme.PropKind.DeadTree, 3.3f, 2.4f, "DeadTree_1", "DeadTree_2", "DeadTree_3", "DeadTree_4", "DeadTree_5") },
            { WorldTheme.PropKind.TwistedTree, Set(WorldTheme.PropKind.TwistedTree, 3.8f, 3.2f, "TwistedTree_1", "TwistedTree_2", "TwistedTree_3", "TwistedTree_4", "TwistedTree_5") },
            { WorldTheme.PropKind.Mushroom, Set(WorldTheme.PropKind.Mushroom, 0.4f, 0.6f, "Mushroom_Common", "Mushroom_Laetiporus") },
            { WorldTheme.PropKind.Fern, Set(WorldTheme.PropKind.Fern, 1.1f, 1.4f, "Fern_1", "Plant_7_Big", "Plant_1_Big") },
            { WorldTheme.PropKind.Rock, Set(WorldTheme.PropKind.Rock, 1.2f, 1.5f, "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3") },
            { WorldTheme.PropKind.Pebbles, Set(WorldTheme.PropKind.Pebbles, 0.12f, 0.45f, "Pebble_Round_1", "Pebble_Round_2", "Pebble_Round_3", "Pebble_Square_1", "Pebble_Square_2", "Pebble_Square_3") },
            { WorldTheme.PropKind.Path, Set(WorldTheme.PropKind.Path, 0.1f, 0.9f, "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Square_Small_1", "RockPath_Square_Small_2") },
        };

        static WorldTheme.PropSet Set(WorldTheme.PropKind kind, float height, float width, params string[] names)
        {
            return new WorldTheme.PropSet { kind = kind, height = height, maxWidth = width, sizeJitter = 0.15f, models = new List<string>(names) };
        }

        /// <summary>The theme's models for <paramref name="kind"/>, else the kit's defaults (null if neither).</summary>
        public static WorldTheme.PropSet For(WorldTheme theme, WorldTheme.PropKind kind)
        {
            var set = theme != null ? theme.Props(kind) : null;
            WorldTheme.PropSet fallback;
            return set ?? (defaults.TryGetValue(kind, out fallback) ? fallback : null);
        }

        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Bounds> sizes = new Dictionary<string, Bounds>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Shader toon;

        /// <summary>One prop of <paramref name="set"/> at <paramref name="basePosition"/> (its feet), picked and turned by
        /// <paramref name="seed"/>; null if the model is missing.</summary>
        public static GameObject Place(Transform parent, WorldTheme theme, WorldTheme.PropSet set, Vector3 basePosition, int seed, float scale = 1f)
        {
            if (set == null || set.models.Count == 0) return null;
            var rng = new System.Random(seed);
            string name = set.models[rng.Next(set.models.Count)];
            var prefab = Model(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            Bounds b = sizes[name];
            float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * set.sizeJitter;
            float k = Mathf.Min(set.height / Mathf.Max(0.01f, b.size.y), set.maxWidth / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)));
            k *= jitter * scale;
            go.transform.localScale = prefab.transform.localScale * k;
            go.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f) * prefab.transform.rotation;
            // Feet on the ground: the model's lowest point (its bounds sit a little under its origin in the kit).
            go.transform.position = basePosition - Vector3.up * (b.min.y * k);
            bool shadows = b.size.y * k >= ShadowHeight;   // its real height once scaled
            float shade = FoliageShades[rng.Next(FoliageShades.Length)];
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var shared = r.sharedMaterials;
                // A slot whose material isn't one of the kit's (Bush_Common has a stray "Material") borrows its neighbour's.
                string fallback = null;
                foreach (var m in shared) if (m != null && Textured(KitName(m.name))) { fallback = KitName(m.name); break; }
                for (int i = 0; i < shared.Length; i++)
                {
                    string kit = shared[i] != null ? KitName(shared[i].name) : "";
                    if (!Textured(kit)) kit = fallback ?? "Leaves";
                    string texture;
                    Color colour;
                    float glow;
                    Color rim, shadeTint;
                    Recolour(theme, kit, out texture, out colour, out glow, out rim, out shadeTint);
                    if (Foliage.Contains(kit))
                    {
                        colour = new Color(colour.r * shade, colour.g * shade, colour.b * shade, colour.a);
                        if (rim.a <= 0f) rim = FoliageEdge;
                    }
                    shared[i] = Toon(kit, texture, set.tint * colour, theme != null ? theme.wind : 1f, glow, rim, shadeTint);
                }
                r.sharedMaterials = shared;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return go;
        }

        /// <summary>The model, and its size at scale 1 measured once.</summary>
        static GameObject Model(string name)
        {
            GameObject prefab;
            if (models.TryGetValue(name, out prefab)) return prefab;
            prefab = Resources.Load<GameObject>(ModelPath + name);
            models[name] = prefab;
            if (prefab == null)
            {
                Debug.LogWarning("NatureKit: no model called " + name + " in Resources/" + ModelPath);
                return null;
            }
            // Measured on a throwaway copy at the origin, standing as the kit made it (whatever its root's rotation).
            var probe = Object.Instantiate(prefab);
            probe.transform.position = Vector3.zero;
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in probe.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(probe);
            sizes[name] = bounds;
            return prefab;
        }

        /// <summary>A kit material we have a texture for.</summary>
        static bool Textured(string kit)
        {
            Texture2D tex;
            if (!textures.TryGetValue(kit, out tex)) textures[kit] = tex = string.IsNullOrEmpty(kit) ? null : Resources.Load<Texture2D>(TexturePath + kit);
            return tex != null;
        }

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        /// <summary>The theme's colour for the kit's white or grey textures (grass; bush and twisted-tree leaves).</summary>
        static Color ThemeColour(WorldTheme theme, string kit)
        {
            if (theme == null) return Color.white;
            if (kit == "Grass") return theme.grassColor;
            if (kit == "Leaves_TwistedTree") return theme.leafColor;
            return Color.white;
        }

        /// <summary>Which texture and colour a kit material gets in <paramref name="theme"/>: a theme that recolours its
        /// foliage or bark (Frozen's ice-blue glass) uses the greyscale copy (&lt;name&gt;_Grey) times its colour, with
        /// the foliage glow; otherwise the kit's own texture, tinted only where it's white or grey (<see cref="ThemeColour"/>).</summary>
        static void Recolour(WorldTheme theme, string kit, out string texture, out Color colour, out float glow, out Color rim, out Color shadeTint)
        {
            texture = kit;
            colour = ThemeColour(theme, kit);
            glow = 0f;
            rim = Color.clear;
            shadeTint = Color.white;
            if (theme == null) return;
            bool foliage = Foliage.Contains(kit), bark = kit.StartsWith("Bark_", System.StringComparison.Ordinal);
            if (foliage && theme.foliageRecolor.a > 0f && Textured(kit + "_Grey"))
            {
                texture = kit + "_Grey";
                colour = theme.foliageRecolor;
                glow = theme.foliageGlow;
                rim = theme.foliageRim;
                shadeTint = theme.foliageShade;
            }
            else if (bark && theme.barkRecolor.a > 0f && Textured(kit + "_Grey"))
            {
                texture = kit + "_Grey";
                colour = theme.barkRecolor;
                glow = theme.foliageGlow * 0.5f;   // ice all the way through, a little dimmer than the leaves
                rim = theme.foliageRim;
                shadeTint = theme.foliageShade;
            }
        }

        /// <summary>The toon material for a kit material name ("Leaves_Pine", maybe with Unity's " (Instance)").</summary>
        public static Material Toon(string kitMaterial, Color tint, float wind)
        {
            string key = KitName(kitMaterial);
            return Toon(key, key, tint, wind, 0f, Color.clear, Color.white);
        }

        /// <summary>The toon material for kit material <paramref name="key"/> drawn with <paramref name="texture"/>, tinted,
        /// swaying (foliage only), glowing by <paramref name="glow"/> (0-1, in its own colour) and shading into
        /// <paramref name="shadeTint"/> away from the sun (white = plain).</summary>
        static Material Toon(string key, string texture, Color tint, float wind, float glow, Color rim, Color shadeTint)
        {
            bool foliage = Foliage.Contains(key);
            string cacheKey = texture + "|" + ColorUtility.ToHtmlStringRGBA(tint) + "|" + (foliage ? wind : 0f) + "|" + glow + "|" + ColorUtility.ToHtmlStringRGBA(rim)
                + "|" + ColorUtility.ToHtmlStringRGBA(shadeTint);
            Material m;
            if (materials.TryGetValue(cacheKey, out m) && m != null) return m;
            if (toon == null) toon = WorldArt.FindShader("Reverse Shell/WorldToon", "Legacy Shaders/Transparent/Cutout/Diffuse");
            m = new Material(toon) { name = "Toon " + texture, enableInstancing = true, color = tint };
            if (Textured(texture)) m.mainTexture = textures[texture];
            if (glow > 0f && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", tint * glow);
            if (rim.a > 0f && m.HasProperty("_RimColor")) m.SetColor("_RimColor", rim);
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", shadeTint);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", foliage ? 0f : 2f);
            // Leaves wrap the light half way: lit from both sides, but a canopy's far side still falls into shade.
            if (m.HasProperty("_Wrap")) m.SetFloat("_Wrap", foliage ? 0.5f : 0f);
            if (m.HasProperty("_Wind")) m.SetFloat("_Wind", foliage ? wind : 0f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            materials[cacheKey] = m;
            return m;
        }

        /// <summary>The kit's material name without Unity's suffixes ("Leaves_Pine (Instance)" → "Leaves_Pine").</summary>
        static string KitName(string name)
        {
            int cut = name.IndexOf(" (", System.StringComparison.Ordinal);
            if (cut >= 0) name = name.Substring(0, cut);
            return name.Trim();
        }
    }
}
