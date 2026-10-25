using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Combat.View
{
    /// <summary>
    /// Player settings shared by the title screen and the battle pause menu (the overworld's Esc → Settings adds
    /// Enter / Leave Sandbox Mode, Game.World.PauseCards). <see cref="Options"/> is the list both
    /// menus show: a label with the current value, what selecting it does, and a help line for its "?". Every
    /// Settings menu also has the Tutorials row (<see cref="GuideLabel"/>), which opens the guide.
    /// </summary>
    public static class GameSettings
    {
        // Screen.fullScreen only changes on the next frame, so the label tracks what was asked for.
        static bool? pendingFullscreen;

        public static bool Fullscreen { get { return pendingFullscreen ?? Screen.fullScreen; } }

        public static string FullscreenLabel { get { return "Fullscreen:  " + (Fullscreen ? "On" : "Off"); } }

        public static void ToggleFullscreen()
        {
            pendingFullscreen = !Fullscreen;
            Screen.fullScreen = pendingFullscreen.Value;
        }

        /// <summary>Browsers only go fullscreen on a click, and keep Esc for leaving it.</summary>
        const string WebFullscreenHelp = "Fill the screen (or use the button under the game). The browser switches on your " +
                                         "next click, and Esc leaves fullscreen, so open the menu with P there.";

        // ------------------------------------------------------------------ Controls

        /// <summary>How menus and battle are driven: the mouse (hover, click, drag), the keyboard (arrows / WASD,
        /// Enter, Backspace) or both. Walking, F, Q and Esc / P always work. Saved (PlayerPrefs).</summary>
        public enum ControlMode { Both, Mouse, Keyboard }

        const string ControlsKey = "settings.controls";

        public static ControlMode Controls
        {
            get { return (ControlMode)Mathf.Clamp(PlayerPrefs.GetInt(ControlsKey, 0), 0, 2); }
            set { PlayerPrefs.SetInt(ControlsKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Shown as "Input" (the guide, <see cref="GuideBook"/>, has a Controls tab).</summary>
        public static string ControlsLabel { get { return "Input:  " + Controls; } }

        /// <summary>Both → Mouse → Keyboard → Both.</summary>
        public static void CycleControls() { Controls = (ControlMode)(((int)Controls + 1) % 3); }

        public const string ControlsHelp = "Mouse: hover, click and drag. Keyboard: arrows or WASD, Enter and Backspace. " +
                                           "Both: either. Walking, F, Q and Esc (or P) always work.";

        // ------------------------------------------------------------------ Graphics

        const string GraphicsKey = "settings.graphics";

        /// <summary>
        /// Fast: no bloom / colour grading / vignette, no HDR, fewer drifting particles (much lighter in a browser).
        /// Pretty: the full look. Fast is the default in the web build, Pretty everywhere else. Saved (PlayerPrefs).
        /// </summary>
        public static bool FastGraphics
        {
            get { return PlayerPrefs.GetInt(GraphicsKey, Application.platform == RuntimePlatform.WebGLPlayer ? 1 : 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(GraphicsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (GraphicsChanged != null) GraphicsChanged();
            }
        }

        /// <summary>Raised when Graphics changes, so the cameras and post-processing switch right away.</summary>
        public static event Action GraphicsChanged;

        public static string GraphicsLabel { get { return "Graphics:  " + (FastGraphics ? "Fast" : "Pretty"); } }

        public static void ToggleGraphics() { FastGraphics = !FastGraphics; }

        public const string GraphicsHelp = "Fast: no glow or colour filter and fewer leaves, so the game runs cooler " +
                                           "(best in a browser). Pretty: the full look.";

        /// <summary>A camera's HDR follows the Graphics setting (off when Fast).</summary>
        public static void ApplyGraphics(Camera cam)
        {
            if (cam != null) cam.allowHDR = !FastGraphics;
        }

        // ------------------------------------------------------------------ Guide

        /// <summary>The Settings row that opens the guide (<see cref="GuideBook"/>), in every Settings menu.</summary>
        public const string GuideLabel = "Tutorials";

        public const string GuideHelp = "Every key, and the battle tutorials: turns and AP, merging, Ult, resonance and more.";

        // ------------------------------------------------------------------ Battle speed

        const string BattleSpeedKey = "settings.battleSpeed";

        /// <summary>How fast a turn plays out once Go is pressed: 1x, 2x or 4x. Saved (PlayerPrefs).</summary>
        public static int BattleSpeed
        {
            get
            {
                int v = PlayerPrefs.GetInt(BattleSpeedKey, 1);
                return v == 2 || v == 4 ? v : 1;
            }
            set { PlayerPrefs.SetInt(BattleSpeedKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>1x → 2x → 4x → 1x.</summary>
        public static void CycleBattleSpeed() { BattleSpeed = BattleSpeed == 1 ? 2 : BattleSpeed == 2 ? 4 : 1; }

        public class Option
        {
            public Func<string> Label;
            public Action Toggle;
            public string Help;
        }

        public static readonly Option[] Options =
        {
            new Option
            {
                Label = () => FullscreenLabel,
                Toggle = ToggleFullscreen,
                Help = Application.platform == RuntimePlatform.WebGLPlayer ? WebFullscreenHelp : "Play in fullscreen or in a window. Changes right away.",
            },
            new Option
            {
                Label = () => ControlsLabel,
                Toggle = CycleControls,
                Help = ControlsHelp,
            },
            new Option
            {
                Label = () => GraphicsLabel,
                Toggle = ToggleGraphics,
                Help = GraphicsHelp,
            },
        };
    }

    /// <summary>
    /// Menu and battle keys, honouring the Controls setting: with Controls = Mouse they never fire. Menus read
    /// <see cref="Pressed"/> instead of Input.GetKeyDown for everything but Esc / P (<see cref="Back"/>, which always works).
    /// </summary>
    public static class UIKeys
    {
        public static bool KeyboardOn { get { return GameSettings.Controls != GameSettings.ControlMode.Mouse; } }
        public static bool MouseOn { get { return GameSettings.Controls != GameSettings.ControlMode.Keyboard; } }

        public static bool Pressed(KeyCode key) { return KeyboardOn && Input.GetKeyDown(key); }

        /// <summary>
        /// Esc, or P (except while typing in a text field): opens and closes the pause menus and backs out of screens.
        /// Both always work. P is there for the web build, where Esc only leaves the browser's fullscreen.
        /// </summary>
        public static bool Back
        {
            get { return Input.GetKeyDown(KeyCode.Escape) || (Input.GetKeyDown(KeyCode.P) && !Typing); }
        }

        /// <summary>A text field (the name entry) has the keyboard.</summary>
        static bool Typing
        {
            get
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var field = selected != null ? selected.GetComponent<UnityEngine.UI.InputField>() : null;
                return field != null && field.isFocused;
            }
        }
    }

    /// <summary>
    /// The event system's input: the standard mouse handling, switched off (and the cursor hidden) when Controls =
    /// Keyboard, so nothing hovers, clicks or drags.
    /// </summary>
    public class ControlInputModule : StandaloneInputModule
    {
        public override void Process()
        {
            bool mouse = UIKeys.MouseOn;
            if (Cursor.visible != mouse) Cursor.visible = mouse;
            if (mouse) base.Process();
        }
    }
}
