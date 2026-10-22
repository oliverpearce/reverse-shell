using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    public enum StepTarget { Caster, Targets }

    /// <summary>
    /// One piece of a cast animation. Subclass with [Serializable] to add new ones; they appear in
    /// the CastSequence inspector's dropdown automatically.
    /// </summary>
    [Serializable]
    public abstract class CastStep
    {
        [Tooltip("Start the next step immediately instead of waiting for this one.")]
        public bool runInParallel;

        public abstract IEnumerator Play(CastContext ctx);
    }

    /// <summary>Plays a body animation clip (e.g. "slash", "spellcast") from the unit's SpriteAnimationSet.</summary>
    [Serializable]
    public class PlayAnimationStep : CastStep
    {
        public string clip = "slash";
        public StepTarget at = StepTarget.Caster;
        [Tooltip("Wait for the clip to finish before the next step.")]
        public bool waitForEnd;

        public override IEnumerator Play(CastContext ctx)
        {
            float longest = 0f;
            foreach (var v in ctx.Resolve(at))
                longest = Mathf.Max(longest, v.PlayAnimation(clip));
            if (waitForEnd && longest > 0f) yield return Tween.Wait(longest);
        }
    }

    [Serializable]
    public class WaitStep : CastStep
    {
        public float duration = 0.2f;

        public override IEnumerator Play(CastContext ctx) { yield return Tween.Wait(duration); }
    }

    /// <summary>Caster dashes toward the targets and back.</summary>
    [Serializable]
    public class LungeStep : CastStep
    {
        public float distance = 90f;
        public float duration = 0.3f;

        public override IEnumerator Play(CastContext ctx)
        {
            if (ctx.Caster == null) yield break;
            var body = ctx.Caster.Body;
            Vector2 origin = ctx.Caster.BodyOrigin;
            Vector3 dir3 = ctx.TargetsCenter() - ctx.Caster.Center;
            Vector2 dir = new Vector2(dir3.x, dir3.y).normalized;
            if (dir == Vector2.zero) dir = ctx.Caster.Unit.Team == Team.Player ? Vector2.right : Vector2.left;

            yield return Tween.Run(duration * 0.45f, p => { if (body != null) body.anchoredPosition = origin + dir * distance * p; }, Tween.OutCubic);
            yield return Tween.Run(duration * 0.55f, p => { if (body != null) body.anchoredPosition = origin + dir * distance * (1f - p); }, Tween.InOutQuad);
            if (body != null) body.anchoredPosition = origin;
        }
    }

    /// <summary>Tints the portrait(s) with a color that fades out.</summary>
    [Serializable]
    public class FlashStep : CastStep
    {
        public StepTarget at = StepTarget.Targets;
        public Color color = Color.white;
        [Tooltip("Ignore Color and use the caster's color.")]
        public bool useCasterColor;
        public float duration = 0.2f;

        public override IEnumerator Play(CastContext ctx)
        {
            var c = useCasterColor ? ctx.CasterColor : color;
            var routines = new List<Coroutine>();
            foreach (var v in ctx.Resolve(at))
                routines.Add(ctx.View.StartCoroutine(v.Flash(c, duration)));
            foreach (var r in routines) yield return r;
        }
    }

    /// <summary>Quick scale pulse on the portrait(s).</summary>
    [Serializable]
    public class PulseStep : CastStep
    {
        public StepTarget at = StepTarget.Caster;
        public float amount = 0.12f;
        public float duration = 0.25f;

        public override IEnumerator Play(CastContext ctx)
        {
            var routines = new List<Coroutine>();
            foreach (var v in ctx.Resolve(at))
                routines.Add(ctx.View.StartCoroutine(Tween.Punch(v.Body, amount, duration)));
            foreach (var r in routines) yield return r;
        }
    }

    /// <summary>A glowing square flies from the caster to each target.</summary>
    [Serializable]
    public class ProjectileStep : CastStep
    {
        public Color color = Color.white;
        public bool useCasterColor = true;
        public float size = 34f;
        public float duration = 0.25f;

        public override IEnumerator Play(CastContext ctx)
        {
            if (ctx.Caster == null) yield break;
            var c = useCasterColor ? ctx.CasterColor : color;
            var routines = new List<Coroutine>();
            foreach (var target in ctx.Targets)
            {
                var img = UIFactory.CreateImage(ctx.View.FxLayer, "Projectile", Color.Lerp(c, Color.white, 0.4f));
                img.rectTransform.sizeDelta = new Vector2(size, size);
                img.rectTransform.position = ctx.Caster.Center;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                routines.Add(ctx.View.StartCoroutine(Fly(img, target.Center)));
            }
            foreach (var r in routines) yield return r;
        }

        IEnumerator Fly(Image img, Vector3 to)
        {
            yield return Tween.MoveWorld(img.rectTransform, to, duration, Tween.InOutQuad);
            if (img != null) UnityEngine.Object.Destroy(img.gameObject);
        }
    }

    [Serializable]
    public class ScreenShakeStep : CastStep
    {
        public float strength = 10f;
        public float duration = 0.25f;

        public override IEnumerator Play(CastContext ctx)
        {
            yield return Tween.Shake(ctx.View.Stage, Vector2.zero, strength, duration);
        }
    }

    /// <summary>
    /// Spawns a prefab at the caster or each target, under the overlay canvas. Use UI prefabs
    /// (Image + Animator, etc.); world-space particle systems won't render on an overlay canvas.
    /// </summary>
    [Serializable]
    public class SpawnVfxStep : CastStep
    {
        public GameObject prefab;
        public StepTarget at = StepTarget.Targets;
        public float lifetime = 1f;
        [Tooltip("How long to wait before the next step (when not parallel).")]
        public float hold = 0.2f;

        public override IEnumerator Play(CastContext ctx)
        {
            if (prefab == null) yield break;
            foreach (var v in ctx.Resolve(at))
            {
                var go = UnityEngine.Object.Instantiate(prefab, ctx.View.FxLayer);
                go.transform.position = v.Center;
                UnityEngine.Object.Destroy(go, lifetime);
            }
            yield return Tween.Wait(hold);
        }
    }
}
