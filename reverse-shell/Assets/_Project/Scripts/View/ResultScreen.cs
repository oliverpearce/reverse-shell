using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The end-of-battle results, Persona / Fire Emblem-style and animated:
    ///   1. "RESULTS" (or "DEFEAT") slides in between gold rules, with "+N XP".
    ///   2. One row per member slides in: colour swatch, name, Lv, and a slanted XP bar.
    ///   3. The bars fill from the old XP to the new; crossing a level shows "LEVEL UP" on the row and the Lv number
    ///      punches up.
    ///   4. For each member who levelled: a big gold diamond "LEVEL UP / N" burst, then their stat panel on the right,
    ///      stacked under the previous one (one line per stat: label, old value → new value counting up, gold "+N ▲").
    ///   5. The Continue / Fight Again button appears.
    /// Only members who took part are listed: a backup who never stepped in earns no XP (BattleResult.Build).
    /// Built into the result overlay's content rect by <see cref="HudView.ShowResult"/>.
    /// </summary>
    public static class ResultScreen
    {
        static readonly Color Gold = InkStyle.Gold;
        static readonly Color Up = new Color(1f, 0.82f, 0.32f);
        static readonly Color RowColor = new Color(0.08f, 0.075f, 0.07f, 0.9f);
        static readonly StatType[] GrowStats = { StatType.MaxHP, StatType.Attack, StatType.PhysicalDefense, StatType.SpecialDefense };
        static string[] GrowLabels
        {
            get { return new[] { UIText.Get("stat.hp"), UIText.Get("stat.atk"), UIText.Get("stat.phys_def"), UIText.Get("stat.spec_def") }; }
        }

        const float RowsX = -360f, RowW = 860f, RowH = 112f, RowGap = 18f, RowsTop = 300f;
        // Stat panels: a name strip and one line per stat; three stacked panels end above the button.
        const float PanelX = 470f, PanelW = 640f, PanelHead = 54f, StatLineH = 42f, PanelGap = 16f;
        static float PanelH { get { return PanelHead + GrowStats.Length * StatLineH + 14f; } }

        class Row
        {
            public CharacterDefinition Member;
            public MemberProgress Before, After;
            public RectTransform Rect;
            public SlantBar Bar;
            public Text Level, Xp, Tag;
        }

        public static IEnumerator Play(RectTransform content, bool victory, BattleResult result, GameObject button)
        {
            UIFactory.ClearChildren(content);
            if (button != null) button.SetActive(false);

            // 1. Header
            var header = InkStyle.Title(content, "Header", UIText.Get(victory ? "result.victory" : "result.defeat"), 120,
                victory ? InkStyle.GoldBright : InkStyle.Crimson, TextAnchor.MiddleCenter);
            header.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            header.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 520f), new Vector2(1400f, 150f));
            foreach (float side in new[] { -1f, 1f })
            {
                var rule = InkStyle.Line(content, "Rule", Gold);
                rule.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(side * 560f, 520f), new Vector2(360f, 3f));
            }
            yield return SlideIn(header.rectTransform, new Vector2(0f, 60f), 0.35f);

            if (!victory || result == null || result.XpGained <= 0)
            {
                if (button != null) button.SetActive(true);
                yield break;
            }

            var xp = InkStyle.Title(content, "Xp", UIText.Get("result.xp", "n", result.XpGained.ToString()), 54, Up, TextAnchor.MiddleCenter);
            xp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(800f, 70f));
            yield return Tween.Punch(xp.rectTransform, 0.15f, 0.2f);

            // 2. Rows
            var rows = new List<Row>();
            float y = RowsTop;
            foreach (var entry in result.Party)
            {
                MemberProgress before;
                if (!result.Before.TryGetValue(entry.Key, out before)) continue;   // didn't take part
                var row = BuildRow(content, entry.Key, before, entry.Value, y);
                rows.Add(row);
                y -= RowH + RowGap;
            }
            foreach (var row in rows)
            {
                Host.StartCoroutine(SlideIn(row.Rect, new Vector2(-160f, 0f), 0.3f));
                yield return Tween.Wait(0.1f);
            }
            yield return Tween.Wait(0.3f);

            // 3. XP bars fill together
            var fills = new List<Coroutine>();
            foreach (var row in rows) fills.Add(Host.StartCoroutine(FillXp(row)));
            foreach (var f in fills) yield return f;
            yield return Tween.Wait(0.35f);

            // 4. Level up bursts and stat panels
            float panelY = RowsTop + 40f;   // panels stack down the right side
            foreach (var row in rows)
            {
                if (row.After.Level <= row.Before.Level) continue;
                yield return Burst(content, row.After.Level);
                yield return StatPanel(content, row, panelY);
                panelY -= PanelH + PanelGap;
            }

            if (button != null)
            {
                button.SetActive(true);
                button.transform.localScale = Vector3.one * 0.8f;
                yield return Tween.Scale(button.transform, Vector3.one, 0.25f, Tween.OutBack);
            }
        }

        // ------------------------------------------------------------------ Rows and XP

        static Row BuildRow(RectTransform content, CharacterDefinition member, MemberProgress before, MemberProgress after, float y)
        {
            var row = new Row { Member = member, Before = before, After = after };
            var plate = UIFactory.CreateImage(content, member.DisplayName, RowColor);
            row.Rect = plate.rectTransform;
            row.Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(RowsX, y), new Vector2(RowW, RowH));
            InkStyle.Frame(row.Rect, InkStyle.Rule, 2f);
            var swatch = UIFactory.CreateImage(row.Rect, "Color", member.color);
            swatch.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(8f, RowH - 24f));

            var name = InkStyle.Title(row.Rect, "Name", member.DisplayName, 44, InkStyle.Paper, TextAnchor.UpperLeft);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -10f), new Vector2(340f, 56f));

            row.Level = InkStyle.Title(row.Rect, "Lv", LvText(before.Level), 56, InkStyle.GoldBright, TextAnchor.UpperRight);
            row.Level.supportRichText = true;
            row.Level.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -8f), new Vector2(240f, 64f));

            // XP bar along the bottom, slanted like the HP bars.
            var track = UIFactory.CreateRect("XpBar", row.Rect).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 18f), new Vector2(RowW - 300f, 16f));
            SlantBar.Create(track, "Back", new Color(1f, 1f, 1f, 0.12f), 10f, 2f);
            row.Bar = SlantBar.Create(track, "Fill", new Color(0.45f, 0.75f, 1f), 10f);
            row.Bar.Fill = Fraction(before);
            row.Xp = UIFactory.CreateText(row.Rect, "XpText", XpText(before), 24, InkStyle.PaperDim, TextAnchor.LowerRight);
            row.Xp.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 10f), new Vector2(240f, 34f));

            row.Tag = InkStyle.Title(row.Rect, "LevelUp", UIText.Get("result.level_up"), 40, Up, TextAnchor.MiddleCenter);
            row.Tag.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(60f, -14f), new Vector2(300f, 52f));
            row.Tag.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            row.Tag.gameObject.SetActive(false);
            return row;
        }

        static string LvText(int level) { return "<size=" + UIFactory.Px(30) + ">" + UIText.Get("common.lv") + "</size> " + level; }

        static string XpText(MemberProgress p)
        {
            return p.Level >= Leveling.MaxLevel ? UIText.Get("common.max") : p.Xp + " / " + Leveling.XpToNext(p.Level) + " " + UIText.Get("common.xp");
        }

        static float Fraction(MemberProgress p)
        {
            return p.Level >= Leveling.MaxLevel ? 1f : Mathf.Clamp01((float)p.Xp / Leveling.XpToNext(p.Level));
        }

        /// <summary>Fills the bar level by level; each wrap is a level up on the row.</summary>
        static IEnumerator FillXp(Row row)
        {
            int level = row.Before.Level;
            float from = Fraction(row.Before);
            while (level < row.After.Level)
            {
                yield return Bar(row, from, 1f, 0.6f);
                level++;
                row.Level.text = LvText(level);
                row.Bar.Fill = 0f;
                from = 0f;
                LevelUpFlash(row);
            }
            yield return Bar(row, from, Fraction(row.After), 0.7f);
            row.Xp.text = XpText(row.After);
        }

        static IEnumerator Bar(Row row, float from, float to, float seconds)
        {
            if (to <= from) yield break;
            yield return Tween.Run(seconds * Mathf.Clamp01(to - from + 0.25f), p => { if (row.Bar != null) row.Bar.Fill = Mathf.Lerp(from, to, p); }, Tween.OutCubic);
        }

        static void LevelUpFlash(Row row)
        {
            row.Tag.gameObject.SetActive(true);
            Host.StartCoroutine(Tween.Punch(row.Tag.rectTransform, 0.3f, 0.3f));
            Host.StartCoroutine(Tween.Punch(row.Level.rectTransform, 0.3f, 0.25f));
        }

        // ------------------------------------------------------------------ Level up burst and stats

        /// <summary>A big gold diamond with "LEVEL UP" and the new level, scaling in over the screen, then away.</summary>
        static IEnumerator Burst(RectTransform content, int level)
        {
            var root = UIFactory.CreateRect("LevelUpBurst", content).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(560f, 560f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var outer = UIFactory.CreateImage(root, "Outer", new Color(0.12f, 0.09f, 0.05f, 0.9f));
            outer.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 380f));
            outer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            InkStyle.Frame(outer.rectTransform, InkStyle.GoldBright, 4f);
            InkStyle.Frame(outer.rectTransform, UIFactory.WithAlpha(InkStyle.Gold, 0.6f), 2f, 16f);
            var ring = UIFactory.CreateRect("Ring", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470f, 470f));
            ring.localRotation = Quaternion.Euler(0f, 0f, 45f);
            InkStyle.Frame(ring, UIFactory.WithAlpha(InkStyle.Gold, 0.45f), 2f);
            var title = InkStyle.Title(root, "Title", UIText.Get("result.level_up"), 70, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 90f));
            var number = InkStyle.Title(root, "Level", level.ToString(), 150, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            number.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            number.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(600f, 170f));
            FxLibrary.PlayAt(root, BattleFx.Buff, root.position, 9f);

            root.localScale = Vector3.one * 0.3f;
            Host.StartCoroutine(Tween.Run(1.5f, p => { if (ring != null) ring.localRotation = Quaternion.Euler(0f, 0f, 45f + p * 90f); }));
            yield return Tween.Scale(root, Vector3.one, 0.35f, Tween.OutBack);
            yield return Tween.Wait(0.8f);
            Host.StartCoroutine(Tween.Fade(group, 0f, 0.25f));
            yield return Tween.Scale(root, Vector3.one * 0.6f, 0.25f);
            Object.Destroy(root.gameObject);
        }

        static float StatAt(CharacterDefinition member, StatType stat, int level)
        {
            if (stat == StatType.MaxHP) return Leveling.MaxHP(member, level);
            return member.stats.Get(stat) * Leveling.StatScale(level, member.growthPerLevel);
        }

        /// <summary>The member's stat panel: one line per stat, "LABEL   old → new   +N ▲", the new value counting up.</summary>
        static IEnumerator StatPanel(RectTransform content, Row row, float y)
        {
            var member = row.Member;
            var panel = UIFactory.CreateImage(content, member.DisplayName + "Stats", new Color(0.06f, 0.07f, 0.075f, 0.94f));
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(PanelX, y), new Vector2(PanelW, PanelH));
            InkStyle.Frame(panel.rectTransform, UIFactory.WithAlpha(member.color, 0.9f), 2f);
            var strip = UIFactory.CreateImage(panel.rectTransform, "Head", UIFactory.WithAlpha(member.color, 0.18f));
            strip.rectTransform.TopStrip(0f, PanelHead, 0f);
            var swatch = UIFactory.CreateImage(panel.rectTransform, "Color", member.color);
            swatch.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(8f, PanelHead));
            var head = InkStyle.Title(panel.rectTransform, "Name", member.DisplayName, 38, InkStyle.Paper, TextAnchor.MiddleLeft);
            head.rectTransform.TopStrip(2f, PanelHead - 4f, 24f);
            var lv = InkStyle.Title(panel.rectTransform, "Lv", LvText(row.Before.Level) + "  →  " + row.After.Level, 38, InkStyle.GoldBright, TextAnchor.MiddleRight);
            lv.supportRichText = true;
            lv.rectTransform.TopStrip(2f, PanelHead - 4f, 22f);
            yield return SlideIn(panel.rectTransform, new Vector2(120f, 0f), 0.25f);

            var counts = new List<Coroutine>();
            var topLeft = new Vector2(0f, 1f);
            for (int i = 0; i < GrowStats.Length; i++)
            {
                float top = PanelHead + 6f + i * StatLineH;
                int oldV = Mathf.RoundToInt(StatAt(member, GrowStats[i], row.Before.Level));
                int newV = Mathf.RoundToInt(StatAt(member, GrowStats[i], row.After.Level));
                if (i > 0)
                {
                    var rule = InkStyle.Line(panel.rectTransform, "Rule", UIFactory.WithAlpha(InkStyle.Rule, 0.5f));
                    rule.rectTransform.Place(topLeft, topLeft, new Vector2(22f, -top), new Vector2(PanelW - 44f, 1f));
                }
                var label = UIFactory.CreateText(panel.rectTransform, GrowLabels[i], GrowLabels[i], 26, InkStyle.PaperDim, TextAnchor.MiddleLeft);
                label.rectTransform.Place(topLeft, topLeft, new Vector2(24f, -top), new Vector2(170f, StatLineH));
                var old = UIFactory.CreateText(panel.rectTransform, "Old", oldV.ToString(), 28, InkStyle.PaperDim, TextAnchor.MiddleRight);
                old.rectTransform.Place(topLeft, topLeft, new Vector2(196f, -top), new Vector2(110f, StatLineH));
                var arrow = UIFactory.CreateText(panel.rectTransform, "Arrow", "→", 28, InkStyle.PaperDim, TextAnchor.MiddleCenter);
                arrow.rectTransform.Place(topLeft, topLeft, new Vector2(310f, -top), new Vector2(44f, StatLineH));
                var value = InkStyle.Title(panel.rectTransform, "Value", oldV.ToString(), 34, InkStyle.Paper, TextAnchor.MiddleRight);
                value.rectTransform.Place(topLeft, topLeft, new Vector2(356f, -top), new Vector2(120f, StatLineH));
                var gain = UIFactory.CreateText(panel.rectTransform, "Gain", "", 28, Up, TextAnchor.MiddleLeft, FontStyle.Bold);
                gain.rectTransform.Place(topLeft, topLeft, new Vector2(492f, -top), new Vector2(130f, StatLineH));
                counts.Add(Host.StartCoroutine(CountUp(value, gain, oldV, newV, i * 0.12f)));
            }
            foreach (var c in counts) yield return c;
            yield return Tween.Wait(0.4f);
        }

        static IEnumerator CountUp(Text value, Text gain, int from, int to, float delay)
        {
            yield return Tween.Wait(delay);
            yield return Tween.Run(0.5f, p => { if (value != null) value.text = Mathf.RoundToInt(Mathf.Lerp(from, to, p)).ToString(); });
            if (to > from)
            {
                value.color = InkStyle.GoldBright;
                gain.text = "+" + (to - from) + " ▲";
                yield return Tween.Punch(gain.rectTransform, 0.3f, 0.2f);
            }
        }

        static IEnumerator SlideIn(RectTransform rt, Vector2 offset, float seconds)
        {
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            Vector2 end = rt.anchoredPosition;
            rt.anchoredPosition = end + offset;
            group.alpha = 0f;
            yield return Tween.Run(seconds, p =>
            {
                if (rt == null) return;
                rt.anchoredPosition = Vector2.Lerp(end + offset, end, p);
                group.alpha = p;
            }, Tween.OutCubic);
        }

        /// <summary>Runs the screen's side animations (set by HudView before playing).</summary>
        public static MonoBehaviour Host;
    }
}
