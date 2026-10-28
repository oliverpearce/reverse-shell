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
        /// <summary>Blank: the fanciest, a pearl sheen, two bands sweeping on their own and many pastel twinkles; it
        /// shines even with no tilt (a blank card lying on the pack table).</summary>
        public enum Kind { Common, Rare, Soul, Blank }

        /// <summary>
        /// An ability card's shine by its type (Common cards only; Soul keeps its rainbow, Sun / Moon / Star their gold):
        /// the band's colour and width, the sparkles' colour, and which way they drift (up for Buff and Heal, down for
        /// Debuff). Attack is a narrow hot band, Defense a wide cool one.
        /// </summary>
        struct TypeShine
        {
            public Color Band, Sparkle;
            public float Width, Drift;

            public TypeShine(Color band, Color sparkle, float width, float drift)
            {
                Band = band; Sparkle = sparkle; Width = width; Drift = drift;
            }
        }

        static TypeShine? ShineFor(CardKind? type)
        {
            if (!type.HasValue) return null;
            switch (type.Value)
            {
                case CardKind.Attack: return new TypeShine(new Color(1f, 0.5f, 0.32f), new Color(1f, 0.72f, 0.5f), 0.24f, 0f);
                case CardKind.Buff: return new TypeShine(new Color(1f, 0.86f, 0.4f), new Color(1f, 0.93f, 0.6f), 0.35f, 1f);
                case CardKind.Debuff: return new TypeShine(new Color(0.72f, 0.5f, 1f), new Color(0.85f, 0.7f, 1f), 0.35f, -1f);
                case CardKind.Defense: return new TypeShine(new Color(0.55f, 0.85f, 1f), new Color(0.8f, 0.95f, 1f), 0.55f, 0f);
                case CardKind.Counter: return new TypeShine(new Color(1f, 0.72f, 0.28f), new Color(1f, 0.85f, 0.5f), 0.24f, 0f);
                case CardKind.Heal: return new TypeShine(new Color(0.5f, 1f, 0.62f), new Color(0.75f, 1f, 0.8f), 0.4f, 0.6f);
            }
            return null;
        }

        TypeShine? shine;

        /// <summary>Overall brightness (1 = a zoomed card; lower for the gentle glint on cards in a grid).</summary>
        public float Strength = 1f;

        const float BandAngle = 28f;

        CardTilt tilt;
        Kind kind;
        RectTransform band, band2, rainbowRect;
        RawImage band2Raw;
        RawImage rainbow;
        Image[] sparkles;
        Vector2[] sparkleSpots;
        float[] sparklePhase;
        float width, height;

        static Texture2D bandTexture, rainbowTexture, pearlTexture;
        static Sprite sparkleSprite;

        /// <param name="card">The card face (the shine fills it and is masked to it).</param>
        /// <param name="shape">The card's art when it's narrower than the card (a tarot Soul card, Sun / Moon / Star): the
        /// shine is masked to it, fitted the same way the art is; null masks to the card's rectangle.</param>
        /// <param name="type">An ability card's type, for its own shine (<see cref="ShineFor"/>); null = the plain one.</param>
        public static CardHolo Add(RectTransform card, CardTilt tilt, Kind kind, Sprite shape = null, CardKind? type = null)
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
            holo.shine = kind == Kind.Common ? ShineFor(type) : null;
            holo.Build(rt);
            return holo;
        }

        void Build(RectTransform rt)
        {
            width = rt.rect.width;
            height = rt.rect.height;
            if (kind == Kind.Soul || kind == Kind.Blank)
            {
                // A full-card rainbow sheen (Blank: a soft pearl one); its hues slide sideways with the tilt (uvRect).
                var go = new GameObject("Rainbow", typeof(RectTransform));
                go.transform.SetParent(rt, false);
                rainbow = go.AddComponent<RawImage>();
                rainbow.texture = kind == Kind.Blank ? PearlTexture() : RainbowTexture();
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
            float bandWidth = shine.HasValue ? shine.Value.Width : kind == Kind.Common ? 0.35f : 0.5f;
            band.sizeDelta = new Vector2(width * bandWidth, Mathf.Sqrt(width * width + height * height) * 1.4f);
            band.localRotation = Quaternion.Euler(0f, 0f, -BandAngle);
            bandRaw = raw;
            if (kind == Kind.Blank)
            {
                // A second, thin and bright band that sweeps the other way.
                var go2 = new GameObject("Band2", typeof(RectTransform));
                go2.transform.SetParent(rt, false);
                band2Raw = go2.AddComponent<RawImage>();
                band2Raw.texture = BandTexture();
                band2Raw.raycastTarget = false;
                band2 = band2Raw.rectTransform;
                band2.anchorMin = band2.anchorMax = new Vector2(0.5f, 0.5f);
                band2.sizeDelta = new Vector2(width * 0.16f, Mathf.Sqrt(width * width + height * height) * 1.4f);
                band2.localRotation = Quaternion.Euler(0f, 0f, -BandAngle - 14f);
            }

            int count = kind == Kind.Blank ? 22 : kind == Kind.Soul ? 14 : kind == Kind.Rare ? 9 : 5;
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
            if (shine.HasValue)
            {
                var c = shine.Value.Band;
                return new Color(c.r, c.g, c.b, alpha);
            }
            switch (kind)
            {
                case Kind.Rare: return new Color(1f, 0.62f, 0.22f, alpha);
                case Kind.Soul: return new Color(1f, 1f, 1f, alpha);
                case Kind.Blank: return new Color(0.95f, 0.98f, 1f, alpha);
                default: return new Color(1f, 0.97f, 0.9f, alpha);
            }
        }

        void Update()
        {
            if (band == null || (tilt == null && kind != Kind.Blank)) return;
            // Stay on top of a face that's rebuilt under the same parent (scrolling through ranks).
            if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
            float time = Time.unscaledTime;
            var a = tilt != null ? tilt.Angles / Mathf.Max(1f, tilt.MaxAngle) : Vector2.zero;   // -1..1
            float amount = Mathf.Clamp01(a.magnitude);
            bool blank = kind == Kind.Blank;

            // The band crosses the card as it leans (left-right tilt moves it across, up-down nudges it along). A blank
            // card's bands also sweep across on their own, so it glitters lying still.
            float across = (a.x * 0.9f + a.y * 0.35f) * width;
            if (blank) across += Mathf.Sin(time * 0.9f) * width * 0.75f;
            band.anchoredPosition = new Vector2(across, 0f);
            float baseAlpha = blank ? 0.2f : shine.HasValue ? 0.09f : kind == Kind.Common ? 0.05f : kind == Kind.Rare ? 0.1f : 0.12f;
            float gain = kind == Kind.Common ? 0.28f : kind == Kind.Rare ? 0.45f : 0.55f;
            bandRaw.color = BandColor((baseAlpha + gain * amount) * Strength);
            if (band2 != null)
            {
                float across2 = -(a.x * 0.9f) * width + Mathf.Sin(time * 1.7f + 1.3f) * width * 0.8f;
                band2.anchoredPosition = new Vector2(across2, 0f);
                band2Raw.color = new Color(1f, 1f, 1f, (0.32f + 0.4f * amount) * Strength * (0.6f + 0.4f * Mathf.Sin(time * 3.3f)));
            }

            if (rainbow != null)
            {
                // Hues slide with the tilt (and drift a little on their own), stronger as it leans. Pearl drifts faster.
                rainbow.uvRect = new Rect(a.x * 0.6f + a.y * 0.25f + time * (blank ? 0.08f : 0.03f), blank ? time * 0.04f : 0f, 1.2f, 1f);
                rainbow.color = new Color(1f, 1f, 1f, ((blank ? 0.3f : 0.16f) + 0.3f * amount) * Strength);
            }

            float sparkleGain = kind == Kind.Common ? 0.35f : kind == Kind.Rare ? 0.7f : 1f;
            for (int i = 0; i < sparkles.Length; i++)
            {
                float twinkle = Mathf.Max(0f, Mathf.Sin(time * 3.1f + sparklePhase[i]));
                twinkle = twinkle * twinkle * twinkle;
                // Sparkles near the band shine brightest, so they seem to catch the light.
                float near = 1f - Mathf.Clamp01(Mathf.Abs(sparkleSpots[i].x - across) / (width * 0.6f));
                float alpha = sparkleGain * twinkle * (0.25f + 0.75f * Mathf.Max(amount, near * 0.6f));
                if (shine.HasValue && shine.Value.Drift != 0f)
                {
                    // Buff / Heal sparkles float up, Debuff ones sink, wrapping round the card; they fade at the edges.
                    float t = Mathf.Repeat(time * 0.12f * Mathf.Abs(shine.Value.Drift) + sparklePhase[i] * 0.1f, 1f);
                    float y = Mathf.Lerp(-0.45f, 0.45f, shine.Value.Drift > 0f ? t : 1f - t) * height;
                    sparkles[i].rectTransform.anchoredPosition = new Vector2(sparkleSpots[i].x, y);
                    alpha *= Mathf.Sin(t * Mathf.PI);
                }
                if (blank) alpha = Mathf.Max(alpha, twinkle * 0.8f);   // twinkles whether or not the band is near
                var c = shine.HasValue ? shine.Value.Sparkle
                      : blank ? Color.HSVToRGB(Mathf.Repeat(time * 0.15f + i * 0.21f, 1f), 0.22f, 1f)
                      : kind == Kind.Soul ? Color.HSVToRGB(Mathf.Repeat(time * 0.2f + i * 0.13f, 1f), 0.45f, 1f)
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

        /// <summary>A soft pearl sheen: pale pastel hues (the blank card's). Seamless both ways (whole hue turns across and
        /// down), so sliding the uvRect never shows a hard line where it wraps.</summary>
        static Texture PearlTexture()
        {
            if (pearlTexture != null) return pearlTexture;
            const int w = 128, h = 128;
            pearlTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float hue = Mathf.Repeat(u + v + 0.06f * Mathf.Sin(v * Mathf.PI * 2f), 1f);
                    var c = Color.HSVToRGB(hue, 0.22f, 1f);
                    pearlTexture.SetPixel(x, y, c);
                }
            pearlTexture.Apply();
            return pearlTexture;
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
