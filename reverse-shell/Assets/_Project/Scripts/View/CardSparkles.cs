using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Sparkles over a card (the Esc menu's Resonance card): four-point stars and tiny glints bloom at random spots on
    /// it, turn and fade, in white and the card's own colour. Pixel art (drawn here), unscaled time, never blocks clicks.
    /// </summary>
    public class CardSparkles : MonoBehaviour
    {
        const float Pixel = 4f;                   // canvas units per sparkle pixel

        class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public float Age, Life, Spin;
            public Color Color;
        }

        readonly List<Spark> sparks = new List<Spark>();
        RectTransform area;
        Color tint = Color.white;
        float next;
        static Sprite star, glint;

        /// <summary>Adds sparkles over <paramref name="card"/>, tinted toward <paramref name="color"/>.</summary>
        public static CardSparkles Add(RectTransform card, Color color)
        {
            var layer = UIFactory.CreateRect("Sparkles", card).Fill();
            var fx = layer.gameObject.AddComponent<CardSparkles>();
            fx.area = layer;
            fx.tint = color;
            return fx;
        }

        void OnDisable()
        {
            foreach (var s in sparks) if (s.Rect != null) Destroy(s.Rect.gameObject);
            sparks.Clear();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            if (now >= next)
            {
                next = now + Random.Range(0.12f, 0.35f);
                Spawn(Random.value < 0.6f);
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.Age += dt;
                if (s.Rect == null || s.Age >= s.Life)
                {
                    if (s.Rect != null) Destroy(s.Rect.gameObject);
                    sparks.RemoveAt(i);
                    continue;
                }
                float t = s.Age / s.Life;
                s.Rect.localScale = Vector3.one * Mathf.Sin(t * Mathf.PI);
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, s.Spin * t);
                var c = s.Color;
                c.a = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 1.2f);
                s.Image.color = c;
            }
        }

        void Spawn(bool isStar)
        {
            var size = area.rect.size;
            var img = UIFactory.CreateImage(area, isStar ? "Star" : "Glint", Color.white);
            img.raycastTarget = false;
            img.sprite = isStar ? Star() : Glint();
            img.rectTransform.sizeDelta = img.sprite.rect.size * Pixel * (isStar ? Random.Range(0.8f, 1.2f) : 1f);
            img.rectTransform.anchoredPosition = new Vector2(Random.Range(-0.45f, 0.45f) * size.x, Random.Range(-0.45f, 0.45f) * size.y);
            sparks.Add(new Spark
            {
                Rect = img.rectTransform,
                Image = img,
                Life = isStar ? Random.Range(0.6f, 1f) : Random.Range(0.35f, 0.6f),
                Spin = isStar ? Random.Range(-80f, 80f) : 0f,
                Color = Random.value < 0.5f ? Color.white : Color.Lerp(Color.white, tint, 0.6f),
            });
        }

        /// <summary>A 9 px four-point star with a bright centre.</summary>
        static Sprite Star()
        {
            return star != null ? star : star = Draw(9, (x, y) =>
            {
                int dx = Mathf.Abs(x - 4), dy = Mathf.Abs(y - 4);
                if ((dx == 0 && dy <= 4) || (dy == 0 && dx <= 4)) return dx + dy <= 2 ? 1f : 0.7f;
                return dx == 1 && dy == 1 ? 0.55f : 0f;
            });
        }

        /// <summary>A 3 px plus-shaped glint.</summary>
        static Sprite Glint()
        {
            return glint != null ? glint : glint = Draw(3, (x, y) => x == 1 || y == 1 ? (x == 1 && y == 1 ? 1f : 0.6f) : 0f);
        }

        static Sprite Draw(int n, System.Func<int, int, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
