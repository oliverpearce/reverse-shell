using System;
using System.Collections.Generic;
using System.IO;
using Combat;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Saves WorldSession (party level, XP and HP, and defeated encounters) as JSON in
    /// Application.persistentDataPath/save.json. The overworld saves after every battle and at Access Points,
    /// and loads once when the game starts. In the editor, Tools > Overworld > Delete Save Data starts fresh.
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 1;

        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, "save.json"); } }

        [Serializable]
        public class Data
        {
            public int version = Version;
            public List<Member> party = new List<Member>();
            public List<string> defeatedEncounters = new List<string>();
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
            return data;
        }

        public static void Restore(Data data)
        {
            WorldSession.Party.Clear();
            WorldSession.DefeatedEncounters.Clear();
            if (data == null) return;
            foreach (var member in data.party)
                if (!string.IsNullOrEmpty(member.id) && member.progress != null) WorldSession.Party[member.id] = member.progress.Clone();
            foreach (var id in data.defeatedEncounters) WorldSession.DefeatedEncounters.Add(id);
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
