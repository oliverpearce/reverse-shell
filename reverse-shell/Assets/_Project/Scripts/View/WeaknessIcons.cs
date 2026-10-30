using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// An enemy's Break weaknesses as a row of icons (the inspect screen and the pre-fight enemy list), drawn with the
    /// cards' own pixel icons: Physical is the cards' sword type icon, Incantation their star, and each resonance the
    /// gem from the card backs in that resonance's colour, each over its name. A weakness the player hasn't found yet
    /// (<see cref="WeaknessBook"/>) shows as "?", so the count is known but not what they are.
    /// </summary>
    public static class WeaknessIcons
    {
        static readonly Color Unknown = new Color(0.72f, 0.7f, 0.66f);
        /// <summary>Where the gem sits on the 48 x 64 card back (CardBackGem.png), in pixels from the bottom left.</summary>
        static readonly Rect GemPixels = new Rect(20f, 28f, 8f, 8f);
        static Sprite gem;

        /// <summary>The card backs' resonance gem on its own (untinted; tint it with <see cref="Tint"/>).</summary>
        static Sprite Gem
        {
            get
            {
                if (gem != null) return gem;
                var back = CardArt.Picture("Cards/CardBackGem");
                if (back == null) return null;
                gem = Sprite.Create(back.texture, GemPixels, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                gem.name = "WeaknessGem";
                return gem;
            }
        }

        public static Sprite Icon(Weakness w)
        {
            switch (w)
            {
                case Weakness.Physical: return CardArt.Type(CardIcons.Type.Attack) ?? CardIcons.Glyph(CardIcons.Type.Attack);
                case Weakness.Incantation: return CardArt.Type(CardIcons.Type.Ultimate) ?? CardIcons.Glyph(CardIcons.Type.Ultimate);
                case Weakness.None: return null;
            }
            return Gem;
        }

        /// <summary>The type icons keep their own colours; a gem takes its resonance's.</summary>
        public static Color Tint(Weakness w)
        {
            switch (w)
            {
                case Weakness.Arc: return ResonanceInfo.Color(Resonance.Arc);
                case Weakness.Cinder: return ResonanceInfo.Color(Resonance.Cinder);
                case Weakness.Root: return ResonanceInfo.Color(Resonance.Root);
                case Weakness.Cryo: return ResonanceInfo.Color(Resonance.Cryo);
                case Weakness.Prism: return ResonanceInfo.Color(Resonance.Prism);
            }
            return Color.white;
        }

        /// <summary>Builds the row left-aligned at <paramref name="position"/> (top-left anchored in
        /// <paramref name="parent"/>); returns its width. <paramref name="labels"/> adds each one's name underneath.</summary>
        public static float Row(RectTransform parent, EnemyDefinition enemy, Vector2 position, float size, bool labels)
        {
            if (enemy == null) return 0f;
            var known = WeaknessBook.Known(enemy);
            float x = 0f, step = labels ? size + 70f : size + 8f;
            var tl = new Vector2(0f, 1f);
            foreach (var w in BreakRules.All)
            {
                if ((enemy.weaknesses & w) == 0) continue;
                bool found = (known & w) != 0;
                var cell = UIFactory.CreateRect("Weak_" + w, parent).Place(tl, tl, position + new Vector2(x, 0f), new Vector2(step, size + (labels ? 34f : 0f)));
                if (found)
                {
                    var img = UIFactory.CreateImage(cell, "Icon", Tint(w));
                    img.sprite = Icon(w);
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    img.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size));
                }
                else
                {
                    var q = UIFactory.CreateText(cell, "Unknown", "?", Mathf.RoundToInt(size * 0.8f), Unknown, TextAnchor.MiddleCenter);
                    q.raycastTarget = false;
                    q.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size));
                }
                if (labels)
                {
                    var name = UIFactory.CreateText(cell, "Name", found ? BreakRules.Label(w) : "???", UIFactory.TextSize.Small, found ? Color.white : Unknown, TextAnchor.UpperCenter);
                    name.raycastTarget = false;
                    name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -size - 2f), new Vector2(step, 30f));
                }
                x += step;
            }
            return x;
        }
    }
}
