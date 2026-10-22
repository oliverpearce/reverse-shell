using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum Facing { Up, Left, Down, Right }

    /// <summary>
    /// A character's animations, one sheet per clip, frames laid out in a grid of square cells.
    /// Default layout is LPC: 4 rows = up, left, down, right (a 1-row sheet like "hurt" is used for every facing).
    /// Used by battle (UnitView) and the overworld (CharacterSprite). Clip names used by the game:
    ///   idle, walk, slash (attacks), spellcast (buffs, heals, rituals), hurt (defeat).
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Sprite Animation Set", fileName = "Anim_New")]
    public class SpriteAnimationSet : ScriptableObject
    {
        [Min(1)] public int frameSize = 64;
        [Tooltip("Where the feet are, as a fraction of frame height from the bottom (overworld pivot).")]
        [Range(0f, 0.5f)] public float feetPivot = 0.04f;
        public List<SpriteAnimationClip> clips = new List<SpriteAnimationClip>();

        [NonSerialized] Dictionary<string, Sprite[]> cache;

        public SpriteAnimationClip Find(string clipName)
        {
            foreach (var c in clips)
                if (c != null && c.sheet != null && c.name == clipName) return c;
            return null;
        }

        public bool Has(string clipName) { return Find(clipName) != null; }

        /// <summary>Frames for a clip and facing, pivoted at the feet (world) or center (UI). Cached.</summary>
        public Sprite[] Frames(string clipName, Facing facing, bool feetPivoted)
        {
            var clip = Find(clipName);
            if (clip == null) return null;
            if (cache == null) cache = new Dictionary<string, Sprite[]>();

            string key = clipName + "/" + facing + "/" + feetPivoted;
            Sprite[] frames;
            if (cache.TryGetValue(key, out frames) && frames != null && frames.Length > 0 && frames[0] != null) return frames;

            var sheet = clip.sheet;
            int rows = Mathf.Max(1, sheet.height / frameSize);
            int cols = Mathf.Max(1, sheet.width / frameSize);
            int row = rows >= 4 ? (int)facing : 0; // row 0 = top of the sheet
            int first = Mathf.Clamp(clip.firstFrame, 0, cols - 1);
            int count = clip.frameCount > 0 ? Mathf.Min(clip.frameCount, cols - first) : cols - first;
            var pivot = feetPivoted ? new Vector2(0.5f, feetPivot) : new Vector2(0.5f, 0.5f);

            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect((first + i) * frameSize, sheet.height - (row + 1) * frameSize, frameSize, frameSize);
                frames[i] = Sprite.Create(sheet, rect, pivot, frameSize, 0, SpriteMeshType.FullRect);
                frames[i].name = name + "_" + clipName + "_" + facing + "_" + i;
            }
            cache[key] = frames;
            return frames;
        }
    }

    [Serializable]
    public class SpriteAnimationClip
    {
        public string name = "idle";
        public Texture2D sheet;
        public float fps = 8f;
        public bool loop = true;
        [Tooltip("Skip leading frames (LPC walk frame 0 is the standing pose).")]
        [Min(0)] public int firstFrame;
        [Tooltip("0 = all remaining frames.")]
        [Min(0)] public int frameCount;
    }
}
