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

    /// <summary>
    /// The attack: the camera pushes in on attacker and target, the caster crouches, dashes most of the way across, and
    /// the step ends on the slash clip's strike frame, so the next step (the target's flash) lands with the blow. The hop
    /// back home and the zoom out carry on behind it. Plays the "slash" clip itself (PlayCast doesn't add one).
    /// </summary>
    [Serializable]
    public class LungeStep : CastStep
    {
        [Tooltip("How far to dash at least (canvas px); it goes further when the target is far away.")]
        public float distance = 90f;
        [Tooltip("The dash and the hop back together (s).")]
        public float duration = 0.3f;
        [Tooltip("Share of the gap to the target covered by the dash (it stops short so the two don't overlap).")]
        public float reach = 0.62f;
        [Tooltip("Keep at least this much room (canvas px) between attacker and target.")]
        public float gap = 200f;
        [Tooltip("The anticipation crouch before the dash (s).")]
        public float windup = 0.08f;
        [Tooltip("Camera push-in on attacker and target (1 = none); a hit on several targets pushes in half as much.")]
        public float zoom = 1.25f;

        /// <summary>Which frame of "slash" is the blow (A3's model: frame 4, the raised strike).</summary>
        public const float ImpactShare = 0.5f;

        public override IEnumerator Play(CastContext ctx)
        {
            if (ctx.Caster == null) yield break;
            var body = ctx.Caster.Body;
            Vector2 origin = ctx.Caster.BodyOrigin;
            // The gap in the unit's own space, measured before the camera moves.
            var space = body.parent as RectTransform;
            Vector3 from = space != null ? space.InverseTransformPoint(ctx.Caster.Center) : ctx.Caster.Center;
            Vector3 to = space != null ? space.InverseTransformPoint(ctx.TargetsCenter()) : ctx.TargetsCenter();
            Vector2 delta = new Vector2(to.x - from.x, to.y - from.y);
            Vector2 dir = delta.sqrMagnitude > 1f ? delta.normalized
                : (ctx.Caster.Unit.Team == Team.Player ? Vector2.right : Vector2.left);
            float dash = Mathf.Max(distance, Mathf.Min(delta.magnitude * reach, delta.magnitude - gap));

            float push = ctx.Targets.Count > 1 ? 1f + (zoom - 1f) * 0.5f : zoom;
            if (ctx.Ritual) push += 0.1f;
            if (push > 1.001f) ctx.View.CameraFocus(ctx.Caster.Center, ctx.TargetsCenter(), push, 0.18f);

            float clip = ctx.Caster.PlayAnimation("slash");
            float impact = clip > 0f ? clip * ImpactShare : windup + duration * 0.45f;
            float dashTime = Mathf.Max(0.08f, Mathf.Min(duration * 0.5f, impact - windup));
            float t = 0f;
            // Crouch (the clip's first frames), then the dash, arriving on the blow.
            yield return Tween.Wait(Mathf.Max(0f, impact - dashTime));
            t += Mathf.Max(0f, impact - dashTime);
            yield return Tween.Run(dashTime, p => { if (body != null) body.anchoredPosition = origin + dir * dash * p; }, Tween.OutCubic);
            ctx.View.StartCoroutine(HopBack(ctx, body, origin, dir * dash));
        }

        IEnumerator HopBack(CastContext ctx, RectTransform body, Vector2 origin, Vector2 offset)
        {
            yield return Tween.Wait(0.12f);   // the follow-through holds a beat
            float back = Mathf.Max(0.16f, duration * 0.75f);
            yield return Tween.Run(back, p =>
            {
                if (body == null) return;
                // a low hop home: up a little at the middle, no squash
                body.anchoredPosition = origin + offset * (1f - p) + new Vector2(0f, Mathf.Sin(p * Mathf.PI) * 18f);
            }, Tween.InOutQuad);
            if (body != null) body.anchoredPosition = origin;
            ctx.View.CameraReset(0.25f);
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
            yield return ctx.View.ShakeStage(strength, duration);
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
