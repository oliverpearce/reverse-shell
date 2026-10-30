using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The overworld UI's look, after NieR:Automata's menus (PlatinumGames' design notes), on dark panels: dark grey,
    /// slightly see-through panels with light text, flat and clean with straight thin rules, colour used sparingly (a
    /// drab red-orange accent for the selection marker, keys and warnings), a lighter grey bar for the selected row,
    /// a very slight edge darkening like a curved old screen (<see cref="Vignette"/>), and soft, slightly springy
    /// motion when panels open (<see cref="SoftOpen"/>). The battle UI and the cards keep <see cref="InkStyle"/>.
    /// </summary>
    public static class NierStyle
    {
        // Dark grey panels, a little see-through, with light text (the user preferred dark to NieR's beige).
        public static readonly Color PanelGray = new Color(0.13f, 0.13f, 0.13f, 0.82f);
        public static readonly Color RowGray = new Color(0.17f, 0.17f, 0.17f, 0.82f);
        public static readonly Color ScreenWash = new Color(0.07f, 0.07f, 0.07f, 0.8f);
        public static readonly Color Text = new Color(0.88f, 0.85f, 0.77f);
        public static readonly Color TextDim = new Color(0.88f, 0.85f, 0.77f, 0.5f);
        public static readonly Color SelectBar = new Color(0.33f, 0.32f, 0.3f, 0.92f);
        public static readonly Color Accent = new Color(0.72f, 0.36f, 0.24f);
        public static readonly Color Rule = new Color(0.88f, 0.85f, 0.77f, 0.32f);
        /// <summary>HUD text straight on the world (party names), with a dark edge.</summary>
        public static readonly Color HudText = new Color(0.92f, 0.89f, 0.8f);

        /// <summary>A flat dark grey panel with a thin light rule along its top and bottom.</summary>
        public static Image Panel(Transform parent, string name, Color? color = null, bool raycast = false)
        {
            var panel = UIFactory.CreateImage(parent, name, color ?? PanelGray, raycast);
            Rules(panel.rectTransform);
            return panel;
        }

        /// <summary>Thin light rules along the top and bottom edges of <paramref name="rt"/>, inset a little.</summary>
        public static void Rules(RectTransform rt, float inset = 10f)
        {
            for (int i = 0; i < 2; i++)
            {
                var line = UIFactory.CreateImage(rt, i == 0 ? "RuleTop" : "RuleBottom", Rule);
                var a = line.rectTransform;
                a.anchorMin = new Vector2(0f, i == 0 ? 1f : 0f);
                a.anchorMax = new Vector2(1f, i == 0 ? 1f : 0f);
                a.pivot = new Vector2(0.5f, i == 0 ? 1f : 0f);
                a.sizeDelta = new Vector2(-inset * 2f, 2f);
                a.anchoredPosition = new Vector2(0f, i == 0 ? -5f : 5f);
                line.raycastTarget = false;
            }
        }

        /// <summary>A small accent square, the marker beside a selected row.</summary>
        public static Image Marker(Transform parent, string name = "Marker")
        {
            var m = UIFactory.CreateImage(parent, name, Accent);
            m.raycastTarget = false;
            return m;
        }

        static Texture2D vignette;

        /// <summary>A full-screen, very slight darkening toward the edges and corners (the curved-monitor touch).</summary>
        public static RawImage Vignette(RectTransform parent, float strength = 0.28f)
        {
            if (vignette == null)
            {
                const int size = 64;
                vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx * 0.8f + dy * dy) - 0.55f) / 0.75f);
                        vignette.SetPixel(x, y, new Color(0.12f, 0.1f, 0.08f, d * d));
                    }
                vignette.Apply();
            }
            var go = new GameObject("Vignette", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.texture = vignette;
            img.color = new Color(1f, 1f, 1f, strength);
            img.raycastTarget = false;
            img.rectTransform.Fill();
            return img;
        }

        /// <summary>A panel's opening: it fades in and rises a little, overshooting softly, over a quarter second
        /// (unscaled time, so it works with the world paused).</summary>
        public static IEnumerator SoftOpen(CanvasGroup group, RectTransform moving, float rise = 26f, float seconds = 0.28f)
        {
            if (group == null) yield break;
            Vector2 home = moving != null ? moving.anchoredPosition : Vector2.zero;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = t / seconds;
                group.alpha = Tween.OutCubic(p);
                if (moving != null) moving.anchoredPosition = home - new Vector2(0f, rise * (1f - Tween.OutBack(p)));
                yield return null;
            }
            group.alpha = 1f;
            if (moving != null) moving.anchoredPosition = home;
        }
    }
}
