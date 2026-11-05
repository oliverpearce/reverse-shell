using System;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Plays SpriteAnimationSet clips on one or more UI Images (e.g. the sprite plus its flash overlay).
    /// Loops "idle" by default; one-shot clips return to idle unless told to hold their last frame.
    /// </summary>
    public class UISpriteAnimator : MonoBehaviour
    {
        public const string Idle = "idle";

        SpriteAnimationSet set;
        Facing facing;
        Image[] targets;
        Sprite[] frames;
        SpriteAnimationClip clip;
        float time;
        bool holdLast, finished;
        Action onComplete;

        public string Current { get; private set; }

        /// <summary>The looping clip to rest on (battle uses "battle_idle" when a set has one: A3 holds her sword only in a fight).</summary>
        public string IdleClip = Idle;

        /// <summary>Animate on unscaled time (for menus shown while the game is paused).</summary>
        public bool UnscaledTime;

        /// <summary>Playback speed (1 = the clip's fps). <see cref="Desync"/> nudges it per unit.</summary>
        public float Speed = 1f;

        /// <summary>Idle loops start at a random point (and come back from one-shots at one), so a row of units
        /// doesn't breathe in lockstep.</summary>
        public bool RandomPhase;

        /// <summary>Breaks lockstep: random idle phase and a small speed jitter (+/- <paramref name="jitter"/>).</summary>
        public void Desync(float jitter = 0.08f)
        {
            RandomPhase = true;
            Speed = UnityEngine.Random.Range(1f - jitter, 1f + jitter);
            if (clip != null && clip.loop && frames != null && frames.Length > 1) time = RandomTime(clip, frames.Length);
        }

        /// <summary>A start time somewhere in one loop of the clip.</summary>
        public static float RandomTime(SpriteAnimationClip c, int frameCount)
        {
            if (c == null || c.fps <= 0f || frameCount <= 1) return 0f;
            return UnityEngine.Random.Range(0f, frameCount / c.fps);
        }

        public void Init(SpriteAnimationSet animationSet, Facing face, params Image[] images)
        {
            set = animationSet;
            facing = face;
            targets = images;
            Play(IdleClip);
        }

        /// <summary>Length of a clip in seconds (0 if missing or looping forever).</summary>
        public float Duration(string clipName)
        {
            var c = set != null ? set.Find(clipName) : null;
            if (c == null || c.fps <= 0f) return 0f;
            var f = set.Frames(clipName, facing, false);
            return f != null ? f.Length / c.fps : 0f;
        }

        public bool Play(string clipName, bool holdLastFrame = false, Action completed = null)
        {
            if (set == null) return false;
            var c = set.Find(clipName);
            var f = c != null ? set.Frames(clipName, facing, false) : null;
            if (f == null || f.Length == 0)
            {
                if (completed != null) completed();
                return false;
            }
            clip = c;
            frames = f;
            Current = clipName;
            time = RandomPhase && c.loop && clipName == IdleClip ? RandomTime(c, f.Length) : 0f;
            holdLast = holdLastFrame;
            finished = false;
            onComplete = completed;
            Apply(Mathf.Clamp(Mathf.FloorToInt(time * Mathf.Max(0.01f, c.fps)), 0, f.Length - 1));
            return true;
        }

        void Update()
        {
            if (frames == null || finished) return;
            time += (UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * Speed;
            int index = Mathf.FloorToInt(time * Mathf.Max(0.01f, clip.fps));

            if (index >= frames.Length)
            {
                if (clip.loop)
                {
                    index %= frames.Length;
                }
                else
                {
                    Apply(frames.Length - 1);
                    finished = true;
                    var done = onComplete;
                    onComplete = null;
                    if (!holdLast) Play(IdleClip);
                    if (done != null) done();
                    return;
                }
            }
            Apply(index);
        }

        void Apply(int index)
        {
            foreach (var img in targets)
                if (img != null) img.sprite = frames[index];
        }
    }
}
