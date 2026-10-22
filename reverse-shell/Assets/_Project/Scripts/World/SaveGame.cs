using System;
using System.Collections.Generic;
using System.IO;
using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Saves WorldSession (party level, XP, HP and loadout, party order / leader, spare Parts and defeated encounters) as JSON in
    /// Application.persistentDataPath/save.json. The overworld saves after every battle and at Access Points,
    /// and loads once when the game starts. In the editor, Tools > Overworld > Delete Save Data starts fresh.
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 2;

        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, "save.json"); } }

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
        }

        [Serializable]
        public class PartStack
        {
            public string id;
            public int count;
        }

        /// <summary>Characters that were renamed: old save keys (asset names) to the new ones.</summary>
        static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
        {
            { "Char_2B", "Char_MC" },
            { "Char_V1", "Char_HealBot" },
            { "Char_V2", "Char_Lucy" },
        };

        public static string Migrate(string id)
        {
            string renamed;
            return id != null && Renamed.TryGetValue(id, out renamed) ? renamed : id;
        }

        [Serializable]
        public class Member
        {
            public string id;
            public MemberProgress progress;
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
            return data;
        }

        public static void Restore(Data data)
        {
            WorldSession.Party.Clear();
            WorldSession.DefeatedEncounters.Clear();
            WorldSession.PartyOrder.Clear();
            WorldSession.Inventory.Clear();
            WorldSession.StarterPartsGiven = false;
            if (data == null) return;
            if (data.partyOrder != null)
                foreach (var id in data.partyOrder) WorldSession.PartyOrder.Add(Migrate(id));
            foreach (var member in data.party)
                if (!string.IsNullOrEmpty(member.id) && member.progress != null) WorldSession.Party[Migrate(member.id)] = member.progress.Clone();
            foreach (var id in data.defeatedEncounters) WorldSession.DefeatedEncounters.Add(id);
            if (data.inventory != null)
                foreach (var stack in data.inventory)
                    if (!string.IsNullOrEmpty(stack.id) && stack.count > 0) WorldSession.Inventory[stack.id] = stack.count;
            WorldSession.StarterPartsGiven = data.starterPartsGiven; // older saves get the starter Parts on load
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Capture(), true));
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

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
