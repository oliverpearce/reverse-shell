using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The game's UI look, in the spirit of Reverse: 1999: quiet ink panels and cream paper, thin gold hairlines,
    /// a double rule with corner ticks for hero frames, serif headings (Cormorant Garamond) over clean M PLUS text,
    /// and gold for numbers. Straight edges only: no slants, tilts, halftone or burst rays.
    /// Shared by the battle HUD, pause menu, result screen, overworld HUD and the character menu.
    /// </summary>
    public static class InkStyle
    {
        public static readonly Color Ink = new Color(0.106f, 0.102f, 0.094f);              // #1B1A18
        public static readonly Color InkPanel = new Color(0.106f, 0.102f, 0.094f, 0.88f);
        public static readonly Color Smoke = new Color(0.07f, 0.065f, 0.06f, 0.72f);
        public static readonly Color Paper = new Color(0.925f, 0.898f, 0.827f);           // #ECE5D3
        public static readonly Color PaperDim = new Color(0.925f, 0.898f, 0.827f, 0.62f);
        public static readonly Color Gold = new Color(0.761f, 0.627f, 0.38f);             // #C2A061
        public static readonly Color GoldBright = new Color(0.9f, 0.78f, 0.52f);
        public static readonly Color Rule = new Color(0.761f, 0.627f, 0.38f, 0.55f);
        public static readonly Color Muted = new Color(0.68f, 0.65f, 0.6f);
        public static readonly Color Crimson = new Color(0.72f, 0.24f, 0.24f);
        /// <summary>Card-target preview highlight.</summary>
        public static readonly Color TargetOrange = new Color(1f, 0.6f, 0.16f);

        static Texture2D grain;

        /// <summary>A plain straight panel (ink by default).</summary>
        public static Image Panel(Transform parent, string name, Color color, bool raycast = false)
        {
            return UIFactory.CreateImage(parent, name, color, raycast);
        }

        /// <summary>A hairline rectangle inside <paramref name="rt"/>, inset by <paramref name="inset"/>.</summary>
        public static RectTransform Frame(RectTransform rt, Color color, float thickness = 2f, float inset = 0f)
        {
            var frame = UIFactory.CreateRect("Frame", rt).Fill(inset);
            Edge(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);
            Edge(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
            Edge(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);
            Edge(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);
            return frame;
        }

        static void Edge(RectTransform parent, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax, Color color)
        {
            var img = UIFactory.CreateImage(parent, "Edge", color);
            img.rectTransform.anchorMin = min;
            img.rectTransform.anchorMax = max;
            img.rectTransform.offsetMin = offMin;
            img.rectTransform.offsetMax = offMax;
        }

        /// <summary>The hero frame: an outer gold hairline, a fainter inner one, and small L ticks at the corners.</summary>
        public static void HeroFrame(RectTransform rt, Color color, float gap = 8f)
        {
            Frame(rt, color, 2f);
            Frame(rt, UIFactory.WithAlpha(color, color.a * 0.45f), 1f, gap);
            const float tick = 18f, t = 2f;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var dir = new Vector2(corner.x > 0f ? -1f : 1f, corner.y > 0f ? -1f : 1f);
                var h = UIFactory.CreateImage(rt, "Tick", color);
                h.rectTransform.Place(corner, corner, new Vector2(-dir.x * 6f, -dir.y * 6f), new Vector2(tick, t));
                h.rectTransform.pivot = new Vector2(dir.x > 0f ? 0f : 1f, 0.5f);
                var v = UIFactory.CreateImage(rt, "Tick", color);
                v.rectTransform.Place(corner, corner, new Vector2(-dir.x * 6f, -dir.y * 6f), new Vector2(t, tick));
                v.rectTransform.pivot = new Vector2(0.5f, dir.y > 0f ? 0f : 1f);
            }
        }

        /// <summary>A thin horizontal rule.</summary>
        public static Image Line(Transform parent, string name, Color color)
        {
            return UIFactory.CreateImage(parent, name, color);
        }

        /// <summary>A serif heading (Cormorant Garamond). No hard shadow: R1999 headings sit cleanly on the panel.</summary>
        public static Text Title(Transform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            var t = UIFactory.CreateText(parent, name, text, size, color, anchor);
            t.font = UIFactory.SerifFont;
            return t;
        }

        /// <summary>A small letter-spaced caps label ("T U R N"), the quiet captions under headings.</summary>
        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            return UIFactory.CreateText(parent, name, Spaced(text), size, color, anchor, FontStyle.Bold);
        }

        /// <summary>"Turn" -> "T U R N" (word gaps stay wider).</summary>
        public static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder();
            foreach (var word in text.ToUpperInvariant().Split(' '))
            {
                if (word.Length == 0) continue;
                if (sb.Length > 0) sb.Append("   ");
                for (int i = 0; i < word.Length; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(word[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// A straight button: an ink plate with a gold hairline and serif label (primary = gold plate, ink label),
        /// plus the shared hover effect. Buttons never keep keyboard focus.
        /// </summary>
        public static Button Button(Transform parent, string name, string label, UnityAction onClick, bool primary, int fontSize = 40)
        {
            var root = UIFactory.CreateRect(name, parent);
            var plate = UIFactory.CreateImage(root, "Plate", primary ? Gold : InkPanel, true);
            plate.rectTransform.Fill();
            Frame(plate.rectTransform, primary ? GoldBright : Rule, 2f);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(() =>
            {
                if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            });
            var text = UIFactory.CreateText(root, "Label", label, fontSize, primary ? Ink : Paper, TextAnchor.MiddleCenter);
            text.font = UIFactory.SerifFont;
            text.rectTransform.Fill(8f);
            root.gameObject.AddComponent<ButtonHover>();
            return button;
        }

        /// <summary>Subtle paper grain over a rect (tiled noise).</summary>
        public static RawImage Grain(RectTransform parent, Color color, float cell = 96f)
        {
            var img = new GameObject("Grain", typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(parent, false);
            img.rectTransform.Fill();
            img.texture = GrainTexture();
            img.color = color;
            img.raycastTarget = false;
            var size = UIFactory.ReferenceResolution;
            img.uvRect = new Rect(0f, 0f, size.x / cell, size.y / cell);
            return img;
        }

        /// <summary>A soft vertical gradient (top color to bottom color), for page backgrounds.</summary>
        public static RawImage Gradient(RectTransform parent, Color top, Color bottom)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            var img = new GameObject("Gradient", typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(parent, false);
            img.rectTransform.Fill();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        static Texture2D GrainTexture()
        {
            if (grain != null) return grain;
            const int size = 96;
            grain = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var rng = new System.Random(7);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    grain.SetPixel(x, y, new Color(1f, 1f, 1f, (float)rng.NextDouble() * (float)rng.NextDouble()));
            grain.Apply();
            return grain;
        }
    }
}
