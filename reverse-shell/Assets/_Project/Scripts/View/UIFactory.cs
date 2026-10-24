using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>Helpers for building placeholder uGUI from code.</summary>
    public static class UIFactory
    {
        /// <summary>Overall UI size. Lower = smaller UI (canvases lay out at 1920x1080 / UIScale and shrink to fit the screen).</summary>
        public const float UIScale = 0.8f;

        public static Vector2 ReferenceResolution { get { return new Vector2(1920f, 1080f) / UIScale; } }
        // UI typography: one pixel font for everything, monogram (CC0, datagoblin), so the UI matches the pixel art.
        // It has a single weight: "bold" and "italic" requests render plain, since synthesized styles smear pixel glyphs.
        // Imported as Hinted Raster (Editor/World/PixelArtImporter) and its glyph texture is point-filtered at runtime.
        static Font pixelFont;

        /// <summary>
        /// monogram's capitals are 7 px of a 16 px em, much smaller than the old fonts at the same size, so every size
        /// handed to <see cref="CreateText"/>, <see cref="BestFit"/>, <see cref="FitRich"/> and <see cref="Size"/> is scaled by this.
        /// </summary>
        public const float FontScale = 1.6f;

        /// <summary>A layout font size in the game's old units, as the actual monogram size.</summary>
        public static int Px(int size) { return Mathf.Max(1, Mathf.RoundToInt(size * FontScale)); }

        /// <summary>A rich-text size tag ("&lt;size=N&gt;") scaled like every other font size.</summary>
        public static string Size(int size) { return "<size=" + Px(size) + ">"; }

        /// <summary>monogram-extended: the game's only font.</summary>
        public static Font PixelFont
        {
            get
            {
                if (pixelFont != null) return pixelFont;
                pixelFont = Resources.Load<Font>("Fonts/monogram-extended");
                if (pixelFont == null) return pixelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                Crisp(pixelFont);
                Font.textureRebuilt += f => { if (f == pixelFont) Crisp(f); };
                return pixelFont;
            }
        }

        static void Crisp(Font f)
        {
            if (f.material != null && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Point;
        }

        /// <summary>The default UI font.</summary>
        public static Font Font { get { return PixelFont; } }

        /// <summary>Names, headings, buttons (anything created with FontStyle.Bold).</summary>
        public static Font MediumFont { get { return PixelFont; } }

        /// <summary>Big statement words.</summary>
        public static Font BoldFont { get { return PixelFont; } }

        /// <summary>Body copy that has to stay readable at small sizes (card text, descriptions).</summary>
        public static Font BodyFont { get { return PixelFont; } }

        /// <summary>Headings in the <see cref="InkStyle"/> UI (was a serif; now the same pixel font).</summary>
        public static Font SerifFont { get { return PixelFont; } }

        /// <summary>The display face for big labels (dialogue speaker tags, card words).</summary>
        public static Font DisplayFont { get { return PixelFont; } }

        static readonly System.Text.RegularExpressions.Regex Number =
            new System.Text.RegularExpressions.Regex(@"[+-]?\d+(?:\.\d+)?%?");

        /// <summary>Wraps every number / percentage in a bigger size and an accent color (rich text).</summary>
        public static string EmphasizeNumbers(string text, int numberSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return Number.Replace(text, m => Size(numberSize) + "<color=#" + hex + ">" + m.Value + "</color></size>");
        }

        /// <summary>
        /// Like BestFit, but for rich text whose &lt;size&gt; tags must scale too: tries font sizes from max down to
        /// min and keeps the first whose text fits the rect's height. <paramref name="build"/> makes the text for a size.
        /// </summary>
        public static void FitRich(Text t, System.Func<int, string> build, int min, int max)
        {
            t.resizeTextForBestFit = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            float height = t.rectTransform.rect.height, width = t.rectTransform.rect.width;
            for (int size = max; size >= min; size--)
            {
                t.fontSize = Px(size);
                t.text = build(size);
                if (width <= 0f || height <= 0f) return;
                var settings = t.GetGenerationSettings(new Vector2(width, 0f));
                float needed = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;
                if (needed <= height) return;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchors to a single point.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretches to fill the parent with an inset (negative inset grows outward).</summary>
        public static RectTransform Fill(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Stretches horizontally, pinned to the parent's top edge.</summary>
        public static RectTransform TopStrip(this RectTransform rt, float top, float height, float sideInset = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sideInset, -top - height);
            rt.offsetMax = new Vector2(-sideInset, -top);
            return rt;
        }

        public static Image CreateImage(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text CreateText(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            // One pixel font with one weight: styles are accepted for call-site intent but always render plain.
            t.font = Font;
            t.fontStyle = FontStyle.Normal;
            t.text = text;
            t.fontSize = Px(size);
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text BestFit(this Text t, int min, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Px(min);
            t.resizeTextMaxSize = Px(max);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color color, UnityAction onClick, int fontSize = 26)
        {
            var img = CreateImage(parent, name, color, true);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(onClick);
            // Keyboard keys are game controls (arrows move the target ring, Space inspects), so buttons never
            // keep keyboard focus: otherwise Space would re-press the last clicked button.
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(() =>
            {
                if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            });
            img.gameObject.AddComponent<ButtonHover>();
            var text = CreateText(img.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.Fill();
            return button;
        }

        /// <summary>Destroys all children; deactivates them first so layout groups ignore them this frame.</summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        public static Color Darken(Color c, float amount)
        {
            return Color.Lerp(c, Color.black, amount);
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static string Roman(int rank)
        {
            switch (rank)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                default: return rank.ToString();
            }
        }
    }
}
