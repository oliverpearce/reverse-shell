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

        /// <summary>
        /// Who is in the party (asset names). A new game starts with just MC; entering the Sandbox recruits the
        /// rest (<see cref="RecruitAll"/>). Empty = everyone (saves from before recruiting existed).
        /// </summary>
        public static readonly HashSet<string> Members = new HashSet<string>();

        /// <summary>The first member of a new game.</summary>
        public const string StartingMember = "Char_MC";

        /// <summary>The roster filtered to who has joined, in the player's order.</summary>
        public static List<CharacterDefinition> Joined(IList<CharacterDefinition> roster)
        {
            var list = Ordered(roster);
            if (Members.Count > 0) list.RemoveAll(m => !Members.Contains(m.name));
            return list;
        }

        /// <summary>Adds everyone in <paramref name="roster"/> to the party (the Sandbox does this), filling
        /// empty formation slots with them. Returns the names of those who weren't in it yet.</summary>
        public static List<string> RecruitAll(IList<CharacterDefinition> roster)
        {
            var added = new List<string>();
            if (Members.Count == 0) return added;   // an old save: everyone is already in
            foreach (var m in roster)
                if (m != null && Members.Add(m.name))
                {
                    added.Add(m.DisplayName);
                    AddToFormation(m.name);
                }
            return added;
        }

        // ------------------------------------------------------------------ Formation (3 active + 1 backup)

        public const int ActiveSlots = 3, FormationSlots = 4, BackupSlot = 3;

        /// <summary>
        /// Who fights: slots 0-2 are the active members (the first filled one leads and is who you walk as), slot 3 is
        /// the backup. "" = empty. Saved; edited in the Party screen and on the pre-fight screen.
        /// </summary>
        public static readonly string[] Formation = { "", "", "", "" };

        /// <summary>Drops anyone not in the party, removes duplicates, and fills an empty formation from the joined
        /// members in order. There is always at least one active member while anyone has joined, and a backup only
        /// while all three active slots are filled (otherwise the backup steps into the first empty one).</summary>
        public static void EnsureFormation(IList<CharacterDefinition> roster)
        {
            var joined = Joined(roster);
            var names = joined.ConvertAll(m => m.name);
            var seen = new HashSet<string>();
            for (int i = 0; i < FormationSlots; i++)
            {
                string n = Formation[i] ?? "";
                if (n.Length == 0 || !names.Contains(n) || !seen.Add(n)) Formation[i] = "";
            }
            if (seen.Count == 0)
                for (int i = 0; i < joined.Count && i < FormationSlots; i++) Formation[i] = joined[i].name;
            if (!string.IsNullOrEmpty(Formation[BackupSlot]))
                for (int i = 0; i < ActiveSlots; i++)
                    if (string.IsNullOrEmpty(Formation[i]))
                    {
                        // A gap in the party: the backup fills it.
                        Formation[i] = Formation[BackupSlot];
                        Formation[BackupSlot] = "";
                        break;
                    }
            if (FirstActive() < 0 && joined.Count > 0) Formation[0] = joined[0].name;
        }

        /// <summary>The backup slot is open only when all three active slots are filled.</summary>
        public static bool BackupAllowed
        {
            get
            {
                for (int i = 0; i < ActiveSlots; i++) if (string.IsNullOrEmpty(Formation[i])) return false;
                return true;
            }
        }

        static int FirstActive()
        {
            for (int i = 0; i < ActiveSlots; i++) if (!string.IsNullOrEmpty(Formation[i])) return i;
            return -1;
        }

        /// <summary>Puts a newcomer in the first empty slot (active first, then the backup slot).</summary>
        static void AddToFormation(string name)
        {
            if (System.Array.IndexOf(Formation, name) >= 0) return;
            for (int i = 0; i < FormationSlots; i++)
                if (string.IsNullOrEmpty(Formation[i])) { Formation[i] = name; return; }
        }

        /// <summary>The active members in slot order (the first leads).</summary>
        public static List<CharacterDefinition> FormationActive(IList<CharacterDefinition> roster)
        {
            EnsureFormation(roster);
            var list = new List<CharacterDefinition>();
            for (int i = 0; i < ActiveSlots; i++)
            {
                var m = Find(roster, Formation[i]);
                if (m != null) list.Add(m);
            }
            return list;
        }

        public static CharacterDefinition FormationBackup(IList<CharacterDefinition> roster)
        {
            EnsureFormation(roster);
            return Find(roster, Formation[BackupSlot]);
        }

        public static CharacterDefinition Find(IList<CharacterDefinition> roster, string name)
        {
            if (string.IsNullOrEmpty(name) || roster == null) return null;
            foreach (var m in roster) if (m != null && m.name == name) return m;
            return null;
        }

        /// <summary>Puts <paramref name="member"/> in <paramref name="slot"/>; if they were in another slot, the two swap.</summary>
        public static void Assign(int slot, CharacterDefinition member, IList<CharacterDefinition> roster)
        {
            if (member == null || slot < 0 || slot >= FormationSlots) return;
            var next = (string[])Formation.Clone();
            int from = System.Array.IndexOf(next, member.name);
            if (from >= 0) next[from] = next[slot];
            next[slot] = member.name;
            // Refused: a backup while an active slot would be empty.
            if (!string.IsNullOrEmpty(next[BackupSlot]))
                for (int i = 0; i < ActiveSlots; i++) if (string.IsNullOrEmpty(next[i])) return;
            for (int i = 0; i < FormationSlots; i++) Formation[i] = next[i];
            EnsureFormation(roster);
        }

        /// <summary>Can <paramref name="slot"/> be emptied? Not if it's empty, or holds the only fighter.</summary>
        public static bool CanClear(int slot)
        {
            if (slot < 0 || slot >= FormationSlots || string.IsNullOrEmpty(Formation[slot])) return false;
            if (slot >= ActiveSlots) return true;
            int active = 0;
            for (int i = 0; i < ActiveSlots; i++) if (!string.IsNullOrEmpty(Formation[i])) active++;
            return active > 1;
        }

        /// <summary>Empties a slot. Refused (false) when it would leave no active member.</summary>
        public static bool ClearSlot(int slot, IList<CharacterDefinition> roster)
        {
            if (!CanClear(slot)) return false;
            Formation[slot] = "";
            EnsureFormation(roster);
            return true;
        }

        /// <summary>Spare Parts (not fitted to anyone): asset name to count.</summary>
        public static readonly Dictionary<string, int> Inventory = new Dictionary<string, int>();

        /// <summary>True once a new game has been handed the catalog's starter Parts (saved, so it happens once).</summary>
        public static bool StarterPartsGiven;

        /// <summary>Every Part in the game, for turning saved names back into assets. Set by the overworld.</summary>
        public static PartCatalog Catalog;

        /// <summary>Which overworld area (<see cref="WorldAreas"/>) the scene builds. Saved; a new game starts in the city.</summary>
        public static string CurrentArea = WorldAreas.City;

        /// <summary>The Sandbox chest's settings: practice-fight enemy level (1-99) and boss mode. Saved.</summary>
        public static int SandboxEnemyLevel = Combat.BattleUnit.DefaultEnemyLevel;
        public static bool SandboxBoss;

        /// <summary>A place in the world: an area and a spot in it, with the way the player faces. Saved.</summary>
        [System.Serializable]
        public class Spot
        {
            public string area;
            public Vector3 position;
            public int facing;

            public Spot Clone() { return new Spot { area = area, position = position, facing = facing }; }
        }

        /// <summary>Where the player is now (kept up to date by the overworld; null while travelling). Saved, so
        /// Continue puts you back exactly there.</summary>
        public static Spot PlayerSpot;

        /// <summary>The last Access Point used (where you stood to rest): a lost fight wakes you here. Saved.</summary>
        public static Spot LastAccessPoint;

        /// <summary>Puts the next area load at <paramref name="spot"/> (its area, position and facing).</summary>
        public static void ReturnTo(Spot spot)
        {
            CurrentArea = spot.area;
            ArrivedFrom = null;
            HasReturnPoint = true;
            ReturnPosition = spot.position;
            ReturnFacing = spot.facing;
            PlayerSpot = spot.Clone();
        }

        /// <summary>Resting at an Access Point: every beaten patrol machine comes back (story fights stay gone, their
        /// flags keep them away). Returns how many were beaten.</summary>
        public static int RespawnPatrols()
        {
            int n = DefeatedEncounters.Count;
            DefeatedEncounters.Clear();
            return n;
        }

        /// <summary>In the Sandbox (entered from Settings → Enter Sandbox Mode).</summary>
        public static bool InSandbox { get { return CurrentArea == WorldAreas.Sandbox; } }

        /// <summary>Where Leave Sandbox Mode puts you back: the area, spot and facing you entered from. Saved.</summary>
        public static bool HasSandboxReturn;
        public static string SandboxReturnArea = WorldAreas.City;
        public static Vector3 SandboxReturnPosition;
        public static int SandboxReturnFacing;

        /// <summary>The area you just warped from (null otherwise): you arrive beside the warp that leads back there.</summary>
        public static string ArrivedFrom;

        public static bool HasReturnPoint;
        public static Vector3 ReturnPosition;
        public static int ReturnFacing;

        /// <summary>Shown in the message box when the overworld loads (e.g. a battle outcome).</summary>
        public static string PendingSpeaker;
        public static string PendingMessage;

        static bool loaded;

        /// <summary>True once the player has picked New Game / Continue / Load on the title screen this session.</summary>
        public static bool TitleDone;

        /// <summary>Loads the save file once per play session (the first time the overworld starts).</summary>
        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            SaveGame.Load();
        }

#if UNITY_EDITOR
        /// <summary>The Level Designer's Play This Area writes an area id here (editor session only) before Play.</summary>
        public const string PlayAreaKey = "ReverseShell.PlayArea";

        /// <summary>Play This Area: skips the title, loads the latest save (or a new game) and starts at that area's spawn.
        /// Saving while testing (Access Points) saves into that slot as usual.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PlayAreaFromDesigner()
        {
            string id = UnityEditor.SessionState.GetString(PlayAreaKey, "");
            if (id.Length == 0) return;
            UnityEditor.SessionState.EraseString(PlayAreaKey);
            int latest = SaveGame.LatestSlot();
            StartSlot(latest > 0 ? latest : 1, latest <= 0);
            CurrentArea = id;
            PlayerSpot = null;
            HasReturnPoint = false;
            ArrivedFrom = null;
        }
#endif

        /// <summary>The title screen's choice: play <paramref name="slot"/>, from its save or (newGame) from scratch
        /// (its old save is deleted). The caller reloads the area afterwards.</summary>
        public static void StartSlot(int slot, bool newGame)
        {
            SaveGame.Slot = slot;
            SaveGame.Restore(null);       // clears the session
            HasReturnPoint = false;
            ArrivedFrom = null;
            PendingSpeaker = null;
            PendingMessage = null;
            if (newGame)
            {
                SaveGame.Delete();
                Members.Add(StartingMember);   // a new game is just MC
                Formation[0] = StartingMember;
            }
            else SaveGame.Load();
            loaded = true;
            TitleDone = true;
        }

        /// <summary>
        /// The title screen's Sandbox Mode: plays the latest save (or a new game in slot 1 when
        /// there is none) but starts in the Sandbox. Leave Sandbox Mode then goes to where that save left off
        /// (a new game: the city's start, where the opening plays). The caller reloads the area afterwards.
        /// </summary>
        public static void StartSandbox()
        {
            int latest = SaveGame.LatestSlot();
            StartSlot(latest > 0 ? latest : 1, latest <= 0);
            if (InSandbox) return;   // the save was already in the Sandbox
            var spot = PlayerSpot;
            HasSandboxReturn = spot != null && spot.area == CurrentArea;
            SandboxReturnArea = CurrentArea;
            if (HasSandboxReturn)
            {
                SandboxReturnPosition = spot.position;
                SandboxReturnFacing = spot.facing;
            }
            CurrentArea = WorldAreas.Sandbox;
            // The Sandbox places you at its start: the save's spot (Load's ReturnTo) is in the other area.
            PlayerSpot = null;
            HasReturnPoint = false;
            ArrivedFrom = null;
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
        // ------------------------------------------------------------------ Resonance (the protagonist)

        /// <summary>Resonances unlocked by absorbing Soul Cards (the starting one is always available). Saved.</summary>
        public static readonly HashSet<Afflatus> UnlockedResonances = new HashSet<Afflatus>();

        /// <summary>The protagonist's active resonance (free to switch outside combat). Saved.</summary>
        public static Afflatus ActiveResonance = Afflatus.None;

        public static void UnlockResonance(Afflatus element) { UnlockedResonances.Add(element); }

        /// <summary>The Esc menu's Resonance card: after Chapter One, and always in Sandbox Mode.</summary>
        public static bool ResonanceMenuUnlocked { get { return InSandbox || StoryState.Has(Chapter1.Complete); } }

        /// <summary>
        /// The resonances <paramref name="member"/> can switch to now, in kit order (empty = no kits). Sandbox Mode
        /// lends every kit, so they can be tried before the story unlocks them.
        /// </summary>
        public static List<Afflatus> AvailableResonances(CharacterDefinition member)
        {
            var list = new List<Afflatus>();
            if (member == null) return list;
            for (int i = 0; i < member.resonances.Count; i++)
            {
                var kit = member.resonances[i];
                if (kit != null && (i == 0 || InSandbox || UnlockedResonances.Contains(kit.element))) list.Add(kit.element);
            }
            return list;
        }

        /// <summary>A member's colour (cards, the party HUD): their active resonance's, else their own.</summary>
        public static Color ColorOf(CharacterDefinition member)
        {
            if (member == null) return Color.white;
            var kit = ActiveKit(member);
            return AfflatusInfo.ResonanceColor(member.color, kit != null ? kit.element : (Afflatus?)null);
        }

        /// <summary>The kit in use: the active resonance if unlocked, else the first. Null for members without kits.</summary>
        public static ResonanceKit ActiveKit(CharacterDefinition member)
        {
            if (member == null || member.resonances.Count == 0) return null;
            if (AvailableResonances(member).Contains(ActiveResonance)) return member.KitFor(ActiveResonance) ?? member.resonances[0];
            return member.resonances[0];
        }

        public static void SetResonance(Afflatus element) { ActiveResonance = element; }

        /// <summary>The Ult card a member brings: their active resonance's, else their ritual.</summary>
        public static CardDefinition UltimateOf(CharacterDefinition member)
        {
            var kit = ActiveKit(member);
            return kit != null && kit.ultimate != null ? kit.ultimate : (member != null ? member.ritual : null);
        }

        /// <summary>A member's element: their active resonance's, else their definition's.</summary>
        public static Afflatus ElementOf(CharacterDefinition member)
        {
            var kit = ActiveKit(member);
            return kit != null ? kit.element : (member != null ? member.afflatus : Afflatus.None);
        }

        public static List<CardDefinition> DeckOf(CharacterDefinition member)
        {
            var deck = new List<CardDefinition>();
            // A resonance kit sets the innate cards outright.
            var activeKit = ActiveKit(member);
            if (activeKit != null)
            {
                foreach (var c in activeKit.cards) if (c != null) deck.Add(c);
                if (deck.Count > 0) return deck;
            }
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
            if (member == null || part == null || IsBound(PartIn(member, part.slot)) || !TakePart(part)) return false;
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
            if (old == null || IsBound(old)) return;
            int hpBefore = HPOf(member), maxBefore = MaxHPOf(member);
            AddPart(old);
            SetSlot(member, slot, "");
            KeepMissingHP(member, hpBefore, maxBefore);
        }

        /// <summary>The protagonist's own Core: the Soul Card binds to it, so it never comes out or gets swapped.</summary>
        public static bool IsBound(PartDefinition part)
        {
            return part != null && Catalog != null && (part == Catalog.starterCore || part == Catalog.boundCore);
        }

        /// <summary>The Soul Card binds with the protagonist's Core: the Fragmented Core becomes the catalog's bound Core
        /// (the Resonant Core). Safe to call again; older saves that already have the card get it on load.</summary>
        public static void BindSoulCard()
        {
            if (Catalog == null || Catalog.boundCore == null) return;
            MemberProgress progress;
            if (!Party.TryGetValue(StartingMember, out progress)) return;
            if (progress.Parts == null) progress.Parts = new List<string>();
            int slot = (int)Catalog.boundCore.slot;
            while (progress.Parts.Count <= slot) progress.Parts.Add("");
            string fitted = progress.Parts[slot];
            if (string.IsNullOrEmpty(fitted) || (Catalog.starterCore != null && fitted == Catalog.starterCore.name))
                progress.Parts[slot] = Catalog.boundCore.name;
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

        /// <summary>A new game's starting Parts, handed out once: the catalog's spare Parts (none in Chapter One) and the
        /// protagonist's own Fragmented Core, fitted in their Core slot.</summary>
        public static void GiveStarterParts()
        {
            if (StarterPartsGiven || Catalog == null) return;
            StarterPartsGiven = true;
            foreach (var part in Catalog.starterParts) AddPart(part);
            var core = Catalog.starterCore;
            if (core == null) return;
            MemberProgress progress;
            if (!Party.TryGetValue(StartingMember, out progress))
            {
                progress = new MemberProgress();
                Party[StartingMember] = progress;
            }
            if (progress.Parts == null) progress.Parts = new List<string>();
            int slot = (int)core.slot;
            while (progress.Parts.Count <= slot) progress.Parts.Add("");
            if (string.IsNullOrEmpty(progress.Parts[slot])) progress.Parts[slot] = core.name;
        }

        /// <summary>Each member's deck and fitted Parts, in the form a BattleRequest takes.</summary>
        public static Dictionary<CharacterDefinition, Loadout> LoadoutsFor(IEnumerable<CharacterDefinition> members)
        {
            var loadouts = new Dictionary<CharacterDefinition, Loadout>();
            foreach (var member in members)
                if (member != null)
                {
                    var kit = ActiveKit(member);
                    loadouts[member] = new Loadout
                    {
                        Cards = DeckOf(member),
                        Parts = PartsOf(member),
                        Element = kit != null ? kit.element : (Afflatus?)null,
                        Ultimate = kit != null ? kit.ultimate : null,
                    };
                }
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
            // The leader takes the first formation slot (swapping with whoever was there).
            EnsureFormation(members);
            int at = System.Array.IndexOf(Formation, leader.name);
            if (at < 0) Formation[0] = leader.name;
            else if (at != 0)
            {
                Formation[at] = Formation[0];
                Formation[0] = leader.name;
            }
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
            PlayerSpot = null;
            LastAccessPoint = null;
            PendingSpeaker = null;
            PendingMessage = null;
            loaded = false;
            TitleDone = false;
        }
    }
}
