using Game.World;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The story debug tools from Unity's menu bar, for when F8 doesn't reach the game (a laptop's F keys often need
    /// Fn, and the Game view has to have focus): Reverse Shell → Story → ... (Play mode, in the overworld).
    /// </summary>
    static class StoryDebugMenu
    {
        [MenuItem("Reverse Shell/Story/Play the Ending Here")]
        static void PlayEnding()
        {
            var world = Object.FindObjectOfType<OverworldBuilder>();
            if (world == null) { Debug.LogWarning("Play the Ending: start Play mode and get into the overworld first."); return; }
            ChapterOneDirector.ReplayEnding(world);
        }

        [MenuItem("Reverse Shell/Story/Play the Reflection Here")]
        static void PlayReflection()
        {
            var world = Object.FindObjectOfType<OverworldBuilder>();
            if (world == null) { Debug.LogWarning("Play the Reflection: start Play mode and get into the overworld first."); return; }
            ChapterOneDirector.ReplayReflection(world);
        }

        [MenuItem("Reverse Shell/Story/Story Debug Panel (F8)")]
        static void TogglePanel()
        {
            StoryDebugPanel.Ensure();
            var panel = Object.FindObjectOfType<StoryDebugPanel>();
            if (panel != null) panel.Toggle();
        }

        [MenuItem("Reverse Shell/Story/Play the Ending Here", true)]
        [MenuItem("Reverse Shell/Story/Play the Reflection Here", true)]
        [MenuItem("Reverse Shell/Story/Story Debug Panel (F8)", true)]
        static bool InPlayMode() { return Application.isPlaying; }
    }
}
