using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// EFFECTS > UI PARTICLES
    /// Simple sparks for menus (the pack opening): pixel glints that burst out, fall a little and fade, plus helpers for
    /// a soft glow, a ring shockwave and rotating light rays. Everything lives on one layer and runs on unscaled time,
    /// so it works over a paused world.
    /// </summary>
    public class UiParticles : MonoBehaviour
    {
        class Spark
        {
            public Image Image;
            public Vector2 Position, Velocity;
            public float Life, Age, Size, Spin;
            public Func<float, Color> Color;
        }

        readonly List<Spark> sparks = new List<Spark>();
        RectTransform layer;
        static Sprite glintSprite, glowSprite;

        public static UiParticles On(RectTransform layer)
        {
            var p = layer.GetComponent<UiParticles>();
            if (p == null)
            {
                p = layer.gameObject.AddComponent<UiParticles>();
                p.layer = layer;
            }
            return p;
        }

        /// <summary>Sparks bursting out from <paramref name="at"/> (layer units). <paramref name="color"/> gets each spark's
        /// age 0-1 and returns its colour (alpha included), so sparks can shift hue or fade.</summary>
        public void Burst(Vector2 at, int count, float speed, float life, float size, Func<float, Color> color, float upward = 0f)
        {
            var rng = new System.Random(Environment.TickCount ^ count);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float v = speed * (0.4f + 0.6f * (float)rng.NextDouble());
                var img = UIFactory.CreateImage(layer, "Spark", Color.white);
                img.sprite = Glint();
                img.raycastTarget = false;
                float s = size * (0.6f + 0.8f * (float)rng.NextDouble());
                img.rectTransform.sizeDelta = Vector2.one * s;
                sparks.Add(new Spark
                {
                    Image = img,
                    Position = at,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * v + Vector2.up * upward,
                    Life = life * (0.6f + 0.6f * (float)rng.NextDouble()),
                    Size = s,
                    Spin = (float)(rng.NextDouble() - 0.5) * 360f,
                    Color = color,
                });
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.Age += dt;
                float t = s.Age / s.Life;
                if (t >= 1f || s.Image == null)
                {
                    if (s.Image != null) Destroy(s.Image.gameObject);
                    sparks.RemoveAt(i);
                    continue;
                }
                s.Velocity += Vector2.down * 900f * dt;   // a little gravity
                s.Velocity *= 1f - 1.6f * dt;             // and drag
                s.Position += s.Velocity * dt;
                var rt = s.Image.rectTransform;
                rt.anchoredPosition = new Vector2(Mathf.Round(s.Position.x), Mathf.Round(s.Position.y));
                rt.localRotation = Quaternion.Euler(0f, 0f, s.Spin * s.Age);
                rt.localScale = Vector3.one * (1f - 0.5f * t);
                var c = s.Color(t);
                c.a *= 1f - t * t;
                s.Image.color = c;
            }
        }

        // EFFECTS > UI PARTICLES > SHAPES

        /// <summary>A soft round glow (tint it with the Image colour).</summary>
        public static Image Glow(RectTransform parent, Vector2 at, float size, Color color)
        {
            var img = UIFactory.CreateImage(parent, "Glow", color);
            img.sprite = GlowSprite();
            img.raycastTarget = false;
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.sizeDelta = Vector2.one * size;
            return img;
        }

        /// <summary>Thin light rays fanned around a point (rotate the returned rect to spin them).</summary>
        public static RectTransform Rays(RectTransform parent, Vector2 at, int count, float length, float width)
        {
            var root = UIFactory.CreateRect("Rays", parent);
            root.anchoredPosition = at;
            for (int i = 0; i < count; i++)
            {
                var ray = UIFactory.CreateImage(root, "Ray" + i, Color.white);
                ray.sprite = GlowSprite();
                ray.raycastTarget = false;
                ray.rectTransform.pivot = new Vector2(0.5f, 0f);
                ray.rectTransform.anchoredPosition = Vector2.zero;
                ray.rectTransform.sizeDelta = new Vector2(width, length);
                ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 360f / count * i);
            }
            return root;
        }

        public static Sprite Glint()
        {
            if (glintSprite != null) return glintSprite;
            var tex = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    bool on = x == 2 || y == 2;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, !on ? 0f : (x == 2 && y == 2) ? 1f : 0.75f));
                }
            tex.Apply();
            return glintSprite = Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A hue that cycles over time (the rainbow of SOUL effects).</summary>
        public static Color Rainbow(float offset, float alpha = 1f)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.5f + offset, 1f), 0.6f, 1f);
            c.a = alpha;
            return c;
        }
    }
}
