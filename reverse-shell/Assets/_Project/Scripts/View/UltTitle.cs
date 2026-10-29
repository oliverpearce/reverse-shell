using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A Soul Card (Ult) being played: an ink band snaps open across the screen with two hairlines in the owner's
    /// colour sweeping in from the sides, then the card's name slams in letter by letter (each one dropping from big
    /// and tilted into place), a white glint runs along the word, it holds, and the letters peel off to the right as
    /// the band closes. Scaled time, so the battle speed applies.
    /// </summary>
    public static class UltTitle
    {
        const int FontSize = 104;
        const float BandHeight = 190f;
        const float LetterStagger = 0.045f, LetterIn = 0.26f, Hold = 0.45f, Out = 0.3f;

        /// <param name="unscaled">Real time instead (Pack Mode runs over the paused title screen).</param>
        public static IEnumerator Play(RectTransform layer, string name, Color color, bool unscaled = false)
        {
            var center = new Vector2(0.5f, 0.5f);
            var host = layer.gameObject.AddComponent<CanvasGroup>();
            host.blocksRaycasts = false;
            host.interactable = false;

            // The band: ink, tinted toward the owner's colour, opening from a line.
            var band = UIFactory.CreateImage(layer, "Band", Color.Lerp(InkStyle.InkPanel, UIFactory.WithAlpha(color, 0.92f), 0.3f));
            band.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(0f, BandHeight);
            band.rectTransform.anchoredPosition = Vector2.zero;
            band.rectTransform.localScale = new Vector3(1f, 0f, 1f);

            float width = layer.rect.width > 0f ? layer.rect.width : 1920f;
            var lines = new List<RectTransform>();
            for (int i = 0; i < 2; i++)
            {
                var line = UIFactory.CreateImage(layer, "Line" + i, Color.Lerp(color, Color.white, 0.35f));
                float y = (i == 0 ? 1f : -1f) * (BandHeight / 2f + 6f);
                line.rectTransform.Place(center, center, new Vector2(i == 0 ? -width : width, y), new Vector2(width, 4f));
                lines.Add(line.rectTransform);
            }

            // The letters, laid out by their own widths around the centre.
            var letters = new List<Text>();
            var widths = new List<float>();
            float total = 0f;
            foreach (char c in name)
            {
                var t = UIFactory.CreateText(layer, "L", c.ToString(), FontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(4f, -4f);
                t.gameObject.AddComponent<Shadow>().effectColor = UIFactory.WithAlpha(color, 0.8f);
                float w = c == ' ' ? FontSize * 0.4f : t.preferredWidth;
                letters.Add(t);
                widths.Add(w);
                total += w;
            }
            // Long names shrink to fit the screen.
            float fit = Mathf.Min(1f, (width - 160f) / Mathf.Max(1f, total));
            float x = -total * fit / 2f;
            var homes = new List<Vector2>();
            for (int i = 0; i < letters.Count; i++)
            {
                var rt = letters[i].rectTransform;
                float w = widths[i] * fit;
                homes.Add(new Vector2(x + w / 2f, 0f));
                rt.Place(center, center, homes[i], new Vector2(w + 20f, FontSize * 1.4f));
                rt.localScale = Vector3.zero;
                x += w;
            }

            // Open: the band and the hairlines.
            yield return Tween.Run(0.18f, p =>
            {
                band.rectTransform.localScale = new Vector3(1f, p, 1f);
                for (int i = 0; i < lines.Count; i++)
                    lines[i].anchoredPosition = new Vector2(Mathf.Lerp(i == 0 ? -width : width, 0f, p), lines[i].anchoredPosition.y);
            }, Tween.OutCubic, unscaled);

            // The letters slam in one after another: big, tilted and raised, dropping into place.
            float inTime = LetterIn + LetterStagger * Mathf.Max(0, letters.Count - 1);
            var tilts = new List<float>();
            for (int i = 0; i < letters.Count; i++) tilts.Add(Random.Range(-18f, 18f));
            yield return Tween.Run(inTime, p =>
            {
                float t = p * inTime;
                for (int i = 0; i < letters.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * LetterStagger) / LetterIn);
                    if (letters[i] == null) continue;
                    var rt = letters[i].rectTransform;
                    float e = Tween.OutBack(k);
                    rt.localScale = Vector3.one * (k <= 0f ? 0f : Mathf.LerpUnclamped(2.4f, 1f, e) * fit);
                    rt.anchoredPosition = homes[i] + new Vector2(0f, Mathf.LerpUnclamped(60f, 0f, e));
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(tilts[i], 0f, e));
                    letters[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
                }
            }, Tween.Linear, unscaled);

            // A glint runs along the word: each letter flashes from the owner's colour to white as it passes.
            Color tint = Color.Lerp(color, Color.white, 0.55f);
            yield return Tween.Run(Hold, p =>
            {
                float head = p * (letters.Count + 3);
                for (int i = 0; i < letters.Count; i++)
                {
                    if (letters[i] == null) continue;
                    float d = Mathf.Abs(head - i);
                    letters[i].color = Color.Lerp(tint, Color.white, Mathf.Clamp01(1f - d / 2f));
                }
            }, Tween.Linear, unscaled);

            // Out: the letters peel off to the right, one after another, and the band closes.
            float outTime = Out + 0.02f * letters.Count;
            yield return Tween.Run(outTime, p =>
            {
                float t = p * outTime;
                for (int i = 0; i < letters.Count; i++)
                {
                    if (letters[i] == null) continue;
                    float k = Mathf.Clamp01((t - (letters.Count - 1 - i) * 0.02f) / Out);
                    var rt = letters[i].rectTransform;
                    rt.anchoredPosition = homes[i] + new Vector2(Tween.OutCubic(k) * 160f, 0f);
                    var c = letters[i].color;
                    letters[i].color = new Color(c.r, c.g, c.b, 1f - k);
                }
                band.rectTransform.localScale = new Vector3(1f, 1f - Tween.OutCubic(p), 1f);
                host.alpha = 1f - Mathf.Clamp01((p - 0.6f) / 0.4f);
            }, Tween.Linear, unscaled);
        }
    }
}
