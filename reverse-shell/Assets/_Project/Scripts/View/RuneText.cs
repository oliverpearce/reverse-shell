using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// CARDS > RUNES
    /// Card names on cards that aren't zoomed in: written in runes (the Standard Galactic Alphabet, the enchanting-table
    /// look) instead of small text, so a small card's plate reads as decoration and the real name shows when zoomed in.
    /// The runes are our own 5 px tall pixel glyphs (one per letter, drawn below), cut into sprites once, point filtered.
    /// Each rune is its own letter (<see cref="Letters"/>), exactly like the enchanting table: the runes translate with
    /// any Standard Galactic Alphabet chart.
    /// </summary>
    public static class RuneText
    {
        const int Rows = 5, Gap = 1, SpaceWidth = 2;

        // a-z, each 5 rows; '#' is ink. Widths vary (1-3 px).
        static readonly string[][] Glyphs =
        {
            new[] { "#..", "...", "..#", "..#", "###" },   // a  ᔑ
            new[] { "##.", "..#", ".#.", ".#.", ".##" },   // b  ʖ
            new[] { ".#.", "#..", "#.#", "#..", ".##" },   // c  ᓵ
            new[] { "###", "##.", "#.#", "...", "..." },   // d  ↸
            new[] { "#.#", "#.#", "#.#", "###", ".#." },   // e  ᒷ
            new[] { "...", "###", "...", "#.#", "..." },   // f  ⎓
            new[] { "..#", "..#", "###", "..#", "..#" },   // g  ⊣
            new[] { "###", "...", "###", ".#.", ".#." },   // h  ⍑
            new[] { "#", "#", ".", "#", "#" },             // i  ╎
            new[] { "#", ".", "#", ".", "#" },             // j  ⋮
            new[] { ".#.", "#.#", "###", "#..", "#.." },   // k  ꖌ
            new[] { "#..", "#..", "...", "#..", "###" },   // l  ꖎ
            new[] { "#..", "#..", "#.#", "#.#", "###" },   // m  ᒲ
            new[] { "#.#", "#.#", "#.#", "..#", ".#." },   // n  リ
            new[] { "###", "..#", "..#", "#.#", ".#." },   // o  𝙹
            new[] { "#.#", "#..", "#.#", "..#", "#.#" },   // p  !¡
            new[] { "###", "#.#", "#.#", "#.#", "..#" },   // q  ᑑ
            new[] { "...", "#.#", "...", "#.#", "..." },   // r  ∷
            new[] { "..#", "###", "#..", "#..", "###" },   // s  ᓭ
            new[] { "###", "..#", "..#", "..#", "..#" },   // t  ℸ
            new[] { "...", "#.#", "...", "###", "..." },   // u  ⚍
            new[] { ".#.", ".#.", "###", "...", "###" },   // v  ⍊
            new[] { "...", ".#.", "...", "#.#", "..." },   // w  ∴
            new[] { "#.#", "..#", ".#.", "#..", "..." },   // x  ̇/
            new[] { "#.#", "#.#", "#.#", "#.#", "#.#" },   // y  ||
            new[] { "###", "#.#", "#.#", "#.#", "#.#" },   // z  ⨅
        };

        static Sprite[] sprites;

        /// <summary>
        /// Writes <paramref name="text"/> in runes inside <paramref name="parent"/>, centred, as big as fits
        /// <paramref name="width"/> x <paramref name="height"/> (canvas units) but never taller than
        /// <paramref name="capHeight"/> (the height of the words it stands in for, so swapping between them doesn't jump).
        /// Letters without a rune (digits, punctuation) are skipped; spaces are a small gap.
        /// </summary>
        public static RectTransform Build(RectTransform parent, string text, Color color, float width, float height, float capHeight = float.MaxValue,
            bool left = false)
        {
            var row = UIFactory.CreateRect("Runes", parent).Fill();
            if (string.IsNullOrEmpty(text)) return row;
            Load();
            var letters = new List<int>();   // glyph index, or -1 for a space
            int total = 0;
            foreach (char raw in Letters(text))
            {
                if (raw == ' ') { letters.Add(-1); total += SpaceWidth; continue; }
                int k = raw - 'a';
                if (k < 0 || k >= Glyphs.Length) continue;
                if (letters.Count > 0 && letters[letters.Count - 1] >= 0) total += Gap;
                letters.Add(k);
                total += Glyphs[k][0].Length;
            }
            if (total == 0) return row;
            // One rune pixel: as big as fits, never taller than the space allows.
            float px = Mathf.Min(width / total, Mathf.Min(height, capHeight) / Rows);
            float x = left ? -width / 2f : -total * px / 2f;
            int prev = -1;
            foreach (int k in letters)
            {
                if (k < 0) { x += SpaceWidth * px; prev = -1; continue; }
                if (prev >= 0) x += Gap * px;
                int w = Glyphs[k][0].Length;
                var img = UIFactory.CreateImage(row, "Rune", color);
                img.sprite = sprites[k];
                img.raycastTarget = false;
                img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w * px, Rows * px));
                x += w * px;
                prev = k;
            }
            return row;
        }

        /// <summary>
        /// The letters the runes are drawn from: the text itself, lower case, one rune per letter (a = ᔑ, b = ʖ, ...), so
        /// "Strike" in runes reads s-t-r-i-k-e to anyone with an enchanting-table (SGA) chart. Letters with accents lose
        /// them; digits and punctuation have no rune and are left out where the runes are drawn.
        /// </summary>
        public static string Letters(string text)
        {
            var plain = (text ?? "").Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(plain.Length);
            foreach (char c in plain)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>The height of a capital letter in <paramref name="font"/> at <paramref name="size"/> (canvas units).</summary>
        public static float CapHeight(Font font, int size)
        {
            if (font == null) return size * 0.6f;
            font.RequestCharactersInTexture("E", size, FontStyle.Normal);
            CharacterInfo info;
            return font.GetCharacterInfo('E', out info, size, FontStyle.Normal) && info.glyphHeight > 0 ? info.glyphHeight : size * 0.6f;
        }

        /// <summary>
        /// A paragraph in runes, wrapped to <paramref name="area"/> from its top-left, as big as fits (capital height
        /// from <paramref name="maxCap"/> down to <paramref name="minCap"/>). Words become runes in
        /// <paramref name="color"/>, or in a phrase's colour where <paramref name="phrases"/> name it (statuses); words
        /// with a digit, and Roman numerals, stay readable, in <paramref name="numberColor"/> and
        /// <paramref name="numberScale"/> times as tall.
        /// </summary>
        public static void Paragraph(RectTransform area, string text, Color color, float maxCap, float minCap,
            IList<KeyValuePair<string, Color>> phrases, Color numberColor, float numberScale = 1.3f)
        {
            Load();
            var words = Words(text ?? "", color, phrases);
            var size = area.rect.size;
            for (float cap = maxCap; ; cap = Mathf.Max(minCap, cap - Mathf.Max(1f, maxCap * 0.08f)))
            {
                UIFactory.ClearChildren(area);
                if (Lay(area, words, cap, size, numberColor, numberScale) || cap <= minCap) return;
            }
        }

        struct Word
        {
            public string Text;
            public Color Color;
            public bool Readable;
        }

        static List<Word> Words(string text, Color color, IList<KeyValuePair<string, Color>> phrases)
        {
            // Each character's colour: the base, or a phrase's where it's named.
            var colors = new Color[text.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            if (phrases != null)
                foreach (var phrase in phrases)
                {
                    if (string.IsNullOrEmpty(phrase.Key)) continue;
                    for (int at = text.IndexOf(phrase.Key); at >= 0; at = text.IndexOf(phrase.Key, at + phrase.Key.Length))
                        for (int i = at; i < at + phrase.Key.Length; i++) colors[i] = phrase.Value;
                }
            var words = new List<Word>();
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && !char.IsWhiteSpace(text[i])) continue;
                if (i > start)
                {
                    string w = text.Substring(start, i - start);
                    words.Add(new Word { Text = w, Color = colors[start], Readable = IsNumber(w) });
                }
                if (i < text.Length && text[i] == '\n') words.Add(new Word { Text = "\n" });   // a forced line break
                start = i + 1;
            }
            return words;
        }

        /// <summary>A number ("180%", "2", "(2") or a Roman numeral ("II", "III."): kept readable.</summary>
        static bool IsNumber(string w)
        {
            foreach (char c in w) if (char.IsDigit(c)) return true;
            string core = w.Trim('.', ',', ':', ';', '(', ')');
            if (core.Length == 0 || core.Length > 4) return false;
            foreach (char c in core) if (c != 'I' && c != 'V' && c != 'X') return false;
            return true;
        }

        /// <summary>Lays the words out at one size; false if they run past the bottom.</summary>
        static bool Lay(RectTransform area, List<Word> words, float cap, Vector2 size, Color numberColor, float numberScale)
        {
            float px = cap / Rows, lineH = cap * 2.1f, space = px * 3f;
            float x = 0f, y = 0f;
            var font = UIFactory.PixelFont;
            float ratio = CapHeight(font, 100) / 100f;
            var topLeft = new Vector2(0f, 1f);
            foreach (var word in words)
            {
                if (word.Text == "\n") { x = 0f; y += lineH; continue; }
                RectTransform piece;
                float w;
                if (word.Readable)
                {
                    var t = UIFactory.CreateText(area, "Number", word.Text, 8, numberColor, TextAnchor.MiddleLeft);
                    t.fontSize = Mathf.Max(1, Mathf.RoundToInt(cap * numberScale / Mathf.Max(0.1f, ratio)));
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    w = t.preferredWidth;
                    piece = t.rectTransform;
                    piece.sizeDelta = new Vector2(w, lineH);
                }
                else
                {
                    piece = UIFactory.CreateRect("Word", area);
                    w = WordGlyphs(piece, Letters(word.Text), word.Color, px);
                    if (w <= 0f) { Object.Destroy(piece.gameObject); continue; }
                    piece.sizeDelta = new Vector2(w, Rows * px);
                }
                if (x > 0f && x + w > size.x)
                {
                    x = 0f;
                    y += lineH;
                }
                piece.anchorMin = piece.anchorMax = topLeft;
                piece.pivot = new Vector2(0f, 0.5f);
                piece.anchoredPosition = new Vector2(x, -(y + lineH / 2f));
                x += w + space;
            }
            return y + lineH <= size.y + 0.5f;
        }

        /// <summary>One word's runes, left to right inside <paramref name="parent"/>; returns their width.</summary>
        static float WordGlyphs(RectTransform parent, string word, Color color, float px)
        {
            float x = 0f;
            bool first = true;
            foreach (char c in word)
            {
                int k = c - 'a';
                if (k < 0 || k >= Glyphs.Length) continue;
                if (!first) x += Gap * px;
                first = false;
                int w = Glyphs[k][0].Length;
                var img = UIFactory.CreateImage(parent, "Rune", color);
                img.sprite = sprites[k];
                img.raycastTarget = false;
                img.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w * px, Rows * px));
                x += w * px;
            }
            return x;
        }

        static void Load()
        {
            if (sprites != null) return;
            sprites = new Sprite[Glyphs.Length];
            for (int k = 0; k < Glyphs.Length; k++)
            {
                var rows = Glyphs[k];
                int w = rows[0].Length;
                var tex = new Texture2D(w, Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x < w; x++)
                        tex.SetPixel(x, Rows - 1 - y, rows[y][x] == '#' ? Color.white : new Color(1f, 1f, 1f, 0f));
                tex.Apply();
                sprites[k] = Sprite.Create(tex, new Rect(0, 0, w, Rows), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { sprites = null; }
    }
}
