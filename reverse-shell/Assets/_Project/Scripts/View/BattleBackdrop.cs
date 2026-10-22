using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Battle background: a pixel-art street painted from a BattleBackdropDefinition (480x270 art pixels,
    /// shown at 4x) plus scrolling rain layers. Painted textures are cached per definition.
    /// </summary>
    public class BattleBackdrop : MonoBehaviour
    {
        public const int ArtWidth = 480, ArtHeight = 270;

        static readonly Dictionary<BattleBackdropDefinition, Texture2D> painted = new Dictionary<BattleBackdropDefinition, Texture2D>();
        static Texture2D rainTexture;

        RawImage rainNear, rainFar;
        float intensity = 1f;

        public static BattleBackdrop Create(RectTransform parent, BattleBackdropDefinition def)
        {
            var root = UIFactory.CreateRect("Backdrop", parent).Fill();
            var backdrop = root.gameObject.AddComponent<BattleBackdrop>();

            var image = new GameObject("Painting", typeof(RectTransform)).AddComponent<RawImage>();
            image.rectTransform.SetParent(root, false);
            image.rectTransform.Fill();
            image.texture = Paint(def);
            image.raycastTarget = false;

            if (def.rain)
            {
                backdrop.intensity = def.rainIntensity;
                backdrop.rainFar = backdrop.RainLayer(root, "RainFar", 0.35f * def.rainIntensity, 1.6f);
                backdrop.rainNear = backdrop.RainLayer(root, "RainNear", 0.55f * def.rainIntensity, 1f);
            }
            return backdrop;
        }

        RawImage RainLayer(RectTransform root, string name, float alpha, float tiling)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(root, false);
            img.rectTransform.Fill();
            img.texture = RainTexture();
            img.color = new Color(0.7f, 0.8f, 1f, Mathf.Clamp01(alpha));
            img.raycastTarget = false;
            img.uvRect = new Rect(0f, 0f, ArtWidth / 128f * tiling, ArtHeight / 128f * tiling);
            return img;
        }

        void Update()
        {
            Scroll(rainNear, 1.9f * intensity, 0.35f);
            Scroll(rainFar, 1.2f * intensity, 0.2f);
        }

        static void Scroll(RawImage img, float speed, float drift)
        {
            if (img == null) return;
            var r = img.uvRect;
            r.y += speed * Time.deltaTime;
            r.x += drift * Time.deltaTime;
            img.uvRect = r;
        }

        // ------------------------------------------------------------------ Painting

        public static Texture2D Paint(BattleBackdropDefinition def)
        {
            Texture2D tex;
            if (painted.TryGetValue(def, out tex) && tex != null) return tex;

            var p = new BackdropParams
            {
                Seed = def.seed,
                SkyTop = ToRgb(def.skyTop),
                SkyHorizon = ToRgb(def.skyHorizon),
                StreetLine = def.streetLine,
                FacadeBrightness = def.facadeBrightness,
            };
            if (def.neon != null && def.neon.Length > 0)
            {
                p.Neon = new Rgb[def.neon.Length];
                for (int i = 0; i < def.neon.Length; i++) p.Neon[i] = ToRgb(def.neon[i]);
            }

            var pixels = BackdropPainterCore.Paint(ArtWidth, ArtHeight, p, AtlasFor(def));
            tex = new Texture2D(ArtWidth, ArtHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Backdrop_" + def.name
            };
            var colors = new Color32[pixels.Length];
            for (int y = 0; y < ArtHeight; y++)
                for (int x = 0; x < ArtWidth; x++)
                {
                    var c = pixels[y * ArtWidth + x];
                    // Painter rows run top-down; Unity textures run bottom-up.
                    colors[(ArtHeight - 1 - y) * ArtWidth + x] = new Color32(To8(c.R), To8(c.G), To8(c.B), 255);
                }
            tex.SetPixels32(colors);
            tex.Apply(false, false);
            painted[def] = tex;
            return tex;
        }

        static IBackdropAtlas AtlasFor(BattleBackdropDefinition def)
        {
            if (def.tileAtlas == null) return null;
            try
            {
                return new TextureAtlas(def.tileAtlas, def.atlasCell, def.atlasPixelScale);
            }
            catch (UnityException)
            {
                Debug.LogWarning("Backdrop atlas '" + def.tileAtlas.name + "' isn't Read/Write enabled; painting a plain skyline instead.");
                return null;
            }
        }

        class TextureAtlas : IBackdropAtlas
        {
            readonly Color32[] pixels;
            readonly int width, height, cell, scale;

            public TextureAtlas(Texture2D tex, int cellSize, int pixelScale)
            {
                pixels = tex.GetPixels32(); // throws if not readable
                width = tex.width;
                height = tex.height;
                cell = cellSize;
                scale = Mathf.Max(1, pixelScale);
            }

            public bool Sample(int col, int row, int u, int v, out Rgb color)
            {
                int x = col * cell + u * scale + scale / 2;
                int yTop = row * cell + v * scale + scale / 2;
                if (x < 0 || x >= width || yTop < 0 || yTop >= height)
                {
                    color = new Rgb();
                    return false;
                }
                var c = pixels[(height - 1 - yTop) * width + x];
                color = new Rgb(c.r / 255f, c.g / 255f, c.b / 255f);
                return c.a > 127;
            }
        }

        static Texture2D RainTexture()
        {
            if (rainTexture != null) return rainTexture;
            const int size = 128;
            rainTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var clear = new Color32[size * size];
            rainTexture.SetPixels32(clear);
            var rng = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                int x = rng.Next(size), y = rng.Next(size), len = rng.Next(5, 11);
                byte a = (byte)rng.Next(110, 230);
                for (int k = 0; k < len; k++)
                {
                    int px = ((x - k / 3) % size + size) % size, py = (y + k) % size; // slight slant
                    rainTexture.SetPixel(px, py, new Color32(255, 255, 255, a));
                }
            }
            rainTexture.Apply();
            return rainTexture;
        }

        static Rgb ToRgb(Color c) { return new Rgb(c.r, c.g, c.b); }
        static byte To8(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255); }
    }
}
