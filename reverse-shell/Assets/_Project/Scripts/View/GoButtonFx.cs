using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The battle's Go button flourish (on an ordinary InkStyle button, so it has the same ink plate and corner brackets
    /// as the rest of the UI). Hovering it slides a pair of gold chevrons out to the right of the word, looping, while
    /// the word eases left. Pressing it (a click, or Enter through <see cref="Press"/>) punches the button and flashes it.
    /// Unscaled time.
    /// </summary>
    public class GoButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform label;
        Text[] chevrons;
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

        void Build(RectTransform root)
        {
            var labelText = root.Find("Label");
            label = labelText != null ? (RectTransform)labelText : null;
            if (label != null) labelHome = label.anchoredPosition;
            chevrons = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                var c = UIFactory.CreateText(root, "Chevron" + i, ">", 44, InkStyle.GoldBright, TextAnchor.MiddleCenter);
                c.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f + i * 26f, 2f), new Vector2(40f, 60f));
                c.color = new Color(c.color.r, c.color.g, c.color.b, 0f);
                chevrons[i] = c;
            }
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
            t = Mathf.MoveTowards(t, hovered ? 1f : 0f, dt * 6f);
            float ease = t * t * (3f - 2f * t);
            if (label != null) label.anchoredPosition = labelHome + new Vector2(-22f * ease, 0f);
            float time = Time.unscaledTime;
            for (int i = 0; i < chevrons.Length; i++)
            {
                // Each chevron slides out and fades, one after the other, on a loop.
                float phase = Mathf.Repeat(time * 1.6f - i * 0.35f, 1f);
                var rt = chevrons[i].rectTransform;
                rt.anchoredPosition = new Vector2(30f + i * 24f + phase * 18f, 2f);
                var c = chevrons[i].color;
                c.a = ease * Mathf.Sin(phase * Mathf.PI);
                chevrons[i].color = c;
            }
            if (flash.color.a > 0f)
            {
                var c = flash.color;
                c.a = Mathf.Max(0f, c.a - dt * 3f);
                flash.color = c;
            }
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
