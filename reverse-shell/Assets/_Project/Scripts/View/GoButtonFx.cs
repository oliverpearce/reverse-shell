using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The battle's Go button flourish (on an ordinary InkStyle button, so it has the same ink plate and corner brackets
    /// as the rest of the UI). Hovering it slides the chevron icon (CardIcons.GoChevron) out to the right of the word,
    /// looping, while the word eases left. Pressing it (a click, or Enter through <see cref="Press"/>) punches the button and flashes it.
    /// Unscaled time.
    /// </summary>
    public class GoButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform label;
        Image chevron;
        Image flash;
        bool hovered;
        float t;
        Vector2 labelHome;

        public static GoButtonFx Add(Button button)
        {
            var fx = button.gameObject.AddComponent<GoButtonFx>();
            fx.Build((RectTransform)button.transform);
            button.onClick.AddListener(fx.Press);
            return fx;
        }

        Text labelText;
        string centredFor;
        Vector2 labelBase;

        void Build(RectTransform root)
        {
            var found = root.Find("Label");
            label = found != null ? (RectTransform)found : null;
            labelText = label != null ? label.GetComponent<Text>() : null;
            if (label != null) labelHome = labelBase = label.anchoredPosition;
            chevron = UIFactory.CreateImage(root, "Chevron", new Color(1f, 1f, 1f, 0f));
            chevron.sprite = CardIcons.GoChevron;
            chevron.preserveAspect = true;
            chevron.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(48f, 48f));
            flash = UIFactory.CreateImage(root, "Flash", new Color(1f, 0.95f, 0.8f, 0f));
            flash.rectTransform.Fill();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }

        public void OnPointerExit(PointerEventData eventData) { hovered = false; }

        void OnDisable()
        {
            hovered = false;
            t = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            CentreLabel();
            t = Mathf.MoveTowards(t, hovered ? 1f : 0f, dt * 6f);
            float ease = t * t * (3f - 2f * t);
            if (label != null) label.anchoredPosition = labelHome + new Vector2(-22f * ease, 0f);
            float time = Time.unscaledTime;
            // The chevrons start just right of the word, whatever it says ("Go", "Continue"), and slide out and fade on
            // a loop.
            float start = labelText != null ? labelText.preferredWidth / 2f - 22f * ease + 6f : 30f;
            float phase = Mathf.Repeat(time * 1.6f, 1f);
            chevron.rectTransform.anchoredPosition = new Vector2(start + phase * 18f, 0f);
            chevron.color = new Color(1f, 1f, 1f, ease * Mathf.Sin(phase * Mathf.PI));
            if (flash.color.a > 0f)
            {
                var c = flash.color;
                c.a = Mathf.Max(0f, c.a - dt * 3f);
                flash.color = c;
            }
        }

        /// <summary>
        /// The pixel font sits off-centre in its line box, so the word is centred on its drawn glyphs instead: once the
        /// text has been generated (and again whenever it changes), the gap between the glyphs' middle and the label's
        /// middle becomes a vertical nudge.
        /// </summary>
        void CentreLabel()
        {
            if (labelText == null || labelText.text == centredFor) return;
            var verts = labelText.cachedTextGenerator.verts;
            if (verts.Count == 0) return;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts)
            {
                min = Mathf.Min(min, v.position.y);
                max = Mathf.Max(max, v.position.y);
            }
            float glyphMiddle = (min + max) / 2f / labelText.pixelsPerUnit;
            float nudge = Mathf.Round(label.rect.center.y - glyphMiddle);
            labelHome = labelBase + new Vector2(0f, nudge);
            centredFor = labelText.text;
        }

        /// <summary>The press: a quick punch and a pale flash (also called for Enter).</summary>
        public void Press()
        {
            flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0.6f);
            if (isActiveAndEnabled) StartCoroutine(Punch());
        }

        IEnumerator Punch()
        {
            var rt = (RectTransform)transform;
            const float time = 0.18f;
            for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
            {
                float k = e / time;
                rt.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
    }
}
