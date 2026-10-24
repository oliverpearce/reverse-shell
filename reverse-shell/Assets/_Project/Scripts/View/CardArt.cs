using System.Collections.Generic;
using UnityEngine;

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
        public const int PipX = 6, PipStep = 6, PipY = 5, PipSize = 5;
        public const int ArtX = 8, ArtY = 15, ArtSize = 32;
        public static readonly RectInt NamePlate = new RectInt(7, 52, 34, 7);
        public static readonly RectInt TypeIcon = new RectInt(34, 3, 10, 10);

        static Sprite swirl, frame, pipOn, pipOff;
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

        /// <summary>The owner's full-art Ult card (by character asset name), or null.</summary>
        public static Sprite Ult(UnitDefinition owner)
        {
            if (owner == null) return null;
            Sprite s;
            if (!ults.TryGetValue(owner.name, out s))
                ults[owner.name] = s = Whole("Cards/Ult_" + owner.name);
            return s;
        }

        static void LoadIcons()
        {
            icons = new Dictionary<string, Sprite>();
            var tex = Load("Cards/CardArt");
            var map = Resources.Load<TextAsset>("Cards/CardArt");
            if (tex == null || map == null) return;
            int perRow = tex.width / ArtSize;
            foreach (var raw in map.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(' ');
                int cell;
                if (parts.Length < 2 || !int.TryParse(parts[0], out cell)) continue;
                int x = (cell % perRow) * ArtSize, yTop = (cell / perRow) * ArtSize;
                var rect = new Rect(x, tex.height - yTop - ArtSize, ArtSize, ArtSize);
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
            return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
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
