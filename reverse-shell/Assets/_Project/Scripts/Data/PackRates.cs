using UnityEngine;

namespace Combat
{
    /// <summary>A card's binding: a Major Arcana card fixed onto an ability card, which adds a small bonus whenever it's used.</summary>
    public enum CardBinding { None, Sun, Moon, Star }

    /// <summary>
    /// Every number of the card collection in one asset (Resources/Cards/PackRates.asset), so they can be tuned without
    /// code: what a pack holds and how often each tier drops, how many copies rank a card up, the binding and Soul
    /// caps, and what Sun / Moon / Star and Soul levels do. The generator makes the asset only if it's missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Pack Rates", fileName = "PackRates")]
    public class PackRates : ScriptableObject
    {
        [Header("Packs")]
        [Tooltip("Cards in one pack (never the same card twice in one pack). The last one is revealed slowest and is the only one that can be a rare or SOUL card.")]
        [Min(1)] public int cardsPerPack = 3;
        [Tooltip("Chance that a pack's last card is a SOUL card (0.1 = 10%). Only for units below the Soul level cap.")]
        [Range(0f, 1f)] public float soulChance = 0.1f;
        [Tooltip("Chance that a pack's last card is a rare (Sun, Moon or Star). Only kinds found fewer than the cap.")]
        [Range(0f, 1f)] public float rareChance = 0.4f;
        [Tooltip("Packs given after every won fight.")]
        [Min(0)] public int packsPerWin = 1;

        [Header("Copies (duplicates rank cards up)")]
        [Tooltip("Copies of a card needed for it to start battles at rank II.")]
        [Min(1)] public int copiesForRankII = 3;
        [Tooltip("Copies of a card needed for it to start battles at rank III.")]
        [Min(1)] public int copiesForRankIII = 6;

        [Header("Bindings (Sun, Moon, Star)")]
        [Tooltip("Off for now (2026-10-28): no Bind button, no binding text, and bindings do nothing in battle. Sun / Moon / Star are still collected.")]
        public bool bindingOn;
        [Tooltip("The most of each binding kind you can ever own.")]
        [Min(0)] public int maxPerBinding = 2;
        [Tooltip("Sun: the card's user heals this share of their max HP (0.05 = 5%).")]
        [Range(0f, 1f)] public float sunHeal = 0.05f;
        [Tooltip("Moon: the status given to the card's user (DEF Up).")]
        public StatusDefinition moonStatus;
        [Tooltip("Moon: how many turns it lasts.")]
        [Min(1)] public int moonTurns = 1;
        [Tooltip("Star: Ult points the card's user gains.")]
        [Min(0)] public int starUlt = 1;

        [Header("Decks")]
        [Tooltip("Cards a unit can bring besides its locked card (its leftmost card) and its Soul card.")]
        [Min(0)] public int extraDeckSlots = 1;
        [Tooltip("Off: every unit starts with just its locked card and its Soul card. On: its second card fills the extra slot to start.")]
        public bool startWithSecondCard;

        [Header("Soul cards")]
        [Tooltip("The highest Soul level a unit can reach.")]
        [Min(0)] public int maxSoulLevel = 3;
        [Tooltip("Extra Ult power per Soul level (0.1 = +10% Ult damage and healing).")]
        [Range(0f, 1f)] public float ultPowerPerSoul = 0.1f;

        static PackRates current;

        /// <summary>The project's rates (Resources/Cards/PackRates), or the defaults above if the asset is missing.</summary>
        public static PackRates Current
        {
            get
            {
                if (current == null) current = Resources.Load<PackRates>("Cards/PackRates");
                if (current == null) current = CreateInstance<PackRates>();
                return current;
            }
        }

        /// <summary>The rank a card starts battles at with this many copies (1-3).</summary>
        public int StartRank(int copies)
        {
            if (copies >= copiesForRankIII) return 3;
            return copies >= copiesForRankII ? 2 : 1;
        }
    }
}
