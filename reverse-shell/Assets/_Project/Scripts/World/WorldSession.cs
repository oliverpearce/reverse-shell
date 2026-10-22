using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// State that survives the trip overworld -> battle -> overworld, and (through <see cref="SaveGame"/>)
    /// quitting the game. This is the seed of the GameSession from GAME_PLAN.md.
    /// </summary>
    public static class WorldSession
    {
        public static readonly HashSet<string> DefeatedEncounters = new HashSet<string>();

        /// <summary>Level, XP and HP per party member, keyed by the CharacterDefinition asset name.</summary>
        public static readonly Dictionary<string, MemberProgress> Party = new Dictionary<string, MemberProgress>();

        /// <summary>
        /// Party order by asset name; the first is the leader (the character you walk around as, and first in
        /// battle). Empty = the BattleSetup's own order.
        /// </summary>
        public static readonly List<string> PartyOrder = new List<string>();

        /// <summary>Spare Parts (not fitted to anyone): asset name to count.</summary>
        public static readonly Dictionary<string, int> Inventory = new Dictionary<string, int>();

        /// <summary>True once a new game has been handed the catalog's starter Parts (saved, so it happens once).</summary>
        public static bool StarterPartsGiven;

        /// <summary>Every Part in the game, for turning saved names back into assets. Set by the overworld.</summary>
        public static PartCatalog Catalog;

        public static bool HasReturnPoint;
        public static Vector3 ReturnPosition;
        public static int ReturnFacing;

        /// <summary>Shown in the message box when the overworld loads (e.g. a battle outcome).</summary>
        public static string PendingSpeaker;
        public static string PendingMessage;

        static bool loaded;

        /// <summary>Loads the save file once per play session (the first time the overworld starts).</summary>
        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            SaveGame.Load();
        }

        public static MemberProgress ProgressOf(CharacterDefinition member)
        {
            MemberProgress progress;
            if (!Party.TryGetValue(member.name, out progress))
            {
                progress = new MemberProgress();
                Party[member.name] = progress;
            }
            return progress;
        }

        public static int MaxHPOf(CharacterDefinition member)
        {
            return Leveling.MaxHP(member, ProgressOf(member).Level, PartsOf(member));
        }

        public static int HPOf(CharacterDefinition member)
        {
            var progress = ProgressOf(member);
            return progress.HP < 0 ? MaxHPOf(member) : Mathf.Min(progress.HP, MaxHPOf(member));
        }

        /// <summary>Copies of the given members' progress, in the form a BattleRequest takes.</summary>
        public static Dictionary<CharacterDefinition, MemberProgress> PartyFor(IEnumerable<CharacterDefinition> members)
        {
            var party = new Dictionary<CharacterDefinition, MemberProgress>();
            foreach (var member in members)
                if (member != null) party[member] = ProgressOf(member).Clone();
            return party;
        }

        // ------------------------------------------------------------------ Loadouts (deck + Parts)

        /// <summary>The cards in this member's deck: the saved choice, or the character's defaults.</summary>
        public static List<CardDefinition> DeckOf(CharacterDefinition member)
        {
            var deck = new List<CardDefinition>();
            var progress = ProgressOf(member);
            var all = member.AllIncantations();
            if (progress.Cards != null)
                foreach (var cardName in progress.Cards)
                {
                    var card = all.Find(c => c.name == cardName);
                    if (card != null && !deck.Contains(card)) deck.Add(card);
                }
            if (deck.Count == 0)
                foreach (var c in member.incantations) if (c != null) deck.Add(c);
            return deck;
        }

        /// <summary>Puts <paramref name="card"/> in deck slot <paramref name="index"/>. A card already in the deck swaps places.</summary>
        public static void SetDeckCard(CharacterDefinition member, int index, CardDefinition card)
        {
            var deck = DeckOf(member);
            if (card == null || index < 0 || index >= deck.Count) return;
            int existing = deck.IndexOf(card);
            if (existing >= 0) deck[existing] = deck[index];
            deck[index] = card;
            ProgressOf(member).Cards = deck.ConvertAll(c => c.name);
        }

        /// <summary>The Part fitted in a slot, or null.</summary>
        public static PartDefinition PartIn(CharacterDefinition member, PartSlot slot)
        {
            var parts = ProgressOf(member).Parts;
            int i = (int)slot;
            if (Catalog == null || parts == null || i >= parts.Count) return null;
            return Catalog.Find(parts[i]);
        }

        public static List<PartDefinition> PartsOf(CharacterDefinition member)
        {
            var list = new List<PartDefinition>();
            foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            {
                var part = PartIn(member, slot);
                if (part != null) list.Add(part);
            }
            return list;
        }

        public static int CountOf(PartDefinition part)
        {
            int n;
            return part != null && Inventory.TryGetValue(part.name, out n) ? n : 0;
        }

        public static void AddPart(PartDefinition part, int count = 1)
        {
            if (part == null || count <= 0) return;
            Inventory[part.name] = CountOf(part) + count;
        }

        static bool TakePart(PartDefinition part)
        {
            int n = CountOf(part);
            if (n <= 0) return false;
            if (n == 1) Inventory.Remove(part.name);
            else Inventory[part.name] = n - 1;
            return true;
        }

        /// <summary>
        /// Fits a spare Part into its slot; whatever was there goes back to the inventory. HP keeps the same
        /// missing amount, so fitting a Part never heals and removing one never knocks anyone out.
        /// </summary>
        public static bool Equip(CharacterDefinition member, PartDefinition part)
        {
            if (member == null || part == null || !TakePart(part)) return false;
            int hpBefore = HPOf(member), maxBefore = MaxHPOf(member);
            var old = PartIn(member, part.slot);
            if (old != null) AddPart(old);
            SetSlot(member, part.slot, part.name);
            KeepMissingHP(member, hpBefore, maxBefore);
            return true;
        }

        public static void Unequip(CharacterDefinition member, PartSlot slot)
        {
            var old = PartIn(member, slot);
            if (old == null) return;
            int hpBefore = HPOf(member), maxBefore = MaxHPOf(member);
            AddPart(old);
            SetSlot(member, slot, "");
            KeepMissingHP(member, hpBefore, maxBefore);
        }

        static void SetSlot(CharacterDefinition member, PartSlot slot, string partName)
        {
            var progress = ProgressOf(member);
            if (progress.Parts == null) progress.Parts = new List<string>();
            while (progress.Parts.Count <= (int)slot) progress.Parts.Add("");
            progress.Parts[(int)slot] = partName;
        }

        static void KeepMissingHP(CharacterDefinition member, int hpBefore, int maxBefore)
        {
            var progress = ProgressOf(member);
            if (progress.HP < 0) return; // full stays full
            int max = MaxHPOf(member);
            progress.HP = Mathf.Clamp(hpBefore + (max - maxBefore), 1, max);
            if (progress.HP >= max) progress.HP = -1;
        }

        /// <summary>A new game's starting Parts, handed out once.</summary>
        public static void GiveStarterParts()
        {
            if (StarterPartsGiven || Catalog == null) return;
            StarterPartsGiven = true;
            foreach (var part in Catalog.starterParts) AddPart(part);
        }

        /// <summary>Each member's deck and fitted Parts, in the form a BattleRequest takes.</summary>
        public static Dictionary<CharacterDefinition, Loadout> LoadoutsFor(IEnumerable<CharacterDefinition> members)
        {
            var loadouts = new Dictionary<CharacterDefinition, Loadout>();
            foreach (var member in members)
                if (member != null) loadouts[member] = new Loadout { Cards = DeckOf(member), Parts = PartsOf(member) };
            return loadouts;
        }

        /// <summary>Stores what a battle sent back (levels, XP, HP).</summary>
        public static void Apply(Dictionary<CharacterDefinition, MemberProgress> party)
        {
            foreach (var entry in party) Party[entry.Key.name] = entry.Value.Clone();
        }

        /// <summary>The members sorted by <see cref="PartyOrder"/> (members it doesn't list keep their relative order, after).</summary>
        public static List<CharacterDefinition> Ordered(IList<CharacterDefinition> members)
        {
            var list = new List<CharacterDefinition>();
            foreach (var m in members) if (m != null) list.Add(m);
            var original = new List<CharacterDefinition>(list);
            list.Sort((a, b) =>
            {
                int ia = PartyOrder.IndexOf(a.name), ib = PartyOrder.IndexOf(b.name);
                if (ia < 0) ia = PartyOrder.Count + original.IndexOf(a);
                if (ib < 0) ib = PartyOrder.Count + original.IndexOf(b);
                return ia.CompareTo(ib);
            });
            return list;
        }

        /// <summary>Makes <paramref name="leader"/> the party leader; everyone else keeps their order.</summary>
        public static void SetLeader(CharacterDefinition leader, IList<CharacterDefinition> members)
        {
            var ordered = Ordered(members);
            ordered.Remove(leader);
            ordered.Insert(0, leader);
            PartyOrder.Clear();
            foreach (var m in ordered) PartyOrder.Add(m.name);
        }

        /// <summary>Access points (and defeat) bring everyone back to full HP. Levels and XP are kept.</summary>
        public static void RestoreParty()
        {
            foreach (var progress in Party.Values) progress.HP = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DefeatedEncounters.Clear();
            Party.Clear();
            PartyOrder.Clear();
            Inventory.Clear();
            StarterPartsGiven = false;
            Catalog = null;
            HasReturnPoint = false;
            PendingSpeaker = null;
            PendingMessage = null;
            loaded = false;
        }
    }
}
