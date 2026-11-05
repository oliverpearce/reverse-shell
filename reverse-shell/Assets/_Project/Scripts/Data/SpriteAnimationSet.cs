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
    ///   idle, walk, slash (attacks), spellcast (buffs, heals, rituals), hurt (defeat), sit (Esc menu), read (Cards), and
    ///   an optional battle_idle that replaces idle in battle (A3: sword out in a fight, put away everywhere else),
    ///   optional walk_s1..walk_s5 (Settings > Walk Style, see WalkClip), and an optional "portrait" (A3's A2 study) that
    ///   the Esc menu's party page and Soul cards show instead of idle; the overworld and battle never use it.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Sprite Animation Set", fileName = "Anim_New")]
    public class SpriteAnimationSet : ScriptableObject
    {
        [Min(1)] public int frameSize = 64;
        [Tooltip("Where the feet are, as a fraction of frame height from the bottom (overworld pivot).")]
        [Range(0f, 0.5f)] public float feetPivot = 0.04f;
        [Tooltip("0 = frameSize. Set smaller when the cells have spare room around art drawn at the crew's pixel size, e.g. "
               + "128 px cells (for wide sword swings) with art at 64 px scale: 64. Keeps every character at the same pixel density.")]
        [Min(0)] public int referenceSize;
        [Tooltip("The part of the first idle frame (facing down) shown on the Soul card, as fractions of the frame, from the bottom left.")]
        public Rect soulCrop = new Rect(0.25f, 0.02f, 0.5f, 0.62f);
        [Tooltip("The Soul card's crop of the \"portrait\" clip (when the set has one), as fractions of the frame from the bottom left.")]
        public Rect portraitCrop = new Rect(0.25f, 0.02f, 0.5f, 0.62f);
        public List<SpriteAnimationClip> clips = new List<SpriteAnimationClip>();
        [Tooltip("The same character in the other look (Settings > Machine Look, GameSettings.AltArt): the machines and " +
                 "NPCs have a Tech Dungeon version (Art/Characters/Alt_<name>). Null = one look only.")]
        public SpriteAnimationSet alternate;

        [Tooltip("The dialogue face (fractions of a frame, from the bottom left): a square on the head and shoulders. " +
                 "Width 0 = worked out from the portrait crop (or the Soul card crop).")]
        public Rect faceCrop;

        /// <summary>The face square for dialogue (fractions of a frame): <see cref="faceCrop"/>, or the top of the
        /// portrait crop (sets with a portrait clip) / Soul card crop, centred.</summary>
        public Rect FaceRect
        {
            get
            {
                if (faceCrop.width > 0f && faceCrop.height > 0f) return faceCrop;
                var c = Has(Portrait) ? portraitCrop : soulCrop;
                float side = Mathf.Clamp(c.height * 0.5f, 0.05f, 1f);
                return new Rect(c.center.x - side * 0.5f, c.yMax - side, side, side);
            }
        }

        /// <summary>This set, or its <see cref="alternate"/> while Settings > Machine Look picks the other look.</summary>
        public SpriteAnimationSet Current
        {
            get { return alternate != null && Combat.View.GameSettings.AltArt ? alternate : this; }
        }

        [Tooltip("Hex colours (e.g. dae0ea) that Tinted() recolours: the protagonist's hair and eye ramps, drawn in greys to " +
                 "white so the game can paint them in her resonance colour. Written by Tools/ChibiForge/alt_recolour.py.")]
        public List<string> tintColors = new List<string>();

        [NonSerialized] Dictionary<string, Sprite[]> cache;
        [NonSerialized] Dictionary<int, SpriteAnimationSet> tintedSets;
        [NonSerialized] bool isTinted;
        [NonSerialized] Color tint = Color.white;
        [NonSerialized] HashSet<int> tintKeys;

        /// <summary>This set with its <see cref="tintColors"/> pixels painted in <paramref name="color"/> (shading kept:
        /// each pixel takes the colour at its own brightness). A runtime copy, cached per colour; each sheet is tinted the
        /// first time one of its frames is asked for. Returns this set when it has no tint colours.</summary>
        public SpriteAnimationSet Tinted(Color color)
        {
            if (tintColors == null || tintColors.Count == 0 || isTinted) return this;
            Color32 c32 = color;
            int key = (c32.r << 16) | (c32.g << 8) | c32.b;
            if (tintedSets == null) tintedSets = new Dictionary<int, SpriteAnimationSet>();
            SpriteAnimationSet copy;
            if (tintedSets.TryGetValue(key, out copy) && copy != null) return copy;
            copy = Instantiate(this);
            copy.name = name;
            copy.isTinted = true;
            copy.tint = color;
            copy.tintKeys = new HashSet<int>();
            foreach (var hex in tintColors)
            {
                Color parsed;
                if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out parsed))
                {
                    Color32 p = parsed;
                    copy.tintKeys.Add((p.r << 16) | (p.g << 8) | p.b);
                }
            }
            tintedSets[key] = copy;
            return copy;
        }

        static readonly Dictionary<string, Texture2D> tintedSheets = new Dictionary<string, Texture2D>();

        /// <summary>The clip's sheet as this (tinted) set draws it.</summary>
        Texture2D SheetOf(SpriteAnimationClip clip)
        {
            if (!isTinted || clip.sheet == null || tintKeys == null || tintKeys.Count == 0) return clip.sheet;
            Color32 c32 = tint;
            string key = clip.sheet.GetInstanceID() + "/" + c32.r + "," + c32.g + "," + c32.b;
            Texture2D done;
            if (tintedSheets.TryGetValue(key, out done) && done != null) return done;
            done = TintSheet(clip.sheet, tintKeys, tint);
            tintedSheets[key] = done;
            return done;
        }

        /// <summary>A copy of <paramref name="sheet"/> with every pixel whose colour is in <paramref name="keys"/> (0xRRGGBB)
        /// painted in <paramref name="color"/> at its own brightness; the brightest highlights stay a little whiter. The
        /// sheet must be readable (A3's .metas are); otherwise it comes back unchanged.</summary>
        public static Texture2D TintSheet(Texture2D sheet, HashSet<int> keys, Color color)
        {
            Color32[] px;
            try { px = sheet.GetPixels32(); }
            catch (UnityException) { return sheet; }
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.a == 0 || !keys.Contains((p.r << 16) | (p.g << 8) | p.b)) continue;
                px[i] = TintPixel(p, color);
            }
            var tex = new Texture2D(sheet.width, sheet.height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = sheet.name + "_tinted";
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>One tinted pixel: the colour at the pixel's brightness, highlights eased toward white.</summary>
        public static Color32 TintPixel(Color32 p, Color color)
        {
            float lum = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            float white = Mathf.Clamp01((lum - 0.85f) / 0.15f) * 0.35f;
            var c = Color.Lerp(color * lum, Color.white * lum, white);
            return new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), p.a);
        }
        [NonSerialized] Dictionary<Sprite, Sprite> windows;

        /// <summary>Pixels per world unit: the reference size, so a frame is one unit across at 64 px scale.</summary>
        public int PixelsPerUnit { get { return referenceSize > 0 ? referenceSize : frameSize; } }

        /// <summary>How much bigger than a standard frame to draw this set's frames in UI that sizes a frame as one
        /// 64-px-scale cell (1 for ordinary sets, 2 for 128 px cells with 64 px art).</summary>
        public float CellScale { get { return frameSize / (float)PixelsPerUnit; } }

        /// <summary>The reference-size window of a frame (bottom centre, where the figure stands), so the figure fills a
        /// portrait the way a standard frame's does. Returns the frame itself for ordinary sets.</summary>
        public Sprite Window(Sprite frame)
        {
            if (frame == null || PixelsPerUnit >= frameSize) return frame;
            if (windows == null) windows = new Dictionary<Sprite, Sprite>();
            Sprite s;
            if (windows.TryGetValue(frame, out s) && s != null) return s;
            var r = frame.rect;
            float side = PixelsPerUnit;
            var rect = new Rect(r.x + (r.width - side) / 2f, r.y, side, side);
            s = Sprite.Create(frame.texture, rect, new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = frame.name + "_window";
            windows[frame] = s;
            return s;
        }

        public SpriteAnimationClip Find(string clipName)
        {
            foreach (var c in clips)
                if (c != null && c.sheet != null && c.name == clipName) return c;
            return null;
        }

        public bool Has(string clipName) { return Find(clipName) != null; }

        /// <summary>The menus' and cards' still of the character (A3: the A2 study, Tools/ChibiForge/a3_portrait.py).</summary>
        public const string Portrait = "portrait";

        /// <summary>The clip menus show standing: the portrait if the set has one, else idle.</summary>
        public string MenuClip { get { return Has(Portrait) ? Portrait : "idle"; } }

        /// <summary>The walk to play: walk_s&lt;style&gt; (Settings > Walk Style) when the set has it, else walk.</summary>
        public string WalkClip(int style)
        {
            string styled = "walk_s" + style;
            return Has(styled) ? styled : "walk";
        }

        /// <summary>Frames for a clip and facing, pivoted at the feet (world) or center (UI). Cached.</summary>
        public Sprite[] Frames(string clipName, Facing facing, bool feetPivoted)
        {
            var clip = Find(clipName);
            if (clip == null) return null;
            if (cache == null) cache = new Dictionary<string, Sprite[]>();

            string key = clipName + "/" + facing + "/" + feetPivoted;
            Sprite[] frames;
            if (cache.TryGetValue(key, out frames) && frames != null && frames.Length > 0 && frames[0] != null) return frames;

            var sheet = SheetOf(clip);
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
                frames[i] = Sprite.Create(sheet, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
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
