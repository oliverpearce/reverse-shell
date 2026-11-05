using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Who looks like what in dialogue: speaker name → their animation set, so every line from someone with a sprite
    /// shows their animated face beside the text (DialogueBox; CardArt.FaceFrames). The overworld registers the crew
    /// (and the protagonist under the player's chosen name and "Thought"), the Bestiary's machines and each NPC as it
    /// spawns. Narration and the Scanner / Core / System voices have no face. Static, so the battle's tutorial lines
    /// (Erin) find her too.
    /// </summary>
    public static class DialogueFaces
    {
        static readonly Dictionary<string, UnitDefinition> units = new Dictionary<string, UnitDefinition>();
        static readonly Dictionary<string, SpriteAnimationSet> sets = new Dictionary<string, SpriteAnimationSet>();
        static readonly HashSet<string> faceless = new HashSet<string> { "Scanner", "Core", "System" };

        /// <summary>A character or machine: its current look (resonance tint, Machine Look) is used when the line shows.</summary>
        public static void Register(string name, UnitDefinition unit)
        {
            if (string.IsNullOrEmpty(name) || unit == null) return;
            units[name] = unit;
            sets.Remove(name);
        }

        /// <summary>Someone with only a sprite (an NPC).</summary>
        public static void Register(string name, SpriteAnimationSet set)
        {
            if (string.IsNullOrEmpty(name) || set == null || units.ContainsKey(name)) return;
            sets[name] = set;
        }

        /// <summary>The speaker's face frames and fps, or null (no speaker, a machine voice, or nobody by that name).</summary>
        public static Sprite[] For(string speaker, out float fps)
        {
            fps = 6f;
            if (string.IsNullOrEmpty(speaker) || faceless.Contains(speaker)) return null;
            UnitDefinition unit;
            SpriteAnimationSet set = null;
            if (units.TryGetValue(speaker, out unit) && unit != null) set = unit.Anim;
            else if (sets.TryGetValue(speaker, out set) && set != null) set = set.Current;
            return set != null ? CardArt.FaceFrames(set, out fps) : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            units.Clear();
            sets.Clear();
        }
    }
}
