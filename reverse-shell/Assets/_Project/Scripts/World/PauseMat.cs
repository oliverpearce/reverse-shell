using System.Collections;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The Esc menu's mat: a woven cloth the player sits on and lays the menu cards out across. One procedural pixel
    /// texture (<see cref="ClothTexture"/>: deep red weave, gold border, plum outline) is shared by the mat on the ground
    /// (<see cref="Create"/>, unrolled beside the seated player) and the big mat the UI rolls across the screen
    /// (PauseCards), plus <see cref="RollTexture"/> for the rolled-up end that rides the unrolling edge.
    /// </summary>
    public class PauseMat : MonoBehaviour
    {
        public const float Width = 1.7f, Depth = 0.95f, RollRadius = 0.07f;

        static readonly System.Collections.Generic.Dictionary<int, Texture2D> cloths = new System.Collections.Generic.Dictionary<int, Texture2D>();
        static readonly System.Collections.Generic.Dictionary<ClothStyle, Texture2D> rolls = new System.Collections.Generic.Dictionary<ClothStyle, Texture2D>();

        /// <summary>The mats' cloths: deep red with gold (the Esc menu), grey with a pearl trim (the Party screen's
        /// Members), teal velvet with a silver trim (the Items screen).</summary>
        public enum ClothStyle { Red, Pearl, Velvet }

        // Per style: outline, trim lit, trim, body, weave, thread (the cloth), then the roll's five shades dark to light.
        static Color32 C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

        static Color32[] Palette(ClothStyle style)
        {
            switch (style)
            {
                case ClothStyle.Pearl:
                    return new[] { C(0x22, 0x24, 0x2C), C(0xFF, 0xFF, 0xFF), C(0xD6, 0xDC, 0xEA), C(0x6A, 0x6E, 0x7A), C(0x58, 0x5B, 0x66), C(0x7A, 0x7E, 0x8A),
                                   C(0x44, 0x47, 0x52), C(0x8E, 0x92, 0x9E), C(0xA8, 0xAC, 0xB8) };
                case ClothStyle.Velvet:
                    return new[] { C(0x0E, 0x1C, 0x22), C(0xEE, 0xF4, 0xF6), C(0xA8, 0xB8, 0xC0), C(0x1E, 0x4E, 0x5A), C(0x17, 0x3E, 0x48), C(0x2A, 0x64, 0x72),
                                   C(0x14, 0x34, 0x3C), C(0x2E, 0x6E, 0x7E), C(0x46, 0x8E, 0x9E) };
                default:
                    return new[] { C(0x2A, 0x0E, 0x14), C(0xF2, 0xD2, 0x7A), C(0xC9, 0x9A, 0x48), C(0x74, 0x22, 0x30), C(0x62, 0x1B, 0x28), C(0x8C, 0x30, 0x3C),
                                   C(0x4E, 0x16, 0x22), C(0x9A, 0x36, 0x44), C(0xB4, 0x48, 0x54) };
            }
        }

        /// <summary>The cloth texture's height in pixels (its width varies, so pixels stay square on any mat).</summary>
        public const int ClothHeight = 21;
        Transform sheet, rollEnd;
        float left;

        /// <summary>
        /// A rolled-up mat centred on <paramref name="center"/> (on the ground; unroll it with <see cref="Unroll"/> or
        /// <see cref="Set"/>). Lying left to right, it unrolls from the west end; <paramref name="northToSouth"/> turns it
        /// to lie north-south, unrolling from the north end.
        /// </summary>
        public static PauseMat Create(Transform parent, Vector3 center, bool northToSouth = false)
        {
            var root = new GameObject("PauseMat");
            root.transform.SetParent(parent, false);
            root.transform.position = center + new Vector3(0f, 0.025f, 0f);
            // A quarter turn about Y takes the mat's local +x (the unrolling direction) to world south (-z).
            root.transform.rotation = Quaternion.Euler(0f, northToSouth ? 90f : 0f, 0f);
            var mat = root.AddComponent<PauseMat>();
            var material = WorldArt.WorldMaterial(ClothTexture(), 0.05f);

            var quad = WorldArt.Primitive(PrimitiveType.Quad, "Cloth", root.transform, material);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.GetComponent<Renderer>().receiveShadows = true;
            mat.sheet = quad.transform;

            var end = WorldArt.Primitive(PrimitiveType.Cylinder, "Roll", root.transform, material);
            end.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // its axis runs front to back
            mat.rollEnd = end.transform;

            mat.left = -Width / 2f;
            mat.Set(0f);
            return mat;
        }

        /// <summary>How far it's unrolled (0 = a roll at the left end, 1 = flat).</summary>
        public void Set(float t)
        {
            t = Mathf.Clamp01(t);
            float w = Mathf.Max(0.001f, Width * t);
            sheet.localScale = new Vector3(w, Depth, 1f);
            sheet.localPosition = new Vector3(left + w / 2f, 0f, 0f);
            // The roll gets thinner as cloth comes off it, and vanishes once it's flat.
            float r = RollRadius * Mathf.Lerp(1.6f, 0.6f, t);
            rollEnd.gameObject.SetActive(t < 0.999f);
            rollEnd.localScale = new Vector3(r * 2f, Depth / 2f + 0.02f, r * 2f);
            rollEnd.localPosition = new Vector3(left + w, r, 0f);
        }

        public IEnumerator Unroll(float seconds) { return Animate(0f, 1f, seconds, false); }

        /// <summary>Rolls back up, then removes the mat.</summary>
        public IEnumerator RollUp(float seconds) { return Animate(1f, 0f, seconds, true); }

        IEnumerator Animate(float from, float to, float seconds, bool destroy)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (this == null) yield break;
                float k = t / seconds;
                Set(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
                yield return null;
            }
            if (this == null) yield break;
            Set(to);
            if (destroy) Destroy(gameObject);
        }

        // ------------------------------------------------------------------ Textures

        /// <summary>The woven cloth, 68 x 21 px (the ground mat's shape; pixel art, point-filtered).</summary>
        public static Texture2D ClothTexture() { return ClothTexture(68); }

        /// <summary>
        /// The woven cloth, <paramref name="w"/> x 21 px (cached per width). The default is deep red with a gold trim
        /// (the Esc menu); <paramref name="pearl"/> is grey with a white pearl trim (the Party screen's Members mat).
        /// </summary>
        public static Texture2D ClothTexture(int w, bool pearl = false)
        {
            return ClothTexture(w, pearl ? ClothStyle.Pearl : ClothStyle.Red);
        }

        public static Texture2D ClothTexture(int w, ClothStyle style)
        {
            Texture2D cloth;
            int key = w * 4 + (int)style;
            if (cloths.TryGetValue(key, out cloth) && cloth != null) return cloth;
            const int h = ClothHeight;
            var pal = Palette(style);
            Color32 outline = pal[0], goldLit = pal[1], gold = pal[2], baseRed = pal[3], weave = pal[4], thread = pal[5];
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
                    Color32 c;
                    if (d == 0) c = outline;
                    else if (d == 2) c = (y == 2 || x == 2) ? goldLit : gold;   // the gold border, lit top-left
                    else if (d == 1 || d == 3) c = weave;
                    else
                    {
                        // A plain weave, with a row of gold diamonds down the middle.
                        c = ((x / 2 + y / 2) % 2 == 0) ? baseRed : thread;
                        if ((x + y) % 7 == 0) c = weave;
                        int cy = h / 2, dx = (x - 6) % 10 - 4;
                        if (x > 5 && x < w - 6 && Mathf.Abs(dx) + Mathf.Abs(y - cy) <= 2)
                            c = Mathf.Abs(dx) + Mathf.Abs(y - cy) == 2 ? gold : goldLit;
                    }
                    px[(h - 1 - y) * w + x] = c;
                }
            cloth = Make(w, h, px);
            cloths[key] = cloth;
            return cloth;
        }

        /// <summary>The rolled-up end seen side-on: shaded like a cylinder (light in the middle), with trim rims top and
        /// bottom (gold, or white pearl for <paramref name="pearl"/>).</summary>
        public static Texture2D RollTexture(bool pearl = false)
        {
            return RollTexture(pearl ? ClothStyle.Pearl : ClothStyle.Red);
        }

        public static Texture2D RollTexture(ClothStyle style)
        {
            Texture2D cached;
            if (rolls.TryGetValue(style, out cached) && cached != null) return cached;
            const int w = 9, h = 24;
            var pal = Palette(style);
            var shades = new[] { pal[0], pal[6], pal[3], pal[7], pal[8], pal[7], pal[3], pal[6], pal[0] };
            var gold = pal[2];
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = shades[x];
                    if ((y == 1 || y == h - 2) && x > 0 && x < w - 1) c = gold;
                    if (y == 0 || y == h - 1) c = shades[0];
                    px[y * w + x] = c;
                }
            var tex = Make(w, h, px);
            rolls[style] = tex;
            return tex;
        }

        static Texture2D Make(int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
