using System;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>Display settings for cards.</summary>
    public static class CardSettings
    {
        /// <summary>Rank is always shown as stars in the card's top-left corner (the stars / pips option was removed
        /// from the pause menu; the pip layout code is kept).</summary>
        public static bool RankStars { get { return true; } }
    }

    /// <summary>
    /// Card rank as stacking stars: rank I has none, rank II one star, rank III two, and Ritual (ult) cards three.
    /// The star is the icon atlas's star (CardIcons.Star, from the Raven pack); without the atlas it is drawn in code
    /// as a smooth, anti-aliased gold star with a dark rim.
    /// </summary>
    public static class CardStars
    {
        static readonly Color StarGold = new Color(1f, 0.83f, 0.3f);
        static Sprite star;

        public static int CountFor(CardInstance card)
        {
            if (card.IsRitual) return 3;
            return Mathf.Clamp(card.Rank - 1, 0, 2);
        }

        /// <summary>A row of stars growing to the right from <paramref name="position"/> (its top-left corner).</summary>
        public static RectTransform Build(RectTransform parent, int count, float size, Vector2 anchor, Vector2 position)
        {
            float gap = size * 0.08f;
            var row = UIFactory.CreateRect("Stars", parent).Place(anchor, new Vector2(0f, 1f), position,
                new Vector2(Mathf.Max(0, count) * (size + gap), size));
            var atlasStar = CardIcons.Star;   // the Raven star (drawn in its own colors); the code star as a fallback
            for (int i = 0; i < count; i++)
            {
                var img = UIFactory.CreateImage(row, "Star", atlasStar != null ? Color.white : StarGold);
                img.sprite = atlasStar != null ? atlasStar : Sprite();
                img.raycastTarget = false;
                img.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (size + gap), 0f), new Vector2(size, size));
            }
            return row;
        }

        /// <summary>White star with a dark rim (tinted gold by the Image), 4x4 supersampled.</summary>
        static Sprite Sprite()
        {
            if (star != null) return star;
            const int res = 96;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[res * res];
            var rim = new Color(0.25f, 0.15f, 0.02f);
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float body = 0f, edge = 0f;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 4f) / res * 2f - 1f;
                            float v = (y + (sy + 0.5f) / 4f) / res * 2f - 1f;
                            if (InStar(u, v, 0.8f)) body += 1f / 16f;
                            else if (InStar(u, v, 0.98f)) edge += 1f / 16f;
                        }
                    float a = Mathf.Clamp01(body + edge);
                    if (a <= 0f) continue;
                    // Rim is darkened (the gold tint multiplies it toward brown); the body stays white -> gold.
                    var c = Color.Lerp(rim, Color.white, a > 0f ? body / a : 0f);
                    c.a = a;
                    px[y * res + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return star = UnityEngine.Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Five-pointed star (point up), outer radius r, inner radius 0.45 r, centered at the origin.</summary>
        static bool InStar(float x, float y, float r)
        {
            y += 0.06f; // optical centering
            float angle = Mathf.Atan2(x, y); // 0 = straight up
            float sector = Mathf.PI * 2f / 5f;
            float a = Mathf.Repeat(angle + sector / 2f, sector) - sector / 2f; // angle from the nearest point
            float inner = r * 0.45f;
            // Edge from the point (r, angle 0) to the inner vertex (inner, half sector), in polar form.
            var p0 = new Vector2(0f, r);
            var p1 = new Vector2(Mathf.Sin(sector / 2f) * inner, Mathf.Cos(sector / 2f) * inner);
            float d = Mathf.Sqrt(x * x + y * y);
            var q = new Vector2(Mathf.Sin(Mathf.Abs(a)) * d, Mathf.Cos(Mathf.Abs(a)) * d);
            // Inside if q is on the origin's side of the line p0 -> p1.
            float cross = (p1.x - p0.x) * (q.y - p0.y) - (p1.y - p0.y) * (q.x - p0.x);
            return cross <= 0f;
        }
    }
}
