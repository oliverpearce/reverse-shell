using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// HD-2D style character: a lit, upright pixel-art billboard facing the camera, with a blob
    /// shadow on the ground. Frames are [row, col] in RPG Maker layout: rows = down, left, right, up;
    /// cols = 3 walk frames (middle = standing).
    /// </summary>
    public class CharacterSprite : MonoBehaviour
    {
        public const int Down = 0, Left = 1, Right = 2, Up = 3;
        static readonly int[] WalkCycle = { 0, 1, 2, 1 };

        public int Facing { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        Sprite[,] frames;
        SpriteAnimationSet animations;
        bool moving, running;
        float animTime;
        float bobAmount;
        float spriteScale = 1f;
        Vector3 spriteBase;
        Camera cam;

        public static CharacterSprite Create(Transform parent, string name, Sprite[,] frames, float scale, Vector3 position, float idleBob = 0f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var cs = root.AddComponent<CharacterSprite>();
            cs.frames = frames;
            cs.bobAmount = idleBob;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(root.transform, false);
            spriteGo.transform.localScale = Vector3.one * scale;
            cs.Renderer = spriteGo.AddComponent<SpriteRenderer>();
            cs.Renderer.sharedMaterial = WorldArt.SpriteLit();
            cs.Renderer.color = WorldArt.SpriteTint;
            cs.Renderer.sprite = frames[0, Mathf.Min(1, frames.GetLength(1) - 1)];
            cs.spriteBase = spriteGo.transform.localPosition;
            cs.spriteScale = scale;

            var shadow = WorldArt.Primitive(PrimitiveType.Quad, "Shadow", root.transform, WorldArt.Blob());
            shadow.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(0.75f, 0.4f, 1f) * scale;
            return cs;
        }

        /// <summary>Animated character from a SpriteAnimationSet (LPC: 64px frames, ~32x48 body). Uses "walk" and "idle".</summary>
        public static CharacterSprite Create(Transform parent, string name, SpriteAnimationSet set, float scale, Vector3 position)
        {
            var standing = set.Frames("idle", Combat.Facing.Down, true) ?? set.Frames("walk", Combat.Facing.Down, true);
            var cs = Create(parent, name, new Sprite[1, 1] { { standing != null ? standing[0] : null } }, scale, position);
            cs.animations = set;
            var shadow = cs.transform.Find("Shadow");
            if (shadow != null) shadow.localScale = new Vector3(0.42f, 0.2f, 1f) * scale;
            return cs;
        }

        public void SetFacing(int facing)
        {
            Facing = Mathf.Clamp(facing, 0, 3);
        }

        /// <summary>Faces the dominant axis of a movement direction (x = east, y = north).</summary>
        public void Face(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) SetFacing(dir.x > 0 ? Right : Left);
            else SetFacing(dir.y > 0 ? Up : Down);
        }

        public void SetMoving(bool isMoving, bool isRunning = false)
        {
            if (isMoving && !moving) animTime = 0f;
            moving = isMoving;
            running = isRunning;
        }

        // A held pose (the Esc menu's "sit"): the clip plays once on unscaled time and stays on its last frame.
        string pose;
        float poseTime;

        /// <summary>Plays <paramref name="clip"/> once and holds its last frame until <see cref="ClearPose"/>
        /// (<paramref name="instant"/>: straight to the last frame). A set without that clip (or a plain sheet) squashes
        /// the standing frame down a little instead.</summary>
        public void PlayPose(string clip, bool instant = false)
        {
            pose = clip;
            poseTime = instant ? 999f : 0f;
        }

        public void ClearPose()
        {
            pose = null;
            animTime = 0f;
        }

        public bool HasPose { get { return pose != null; } }

        /// <summary>This character's animation set has a drawn clip with this name (e.g. "read", the Cards book).</summary>
        public bool HasClip(string clip) { return animations != null && animations.Find(clip) != null; }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                // Octopath-style upright billboard: turn to the camera's yaw but stay vertical, so heads never lean
                // back into walls. Stretch vertically by 1/cos(pitch) so proportions read correctly from above.
                var euler = cam.transform.eulerAngles;
                Renderer.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), 0f, 70f) * Mathf.Deg2Rad;
                Renderer.transform.localScale = new Vector3(spriteScale, spriteScale / Mathf.Cos(pitch), spriteScale);
            }

            animTime += Time.deltaTime * (running ? 1.6f : 1f);
            if (pose != null && AnimatePose()) return;
            if (animations != null)
            {
                AnimateFromSet();
                return;
            }
            int rows = frames.GetLength(0), cols = frames.GetLength(1);
            int col = cols >= 3 && moving ? WalkCycle[(int)(animTime * 7f) % WalkCycle.Length] : Mathf.Min(1, cols - 1);
            Renderer.sprite = frames[Facing % rows, col];

            float bob = bobAmount > 0f && !moving ? Mathf.Abs(Mathf.Sin(Time.time * 2.2f + transform.position.x)) * bobAmount : 0f;
            Renderer.transform.localPosition = spriteBase + new Vector3(0f, bob, 0f);
        }

        static readonly Combat.Facing[] FacingByIndex = { Combat.Facing.Down, Combat.Facing.Left, Combat.Facing.Right, Combat.Facing.Up };

        /// <summary>The held pose; false if there is nothing to show (then the usual idle runs).</summary>
        bool AnimatePose()
        {
            poseTime += Time.unscaledDeltaTime;
            var clip = animations != null ? animations.Find(pose) : null;
            var set = clip != null ? animations.Frames(pose, FacingByIndex[Facing], true) : null;
            if (set != null && set.Length > 0)
            {
                Renderer.sprite = set[Mathf.Min(set.Length - 1, (int)(poseTime * Mathf.Max(1f, clip.fps)))];
                Renderer.transform.localPosition = spriteBase;
                return true;
            }
            // No drawn pose: keep the standing frame and sink it a little (squashed from the feet).
            if (animations != null) AnimateFromSet();
            float sink = Mathf.Lerp(1f, 0.8f, Mathf.Clamp01(poseTime / 0.25f));
            var s = Renderer.transform.localScale;
            Renderer.transform.localScale = new Vector3(s.x * (2f - sink) * 0.98f, s.y * sink, s.z);
            return true;
        }

        void AnimateFromSet()
        {
            string clipName = moving && animations.Has("walk") ? "walk" : "idle";
            var clip = animations.Find(clipName);
            var set = clip != null ? animations.Frames(clipName, FacingByIndex[Facing], true) : null;
            if (set == null || set.Length == 0) return;
            float fps = clip.fps * (moving && running ? 1.5f : 1f);
            int index = (int)(animTime * fps) % set.Length;
            Renderer.sprite = set[index];
            Renderer.transform.localPosition = spriteBase;
        }
    }
}
