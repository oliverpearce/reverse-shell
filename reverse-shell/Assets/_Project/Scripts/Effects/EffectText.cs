using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Combat
{
    /// <summary>
    /// An effect's description as a template ("Deal {0} ATK damage to {1}.") plus its values. Keeping the
    /// numbers separate lets <see cref="CardText"/> merge all ranks into one line:
    /// "Deal (120% I / 180% II / 280% III) ATK damage to 1 enemy."
    /// </summary>
    public class EffectText
    {
        public readonly string Template;
        public readonly string[] Args;

        public EffectText(string template, params string[] args)
        {
            Template = template;
            Args = args ?? new string[0];
        }

        public string Format() { return Format(Args); }

        public string Format(string[] args)
        {
            return string.Format(Template, args.Cast<object>().ToArray());
        }
    }

    /// <summary>Builds card descriptions for showcasing abilities across ranks.</summary>
    public static class CardText
    {
        /// <summary>Placeholder an effect uses for "whoever the card targets"; swapped for e.g. "all allies".</summary>
        public const string CardTargetToken = "@target";

        public static string TargetPhrase(TargetMode mode)
        {
            switch (mode)
            {
                case TargetMode.SingleEnemy: return "1 enemy";
                case TargetMode.AllEnemies: return "all enemies";
                case TargetMode.RandomEnemy: return "a random enemy";
                case TargetMode.Self: return "self";
                case TargetMode.SingleAlly: return "the weakest ally";
                case TargetMode.AllAllies: return "all allies";
                default: return "a random ally";
            }
        }

        static string WithTargets(CardDefinition card, string text)
        {
            return text.Replace(CardTargetToken, TargetPhrase(card.targetMode));
        }
        public static string Roman(int rank)
        {
            switch (rank)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                default: return rank.ToString();
            }
        }

        /// <summary>Description of one rank (custom text if the rank has one, otherwise generated from its effects).</summary>
        public static string ForRank(CardDefinition card, int rank)
        {
            var data = card.GetRank(rank);
            if (data == null) return "";
            if (!string.IsNullOrEmpty(data.description)) return data.description;
            var sentences = Sentences(data).Select(t => t.Format()).ToArray();
            return WithTargets(card, string.Join(" ", sentences));
        }

        /// <summary>
        /// A rank's effects as sentences. Neighbouring effects that hit the same target share one sentence and name
        /// the target once at the end: "Deal 120% ATK damage and poison 1 enemy (x1, 2 turns)."
        /// </summary>
        public static List<EffectText> Sentences(CardRank data)
        {
            var result = new List<EffectText>();
            var effects = data.effects.Where(e => e != null).ToList();
            int i = 0;
            while (i < effects.Count)
            {
                var e = effects[i];
                if (!e.Joinable)
                {
                    result.Add(e.Text());
                    i++;
                    continue;
                }
                int j = i + 1;
                while (j < effects.Count && effects[j].Joinable && effects[j].TargetKey == e.TargetKey) j++;
                var clauses = new List<EffectText>();
                for (int k = i; k < j; k++) clauses.Add(effects[k].Clause(k == j - 1));
                result.Add(Compose(clauses));
                i = j;
            }
            return result;
        }

        static readonly System.Text.RegularExpressions.Regex Slot = new System.Text.RegularExpressions.Regex(@"\{(\d+)\}");

        /// <summary>Joins clauses ("a", "a and b", "a, b and c"), renumbering their placeholders, as one capitalized sentence.</summary>
        static EffectText Compose(List<EffectText> clauses)
        {
            var template = new StringBuilder();
            var args = new List<string>();
            for (int c = 0; c < clauses.Count; c++)
            {
                if (c > 0) template.Append(c == clauses.Count - 1 ? " and " : ", ");
                int offset = args.Count;
                template.Append(Slot.Replace(clauses[c].Template, m => "{" + (int.Parse(m.Groups[1].Value) + offset) + "}"));
                args.AddRange(clauses[c].Args);
            }
            string t = template.ToString();
            if (t.Length > 0) t = char.ToUpperInvariant(t[0]) + t.Substring(1);
            if (!t.EndsWith(".")) t += ".";
            return new EffectText(t, args.ToArray());
        }

        /// <summary>
        /// All ranks in one description. Values that change between ranks read "(a I / b II / c III)";
        /// effects only some ranks have read "II/III only: ...". Ranks with hand-written descriptions
        /// fall back to one line per rank.
        /// </summary>
        public static string AllRanks(CardDefinition card)
        {
            int ranks = card.MaxRank;
            if (ranks <= 1) return ForRank(card, 1);

            for (int r = 1; r <= ranks; r++)
            {
                var d = card.GetRank(r);
                if (d != null && !string.IsNullOrEmpty(d.description)) return PerRankLines(card, ranks);
            }

            var perRank = new List<List<EffectText>>();
            int longest = 0;
            for (int r = 1; r <= ranks; r++)
            {
                var data = card.GetRank(r);
                var texts = data != null ? Sentences(data) : new List<EffectText>();
                perRank.Add(texts);
                longest = System.Math.Max(longest, texts.Count);
            }

            var sentences = new List<string>();
            for (int i = 0; i < longest; i++)
            {
                var atIndex = new List<EffectText>();
                foreach (var texts in perRank) atIndex.Add(i < texts.Count ? texts[i] : null);

                bool allSameShape = atIndex.All(t => t != null && t.Template == atIndex[0].Template && t.Args.Length == atIndex[0].Args.Length);
                if (allSameShape)
                {
                    sentences.Add(MergeSentence(atIndex));
                    continue;
                }

                // Differs between ranks: group ranks that read the same.
                var groups = new List<KeyValuePair<string, List<int>>>();
                for (int r = 0; r < atIndex.Count; r++)
                {
                    if (atIndex[r] == null) continue;
                    string text = atIndex[r].Format();
                    var g = groups.FirstOrDefault(x => x.Key == text);
                    if (g.Key == null) groups.Add(new KeyValuePair<string, List<int>>(text, new List<int> { r + 1 }));
                    else g.Value.Add(r + 1);
                }
                foreach (var g in groups)
                {
                    bool everyRank = g.Value.Count == ranks;
                    sentences.Add(everyRank ? g.Key : string.Join("/", g.Value.Select(Roman).ToArray()) + " only: " + g.Key);
                }
            }
            return WithTargets(card, string.Join(" ", sentences.ToArray()));
        }

        static string MergeSentence(List<EffectText> texts)
        {
            var first = texts[0];
            var merged = new string[first.Args.Length];
            for (int a = 0; a < merged.Length; a++)
            {
                var values = texts.Select(t => t.Args[a]).ToList();
                if (values.All(v => v == values[0]))
                {
                    merged[a] = values[0];
                    continue;
                }
                var parts = new List<string>();
                for (int r = 0; r < values.Count; r++) parts.Add(values[r] + " " + Roman(r + 1));
                merged[a] = "(" + string.Join(" / ", parts.ToArray()) + ")";
            }
            return first.Format(merged);
        }

        static string PerRankLines(CardDefinition card, int ranks)
        {
            var lines = new List<string>();
            for (int r = 1; r <= ranks; r++) lines.Add(Roman(r) + ": " + ForRank(card, r));
            return string.Join("\n", lines.ToArray());
        }
    }
}
