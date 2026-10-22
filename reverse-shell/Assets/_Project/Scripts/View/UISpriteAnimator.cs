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

        public void Init(SpriteAnimationSet animationSet, Facing face, params Image[] images)
        {
            set = animationSet;
            facing = face;
            targets = images;
            Play(Idle);
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
            time = 0f;
            holdLast = holdLastFrame;
            finished = false;
            onComplete = completed;
            Apply(0);
            return true;
        }

        void Update()
        {
            if (frames == null || finished) return;
            time += Time.deltaTime;
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
                    if (!holdLast) Play(Idle);
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
