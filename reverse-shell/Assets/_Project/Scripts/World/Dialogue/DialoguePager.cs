using System;
using System.Collections.Generic;
using System.Text;

namespace Game.World
{
    /// <summary>
    /// Splits a dialogue line that doesn't fit the box into several boxes. Breaks between words (preferring {p} pauses
    /// and sentence ends), never inside a {tag}, and carries open effects ({gold}, {wave}, {slow}...) over to the next box
    /// so colours and motion continue where they left off. <c>fits</c> measures the plain text against the box.
    /// </summary>
    public static class DialoguePager
    {
        public static List<string> Split(string markup, Func<string, bool> fits)
        {
            var pages = new List<string>();
            if (string.IsNullOrEmpty(markup) || fits(Plain(markup)))
            {
                pages.Add(markup ?? "");
                return pages;
            }

            var tokens = Tokens(markup);
            var page = new StringBuilder();
            var open = new List<string>();      // tags open at the start of the current page
            int lastGood = -1;                  // token index of the last break that still fit
            int start = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                string trial = page.ToString() + tokens[i];
                if (fits(Plain(trial)) || page.Length == 0)
                {
                    page.Append(tokens[i]);
                    lastGood = i;
                    continue;
                }
                // Prefer to end the page after the latest pause or sentence end on it, if there is one.
                int cut = lastGood;
                for (int j = lastGood; j > start; j--)
                {
                    string t = tokens[j].TrimEnd();
                    if (t.EndsWith("}") && t.Contains("{p") || t.EndsWith(".") || t.EndsWith("!") || t.EndsWith("?")) { cut = j; break; }
                }
                var body = new StringBuilder();
                foreach (var tag in open) body.Append('{').Append(tag).Append('}');
                for (int k = start; k <= cut; k++) body.Append(tokens[k]);
                var stillOpen = OpenTags(open, tokens, start, cut);
                pages.Add(Close(body.ToString().TrimEnd(), stillOpen));
                open = stillOpen;
                start = cut + 1;
                page.Length = 0;
                foreach (var tag in open) page.Append('{').Append(tag).Append('}');
                for (int k = start; k <= i; k++) page.Append(tokens[k]);
                lastGood = i;
            }
            if (start < tokens.Count)
            {
                var body = new StringBuilder();
                foreach (var tag in open) body.Append('{').Append(tag).Append('}');
                for (int k = start; k < tokens.Count; k++) body.Append(tokens[k]);
                string last = body.ToString().Trim();
                if (Plain(last).Trim().Length > 0) pages.Add(last);
            }
            return pages;
        }

        /// <summary>Words with their trailing spaces; a {tag} stays glued to the word it touches.</summary>
        static List<string> Tokens(string markup)
        {
            var tokens = new List<string>();
            var cur = new StringBuilder();
            bool inTag = false;
            for (int i = 0; i < markup.Length; i++)
            {
                char c = markup[i];
                cur.Append(c);
                if (c == '{') inTag = true;
                else if (c == '}') inTag = false;
                else if (c == ' ' && !inTag && (i + 1 >= markup.Length || markup[i + 1] != ' '))
                {
                    tokens.Add(cur.ToString());
                    cur.Length = 0;
                }
            }
            if (cur.Length > 0) tokens.Add(cur.ToString());
            return tokens;
        }

        /// <summary>Which effect tags are still open after tokens[from..to], given those open before.</summary>
        static List<string> OpenTags(List<string> before, List<string> tokens, int from, int to)
        {
            var open = new List<string>(before);
            for (int k = from; k <= to; k++)
            {
                string t = tokens[k];
                int i = 0;
                while ((i = t.IndexOf('{', i)) >= 0)
                {
                    int end = t.IndexOf('}', i);
                    if (end < 0) break;
                    string tag = t.Substring(i + 1, end - i - 1).Trim();
                    i = end + 1;
                    string lower = tag.ToLowerInvariant();
                    if (lower == "p" || lower.StartsWith("p=")) continue;
                    if (lower.StartsWith("/"))
                    {
                        // {/c} closes the latest colour; {/wave} etc. close their own kind.
                        string kind = lower.Substring(1);
                        for (int j = open.Count - 1; j >= 0; j--)
                        {
                            string o = open[j].ToLowerInvariant();
                            bool colour = !(o == "wave" || o == "shake" || o == "slow" || o == "fast");
                            if ((kind == "c" && colour) || o == kind || (kind == "slow" && o == "fast") || (kind == "fast" && o == "slow"))
                            {
                                open.RemoveAt(j);
                                break;
                            }
                        }
                    }
                    else open.Add(tag);
                }
            }
            return open;
        }

        static string Close(string body, List<string> open)
        {
            var sb = new StringBuilder(body);
            for (int j = open.Count - 1; j >= 0; j--)
            {
                string o = open[j].ToLowerInvariant();
                sb.Append(o == "wave" || o == "shake" || o == "slow" || o == "fast" ? "{/" + o + "}" : "{/c}");
            }
            return sb.ToString();
        }

        /// <summary>The text without markup (what is measured).</summary>
        public static string Plain(string markup)
        {
            return DialogueMarkup.Parse(markup ?? "", 1f).Text;
        }
    }
}
