using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>Atlas lookups, sprite-sheet slicing, generated helper textures and runtime materials.</summary>
    public static class WorldArt
    {
        public const int AtlasCell = 64;

        public static readonly Color Sunlight = new Color(1f, 0.93f, 0.78f);
        /// <summary>Machine eye / threat glow.</summary>
        public static readonly Color MachineRed = new Color(1f, 0.28f, 0.16f);

        // Overworld UI: sepia paper panels with dark ink, like an old field terminal.
        public static readonly Color UiPanel = new Color(0.85f, 0.82f, 0.71f, 0.94f);
        public static readonly Color UiInk = new Color(0.27f, 0.25f, 0.2f);
        public static readonly Color UiEdge = new Color(0.4f, 0.37f, 0.3f);

        /// <summary>Atlas cells for props (see Tools/TileForge/ruins_atlas.py), as (col, row from top).</summary>
        public static readonly Vector2Int LeafCell = new Vector2Int(0, 0), BarkCell = new Vector2Int(1, 0),
            GrassCell = new Vector2Int(2, 0), FlowerCell = new Vector2Int(3, 0), VineCell = new Vector2Int(4, 0),
            ConcreteCell = new Vector2Int(7, 0);

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

        static Texture2D blob, frame, dot, dapple;

        /// <summary>
        /// Tileable sun cookie: mostly canopy shade (dim) with scattered soft-edged spots of full sunlight,
        /// like light filtering through leaves. Built-in pipeline cookies read the alpha channel.
        /// </summary>
        public static Texture2D DappleCookie()
        {
            if (dapple != null) return dapple;
            const int size = 256;
            const float shade = 0.2f;
            var value = new float[size * size];
            for (int i = 0; i < value.Length; i++) value[i] = shade;

            var rng = new System.Random(77);
            for (int s = 0; s < 70; s++)
            {
                float cx = rng.Next(size), cy = rng.Next(size);
                float r = 5f + (float)rng.NextDouble() * (s < 12 ? 20f : 9f); // a few big pools, many small flecks
                float stretch = 0.7f + (float)rng.NextDouble() * 0.6f;      // slightly oval
                float peak = 0.7f + (float)rng.NextDouble() * 0.3f;
                int reach = Mathf.CeilToInt(r * 1.4f);
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx * stretch + dy * dy / stretch) / r;
                        if (d >= 1f) continue;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - d) * 3f)); // soft rim, solid core
                        int x = ((int)cx + dx + size) % size, y = ((int)cy + dy + size) % size; // wraps: tileable
                        int i = y * size + x;
                        value[i] = Mathf.Max(value[i], Mathf.Lerp(shade, peak, k));
                    }
            }

            dapple = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1f, 1f, 1f, value[i]);
            dapple.SetPixels(colors);
            dapple.Apply();
            return dapple;
        }

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

        /// <summary>Tiny plus-shaped mote for dust, pollen and leaf particles.</summary>
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
        // Built at runtime from built-in shaders. For player builds, add "Sprites/Diffuse",
        // "Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Alpha Blended" and
        // "Legacy Shaders/Transparent/Cutout/Diffuse" to Project Settings > Graphics > Always Included Shaders.

        static readonly Dictionary<Color, Material> emissive = new Dictionary<Color, Material>();
        static readonly Dictionary<string, Material> cellMaterials = new Dictionary<string, Material>();
        static Material spriteLit, additive, alphaBlended, blobMat, darkMetal;

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

        /// <summary>
        /// One atlas cell stretched over a whole mesh (0-1 UVs, e.g. a primitive cube or quad). Cutout cells
        /// (grass, vines) use alpha testing so they cast leafy shadows and sort correctly.
        /// </summary>
        public static Material CellMaterial(Texture atlas, Vector2Int cell, bool cutout = false)
        {
            string key = atlas.GetInstanceID() + ":" + cell + ":" + cutout;
            Material m;
            if (cellMaterials.TryGetValue(key, out m) && m != null) return m;
            m = cutout
                ? new Material(FindShader("Legacy Shaders/Transparent/Cutout/Diffuse", "Standard")) { mainTexture = atlas }
                : WorldMaterial(atlas, 0.05f);
            if (cutout && m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            var uv = AtlasUV(atlas, cell.x, cell.y);
            m.mainTextureOffset = uv.min;
            m.mainTextureScale = uv.size;
            cellMaterials[key] = m;
            return m;
        }

        /// <summary>
        /// Character sprites: a steady light (unlit sprite shader) so detailed, dark-palette characters stay readable
        /// in the dim, dappled scene, tinted with <see cref="SpriteTint"/> so they still sit in its warm haze.
        /// </summary>
        public static Material SpriteLit()
        {
            if (spriteLit == null) spriteLit = new Material(FindShader("Sprites/Default"));
            return spriteLit;
        }

        /// <summary>The scene tint for character sprites: a touch of the warm, hazy sunlight.</summary>
        public static readonly Color SpriteTint = new Color(0.94f, 0.93f, 0.86f);

        public static Material Additive()
        {
            if (additive == null)
            {
                additive = new Material(FindShader("Legacy Shaders/Particles/Additive", "Particles/Standard Unlit"));
                additive.mainTexture = DotTexture();
            }
            return additive;
        }

        /// <summary>Soft (non-glowing) particles such as falling leaves and pollen.</summary>
        public static Material AlphaBlended()
        {
            if (alphaBlended == null)
            {
                alphaBlended = new Material(FindShader("Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit"));
                alphaBlended.mainTexture = DotTexture();
            }
            return alphaBlended;
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

        /// <summary>HDR-bright unlit-looking material; with bloom it reads as a glowing light source.</summary>
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
