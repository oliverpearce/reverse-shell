using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Modern, Blue Archive-style lock-on graphics: smooth segmented rings that spin like a loading circle.
    /// The red target reticle marks the selected enemy; the white focus ring (sized to fit around it) marks
    /// the unit under the mouse or arrow-key cursor. Each ring sits on a soft dark shadow (contrast on bright
    /// backgrounds) and carries a blurred colored glow that spins with its segments.
    /// Textures are drawn in code with anti-aliased edges; radii leave room inside the texture for the glow.
    /// </summary>
    public static class TargetReticle
    {
        public static readonly Color LockRed = new Color(1f, 0.27f, 0.32f);
        static readonly Color FocusGlow = new Color(0.75f, 0.92f, 1f, 0.8f);
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        const int Res = 256;
        const float GlowBlur = 9f; // texture pixels

        static Sprite focusOuter, focusOuterGlow, focusInner, focusShadow;
        static Sprite lockArcs, lockArcsGlow, lockTicks, lockShadow;

        /// <summary>White focus ring: three long arcs (outer, spinning) plus a faint dashed inner ring (counter-spinning).</summary>
        public static Reticle CreateFocus(RectTransform parent, Vector2 anchor, Vector2 position, float size)
        {
            return CreateFocus(parent, anchor, position, size, Color.white, FocusGlow, "FocusRing");
        }

        /// <summary>The same ring in another color (e.g. the orange card-target preview).</summary>
        public static Reticle CreateFocus(RectTransform parent, Vector2 anchor, Vector2 position, float size, Color tint, Color glow, string name)
        {
            if (focusOuter == null)
            {
                focusOuter = Arcs(0.4f, 0.019f, 3, 96f, 0f, 0f);
                focusOuterGlow = Arcs(0.4f, 0.019f, 3, 96f, 0f, GlowBlur);
                focusInner = Arcs(0.345f, 0.008f, 24, 7f, 0f, 0f);
                focusShadow = Arcs(0.4f, 0.03f, 1, 360f, 0f, 12f);
            }
            var r = Reticle.Create(parent, name, anchor, position, size);
            r.AddLayer(focusShadow, Shadow, 0f, null, Color.clear);
            r.AddLayer(focusOuter, tint, 70f, focusOuterGlow, glow);
            var inner = tint;
            inner.a *= 0.6f;
            r.AddLayer(focusInner, inner, -30f, null, Color.clear);
            return r;
        }

        /// <summary>Red lock-on: four arcs (spinning) plus four inward ticks and a center pip (still).</summary>
        public static Reticle CreateLock(RectTransform parent, Vector2 anchor, Vector2 position, float size)
        {
            if (lockArcs == null)
            {
                lockArcs = Arcs(0.31f, 0.026f, 4, 52f, 45f, 0f);
                lockArcsGlow = Arcs(0.31f, 0.026f, 4, 52f, 45f, GlowBlur);
                lockTicks = Ticks();
                lockShadow = Arcs(0.31f, 0.05f, 1, 360f, 0f, 12f);
            }
            var glow = LockRed;
            glow.a = 0.85f;
            var r = Reticle.Create(parent, "LockOn", anchor, position, size);
            r.AddLayer(lockShadow, Shadow, 0f, null, Color.clear);
            r.AddLayer(lockArcs, LockRed, -110f, lockArcsGlow, glow);
            r.AddLayer(lockTicks, LockRed, 0f, null, Color.clear);
            return r;
        }

        /// <summary>
        /// A ring of <paramref name="count"/> evenly spaced arcs. Radius and thickness are fractions of the texture
        /// size; segment length and offset are in degrees. blur = 0 gives crisp anti-aliased edges; blur > 0 gives a
        /// soft gaussian falloff around the same shape (used for glows and shadows).
        /// </summary>
        static Sprite Arcs(float radius, float thickness, int count, float segmentDeg, float offsetDeg, float blur)
        {
            var tex = NewTexture();
            float c = (Res - 1) / 2f, rad = radius * Res, half = thickness * Res / 2f;
            float spacing = 360f / count;
            var px = new Color32[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float radialOut = Mathf.Abs(d - rad) - half; // px outside the band (negative = inside)

                    float angularOut = 0f; // px outside the nearest segment along the circle (negative = inside)
                    if (count > 1 || segmentDeg < 360f)
                    {
                        float angle = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - offsetDeg + segmentDeg / 2f, spacing);
                        float pxPerDeg = Mathf.Max(0.001f, d * Mathf.Deg2Rad);
                        angularOut = angle <= segmentDeg
                            ? -Mathf.Min(angle, segmentDeg - angle) * pxPerDeg
                            : Mathf.Min(angle - segmentDeg, spacing - angle) * pxPerDeg;
                    }

                    float a = blur > 0f
                        ? Gauss(radialOut, blur) * Gauss(angularOut, blur)
                        : Mathf.Clamp01(0.5f - radialOut) * Mathf.Clamp01(0.5f - angularOut);
                    if (a > 0.002f) px[y * Res + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), 100f);
        }

        static float Gauss(float outside, float blur)
        {
            if (outside <= 0f) return 1f;
            float t = outside / blur;
            return Mathf.Exp(-t * t);
        }

        /// <summary>Four short ticks pointing in from outside the arcs, and a small diamond in the middle.</summary>
        static Sprite Ticks()
        {
            var tex = NewTexture();
            float c = (Res - 1) / 2f;
            var px = new Color32[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float ax = Mathf.Abs(x - c), ay = Mathf.Abs(y - c);
                    float along = Mathf.Max(ax, ay), across = Mathf.Min(ax, ay);
                    float a = 0f;
                    if (along >= 0.35f * Res && along <= 0.42f * Res) a = Mathf.Clamp01(3f - across);
                    a = Mathf.Max(a, Mathf.Clamp01(0.03f * Res - (ax + ay) + 0.5f));
                    if (a > 0f) px[y * Res + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D NewTexture()
        {
            return new Texture2D(Res, Res, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }
    }

    /// <summary>
    /// A reticle: a root that snaps in like a hit marker (scales down from large while fading in, then breathes),
    /// holding spinning layers. Runs on unscaled time so it keeps moving while paused.
    /// </summary>
    public class Reticle : MonoBehaviour
    {
        const float SnapSeconds = 0.16f;

        CanvasGroup group;
        float age;

        public static Reticle Create(RectTransform parent, string name, Vector2 anchor, Vector2 position, float size)
        {
            var rt = UIFactory.CreateRect(name, parent).Place(anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            var reticle = rt.gameObject.AddComponent<Reticle>();
            reticle.group = rt.gameObject.AddComponent<CanvasGroup>();
            reticle.group.blocksRaycasts = false;
            reticle.group.interactable = false;
            rt.gameObject.SetActive(false);
            return reticle;
        }

        /// <summary>Adds a spinning layer; an optional glow sprite is drawn just behind it and spins with it.</summary>
        public void AddLayer(Sprite sprite, Color color, float degreesPerSecond, Sprite glow, Color glowColor)
        {
            var layer = UIFactory.CreateRect("Layer", transform).Fill();
            layer.gameObject.AddComponent<Spin>().degreesPerSecond = degreesPerSecond;
            if (glow != null) Image(layer, glow, glowColor);
            Image(layer, sprite, color);
        }

        static void Image(RectTransform parent, Sprite sprite, Color color)
        {
            var img = UIFactory.CreateImage(parent, sprite.name, color);
            img.sprite = sprite;
            img.raycastTarget = false;
            img.rectTransform.Fill();
        }

        public void Show(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void OnEnable()
        {
            age = 0f;
            Apply();
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            Apply();
        }

        void Apply()
        {
            float snap = Mathf.Clamp01(age / SnapSeconds);
            float ease = 1f - (1f - snap) * (1f - snap) * (1f - snap);
            float breathe = 1f + Mathf.Sin(age * 3.5f) * 0.03f * snap;
            transform.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, ease) * breathe;
            if (group != null) group.alpha = ease;
        }
    }

    /// <summary>Constant rotation on unscaled time.</summary>
    public class Spin : MonoBehaviour
    {
        public float degreesPerSecond;

        void Update()
        {
            if (degreesPerSecond != 0f) transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
