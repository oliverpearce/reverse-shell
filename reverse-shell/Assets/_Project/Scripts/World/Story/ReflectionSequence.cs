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
    /// Chapter One's reflection, right after the first steps out of the capsule: the protagonist stops at a puddle and
    /// learns what she is, as full-screen pixel panels (the same kind as the ending, <see cref="StoryPanels"/>):
    /// <list type="number">
    /// <item>looking down: her reflection in rainwater on the cracked road, rippling ([reflection.look]);</item>
    /// <item>the glitch: it tears into static, and when it settles there are seams on her skin and a plate over her
    /// chest slides open on the cracked Fragmented Core ([reflection.seams]);</item>
    /// <item>the touch: a close-up of the Core, her gloved hand reaching up to it ([reflection.touch]);</item>
    /// <item>her Soul Card rises out of it, glowing, its picture flickering to static and back ([reflection.card]),
    /// then it sinks home and the panels fade back to the world ([reflection.after]).</item>
    /// </list>
    /// Hold to Skip (top right, or hold Esc) ends it at once.
    /// </summary>
    public class ReflectionSequence : StoryPanels
    {
        static readonly Color CoreCyan = new Color(0.55f, 0.92f, 1f);
        static readonly Color Silver = new Color(0.86f, 0.88f, 0.92f);

        CharacterDefinition self;
        Sprite portrait;

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
            // 1. The puddle.
            RectTransform reflection;
            Ripple ripple;
            Image noise;
            BuildPuddle(out reflection, out ripple, out noise);
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

            // 3. The touch.
            yield return Cut();
            RectTransform core;
            Image coreGlow;
            BuildCoreCloseUp(out core, out coreGlow);
            yield return FromBlack(0.5f);
            yield return Talk("reflection.touch");
            yield return Touch(core, coreGlow);

            // 4. Her Soul Card rises out of the Core, flickering to static.
            var card = RiseCard(core);
            yield return Tween.Run(1.1f, p =>
            {
                card.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, size.y * 0.08f, Tween.OutCubic(p)));
                card.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Tween.OutBack(p));
            }, null, true);
            yield return Talk("reflection.card");
            yield return Tween.Run(0.9f, p =>
            {
                card.anchoredPosition = new Vector2(0f, Mathf.Lerp(size.y * 0.08f, 0f, p));
                card.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, p * p);
            }, null, true);
            card.gameObject.SetActive(false);
            yield return Talk("reflection.after");
            yield return FadeAway(1.2f);
        }

        // ------------------------------------------------------------------ 1. the puddle

        void BuildPuddle(out RectTransform reflection, out Ripple ripple, out Image noise)
        {
            var center = new Vector2(0.5f, 0.5f);
            int gw = Mathf.CeilToInt(size.x / px) + 2, gh = Mathf.CeilToInt(size.y / px) + 2;
            int rx = Mathf.RoundToInt(gh * 0.36f), ry = Mathf.RoundToInt(gh * 0.3f);
            var ground = Raw(stage, "Ground", Ground(gw, gh, rx, ry));
            ground.rectTransform.Place(center, center, Vector2.zero, new Vector2(gw, gh) * px);

            // The water, masked to the puddle's oval.
            var puddleSize = new Vector2(rx * 2, ry * 2) * px;
            var mask = UIFactory.CreateImage(stage, "Puddle", Color.white);
            mask.sprite = Oval(rx * 2, ry * 2);
            mask.rectTransform.Place(center, center, Vector2.zero, puddleSize);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            mask.color = new Color(0.07f, 0.1f, 0.14f, 1f);
            var sky = UIFactory.CreateImage(mask.rectTransform, "Sky", new Color(0.5f, 0.62f, 0.7f, 0.25f));
            sky.sprite = UiParticles.GlowSprite();
            sky.rectTransform.Place(center, center, new Vector2(-puddleSize.x * 0.15f, puddleSize.y * 0.2f), puddleSize * 1.1f);

            // Her reflection: the portrait mirrored, in horizontal strips that ripple.
            float h = puddleSize.y * 0.95f;
            float aspect = portrait != null ? portrait.rect.width / portrait.rect.height : 0.6f;
            reflection = UIFactory.CreateRect("Reflection", mask.rectTransform).Place(center, center, new Vector2(0f, -puddleSize.y * 0.04f), new Vector2(h * aspect, h));
            reflection.localScale = new Vector3(-1f, 1f, 1f);   // a mirror image
            ripple = reflection.gameObject.AddComponent<Ripple>();
            ripple.Build(portrait, reflection.sizeDelta, px);

            noise = UIFactory.CreateImage(mask.rectTransform, "Static", new Color(1f, 1f, 1f, 0f));
            noise.rectTransform.Fill();
            noise.gameObject.AddComponent<Drips>().Set(mask.rectTransform, puddleSize, px);
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
            var plateColor = new Color(0.16f, 0.15f, 0.2f, 1f);
            var left = UIFactory.CreateImage(reflection, "PlateL", plateColor);
            var right = UIFactory.CreateImage(reflection, "PlateR", plateColor);
            var half = new Vector2(holeSize.x * 0.5f, holeSize.y);
            left.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), at, half);
            right.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), at, half);
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

        // ------------------------------------------------------------------ 3. the touch

        void BuildCoreCloseUp(out RectTransform core, out Image glow)
        {
            var center = new Vector2(0.5f, 0.5f);
            // Her chest, close: dark plating with the open hatch in the middle.
            int cw = Mathf.CeilToInt(size.x / px) + 2, ch = Mathf.CeilToInt(size.y / px) + 2;
            var chest = Raw(stage, "Chest", Plating(cw, ch));
            chest.rectTransform.Place(center, center, Vector2.zero, new Vector2(cw, ch) * px);
            float coreSize = Mathf.Round(size.y * 0.34f / 16f) * 16f;
            glow = UIFactory.CreateImage(stage, "Glow", UIFactory.WithAlpha(CoreCyan, 0.45f));
            glow.sprite = UiParticles.GlowSprite();
            glow.rectTransform.Place(center, center, Vector2.zero, Vector2.one * coreSize * 2.6f);
            glow.gameObject.AddComponent<Flicker>().Set(glow, 0.3f, 0.55f);
            var c = UIFactory.CreateImage(stage, "Core", CoreCyan);
            c.sprite = CharacterMenu.CoreIcon(true);
            core = c.rectTransform;
            core.Place(center, center, Vector2.zero, Vector2.one * coreSize);
        }

        IEnumerator Touch(RectTransform core, Image glow)
        {
            var center = new Vector2(0.5f, 0.5f);
            // Her hand (a dark glove, a cyan seam down the finger) rises from the bottom right to the Core.
            var hand = Raw(stage, "Hand", Hand());
            var handSize = new Vector2(HandW, HandH) * px * 1.6f;
            hand.rectTransform.pivot = new Vector2(11.5f / HandW, 1f);   // the fingertip
            hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = center;
            hand.rectTransform.sizeDelta = handSize;
            var from = new Vector2(size.x * 0.22f, -size.y * 0.75f);
            var to = new Vector2(core.sizeDelta.x * 0.12f, -core.sizeDelta.y * 0.18f);
            hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            yield return Tween.Run(1.6f, p => hand.rectTransform.anchoredPosition = Vector2.Lerp(from, to, Tween.InOutQuad(p)), null, true);
            yield return new WaitForSecondsRealtime(0.25f);
            // Contact: the Core flares, a ring of light goes out, a white flash.
            for (int i = 0; i < 3; i++)
            {
                var ring = UIFactory.CreateImage(stage, "Ring", UIFactory.WithAlpha(Color.white, 0.6f));
                ring.sprite = UiParticles.GlowSprite();
                ring.rectTransform.Place(center, center, Vector2.zero, core.sizeDelta);
                StartCoroutine(Grow(ring, core.sizeDelta.x * (5f + i * 2f), 0.9f + i * 0.2f));
                yield return new WaitForSecondsRealtime(0.12f);
            }
            black.color = new Color(1f, 1f, 1f, 0.85f);
            yield return Tween.Run(0.6f, p => black.color = new Color(1f, 1f, 1f, 0.85f * (1f - p)), null, true);
            black.color = new Color(0f, 0f, 0f, 0f);
            yield return Tween.Run(0.6f, p => hand.rectTransform.anchoredPosition = Vector2.Lerp(to, to + new Vector2(size.x * 0.05f, -size.y * 0.25f), p), null, true);
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

        RectTransform RiseCard(RectTransform core)
        {
            var center = new Vector2(0.5f, 0.5f);
            float unit = Mathf.Round(size.y * 0.5f / CardArt.Height);
            var holder = UIFactory.CreateRect("SoulCard", stage).Place(center, center, Vector2.zero, new Vector2(CardArt.Width, CardArt.Height) * unit);
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

        const int HandW = 22, HandH = 36;
        static Sprite[] noiseFrames;
        static Texture2D handTex;

        /// <summary>Cracked old road seen from above, grass in the cracks and at the edges, the puddle's wet rim
        /// darkening the stone around an oval of rx by ry (the water itself is a masked image over it).</summary>
        static Texture2D Ground(int w, int h, int rx, int ry)
        {
            var rng = new System.Random(3);
            var cracks = new HashSet<int>();
            for (int c = 0; c < 9; c++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                for (int s = 0; s < 40; s++)
                {
                    cracks.Add(y * w + x);
                    x += rng.Next(-1, 2);
                    y += rng.Next(-1, 2) + (c % 2);
                    if (x < 0 || x >= w || y < 0 || y >= h) break;
                }
            }
            float cx = w / 2f, cy = h / 2f;
            return Draw(w, h, (x, y) =>
            {
                float dx = (x - cx) / (rx + 3f), dy = (y - cy) / (ry + 3f);
                float d = dx * dx + dy * dy;
                int n = (x * 73856093 ^ y * 19349663) & 15;
                int tile = ((x / 11) + (y / 9)) % 2;
                byte v = (byte)(58 + n + tile * 5);
                var stone = new Color32(v, (byte)(v + 2), (byte)(v - 4), 255);
                if (d < 1.25f) stone = new Color32((byte)(v * 0.55f), (byte)(v * 0.6f), (byte)(v * 0.62f), 255);   // the wet rim
                bool edge = x < 6 || x > w - 7 || y < 5 || y > h - 6;
                if (cracks.Contains(y * w + x)) return (n & 3) == 0 ? new Color32(70, 120, 60, 255) : new Color32(30, 32, 30, 255);
                if (edge && (n & 5) == 0) return new Color32(84, 140, 70, 255);
                if ((x % 11 == 0 || y % 9 == 0) && d > 1.25f) return new Color32((byte)(v - 12), (byte)(v - 10), (byte)(v - 14), 255);
                return stone;
            });
        }

        static Sprite Oval(int w, int h)
        {
            var tex = Draw(w, h, (x, y) =>
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                return dx * dx + dy * dy <= 1f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            });
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
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

        /// <summary>Close on her chest: dark plating in panels, with the hatch's rim around the middle.</summary>
        static Texture2D Plating(int w, int h)
        {
            float cx = w / 2f, cy = h / 2f;
            return Draw(w, h, (x, y) =>
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d < h * 0.2f) return new Color32(8, 10, 13, 255);                  // the open hatch
                if (d < h * 0.22f) return new Color32(70, 76, 90, 255);                // its rim
                bool seam = x % 26 == 0 || y % 20 == 0 || Mathf.Abs(x - cx) < 0.6f;
                int n = (x * 92821 ^ y * 68917) & 7;
                if (seam) return new Color32(14, 14, 20, 255);
                if ((x % 26 == 3 || x % 26 == 23) && (y % 20 == 3 || y % 20 == 17)) return new Color32(90, 94, 110, 255);   // rivets
                return new Color32((byte)(34 + n), (byte)(32 + n), (byte)(44 + n), 255);
            });
        }

        /// <summary>Her gloved hand from below, the index finger reaching up (its tip at x 11, y 0), a cyan seam down
        /// it.</summary>
        static Texture2D Hand()
        {
            if (handTex != null) return handTex;
            var glove = new Color32(26, 22, 32, 255);
            var light = new Color32(72, 66, 92, 255);
            var seam = new Color32(120, 225, 240, 255);
            var clear = new Color32(0, 0, 0, 0);
            handTex = Draw(HandW, HandH, (x, y) =>
            {
                bool finger = x >= 9 && x <= 13 && y >= 0 && y < 17 && !(y == 0 && (x == 9 || x == 13));
                bool knuckles = x >= 3 && x <= 19 && y >= 14 && y < 21 && !(y == 14 && (x < 5 || x > 17));
                bool palm = x >= 4 && x <= 18 && y >= 19;
                bool thumb = x >= 0 && x <= 5 && y >= 21 && y <= 29 && !(x == 0 && (y == 21 || y == 29));
                if (!(finger || knuckles || palm || thumb)) return clear;
                if (finger && x == 11 && y > 1 && y < 15) return seam;
                if ((finger && x == 9) || (knuckles && y == 14) || (thumb && x == 0) || (palm && x == 4)) return light;
                if (knuckles && (x == 8 || x == 14) && y < 20) return new Color32(14, 12, 18, 255);
                return glove;
            });
            return handTex;
        }

        // ------------------------------------------------------------------ small behaviours

        /// <summary>The reflection, as horizontal strips of the portrait that sway like water; <see cref="Glitch"/>
        /// (0-1) tears them sideways and tints them.</summary>
        class Ripple : MonoBehaviour
        {
            public float Glitch;
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
                    raw.color = new Color(0.72f, 0.82f, 0.92f, 0.85f);
                    var rt = raw.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f - v1);
                    rt.anchorMax = new Vector2(1f, 1f - v0);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    strips.Add(raw);
                }
            }

            void Update()
            {
                float t = Time.unscaledTime;
                for (int i = 0; i < strips.Count; i++)
                {
                    float sway = Mathf.Sin(t * 1.7f + i * 0.45f) * unit * 0.6f;
                    float tear = Glitch > 0f && UnityEngine.Random.value < 0.3f ? UnityEngine.Random.Range(-1f, 1f) * unit * 12f * Glitch : 0f;
                    var rt = strips[i].rectTransform;
                    rt.offsetMin = new Vector2(sway + tear, 0f);
                    rt.offsetMax = new Vector2(sway + tear, 0f);
                    strips[i].color = Glitch > 0f && UnityEngine.Random.value < 0.2f
                        ? (UnityEngine.Random.value < 0.5f ? new Color(1f, 0.4f, 0.45f, 0.9f) : new Color(0.4f, 1f, 1f, 0.9f))
                        : new Color(0.72f, 0.82f, 0.92f, 0.85f);
                }
            }
        }

        /// <summary>Now and then a drop lands in the puddle: a small ring that widens and fades.</summary>
        class Drips : MonoBehaviour
        {
            RectTransform area;
            Vector2 size;
            float unit, next = 0.8f;
            public void Set(RectTransform a, Vector2 s, float pixel) { area = a; size = s; unit = pixel; }
            void Update()
            {
                next -= Time.unscaledDeltaTime;
                if (next > 0f || area == null) return;
                next = UnityEngine.Random.Range(0.6f, 1.8f);
                var ring = UIFactory.CreateImage(area, "Drop", new Color(0.8f, 0.9f, 1f, 0.35f));
                ring.sprite = UiParticles.GlowSprite();
                var at = new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f) * size.x, UnityEngine.Random.Range(-0.3f, 0.3f) * size.y);
                ring.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, Vector2.one * unit * 2f);
                StartCoroutine(Spread(ring));
            }
            IEnumerator Spread(Image ring)
            {
                yield return Tween.Run(1.2f, p =>
                {
                    if (ring == null) return;
                    ring.rectTransform.sizeDelta = new Vector2(unit * (2f + 26f * p), unit * (1f + 13f * p));
                    ring.color = new Color(0.8f, 0.9f, 1f, 0.35f * (1f - p));
                }, Tween.Linear, true);
                if (ring != null) Destroy(ring.gameObject);
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
