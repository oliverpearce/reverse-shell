using System;

namespace Combat.View
{
    /// <summary>Linear RGB color used by the backdrop painter (kept engine-free so it can be unit-tested and previewed).</summary>
    public struct Rgb
    {
        public float R, G, B;

        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static Rgb operator *(Rgb a, float k) { return new Rgb(a.R * k, a.G * k, a.B * k); }
        public static Rgb operator +(Rgb a, Rgb b) { return new Rgb(a.R + b.R, a.G + b.G, a.B + b.B); }
        public static Rgb Lerp(Rgb a, Rgb b, float t) { return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t); }
        public float Luma { get { return R * 0.3f + G * 0.59f + B * 0.11f; } }
    }

    public interface IBackdropAtlas
    {
        /// <summary>Samples art pixel (u, v) of tile (col, row); v and row count from the top. Returns false if transparent.</summary>
        bool Sample(int col, int row, int u, int v, out Rgb color);
    }

    public class BackdropParams
    {
        public int Seed = 1999;
        public Rgb SkyTop = new Rgb(0.03f, 0.02f, 0.07f);
        public Rgb SkyHorizon = new Rgb(0.26f, 0.07f, 0.26f);
        public Rgb[] Neon = { new Rgb(1f, 0.18f, 0.55f), new Rgb(0.18f, 0.95f, 1f), new Rgb(0.7f, 1f, 0.23f), new Rgb(1f, 0.6f, 0.24f) };
        public float StreetLine = 0.37f;
        public float FacadeBrightness = 0.45f;
    }

    /// <summary>
    /// Paints a night-time neon street as pixel art: sky haze, far skyline with lit windows, tiled building
    /// facades with glowing signs, sidewalk + wet road with reflections, lamp posts, vignette.
    /// Tiles come from an atlas of 16x16-art-pixel cells (CyberCity layout, see WorldMeshBuilder).
    /// </summary>
    public static class BackdropPainterCore
    {
        public const int Tile = 16;

        public static Rgb[] Paint(int w, int h, BackdropParams p, IBackdropAtlas atlas)
        {
            var px = new Rgb[w * h];
            var rng = new Random(p.Seed);
            int street = (int)(h * p.StreetLine);

            Sky(px, w, street, p);
            FarSkyline(px, w, street, p, rng);
            Facades(px, w, street, p, rng, atlas);

            var upper = (Rgb[])px.Clone(); // for reflections
            Ground(px, w, h, street, rng);
            Reflections(px, upper, w, h, street);
            Lamps(px, w, h, street, p, rng);
            Finish(px, w, h);
            return px;
        }

        // ------------------------------------------------------------------ Layers

        static void Sky(Rgb[] px, int w, int street, BackdropParams p)
        {
            for (int y = 0; y < street; y++)
            {
                float t = (float)Math.Pow(y / (float)street, 1.8);
                var c = Rgb.Lerp(p.SkyTop, p.SkyHorizon, t);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
        }

        static void FarSkyline(Rgb[] px, int w, int street, BackdropParams p, Random rng)
        {
            var far = new Rgb(0.06f, 0.045f, 0.11f);
            int x = -rng.Next(0, 10);
            while (x < w)
            {
                int bw = rng.Next(14, 42);
                int top = street - rng.Next(30, 90);
                for (int xx = x; xx < x + bw; xx++)
                {
                    if (xx < 0 || xx >= w) continue;
                    for (int yy = Math.Max(0, top); yy < street; yy++)
                    {
                        float haze = Smooth((yy - (street - 34)) / 34f) * 0.5f;
                        var c = Rgb.Lerp(far, p.SkyHorizon, haze);
                        if ((xx - x) % 4 == 1 && (yy - top) % 5 == 2 && rng.NextDouble() < 0.13)
                            c = rng.NextDouble() < 0.5 ? new Rgb(0.85f, 0.65f, 0.35f) * 0.6f : Pick(p.Neon, rng) * 0.6f;
                        px[yy * w + xx] = c;
                    }
                }
                if (rng.NextDouble() < 0.35)
                {
                    int ax = x + rng.Next(2, Math.Max(3, bw - 2));
                    int height = rng.Next(4, 13);
                    for (int yy = Math.Max(0, top - height); yy < top; yy++) Set(px, w, ax, yy, far);
                    Set(px, w, ax, Math.Max(0, top - height), new Rgb(0.95f, 0.15f, 0.25f));
                }
                x += Math.Max(6, bw + rng.Next(-4, 4));
            }
        }

        static void Facades(Rgb[] px, int w, int street, BackdropParams p, Random rng, IBackdropAtlas atlas)
        {
            int x = -rng.Next(0, 12);
            while (x < w)
            {
                int tilesW = rng.Next(2, 6), tilesH = rng.Next(2, 6);
                int bw = tilesW * Tile, top = street - tilesH * Tile;
                bool brick = rng.NextDouble() < 0.25;
                int ci = rng.Next(0, 4);

                for (int ty = 0; ty < tilesH; ty++)
                {
                    for (int tx = 0; tx < tilesW; tx++)
                    {
                        int col, row;
                        bool window = false;
                        if (ty == tilesH - 1) { col = 4 + ci; row = 4; }            // shopfront
                        else if (brick) { col = 4 + ci; row = 1; }
                        else { col = ci; row = rng.NextDouble() < 0.15 ? 3 : 2; window = true; }

                        bool lit = window && rng.NextDouble() < 0.5;
                        var litColor = rng.NextDouble() < 0.6 ? new Rgb(1f, 0.78f, 0.45f) : Pick(p.Neon, rng);
                        DrawTile(px, w, x + tx * Tile, top + ty * Tile, col, row, atlas, p.FacadeBrightness, lit, litColor);
                    }
                }
                for (int xx = x; xx < x + bw; xx++) Set(px, w, xx, top, new Rgb(0.28f, 0.24f, 0.36f)); // roof edge

                if (rng.NextDouble() < 0.7) Sign(px, w, x, top, bw, tilesH, p, rng);
                x += bw + rng.Next(0, 12);
            }
        }

        static void DrawTile(Rgb[] px, int w, int ox, int oy, int col, int row, IBackdropAtlas atlas, float brightness, bool lit, Rgb litColor)
        {
            for (int v = 0; v < Tile; v++)
            {
                for (int u = 0; u < Tile; u++)
                {
                    int x = ox + u, y = oy + v;
                    if (x < 0 || x >= w || y < 0) continue;
                    Rgb c;
                    if (atlas == null || !atlas.Sample(col, row, u, v, out c))
                    {
                        // Fallback facade: dark panel with a window grid.
                        c = (u % 8 >= 2 && u % 8 <= 5 && v % 8 >= 2 && v % 8 <= 5) ? new Rgb(0.3f, 0.35f, 0.45f) : new Rgb(0.12f, 0.1f, 0.18f);
                    }
                    var shaded = c * brightness;
                    if (c.Luma > 0.7f)
                        shaded = lit ? Rgb.Lerp(shaded, litColor, 0.75f) : new Rgb(0.13f, 0.15f, 0.24f) + shaded * 0.12f; // lit vs dark glass
                    px[y * w + x] = shaded;
                }
            }
        }

        static void Sign(Rgb[] px, int w, int bx, int top, int bw, int tilesH, BackdropParams p, Random rng)
        {
            int sw = Math.Min(bw - 4, rng.Next(10, 23)), sh = rng.Next(4, 7);
            if (sw < 6) return;
            int sx = bx + rng.Next(2, Math.Max(3, bw - sw - 1));
            int sy = top + rng.Next(3, Math.Max(4, tilesH * Tile - Tile - sh));
            var c = Pick(p.Neon, rng);

            Glow(px, w, sx + sw / 2, sy + sh / 2, sw / 2 + 7, c, 0.28f);
            for (int y = sy; y < sy + sh; y++)
                for (int x = sx; x < sx + sw; x++)
                {
                    bool border = y == sy || y == sy + sh - 1 || x == sx || x == sx + sw - 1;
                    Set(px, w, x, y, border ? c * 0.75f : Clamp(c * 1.25f + new Rgb(0.2f, 0.2f, 0.2f)));
                }
            // Fake lettering: dark notches along the middle.
            int mid = sy + sh / 2;
            for (int x = sx + 2; x < sx + sw - 2; x += 2)
                if (rng.NextDouble() < 0.55) Set(px, w, x, mid, c * 0.35f);
        }

        /// <summary>Sidewalk slabs, a curb, and one wide wet asphalt road with a single dashed center line.</summary>
        static void Ground(Rgb[] px, int w, int h, int street, Random rng)
        {
            const int walk = 18;
            var slab = new Rgb(0.2f, 0.19f, 0.25f);
            var seam = new Rgb(0.11f, 0.1f, 0.15f);
            var asphalt = new Rgb(0.085f, 0.08f, 0.115f);
            int lane = street + walk + (h - street - walk) / 3;

            for (int y = street; y < h; y++)
            {
                bool sidewalk = y < street + walk;
                for (int x = 0; x < w; x++)
                {
                    Rgb c;
                    if (sidewalk)
                    {
                        int row = (y - street) / 9;
                        bool isSeam = (y - street) % 9 == 0 || ((x + row * 8) % 16 == 0);
                        c = isSeam ? seam : slab;
                    }
                    else
                    {
                        float grain = (float)((x * 73 + y * 151) % 7) / 7f * 0.02f;
                        c = asphalt + new Rgb(grain, grain, grain);
                        if ((y == lane || y == lane + 1) && (x / 10) % 2 == 0) c = new Rgb(0.42f, 0.33f, 0.12f);
                    }
                    px[y * w + x] = c;
                }
            }
            for (int x = 0; x < w; x++)
            {
                Set(px, w, x, street, new Rgb(0.03f, 0.02f, 0.05f));
                Set(px, w, x, street + walk - 1, new Rgb(0.34f, 0.32f, 0.42f));
                Set(px, w, x, street + walk, new Rgb(0.05f, 0.04f, 0.07f));
            }
            // Puddles: slightly lighter, smooth patches that catch reflections.
            for (int i = 0; i < 9; i++)
                Ellipse(px, w, h, rng.Next(0, w), rng.Next(street + walk + 6, h), rng.Next(14, 40), rng.Next(2, 5), new Rgb(0.09f, 0.1f, 0.16f), 0.8f);
        }

        static void Reflections(Rgb[] px, Rgb[] upper, int w, int h, int street)
        {
            for (int y = street + 1; y < h; y++)
            {
                int d = y - street;
                int sy = street - 1 - (int)(d * 0.8f);
                if (sy < 0) break;
                int ox = (int)Math.Round(Math.Sin(y * 0.8) * 1.3 + Math.Sin(y * 0.23) * 0.8);
                float fade = 1f - d / (float)(h - street);
                for (int x = 0; x < w; x++)
                {
                    int sx = Math.Min(w - 1, Math.Max(0, x + ox));
                    var src = upper[sy * w + sx];
                    var c = px[y * w + x];
                    float bright = Math.Max(0f, src.Luma - 0.45f) * 1.4f;
                    px[y * w + x] = c * 0.82f + src * (0.22f * fade) + src * (bright * 0.5f * fade);
                }
            }
        }

        static void Lamps(Rgb[] px, int w, int h, int street, BackdropParams p, Random rng)
        {
            var post = new Rgb(0.05f, 0.04f, 0.07f);
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                int lx = (int)((i + 0.5f) * w / count) + rng.Next(-20, 21);
                int baseY = street + 14, topY = street - 48;
                var c = Pick(p.Neon, rng);

                Glow(px, w, lx + 3, topY + 1, 12, c, 0.32f);
                Ellipse(px, w, h, lx + 3, baseY + 1, 22, 4, c, 0.22f);
                for (int y = topY; y <= baseY; y++) Set(px, w, lx, y, post);
                for (int x = lx; x <= lx + 3; x++) Set(px, w, x, topY, post);
                for (int x = lx + 1; x <= lx + 6; x++) Set(px, w, x, topY + 1, Clamp(c * 1.3f + new Rgb(0.3f, 0.3f, 0.3f)));
            }
        }

        static void Finish(Rgb[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)h - 0.45f) * 2f;
                float bottom = y > h * 0.72f ? 1f - (y - h * 0.72f) / (h * 0.28f) * 0.45f : 1f; // keep the hand area calm
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)w - 0.5f) * 2f;
                    float vignette = Math.Max(0.35f, 1f - 0.32f * (nx * nx + ny * ny));
                    px[y * w + x] = Clamp(px[y * w + x] * (vignette * bottom));
                }
            }
        }

        // ------------------------------------------------------------------ Helpers

        static void Glow(Rgb[] px, int w, int cx, int cy, int radius, Rgb c, float strength)
        {
            int h = px.Length / w;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                    if (d >= 1f) continue;
                    float k = (1f - d) * (1f - d) * strength;
                    px[y * w + x] = px[y * w + x] + c * k;
                }
        }

        static void Ellipse(Rgb[] px, int w, int h, int cx, int cy, int rx, int ry, Rgb c, float strength)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                    float d = dx * dx + dy * dy;
                    if (d >= 1f) continue;
                    px[y * w + x] = px[y * w + x] + c * ((1f - d) * strength);
                }
        }

        static void Set(Rgb[] px, int w, int x, int y, Rgb c)
        {
            if (x < 0 || x >= w || y < 0 || y * w + x >= px.Length) return;
            px[y * w + x] = c;
        }

        static Rgb Pick(Rgb[] colors, Random rng) { return colors[rng.Next(colors.Length)]; }

        static Rgb Clamp(Rgb c) { return new Rgb(Math.Min(1f, Math.Max(0f, c.R)), Math.Min(1f, Math.Max(0f, c.G)), Math.Min(1f, Math.Max(0f, c.B))); }

        static float Smooth(float t)
        {
            t = Math.Min(1f, Math.Max(0f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
