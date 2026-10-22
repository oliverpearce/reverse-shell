using System.Collections.Generic;
using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// Slay the Spire-style intent icons, drawn as 16x16 pixel art in code: sword (attack), up arrow (buff),
    /// down arrow (debuff), plus (heal). Each gets a 1px dark outline and simple top-left lighting.
    /// </summary>
    public static class IntentIcons
    {
        const int Size = 16;
        static readonly Dictionary<CardKind, Sprite> cache = new Dictionary<CardKind, Sprite>();

        public static Sprite For(CardKind kind)
        {
            Sprite sprite;
            if (cache.TryGetValue(kind, out sprite) && sprite != null) return sprite;

            var px = new Color[Size, Size]; // [x, y], y = 0 at the top
            switch (kind)
            {
                case CardKind.Attack: Sword(px); break;
                case CardKind.Buff: Arrow(px, true, new Color(1f, 0.8f, 0.3f)); break;
                case CardKind.Debuff: Arrow(px, false, new Color(0.7f, 0.45f, 0.95f)); break;
                default: Plus(px, new Color(0.4f, 0.9f, 0.5f)); break;
            }
            Outline(px, new Color(0.08f, 0.06f, 0.1f));

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    tex.SetPixel(x, Size - 1 - y, px[x, y]);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            cache[kind] = sprite;
            return sprite;
        }

        static void Sword(Color[,] px)
        {
            var blade = new Color(0.86f, 0.88f, 0.92f);
            var edge = new Color(1f, 1f, 1f);
            var shade = new Color(0.58f, 0.6f, 0.68f);
            var guard = new Color(0.95f, 0.72f, 0.25f);
            var grip = new Color(0.5f, 0.3f, 0.18f);

            // Blade: a chunky diagonal from the guard (5,10) up to the tip (13,2), lit edge on top.
            for (int i = 0; i <= 8; i++)
            {
                int x = 5 + i, y = 10 - i;
                Set(px, x, y, edge);
                Set(px, x + 1, y, i == 8 ? shade : blade);
                Set(px, x, y + 1, blade);
                if (i < 8) Set(px, x + 1, y + 1, shade);
            }
            // Crossguard: a thick bar across the blade's base.
            for (int i = -3; i <= 3; i++)
            {
                Set(px, 5 + i, 11 + i, guard);
                Set(px, 4 + i, 11 + i, guard);
            }
            // Grip and pommel.
            Set(px, 4, 12, grip);
            Set(px, 3, 13, grip);
            Set(px, 2, 13, grip);
            Set(px, 3, 14, grip);
            Set(px, 1, 14, guard);
            Set(px, 2, 15, guard);
            Set(px, 1, 15, guard);
        }

        static void Arrow(Color[,] px, bool up, Color color)
        {
            var light = Color.Lerp(color, Color.white, 0.45f);
            var dark = Color.Lerp(color, Color.black, 0.3f);
            for (int row = 0; row < 7; row++) // triangle head
            {
                int y = up ? 2 + row : 13 - row;
                for (int x = 7 - row; x <= 8 + row; x++)
                    Set(px, x, y, x <= 7 ? light : (x >= 8 + row - 1 ? dark : color));
            }
            for (int row = 0; row < 5; row++) // shaft
            {
                int y = up ? 9 + row : 6 - row;
                for (int x = 6; x <= 9; x++) Set(px, x, y, x == 6 ? light : (x == 9 ? dark : color));
            }
        }

        static void Plus(Color[,] px, Color color)
        {
            var light = Color.Lerp(color, Color.white, 0.45f);
            var dark = Color.Lerp(color, Color.black, 0.3f);
            for (int y = 2; y <= 13; y++)
                for (int x = 2; x <= 13; x++)
                {
                    bool vertical = x >= 6 && x <= 9, horizontal = y >= 6 && y <= 9;
                    if (!vertical && !horizontal) continue;
                    bool lit = (vertical && x == 6) || (horizontal && y == 6);
                    bool shadow = (vertical && x == 9 && !horizontal) || (horizontal && y == 9 && !vertical);
                    Set(px, x, y, lit ? light : (shadow ? dark : color));
                }
        }

        static void Outline(Color[,] px, Color outline)
        {
            var filled = new bool[Size, Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    filled[x, y] = px[x, y].a > 0f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (filled[x, y]) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            near = nx >= 0 && ny >= 0 && nx < Size && ny < Size && filled[nx, ny];
                        }
                    if (near) px[x, y] = outline;
                }
        }

        static void Set(Color[,] px, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[x, y] = c;
        }
    }
}
