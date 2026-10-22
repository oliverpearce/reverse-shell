using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// The card-type icon atlas, Resources/UI/CardIcons.png (painted by Tools/IconForge/card_icons.py; keep the layout
    /// in sync). Six types (Attack, Debuff, Buff, Counter, Heal, Ultimate) as white glyphs to tint, colored glyphs and
    /// small intent cards in three ranks (cream I, silver II, gold III), plus UI bits: the empty unit-type diamond,
    /// Ult pips, a star, a filled diamond and a card back. Sprites are cut from the atlas once and cached.
    /// </summary>
    public static class CardIcons
    {
        public enum Type { Attack = 0, Debuff = 1, Buff = 2, Counter = 3, Heal = 4, Ultimate = 5 }

        const int Cell = 128, Cols = 6, Rows = 6;
        const int RowWhite = 0, RowColored = 1, RowIntent = 2, RowMisc = 5;

        static Texture2D atlas;
        static Sprite[,] sprites;
        static bool missingWarned;

        /// <summary>The icon type for a card: Rituals are Ultimate, the rest follow their kind.</summary>
        public static Type TypeOf(CardDefinition card)
        {
            if (card == null) return Type.Attack;
            if (card.isRitual) return Type.Ultimate;
            switch (card.kind)
            {
                case CardKind.Debuff: return Type.Debuff;
                case CardKind.Buff: return Type.Buff;
                case CardKind.Heal: return Type.Heal;
                case CardKind.Counter: return Type.Counter;
                default: return Type.Attack;
            }
        }

        /// <summary>The type's symbol: in its own color, or white to tint.</summary>
        public static Sprite Glyph(Type type, bool colored = true)
        {
            return Get(colored ? RowColored : RowWhite, (int)type);
        }

        /// <summary>A small intent card: the type's symbol on a cream (rank I), silver (II) or gold (III) card.</summary>
        public static Sprite IntentCard(Type type, int rank)
        {
            return Get(RowIntent + Mathf.Clamp(rank, 1, 3) - 1, (int)type);
        }

        public static Sprite IntentCard(CardInstance card)
        {
            return IntentCard(TypeOf(card.Definition), card.Rank);
        }

        /// <summary>The empty diamond frame next to a unit's bars (reserved for unit-type icons).</summary>
        public static Sprite EmptyDiamond { get { return Get(RowMisc, 0); } }
        public static Sprite PipEmpty { get { return Get(RowMisc, 1); } }
        public static Sprite PipFull { get { return Get(RowMisc, 2); } }
        public static Sprite Star { get { return Get(RowMisc, 3); } }
        public static Sprite Diamond { get { return Get(RowMisc, 4); } }
        public static Sprite CardBack { get { return Get(RowMisc, 5); } }

        static Sprite Get(int row, int col)
        {
            if (sprites == null)
            {
                atlas = Resources.Load<Texture2D>("UI/CardIcons");
                if (atlas == null)
                {
                    if (!missingWarned) Debug.LogWarning("Resources/UI/CardIcons.png is missing: run python Tools/IconForge/card_icons.py");
                    missingWarned = true;
                    return null;
                }
                sprites = new Sprite[Rows, Cols];
            }
            var sprite = sprites[row, col];
            if (sprite != null) return sprite;
            // Texture rows count from the bottom; the atlas's row 0 is its top row.
            var rect = new Rect(col * Cell, atlas.height - (row + 1) * Cell, Cell, Cell);
            sprite = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "CardIcon_" + row + "_" + col;
            sprites[row, col] = sprite;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            atlas = null;
            sprites = null;
            missingWarned = false;
        }
    }
}
