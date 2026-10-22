using System;
using UnityEngine;

namespace Combat
{
    public enum BattleOutcome { Victory, Defeat, Fled }

    /// <summary>A fight requested by another scene (e.g. the overworld), plus who to tell when it ends.</summary>
    public class BattleRequest
    {
        public BattleSetup Setup;
        /// <summary>Called when the player leaves the battle (result screen, or "Leave Fight" from pause).</summary>
        public Action<BattleOutcome> OnComplete;
    }

    /// <summary>
    /// Hand-off point between the world and the battle scene. The caller sets a request and loads the
    /// Battle scene; BattleBootstrap consumes it. Combat never needs to know about the overworld.
    /// </summary>
    public static class BattleLauncher
    {
        static BattleRequest pending;

        public static bool HasPending { get { return pending != null; } }

        public static void Request(BattleSetup setup, Action<BattleOutcome> onComplete)
        {
            pending = new BattleRequest { Setup = setup, OnComplete = onComplete };
        }

        public static BattleRequest Consume()
        {
            var request = pending;
            pending = null;
            return request;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pending = null;
        }
    }
}
