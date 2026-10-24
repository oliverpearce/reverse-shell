using System;
using UnityEngine;

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
        };
    }
}
