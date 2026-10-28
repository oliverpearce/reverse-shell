using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The pixel-art card pieces painted by Tools/CardForge into Resources/Cards: a 48 x 64 px card (shown at 4x in the
    /// hand) made of a grey swirl tinted with the owner's colour, a fixed frame (rim, pip strip, name plate), rank pips,
    /// a 32 px centre icon per card (CardArt.png + CardArt.txt) and full-art Ult cards per character (Ult_&lt;Char&gt;.png).
    /// Pixel positions here match the layout in card_forge.py.
    /// </summary>
    public static class CardArt
    {
        public const int Width = 48, Height = 64;
        /// <summary>Canvas units per card pixel on the hand card (CardView.Size / 48): 5x, big enough to read the name.</summary>
        public const float HandPixel = 5f;

        // Layout in card pixels (x from the left, y from the top).
        public const int PipX = 6, PipStep = 6, PipY = 6, PipSize = 5;
        public const int ArtX = 8, ArtY = 15, ArtSize = 32;
        /// <summary>Where a centre icon goes: its 34 px cell (the 32 px drawing plus its 1 px outline all round,
        /// card_forge.framed) over the art window, one pixel out on every side.</summary>
        public const int IconX = ArtX - 1, IconY = ArtY - 1, IconSize = ArtSize + 2;
        public static readonly RectInt NamePlate = new RectInt(7, 52, 34, 7);
        /// <summary>The type icon: 7x7 card pixels in the pip strip's right end (CardTypes.png, drawn at card pixel size).</summary>
        public static readonly RectInt TypeIcon = new RectInt(35, 5, 7, 7);

        static Sprite swirl, frame, pipOn, pipOff;
        static Sprite[] types;
        static Dictionary<string, Sprite> icons;
        static readonly Dictionary<string, Sprite> ults = new Dictionary<string, Sprite>();

        public static Sprite Swirl { get { return swirl != null ? swirl : swirl = Whole("Cards/CardSwirl"); } }
        public static Sprite Frame { get { return frame != null ? frame : frame = Whole("Cards/CardFrame"); } }

        public static Sprite Pip(bool lit)
        {
            if (pipOn == null)
            {
                var tex = Load("Cards/CardPips");
                if (tex == null) return null;
                pipOn = Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                pipOff = Sprite.Create(tex, new Rect(5, 0, 5, 5), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            return lit ? pipOn : pipOff;
        }

        /// <summary>The card type's 7x7 pixel icon for the pip strip (CardTypes.png), or null if it's missing.</summary>
        public static Sprite Type(CardIcons.Type type)
        {
            if (types == null)
            {
                var tex = Load("Cards/CardTypes");
                if (tex == null) return null;
                types = new Sprite[tex.width / TypeIcon.width];
                for (int i = 0; i < types.Length; i++)
                    types[i] = Sprite.Create(tex, new Rect(i * TypeIcon.width, 0, TypeIcon.width, TypeIcon.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            int k = (int)type;
            return k < types.Length ? types[k] : null;
        }

        /// <summary>Places a centre icon (CardArt.png cell or ArcanaIcon) on a card face.</summary>
        public static RectTransform PlaceIcon(RectTransform rt, float unit)
        {
            return PlacePixels(rt, IconX, IconY, IconSize, IconSize, unit);
        }

        /// <summary>A card's name on its plate, on one line: as big as fits the plate's width (up to 26 * s), shrinking for
        /// long names rather than wrapping.</summary>
        public static Text NameLabel(RectTransform root, string label, Color color, float s)
        {
            float u = HandPixel * s;
            var t = UIFactory.CreateText(root, "Name", (label ?? "").ToUpperInvariant(), Mathf.RoundToInt(26 * s), color, TextAnchor.MiddleCenter);
            PlacePixels(t.rectTransform, NamePlate.x, NamePlate.y - 1, NamePlate.width, NamePlate.height + 2, u);
            UIFactory.FitOneLine(t, (NamePlate.width - 2) * u);
            return t;
        }

        /// <summary>A small card's name on its plate in runes (<see cref="RuneText"/>, the enchanting-table look): the
        /// real name only shows on zoomed-in cards. The runes are as tall as the name's capital letters would be at this
        /// size (the same fitting as <see cref="NameLabel"/>), so switching between runes and words doesn't jump.</summary>
        public static RectTransform RuneLabel(RectTransform root, string label, Color color, float s)
        {
            float u = HandPixel * s;
            // Lay the words out as NameLabel would, read their capital height, then put runes there instead.
            var words = NameLabel(root, label, color, s);
            float cap = RuneText.CapHeight(words.font, words.fontSize);
            words.gameObject.SetActive(false);
            Object.Destroy(words.gameObject);
            var area = UIFactory.CreateRect("Name", root);
            PlacePixels(area, NamePlate.x + 1, NamePlate.y, NamePlate.width - 2, NamePlate.height, u);
            RuneText.Build(area, label, color, (NamePlate.width - 2) * u, NamePlate.height * u, cap);
            return area;
        }



        /// <summary>The card's centre icon (by card asset name), or null if CardForge hasn't drawn one.</summary>
        public static Sprite Icon(CardDefinition card)
        {
            return card == null ? null : Icon(card.name);
        }

        /// <summary>A centre icon by its CardArt.txt name (card asset names, or the menu cards' "Menu_Resume" etc.).</summary>
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (icons == null) LoadIcons();
            Sprite s;
            return icons.TryGetValue(name, out s) ? s : null;
        }

        /// <summary>The owner's full-art Ult picture (by character asset name), or null. Soul cards themselves are now
        /// drawn in the card format with the character's sprite (<see cref="SoulSprite"/>); this picture is still used by
        /// the Party screen's member cards and by enemy Ults.</summary>
        public static Sprite Ult(UnitDefinition owner)
        {
            if (owner == null) return null;
            Sprite s;
            if (!ults.TryGetValue(owner.name, out s))
                ults[owner.name] = s = Whole("Cards/Ult_" + owner.name);
            return s;
        }

        /// <summary>Old tarot Soul cards (retired 2026-10-27); kept so older callers compile. Always false now.</summary>
        public static bool IsTarot(Sprite art)
        {
            return art != null && art.name.StartsWith("Soul_");
        }

        /// <summary>The character's own pixel sprite for its Soul card: the first idle frame facing the viewer, or null
        /// if it has no animation set.</summary>
        public static Sprite SoulSprite(UnitDefinition owner)
        {
            if (owner == null || owner.animations == null || !owner.animations.Has("idle")) return null;
            Sprite s;
            if (souls.TryGetValue(owner.name, out s) && s != null) return s;
            var frames = owner.animations.Frames("idle", Facing.Down, false);
            if (frames == null || frames.Length == 0) return null;
            // Cropped to the figure (the frame has a lot of empty room around it), so the character fills the card.
            var r = frames[0].rect;
            var crop = new Rect(r.x + r.width * SoulCropX, r.y + r.height * 0.02f, r.width * (1f - SoulCropX * 2f), r.height * SoulCropTop);
            s = Sprite.Create(frames[0].texture, crop, new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
            s.name = "Soul_" + owner.name;
            souls[owner.name] = s;
            return s;
        }

        /// <summary>How much of an idle frame the figure takes: the middle half across, the bottom 62% up (feet to head).</summary>
        const float SoulCropX = 0.25f, SoulCropTop = 0.62f;
        static readonly Dictionary<string, Sprite> souls = new Dictionary<string, Sprite>();

        static readonly Dictionary<string, Sprite> pictures = new Dictionary<string, Sprite>();

        /// <summary>Any whole picture in Resources (point filtered, cached), e.g. "Cards/Arcana_Sun"; null if missing.</summary>
        public static Sprite Picture(string path)
        {
            Sprite s;
            if (!pictures.TryGetValue(path, out s)) pictures[path] = s = Whole(path);
            return s;
        }

        /// <summary>A picture with a 9-slice border (pixels on every side), cached.</summary>
        public static Sprite Bordered(string path, float border)
        {
            Sprite s;
            string key = path + "#" + border;
            if (pictures.TryGetValue(key, out s)) return s;
            var tex = Load(path);
            if (tex != null)
            {
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(border, border, border, border));
                s.name = tex.name;
            }
            pictures[key] = s;
            return s;
        }

        /// <summary>An Ult card's full art: its own (Ult_&lt;card asset&gt;, e.g. a resonance's Ult), else its owner's.</summary>
        public static Sprite Ult(CardDefinition card, UnitDefinition owner)
        {
            if (card != null)
            {
                Sprite s;
                string key = "card:" + card.name;
                if (!ults.TryGetValue(key, out s))
                    ults[key] = s = Whole("Cards/Ult_" + card.name);
                if (s != null) return s;
            }
            return Ult(owner);
        }

        static void LoadIcons()
        {
            icons = new Dictionary<string, Sprite>();
            var tex = Load("Cards/CardArt");
            var map = Resources.Load<TextAsset>("Cards/CardArt");
            if (tex == null || map == null) return;
            int perRow = tex.width / IconSize;
            foreach (var raw in map.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(' ');
                int cell;
                if (parts.Length < 2 || !int.TryParse(parts[0], out cell)) continue;
                int x = (cell % perRow) * IconSize, yTop = (cell / perRow) * IconSize;
                var rect = new Rect(x, tex.height - yTop - IconSize, IconSize, IconSize);
                icons[parts[1]] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        static Texture2D Load(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) tex.filterMode = FilterMode.Point;
            return tex;
        }

        static Sprite Whole(string path)
        {
            var tex = Load(path);
            if (tex == null) return null;
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = tex.name;   // IsTarot reads it
            return s;
        }

        /// <summary>Places <paramref name="rt"/> over a rect given in card pixels (top-left origin) on a card face whose
        /// card pixel is <paramref name="unit"/> canvas units.</summary>
        public static RectTransform PlacePixels(RectTransform rt, int x, int y, int w, int h, float unit)
        {
            var tl = new Vector2(0f, 1f);
            return rt.Place(tl, tl, new Vector2(x * unit, -y * unit), new Vector2(w * unit, h * unit));
        }
    }
}
