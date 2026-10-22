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
            return Leveling.MaxHP(member, ProgressOf(member).Level);
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

        /// <summary>Stores what a battle sent back (levels, XP, HP).</summary>
        public static void Apply(Dictionary<CharacterDefinition, MemberProgress> party)
        {
            foreach (var entry in party) Party[entry.Key.name] = entry.Value.Clone();
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
            HasReturnPoint = false;
            PendingSpeaker = null;
            PendingMessage = null;
            loaded = false;
        }
    }
}
