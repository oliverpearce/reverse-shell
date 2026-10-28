using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A volume slider inside a settings row: a dark track filling with gold, a cream knob and the percent to its left,
    /// on the row's right side. Click or drag along it; <see cref="Step"/> moves it 10% (Left / Right in the menus).
    /// </summary>
    public class VolumeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        Func<float> get;
        Action<float> set;
        RectTransform track, fill, knob;
        Text percent;
        /// <summary>Room kept at the row's right end.</summary>
        const float Inset = 36f;

        /// <param name="width">The track's length.</param>
        /// <param name="drop">How far below the row's middle it sits (a row with its label on a line above).</param>
        public static VolumeSlider Add(RectTransform row, Func<float> get, Action<float> set, float width = 360f, float drop = 0f)
        {
            var right = new Vector2(1f, 0.5f);
            var trackImage = UIFactory.CreateImage(row, "Slider", new Color(0f, 0f, 0f, 0.55f), true);
            var slider = trackImage.gameObject.AddComponent<VolumeSlider>();
            slider.get = get;
            slider.set = set;
            slider.track = trackImage.rectTransform;
            slider.track.Place(right, right, new Vector2(-Inset, -drop), new Vector2(width, 18f));
            InkStyle.Frame(slider.track, InkStyle.Rule, 2f);

            var fillImage = UIFactory.CreateImage(slider.track, "Fill", InkStyle.Gold);
            fillImage.raycastTarget = false;
            slider.fill = fillImage.rectTransform;
            slider.fill.anchorMin = Vector2.zero;
            slider.fill.offsetMin = slider.fill.offsetMax = Vector2.zero;

            var knobImage = UIFactory.CreateImage(slider.track, "Knob", InkStyle.Paper);
            knobImage.raycastTarget = false;
            slider.knob = knobImage.rectTransform;
            slider.knob.sizeDelta = new Vector2(14f, 36f);

            slider.percent = UIFactory.CreateText(row, "Percent", "", UIFactory.TextSize.Medium, InkStyle.Gold, TextAnchor.MiddleRight);
            slider.percent.raycastTarget = false;
            slider.percent.rectTransform.Place(right, right, new Vector2(-Inset - width - 20f, -drop), new Vector2(110f, 40f));
            slider.Refresh();
            return slider;
        }

        /// <summary>Moves 10% up (+1) or down (-1), on a whole 10%.</summary>
        public void Step(int direction)
        {
            Set(Mathf.Round(get() * 10f + direction) / 10f);
        }

        public void OnPointerDown(PointerEventData data) { Drag(data); }
        public void OnDrag(PointerEventData data) { Drag(data); }

        void Drag(PointerEventData data)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, data.position, data.pressEventCamera, out local)) return;
            float t = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
            Set(Mathf.Round(t * 20f) / 20f);   // 5% steps
        }

        void Set(float value)
        {
            set(Mathf.Clamp01(value));
            Refresh();
        }

        void Refresh()
        {
            float v = Mathf.Clamp01(get());
            fill.anchorMax = new Vector2(v, 1f);
            knob.anchorMin = knob.anchorMax = new Vector2(v, 0.5f);
            knob.anchoredPosition = Vector2.zero;
            percent.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }
}
