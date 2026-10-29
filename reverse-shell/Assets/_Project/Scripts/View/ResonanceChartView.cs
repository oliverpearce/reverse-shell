using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A resonance type chart for menus: the advantage wheel as a ring (Arc at the top, each one's arrow pointing at
    /// the one it beats, round clockwise) with Prism in the middle, which beats nothing but crits more. Basic is left
    /// out (it's neutral to everything). Built from <see cref="ResonanceChart.Beats"/>, so it always matches the rules
    /// (a fifth element in the wheel would make it a pentagon). Element orbs from <see cref="CardIcons.Element"/>,
    /// names in their colours (<see cref="ResonanceInfo"/>).
    /// </summary>
    public static class ResonanceChartView
    {
        public static readonly Vector2 Size = new Vector2(620f, 340f);

        const float Orb = 60f, NameGap = 6f, NameH = 40f;   // the name box is taller than the text, so it never clips
        const float RadiusX = 170f, RadiusY = 110f;
        static readonly Vector2 RingCenter = new Vector2(60f, 18f);   // right of the title

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
            var title = InkStyle.Label(rt, "Title", "Resonance\nchart", UIFactory.TextSize.Large, InkStyle.Gold, TextAnchor.UpperLeft);
            title.rectTransform.Place(tl, tl, new Vector2(20f, -12f), new Vector2(200f, 90f));

            // The wheel: start at Arc and follow who each one beats until it comes back round.
            var wheel = new List<Resonance> { Resonance.Arc };
            for (int guard = 0; guard < 8; guard++)
            {
                var next = BeatenBy(wheel[wheel.Count - 1]);
                if (next == null || next.Value == wheel[0]) break;
                wheel.Add(next.Value);
            }

            // Round the ring clockwise from the top, an arrow from each to the one it beats.
            var spots = new List<Vector2>();
            for (int i = 0; i < wheel.Count; i++)
            {
                float angle = (90f - i * 360f / wheel.Count) * Mathf.Deg2Rad;
                spots.Add(RingCenter + new Vector2(Mathf.Cos(angle) * RadiusX, Mathf.Sin(angle) * RadiusY));
            }
            if (wheel.Count > 1)
                for (int i = 0; i < wheel.Count; i++) Arrow(rt, spots[i], spots[(i + 1) % wheel.Count]);
            for (int i = 0; i < wheel.Count; i++) Element(rt, wheel[i], spots[i]);
            Element(rt, Resonance.Prism, RingCenter);
            return rt;
        }

        /// <summary>The element <paramref name="attacker"/> beats (the first one, in enum order), or null.</summary>
        static Resonance? BeatenBy(Resonance attacker)
        {
            foreach (Resonance e in System.Enum.GetValues(typeof(Resonance)))
                if (e != Resonance.Basic && e != attacker && ResonanceChart.Beats(attacker, e)) return e;
            return null;
        }

        /// <summary>One element's orb centred at <paramref name="at"/> (from the panel's centre), its name under it.</summary>
        static void Element(RectTransform panel, Resonance e, Vector2 at)
        {
            var center = new Vector2(0.5f, 0.5f);
            var icon = CardIcons.Element(e);
            if (icon != null)
            {
                var img = UIFactory.CreateImage(panel, "Orb", Color.white);
                img.sprite = icon;
                img.preserveAspect = true;
                img.rectTransform.Place(center, center, at, new Vector2(Orb, Orb));
            }
            var name = UIFactory.CreateText(panel, "Name", ResonanceInfo.Display(e), UIFactory.TextSize.Medium, ResonanceInfo.Color(e), TextAnchor.MiddleCenter);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Overflow;
            name.rectTransform.Place(center, center, at - new Vector2(0f, Orb / 2f + NameGap + NameH / 2f), new Vector2(140f, NameH));
        }

        /// <summary>A gold arrow from one orb to the next, stopping short of both (and of the names under them).</summary>
        static void Arrow(RectTransform panel, Vector2 from, Vector2 to)
        {
            var dir = (to - from).normalized;
            var start = from + dir * (Orb / 2f + 14f);
            var end = to - dir * (Orb / 2f + 14f);
            Bar(panel, "Arrow", start, end, 4f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            foreach (float side in new[] { 150f, -150f })   // the head: two short strokes back from the tip
            {
                float a = (angle + side) * Mathf.Deg2Rad;
                Bar(panel, "Head", end, end + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 16f, 4f);
            }
        }

        /// <summary>A straight gold stroke between two points (from the panel's centre).</summary>
        static void Bar(RectTransform panel, string name, Vector2 a, Vector2 b, float width)
        {
            var center = new Vector2(0.5f, 0.5f);
            var line = InkStyle.Line(panel, name, InkStyle.Gold);
            var d = b - a;
            line.rectTransform.Place(center, center, (a + b) / 2f, new Vector2(d.magnitude + width / 2f, width));
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }
}
