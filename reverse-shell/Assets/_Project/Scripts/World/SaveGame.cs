using System;
using System.Collections.Generic;
using System.IO;
using Combat;
using UnityEngine;
using Combat.View;

namespace Game.World
{
    /// <summary>
    /// Saves WorldSession (party level, XP, HP and loadout, party order / leader, spare Parts and defeated encounters) as JSON in a
    /// slot file, Application.persistentDataPath/save_N.json (picked on the title screen). The overworld saves after every battle and at Access Points,
    /// and loads once when the game starts. In the editor, Tools > Overworld > Delete Save Data starts fresh.
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 2;

        /// <summary>Save slots (chosen on the title screen): save_1.json .. save_3.json.</summary>
        public const int SlotCount = 3;
        public static int Slot = 1;

        /// <summary>Sandbox Mode's own save (save_sandbox.json): the whole crew, kept apart from the story slots, which
        /// only ever hold who has joined in the story. Never listed with the story slots.</summary>
        public const int SandboxSlot = 0;
        public static bool InSandboxSlot { get { return Slot == SandboxSlot; } }

        public static string FilePath { get { return SlotPath(Slot); } }

        /// <summary>Where the slot files live: persistentDataPath, or another folder (the screenshot run, ShotTour, keeps
        /// its saves apart so it never touches the player's).</summary>
        public static string FolderOverride;

        static string Folder { get { return string.IsNullOrEmpty(FolderOverride) ? Application.persistentDataPath : FolderOverride; } }

        public static string SlotPath(int slot)
        {
            MigrateLegacyFile();
            return Path.Combine(Folder, slot == SandboxSlot ? "save_sandbox.json" : "save_" + slot + ".json");
        }

        /// <summary>The single save.json from before slots becomes slot 1 (once).</summary>
        static void MigrateLegacyFile()
        {
            string legacy = Path.Combine(Folder, "save.json");
            string first = Path.Combine(Folder, "save_1.json");
            try
            {
                if (File.Exists(legacy) && !File.Exists(first)) File.Move(legacy, first);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't move the old save to slot 1: " + e.Message);
            }
        }

        public static bool Exists(int slot)
        {
            return File.Exists(SlotPath(slot));
        }

        /// <summary>What the title screen shows for a slot: the leader, their level and when it was saved. Null if empty.</summary>
        public class Summary
        {
            public string Leader;
            /// <summary>The protagonist's chosen name ("" if none yet).</summary>
            public string PlayerName;
            /// <summary>The area the save is in, as shown ("The City Ruins").</summary>
            public string AreaName;
            public int Level;
            public DateTime Saved;
        }

        public static Summary Peek(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
                var summary = new Summary { Saved = File.GetLastWriteTime(path), Leader = "", Level = 1, PlayerName = data.playerName ?? "",
                    AreaName = AreaTitle(data.area) };
                string leader = data.partyOrder != null && data.partyOrder.Count > 0 ? Migrate(data.partyOrder[0]) : null;
                foreach (var m in data.party)
                    if (m.progress != null && (leader == null || Migrate(m.id) == leader))
                    {
                        summary.Leader = Migrate(m.id);
                        summary.Level = m.progress.Level;
                        break;
                    }
                return summary;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>"THE CITY RUINS" as "The City Ruins".</summary>
        static string AreaTitle(string id)
        {
            var name = WorldAreas.Get(string.IsNullOrEmpty(id) ? WorldAreas.City : id).Name ?? "";
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
        }

        /// <summary>
        /// A save slot's parts for the title screen's slot rows: the leader's name (the player's name for the
        /// protagonist), level, area and save time ("23 Oct, 14:05"). False for an empty slot.
        /// </summary>
        public static bool SlotParts(int slot, out string who, out int level, out string area, out string when)
        {
            var summary = Peek(slot);
            who = area = when = "";
            level = 0;
            if (summary == null) return false;
            who = summary.Leader ?? "";
            if (who == WorldSession.StartingMember && !string.IsNullOrEmpty(summary.PlayerName)) who = summary.PlayerName;
            else if (who.StartsWith("Char_")) who = who.Substring(5);
            level = summary.Level;
            area = summary.AreaName;
            when = summary.Saved.ToString("d MMM, HH:mm");
            return true;
        }

        /// <summary>
        /// A save slot as a tall row on the Save page: the heading ("Slot 2 (current)"), the detail lines (the leader's
        /// name big with the level in gold, then the area) and the save time for the corner. An empty slot's detail
        /// just says so.
        /// </summary>
        public static void SlotCard(int slot, bool markCurrent, out string heading, out string detail, out string corner)
        {
            var summary = Peek(slot);
            heading = UIText.Get("save.slot", "slot", slot.ToString()) + (markCurrent && slot == Slot ? "  <color=#C2A061>" + UIText.Get("save.current") + "</color>" : "");
            if (summary == null)
            {
                detail = "<color=#8A857A>" + UIText.Get("save.empty") + "</color>";
                corner = "";
                return;
            }
            string who = summary.Leader;
            if (who == WorldSession.StartingMember && !string.IsNullOrEmpty(summary.PlayerName)) who = summary.PlayerName;
            else if (who != null && who.StartsWith("Char_")) who = who.Substring(5);
            detail = Combat.View.UIFactory.Size(48) + who + "</size>    <color=#C2A061>" + UIText.Get("common.lv") + " " + summary.Level + "</color>\n"
                     + "<color=#B8B1A3>" + summary.AreaName + "</color>";
            corner = summary.Saved.ToString("d MMM, HH:mm");
        }

        /// <summary>The title's Cards book: puts <paramref name="slot"/>'s save into the session so the book shows its
        /// copies, card levels and Soul levels. Safe before a game starts: starting one (StartSlot) clears it first.</summary>
        public static void Preview(int slot)
        {
            string path = SlotPath(slot);
            if (slot <= 0 || !File.Exists(path)) return;
            try
            {
                Restore(JsonUtility.FromJson<Data>(File.ReadAllText(path)));
            }
            catch (Exception)
            {
                Restore(null);
            }
        }

        /// <summary>The most recently written slot, or 0 when there are none.</summary>
        public static int LatestSlot()
        {
            int best = 0;
            DateTime newest = DateTime.MinValue;
            for (int i = 1; i <= SlotCount; i++)
            {
                string path = SlotPath(i);
                if (!File.Exists(path)) continue;
                var t = File.GetLastWriteTime(path);
                if (t > newest)
                {
                    newest = t;
                    best = i;
                }
            }
            return best;
        }

        [Serializable]
        public class Data
        {
            public int version = Version;
            public List<Member> party = new List<Member>();
            public List<string> defeatedEncounters = new List<string>();
            /// <summary>Party order by asset name; the first is the leader.</summary>
            public List<string> partyOrder = new List<string>();
            /// <summary>Spare Parts (version 2).</summary>
            public List<PartStack> inventory = new List<PartStack>();
            public bool starterPartsGiven;
            /// <summary>The card collection (empty in older saves): extra copies by card, bindings (id = card, count unused;
            /// kind in <see cref="BindingStack"/>), Sun / Moon / Star found, unopened packs.</summary>
            public List<PartStack> cardCopies = new List<PartStack>();
            public List<BindingStack> bindings = new List<BindingStack>();
            public List<PartStack> bindingsFound = new List<PartStack>();
            public int packs;
            /// <summary>Each unopened pack's id (older saves only have the count).</summary>
            public List<string> packKinds = new List<string>();
            /// <summary>Cards pulled but not looked at yet (their NEW tags); empty in older saves.</summary>
            public List<string> unseenCards = new List<string>();
            /// <summary>Items used ("area:x:row"): walkable from then on.</summary>
            public List<string> usedItems = new List<string>();
            /// <summary>The overworld area you were in (WorldAreas id); empty in older saves = the city.</summary>
            public string area;
            /// <summary>Who has joined the party (asset names); empty in older saves = everyone.</summary>
            public List<string> members = new List<string>();
            /// <summary>The formation: 3 active slots then the backup ("" = empty). Empty in older saves = filled in order.</summary>
            public List<string> formation = new List<string>();
            /// <summary>Story progress (StoryState): flags set so far, and the protagonist's chosen name.</summary>
            public List<string> flags = new List<string>();
            /// <summary>Unlocked resonances and the active one, by element name ("" = Basic).</summary>
            public List<string> resonances = new List<string>();
            public string activeResonance = "";
            public string playerName = "";
            /// <summary>Bonds (Bonds.cs): rank and points per character; empty in older saves.</summary>
            public List<BondStack> bonds = new List<BondStack>();
            /// <summary>The Sandbox chest (0 in older saves = the default level).</summary>
            public int sandboxLevel;
            public bool sandboxBoss;
            /// <summary>Where Leave Sandbox Mode returns to (set when you enter the Sandbox from Settings).</summary>
            public bool hasSandboxReturn;
            public string sandboxReturnArea;
            public Vector3 sandboxReturnPosition;
            public int sandboxReturnFacing;
            /// <summary>Where you were when this was saved, and the last Access Point you rested at (empty area = none).</summary>
            public WorldSession.Spot position;
            public WorldSession.Spot accessPoint;
        }

        [Serializable]
        public class PartStack
        {
            public string id;
            public int count;
        }

        [Serializable]
        public class BindingStack
        {
            public string card;
            public string kind;
        }

        /// <summary>Characters that were renamed: old save keys (asset names) to the new ones.</summary>
        static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
        {
            { "Char_2B", "Char_A3" },
            { "Char_MC", "Char_A3" },
            { "Char_V1", "Char_Ashe" },
            { "Char_HealBot", "Char_Ashe" },
            { "Char_V2", "Char_Ashe" },
            { "Char_Lucy", "Char_Ashe" },   // Lucy and Blue were removed (2026-11-03)
            { "Char_Blue", "Char_Ashe" },
        };

        public static string Migrate(string id)
        {
            string renamed;
            return id != null && Renamed.TryGetValue(id, out renamed) ? renamed : id;
        }

        /// <summary>Card asset names from older saves: Heal Bot's cards became Ashe's (Card_HealBot_X is Card_Ashe_X) and
        /// MC's became A3's (Card_MC_X is Card_A3_X).</summary>
        public static string MigrateCard(string id)
        {
            if (id == null) return null;
            foreach (var r in RenamedCardPrefixes)
                if (id.StartsWith(r.Key)) return r.Value + id.Substring(r.Key.Length);
            return id;
        }

        static readonly KeyValuePair<string, string>[] RenamedCardPrefixes =
        {
            new KeyValuePair<string, string>("Card_HealBot_", "Card_Ashe_"),
            new KeyValuePair<string, string>("Card_MC_", "Card_A3_"),
        };

        static void MigrateCards(List<string> ids)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Count; i++) ids[i] = MigrateCard(ids[i]);
        }

        /// <summary>The Training Center was renamed the Sandbox: older saves name its old area id.</summary>
        static void MigrateAreas(Data data)
        {
            data.area = WorldAreas.Migrate(data.area);
            data.sandboxReturnArea = WorldAreas.Migrate(data.sandboxReturnArea);
            if (data.position != null) data.position.area = WorldAreas.Migrate(data.position.area);
            if (data.accessPoint != null) data.accessPoint.area = WorldAreas.Migrate(data.accessPoint.area);
        }

        [Serializable]
        public class Member
        {
            public string id;
            public MemberProgress progress;
        }

        [Serializable]
        public class BondStack
        {
            public string id;
            public int rank;
            public int points;
        }

        public static Data Capture()
        {
            var data = new Data();
            foreach (var entry in WorldSession.Party)
                data.party.Add(new Member { id = entry.Key, progress = entry.Value.Clone() });
            data.defeatedEncounters.AddRange(WorldSession.DefeatedEncounters);
            data.partyOrder.AddRange(WorldSession.PartyOrder);
            foreach (var entry in WorldSession.Inventory)
                if (entry.Value > 0) data.inventory.Add(new PartStack { id = entry.Key, count = entry.Value });
            data.starterPartsGiven = WorldSession.StarterPartsGiven;
            foreach (var entry in WorldSession.CardCopies)
                if (entry.Value > 0) data.cardCopies.Add(new PartStack { id = entry.Key, count = entry.Value });
            foreach (var entry in WorldSession.Bindings) data.bindings.Add(new BindingStack { card = entry.Key, kind = entry.Value });
            foreach (var entry in WorldSession.BindingsFound)
                if (entry.Value > 0) data.bindingsFound.Add(new PartStack { id = entry.Key, count = entry.Value });
            data.packs = WorldSession.Packs;
            data.packKinds.AddRange(WorldSession.PackQueue);
            data.unseenCards.AddRange(WorldSession.UnseenCards);
            data.usedItems.AddRange(WorldSession.UsedItems);
            data.area = WorldSession.CurrentArea;
            data.members.AddRange(WorldSession.Members);
            data.formation.AddRange(WorldSession.Formation);
            data.flags.AddRange(StoryState.Flags);
            foreach (var r in WorldSession.UnlockedResonances) data.resonances.Add(r.ToString());
            data.activeResonance = WorldSession.ActiveResonance.ToString();
            data.playerName = StoryState.PlayerName ?? "";
            foreach (var entry in WorldSession.Bonds)
                if (entry.Value != null) data.bonds.Add(new BondStack { id = entry.Key, rank = entry.Value.Rank, points = entry.Value.Points });
            data.sandboxLevel = WorldSession.SandboxEnemyLevel;
            data.sandboxBoss = WorldSession.SandboxBoss;
            data.hasSandboxReturn = WorldSession.HasSandboxReturn;
            data.sandboxReturnArea = WorldSession.SandboxReturnArea ?? "";
            data.sandboxReturnPosition = WorldSession.SandboxReturnPosition;
            data.sandboxReturnFacing = WorldSession.SandboxReturnFacing;
            data.position = WorldSession.PlayerSpot != null ? WorldSession.PlayerSpot.Clone() : null;
            data.accessPoint = WorldSession.LastAccessPoint != null ? WorldSession.LastAccessPoint.Clone() : null;
            return data;
        }

        public static void Restore(Data data)
        {
            WorldSession.Party.Clear();
            WorldSession.DefeatedEncounters.Clear();
            WorldSession.PartyOrder.Clear();
            WorldSession.Inventory.Clear();
            WorldSession.CardCopies.Clear();
            WorldSession.Bindings.Clear();
            WorldSession.BindingsFound.Clear();
            WorldSession.PackQueue.Clear();
            WorldSession.UnseenCards.Clear();
            WorldSession.StarterPartsGiven = false;
            WorldSession.UsedItems.Clear();   // picked-up chips and items come back on a new game
            WorldSession.Members.Clear();
            WorldSession.Bonds.Clear();
            StoryState.Clear();
            WorldSession.UnlockedResonances.Clear();
            WorldSession.ActiveResonance = Combat.Resonance.Basic;
            if (data != null) MigrateAreas(data);
            if (data != null && data.resonances != null)
                foreach (var r in data.resonances)
                {
                    Combat.Resonance element;
                    if (Combat.ResonanceInfo.TryParse(r, out element)) WorldSession.UnlockedResonances.Add(element);
                }
            Combat.Resonance active;
            if (data != null && !string.IsNullOrEmpty(data.activeResonance) && Combat.ResonanceInfo.TryParse(data.activeResonance, out active))
                WorldSession.ActiveResonance = active;
            if (data != null && data.flags != null) foreach (var f in data.flags) StoryState.Flags.Add(f);
            if (data != null && !string.IsNullOrEmpty(data.playerName)) StoryState.SetName(data.playerName);
            for (int i = 0; i < WorldSession.FormationSlots; i++)
                WorldSession.Formation[i] = data != null && data.formation != null && i < data.formation.Count ? Migrate(data.formation[i] ?? "") : "";
            WorldSession.SandboxEnemyLevel = data != null && data.sandboxLevel > 0 ? data.sandboxLevel : Combat.BattleUnit.DefaultEnemyLevel;
            WorldSession.SandboxBoss = data != null && data.sandboxBoss;
            WorldSession.HasSandboxReturn = data != null && data.hasSandboxReturn;
            WorldSession.SandboxReturnArea = data != null && !string.IsNullOrEmpty(data.sandboxReturnArea) ? data.sandboxReturnArea : WorldAreas.StartArea;
            WorldSession.SandboxReturnPosition = data != null ? data.sandboxReturnPosition : Vector3.zero;
            WorldSession.SandboxReturnFacing = data != null ? data.sandboxReturnFacing : 0;
            WorldSession.CurrentArea = data != null && !string.IsNullOrEmpty(data.area) ? data.area : WorldAreas.StartArea;
            WorldSession.LastAccessPoint = data != null && data.accessPoint != null && !string.IsNullOrEmpty(data.accessPoint.area)
                ? data.accessPoint.Clone() : null;
            // Continue puts you where you saved (only if it is in the saved area: a save made mid-travel has none).
            WorldSession.PlayerSpot = null;
            if (data != null && data.position != null && data.position.area == WorldSession.CurrentArea)
                WorldSession.ReturnTo(data.position);
            if (data == null) return;
            if (data.members != null)
                foreach (var id in data.members) WorldSession.Members.Add(Migrate(id));
            if (data.partyOrder != null)
                foreach (var id in data.partyOrder)
                {
                    var to = Migrate(id);
                    if (!WorldSession.PartyOrder.Contains(to)) WorldSession.PartyOrder.Add(to);   // two old ids can become one
                }
            foreach (var member in data.party)
                if (!string.IsNullOrEmpty(member.id) && member.progress != null)
                {
                    var progress = member.progress.Clone();
                    MigrateCards(progress.Cards);
                    MigrateCards(progress.ExtraCards);
                    // Two old characters can merge into one (Lucy and Blue became Ashe): keep the further-along progress.
                    string to = Migrate(member.id);
                    MemberProgress had;
                    if (!WorldSession.Party.TryGetValue(to, out had) || had == null || progress.Level > had.Level)
                        WorldSession.Party[to] = progress;
                }
            foreach (var id in data.defeatedEncounters) WorldSession.DefeatedEncounters.Add(id);
            if (data.bonds != null)
                foreach (var b in data.bonds)
                    if (!string.IsNullOrEmpty(b.id))
                        WorldSession.Bonds[Migrate(b.id)] = new WorldSession.BondProgress { Rank = b.rank, Points = b.points };
            if (data.inventory != null)
                foreach (var stack in data.inventory)
                    if (!string.IsNullOrEmpty(stack.id) && stack.count > 0) WorldSession.Inventory[stack.id] = stack.count;
            WorldSession.StarterPartsGiven = data.starterPartsGiven; // older saves get the starter Parts on load
            if (data.cardCopies != null)
                foreach (var stack in data.cardCopies)
                    if (!string.IsNullOrEmpty(stack.id) && stack.count > 0) WorldSession.CardCopies[MigrateCard(stack.id)] = stack.count;
            if (data.bindings != null)
                foreach (var b in data.bindings)
                    if (!string.IsNullOrEmpty(b.card) && !string.IsNullOrEmpty(b.kind)) WorldSession.Bindings[MigrateCard(b.card)] = b.kind;
            if (data.bindingsFound != null)
                foreach (var stack in data.bindingsFound)
                    if (!string.IsNullOrEmpty(stack.id) && stack.count > 0) WorldSession.BindingsFound[stack.id] = stack.count;
            WorldSession.PackQueue.Clear();
            if (data.packKinds != null && data.packKinds.Count > 0) WorldSession.PackQueue.AddRange(data.packKinds);
            else WorldSession.AddPacks("", Mathf.Max(0, data.packs));
            if (data.unseenCards != null)
                foreach (var key in data.unseenCards) if (!string.IsNullOrEmpty(key)) WorldSession.UnseenCards.Add(MigrateCard(key));
            if (data.usedItems != null) foreach (var key in data.usedItems) WorldSession.UsedItems.Add(key);
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Capture(), true));
                WebSync.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't write the save file: " + e.Message);
            }
        }

        /// <summary>Loads the save into WorldSession. Returns false (and starts fresh) when there is no readable save.</summary>
        public static bool Load()
        {
            if (!File.Exists(FilePath)) return false;
            try
            {
                Restore(JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't read the save file, starting fresh: " + e.Message);
                Restore(null);
                return false;
            }
        }

        /// <summary>Deletes the current slot's save.</summary>
        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            WebSync.Flush();
        }

        /// <summary>Deletes every slot and the Sandbox's save (Tools > Overworld > Delete Save Data).</summary>
        public static void DeleteAll()
        {
            for (int i = SandboxSlot; i <= SlotCount; i++)
                if (File.Exists(SlotPath(i))) File.Delete(SlotPath(i));
            WebSync.Flush();
        }
    }
}
