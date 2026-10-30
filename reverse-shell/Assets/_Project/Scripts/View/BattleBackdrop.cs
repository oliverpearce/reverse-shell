using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Battle background: a pixel-art ruin painted from a BattleBackdropDefinition (480x270 art pixels,
    /// shown at 4x) plus slowly scrolling layers of drifting leaves. Painted textures are cached per definition.
    /// </summary>
    public class BattleBackdrop : MonoBehaviour
    {
        public const int ArtWidth = 480, ArtHeight = 270;

        static readonly Dictionary<BattleBackdropDefinition, Texture2D> painted = new Dictionary<BattleBackdropDefinition, Texture2D>();
        static Texture2D leafTexture, fleckTexture;
        float direction = 1f;
        Color? flecks;

        RawImage leavesNear, leavesFar;
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

            if (def.driftingLeaves)
            {
                backdrop.intensity = def.driftIntensity;
                backdrop.direction = def.driftDirection;
                backdrop.flecks = def.driftColor.a > 0f ? (Color?)def.driftColor : null;
                backdrop.leavesFar = backdrop.LeafLayer(root, "LeavesFar", 0.55f * def.driftIntensity, 1.6f);
                backdrop.leavesNear = backdrop.LeafLayer(root, "LeavesNear", 0.85f * def.driftIntensity, 1f);
            }
            return backdrop;
        }

        RawImage LeafLayer(RectTransform root, string name, float alpha, float tiling)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(root, false);
            img.rectTransform.Fill();
            img.texture = flecks.HasValue ? FleckTexture() : LeafTexture();
            var tint = flecks ?? Color.white;
            img.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha) * (flecks.HasValue ? tint.a : 1f));
            img.raycastTarget = false;
            img.uvRect = new Rect(0f, 0f, ArtWidth / 128f * tiling, ArtHeight / 128f * tiling);
            return img;
        }

        void Update()
        {
            Scroll(leavesNear, 0.16f * intensity * direction, -0.07f);
            Scroll(leavesFar, 0.09f * intensity * direction, -0.04f);
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
            if (def.foliage != null && def.foliage.Length >= 4)
            {
                p.Foliage = new Rgb[def.foliage.Length];
                for (int i = 0; i < def.foliage.Length; i++) p.Foliage[i] = ToRgb(def.foliage[i]);
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

        /// <summary>White specks of 1-2 pixels on a transparent, tiling 128x128 texture, tinted per world (snow, embers,
        /// ash, spores: <see cref="BattleBackdropDefinition.driftColor"/>).</summary>
        static Texture2D FleckTexture()
        {
            if (fleckTexture != null) return fleckTexture;
            const int size = 128;
            fleckTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            fleckTexture.SetPixels32(new Color32[size * size]);
            var rng = new System.Random(11);
            for (int i = 0; i < 70; i++)
            {
                int x = rng.Next(size), y = rng.Next(size);
                var c = new Color32(255, 255, 255, (byte)rng.Next(150, 255));
                fleckTexture.SetPixel(x, y, c);
                if (rng.Next(3) == 0) fleckTexture.SetPixel((x + 1) % size, y, c);
            }
            fleckTexture.Apply();
            return fleckTexture;
        }

        /// <summary>Sparse leaf flecks and pollen specks on a transparent, tiling 128x128 texture.</summary>
        static Texture2D LeafTexture()
        {
            if (leafTexture != null) return leafTexture;
            const int size = 128;
            leafTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            leafTexture.SetPixels32(new Color32[size * size]);
            var greens = new[] { new Color32(70, 110, 48, 230), new Color32(110, 150, 60, 230), new Color32(160, 180, 80, 220) };
            var rng = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                int x = rng.Next(size), y = rng.Next(size);
                var c = greens[rng.Next(greens.Length)];
                // A tiny 2-3 pixel leaf, tilted one way or the other.
                leafTexture.SetPixel(x, y, c);
                leafTexture.SetPixel((x + 1) % size, (y + (rng.Next(2) == 0 ? 1 : 0)) % size, c);
                if (rng.Next(2) == 0) leafTexture.SetPixel((x + 2) % size, (y + 1) % size, c);
            }
            for (int i = 0; i < 30; i++) // pollen
                leafTexture.SetPixel(rng.Next(size), rng.Next(size), new Color32(255, 248, 210, (byte)rng.Next(120, 220)));
            leafTexture.Apply();
            return leafTexture;
        }

        static Rgb ToRgb(Color c) { return new Rgb(c.r, c.g, c.b); }
        static byte To8(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255); }
    }
}
