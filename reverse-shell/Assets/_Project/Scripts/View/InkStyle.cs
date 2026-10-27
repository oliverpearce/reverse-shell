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
    /// <summary>
    /// Draws a frame's four corner brackets (two arms each) and keeps the arms at most a third of the side, so small
    /// frames (a key badge) still read as brackets and large ones don't grow lines along their sides.
    /// </summary>
    public class CornerBrackets : MonoBehaviour
    {
        const float MinThickness = 3f;
        readonly RectTransform[] arms = new RectTransform[8];
        float thickness, arm;
        Vector2 lastSize = new Vector2(-1f, -1f);

        public void Init(Color color, float thick, float armLength)
        {
            // At least 3 canvas units, so both arms (the vertical ones too) still cover a whole pixel at 4:3.
            thickness = Mathf.Max(MinThickness, thick);
            arm = armLength;
            var rt = (RectTransform)transform;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                for (int k = 0; k < 2; k++)
                {
                    var img = UIFactory.CreateImage(rt, k == 0 ? "BracketH" : "BracketV", color);
                    var a = img.rectTransform;
                    a.anchorMin = a.anchorMax = corner;
                    a.pivot = corner;              // grows inward from the corner
                    a.anchoredPosition = Vector2.zero;
                    arms[i * 2 + k] = a;
                }
            }
            Apply();
        }

        void OnRectTransformDimensionsChange() { if (arms[0] != null) Apply(); }

        void Apply()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size == lastSize) return;
            lastSize = size;
            float h = size.x > 0f ? Mathf.Min(arm, size.x / 3f) : arm;
            float v = size.y > 0f ? Mathf.Min(arm, size.y / 3f) : arm;
            for (int i = 0; i < 4; i++)
            {
                arms[i * 2].sizeDelta = new Vector2(h, thickness);
                arms[i * 2 + 1].sizeDelta = new Vector2(thickness, v);
            }
        }
    }

    public static class InkStyle
    {
        public static readonly Color Ink = new Color(0.106f, 0.102f, 0.094f);              // #1B1A18
        public static readonly Color InkPanel = new Color(0.106f, 0.102f, 0.094f, 0.88f);
        /// <summary>A hovered / selected plate: the ink panel lifted to a lighter grey (menus grow it a little too).</summary>
        public static readonly Color HoverGray = new Color(0.34f, 0.33f, 0.315f, 0.95f);
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

        /// <summary>
        /// The frame around a panel, card or selection, inside <paramref name="rt"/> and inset by
        /// <paramref name="inset"/>: only its four corners, drawn as brackets (no lines along the sides). Each bracket
        /// arm is <see cref="BracketArm"/> long (longer for thick frames) but never more than a third of the side.
        /// </summary>
        public static RectTransform Frame(RectTransform rt, Color color, float thickness = 2f, float inset = 0f)
        {
            var frame = UIFactory.CreateRect("Frame", rt).Fill(inset);
            frame.gameObject.AddComponent<CornerBrackets>().Init(color, thickness, Mathf.Max(BracketArm, thickness * 7f));
            return frame;
        }

        /// <summary>A bracket arm's length for a 2 px frame.</summary>
        public const float BracketArm = 22f;

        /// <summary>The hero frame: one set of corner brackets, a little bolder than a plain frame
        /// (<paramref name="gap"/> is kept for callers; there is no inner frame any more).</summary>
        public static void HeroFrame(RectTransform rt, Color color, float gap = 8f)
        {
            Frame(rt, color, 4f);
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

        /// <summary>A caps caption ("ITEMS", "DECK"). Not letter-spaced: spaced-out pixel capitals were hard to read.</summary>
        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            return UIFactory.CreateText(parent, name, string.IsNullOrEmpty(text) ? text : text.ToUpperInvariant(), size, color, anchor, FontStyle.Bold);
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

        /// <summary>Close buttons' size (square).</summary>
        public const float CloseSize = 104f;

        /// <summary>
        /// A straight button with the Go button's look: the translucent ink plate with a hairline and the label, plus
        /// the shared hover effect. Buttons never keep keyboard focus.
        /// </summary>
        /// <param name="primary">Unused: primary buttons used to be gold; every button now looks like Go.</param>
        public static Button Button(Transform parent, string name, string label, UnityAction onClick, bool primary, int fontSize = 40,
            bool chevrons = false)
        {
            var root = UIFactory.CreateRect(name, parent);
            var plate = UIFactory.CreateImage(root, "Plate", InkPanel, true);
            plate.rectTransform.Fill();
            Frame(plate.rectTransform, Rule, 2f);
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
            var text = UIFactory.CreateText(root, "Label", label, fontSize, Paper, TextAnchor.MiddleCenter);
            text.font = UIFactory.SerifFont;
            text.rectTransform.Fill(8f);
            root.gameObject.AddComponent<ButtonHover>();
            // Every button has the Go button's feel: a soft glow on hover and a punch + flash on press (chevrons only
            // when asked for: the Go-sized buttons).
            GoButtonFx.Add(button, chevrons && !string.IsNullOrEmpty(label));
            return button;
        }

        /// <summary>
        /// A square close button with the close X icon (CardIcons.Close), dead centre.
        /// </summary>
        public static Button CloseButton(Transform parent, UnityAction onClick)
        {
            var button = Button(parent, "Close", "", onClick, false);
            var label = button.transform.Find("Label");
            if (label != null) Object.Destroy(label.gameObject);
            var icon = UIFactory.CreateImage(button.transform, "Icon", Color.white);
            icon.sprite = CardIcons.Close;
            icon.preserveAspect = true;
            icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            return button;
        }

        /// <summary>
        /// A "?" badge at the right end of a settings row: hovering it calls <paramref name="onEnter"/> (show the setting's
        /// help) and leaving calls <paramref name="onExit"/>.
        /// </summary>
        public static Image HelpBadge(RectTransform row, System.Action onEnter, System.Action onExit)
        {
            var badge = UIFactory.CreateImage(row, "Help", Ink, true);
            badge.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(50f, 50f));
            Frame(badge.rectTransform, GoldBright, 2f);
            var q = UIFactory.CreateText(badge.transform, "Q", "?", 32, GoldBright, TextAnchor.MiddleCenter);
            q.rectTransform.Fill();
            var trigger = badge.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (onEnter != null) onEnter(); });
            trigger.triggers.Add(enter);
            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (onExit != null) onExit(); });
            trigger.triggers.Add(exit);
            return badge;
        }

        /// <summary>The box a settings page shows a setting's help in: an ink panel with a gold hairline and readable
        /// text. Starts hidden; set the returned Text's text and activate its parent panel to show it.</summary>
        public static Text HelpPanel(Transform parent, Vector2 topCenter, Vector2 size)
        {
            var panel = Panel(parent, "HelpPanel", InkPanel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), topCenter, size);
            Frame(panel.rectTransform, Rule, 2f);
            var text = UIFactory.CreateText(panel.transform, "Text", "", 26, Paper, TextAnchor.UpperLeft);
            text.rectTransform.Fill(18f);
            panel.gameObject.SetActive(false);
            return text;
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
