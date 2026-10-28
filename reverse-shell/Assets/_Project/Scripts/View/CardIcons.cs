using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// The icon atlas, Resources/UI/CardIcons.png (built by Tools/IconForge/card_icons.py, mostly from the user's picks
    /// in Clockwork Raven's "Raven Fantasy Icons"; keep the layout in sync). Seven card types (Attack, Debuff, Buff,
    /// Counter, Heal, Ultimate, Defense) as white masks, in their own colors and as small intent cards in three ranks (cream I,
    /// silver II, gold III); UI bits (the empty unit-type diamond, Ult pips, a star, a filled diamond, a card back);
    /// element orbs; Part / status icons; and the button icons (Go, close, pause). Sprites are cut from the atlas once
    /// and cached.
    /// </summary>
    public static class CardIcons
    {
        public enum Type { Attack = 0, Debuff = 1, Buff = 2, Counter = 3, Heal = 4, Ultimate = 5, Defense = 6 }

        /// <summary>The atlas column for a type.</summary>
        static int Slot(Type type) { return (int)type; }

        const int Cell = 128, Cols = 8, Rows = 10;
        const int RowWhite = 0, RowColored = 1, RowIntent = 2, RowMisc = 5, RowElement = 6, RowExtra = 7, RowMore = 8, RowStatus = 9;

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
                case CardKind.Defense: return Type.Defense;
                default: return Type.Attack;
            }
        }

        /// <summary>The type's symbol: in its own color, or white to tint. Buff and Debuff in color are the chunky pixel
        /// block arrows (CardArt.Type, the user's pick), the same everywhere they show.</summary>
        public static Sprite Glyph(Type type, bool colored = true)
        {
            if (colored && (type == Type.Buff || type == Type.Debuff))
            {
                var arrow = CardArt.Type(type);
                if (arrow != null) return arrow;
            }
            return Get(colored ? RowColored : RowWhite, Slot(type));
        }

        /// <summary>A small intent card: the type's symbol on a cream (rank I), silver (II) or gold (III) card.</summary>
        public static Sprite IntentCard(Type type, int rank)
        {
            return Get(RowIntent + Mathf.Clamp(rank, 1, 3) - 1, Slot(type));
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

        /// <summary>Button icons (Raven picks): the Go chevrons, the close X and the pause sign; and the ring on a queue
        /// slot used up by moving a card (tinted gray there).</summary>
        public static Sprite GoChevron { get { return Get(RowMore, 2); } }
        public static Sprite Close { get { return Get(RowMore, 3); } }
        public static Sprite Pause { get { return Get(RowMore, 4); } }
        public static Sprite Swap { get { return Get(RowMore, 5); } }

        /// <summary>The resonance's orb, shown beside a unit's bars. Picked for the five resonances (ELEMENT_ICONS in Tools/IconForge/card_icons.py).</summary>
        public static Sprite Element(Resonance resonance)
        {
            if (resonance == Resonance.Basic) return null;   // Basic has no orb
            return Get(RowElement, (int)resonance);
        }

        /// <summary>The icon for a Part slot: a chip or a core.</summary>
        public static Sprite Part(PartSlot slot)
        {
            return Get(RowExtra, slot == PartSlot.Core ? 1 : 0);
        }

        /// <summary>A status's icon: its own sprite if set, else the atlas icon picked for it (by asset name), else null.</summary>
        public static Sprite Status(StatusDefinition status)
        {
            if (status == null) return null;
            if (status.icon != null) return status.icon;
            switch (status.name)
            {
                case "Status_Poison": return Get(RowExtra, 2);
                case "Status_AtkUp": return Get(RowExtra, 3);
                case "Status_AtkDown": return Get(RowExtra, 4);
                case "Status_PhysicalDefDown": return Get(RowExtra, 5);
                case "Status_Regen": return Get(RowExtra, 6);
                case "Status_IncantationMightUp": return Get(RowExtra, 7);
                case "Status_DefUp": return Get(RowMore, 0);
                case "Status_Stunned": return Get(RowMore, 1);
                case "Status_Burn": return Get(RowStatus, 0);
                case "Status_Chill": return Get(RowStatus, 1);
                case "Status_Frozen": return Get(RowStatus, 2);
                case "Status_Shield": return Get(RowStatus, 3);
                case "Status_Counter": return Get(RowStatus, 4);
                case "Status_Covered": return Get(RowStatus, 5);
                default: return null;
            }
        }

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
