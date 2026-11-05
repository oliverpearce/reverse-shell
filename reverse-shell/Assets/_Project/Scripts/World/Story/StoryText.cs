using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Every word the game says, kept in plain text files so it can be pasted in from the bible or any document: every
    /// .txt in Resources/Story (Chapter1.txt = the chapter's script, World.txt = everything else: objectives, Access
    /// Points, chests, battle outcomes, the Sandbox). Keys are shared across files. Format (also at the top of
    /// each file):
    ///   # comment
    ///   [key]                      starts a block
    ///   Speaker | text             one dialogue box spoken by Speaker
    ///   text                       one box of narration (no speaker)
    ///   Thought | text             the protagonist's inner voice: no name plate, soft blue-grey text (so the scene reads
    ///                              the same whoever leads the party)
    /// {name} becomes the protagonist's name; a few lines take other fill-ins (e.g. {item}), passed to
    /// <see cref="Text(string, string[])"/>. Long lines are split into boxes automatically (DialoguePager), so a whole
    /// paragraph can go on one line. A missing key shows "[missing: key]" in game so it's easy to spot.
    /// </summary>
    public static class StoryText
    {
        /// <summary>The Resources folder holding the script files.</summary>
        public const string ResourceFolder = "Story";

        public struct Entry
        {
            public string Speaker, Text;
        }

        static Dictionary<string, List<Entry>> blocks;

        /// <summary>The block's lines with {name} filled in.</summary>
        public static List<Entry> Get(string key)
        {
            Load();
            var result = new List<Entry>();
            List<Entry> lines;
            if (key == null || !blocks.TryGetValue(key, out lines))
            {
                result.Add(new Entry { Speaker = "", Text = "[missing: " + key + "]" });
                return result;
            }
            foreach (var l in lines)
                result.Add(new Entry { Speaker = FillName(l.Speaker), Text = FillName(l.Text) });
            return result;
        }

        /// <summary>The protagonist's inner voice ("Thought | text"): shown with no speaker, in <see cref="ThoughtColor"/>.</summary>
        public const string Thought = "Thought";
        public const string ThoughtColor = "#AEB9D4";

        /// <summary>The block's lines as dialogue, each with the voice <paramref name="voiceFor"/> picks for its speaker.
        /// Thought lines have no speaker (the narrator's voice) and are tinted.</summary>
        public static List<DialogueLine> Lines(string key, Func<string, DialogueVoice> voiceFor)
        {
            var list = new List<DialogueLine>();
            foreach (var e in Get(key))
            {
                if (e.Speaker == Thought) list.Add(new DialogueLine("", "{c=" + ThoughtColor + "}" + e.Text + "{/c}", voiceFor("")));
                else list.Add(new DialogueLine(e.Speaker, e.Text, voiceFor(e.Speaker)));
            }
            return list;
        }

        /// <summary>The block's text as one string (its lines joined by a pause), for single messages.</summary>
        public static string Text(string key)
        {
            var parts = new List<string>();
            foreach (var e in Get(key)) parts.Add(e.Text);
            return string.Join("{p} ", parts.ToArray());
        }

        /// <summary><see cref="Text(string)"/> with fill-ins: pairs of name and value, e.g. ("item", "Ult Chip") for {item}.</summary>
        public static string Text(string key, params string[] fill)
        {
            return Fill(Text(key), fill);
        }

        /// <summary>The first line's speaker of a block ("" if narration), for one-box messages that show a name.</summary>
        public static string Speaker(string key)
        {
            var lines = Get(key);
            return lines.Count > 0 ? lines[0].Speaker : "";
        }

        /// <summary>Replaces {name}-style fill-ins: <paramref name="fill"/> is name, value, name, value...</summary>
        public static string Fill(string text, params string[] fill)
        {
            if (text == null || fill == null) return text;
            for (int i = 0; i + 1 < fill.Length; i += 2) text = text.Replace("{" + fill[i] + "}", fill[i + 1] ?? "");
            return text;
        }

        /// <summary>Just the texts of a block's lines (lists such as menu options or the opening narration).</summary>
        public static string[] Texts(string key)
        {
            return Get(key).ConvertAll(e => e.Text).ToArray();
        }

        public static bool Has(string key)
        {
            Load();
            return blocks.ContainsKey(key);
        }

        static string FillName(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            string name = string.IsNullOrEmpty(StoryState.PlayerName) ? "A3" : StoryState.PlayerName;
            return s.Replace("{name}", name);
        }

        static void Load()
        {
            if (blocks != null) return;
            blocks = new Dictionary<string, List<Entry>>();
            var files = Resources.LoadAll<TextAsset>(ResourceFolder);
            if (files.Length == 0) Debug.LogWarning("StoryText: no script files in Resources/" + ResourceFolder + ".");
            foreach (var file in files)
                if (file.name != "UI") Parse(file.text, blocks);   // UI.txt is the menus' words (UIText), not story
        }

        /// <summary>Parses the script format into blocks (public for tests).</summary>
        public static void Parse(string text, Dictionary<string, List<Entry>> into)
        {
            List<Entry> current = null;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = new List<Entry>();
                    into[line.Substring(1, line.Length - 2).Trim()] = current;
                    continue;
                }
                if (current == null) continue;
                int bar = line.IndexOf(" | ", StringComparison.Ordinal);
                if (bar >= 0) current.Add(new Entry { Speaker = line.Substring(0, bar).Trim(), Text = line.Substring(bar + 3).Trim() });
                else current.Add(new Entry { Speaker = "", Text = line });
            }
        }

        /// <summary>Forget the cached file (the editor reloads it on the next read).</summary>
        public static void Reload() { blocks = null; }
    }
}
