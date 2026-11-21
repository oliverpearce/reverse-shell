using System;
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// Chapter One's reflection, right after waking: the protagonist sees herself in a cracked mirror beside the capsule
    /// and learns what she is, as full-screen pixel panels (the same kind as the ending, <see cref="StoryPanels"/>):
    /// <list type="number">
    /// <item>her reflection in the cracked glass, framed on a ruined wall ([reflection.look]);</item>
    /// <item>the glitch: it tears into static, and when it settles there are seams on her skin and a plate over her
    /// chest slides open on the cracked Fragmented Core ([reflection.seams]);</item>
    /// <item>the touch, still in the mirror: she reaches for the Core and it flares ([reflection.touch]);</item>
    /// <item>her Soul Card rises out of it, glowing, its picture flickering to static and back ([reflection.card]),
    /// then it sinks home, the plate closes and the panels fade back to the world ([reflection.after]).</item>
    /// </list>
    /// Hold to Skip (top right, or hold Esc) ends it at once.
    /// </summary>
    public class ReflectionSequence : StoryPanels
    {
        static readonly Color CoreCyan = new Color(0.55f, 0.92f, 1f);
        static readonly Color Silver = new Color(0.86f, 0.88f, 0.92f);

        CharacterDefinition self;
        Sprite portrait;
        RectTransform chestCore;
        Image plateLeft, plateRight, chestGlow;
        Vector2 plateAt;
        float plateOpen;

        public static ReflectionSequence Play(OverworldBuilder world, CharacterDefinition protagonist, Action finished)
        {
            var seq = Open<ReflectionSequence>(world, "Reflection", finished);
            seq.self = protagonist;
            seq.portrait = protagonist != null ? CardArt.SoulSprite(protagonist) : null;
            seq.Begin();
            return seq;
        }

        protected override IEnumerator Run()
        {
            // 1. The mirror.
            RectTransform reflection;
            Ripple ripple;
            Image noise;
            BuildMirror(out reflection, out ripple, out noise);
            yield return FromBlack(1.2f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Talk("reflection.look");

            // 2. The glitch: static, the image tearing, then seams on her skin and the plate over her chest opening.
            yield return Glitch(ripple, noise, 1.4f);
            var seams = Seams(reflection);
            yield return Tween.Run(0.5f, p => seams.color = new Color(1f, 1f, 1f, p), null, true);
            yield return Glitch(ripple, noise, 0.35f);
            yield return OpenChest(reflection);
            yield return Talk("reflection.seams");

            // 3. The touch, in the mirror: the Core flares.
            yield return Talk("reflection.touch");
            var at = CoreOnStage();
            yield return Flare(at);

            // 4. Her Soul Card rises out of the Core, flickering to static.
            var rest = new Vector2(0f, size.y * 0.08f);
            var card = RiseCard(at);
            yield return Tween.Run(1.1f, p =>
            {
                card.anchoredPosition = Vector2.Lerp(at, rest, Tween.OutCubic(p));
                card.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Tween.OutBack(p));
            }, null, true);
            yield return Talk("reflection.card");
            yield return Tween.Run(0.9f, p =>
            {
                card.anchoredPosition = Vector2.Lerp(rest, at, p);
                card.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, p * p);
            }, null, true);
            card.gameObject.SetActive(false);
            yield return CloseChest();
            yield return Talk("reflection.after");
            yield return FadeAway(1.2f);
        }

        // ------------------------------------------------------------------ 1. the mirror

        void BuildMirror(out RectTransform reflection, out Ripple ripple, out Image noise)
        {
            var center = new Vector2(0.5f, 0.5f);
            int gw = Mathf.CeilToInt(size.x / px) + 2, gh = Mathf.CeilToInt(size.y / px) + 2;
            int pw = Mathf.RoundToInt(gh * 0.56f), ph = Mathf.RoundToInt(gh * 0.8f);
            pw += (gw - pw) & 1;   // the same parity as the wall, so the pane lands on the frame's art pixels
            ph += (gh - ph) & 1;
            var wall = Raw(stage, "Wall", Wall(gw, gh, pw, ph));
            wall.rectTransform.Place(center, center, Vector2.zero, new Vector2(gw, gh) * px);

            // The glass: everything in it is masked to the pane.
            var paneSize = new Vector2(pw, ph) * px;
            var mask = UIFactory.CreateImage(stage, "Mirror", new Color(0.07f, 0.1f, 0.14f, 1f));
            mask.rectTransform.Place(center, center, Vector2.zero, paneSize);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var sheen = UIFactory.CreateImage(mask.rectTransform, "Sheen", new Color(0.5f, 0.62f, 0.7f, 0.25f));
            sheen.sprite = UiParticles.GlowSprite();
            sheen.rectTransform.Place(center, center, new Vector2(-paneSize.x * 0.15f, paneSize.y * 0.2f), paneSize * 1.1f);

            // Her reflection: the portrait mirrored, in horizontal strips that tear in the glitch.
            float h = paneSize.y * 0.95f;
            float aspect = portrait != null ? portrait.rect.width / portrait.rect.height : 0.6f;
            reflection = UIFactory.CreateRect("Reflection", mask.rectTransform).Place(center, center, new Vector2(0f, -paneSize.y * 0.04f), new Vector2(h * aspect, h));
            reflection.localScale = new Vector3(-1f, 1f, 1f);   // a mirror image
            ripple = reflection.gameObject.AddComponent<Ripple>();
            ripple.Build(portrait, reflection.sizeDelta, px);

            var cracks = Raw(mask.rectTransform, "Cracks", Cracks(pw, ph));
            cracks.rectTransform.Fill();

            noise = UIFactory.CreateImage(mask.rectTransform, "Static", new Color(1f, 1f, 1f, 0f));
            noise.rectTransform.Fill();
        }

        IEnumerator Glitch(Ripple ripple, Image noise, float seconds)
        {
            var frames = NoiseFrames();
            int i = 0;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                ripple.Glitch = 1f;
                noise.sprite = frames[(i++ / 2) % frames.Length];
                noise.color = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.25f, 0.6f));
                yield return null;
            }
            ripple.Glitch = 0f;
            noise.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>Panel lines over her reflection where the portrait is solid: down the middle of the chest, across it,
        /// along the jaw and the arms, with a faint cyan light along each seam and a few rivets.</summary>
        RawImage Seams(RectTransform reflection)
        {
            int w = 1, h = 1;
            Color32[] solid = portrait != null ? CardRip.ReadPixels(portrait, out w, out h) : new Color32[1];
            var dark = new Color32(18, 20, 28, 230);
            var glow = new Color32(120, 220, 240, 150);
            var clear = new Color32(0, 0, 0, 0);
            Func<int, int, bool> on = (x, y) => x >= 0 && y >= 0 && x < w && y < h && solid[y * w + x].a > 40;
            int mid = w / 2, chest = Mathf.RoundToInt(h * 0.42f);
            var tex = Draw(w, h, (x, y) =>
            {
                if (!on(x, y)) return clear;
                bool line = (x == mid && y >= chest)                                   // down the sternum
                            || (y == chest && Mathf.Abs(x - mid) < w * 0.22f)              // across the chest
                            || (y == Mathf.RoundToInt(h * 0.58f) && Mathf.Abs(x - mid) < w * 0.16f)
                            || (y == Mathf.RoundToInt(h * 0.24f) && Mathf.Abs(x - mid) > w * 0.05f && Mathf.Abs(x - mid) < w * 0.14f) // the jaw
                            || ((x == Mathf.RoundToInt(w * 0.22f) || x == Mathf.RoundToInt(w * 0.78f)) && y > h * 0.35f && y < h * 0.7f);   // the arms
                if (line) return dark;
                bool beside = (x == mid + 1 && y >= chest) || (y == chest + 1 && Mathf.Abs(x - mid) < w * 0.22f);
                if (beside) return glow;
                bool rivet = (y == chest + 3 || y == chest + 9) && (x == mid - 4 || x == mid + 4);
                return rivet ? dark : clear;
            });
            var seams = Raw(reflection, "Seams", tex);
            seams.rectTransform.Fill();
            seams.color = new Color(1f, 1f, 1f, 0f);
            return seams;
        }

        /// <summary>Two plates over her chest slide apart on the cracked Core, which glows faintly.</summary>
        IEnumerator OpenChest(RectTransform reflection)
        {
            var r = reflection.rect;
            float unit = portrait != null ? r.height / portrait.rect.height : px;
            var at = new Vector2(0f, r.height * (0.5f - 0.48f));
            var holeSize = Vector2.one * unit * 14f;
            var hole = UIFactory.CreateImage(reflection, "Hole", new Color(0.03f, 0.04f, 0.05f, 1f));
            hole.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, holeSize);
            var glow = UIFactory.CreateImage(reflection, "CoreGlow", UIFactory.WithAlpha(CoreCyan, 0f));
            glow.sprite = UiParticles.GlowSprite();
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, holeSize * 2.4f);
            var core = UIFactory.CreateImage(reflection, "Core", CoreCyan);
            core.sprite = CharacterMenu.CoreIcon(true);
            core.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, Vector2.one * unit * 12f);
            chestCore = core.rectTransform;
            chestGlow = glow;
            var plateColor = new Color(0.16f, 0.15f, 0.2f, 1f);
            var left = UIFactory.CreateImage(reflection, "PlateL", plateColor);
            var right = UIFactory.CreateImage(reflection, "PlateR", plateColor);
            var half = new Vector2(holeSize.x * 0.5f, holeSize.y);
            left.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), at, half);
            right.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), at, half);
            plateLeft = left;
            plateRight = right;
            plateAt = at;
            plateOpen = half.x;
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Tween.Run(1.2f, p =>
            {
                float o = Tween.InOutQuad(p) * half.x;
                left.rectTransform.anchoredPosition = at + new Vector2(-o, 0f);
                right.rectTransform.anchoredPosition = at + new Vector2(o, 0f);
                left.color = right.color = UIFactory.WithAlpha(plateColor, 1f - p * 0.4f);
                glow.color = UIFactory.WithAlpha(CoreCyan, 0.6f * p);
            }, null, true);
            glow.gameObject.AddComponent<Flicker>().Set(glow, 0.35f, 0.65f);
        }

        /// <summary>The plates slide shut over the Core once the card has gone back in.</summary>
        IEnumerator CloseChest()
        {
            var plateColor = new Color(0.16f, 0.15f, 0.2f, 1f);
            var flicker = chestGlow.GetComponent<Flicker>();
            if (flicker != null) Destroy(flicker);
            float glowFrom = chestGlow.color.a;
            yield return Tween.Run(0.8f, p =>
            {
                float o = (1f - Tween.InOutQuad(p)) * plateOpen;
                plateLeft.rectTransform.anchoredPosition = plateAt + new Vector2(-o, 0f);
                plateRight.rectTransform.anchoredPosition = plateAt + new Vector2(o, 0f);
                plateLeft.color = plateRight.color = UIFactory.WithAlpha(plateColor, 0.6f + 0.4f * p);
                chestGlow.color = UIFactory.WithAlpha(CoreCyan, glowFrom * (1f - p));
            }, null, true);
        }

        // ------------------------------------------------------------------ 3. the touch

        /// <summary>Where the Core in the mirror is, in the stage's centre-anchored space.</summary>
        Vector2 CoreOnStage()
        {
            Vector2 local = stage.InverseTransformPoint(chestCore.position);
            return local - stage.rect.center;
        }

        /// <summary>Contact: rings of light go out from the Core in the glass, and a white flash.</summary>
        IEnumerator Flare(Vector2 at)
        {
            var center = new Vector2(0.5f, 0.5f);
            float coreSize = chestCore.rect.width;
            for (int i = 0; i < 3; i++)
            {
                var ring = UIFactory.CreateImage(stage, "Ring", UIFactory.WithAlpha(Color.white, 0.6f));
                ring.sprite = UiParticles.GlowSprite();
                ring.rectTransform.Place(center, center, at, Vector2.one * coreSize);
                StartCoroutine(Grow(ring, coreSize * (6f + i * 3f), 0.9f + i * 0.2f));
                yield return new WaitForSecondsRealtime(0.12f);
            }
            black.color = new Color(1f, 1f, 1f, 0.7f);
            yield return Tween.Run(0.6f, p => black.color = new Color(1f, 1f, 1f, 0.7f * (1f - p)), null, true);
            black.color = new Color(0f, 0f, 0f, 0f);
        }

        IEnumerator Grow(Image ring, float to, float seconds)
        {
            var start = ring.rectTransform.sizeDelta.x;
            yield return Tween.Run(seconds, p =>
            {
                ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(start, to, p);
                ring.color = UIFactory.WithAlpha(Color.white, 0.6f * (1f - p));
            }, null, true);
            Destroy(ring.gameObject);
        }

        RectTransform RiseCard(Vector2 from)
        {
            var center = new Vector2(0.5f, 0.5f);
            float unit = Mathf.Round(size.y * 0.5f / CardArt.Height);
            var holder = UIFactory.CreateRect("SoulCard", stage).Place(center, center, from, new Vector2(CardArt.Width, CardArt.Height) * unit);
            var halo = UIFactory.CreateImage(holder, "Halo", UIFactory.WithAlpha(Silver, 0.7f));
            halo.sprite = UiParticles.GlowSprite();
            halo.rectTransform.Place(center, center, Vector2.zero, holder.sizeDelta * 2.3f);
            var card = UIFactory.CreateImage(holder, "Card", Color.white);
            card.sprite = CardArt.Ult(self) ?? portrait;
            card.rectTransform.Fill();
            card.gameObject.AddComponent<StaticFlicker>().Set(card, NoiseFrames());
            holder.localScale = Vector3.one * 0.2f;
            return holder;
        }

        // ------------------------------------------------------------------ pixel art drawn in code

        static Sprite[] noiseFrames;

        /// <summary>A ruined brick wall with moss along its edges, and the mirror's tarnished gold frame around a pane of
        /// pw by ph in the middle (the glass itself is a masked image over it).</summary>
        static Texture2D Wall(int w, int h, int pw, int ph)
        {
            const int Frame = 4;
            int left = (w - pw) / 2, top = (h - ph) / 2, right = left + pw - 1, bottom = top + ph - 1;
            return Draw(w, h, (x, y) =>
            {
                int out_ = Mathf.Max(left - x, x - right, top - y, y - bottom);   // how far outside the pane
                if (out_ > 0 && out_ <= Frame)
                {
                    if (out_ == Frame) return new Color32(30, 24, 18, 255);
                    if (out_ == 1) return new Color32(52, 40, 26, 255);
                    bool lit = x < left || y < top;
                    return lit ? new Color32(168, 136, 82, 255) : new Color32(112, 88, 52, 255);
                }
                int n = (x * 73856093 ^ y * 19349663) & 15;
                int row = y / 7;
                int brickX = x + (row % 2) * 7;
                bool mortar = y % 7 == 0 || brickX % 14 == 0;
                byte v = (byte)((mortar ? 24 : 44 + (row + brickX / 14) % 3 * 4) + n / 2);
                var stone = new Color32(v, (byte)(v + 1), (byte)(v + 5), 255);
                if (out_ > Frame && out_ <= Frame + 3)   // the frame's shadow on the wall
                    stone = new Color32((byte)(v * 0.6f), (byte)(v * 0.6f), (byte)(v * 0.65f), 255);
                bool edge = x < 6 || x > w - 7 || y < 5 || y > h - 6;
                if (edge && (n & 5) == 0) return new Color32(70, 120, 60, 255);
                return stone;
            });
        }

        /// <summary>Cracks running out from one impact high on the glass: a dark line with a pale edge.</summary>
        static Texture2D Cracks(int w, int h)
        {
            var map = new byte[w * h];
            var rng = new System.Random(7);
            float ix = w * 0.64f, iy = h * 0.27f;
            const int Rays = 11;
            for (int r = 0; r < Rays; r++)
            {
                float angle = r * Mathf.PI * 2f / Rays + (float)rng.NextDouble() * 0.35f;
                float x = ix, y = iy;
                int steps = 20 + rng.Next(w + h);
                for (int s = 0; s < steps; s++)
                {
                    int px0 = Mathf.RoundToInt(x), py0 = Mathf.RoundToInt(y);
                    if (px0 < 0 || py0 < 0 || px0 >= w || py0 >= h) break;
                    map[py0 * w + px0] = 1;
                    if (px0 + 1 < w && map[py0 * w + px0 + 1] == 0) map[py0 * w + px0 + 1] = 2;
                    if (s % 6 == 5) angle += ((float)rng.NextDouble() - 0.5f) * 0.7f;
                    x += Mathf.Cos(angle);
                    y += Mathf.Sin(angle);
                }
            }
            var dark = new Color32(12, 14, 18, 225);
            var pale = new Color32(190, 210, 225, 120);
            var clear = new Color32(0, 0, 0, 0);
            return Draw(w, h, (x, y) => map[y * w + x] == 1 ? dark : map[y * w + x] == 2 ? pale : clear);
        }

        /// <summary>Three frames of TV static with scanlines (the glitch, and her card's flicker).</summary>
        static Sprite[] NoiseFrames()
        {
            if (noiseFrames != null) return noiseFrames;
            noiseFrames = new Sprite[3];
            for (int f = 0; f < 3; f++)
            {
                var rng = new System.Random(11 + f);
                var tex = Draw(48, 64, (x, y) =>
                {
                    byte v = (byte)(rng.Next(40, 230) * (y % 3 == 0 ? 0.55f : 1f));
                    return new Color32(v, v, (byte)Mathf.Min(255, v + 10), 255);
                });
                noiseFrames[f] = Sprite.Create(tex, new Rect(0, 0, 48, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            return noiseFrames;
        }

        // ------------------------------------------------------------------ small behaviours

        /// <summary>The reflection, as horizontal strips of the portrait, still until <see cref="Glitch"/> (0-1) tears them
        /// sideways and tints them.</summary>
        class Ripple : MonoBehaviour
        {
            public float Glitch;
            static readonly Color Clear = new Color(0.88f, 0.92f, 0.97f, 0.95f);
            readonly List<RawImage> strips = new List<RawImage>();
            float unit;

            public void Build(Sprite sprite, Vector2 area, float pixel)
            {
                unit = pixel;
                if (sprite == null) return;
                var tex = sprite.texture;
                var r = sprite.textureRect;
                int rows = Mathf.Max(1, Mathf.RoundToInt(r.height / 3f));
                for (int i = 0; i < rows; i++)
                {
                    var raw = Raw(transform, "Strip", tex);
                    float v0 = i / (float)rows, v1 = (i + 1) / (float)rows;
                    raw.uvRect = new Rect(r.x / tex.width, (r.y + r.height * (1f - v1)) / tex.height, r.width / tex.width, r.height / rows / tex.height);
                    raw.color = Clear;
                    var rt = raw.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f - v1);
                    rt.anchorMax = new Vector2(1f, 1f - v0);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    strips.Add(raw);
                }
            }

            void Update()
            {
                for (int i = 0; i < strips.Count; i++)
                {
                    float tear = Glitch > 0f && UnityEngine.Random.value < 0.3f ? UnityEngine.Random.Range(-1f, 1f) * unit * 12f * Glitch : 0f;
                    var rt = strips[i].rectTransform;
                    rt.offsetMin = new Vector2(tear, 0f);
                    rt.offsetMax = new Vector2(tear, 0f);
                    strips[i].color = Glitch > 0f && UnityEngine.Random.value < 0.2f
                        ? (UnityEngine.Random.value < 0.5f ? new Color(1f, 0.4f, 0.45f, 0.9f) : new Color(0.4f, 1f, 1f, 0.9f))
                        : Clear;
                }
            }
        }

        /// <summary>A glow flickering unsteadily (the Fragmented Core).</summary>
        class Flicker : MonoBehaviour
        {
            Image image;
            float low, high;
            public void Set(Image i, float lo, float hi) { image = i; low = lo; high = hi; }
            void Update()
            {
                if (image == null) return;
                float n = Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.3f);
                image.color = UIFactory.WithAlpha(image.color, Mathf.Lerp(low, high, n) * (UnityEngine.Random.value < 0.03f ? 0.3f : 1f));
            }
        }

        /// <summary>Her Soul Card's picture dropping to static for a moment every so often, then coming back.</summary>
        class StaticFlicker : MonoBehaviour
        {
            Image image;
            Sprite picture;
            Sprite[] noise;
            float next = 0.9f, hold;
            Vector2 home;
            public void Set(Image i, Sprite[] frames) { image = i; picture = i.sprite; noise = frames; home = i.rectTransform.anchoredPosition; }
            void Update()
            {
                if (image == null) return;
                float dt = Time.unscaledDeltaTime;
                if (hold > 0f)
                {
                    hold -= dt;
                    image.sprite = noise[(int)(Time.unscaledTime * 24f) % noise.Length];
                    image.rectTransform.anchoredPosition = home + new Vector2(UnityEngine.Random.Range(-4f, 4f), 0f);
                    if (hold <= 0f)
                    {
                        image.sprite = picture;
                        image.rectTransform.anchoredPosition = home;
                    }
                    return;
                }
                next -= dt;
                if (next > 0f) return;
                next = UnityEngine.Random.Range(0.5f, 1.6f);
                hold = UnityEngine.Random.Range(0.06f, 0.22f);
            }
        }
    }
}
