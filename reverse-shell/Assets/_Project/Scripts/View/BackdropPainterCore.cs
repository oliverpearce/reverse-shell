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
        public Rgb SkyTop = new Rgb(0.52f, 0.66f, 0.8f);
        public Rgb SkyHorizon = new Rgb(0.88f, 0.88f, 0.8f);
        /// <summary>Leaf colors, dark to light.</summary>
        public Rgb[] Foliage = { new Rgb(0.14f, 0.27f, 0.16f), new Rgb(0.21f, 0.42f, 0.2f), new Rgb(0.33f, 0.57f, 0.25f), new Rgb(0.55f, 0.75f, 0.34f) };
        public float StreetLine = 0.42f;
        public float FacadeBrightness = 0.9f;
    }

    /// <summary>
    /// Paints a sunlit city ruin reclaimed by nature as pixel art: hazy sky with clouds, broken far towers,
    /// tiled mossy facades with hanging vines, trees, cracked overgrown ground and slanting sun rays.
    /// Tiles come from an atlas of 16x16-art-pixel cells (ruins atlas layout, see WorldMeshBuilder).
    /// </summary>
    public static class BackdropPainterCore
    {
        public const int Tile = 16;

        public static Rgb[] Paint(int w, int h, BackdropParams p, IBackdropAtlas atlas)
        {
            var px = new Rgb[w * h];
            var rng = new Random(p.Seed);
            int street = (int)(h * p.StreetLine);

            Sky(px, w, street, p, rng);
            FarRuins(px, w, street, p, rng);
            Facades(px, w, street, p, rng, atlas);
            Ground(px, w, h, street, p, rng);
            Trees(px, w, h, street, p, rng);
            SunRays(px, w, h, street);
            Finish(px, w, h);
            return px;
        }

        // ------------------------------------------------------------------ Layers

        static void Sky(Rgb[] px, int w, int street, BackdropParams p, Random rng)
        {
            for (int y = 0; y < street; y++)
            {
                float t = (float)Math.Pow(y / (float)street, 1.4);
                var c = Rgb.Lerp(p.SkyTop, p.SkyHorizon, t);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            // Soft, flat-bottomed clouds.
            var cloud = new Rgb(0.96f, 0.96f, 0.92f);
            for (int i = 0; i < 6; i++)
            {
                int cx = rng.Next(0, w), cy = rng.Next(10, Math.Max(11, street / 2));
                int puffs = rng.Next(3, 6);
                for (int k = 0; k < puffs; k++)
                {
                    int r = rng.Next(5, 12);
                    int ox = cx + k * rng.Next(6, 11) - puffs * 4;
                    for (int y = cy - r; y <= cy; y++)
                        for (int x = ox - r; x <= ox + r; x++)
                        {
                            if (x < 0 || x >= w || y < 0) continue;
                            if ((x - ox) * (x - ox) + (y - cy) * (y - cy) > r * r) continue;
                            float shade = y > cy - 2 ? 0.92f : 1f;
                            px[y * w + x] = Rgb.Lerp(px[y * w + x], cloud * shade, 0.75f);
                        }
                }
            }
        }

        /// <summary>Two layers of distant broken towers fading into the haze, some crowned with green.</summary>
        static void FarRuins(Rgb[] px, int w, int street, BackdropParams p, Random rng)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                float haze = layer == 0 ? 0.62f : 0.4f;
                var body = Rgb.Lerp(new Rgb(0.34f, 0.4f, 0.44f), p.SkyHorizon, haze);
                var window = Rgb.Lerp(new Rgb(0.18f, 0.22f, 0.26f), p.SkyHorizon, haze);
                var green = Rgb.Lerp(p.Foliage[1], p.SkyHorizon, haze);
                int x = -rng.Next(0, 10);
                while (x < w)
                {
                    int bw = rng.Next(16, 40);
                    int height = layer == 0 ? rng.Next(70, 130) : rng.Next(40, 90);
                    int top = street - height;
                    bool leaning = rng.NextDouble() < 0.15;
                    for (int xx = x; xx < x + bw; xx++)
                    {
                        if (xx < 0 || xx >= w) continue;
                        // Broken, jagged roofline.
                        // Collapsed in chunks rather than crenellated: a few wide steps, plus an optional slope.
                        int jag = (int)(Math.Abs(Math.Sin((xx - x) / 6 * 1.7 + x)) * 9) + (leaning ? (xx - x) / 3 : 0);
                        int yTop = Math.Max(0, top + jag);
                        for (int yy = yTop; yy < street; yy++)
                        {
                            var c = body;
                            if ((xx - x) % 5 >= 2 && (xx - x) % 5 <= 3 && (yy - top) % 7 >= 2 && (yy - top) % 7 <= 4) c = window;
                            if (yy < yTop + 3 && rng.NextDouble() < 0.7) c = green;
                            px[yy * w + xx] = c;
                        }
                        // Vines trailing down from the broken top.
                        if (rng.NextDouble() < 0.18)
                        {
                            int len = rng.Next(6, 30);
                            for (int yy = yTop; yy < Math.Min(street, yTop + len); yy++) px[yy * w + xx] = green;
                        }
                    }
                    x += bw + rng.Next(-4, 14);
                }
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
                        // The top row crumbles away: some tiles are just missing.
                        if (ty == 0 && tilesH > 2 && rng.NextDouble() < 0.35) continue;
                        int col, row;
                        if (ty == tilesH - 1) { col = 4 + ci; row = 4; }          // ruined storefront
                        else if (brick) { col = 4 + ci; row = 1; }
                        else { col = ci; row = rng.NextDouble() < 0.4 ? 3 : 2; }  // windows, often ivy-covered
                        DrawTile(px, w, x + tx * Tile, top + ty * Tile, col, row, atlas, p.FacadeBrightness, p);
                    }
                }
                // Grass on the broken roofline, vines spilling down the face.
                for (int xx = x; xx < x + bw; xx++)
                {
                    int roof = FirstSolid(px, w, xx, top, street, p);
                    if (roof < 0) continue;
                    Set(px, w, xx, roof, Pick(p.Foliage, rng));
                    if (rng.NextDouble() < 0.6) Set(px, w, xx, roof - 1, p.Foliage[rng.Next(1, p.Foliage.Length)]);
                    if (rng.NextDouble() < 0.14)
                    {
                        int len = rng.Next(8, Math.Max(9, (street - roof) * 3 / 4));
                        for (int yy = roof; yy < roof + len; yy++)
                        {
                            Set(px, w, xx, yy, yy % 3 == 0 ? p.Foliage[2] : p.Foliage[1]);
                            if (rng.NextDouble() < 0.3) Set(px, w, xx + (rng.Next(2) * 2 - 1), yy, p.Foliage[3]);
                        }
                    }
                }
                x += bw + rng.Next(0, 14);
            }
        }

        /// <summary>First row (from the top) in this column that is no longer sky, or -1.</summary>
        static int FirstSolid(Rgb[] px, int w, int x, int from, int to, BackdropParams p)
        {
            if (x < 0 || x >= w) return -1;
            for (int y = Math.Max(0, from); y < to; y++)
            {
                var c = px[y * w + x];
                var sky = Rgb.Lerp(p.SkyTop, p.SkyHorizon, (float)Math.Pow(y / (float)to, 1.4));
                if (Math.Abs(c.R - sky.R) + Math.Abs(c.G - sky.G) + Math.Abs(c.B - sky.B) > 0.25f) return y;
            }
            return -1;
        }

        static void DrawTile(Rgb[] px, int w, int ox, int oy, int col, int row, IBackdropAtlas atlas, float brightness, BackdropParams p)
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
                        // Fallback facade: weathered concrete with dark window holes.
                        c = (u % 8 >= 2 && u % 8 <= 5 && v % 8 >= 2 && v % 8 <= 5) ? new Rgb(0.09f, 0.1f, 0.11f) : new Rgb(0.55f, 0.55f, 0.52f);
                    }
                    px[y * w + x] = Rgb.Lerp(c * brightness, p.SkyHorizon, 0.12f); // a touch of aerial haze
                }
            }
        }

        /// <summary>Cracked pavement band, then an overgrown road dissolving into grass toward the viewer.</summary>
        static void Ground(Rgb[] px, int w, int h, int street, BackdropParams p, Random rng)
        {
            const int walk = 16;
            var slab = new Rgb(0.6f, 0.59f, 0.55f);
            var seam = new Rgb(0.4f, 0.4f, 0.37f);
            var asphalt = new Rgb(0.42f, 0.42f, 0.4f);

            for (int y = street; y < h; y++)
            {
                bool sidewalk = y < street + walk;
                float depth = (y - street) / (float)(h - street);
                for (int x = 0; x < w; x++)
                {
                    Rgb c;
                    if (sidewalk)
                    {
                        int row = (y - street) / 8;
                        bool isSeam = (y - street) % 8 == 0 || ((x + row * 8) % 16 == 0);
                        c = isSeam ? (rng.NextDouble() < 0.5 ? p.Foliage[1] : seam) : slab;
                    }
                    else
                    {
                        float grain = (float)((x * 73 + y * 151) % 7) / 7f * 0.03f;
                        c = asphalt + new Rgb(grain, grain, grain);
                    }
                    px[y * w + x] = c;
                }
            }
            for (int x = 0; x < w; x++)
            {
                Set(px, w, x, street, new Rgb(0.25f, 0.26f, 0.22f));
                Set(px, w, x, street + walk - 1, new Rgb(0.7f, 0.69f, 0.64f));
                Set(px, w, x, street + walk, new Rgb(0.2f, 0.21f, 0.2f));
            }
            // Grass reclaiming the road: big soft patches plus cracks, denser toward the viewer.
            for (int i = 0; i < 44; i++)
            {
                int cy = rng.Next(street + walk + 2, h);
                float depth = (cy - street) / (float)(h - street);
                Patch(px, w, h, rng.Next(0, w), cy, rng.Next(14, 34) + (int)(depth * 40), rng.Next(3, 6) + (int)(depth * 8), p, rng);
            }
            for (int i = 0; i < 7; i++)
            {
                int x = rng.Next(0, w), y = street + walk + 1;
                while (y < h)
                {
                    Set(px, w, x, y, p.Foliage[1]);
                    if (rng.NextDouble() < 0.5) Set(px, w, x + 1, y, p.Foliage[2]);
                    y++;
                    x += rng.Next(-1, 2);
                }
            }
            // Grass blades along the curb.
            for (int x = 0; x < w; x += rng.Next(1, 4))
            {
                int len = rng.Next(2, 6);
                for (int k = 0; k < len; k++) Set(px, w, x, street + walk - 1 - k, p.Foliage[Math.Min(3, 1 + k)]);
            }
        }

        static void Patch(Rgb[] px, int w, int h, int cx, int cy, int rx, int ry, BackdropParams p, Random rng)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                    float d = dx * dx + dy * dy;
                    if (d >= 1f || (d > 0.6f && rng.NextDouble() < 0.6)) continue; // ragged edge
                    double roll = rng.NextDouble();
                    px[y * w + x] = p.Foliage[roll < 0.55 ? 1 : (roll < 0.9 ? 2 : 3)];
                }
        }

        /// <summary>A few trees standing between the buildings and the street.</summary>
        static void Trees(Rgb[] px, int w, int h, int street, BackdropParams p, Random rng)
        {
            var bark = new Rgb(0.27f, 0.21f, 0.16f);
            int count = 3;
            for (int i = 0; i < count; i++)
            {
                int tx = (int)((i + 0.5f) * w / count) + rng.Next(-40, 41);
                int baseY = street + 6;
                int trunkTop = street - rng.Next(40, 70);
                for (int y = trunkTop; y <= baseY; y++)
                    for (int x = tx - 2; x <= tx + 2; x++)
                        Set(px, w, x, y, x == tx - 2 ? bark * 0.7f : (x == tx + 2 ? bark * 1.3f : bark));

                // Canopy: overlapping blobs, lit from the upper left.
                int blobs = rng.Next(5, 8);
                for (int b = 0; b < blobs; b++)
                {
                    int cx = tx + rng.Next(-26, 27), cy = trunkTop + rng.Next(-18, 8), r = rng.Next(12, 22);
                    for (int y = cy - r; y <= cy + r; y++)
                        for (int x = cx - r; x <= cx + r; x++)
                        {
                            if (x < 0 || x >= w || y < 0 || y >= h) continue;
                            float dx = x - cx, dy = y - cy;
                            float d = (dx * dx + dy * dy) / (r * r);
                            if (d > 1f) continue;
                            float light = -(dx + dy) / (2f * r) + 0.5f; // 1 at upper left, 0 at lower right
                            int idx = Math.Min(3, Math.Max(0, (int)(light * 3.2f + (rng.NextDouble() - 0.5) * 1.2)));
                            px[y * w + x] = p.Foliage[idx];
                        }
                }
                // Soft shadow under the tree.
                Shade(px, w, h, tx, baseY + 2, 30, 4, 0.75f);
            }
        }

        static void SunRays(Rgb[] px, int w, int h, int street)
        {
            var warm = new Rgb(1f, 0.95f, 0.78f);
            for (int r = 0; r < 4; r++)
            {
                int start = 40 + r * 110;
                int width = 16 + r * 6;
                for (int y = 0; y < h; y++)
                {
                    int cx = start + (int)(y * 0.55f);
                    float fade = 1f - y / (float)h;
                    for (int x = cx - width; x <= cx + width; x++)
                    {
                        if (x < 0 || x >= w) continue;
                        float across = 1f - Math.Abs(x - cx) / (float)width;
                        px[y * w + x] = px[y * w + x] + warm * (across * across * 0.08f * fade);
                    }
                }
            }
        }

        static void Finish(Rgb[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)h - 0.45f) * 2f;
                float bottom = y > h * 0.72f ? 1f - (y - h * 0.72f) / (h * 0.28f) * 0.35f : 1f; // keep the hand area calm
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)w - 0.5f) * 2f;
                    float vignette = Math.Max(0.5f, 1f - 0.2f * (nx * nx + ny * ny));
                    px[y * w + x] = Clamp(px[y * w + x] * (vignette * bottom));
                }
            }
        }

        // ------------------------------------------------------------------ Helpers

        static void Shade(Rgb[] px, int w, int h, int cx, int cy, int rx, int ry, float k)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy >= 1f) continue;
                    px[y * w + x] = px[y * w + x] * k;
                }
        }

        static void Set(Rgb[] px, int w, int x, int y, Rgb c)
        {
            if (x < 0 || x >= w || y < 0 || y * w + x >= px.Length) return;
            px[y * w + x] = c;
        }

        static Rgb Pick(Rgb[] colors, Random rng) { return colors[rng.Next(colors.Length)]; }

        static Rgb Clamp(Rgb c) { return new Rgb(Math.Min(1f, Math.Max(0f, c.R)), Math.Min(1f, Math.Max(0f, c.G)), Math.Min(1f, Math.Max(0f, c.B))); }
    }
}
