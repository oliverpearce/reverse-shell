using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>Atlas lookups, sprite-sheet slicing, generated helper textures and runtime materials.</summary>
    public static class WorldArt
    {
        public const int AtlasCell = 64;

        public static readonly Color NeonMagenta = new Color(1f, 0.18f, 0.55f);
        public static readonly Color NeonCyan = new Color(0.18f, 0.95f, 1f);
        public static readonly Color NeonGreen = new Color(0.7f, 1f, 0.23f);
        public static readonly Color NeonOrange = new Color(1f, 0.6f, 0.24f);
        public static readonly Color[] NeonPalette = { NeonMagenta, NeonCyan, NeonGreen, NeonOrange };

        /// <summary>UV rect of atlas cell (col, row) where row 0 is the top row. Inset half a texel to avoid bleeding.</summary>
        public static Rect AtlasUV(Texture atlas, int col, int row)
        {
            float w = atlas.width, h = atlas.height, inset = 0.5f;
            return Rect.MinMaxRect(
                (col * AtlasCell + inset) / w,
                1f - ((row + 1) * AtlasCell - inset) / h,
                ((col + 1) * AtlasCell - inset) / w,
                1f - (row * AtlasCell + inset) / h);
        }

        /// <summary>Slices a sheet into [row, col] sprites (row 0 = top), pivot at the feet, 1 cell = 1 world unit.</summary>
        public static Sprite[,] SliceSheet(Texture2D sheet, int cols, int rows)
        {
            int cw = sheet.width / cols, ch = sheet.height / rows;
            var sprites = new Sprite[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var rect = new Rect(c * cw, sheet.height - (r + 1) * ch, cw, ch);
                    sprites[r, c] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.04f), cw, 0, SpriteMeshType.FullRect);
                }
            return sprites;
        }

        /// <summary>A single cell of a sheet as a 1x1 frame set (for static NPCs).</summary>
        public static Sprite[,] SingleFrame(Sprite[,] sheet, int row, int col)
        {
            return new Sprite[1, 1] { { sheet[row, col] } };
        }

        // ------------------------------------------------------------------ Generated textures

        static Texture2D blob, frame, dot;

        public static Texture2D BlobTexture()
        {
            if (blob != null) return blob;
            const int size = 32;
            blob = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    blob.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(1f - d) * 0.75f));
                }
            blob.Apply();
            return blob;
        }

        /// <summary>16x16 hollow square, for the "stand here" floor marker.</summary>
        public static Texture2D FrameTexture()
        {
            if (frame != null) return frame;
            const int size = 16;
            frame = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x <= 1 || y <= 1 || x >= size - 2 || y >= size - 2;
                    bool corner = (x <= 3 || x >= size - 4) && (y <= 3 || y >= size - 4);
                    frame.SetPixel(x, y, edge || corner ? Color.white : new Color(1f, 1f, 1f, 0.12f));
                }
            frame.Apply();
            return frame;
        }

        /// <summary>Tiny plus-shaped sparkle for Glimmer and rain particles.</summary>
        public static Texture2D DotTexture()
        {
            if (dot != null) return dot;
            dot = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    bool on = x == 2 || y == 2;
                    float a = x == 2 && y == 2 ? 1f : (on ? 0.6f : 0f);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            dot.Apply();
            return dot;
        }

        // ------------------------------------------------------------------ Materials
        // Built at runtime from built-in shaders. For player builds, add "Sprites/Diffuse" and
        // "Legacy Shaders/Particles/Additive" to Project Settings > Graphics > Always Included Shaders.

        static readonly Dictionary<Color, Material> emissive = new Dictionary<Color, Material>();
        static Material spriteLit, additive, blobMat, darkMetal;

        public static Shader FindShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Sprites/Default");
        }

        public static Material WorldMaterial(Texture atlas, float glossiness)
        {
            var m = new Material(FindShader("Standard")) { mainTexture = atlas };
            m.SetFloat("_Glossiness", glossiness);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>Sprites that react to the neon point lights.</summary>
        public static Material SpriteLit()
        {
            if (spriteLit == null) spriteLit = new Material(FindShader("Sprites/Diffuse", "Sprites/Default"));
            return spriteLit;
        }

        public static Material Additive()
        {
            if (additive == null)
            {
                additive = new Material(FindShader("Legacy Shaders/Particles/Additive", "Particles/Standard Unlit"));
                additive.mainTexture = DotTexture();
            }
            return additive;
        }

        public static Material AdditiveTinted(Texture tex, Color tint)
        {
            var m = new Material(FindShader("Legacy Shaders/Particles/Additive", "Particles/Standard Unlit")) { mainTexture = tex };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
            m.color = tint;
            return m;
        }

        public static Material Blob()
        {
            if (blobMat == null) blobMat = new Material(FindShader("Unlit/Transparent", "Sprites/Default")) { mainTexture = BlobTexture() };
            return blobMat;
        }

        public static Material DarkMetal()
        {
            if (darkMetal == null)
            {
                darkMetal = new Material(FindShader("Standard")) { color = new Color(0.08f, 0.07f, 0.1f) };
                darkMetal.SetFloat("_Glossiness", 0.6f);
            }
            return darkMetal;
        }

        /// <summary>HDR-bright unlit-looking material; with bloom this reads as neon tube light.</summary>
        public static Material Emissive(Color color, float intensity = 3.5f)
        {
            Material m;
            if (emissive.TryGetValue(color, out m) && m != null) return m;
            m = new Material(FindShader("Standard")) { color = color * 0.3f };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            emissive[color] = m;
            return m;
        }

        /// <summary>Unity primitive without its collider (we use grid collision).</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
