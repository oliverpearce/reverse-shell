using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// State that survives the trip overworld -> battle -> overworld. This is the seed of the
    /// GameSession from GAME_PLAN.md (party HP, inventory and save data will live here later).
    /// </summary>
    public static class WorldSession
    {
        public static readonly HashSet<string> DefeatedEncounters = new HashSet<string>();

        public static bool HasReturnPoint;
        public static Vector3 ReturnPosition;
        public static int ReturnFacing;

        /// <summary>Shown in the message box when the overworld loads (e.g. a battle outcome).</summary>
        public static string PendingSpeaker;
        public static string PendingMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DefeatedEncounters.Clear();
            HasReturnPoint = false;
            PendingSpeaker = null;
            PendingMessage = null;
        }
    }
}
