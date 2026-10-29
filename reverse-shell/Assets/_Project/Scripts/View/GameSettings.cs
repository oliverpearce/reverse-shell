using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Combat.View
{
    /// <summary>
    /// Player settings shared by the title screen, the overworld's Esc → Settings (which adds Leave Sandbox Mode while
    /// in it, Game.World.PauseCards) and the battle pause menu. Each also has the two volume sliders
    /// (<see cref="SfxVolume"/>, <see cref="MusicVolume"/>, a <see cref="VolumeSlider"/> each). <see cref="Options"/> is the list both
    /// menus show: a label with the current value, what selecting it does, and a help line for its "?". Every
    /// Settings menu also has the Tutorials row (<see cref="GuideLabel"/>), which opens the guide.
    /// </summary>
    public static class GameSettings
    {
        // Screen.fullScreen only changes on the next frame, so the label tracks what was asked for.
        static bool? pendingFullscreen;

        public static bool Fullscreen { get { return pendingFullscreen ?? Screen.fullScreen; } }

        public static string FullscreenLabel
        {
            get { return UIText.Get("settings.fullscreen", "value", UIText.Get(Fullscreen ? "settings.on" : "settings.off")); }
        }

        public static void ToggleFullscreen()
        {
            pendingFullscreen = !Fullscreen;
            Screen.fullScreen = pendingFullscreen.Value;
        }

        /// <summary>The web build's version explains that browsers only go fullscreen on a click, and keep Esc for leaving it.</summary>
        public static string FullscreenHelp
        {
            get
            {
                return UIText.Get(Application.platform == RuntimePlatform.WebGLPlayer
                    ? "settings.fullscreen.help.web" : "settings.fullscreen.help");
            }
        }

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
        public static string ControlsLabel
        {
            get
            {
                string value = Controls == ControlMode.Mouse ? "settings.input.mouse"
                    : Controls == ControlMode.Keyboard ? "settings.input.keyboard" : "settings.input.both";
                return UIText.Get("settings.input", "value", UIText.Get(value));
            }
        }

        /// <summary>Both → Mouse → Keyboard → Both.</summary>
        public static void CycleControls() { Controls = (ControlMode)(((int)Controls + 1) % 3); }

        public static string ControlsHelp { get { return UIText.Get("settings.input.help"); } }

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

        public static string GraphicsLabel
        {
            get { return UIText.Get("settings.graphics", "value", UIText.Get(FastGraphics ? "settings.graphics.fast" : "settings.graphics.pretty")); }
        }

        public static void ToggleGraphics() { FastGraphics = !FastGraphics; }

        public static string GraphicsHelp { get { return UIText.Get("settings.graphics.help"); } }

        /// <summary>A camera's HDR follows the Graphics setting (off when Fast).</summary>
        public static void ApplyGraphics(Camera cam)
        {
            if (cam != null) cam.allowHDR = !FastGraphics;
        }

        // ------------------------------------------------------------------ Volume

        const string SfxKey = "settings.sfx", MusicKey = "settings.music";
        /// <summary>Both volumes start at 70%.</summary>
        public const float DefaultVolume = 0.7f;

        /// <summary>Sound effects volume, 0-1 (the dialogue blips and every effect). Saved (PlayerPrefs).</summary>
        public static float SfxVolume
        {
            get { return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultVolume)); }
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Music volume, 0-1 (for the music when it's in). Saved (PlayerPrefs).</summary>
        public static float MusicVolume
        {
            get { return Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, DefaultVolume)); }
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static string SfxLabel { get { return UIText.Get("settings.sfx"); } }
        public static string SfxHelp { get { return UIText.Get("settings.sfx.help"); } }
        public static string MusicLabel { get { return UIText.Get("settings.music"); } }
        public static string MusicHelp { get { return UIText.Get("settings.music.help"); } }

        // ------------------------------------------------------------------ Guide

        /// <summary>The Settings row that opens the guide (<see cref="GuideBook"/>), in every Settings menu.</summary>
        public static string GuideLabel { get { return UIText.Get("settings.tutorials"); } }

        public static string GuideHelp { get { return UIText.Get("settings.tutorials.help"); } }

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

        /// <summary>Built on each read, so the help lines come fresh from UI.txt.</summary>
        public static Option[] Options
        {
            get
            {
                return new[]
                {
                    new Option { Label = () => FullscreenLabel, Toggle = ToggleFullscreen, Help = FullscreenHelp },
                    new Option { Label = () => ControlsLabel, Toggle = CycleControls, Help = ControlsHelp },
                    new Option { Label = () => GraphicsLabel, Toggle = ToggleGraphics, Help = GraphicsHelp },
                };
            }
        }
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
