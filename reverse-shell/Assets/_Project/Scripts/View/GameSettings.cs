using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Combat.View
{
    /// <summary>
    /// Player settings shared by the title screen and the battle pause menu (the overworld's Esc → Settings adds
    /// Enter / Leave Training Mode, Game.World.PauseCards). <see cref="Options"/> is the list both
    /// menus show: a label with the current value, what selecting it does, and a help line for its "?".
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

        // ------------------------------------------------------------------ Controls

        /// <summary>How menus and battle are driven: the mouse (hover, click, drag), the keyboard (arrows / WASD,
        /// Enter, Backspace) or both. Walking, F, Q and Esc always work. Saved (PlayerPrefs).</summary>
        public enum ControlMode { Both, Mouse, Keyboard }

        const string ControlsKey = "settings.controls";

        public static ControlMode Controls
        {
            get { return (ControlMode)Mathf.Clamp(PlayerPrefs.GetInt(ControlsKey, 0), 0, 2); }
            set { PlayerPrefs.SetInt(ControlsKey, (int)value); PlayerPrefs.Save(); }
        }

        public static string ControlsLabel { get { return "Controls:  " + Controls; } }

        /// <summary>Both → Mouse → Keyboard → Both.</summary>
        public static void CycleControls() { Controls = (ControlMode)(((int)Controls + 1) % 3); }

        public const string ControlsHelp = "Mouse: hover, click and drag. Keyboard: arrows or WASD, Enter and Backspace. " +
                                           "Both: either. Walking, F, Q and Esc always work.";

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
                Help = "Play in fullscreen or in a window. Changes right away.",
            },
            new Option
            {
                Label = () => ControlsLabel,
                Toggle = CycleControls,
                Help = ControlsHelp,
            },
        };
    }

    /// <summary>
    /// Menu and battle keys, honouring the Controls setting: with Controls = Mouse they never fire. Menus read
    /// <see cref="Pressed"/> instead of Input.GetKeyDown for everything but Esc (which always works).
    /// </summary>
    public static class UIKeys
    {
        public static bool KeyboardOn { get { return GameSettings.Controls != GameSettings.ControlMode.Mouse; } }
        public static bool MouseOn { get { return GameSettings.Controls != GameSettings.ControlMode.Keyboard; } }

        public static bool Pressed(KeyCode key) { return KeyboardOn && Input.GetKeyDown(key); }
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
