using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A constant glisten over an Ult card: a pair of light bands (a wide soft one and a thin bright one) sweeps
    /// diagonally across the card every few seconds, and a few pixel sparkles twinkle. Masked by the card's own shape
    /// (so it stays on the card when the hand fan tilts it) and snapped to the card's pixel grid so it reads as pixel art.
    /// </summary>
    public class UltShine : MonoBehaviour
    {
        const float Period = 2.6f;        // seconds between sweeps
        const float Sweep = 0.75f;        // seconds one sweep takes
        const float Angle = 28f;          // band tilt (degrees)

        RectTransform wide, thin;
        Image[] sparkles;
        float width, pixel, phase;

        /// <param name="shape">The card's art (its alpha masks the shine); null masks to the card's rectangle.</param>
        public static UltShine Add(RectTransform card, Sprite shape, float cardPixel)
        {
            var rt = UIFactory.CreateRect("UltShine", card).Fill();
            rt.SetAsLastSibling();
            var maskImage = rt.gameObject.AddComponent<Image>();
            maskImage.sprite = shape;
            maskImage.raycastTarget = false;
            rt.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var shine = rt.gameObject.AddComponent<UltShine>();
            shine.width = card.rect.width > 0f ? card.rect.width : CardView.Size.x;
            float height = card.rect.height > 0f ? card.rect.height : CardView.Size.y;
            shine.pixel = cardPixel;
            shine.phase = Random.value * Period;   // cards in hand don't all flash at once
            float bandH = height * 1.6f;
            shine.wide = Band(rt, "Wide", 6f * cardPixel, bandH, new Color(1f, 1f, 1f, 0.28f));
            shine.thin = Band(rt, "Thin", 2f * cardPixel, bandH, new Color(1f, 1f, 1f, 0.6f));

            // Pixel sparkles: tiny plus shapes at fixed spots, twinkling out of step.
            var spots = new[] { new Vector2(0.24f, 0.78f), new Vector2(0.76f, 0.62f), new Vector2(0.4f, 0.22f) };
            shine.sparkles = new Image[spots.Length];
            for (int i = 0; i < spots.Length; i++)
                shine.sparkles[i] = Sparkle(rt, spots[i], cardPixel);
            shine.Update();
            return shine;
        }

        static RectTransform Band(RectTransform parent, string name, float w, float h, Color color)
        {
            var img = UIFactory.CreateImage(parent, name, color);
            img.rectTransform.sizeDelta = new Vector2(w, h);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Angle);
            return img.rectTransform;
        }

        static Image Sparkle(RectTransform parent, Vector2 at, float px)
        {
            // A 3x3-pixel plus: a horizontal and a vertical bar.
            var holder = UIFactory.CreateImage(parent, "Sparkle", Color.white);
            holder.color = new Color(1f, 1f, 1f, 0f);
            var rt = holder.rectTransform;
            rt.anchorMin = rt.anchorMax = at;
            rt.sizeDelta = new Vector2(3f * px, px);
            var v = UIFactory.CreateImage(rt, "V", Color.white);
            v.rectTransform.sizeDelta = new Vector2(px, 3f * px);
            return holder;
        }

        void Update()
        {
            float t = ((Time.unscaledTime + phase) % Period) / Sweep;   // 0..1 while sweeping
            bool on = t <= 1f;
            wide.gameObject.SetActive(on);
            thin.gameObject.SetActive(on);
            if (on)
            {
                float x = Mathf.Lerp(-width * 0.85f, width * 0.85f, t);
                x = Mathf.Round(x / pixel) * pixel;                    // move a whole card pixel at a time
                wide.anchoredPosition = new Vector2(x, 0f);
                thin.anchoredPosition = new Vector2(x + 5f * pixel, 0f);
            }
            for (int i = 0; i < sparkles.Length; i++)
            {
                float s = Mathf.Sin((Time.unscaledTime + phase) * 3.1f + i * 2.1f);
                float a = Mathf.Clamp01((s - 0.55f) / 0.45f);       // mostly off, short twinkles
                var c = new Color(1f, 1f, 1f, a * 0.9f);
                sparkles[i].color = c;
                sparkles[i].transform.GetChild(0).GetComponent<Image>().color = c;
            }
        }
    }
}
