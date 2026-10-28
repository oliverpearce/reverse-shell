using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// The enemy intent card's plate (the user's pick, "D"): a dark ink card with rounded corners, a metal rim for its
    /// rank (bronze I, silver II, gold III) and a fine inner hairline in the same metal. Drawn once per rank into a
    /// texture at 2x (smooth edges), cached. The type icon goes on top (UnitView).
    /// </summary>
    public static class IntentCards
    {
        const int W = 144, H = 192;   // 2x the 72 x 96 card
        static readonly Color Ink = new Color(0.106f, 0.102f, 0.094f, 0.92f);
        static readonly Color[] Metal =
        {
            new Color(0.69f, 0.48f, 0.27f), new Color(0.79f, 0.82f, 0.86f), new Color(0.94f, 0.75f, 0.29f),
        };
        static readonly Sprite[] plates = new Sprite[3];

        public static Sprite Plate(int rank)
        {
            int r = Mathf.Clamp(rank, 1, 3) - 1;
            if (plates[r] != null) return plates[r];
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var metal = Metal[r];
            var hair = new Color(metal.r, metal.g, metal.b, 0.6f);
            var pixels = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // The card (inset 12 x 8) with a 12 px corner, its 8 px rim on the edge; the hairline 14 px further in.
                    float outer = RoundRect(p, new Vector2(12f, 8f), new Vector2(W - 24f, H - 16f), 12f);
                    float inner = RoundRect(p, new Vector2(26f, 22f), new Vector2(W - 52f, H - 44f), 6f);
                    var c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, Ink, Mathf.Clamp01(0.5f - outer));
                    c = Over(c, hair, Mathf.Clamp01(1.5f - Mathf.Abs(inner)));
                    c = Over(c, metal, Mathf.Clamp01(4.5f - Mathf.Abs(outer)));
                    pixels[y * W + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            plates[r] = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            plates[r].name = "IntentPlate_" + (r + 1);
            return plates[r];
        }

        /// <summary>Signed distance from <paramref name="p"/> to a rounded rectangle (negative inside).</summary>
        static float RoundRect(Vector2 p, Vector2 min, Vector2 size, float radius)
        {
            var half = size * 0.5f;
            var q = new Vector2(Mathf.Abs(p.x - (min.x + half.x)), Mathf.Abs(p.y - (min.y + half.y))) - half + Vector2.one * radius;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary><paramref name="top"/> (with <paramref name="coverage"/>) over <paramref name="under"/>.</summary>
        static Color Over(Color under, Color top, float coverage)
        {
            float a = top.a * coverage;
            if (a <= 0f) return under;
            float outA = a + under.a * (1f - a);
            var rgb = (new Vector3(top.r, top.g, top.b) * a + new Vector3(under.r, under.g, under.b) * under.a * (1f - a)) / Mathf.Max(outA, 1e-5f);
            return new Color(rgb.x, rgb.y, rgb.z, outA);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { for (int i = 0; i < plates.Length; i++) plates[i] = null; }
    }
}
