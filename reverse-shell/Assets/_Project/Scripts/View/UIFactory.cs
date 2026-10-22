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
        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font;
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
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text BestFit(this Text t, int min, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = max;
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
