using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A resonance type chart for menus: the advantage wheel in one line (each one beats the next, "&gt;") and Prism,
    /// which beats nothing but crits more. Physical is left out (it's neutral to everything). Built from <see cref="ResonanceChart.Beats"/>, so it always matches
    /// the rules. Element orbs from <see cref="CardIcons.Element"/>, names in their colours (<see cref="ResonanceInfo"/>).
    /// </summary>
    public static class ResonanceChartView
    {
        public static readonly Vector2 Size = new Vector2(830f, 280f);

        /// <summary>Builds the chart panel centred at <paramref name="position"/> (anchored to the parent's centre), at
        /// <paramref name="scale"/> of its full size.</summary>
        public static RectTransform Create(RectTransform parent, Vector2 position, float scale = 1f)
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            var panel = InkStyle.Panel(parent, "ResonanceChart", new Color(0.06f, 0.055f, 0.05f, 0.9f));
            var rt = panel.rectTransform;
            rt.Place(center, center, position, Size);
            rt.localScale = Vector3.one * scale;
            InkStyle.Frame(rt, InkStyle.Rule, 2f);
            var title = InkStyle.Label(rt, "Title", "Resonance chart", 30, InkStyle.Gold, TextAnchor.UpperLeft);
            title.rectTransform.Place(tl, tl, new Vector2(20f, -12f), new Vector2(500f, 40f));

            // The wheel: start at Arc and follow who each one beats until it comes back round.
            var wheel = new List<Resonance> { Resonance.Arc };
            for (int guard = 0; guard < 8; guard++)
            {
                var next = BeatenBy(wheel[wheel.Count - 1]);
                if (next == null) break;
                wheel.Add(next.Value);
                if (next.Value == wheel[0]) break;
            }
            Row(rt, wheel, ">", -62f);

            // Prism: outside the wheel.
            Row(rt, new List<Resonance> { Resonance.Prism }, "", -172f, "neutral to all, more crits");
            return rt;
        }

        /// <summary>The element <paramref name="attacker"/> beats (the first one, in enum order), or null.</summary>
        static Resonance? BeatenBy(Resonance attacker)
        {
            foreach (Resonance e in System.Enum.GetValues(typeof(Resonance)))
                if (e != Resonance.Physical && e != attacker && ResonanceChart.Beats(attacker, e)) return e;
            return null;
        }

        /// <summary>One line of orbs with their names, joined by <paramref name="link"/>, and an optional note after.</summary>
        static void Row(RectTransform panel, List<Resonance> elements, string link, float y, string note = null)
        {
            var tl = new Vector2(0f, 1f);
            const float item = 130f, gap = 34f, orb = 56f;
            float x = 20f;
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                var icon = CardIcons.Element(e);
                if (icon != null)
                {
                    var img = UIFactory.CreateImage(panel, "Orb", Color.white);
                    img.sprite = icon;
                    img.preserveAspect = true;
                    img.rectTransform.Place(tl, tl, new Vector2(x + (item - orb) / 2f, y), new Vector2(orb, orb));
                }
                var name = UIFactory.CreateText(panel, "Name", ResonanceInfo.Display(e), 26, ResonanceInfo.Color(e), TextAnchor.UpperCenter);
                name.rectTransform.Place(tl, tl, new Vector2(x, y - orb - 2f), new Vector2(item, 32f));
                x += item;
                if (i < elements.Count - 1)
                {
                    var arrow = UIFactory.CreateText(panel, "Link", link, 32, InkStyle.Paper, TextAnchor.MiddleCenter);
                    arrow.rectTransform.Place(tl, tl, new Vector2(x, y - 4f), new Vector2(gap, orb));
                    x += gap;
                }
            }
            if (note != null)
            {
                var n = UIFactory.CreateText(panel, "Note", note, 26, InkStyle.PaperDim, TextAnchor.MiddleLeft);
                n.rectTransform.Place(tl, tl, new Vector2(x + 20f, y - 4f), new Vector2(360f, orb));
            }
        }
    }
}
