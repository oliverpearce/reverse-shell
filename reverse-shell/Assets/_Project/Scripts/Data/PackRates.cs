using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>A card's binding: a Major Arcana card fixed onto an ability card, which adds a small bonus whenever it's used.</summary>
    public enum CardBinding { None, Sun, Moon, Star }

    /// <summary>One kind of pack (the card sheet's Packs tab): its resonance, how many cards and wins, and its odds.</summary>
    [System.Serializable]
    public class PackKind
    {
        [Tooltip("Pack ID on the sheet; its picture is Resources/Cards/Packs/Pack_<id>.png.")]
        public string id = "Basic";
        public string displayName = "Basic Pack";
        [Tooltip("The resonance whose cards it gives (Basic = the Basic cards only).")]
        public Resonance pool = Resonance.Basic;
        [Min(1)] public int cardsPerPack = 3;
        [Min(0)] public int packsPerWin = 1;
        [Tooltip("Chance each common card is from the resonance; the rest are Basic cards (0.7 = 70%).")]
        [Range(0f, 1f)] public float poolShare = 0.7f;
        [Range(0f, 1f)] public float commonShare = 0.68f;
        [Range(0f, 1f)] public float rareShare = 0.25f;
        [Range(0f, 1f)] public float epicShare = 0.05f;
        [Tooltip("Chance each ability card is a Legendary (0.02 = 2%).")]
        [Range(0f, 1f)] public float legendaryShare = 0.02f;
        [Tooltip("Chance the last card is a Soul card.")]
        [Range(0f, 1f)] public float soulChance = 0.01f;
        [Tooltip("Chance the last card is Sun, Moon or Star.")]
        [Range(0f, 1f)] public float arcanaChance = 0.05f;
        [Tooltip("Chance the second card is a blank card.")]
        [Range(0f, 1f)] public float blankChance = 0.1f;
    }

    /// <summary>Which pack a won fight in an area gives (the card sheet's Areas tab).</summary>
    [System.Serializable]
    public class AreaPack
    {
        public string area;
        public string pack;
    }

    /// <summary>
    /// Every number of the card collection in one asset (Resources/Cards/PackRates.asset), so they can be tuned without
    /// code: what a pack holds and how often each tier drops, how many copies rank a card up, the binding and Soul
    /// caps, and what Sun / Moon / Star and Soul levels do. The generator makes the asset only if it's missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Pack Rates", fileName = "PackRates")]
    public class PackRates : ScriptableObject
    {
        [Header("Packs (only when no packs are set up below; each pack has its own odds)")]
        [Tooltip("Cards in one pack (never the same card twice in one pack). The last one is revealed slowest and is the only one that can be a rare or SOUL card.")]
        [Min(1)] public int cardsPerPack = 3;
        [Tooltip("Chance that a pack's last card is a SOUL card (0.1 = 10%). Only for units below the Soul level cap.")]
        [Range(0f, 1f)] public float soulChance = 0.01f;
        [Tooltip("Chance that a pack's last card is Sun, Moon or Star. Only kinds found fewer than the cap.")]
        [UnityEngine.Serialization.FormerlySerializedAs("rareChance")]
        [Range(0f, 1f)] public float arcanaChance = 0.05f;
        [Tooltip("Chance that a pack's second card is a blank card (0.1 = 10%), with its own reveal.")]
        [Range(0f, 1f)] public float blankChance = 0.1f;
        [Tooltip("Chance each ability card may be a Legendary (0.02 = 2%); otherwise Legendaries are left out.")]
        [Range(0f, 1f)] public float legendaryChance = 0.02f;
        [Tooltip("Packs given after every won fight.")]
        [Min(0)] public int packsPerWin = 1;

        [Header("Packs by chapter (filled from the card sheet by Reverse Shell > Import Cards From Sheet)")]
        public List<PackKind> packs = new List<PackKind>();
        public List<AreaPack> areaPacks = new List<AreaPack>();

        [Header("Copies (duplicates upgrade cards: +2 card levels each, CombatConfig.levelsPerUpgrade)")]
        [Tooltip("Copies each upgrade costs, in order: 5, then 10 more, then 20 more (35 in all for the top).")]
        public int[] upgradeCopies = { 5, 10, 20 };

        [Header("Bindings (Sun, Moon, Star)")]
        [Tooltip("Off: no Use / Bind, no binding text, and bindings do nothing in battle. Sun / Moon / Star are still collected.")]
        public bool bindingOn = true;
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
        [Tooltip("Alt cards a unit can bring besides its Main card (its leftmost card) and its Soul card.")]
        [Min(0)] public int extraDeckSlots = 2;
        [Tooltip("Off: every unit starts with just its locked card and its Soul card. On: its second card fills the extra slot to start.")]
        public bool startWithSecondCard;

        [Header("Soul cards")]
        [Tooltip("The highest Soul level a unit can reach.")]
        [Min(0)] public int maxSoulLevel = 3;
        [Tooltip("Extra Ult power per Soul level (0.1 = +10% Ult damage and healing).")]
        [Range(0f, 1f)] public float ultPowerPerSoul = 0.1f;

        [Header("Break & Stagger (the card sheet's Settings tab)")]
        [Tooltip("A card's Break damage at rank II: its base x this.")]
        [Min(0f)] public float breakRankII = 2f;
        [Tooltip("A card's Break damage at rank III: its base x this.")]
        [Min(0f)] public float breakRankIII = 3f;
        [Tooltip("Extra damage a Staggered enemy takes for the rest of the round (0.5 = +50%), DoT ticks included.")]
        [Min(0f)] public float staggerBonus = 0.5f;
        [Tooltip("Rounds a broken enemy stays Staggered, the one it broke in included (2 = it also loses its next move).")]
        [Min(1)] public int staggerTurns = 2;
        [Tooltip("Extra Break value a boss gets on top of its own.")]
        [Min(0)] public int bossBreakBonus = 3;

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

        /// <summary>The overworld area the current fight is in (set by the overworld, read by the results screen).</summary>
        public static string ActiveArea;

        /// <summary>The pack with this id, or null. "Basic" and its old name "Physical" both find the Basic pool's pack
        /// (old saves, and pack data not re-imported since the rename).</summary>
        public PackKind Pack(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var kind = packs.Find(p => p != null && p.id == id);
            if (kind == null && (id == "Basic" || id == "Physical")) kind = packs.Find(p => p != null && p.pool == Resonance.Basic);
            return kind;
        }

        /// <summary>The Areas tab's pack id meaning "a random pack each win" (the Sandbox).</summary>
        public const string RandomPack = "Random";

        /// <summary>The pack id a won fight in <paramref name="area"/> gives: the Areas tab's (it may be
        /// <see cref="RandomPack"/>), else the Basic pack's.</summary>
        public string PackIdForArea(string area)
        {
            var entry = areaPacks.Find(a => a != null && a.area == area);
            return entry != null && !string.IsNullOrEmpty(entry.pack) ? entry.pack : "Basic";
        }

        /// <summary>The pack a won fight in <paramref name="area"/> gives: the Areas tab's, else the Basic pack, else
        /// null (no packs set up: the old all-cards pack). A Random area rolls one of the packs.</summary>
        public PackKind PackForArea(string area)
        {
            string id = PackIdForArea(area);
            if (id == RandomPack) return RandomKind();
            return Pack(id) ?? Pack("Basic");
        }

        /// <summary>Any one of the packs, at random (null if there are none).</summary>
        public PackKind RandomKind()
        {
            return packs.Count > 0 ? packs[UnityEngine.Random.Range(0, packs.Count)] : null;
        }

        /// <summary>Packs a won fight in <paramref name="area"/> gives.</summary>
        public int PacksPerWin(string area)
        {
            var kind = PackIdForArea(area) == RandomPack ? null : PackForArea(area);
            return kind != null ? kind.packsPerWin : packsPerWin;
        }

        /// <summary>How many upgrades exist (3).</summary>
        public int MaxUpgrades { get { return upgradeCopies != null ? upgradeCopies.Length : 0; } }

        /// <summary>Copies needed in all for <paramref name="upgrade"/> upgrades (1: 5, 2: 15, 3: 35).</summary>
        public int CopiesFor(int upgrade)
        {
            int total = 0;
            for (int i = 0; i < Mathf.Min(upgrade, MaxUpgrades); i++) total += Mathf.Max(1, upgradeCopies[i]);
            return total;
        }

        /// <summary>The upgrades this many copies give (0 to <see cref="MaxUpgrades"/>).</summary>
        public int Upgrades(int copies)
        {
            int n = 0;
            while (n < MaxUpgrades && copies >= CopiesFor(n + 1)) n++;
            return n;
        }
    }
}
