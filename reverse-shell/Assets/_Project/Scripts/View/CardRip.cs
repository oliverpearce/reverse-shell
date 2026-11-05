using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A Soul Card torn in two and decaying pixel by pixel (Chapter One's ending: the Collector rips Erin's card). Works
    /// on the card's own pixels (48 x 64), drawn big with point filtering so every card pixel is a crisp block:
    /// <list type="number">
    /// <item>grip: the card trembles in a hand you can't see, its glow pulsing;</item>
    /// <item>tear: a jagged line runs top to bottom, the pixels on it bursting out as sparks;</item>
    /// <item>split: the halves pull apart and twist;</item>
    /// <item>decay: each half dies pixel by pixel from the torn edge out (dim, grey, gone), every pixel falling away
    /// as a speck of dust in its own colour;</item>
    /// <item>the last specks fade, then a moment of nothing.</item>
    /// </list>
    /// Any card picture works (later chapters can tear other cards). Runs on unscaled time.
    /// </summary>
    public class CardRip : MonoBehaviour
    {
        const float GripTime = 1.1f, TearTime = 0.75f, SplitTime = 0.7f, DecayTime = 2.8f, HoldTime = 1.1f;
        const int MaxDust = 520;

        int w, h;
        Color32[] source;
        int[] tearX;              // the tear's column on each row (top row first); left half is x < tearX
        float[] dieAt;            // when each pixel dies, in seconds after the decay starts
        bool[] dead;
        Texture2D leftTex, rightTex;
        Color32[] leftPx, rightPx;
        RectTransform card, left, right, dustLayer;
        Image glow;
        float pixel;
        Color glowColor;
        Action done;

        readonly List<Dust> dust = new List<Dust>();
        readonly Stack<Image> pool = new Stack<Image>();

        class Dust
        {
            public Image image;
            public Vector2 velocity;
            public float life, age;
            public Color color;
        }

        /// <summary>
        /// Tears <paramref name="picture"/> (a whole card picture, e.g. CardArt.Ult) in the middle of
        /// <paramref name="parent"/>, drawn at <paramref name="pixelSize"/> canvas units per card pixel, glowing in
        /// <paramref name="glowColour"/>. <paramref name="finished"/> runs once the dust has settled.
        /// </summary>
        public static CardRip Play(RectTransform parent, Sprite picture, float pixelSize, Color glowColour, Action finished)
        {
            if (picture == null)
            {
                if (finished != null) finished();
                return null;
            }
            var root = UIFactory.CreateRect("CardRip", parent).Fill();
            var rip = root.gameObject.AddComponent<CardRip>();
            rip.pixel = pixelSize;
            rip.glowColor = glowColour;
            rip.done = finished;
            rip.Build(picture);
            rip.StartCoroutine(rip.Run());
            return rip;
        }

        // ------------------------------------------------------------------ building

        void Build(Sprite picture)
        {
            source = ReadPixels(picture, out w, out h);
            var rng = new System.Random(7);   // the same tear every time
            tearX = new int[h];
            int x = w / 2 + rng.Next(-2, 3);
            for (int y = 0; y < h; y++)
            {
                x += rng.Next(-2, 3);
                x = Mathf.Clamp(x, w / 2 - 8, w / 2 + 8);
                tearX[y] = x;
            }

            // Each pixel's moment: the torn edge goes first, the outer edges last, with noise so it crumbles unevenly.
            dieAt = new float[w * h];
            dead = new bool[w * h];
            float maxDist = w / 2f + 8f;
            for (int y = 0; y < h; y++)
                for (int i = 0; i < w; i++)
                {
                    float d = Mathf.Abs(i - tearX[y] + 0.5f) / maxDist;
                    float noise = Mathf.PerlinNoise(i * 0.31f, y * 0.29f) * 0.35f + (float)rng.NextDouble() * 0.2f;
                    dieAt[Index(i, y)] = Mathf.Clamp01(d * 0.75f + noise) * DecayTime * 0.85f;
                }

            var center = new Vector2(0.5f, 0.5f);
            var size = new Vector2(w, h) * pixel;
            glow = UIFactory.CreateImage(transform, "Glow", UIFactory.WithAlpha(glowColor, 0f));
            glow.sprite = UiParticles.GlowSprite();
            glow.rectTransform.Place(center, center, Vector2.zero, size * 2.1f);

            card = UIFactory.CreateRect("Card", transform).Place(center, center, Vector2.zero, size);
            leftPx = new Color32[w * h];
            rightPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int i = 0; i < w; i++)
                {
                    int k = Index(i, y);
                    if (i < tearX[y]) leftPx[k] = source[k];
                    else rightPx[k] = source[k];
                }
            leftTex = MakeTexture(leftPx);
            rightTex = MakeTexture(rightPx);
            // Both halves fill the whole card rect; each pivots on the tear's middle, so they twist from the rip.
            float pivotX = tearX[h / 2] / (float)w;
            left = Half("Left", leftTex, pivotX, size);
            right = Half("Right", rightTex, pivotX, size);
            dustLayer = UIFactory.CreateRect("Dust", transform).Fill();
        }

        RectTransform Half(string name, Texture2D tex, float pivotX, Vector2 size)
        {
            var raw = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            raw.transform.SetParent(card, false);
            raw.texture = tex;
            raw.raycastTarget = false;
            var rt = raw.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(pivotX, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2((pivotX - 0.5f) * size.x, 0f);
            return rt;
        }

        Texture2D MakeTexture(Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(Flip(px));
            tex.Apply();
            return tex;
        }

        /// <summary>Our rows run top first; a texture's run bottom first.</summary>
        Color32[] Flip(Color32[] px)
        {
            var o = new Color32[px.Length];
            for (int y = 0; y < h; y++) Array.Copy(px, y * w, o, (h - 1 - y) * w, w);
            return o;
        }

        int Index(int x, int y) { return y * w + x; }

        /// <summary>A sprite's pixels, top row first, even when its texture isn't readable (through a RenderTexture).</summary>
        public static Color32[] ReadPixels(Sprite sprite, out int width, out int height)
        {
            var r = sprite.textureRect;
            width = Mathf.RoundToInt(r.width);
            height = Mathf.RoundToInt(r.height);
            var tex = sprite.texture;
            Color32[] bottomFirst;
            if (tex.isReadable)
            {
                var c = tex.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), width, height);
                bottomFirst = new Color32[c.Length];
                for (int i = 0; i < c.Length; i++) bottomFirst[i] = c[i];
            }
            else
            {
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                var previous = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(r.x, r.y, width, height), 0, 0);
                read.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                bottomFirst = read.GetPixels32();
                Destroy(read);
            }
            var topFirst = new Color32[bottomFirst.Length];
            for (int y = 0; y < height; y++) Array.Copy(bottomFirst, y * width, topFirst, (height - 1 - y) * width, width);
            return topFirst;
        }

        // ------------------------------------------------------------------ the animation

        IEnumerator Run()
        {
            // 1. Grip: trembling harder and harder, the glow breathing.
            for (float t = 0f; t < GripTime; t += Time.unscaledDeltaTime)
            {
                float p = t / GripTime;
                card.anchoredPosition = UnityEngine.Random.insideUnitCircle * pixel * 0.6f * p;
                card.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-1f, 1f) * 1.5f * p);
                glow.color = UIFactory.WithAlpha(glowColor, 0.45f + 0.25f * Mathf.Sin(t * 9f));
                yield return null;
            }

            // 2. Tear: the rip runs down the card, its pixels bursting out as bright sparks.
            int torn = 0;
            for (float t = 0f; t < TearTime; t += Time.unscaledDeltaTime)
            {
                int row = Mathf.Min(h, Mathf.CeilToInt(Tween.OutCubic(t / TearTime) * h));
                for (; torn < row; torn++) TearRow(torn);
                card.anchoredPosition = UnityEngine.Random.insideUnitCircle * pixel * 0.9f;
                glow.color = UIFactory.WithAlpha(Color.Lerp(glowColor, Color.white, 0.4f), 0.85f);
                Apply();
                StepDust();
                yield return null;
            }
            for (; torn < h; torn++) TearRow(torn);
            Apply();
            card.anchoredPosition = Vector2.zero;
            card.localRotation = Quaternion.identity;

            // 3. Split: the halves pull apart and twist away from each other.
            var leftFrom = left.anchoredPosition;
            var rightFrom = right.anchoredPosition;
            for (float t = 0f; t < SplitTime; t += Time.unscaledDeltaTime)
            {
                float p = Tween.OutCubic(t / SplitTime);
                left.anchoredPosition = leftFrom + new Vector2(-pixel * 7f * p, -pixel * 1.5f * p);
                right.anchoredPosition = rightFrom + new Vector2(pixel * 7f * p, pixel * 1f * p);
                left.localRotation = Quaternion.Euler(0f, 0f, 9f * p);
                right.localRotation = Quaternion.Euler(0f, 0f, -7f * p);
                glow.color = UIFactory.WithAlpha(glowColor, Mathf.Lerp(0.85f, 0.35f, p));
                StepDust();
                yield return null;
            }

            // 4. Decay: pixel by pixel from the torn edge out; each one dims, greys and falls away as dust.
            for (float t = 0f; t < DecayTime; t += Time.unscaledDeltaTime)
            {
                Decay(t);
                left.anchoredPosition += new Vector2(-pixel * 0.25f, -pixel * 0.35f) * Time.unscaledDeltaTime;
                right.anchoredPosition += new Vector2(pixel * 0.25f, -pixel * 0.3f) * Time.unscaledDeltaTime;
                glow.color = UIFactory.WithAlpha(glowColor, Mathf.Lerp(0.35f, 0f, t / DecayTime));
                Apply();
                StepDust();
                yield return null;
            }
            Decay(DecayTime * 2f);
            Apply();

            // 5. The last specks settle, then nothing.
            for (float t = 0f; t < HoldTime || dust.Count > 0; t += Time.unscaledDeltaTime)
            {
                StepDust();
                if (t > HoldTime + 3f) break;
                yield return null;
            }
            var then = done;
            done = null;
            if (then != null) then();
        }

        void TearRow(int y)
        {
            int x = tearX[y];
            for (int i = x - 1; i <= x; i++)
            {
                if (i < 0 || i >= w) continue;
                int k = Index(i, y);
                if (source[k].a < 10 || dead[k]) continue;
                dead[k] = true;
                leftPx[k] = rightPx[k] = new Color32(0, 0, 0, 0);
                var bright = Color.Lerp(source[k], Color.white, 0.6f);
                SpawnDust(i, y, i < x ? left : right, bright, new Vector2((i < x ? -1f : 1f) * UnityEngine.Random.Range(2f, 6f),
                    UnityEngine.Random.Range(-1f, 4f)) * pixel, 0.7f);
            }
        }

        void Decay(float t)
        {
            var grey = new Color(0.32f, 0.31f, 0.3f);
            for (int y = 0; y < h; y++)
                for (int i = 0; i < w; i++)
                {
                    int k = Index(i, y);
                    if (dead[k] || source[k].a < 10) continue;
                    float until = dieAt[k] - t;
                    bool isLeft = i < tearX[y];
                    if (until <= 0f)
                    {
                        dead[k] = true;
                        if (isLeft) leftPx[k] = new Color32(0, 0, 0, 0);
                        else rightPx[k] = new Color32(0, 0, 0, 0);
                        // Every other pixel becomes a visible speck (plenty, and cheap).
                        if (((i + y) & 1) == 0)
                            SpawnDust(i, y, isLeft ? left : right, Color.Lerp(source[k], grey, 0.5f),
                                new Vector2(UnityEngine.Random.Range(-1.2f, 1.2f), UnityEngine.Random.Range(-0.5f, 1.5f)) * pixel, 1.1f);
                        continue;
                    }
                    if (until < 0.45f)
                    {
                        // Dying: draining to grey and dark.
                        float p = 1f - until / 0.45f;
                        Color c = source[k];
                        var dim = Color.Lerp(c, grey * 0.6f, p);
                        dim.a = c.a;
                        if (isLeft) leftPx[k] = dim;
                        else rightPx[k] = dim;
                    }
                }
        }

        void Apply()
        {
            leftTex.SetPixels32(Flip(leftPx));
            leftTex.Apply();
            rightTex.SetPixels32(Flip(rightPx));
            rightTex.Apply();
        }

        // ------------------------------------------------------------------ dust

        void SpawnDust(int x, int y, RectTransform half, Color color, Vector2 velocity, float life)
        {
            if (dust.Count >= MaxDust) return;
            // The pixel's centre on the half (whose rect covers the whole card), into the dust layer.
            var size = half.rect.size;
            var local = new Vector2((x + 0.5f) / w * size.x - half.pivot.x * size.x, (0.5f - (y + 0.5f) / h) * size.y + (half.pivot.y - 0.5f) * size.y);
            var world = half.TransformPoint(local);
            var image = pool.Count > 0 ? pool.Pop() : UIFactory.CreateImage(dustLayer, "Speck", Color.white);
            image.gameObject.SetActive(true);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = Vector2.one * pixel;
            image.rectTransform.localRotation = Quaternion.identity;
            image.rectTransform.position = world;
            image.color = color;
            dust.Add(new Dust { image = image, velocity = velocity, life = life * UnityEngine.Random.Range(0.8f, 1.4f), color = color });
        }

        void StepDust()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = dust.Count - 1; i >= 0; i--)
            {
                var d = dust[i];
                d.age += dt;
                if (d.age >= d.life)
                {
                    d.image.gameObject.SetActive(false);
                    pool.Push(d.image);
                    dust.RemoveAt(i);
                    continue;
                }
                d.velocity += new Vector2(0f, -pixel * 5f) * dt;   // falling, slowly
                d.velocity *= 1f - 0.8f * dt;                      // and drifting to a stop
                d.image.rectTransform.anchoredPosition += d.velocity * dt;
                float p = d.age / d.life;
                d.image.color = UIFactory.WithAlpha(Color.Lerp(d.color, new Color(0.25f, 0.24f, 0.23f), p), 1f - p * p);
                float s = Mathf.Lerp(1f, 0.5f, p) * pixel;
                d.image.rectTransform.sizeDelta = new Vector2(s, s);
            }
        }

        void OnDestroy()
        {
            if (leftTex != null) Destroy(leftTex);
            if (rightTex != null) Destroy(rightTex);
        }
    }
}
