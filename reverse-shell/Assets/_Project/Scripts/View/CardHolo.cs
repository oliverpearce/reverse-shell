using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// CARDS > HOLO SHINE
    /// The shine on a zoomed card that moves as you tilt it (<see cref="CardTilt"/>): a diagonal light band slides across
    /// with the angle, and pixel sparkles twinkle brighter the further it leans. Scaled by rarity:
    ///   Common  a soft white glint
    ///   Rare    an orange holo band and warm sparkles (Sun, Moon, Star)
    ///   Soul    a rainbow sheen whose hues slide with the tilt, a bright band and dense sparkles
    /// It sits inside the card (masked to its shape), so it tilts with it. Unscaled time.
    /// </summary>
    public class CardHolo : MonoBehaviour
    {
        public enum Kind { Common, Rare, Soul }

        /// <summary>Overall brightness (1 = a zoomed card; lower for the gentle glint on cards in a grid).</summary>
        public float Strength = 1f;

        const float BandAngle = 28f;

        CardTilt tilt;
        Kind kind;
        RectTransform band, rainbowRect;
        RawImage rainbow;
        Image[] sparkles;
        Vector2[] sparkleSpots;
        float[] sparklePhase;
        float width, height;

        static Texture2D bandTexture, rainbowTexture;
        static Sprite sparkleSprite;

        /// <param name="card">The card face (the shine fills it and is masked to it).</param>
        /// <param name="shape">The card's art when it's narrower than the card (a tarot Soul card, Sun / Moon / Star): the
        /// shine is masked to it, fitted the same way the art is; null masks to the card's rectangle.</param>
        public static CardHolo Add(RectTransform card, CardTilt tilt, Kind kind, Sprite shape = null)
        {
            var rt = UIFactory.CreateRect("Holo", card).Fill();
            rt.SetAsLastSibling();
            var mask = rt.gameObject.AddComponent<Image>();
            mask.sprite = shape;
            mask.preserveAspect = shape != null;   // the same fit as the art, so nothing shines outside it
            mask.raycastTarget = false;
            rt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var holo = rt.gameObject.AddComponent<CardHolo>();
            holo.tilt = tilt;
            holo.kind = kind;
            holo.Build(rt);
            return holo;
        }

        void Build(RectTransform rt)
        {
            width = rt.rect.width;
            height = rt.rect.height;
            if (kind == Kind.Soul)
            {
                // A full-card rainbow sheen; its hues slide sideways with the tilt (uvRect).
                var go = new GameObject("Rainbow", typeof(RectTransform));
                go.transform.SetParent(rt, false);
                rainbow = go.AddComponent<RawImage>();
                rainbow.texture = RainbowTexture();
                rainbow.raycastTarget = false;
                rainbowRect = rainbow.rectTransform;
                rainbowRect.Fill();
            }
            // The light band: long and diagonal, wider than the card so its ends never show.
            var bandGo = new GameObject("Band", typeof(RectTransform));
            bandGo.transform.SetParent(rt, false);
            var raw = bandGo.AddComponent<RawImage>();
            raw.texture = BandTexture();
            raw.raycastTarget = false;
            raw.color = BandColor(0f);
            band = raw.rectTransform;
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            band.sizeDelta = new Vector2(width * (kind == Kind.Common ? 0.35f : 0.5f), Mathf.Sqrt(width * width + height * height) * 1.4f);
            band.localRotation = Quaternion.Euler(0f, 0f, -BandAngle);
            bandRaw = raw;

            int count = kind == Kind.Soul ? 14 : kind == Kind.Rare ? 9 : 5;
            sparkles = new Image[count];
            sparkleSpots = new Vector2[count];
            sparklePhase = new float[count];
            var rng = new System.Random(GetInstanceID());
            float px = Mathf.Max(2f, Mathf.Round(width / 48f));   // one card pixel
            for (int i = 0; i < count; i++)
            {
                var img = UIFactory.CreateImage(rt, "Sparkle" + i, Color.white);
                img.sprite = SparkleSprite();
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = Vector2.one * px * 5f;
                sparkleSpots[i] = new Vector2((float)(rng.NextDouble() - 0.5) * width * 0.85f, (float)(rng.NextDouble() - 0.5) * height * 0.85f);
                img.rectTransform.anchoredPosition = sparkleSpots[i];
                sparklePhase[i] = (float)rng.NextDouble() * 10f;
                sparkles[i] = img;
            }
        }

        RawImage bandRaw;

        Color BandColor(float alpha)
        {
            switch (kind)
            {
                case Kind.Rare: return new Color(1f, 0.62f, 0.22f, alpha);
                case Kind.Soul: return new Color(1f, 1f, 1f, alpha);
                default: return new Color(1f, 0.97f, 0.9f, alpha);
            }
        }

        void Update()
        {
            if (tilt == null || band == null) return;
            // Stay on top of a face that's rebuilt under the same parent (scrolling through ranks).
            if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
            float time = Time.unscaledTime;
            var a = tilt.Angles / Mathf.Max(1f, tilt.MaxAngle);   // -1..1
            float amount = Mathf.Clamp01(a.magnitude);

            // The band crosses the card as it leans (left-right tilt moves it across, up-down nudges it along).
            float across = (a.x * 0.9f + a.y * 0.35f) * width;
            band.anchoredPosition = new Vector2(across, 0f);
            float baseAlpha = kind == Kind.Common ? 0.05f : kind == Kind.Rare ? 0.1f : 0.12f;
            float gain = kind == Kind.Common ? 0.28f : kind == Kind.Rare ? 0.45f : 0.55f;
            bandRaw.color = BandColor((baseAlpha + gain * amount) * Strength);

            if (rainbow != null)
            {
                // Hues slide with the tilt (and drift a little on their own), stronger as it leans.
                rainbow.uvRect = new Rect(a.x * 0.6f + a.y * 0.25f + time * 0.03f, 0f, 1.2f, 1f);
                rainbow.color = new Color(1f, 1f, 1f, (0.16f + 0.3f * amount) * Strength);
            }

            float sparkleGain = kind == Kind.Common ? 0.35f : kind == Kind.Rare ? 0.7f : 1f;
            for (int i = 0; i < sparkles.Length; i++)
            {
                float twinkle = Mathf.Max(0f, Mathf.Sin(time * 3.1f + sparklePhase[i]));
                twinkle = twinkle * twinkle * twinkle;
                // Sparkles near the band shine brightest, so they seem to catch the light.
                float near = 1f - Mathf.Clamp01(Mathf.Abs(sparkleSpots[i].x - across) / (width * 0.6f));
                float alpha = sparkleGain * twinkle * (0.25f + 0.75f * Mathf.Max(amount, near * 0.6f));
                var c = kind == Kind.Soul ? Color.HSVToRGB(Mathf.Repeat(time * 0.2f + i * 0.13f, 1f), 0.45f, 1f)
                      : kind == Kind.Rare ? new Color(1f, 0.8f, 0.45f) : Color.white;
                c.a = alpha * Strength;
                sparkles[i].color = c;
            }
        }

        // CARDS > HOLO SHINE > TEXTURES (made once, in code)

        /// <summary>A soft vertical band: clear at both sides, bright in the middle with a thin hot core.</summary>
        static Texture BandTexture()
        {
            if (bandTexture != null) return bandTexture;
            const int w = 64;
            bandTexture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < w; x++)
            {
                float t = (x + 0.5f) / w * 2f - 1f;
                float soft = Mathf.Exp(-t * t * 4f);
                float core = Mathf.Exp(-t * t * 60f);
                bandTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Clamp01(soft * 0.6f + core * 0.6f)));
            }
            bandTexture.Apply();
            return bandTexture;
        }

        /// <summary>A diagonal rainbow that repeats (so sliding the uvRect cycles the hues).</summary>
        static Texture RainbowTexture()
        {
            if (rainbowTexture != null) return rainbowTexture;
            const int w = 128, h = 128;
            rainbowTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float hue = Mathf.Repeat((x + y * 0.6f) / w, 1f);
                    var c = Color.HSVToRGB(hue, 0.55f, 1f);
                    rainbowTexture.SetPixel(x, y, c);
                }
            rainbowTexture.Apply();
            return rainbowTexture;
        }

        /// <summary>A 5x5 pixel plus-shaped glint (point filtered, so it stays pixel art at any size).</summary>
        static Sprite SparkleSprite()
        {
            if (sparkleSprite != null) return sparkleSprite;
            var tex = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    bool on = x == 2 || y == 2;
                    float a = !on ? 0f : (x == 2 && y == 2) ? 1f : (Mathf.Abs(x - 2) + Mathf.Abs(y - 2) == 1 ? 0.9f : 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            sparkleSprite = Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 100f);
            return sparkleSprite;
        }
    }
}
