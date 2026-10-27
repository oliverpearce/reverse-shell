using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// The words on buttons, menus, banners and key hints, read from Resources/Story/UI.txt so they can be changed
    /// without touching code (story and dialogue stay in the other files there, read by StoryText). Format:
    ///   # comment
    ///   key = text
    /// \n in the text starts a new line. {gold} ... {/c} (also {red} {green} {cyan} {gray} {c=#RRGGBB}) colours words,
    /// like the story files. Some lines take fill-ins such as {slot}, listed in a comment above them. A missing key shows
    /// "[missing: key]" in game so it's easy to spot.
    /// </summary>
    public static class UIText
    {
        /// <summary>The Resources path of the file.</summary>
        public const string ResourcePath = "Story/UI";

        static Dictionary<string, string> lines;

        /// <summary>The line for this key, colours applied.</summary>
        public static string Get(string key)
        {
            Load();
            string text;
            return key != null && lines.TryGetValue(key, out text) ? text : "[missing: " + key + "]";
        }

        /// <summary><see cref="Get(string)"/> with fill-ins: pairs of name and value, e.g. ("slot", "2") for {slot}.</summary>
        public static string Get(string key, params string[] fill)
        {
            string text = Get(key);
            for (int i = 0; i + 1 < fill.Length; i += 2) text = text.Replace("{" + fill[i] + "}", fill[i + 1] ?? "");
            return text;
        }

        public static bool Has(string key)
        {
            Load();
            return lines.ContainsKey(key);
        }

        static void Load()
        {
            if (lines != null) return;
            lines = new Dictionary<string, string>();
            var file = Resources.Load<TextAsset>(ResourcePath);
            if (file == null) Debug.LogWarning("UIText: no Resources/" + ResourcePath + ".txt.");
            else Parse(file.text, lines);
        }

        /// <summary>Parses the key = text format (public for tests).</summary>
        public static void Parse(string text, Dictionary<string, string> into)
        {
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                into[line.Substring(0, eq).Trim()] = Markup(line.Substring(eq + 1).Trim().Replace("\\n", "\n"));
            }
        }

        /// <summary>Forget the cached file (the next read loads it again).</summary>
        public static void Reload() { lines = null; }

        // ------------------------------------------------------------------ Colours

        static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            { "gold", InkStyle.Gold },
            { "red", new Color(0.93f, 0.45f, 0.42f) },
            { "green", new Color(0.55f, 0.85f, 0.5f) },
            { "cyan", new Color(0.45f, 0.8f, 0.88f) },
            { "gray", InkStyle.Muted },
        };

        static readonly Regex Tag = new Regex(@"\{(gold|red|green|cyan|gray|c=#[0-9A-Fa-f]{6})\}");

        /// <summary>The story files' colour tags ({gold} ... {/c}) as rich text, in colours that read on ink.</summary>
        public static string Markup(string text)
        {
            text = Tag.Replace(text, m =>
            {
                string key = m.Groups[1].Value;
                if (key.StartsWith("c=")) return "<color=" + key.Substring(2) + ">";
                return "<color=#" + ColorUtility.ToHtmlStringRGB(Colors[key]) + ">";
            });
            return text.Replace("{/c}", "</color>");
        }
    }
}
