using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// Celeste-style animated text on a uGUI Text: the whole line is laid out up front (no reflow), then each
    /// character pops in on its own schedule (scale + rise + fade), and styled characters keep moving
    /// (wave / shake) or take their own color. Works by editing the Text's mesh, one quad per character.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class DynamicText : BaseMeshEffect
    {
        const float PopSeconds = 0.12f;

        Text text;
        ParsedLine line;
        float[] revealAt;
        float clock;
        bool animatedGlyphs;
        readonly List<int> quadToChar = new List<int>();

        /// <summary>Number of characters that have started to appear.</summary>
        public int RevealedCount { get; private set; }
        public bool FullyRevealed { get { return line == null || clock >= TotalTime + PopSeconds; } }
        public string PlainText { get { return line != null ? line.Text : ""; } }
        float TotalTime { get { return revealAt != null && revealAt.Length > 0 ? revealAt[revealAt.Length - 1] : 0f; } }

        protected override void Awake()
        {
            base.Awake();
            text = GetComponent<Text>();
            text.supportRichText = false; // markup is ours; quads must map 1:1 to characters
        }

        public void SetLine(ParsedLine parsed)
        {
            line = parsed;
            clock = 0f;
            RevealedCount = 0;
            revealAt = new float[parsed.Glyphs.Length];
            float t = 0f;
            animatedGlyphs = false;
            for (int i = 0; i < parsed.Glyphs.Length; i++)
            {
                t += parsed.Glyphs[i].Delay;
                revealAt[i] = t;
                animatedGlyphs |= parsed.Glyphs[i].Wave || parsed.Glyphs[i].Shake;
            }
            if (text == null) text = GetComponent<Text>();
            text.text = parsed.Text;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        /// <summary>Shows everything immediately (the player pressed to skip).</summary>
        public void Complete()
        {
            if (line == null) return;
            clock = TotalTime + PopSeconds;
            RevealedCount = revealAt.Length;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        void Update()
        {
            if (line == null) return;
            bool revealing = !FullyRevealed;
            clock += Time.unscaledDeltaTime;
            while (RevealedCount < revealAt.Length && clock >= revealAt[RevealedCount]) RevealedCount++;
            if (revealing || animatedGlyphs) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || line == null) return;
            int quads = vh.currentVertCount / 4;
            BuildQuadMap(quads);

            var v = new UIVertex();
            float time = Time.unscaledTime;
            for (int q = 0; q < quads && q < quadToChar.Count; q++)
            {
                int ci = quadToChar[q];
                if (ci < 0 || ci >= line.Glyphs.Length) continue;
                var g = line.Glyphs[ci];

                float t = Mathf.Clamp01((clock - revealAt[ci]) / PopSeconds);
                float ease = 1f - (1f - t) * (1f - t);
                float scale = Mathf.Lerp(1.7f, 1f, ease);
                var offset = new Vector3(0f, Mathf.Lerp(-10f, 0f, ease), 0f);
                if (g.Wave) offset.y += Mathf.Sin(time * 7f + ci * 0.55f) * 4f;
                if (g.Shake) offset += new Vector3(Mathf.PerlinNoise(time * 22f, ci * 3.1f) - 0.5f, Mathf.PerlinNoise(ci * 1.7f, time * 22f) - 0.5f, 0f) * 4f;

                var center = Vector3.zero;
                for (int k = 0; k < 4; k++)
                {
                    vh.PopulateUIVertex(ref v, q * 4 + k);
                    center += v.position;
                }
                center /= 4f;

                for (int k = 0; k < 4; k++)
                {
                    int idx = q * 4 + k;
                    vh.PopulateUIVertex(ref v, idx);
                    v.position = center + (v.position - center) * scale + offset;
                    var c = g.HasColor ? g.Color : v.color;
                    c.a = (byte)(c.a * t);
                    v.color = c;
                    vh.SetUIVertex(v, idx);
                }
            }
        }

        /// <summary>
        /// Which character each quad draws. Depending on the Unity version, whitespace and line breaks may or may
        /// not get (empty) quads, so pick the mapping that matches the quad count.
        /// </summary>
        void BuildQuadMap(int quads)
        {
            quadToChar.Clear();
            string s = line.Text;
            int noNewlines = 0, noWhitespace = 0;
            foreach (char c in s)
            {
                if (c != '\n') noNewlines++;
                if (!char.IsWhiteSpace(c)) noWhitespace++;
            }
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quads == s.Length) quadToChar.Add(i);
                else if (quads == noNewlines) { if (c != '\n') quadToChar.Add(i); }
                else if (!char.IsWhiteSpace(c)) quadToChar.Add(i);
            }
        }
    }
}
