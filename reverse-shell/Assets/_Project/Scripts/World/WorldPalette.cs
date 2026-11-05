using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Each area's world palette (a Lospec palette chosen in the palette editor, Tools/PaletteEditor): the WorldToon
    /// shader snaps the world's finished colours to it, with an optional ordered dither. Read from
    /// Resources/Palettes/WorldPalettes.json (written by the editor's Save); an area without an entry is drawn as is.
    /// Characters, UI and other shaders keep their own colours.
    /// </summary>
    public static class WorldPalette
    {
        public const int MaxColors = 64;

        [Serializable]
        public class Area
        {
            public string id = "";
            public string source = "";
            public float dither;
            public List<string> colors = new List<string>();
        }

        [Serializable]
        class File
        {
            public List<Area> areas = new List<Area>();
        }

        static Dictionary<string, Area> byId;

        static readonly int PalId = Shader.PropertyToID("_Pal");
        static readonly int CountId = Shader.PropertyToID("_PalCount");
        static readonly int OnId = Shader.PropertyToID("_PalOn");
        static readonly int DitherId = Shader.PropertyToID("_PalDither");

        /// <summary>Parses the editor's JSON (public for tests).</summary>
        public static Dictionary<string, Area> Parse(string json)
        {
            var map = new Dictionary<string, Area>();
            if (string.IsNullOrEmpty(json)) return map;
            var file = JsonUtility.FromJson<File>(json);
            if (file == null || file.areas == null) return map;
            foreach (var a in file.areas)
                if (a != null && !string.IsNullOrEmpty(a.id) && a.colors != null && a.colors.Count > 0) map[a.id] = a;
            return map;
        }

        /// <summary>This area's palette, or null (drawn as is).</summary>
        public static Area For(string areaId)
        {
            if (byId == null)
            {
                var text = Resources.Load<TextAsset>("Palettes/WorldPalettes");
                byId = Parse(text != null ? text.text : null);
            }
            Area a;
            return areaId != null && byId.TryGetValue(areaId, out a) ? a : null;
        }

        /// <summary>Turns the area's palette on for the world shader (or off if it has none).</summary>
        public static void Apply(string areaId)
        {
            var a = For(areaId);
            if (a == null)
            {
                Off();
                return;
            }
            var colors = new Vector4[MaxColors];
            int n = 0;
            foreach (var hex in a.colors)
            {
                Color c;
                if (n < MaxColors && ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out c)) colors[n++] = c;
            }
            Shader.SetGlobalVectorArray(PalId, colors);
            Shader.SetGlobalFloat(CountId, n);
            Shader.SetGlobalFloat(DitherId, Mathf.Clamp01(a.dither));
            Shader.SetGlobalFloat(OnId, n > 0 ? 1f : 0f);
        }

        /// <summary>No world palette (battle, menus, leaving an area).</summary>
        public static void Off()
        {
            Shader.SetGlobalFloat(OnId, 0f);
        }

        /// <summary>Forget the loaded file (after the editor saves a new one, in the editor).</summary>
        public static void Reload() { byId = null; }
    }
}
