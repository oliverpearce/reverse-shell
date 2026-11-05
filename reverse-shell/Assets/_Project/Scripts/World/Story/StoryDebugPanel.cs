using Combat.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World
{
    /// <summary>
    /// F8 (editor and development builds only): the story at a glance. Shows the area, the objective and every Chapter
    /// One flag, and jumps straight to a beat (sets that beat's flags and area, saves, reloads). For testing the chapter
    /// without replaying it.
    /// </summary>
    public class StoryDebugPanel : MonoBehaviour
    {
        bool open;
        Vector2 scroll;

        struct Beat
        {
            public string Label, Area;
            public string[] Flags;
            public Beat(string label, string area, params string[] flags) { Label = label; Area = area; Flags = flags; }
        }

        static readonly Beat[] Beats =
        {
            new Beat("1  Awake in the city", WorldAreas.City, Chapter1.Named, Chapter1.Awake),
            new Beat("2  Scanner picked up", WorldAreas.City, Chapter1.Named, Chapter1.Awake, Chapter1.Scanner),
            new Beat("3  Collector beaten", WorldAreas.City, Chapter1.Named, Chapter1.Awake, Chapter1.Scanner, Chapter1.CollectorMet, Chapter1.CollectorBeaten),
            new Beat("4  Hidden route open", WorldAreas.City, Chapter1.Named, Chapter1.Awake, Chapter1.Scanner, Chapter1.CollectorMet, Chapter1.CollectorBeaten, Chapter1.Route),
            new Beat("5  In the tunnel", WorldAreas.Tunnel, Chapter1.Named, Chapter1.Awake, Chapter1.Scanner, Chapter1.CollectorMet, Chapter1.CollectorBeaten, Chapter1.Route, Chapter1.Tunnel),
            new Beat("6  Dragon's chamber", WorldAreas.Chamber, Chapter1.Named, Chapter1.Awake, Chapter1.Scanner, Chapter1.CollectorMet, Chapter1.CollectorBeaten, Chapter1.Route, Chapter1.Tunnel),
            new Beat("7  Dragon beaten", WorldAreas.Chamber, Chapter1.Named, Chapter1.Awake, Chapter1.Scanner, Chapter1.CollectorMet, Chapter1.CollectorBeaten, Chapter1.Route, Chapter1.Tunnel, Chapter1.DragonSeen, Chapter1.DragonBeaten),
        };

        public static void Ensure()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (FindObjectOfType<StoryDebugPanel>() != null) return;
            new GameObject("StoryDebugPanel").AddComponent<StoryDebugPanel>();
        }

        void Update()
        {
            // F8, or Ctrl+Shift+D (a laptop's F8 is often a media key unless Fn is held).
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F8) || (ctrl && shift && Input.GetKeyDown(KeyCode.D))) Toggle();
        }

        /// <summary>Opens or closes the panel (F8, Ctrl+Shift+D, or Reverse Shell → Story in the editor).</summary>
        public void Toggle() { open = !open; }

        void OnGUI()
        {
            if (!open) return;
            GUI.skin.label.fontSize = 14;
            GUI.skin.button.fontSize = 14;
            GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("STORY DEBUG (F8 or Ctrl+Shift+D)");
            GUILayout.Label("Area: " + WorldSession.CurrentArea + "    Name: " + (string.IsNullOrEmpty(StoryState.PlayerName) ? "-" : StoryState.PlayerName));
            GUILayout.Label("Objective: " + Chapter1.CurrentObjective());
            GUILayout.Space(6);
            foreach (var flag in Chapter1.All)
                GUILayout.Label((StoryState.Has(flag) ? "[x] " : "[ ] ") + flag);
            GUILayout.Space(6);
            GUILayout.Label("Jump to:");
            foreach (var beat in Beats)
                if (GUILayout.Button(beat.Label)) Jump(beat);
            if (GUILayout.Button("0  Play the reflection here"))
            {
                var world = FindObjectOfType<OverworldBuilder>();
                if (world != null) { open = false; ChapterOneDirector.ReplayReflection(world); }
            }
            // The ending (the Collector's trap, Erin's Soul Card ripped): plays here and now, on this save.
            if (GUILayout.Button("8  Play the ending here"))
            {
                var world = FindObjectOfType<OverworldBuilder>();
                if (world != null) { open = false; ChapterOneDirector.ReplayEnding(world); }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void Jump(Beat beat)
        {
            foreach (var f in beat.Flags) StoryState.Flags.Add(f);
            if (string.IsNullOrEmpty(StoryState.PlayerName)) StoryState.PlayerName = "A3";
            WorldSession.TitleDone = true;
            WorldSession.CurrentArea = beat.Area;
            WorldSession.HasReturnPoint = false;
            WorldSession.ArrivedFrom = null;
            SaveGame.Save();
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
