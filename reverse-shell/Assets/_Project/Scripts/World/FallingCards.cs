using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The title screen's background: Soul Cards (every full-art Ult card in Resources/Cards, Ult_*.png, each tinted a
    /// colour from <see cref="Tints"/> and keeping its shine) drifting down the screen, all one size, at a few speeds, gently
    /// swaying and turning, dimmed so the menu stays readable. Four pixel sparkle effects play over them (brighter than
    /// the dimmed cards):
    ///   Twinkle  a four-point star that blooms and fades at a random spot on a card, riding along with it
    ///   Trail    tiny glints left hanging behind a falling card, fading as it moves on
    ///   Burst    an eight-ray starburst that pops at a card's corner
    ///   Glitter  little diamonds drifting down the whole screen, flickering
    /// Runs on unscaled time (the world is paused under the title).
    /// </summary>
    public class FallingCards : MonoBehaviour
    {
        const float SpawnEvery = 0.55f;          // seconds between new cards
        const float Dim = 0.38f;                 // card opacity behind the menu
        const float SparkleAlpha = 0.85f;        // the sparkles' layer (not dimmed with the cards)
        const float SparklePixel = 4f;           // canvas units per sparkle pixel

        /// <summary>With Graphics: Fast, glitter and trail sparkles come this many times less often (each is a UI object).</summary>
        static float Sparse { get { return GameSettings.FastGraphics ? 3f : 1f; } }
        static readonly float[] Scales = { 0.8f };   // one size for every card (4x card pixels); depth comes from speed and fade

        /// <summary>The Soul Cards' colours (each card is tinted part-way toward one).</summary>
        static readonly Color[] Tints =
        {
            new Color(1f, 0.84f, 0.38f),   // gold
            new Color(0.45f, 0.9f, 1f),    // cyan
            new Color(1f, 0.55f, 0.72f),   // rose
            new Color(0.74f, 0.55f, 1f),   // violet
            new Color(0.55f, 1f, 0.7f),    // mint
            new Color(1f, 0.6f, 0.34f),    // ember
            new Color(0.92f, 0.95f, 1f),   // pearl
        };

        enum Kind { Twinkle, Trail, Burst, Glitter }

        class Faller
        {
            public RectTransform Rect;
            public float Speed, Sway, Phase, Spin, Scale;
            public float X;
            public Color Tint;
            public float NextTwinkle, NextTrail, NextBurst;
        }

        class Sparkle
        {
            public RectTransform Rect;
            public Image Image;
            public Kind Kind;
            public float Age, Life, Size, Spin;
            public Color Color;
            public Vector2 Velocity;
            public RectTransform Follow;            // twinkles ride on their card
            public Vector2 Offset;
        }

        readonly List<Sprite> souls = new List<Sprite>();
        /// <summary>The party's Soul cards (each character's own and its resonances' Ults), drawn as real card faces.</summary>
        readonly List<KeyValuePair<CharacterDefinition, CardDefinition>> soulCards = new List<KeyValuePair<CharacterDefinition, CardDefinition>>();
        readonly List<Faller> fallers = new List<Faller>();
        readonly List<Sparkle> sparkles = new List<Sparkle>();
        RectTransform area, sparkleLayer;
        float timer, glitterTimer;
        int next;

        static Sprite star, ray, diamond, dot;

        /// <param name="party">Whose Soul cards fall (each character's own and its resonances' Ults); with none, the old
        /// full-art Ult_ pictures fall instead.</param>
        public static FallingCards Create(RectTransform parent, IEnumerable<CharacterDefinition> party)
        {
            var rt = UIFactory.CreateRect("FallingCards", parent).Fill();
            // Their own canvases: the cards move and the sparkles come and go every frame, which would otherwise
            // rebuild the whole title-screen canvas each frame.
            rt.gameObject.AddComponent<Canvas>();
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = Dim;
            group.blocksRaycasts = false;
            group.interactable = false;
            var falling = rt.gameObject.AddComponent<FallingCards>();
            falling.area = rt;

            falling.sparkleLayer = UIFactory.CreateRect("Sparkles", parent).Fill();
            falling.sparkleLayer.gameObject.AddComponent<Canvas>();
            var sg = falling.sparkleLayer.gameObject.AddComponent<CanvasGroup>();
            sg.alpha = SparkleAlpha;
            sg.blocksRaycasts = false;
            sg.interactable = false;

            // The party's Soul cards in the card format (the character's own sprite); without a party, the old pictures.
            if (party != null)
                foreach (var member in party)
                {
                    if (member == null || member.ritual == null || CardArt.SoulSprite(member) == null) continue;
                    falling.soulCards.Add(new KeyValuePair<CharacterDefinition, CardDefinition>(member, member.ritual));
                    foreach (var kit in member.resonances)
                        if (kit != null && kit.ultimate != null && kit.ultimate != member.ritual)
                            falling.soulCards.Add(new KeyValuePair<CharacterDefinition, CardDefinition>(member, kit.ultimate));
                }
            if (falling.soulCards.Count == 0)
                foreach (var tex in Resources.LoadAll<Texture2D>("Cards"))
                {
                    if (tex == null || !tex.name.StartsWith("Ult_")) continue;
                    tex.filterMode = FilterMode.Point;
                    falling.souls.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect));
                }
            Shuffle(falling.souls);
            Shuffle(falling.soulCards);
            // Start with the screen already part-filled rather than empty.
            for (int i = 0; i < 6; i++) falling.Spawn(Random.Range(0.1f, 0.9f));
            return falling;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        /// <param name="startHeight">Where it starts, 1 = just above the top edge, 0 = the bottom.</param>
        void Spawn(float startHeight)
        {
            if (souls.Count == 0 && soulCards.Count == 0) return;
            float s = Scales[Random.Range(0, Scales.Length)];
            var tint = Tints[Random.Range(0, Tints.Length)];
            RectTransform rt;
            if (soulCards.Count > 0)
            {
                // A real Soul card face (the character's sprite in the card format), tinted like the old ones.
                var pick = soulCards[next++ % soulCards.Count];
                rt = UIFactory.CreateRect("Soul_" + pick.Value.name, area);
                rt.sizeDelta = CardView.Size * s;
                var unit = new BattleUnit(pick.Key, Team.Player);
                foreach (var kit in pick.Key.resonances)
                    if (kit != null && kit.ultimate == pick.Value) unit.ApplyLoadout(new Loadout { Element = kit.element, Ultimate = kit.ultimate });
                CardView.BuildFace(rt, new CardInstance(pick.Value, unit, 1), s, true);
                foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.color = g.color * Color.Lerp(Color.white, tint, 0.35f);
            }
            else
            {
                var art = souls[next++ % souls.Count];
                rt = UIFactory.CreateRect("Soul_" + art.texture.name, area);
                rt.sizeDelta = CardView.Size * s;
                var face = UIFactory.CreateImage(rt, "Art", Color.Lerp(Color.white, tint, 0.55f));
                face.sprite = art;
                face.rectTransform.Fill();
                UltShine.Add(rt, art, CardArt.HandPixel * s);
            }
            foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            rt.SetSiblingIndex(rt.GetSiblingIndex());

            var size = AreaSize();
            float x = Random.Range(-size.x / 2f + 80f, size.x / 2f - 80f);
            float top = size.y / 2f + CardView.Size.y * s;
            float y = Mathf.Lerp(-top, top, startHeight);
            rt.anchoredPosition = new Vector2(x, y);
            float now = Time.unscaledTime;
            fallers.Add(new Faller
            {
                Rect = rt,
                X = x,
                Scale = s,
                Tint = tint,
                Speed = Mathf.Lerp(70f, 150f, s) * Random.Range(0.85f, 1.15f),
                Sway = Random.Range(18f, 40f),
                Phase = Random.Range(0f, 6.28f),
                Spin = Random.Range(-12f, 12f),
                NextTwinkle = now + Random.Range(0.2f, 1.2f),
                NextTrail = now + Random.Range(0f, 0.2f),
                NextBurst = now + Random.Range(1f, 4f),
            });
        }

        Vector2 AreaSize()
        {
            var size = area.rect.size;
            return size.x < 400f || size.y < 400f ? UIFactory.ReferenceResolution : size;   // before the first layout
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            timer += dt;
            if (timer >= SpawnEvery)
            {
                timer = 0f;
                Spawn(1f);
            }
            float bottom = -Mathf.Max(area.rect.height, 400f) / 2f;
            for (int i = fallers.Count - 1; i >= 0; i--)
            {
                var f = fallers[i];
                if (f.Rect == null)
                {
                    fallers.RemoveAt(i);
                    continue;
                }
                var p = f.Rect.anchoredPosition;
                p.y -= f.Speed * dt;
                p.x = f.X + Mathf.Sin(now * 0.8f + f.Phase) * f.Sway;
                f.Rect.anchoredPosition = p;
                f.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * 0.6f + f.Phase) * f.Spin);
                if (p.y < bottom - f.Rect.rect.height)
                {
                    Destroy(f.Rect.gameObject);
                    fallers.RemoveAt(i);
                    continue;
                }
                CardSparkles(f, now);
            }

            // Glitter drifts down the whole screen.
            glitterTimer += dt;
            if (glitterTimer >= 0.14f * Sparse)
            {
                glitterTimer = 0f;
                var size = AreaSize();
                var c = Tints[Random.Range(0, Tints.Length)];
                Add(Kind.Glitter, new Vector2(Random.Range(-size.x / 2f, size.x / 2f), Random.Range(-size.y / 2f, size.y / 2f + 40f)),
                    Color.Lerp(Color.white, c, 0.5f), Random.Range(3f, 5f), new Vector2(Random.Range(-8f, 8f), -Random.Range(20f, 45f)));
            }
            UpdateSparkles(dt);
        }

        /// <summary>A card's own sparkles: twinkles on it, a trail behind it, a burst at a corner now and then.</summary>
        void CardSparkles(Faller f, float now)
        {
            var size = f.Rect.rect.size;
            var tint = Color.Lerp(Color.white, f.Tint, 0.35f);
            if (now >= f.NextTwinkle)
            {
                f.NextTwinkle = now + Random.Range(0.5f, 1.3f);
                var s = Add(Kind.Twinkle, f.Rect.anchoredPosition, tint, 0.7f, Vector2.zero);
                s.Follow = f.Rect;
                s.Offset = new Vector2(Random.Range(-0.4f, 0.4f) * size.x, Random.Range(-0.4f, 0.4f) * size.y);
            }
            if (now >= f.NextTrail)
            {
                // Left hanging above the card (behind it, as it falls), across its width.
                f.NextTrail = now + Random.Range(0.06f, 0.12f) * Sparse / Mathf.Max(0.6f, f.Scale);
                var at = f.Rect.anchoredPosition + new Vector2(Random.Range(-0.45f, 0.45f) * size.x, size.y * 0.5f + Random.Range(0f, 20f));
                Add(Kind.Trail, at, tint, Random.Range(0.6f, 1.1f), new Vector2(Random.Range(-6f, 6f), Random.Range(4f, 14f)));
            }
            if (now >= f.NextBurst)
            {
                f.NextBurst = now + Random.Range(2.5f, 5f);
                var corner = new Vector2(Random.value < 0.5f ? -0.5f : 0.5f, Random.value < 0.5f ? -0.5f : 0.5f);
                Add(Kind.Burst, f.Rect.anchoredPosition + Vector2.Scale(corner, size), tint, 0.45f, Vector2.zero);
            }
        }

        Sparkle Add(Kind kind, Vector2 at, Color color, float life, Vector2 velocity)
        {
            var img = UIFactory.CreateImage(sparkleLayer, kind.ToString(), color);
            img.raycastTarget = false;
            img.sprite = SpriteFor(kind);
            var px = img.sprite != null ? img.sprite.rect.size : new Vector2(3f, 3f);
            float size = kind == Kind.Glitter ? Random.Range(0.75f, 1f) : kind == Kind.Trail ? Random.Range(0.75f, 1.25f) : 1f;
            img.rectTransform.sizeDelta = px * SparklePixel;
            img.rectTransform.anchoredPosition = at;
            var s = new Sparkle
            {
                Rect = img.rectTransform, Image = img, Kind = kind, Life = life, Size = size, Color = color, Velocity = velocity,
                Spin = kind == Kind.Twinkle ? Random.Range(-90f, 90f) : 0f,
            };
            Apply(s);
            sparkles.Add(s);
            return s;
        }

        void UpdateSparkles(float dt)
        {
            for (int i = sparkles.Count - 1; i >= 0; i--)
            {
                var s = sparkles[i];
                s.Age += dt;
                if (s.Rect == null || s.Age >= s.Life || (s.Follow == null && s.Kind == Kind.Twinkle))
                {
                    if (s.Rect != null) Destroy(s.Rect.gameObject);
                    sparkles.RemoveAt(i);
                    continue;
                }
                if (s.Follow != null)
                    s.Rect.anchoredPosition = s.Follow.anchoredPosition + (Vector2)(s.Follow.localRotation * s.Offset);
                else
                    s.Rect.anchoredPosition += s.Velocity * dt;
                Apply(s);
            }
        }

        /// <summary>Each kind's look over its life (0..1).</summary>
        void Apply(Sparkle s)
        {
            float t = Mathf.Clamp01(s.Age / s.Life);
            float scale, alpha;
            switch (s.Kind)
            {
                case Kind.Twinkle:
                    // Blooms in, holds a moment, shrinks away; turns slowly.
                    scale = Mathf.Sin(t * Mathf.PI);
                    alpha = 1f;
                    s.Rect.localRotation = Quaternion.Euler(0f, 0f, s.Spin * t);
                    break;
                case Kind.Trail:
                    scale = s.Size * (1f - t * 0.5f);
                    alpha = 1f - t;
                    break;
                case Kind.Burst:
                    // Pops out fast, the rays thinning as it fades.
                    scale = Mathf.Lerp(0.4f, 1.6f, 1f - (1f - t) * (1f - t));
                    alpha = 1f - t * t;
                    break;
                default:   // Glitter: flickers as it drifts.
                    scale = s.Size;
                    alpha = Mathf.Sin(t * Mathf.PI) * (0.55f + 0.45f * Mathf.Sin(s.Age * 14f + s.Size * 9f));
                    break;
            }
            s.Rect.localScale = Vector3.one * Mathf.Max(0f, scale);
            var c = s.Color;
            c.a = Mathf.Clamp01(alpha);
            s.Image.color = c;
        }

        // ------------------------------------------------------------------ Sparkle sprites (white pixel art, tinted)

        static Sprite SpriteFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Twinkle: return star != null ? star : star = Draw(9, (x, y) =>
                {
                    int dx = Mathf.Abs(x - 4), dy = Mathf.Abs(y - 4);
                    if (dx == 0 && dy == 0) return 1f;
                    if ((dx == 0 && dy <= 4) || (dy == 0 && dx <= 4)) return dx + dy <= 2 ? 1f : 0.7f;   // the four points
                    return dx == 1 && dy == 1 ? 0.6f : 0f;
                });
                case Kind.Burst: return ray != null ? ray : ray = Draw(11, (x, y) =>
                {
                    int dx = x - 5, dy = y - 5, ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                    if (ax <= 1 && ay <= 1) return 0f;                                                   // open centre
                    if (dx == 0 || dy == 0) return Mathf.Max(ax, ay) >= 2 ? 1f : 0f;                     // straight rays
                    if (ax == ay && ax >= 2 && ax <= 4) return 0.8f;                                    // diagonal rays
                    return 0f;
                });
                case Kind.Glitter: return diamond != null ? diamond : diamond = Draw(5, (x, y) =>
                {
                    int d = Mathf.Abs(x - 2) + Mathf.Abs(y - 2);
                    return d == 0 ? 1f : d == 1 ? 0.85f : d == 2 && (x == 2 || y == 2) ? 0.5f : 0f;
                });
                default: return dot != null ? dot : dot = Draw(3, (x, y) => x == 1 || y == 1 ? (x == 1 && y == 1 ? 1f : 0.6f) : 0f);
            }
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
