using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Story progress: a set of named flags (see <see cref="Chapter1"/> for Chapter One's) and the protagonist's chosen
    /// name. Saved with the game (<see cref="SaveGame"/>). Flags only ever get set, never unset, so progress can't go
    /// backwards; the F8 story debug panel can jump to any beat.
    /// </summary>
    public static class StoryState
    {
        public static readonly HashSet<string> Flags = new HashSet<string>();

        /// <summary>The name the player gave the protagonist (empty until the opening asks).</summary>
        public static string PlayerName = "";

        /// <summary>Raised after a flag is set (the overworld refreshes its objective and props).</summary>
        public static event Action<string> Changed;

        public static bool Has(string flag) { return !string.IsNullOrEmpty(flag) && Flags.Contains(flag); }

        public static void Set(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !Flags.Add(flag)) return;
            Debug.Log("[Story] " + flag);
            if (Changed != null) Changed(flag);
        }

        /// <summary>Every flag in <paramref name="required"/> is set and none in <paramref name="blocked"/>.</summary>
        public static bool Allows(string[] required, string[] blocked)
        {
            if (required != null) foreach (var f in required) if (!Has(f)) return false;
            if (blocked != null) foreach (var f in blocked) if (Has(f)) return false;
            return true;
        }

        /// <summary>The Break weaknesses the player finds in battle are saved as flags too ("weak.Enemy_X.Physical").</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookWeaknessBook()
        {
            Combat.WeaknessBook.Lookup = Has;
            Combat.WeaknessBook.Store = Set;
        }

        public static void Clear()
        {
            Flags.Clear();
            Combat.WeaknessBook.Clear();
            SetName("");
        }

        /// <summary>The protagonist's asset; their chosen name replaces its display name everywhere.</summary>
        public const string ProtagonistAsset = "Char_A3";

        /// <summary>Sets the protagonist's name (shown in place of "A3" in every menu, HUD and battle).</summary>
        public static void SetName(string name)
        {
            PlayerName = name ?? "";
            if (PlayerName.Length > 0) Combat.UnitDefinition.NameOverrides[ProtagonistAsset] = PlayerName;
            else Combat.UnitDefinition.NameOverrides.Remove(ProtagonistAsset);
        }
    }
}
