using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.World
{
    /// <summary>How a speaker sounds and types: blip pitch, typing speed, and their accent color.</summary>
    public struct DialogueVoice
    {
        public float Pitch;
        /// <summary>1 = normal typing speed; 2 = twice as fast.</summary>
        public float Speed;
        public Color Accent;

        public DialogueVoice(float pitch, float speed, Color accent)
        {
            Pitch = pitch;
            Speed = speed;
            Accent = accent;
        }

        public static readonly DialogueVoice Default = new DialogueVoice(1f, 1f, new Color(0.3f, 0.3f, 0.34f));
        public static readonly DialogueVoice System = new DialogueVoice(1.7f, 1.8f, new Color(0.2f, 0.75f, 0.85f));
        /// <summary>Low, slow, red: machine signals and warnings.</summary>
        public static readonly DialogueVoice Machine = new DialogueVoice(0.55f, 1.2f, new Color(1f, 0.28f, 0.16f));
    }

    /// <summary>One line of a conversation.</summary>
    public class DialogueLine
    {
        public string Speaker;
        /// <summary>Text with optional markup; see <see cref="DialogueMarkup"/>.</summary>
        public string Text;
        public DialogueVoice Voice = DialogueVoice.Default;
        public Sprite Portrait;

        public DialogueLine(string speaker, string text, DialogueVoice voice, Sprite portrait = null)
        {
            Speaker = speaker;
            Text = text;
            Voice = voice;
            Portrait = portrait;
        }
    }

    /// <summary>Per-character style produced by the parser.</summary>
    public struct GlyphStyle
    {
        public bool Wave, Shake, HasColor;
        public Color32 Color;
        /// <summary>Seconds to wait after the previous character before this one appears.</summary>
        public float Delay;
    }

    public class ParsedLine
    {
        public string Text;
        public GlyphStyle[] Glyphs;
    }

    /// <summary>
    /// Celeste-style inline markup for dialogue. Tags are in braces and can nest:
    ///   {wave}...{/wave}   letters bob up and down          {shake}...{/shake}  letters tremble
    ///   {red} {gold} {blue} {green} {cyan} {gray} ...{/c}   color (or {c=#RRGGBB})
    ///   {slow}...{/slow}   type slower                        {fast}...{/fast}    type faster
    ///   {p}  short pause    {p=0.8}  pause for 0.8 s
    /// Sentence punctuation (. ! ? and commas) adds a natural beat after itself.
    /// </summary>
    public static class DialogueMarkup
    {
        const float BaseCharSeconds = 0.028f;

        static readonly Dictionary<string, Color32> Named = new Dictionary<string, Color32>
        {
            { "red", new Color32(214, 48, 49, 255) },
            { "gold", new Color32(201, 140, 18, 255) },
            { "blue", new Color32(38, 102, 214, 255) },
            { "green", new Color32(46, 150, 72, 255) },
            { "cyan", new Color32(20, 150, 170, 255) },
            { "gray", new Color32(120, 120, 128, 255) },
        };

        public static ParsedLine Parse(string markup, float speed)
        {
            var text = new StringBuilder();
            var glyphs = new List<GlyphStyle>();
            int wave = 0, shake = 0;
            float tempo = 1f;
            var colors = new Stack<Color32>();
            float pending = 0f; // extra delay carried to the next visible character
            speed = Mathf.Max(0.1f, speed);

            int i = 0;
            while (i < (markup ?? "").Length)
            {
                char ch = markup[i];
                if (ch == '{')
                {
                    int end = markup.IndexOf('}', i);
                    if (end > i)
                    {
                        string tag = markup.Substring(i + 1, end - i - 1).Trim().ToLowerInvariant();
                        ApplyTag(tag, ref wave, ref shake, ref tempo, colors, ref pending);
                        i = end + 1;
                        continue;
                    }
                }

                var g = new GlyphStyle
                {
                    Wave = wave > 0,
                    Shake = shake > 0,
                    HasColor = colors.Count > 0,
                    Color = colors.Count > 0 ? colors.Peek() : new Color32(0, 0, 0, 255),
                    Delay = BaseCharSeconds * tempo / speed + pending,
                };
                pending = 0f;
                text.Append(ch);
                glyphs.Add(g);

                // A beat after punctuation (not inside "..." runs or before a closing quote).
                char next = i + 1 < markup.Length ? markup[i + 1] : ' ';
                if ((ch == '.' || ch == '!' || ch == '?') && next != '.' && next != '!' && next != '?') pending += 0.22f * tempo / speed;
                else if (ch == ',' || ch == ';' || ch == '—') pending += 0.1f * tempo / speed;
                i++;
            }
            return new ParsedLine { Text = text.ToString(), Glyphs = glyphs.ToArray() };
        }

        static void ApplyTag(string tag, ref int wave, ref int shake, ref float tempo, Stack<Color32> colors, ref float pending)
        {
            switch (tag)
            {
                case "wave": wave++; return;
                case "/wave": wave = Mathf.Max(0, wave - 1); return;
                case "shake": shake++; return;
                case "/shake": shake = Mathf.Max(0, shake - 1); return;
                case "slow": tempo = 2.6f; return;
                case "fast": tempo = 0.45f; return;
                case "/slow":
                case "/fast": tempo = 1f; return;
                case "p": pending += 0.35f; return;
                case "/c":
                case "/color":
                    if (colors.Count > 0) colors.Pop();
                    return;
            }
            if (tag.StartsWith("p="))
            {
                float seconds;
                if (float.TryParse(tag.Substring(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds))
                    pending += Mathf.Max(0f, seconds);
                return;
            }
            Color32 named;
            if (Named.TryGetValue(tag, out named))
            {
                colors.Push(named);
                return;
            }
            if (tag.StartsWith("/") && Named.ContainsKey(tag.Substring(1)))
            {
                if (colors.Count > 0) colors.Pop();
                return;
            }
            if (tag.StartsWith("c="))
            {
                Color parsed;
                if (ColorUtility.TryParseHtmlString(tag.Substring(2), out parsed)) colors.Push(parsed);
            }
        }
    }
}
