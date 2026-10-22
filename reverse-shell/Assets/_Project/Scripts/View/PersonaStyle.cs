using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The game's UI look, in the style of Persona 5 but lilac: lilac pages with halftone dots and burst rays,
    /// slanted black / white plates with hard offset shadows, and big tilted Anton type.
    /// Shared by the battle HUD, pause menu, result screen, party menu and overworld HUD.
    /// </summary>
    public static class PersonaStyle
    {
        public static readonly Color Lilac = new Color(0.69f, 0.55f, 0.95f);
        public static readonly Color LilacDeep = new Color(0.33f, 0.18f, 0.58f);
        public static readonly Color LilacLight = new Color(0.88f, 0.8f, 1f);
        public static readonly Color Black = new Color(0.04f, 0.03f, 0.05f);
        public static readonly Color White = new Color(0.98f, 0.97f, 0.99f);
        /// <summary>Card-target preview highlight.</summary>
        public static readonly Color TargetOrange = new Color(1f, 0.6f, 0.16f);

        static Sprite slant;
        static Texture2D halftone, rays;

        /// <summary>A slanted plate (parallelogram) image.</summary>
        public static Image Slant(Transform parent, string name, Color color)
        {
            var img = UIFactory.CreateImage(parent, name, color);
            img.sprite = SlantSprite();
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Slanted plate with a hard offset shadow behind it. Returns the plate.</summary>
        public static Image Plate(RectTransform parent, string name, Color color, Color shadow, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, float angle, float shadowOffset = 12f)
        {
            var s = Slant(parent, name + "Shadow", shadow);
            s.rectTransform.Place(anchor, pivot, position + new Vector2(shadowOffset, -shadowOffset), size);
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var p = Slant(parent, name, color);
            p.rectTransform.Place(anchor, pivot, position, size);
            p.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return p;
        }

        /// <summary>Big Anton text with a hard shadow (a second copy behind it).</summary>
        public static Text Title(Transform parent, string name, string text, int size, Color color, Color shadow, TextAnchor anchor, float shadowOffset = 6f)
        {
            var back = UIFactory.CreateText(parent, name + "Shadow", text, size, shadow, anchor);
            back.font = UIFactory.DisplayFont;
            var front = UIFactory.CreateText(parent, name, text, size, color, anchor);
            front.font = UIFactory.DisplayFont;
            back.gameObject.AddComponent<FollowRect>().Init(front.rectTransform, new Vector2(shadowOffset, -shadowOffset));
            return front;
        }

        /// <summary>
        /// A slanted Persona button: black plate with white Anton text (primary = lilac plate, black text),
        /// a lilac shadow, and the shared hover effect.
        /// </summary>
        public static Button Button(Transform parent, string name, string label, UnityAction onClick, bool primary, int fontSize = 40)
        {
            var root = UIFactory.CreateRect(name, parent);
            var shadow = Slant(root, "Shadow", primary ? Black : LilacDeep);
            shadow.rectTransform.Fill();
            shadow.rectTransform.anchoredPosition = new Vector2(10f, -10f);
            var plate = Slant(root, "Plate", primary ? Lilac : Black);
            plate.rectTransform.Fill();
            plate.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            if (onClick != null) button.onClick.AddListener(onClick);
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(() =>
            {
                if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            });
            var text = UIFactory.CreateText(root, "Label", label, fontSize, primary ? Black : White, TextAnchor.MiddleCenter);
            text.font = UIFactory.DisplayFont;
            text.rectTransform.Fill(8f);
            root.gameObject.AddComponent<ButtonHover>();
            return button;
        }

        /// <summary>Tiled halftone dots over a rect.</summary>
        public static RawImage Halftone(RectTransform parent, Color color, float cell = 22f)
        {
            var dots = new GameObject("Halftone", typeof(RectTransform)).AddComponent<RawImage>();
            dots.rectTransform.SetParent(parent, false);
            dots.rectTransform.Fill();
            dots.texture = HalftoneTexture();
            dots.color = color;
            dots.raycastTarget = false;
            var r = parent.rect;
            float w = r.width > 0f ? r.width : UIFactory.ReferenceResolution.x, h = r.height > 0f ? r.height : UIFactory.ReferenceResolution.y;
            dots.uvRect = new Rect(0f, 0f, w / cell, h / cell);
            return dots;
        }

        /// <summary>Burst rays (turn it slowly with <see cref="Spin"/>).</summary>
        public static RawImage Burst(RectTransform parent, Vector2 position, float size, Color color)
        {
            var img = new GameObject("Burst", typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(parent, false);
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            img.texture = RayTexture();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ Generated textures

        public static Sprite SlantSprite()
        {
            if (slant != null) return slant;
            const int w = 256, h = 64, skew = 18;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float left = skew * (1f - y / (float)(h - 1)); // leans forward
                float right = w - skew * (y / (float)(h - 1));
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(x + 0.5f - left) * Mathf.Clamp01(right - x - 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return slant = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Texture2D HalftoneTexture()
        {
            if (halftone != null) return halftone;
            const int size = 16;
            halftone = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    halftone.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(3.2f - d)));
                }
            halftone.Apply();
            return halftone;
        }

        public static Texture2D RayTexture()
        {
            if (rays != null) return rays;
            const int size = 256, count = 14;
            rays = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float angle = Mathf.Atan2(y - size / 2f, x - size / 2f);
                    rays.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Sin(angle * count) * 6f)));
                }
            rays.Apply();
            return rays;
        }
    }

    /// <summary>Keeps this rect on top of another with an offset (hard text shadows that follow layout and rotation).</summary>
    public class FollowRect : MonoBehaviour
    {
        RectTransform target, self;
        Vector2 offset;
        Text text, targetText;

        public void Init(RectTransform follow, Vector2 by)
        {
            target = follow;
            offset = by;
            self = (RectTransform)transform;
            text = GetComponent<Text>();
            targetText = follow.GetComponent<Text>();
            transform.SetSiblingIndex(follow.GetSiblingIndex()); // drawn just behind it
            LateUpdate();
        }

        void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }
            self.anchorMin = target.anchorMin;
            self.anchorMax = target.anchorMax;
            self.pivot = target.pivot;
            self.sizeDelta = target.sizeDelta;
            self.anchoredPosition = target.anchoredPosition + offset;
            self.localRotation = target.localRotation;
            self.localScale = target.localScale;
            if (text != null && targetText != null)
            {
                if (text.text != targetText.text) text.text = targetText.text;
                text.fontSize = targetText.fontSize;
                text.alignment = targetText.alignment;
                text.resizeTextForBestFit = targetText.resizeTextForBestFit;
                text.resizeTextMinSize = targetText.resizeTextMinSize;
                text.resizeTextMaxSize = targetText.resizeTextMaxSize;
                text.supportRichText = targetText.supportRichText;
            }
        }
    }
}
