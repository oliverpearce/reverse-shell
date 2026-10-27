using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>What a profile poster shows about someone.</summary>
    public class PosterInfo
    {
        public string Name;
        /// <summary>Hostile = enemy "TARGET PROFILE" with a WANTED stamp; otherwise a "PERSONNEL FILE", CLEARED.</summary>
        public bool Hostile;
        /// <summary>The big threat-index number (like a crime coefficient). Negative hides it.</summary>
        public int Index = -1;
        public Color Accent = Color.white;
    }

    /// <summary>
    /// A holographic "virtual wanted poster" inspired by Psycho-Pass's public safety scans: dark teal glass, a faint
    /// grid, a header with an ID code, a cyan-tinted portrait behind scanlines and corner brackets (with an
    /// occasional glitch), the name in caps, a threat index with a colored gauge, a barcode and a stamp.
    /// The compact version (dialogue) keeps the header, portrait, name strip and stamp.
    /// Build() returns the portrait Image; the caller assigns its sprite (or animator) and size.
    /// </summary>
    public static class ProfilePoster
    {
        public static readonly Color Holo = new Color(0.35f, 0.95f, 1f);
        static readonly Color Glass = new Color(0.02f, 0.06f, 0.08f, 0.96f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.2f);
        static readonly Color Lethal = new Color(1f, 0.25f, 0.3f);

        static Texture2D grid, scan;

        /// <summary>Threat index color: calm cyan under 100, amber under 300, red from 300 (enemies only).</summary>
        public static Color IndexColor(int index, bool hostile)
        {
            if (!hostile || index < 100) return Holo;
            return index < 300 ? Amber : Lethal;
        }

        /// <summary>A stable ID code from the name, e.g. "ID 4F-2719".</summary>
        public static string IdCode(string name)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in name ?? "") h = h * 31 + c;
                h &= 0x7fffffff;
                return "ID " + (h % 256).ToString("X2") + "-" + (h / 256 % 10000).ToString("0000");
            }
        }

        public static Image Build(RectTransform root, PosterInfo info, bool compact)
        {
            var glass = UIFactory.CreateImage(root, "Poster", Glass);
            glass.rectTransform.Fill();
            var gridImg = Tiled(root, "Grid", GridTexture(), UIFactory.WithAlpha(Holo, 0.08f), 28f);

            float w = root.rect.width, h = root.rect.height;
            float header = compact ? 26f : 34f;
            float footer = compact ? 40f : 150f;

            // Header: file type on the left, ID on the right.
            var head = UIFactory.CreateImage(root, "Header", UIFactory.WithAlpha(Holo, 0.16f));
            head.rectTransform.TopStrip(0f, header);
            var kind = UIFactory.CreateText(head.transform, "Kind", UIText.Get(info.Hostile ? "poster.target" : "poster.personnel"),
                compact ? 12 : 15, Holo, TextAnchor.MiddleLeft, FontStyle.Bold);
            kind.rectTransform.Fill(8f);
            var id = UIFactory.CreateText(head.transform, "Id", IdCode(info.Name), compact ? 12 : 15, UIFactory.WithAlpha(Holo, 0.8f), TextAnchor.MiddleRight);
            id.rectTransform.Fill(8f);

            // Portrait window.
            var window = UIFactory.CreateRect("PortraitWindow", root);
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.offsetMin = new Vector2(10f, footer);
            window.offsetMax = new Vector2(-10f, -header - 6f);
            window.gameObject.AddComponent<RectMask2D>();
            UIFactory.CreateImage(window, "Backlight", UIFactory.WithAlpha(info.Accent, 0.12f)).rectTransform.Fill();
            var portrait = UIFactory.CreateImage(window, "Portrait", new Color(0.78f, 1f, 1f));
            portrait.raycastTarget = false;
            portrait.gameObject.AddComponent<PosterGlitch>();
            var scanImg = Tiled(window, "Scanlines", ScanTexture(), UIFactory.WithAlpha(Holo, 0.22f), 3f);
            scanImg.gameObject.AddComponent<ScrollUV>().speed = new Vector2(0f, -0.6f);
            Brackets(window, compact ? 14f : 22f, Holo);

            // Stamp across the portrait's corner.
            var stamp = UIFactory.CreateText(window, "Stamp", UIText.Get(info.Hostile ? "poster.wanted" : "poster.cleared"), compact ? 22 : 40,
                info.Hostile ? Lethal : Holo, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            stamp.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -8f), new Vector2(compact ? 130f : 220f, compact ? 30f : 52f));
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var stampBox = stamp.gameObject.AddComponent<Outline>();
            stampBox.effectColor = new Color(0f, 0f, 0f, 0.6f);

            if (compact)
            {
                var strip = UIFactory.CreateImage(root, "NameStrip", UIFactory.WithAlpha(Holo, 0.16f));
                strip.rectTransform.anchorMin = Vector2.zero;
                strip.rectTransform.anchorMax = new Vector2(1f, 0f);
                strip.rectTransform.pivot = new Vector2(0.5f, 0f);
                strip.rectTransform.offsetMin = new Vector2(0f, 6f);
                strip.rectTransform.offsetMax = new Vector2(0f, 6f + 30f);
                var n = UIFactory.CreateText(strip.transform, "Name", (info.Name ?? "").ToUpperInvariant(), 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(10, 20);
                n.rectTransform.Fill(4f);
                return portrait;
            }

            // Name.
            var name = UIFactory.CreateText(root, "Name", (info.Name ?? "").ToUpperInvariant(), 40, Color.white, TextAnchor.LowerLeft, FontStyle.Bold).BestFit(18, 40);
            name.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, footer - 52f), new Vector2(w - 28f, 48f));

            // Threat index: label, big number, gauge.
            if (info.Index >= 0)
            {
                var color = IndexColor(info.Index, info.Hostile);
                var label = UIFactory.CreateText(root, "IndexLabel", UIText.Get(info.Hostile ? "poster.threat" : "poster.combat"), 14, UIFactory.WithAlpha(Holo, 0.85f), TextAnchor.LowerLeft, FontStyle.Bold);
                label.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(14f, 62f), new Vector2(160f, 20f));
                var value = UIFactory.CreateText(root, "Index", info.Index.ToString(), 44, color, TextAnchor.LowerRight, FontStyle.BoldAndItalic);
                value.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 54f), new Vector2(160f, 50f));

                var track = UIFactory.CreateImage(root, "Gauge", UIFactory.WithAlpha(Holo, 0.15f));
                track.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(14f, 44f), new Vector2(w - 28f, 6f));
                var fill = UIFactory.CreateImage(track.transform, "Fill", color);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(info.Index / 400f), 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                foreach (float mark in new[] { 100f, 300f })
                {
                    var tick = UIFactory.CreateImage(track.transform, "Tick", UIFactory.WithAlpha(Color.white, 0.6f));
                    tick.rectTransform.Place(new Vector2(mark / 400f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 12f));
                }
            }

            Barcode(root, info.Name, new Vector2(14f, 12f), new Vector2(w - 28f, 22f));
            return portrait;
        }

        // ------------------------------------------------------------------ Pieces

        static void Brackets(RectTransform parent, float size, Color color)
        {
            const float t = 3f;
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                float sx = corner.x < 0.5f ? 1f : -1f, sy = corner.y < 0.5f ? 1f : -1f;
                var a = UIFactory.CreateImage(parent, "Bracket", color);
                a.rectTransform.Place(corner, new Vector2(corner.x, corner.y), new Vector2(sx * 4f, sy * 4f), new Vector2(size, t));
                var b = UIFactory.CreateImage(parent, "Bracket", color);
                b.rectTransform.Place(corner, new Vector2(corner.x, corner.y), new Vector2(sx * 4f, sy * 4f), new Vector2(t, size));
            }
        }

        static void Barcode(RectTransform root, string seed, Vector2 position, Vector2 size)
        {
            var rng = new System.Random((seed ?? "").GetHashCode());
            float x = 0f;
            while (x < size.x - 4f)
            {
                float bar = rng.Next(1, 5);
                if (rng.NextDouble() < 0.62)
                {
                    var img = UIFactory.CreateImage(root, "Bar", UIFactory.WithAlpha(Holo, 0.55f));
                    img.rectTransform.Place(Vector2.zero, Vector2.zero, position + new Vector2(x, 0f), new Vector2(bar, size.y));
                }
                x += bar + rng.Next(1, 3);
            }
        }

        static RawImage Tiled(RectTransform parent, string name, Texture2D tex, Color color, float tile)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(parent, false);
            img.rectTransform.Fill();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            var r = parent.rect;
            img.uvRect = new Rect(0f, 0f, Mathf.Max(1f, r.width / tile), Mathf.Max(1f, r.height / tile));
            return img;
        }

        static Texture2D GridTexture()
        {
            if (grid != null) return grid;
            const int size = 16;
            grid = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    grid.SetPixel(x, y, new Color(1f, 1f, 1f, x == 0 || y == 0 ? 1f : 0f));
            grid.Apply();
            return grid;
        }

        static Texture2D ScanTexture()
        {
            if (scan != null) return scan;
            scan = new Texture2D(1, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            scan.SetPixel(0, 0, new Color(1f, 1f, 1f, 1f));
            scan.SetPixel(0, 1, new Color(1f, 1f, 1f, 0f));
            scan.SetPixel(0, 2, new Color(1f, 1f, 1f, 0f));
            scan.Apply();
            return scan;
        }
    }

    /// <summary>Scrolls a RawImage's UVs (moving scanlines). Unscaled time.</summary>
    public class ScrollUV : MonoBehaviour
    {
        public Vector2 speed;
        RawImage image;

        void Awake() { image = GetComponent<RawImage>(); }

        void Update()
        {
            if (image == null) return;
            var r = image.uvRect;
            r.position += speed * Time.unscaledDeltaTime;
            image.uvRect = r;
        }
    }

    /// <summary>Hologram glitch: every few seconds the portrait jumps sideways and flickers for a split second.</summary>
    public class PosterGlitch : MonoBehaviour
    {
        RectTransform rt;
        Graphic graphic;
        Vector2 home;
        bool homed;
        float next, until;

        void Awake()
        {
            rt = (RectTransform)transform;
            graphic = GetComponent<Graphic>();
            next = Time.unscaledTime + Random.Range(1f, 3f);
        }

        void Update()
        {
            if (!homed)
            {
                home = rt.anchoredPosition; // the caller places the portrait after Build()
                homed = true;
            }
            float now = Time.unscaledTime;
            if (now >= next)
            {
                until = now + Random.Range(0.06f, 0.14f);
                next = now + Random.Range(2.5f, 5f);
            }
            bool glitching = now < until;
            rt.anchoredPosition = home + (glitching ? new Vector2(Random.Range(-6f, 6f), 0f) : Vector2.zero);
            if (graphic != null)
            {
                var c = graphic.color;
                c.a = glitching ? Random.Range(0.45f, 0.85f) : 1f;
                graphic.color = c;
            }
        }

        /// <summary>Call after moving the portrait so the glitch returns to the new spot.</summary>
        public void Rehome() { homed = false; }
    }
}
