using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum BattleOutcome { Victory, Defeat, Fled }

    /// <summary>A fight requested by another scene (e.g. the overworld), plus who to tell when it ends.</summary>
    public class BattleRequest
    {
        public BattleSetup Setup;
        /// <summary>Level, XP and HP of each party member. Members not listed start fresh (level 1, full HP).</summary>
        public Dictionary<CharacterDefinition, MemberProgress> Party;
        /// <summary>Each member's deck and fitted Parts. Members not listed use their default cards and no Parts.</summary>
        public Dictionary<CharacterDefinition, Loadout> Loadouts;
        /// <summary>Called when the player leaves the battle (result screen, or "Leave Fight" from pause).</summary>
        public Action<BattleResult> OnComplete;
        /// <summary>A guided tutorial to run in this fight ("erin"), or null. The battle scene raises
        /// <see cref="BattleBootstrap.TutorialStarted"/> with it.</summary>
        public string Tutorial;
    }

    /// <summary>What the caller gets back when a requested fight ends.</summary>
    public class BattleResult
    {
        public BattleOutcome Outcome;
        /// <summary>XP each member earned (only on victory).</summary>
        public int XpGained;
        /// <summary>Every party member's progress after the fight, with XP already applied.</summary>
        public Dictionary<CharacterDefinition, MemberProgress> Party = new Dictionary<CharacterDefinition, MemberProgress>();
        /// <summary>Levels gained this fight, for members who leveled up.</summary>
        public Dictionary<CharacterDefinition, int> LevelsGained = new Dictionary<CharacterDefinition, int>();
        /// <summary>Each member's level and XP before this fight's XP was added (the results screen animates from it).</summary>
        public Dictionary<CharacterDefinition, MemberProgress> Before = new Dictionary<CharacterDefinition, MemberProgress>();

        /// <summary>Builds the result: current HP from the battle, plus the XP reward on a win. Knocked-out members earn it
        /// too; a backup who never stepped in does not (and isn't in <see cref="Before"/>, so the results screen skips them).</summary>
        public static BattleResult Build(BattleOutcome outcome, BattleController controller, IDictionary<CharacterDefinition, MemberProgress> start)
        {
            var result = new BattleResult { Outcome = outcome, Party = controller.PartySnapshot(start) };
            if (outcome != BattleOutcome.Victory) return result;

            result.XpGained = controller.XpReward();
            foreach (var entry in result.Party)
            {
                if (controller.State.Backup.Exists(u => u.Definition == entry.Key)) continue;
                result.Before[entry.Key] = entry.Value.Clone();
                int levels = Leveling.AddXp(entry.Key, entry.Value, result.XpGained);
                if (levels > 0) result.LevelsGained[entry.Key] = levels;
            }
            return result;
        }
    }

    /// <summary>
    /// Hand-off point between the world and the battle scene. The caller sets a request and loads the
    /// Battle scene; BattleBootstrap consumes it. Combat never needs to know about the overworld.
    /// </summary>
    public static class BattleLauncher
    {
        static BattleRequest pending;

        public static bool HasPending { get { return pending != null; } }

        public static void Request(BattleSetup setup, Dictionary<CharacterDefinition, MemberProgress> party, Action<BattleResult> onComplete)
        {
            Request(setup, party, null, onComplete);
        }

        public static void Request(BattleSetup setup, Dictionary<CharacterDefinition, MemberProgress> party,
            Dictionary<CharacterDefinition, Loadout> loadouts, Action<BattleResult> onComplete)
        {
            pending = new BattleRequest { Setup = setup, Party = party, Loadouts = loadouts, OnComplete = onComplete };
        }

        /// <summary>Marks the request just made as a guided tutorial (call right after <see cref="Request"/>).</summary>
        public static void SetTutorial(string tutorial)
        {
            if (pending != null) pending.Tutorial = tutorial;
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
