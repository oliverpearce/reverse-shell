using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>Tiny coroutine tween library. Every helper tolerates its target being destroyed mid-tween.</summary>
    public static class Tween
    {
        public static float Linear(float t) { return t; }
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InOutQuad(float t) { return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static IEnumerator Run(float duration, Action<float> onUpdate, Func<float, float> ease = null)
        {
            if (ease == null) ease = OutCubic;
            if (duration <= 0f)
            {
                onUpdate(ease(1f));
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                onUpdate(ease(Mathf.Clamp01(t / duration)));
                yield return null;
            }
        }

        public static IEnumerator MoveWorld(Transform target, Vector3 to, float duration, Func<float, float> ease = null)
        {
            if (target == null) yield break;
            Vector3 from = target.position;
            yield return Run(duration, p => { if (target != null) target.position = Vector3.LerpUnclamped(from, to, p); }, ease);
        }

        public static IEnumerator MoveAnchored(RectTransform target, Vector2 to, float duration, Func<float, float> ease = null)
        {
            if (target == null) yield break;
            Vector2 from = target.anchoredPosition;
            yield return Run(duration, p => { if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, p); }, ease);
        }

        public static IEnumerator Scale(Transform target, Vector3 to, float duration, Func<float, float> ease = null)
        {
            if (target == null) yield break;
            Vector3 from = target.localScale;
            yield return Run(duration, p => { if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, p); }, ease);
        }

        /// <summary>Scales up by <paramref name="amount"/> and back to 1.</summary>
        public static IEnumerator Punch(Transform target, float amount, float duration)
        {
            yield return Run(duration, p =>
            {
                if (target != null) target.localScale = Vector3.one * (1f + amount * Mathf.Sin(p * Mathf.PI));
            }, Linear);
        }

        /// <summary>Random jitter around <paramref name="origin"/>, ending exactly on it.</summary>
        public static IEnumerator Shake(RectTransform target, Vector2 origin, float strength, float duration)
        {
            yield return Run(duration, p =>
            {
                if (target != null) target.anchoredPosition = origin + UnityEngine.Random.insideUnitCircle * strength * (1f - p);
            }, Linear);
            if (target != null) target.anchoredPosition = origin;
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            if (group == null) yield break;
            float from = group.alpha;
            yield return Run(duration, p => { if (group != null) group.alpha = Mathf.Lerp(from, to, p); }, Linear);
        }

        public static IEnumerator ColorTo(Graphic graphic, Color to, float duration)
        {
            if (graphic == null) yield break;
            Color from = graphic.color;
            yield return Run(duration, p => { if (graphic != null) graphic.color = Color.LerpUnclamped(from, to, p); }, Linear);
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
