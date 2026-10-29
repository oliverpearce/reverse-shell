using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Character sheet opened by long-pressing a unit, Reverse: 1999-style. The battlefield zooms in on the unit while
    /// the HUD and hand fade out; the unit stays on its own side (party left, enemies right) and the sheet fills the
    /// open side: a header tab with the element orb, the name and level, HP, stats as thin label / value strips in two
    /// columns, and the cards in a hairline bracket. No effects, Ult points or posters. Esc / clicking anywhere closes.
    /// </summary>
    public class UnitInspectView : MonoBehaviour
    {
        public static readonly Color CardColor = new Color(0.11f, 0.1f, 0.16f, 1f);
        static readonly Color TextColor = new Color(0.93f, 0.9f, 0.84f);           // paper
        public static readonly Color Muted = new Color(0.72f, 0.7f, 0.66f);
        public static readonly Color Gold = new Color(0.76f, 0.63f, 0.38f);        // InkStyle gold
        static readonly Color Good = new Color(0.45f, 1f, 0.6f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.45f);
        static readonly Color Strip = new Color(0.06f, 0.055f, 0.05f, 0.62f);
        static readonly Color Shade = new Color(0.03f, 0.028f, 0.025f, 0.86f);

        /// <summary>Zoom factor: the 6x battle sprites become exactly 9x, so pixels stay crisp.</summary>
        const float Zoom = 1.5f;
        /// <summary>How far from the centre the unit ends up (1920 x 1080 layout), on its own side.</summary>
        static readonly Vector2 Focus = new Vector2(470f, -40f);
        const float SheetW = 820f, SheetInner = 60f, SheetTop = 470f;
        const float OpenSeconds = 0.28f, CloseSeconds = 0.2f;

        Action onClosed;
        RectTransform sheet;
        CanvasGroup group;
        RectTransform[] zoomLayers;
        CanvasGroup[] hiddenUi;
        Vector2 zoomOffset;
        float sheetX, slide;
        bool closing;

        /// <param name="zoomLayers">Full-screen layers that zoom together (backdrop, stage).</param>
        /// <param name="hideUi">UI that fades out while inspecting (HUD, hand).</param>
        public static UnitInspectView Open(RectTransform root, UnitView unitView, RectTransform[] zoomLayers, CanvasGroup[] hideUi, Action onClosed)
        {
            var container = UIFactory.CreateRect("Inspect", root).Fill();
            var view = container.gameObject.AddComponent<UnitInspectView>();
            view.onClosed = onClosed;
            view.zoomLayers = zoomLayers ?? new RectTransform[0];
            view.hiddenUi = hideUi ?? new CanvasGroup[0];
            view.group = container.gameObject.AddComponent<CanvasGroup>();

            // Clicking anywhere closes (the sheet's cards still take scrolls; clicks fall through to here).
            var catcher = UIFactory.CreateImage(container, "CloseCatcher", new Color(0f, 0f, 0f, 0f), true);
            catcher.rectTransform.Fill();
            var close = catcher.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(view.Close);

            // The unit keeps its side of the screen; the zoom moves it there. The offset is clamped so the zoomed
            // layers always cover the screen (no out-of-bounds edges).
            Vector3 local = root.InverseTransformPoint(unitView.Center);
            bool unitOnLeft = local.x < 0f;
            var target = new Vector2(unitOnLeft ? -Focus.x : Focus.x, Focus.y);
            var offset = target - Zoom * new Vector2(local.x, local.y);
            var room = (Zoom - 1f) * root.rect.size / 2f;
            view.zoomOffset = new Vector2(Mathf.Clamp(offset.x, -room.x, room.x), Mathf.Clamp(offset.y, -room.y, room.y));

            view.Build(unitView, !unitOnLeft);
            view.StartCoroutine(view.Animate(0f, 1f, OpenSeconds));
            return view;
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            StartCoroutine(CloseRoutine());
        }

        IEnumerator CloseRoutine()
        {
            yield return Animate(1f, 0f, CloseSeconds);
            if (onClosed != null) onClosed();
            Destroy(gameObject);
        }

        /// <summary>Eases the zoom, the sheet and the hidden UI between closed (0) and open (1).</summary>
        IEnumerator Animate(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // works while paused
                Apply(Mathf.Lerp(from, to, Tween.OutCubic(Mathf.Clamp01(t / duration))));
                yield return null;
            }
            Apply(to);
        }

        void Apply(float p)
        {
            foreach (var layer in zoomLayers)
            {
                if (layer == null) continue;
                layer.localScale = Vector3.one * Mathf.Lerp(1f, Zoom, p);
                layer.anchoredPosition = zoomOffset * p;
            }
            foreach (var ui in hiddenUi)
                if (ui != null) ui.alpha = 1f - p;
            group.alpha = p;
            sheet.anchoredPosition = new Vector2(sheetX + (1f - p) * slide, 0f);
        }

        // ------------------------------------------------------------------ Layout

        void Build(UnitView unitView, bool sheetOnLeft)
        {
            var unit = unitView.Unit;
            var def = unit.Definition;
            var root = (RectTransform)transform;

            // A dark wash over the sheet's half, fading out toward the unit.
            var shade = HorizontalShade(root, sheetOnLeft ? Shade : Clear(Shade), sheetOnLeft ? Clear(Shade) : Shade);
            shade.rectTransform.anchorMin = new Vector2(sheetOnLeft ? 0f : 0.4f, 0f);
            shade.rectTransform.anchorMax = new Vector2(sheetOnLeft ? 0.6f : 1f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            sheet = UIFactory.CreateRect("Sheet", root);
            sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 0.5f);
            sheet.pivot = new Vector2(sheetOnLeft ? 1f : 0f, 0.5f);
            sheet.sizeDelta = new Vector2(SheetW, 1000f);
            sheetX = sheetOnLeft ? -SheetInner : SheetInner;
            slide = sheetOnLeft ? -60f : 60f;
            sheet.anchoredPosition = new Vector2(sheetX, 0f);

            float y = 500f - SheetTop;   // from the sheet's top edge (sheet is 1000 tall, centred)
            y = Header(sheet, unit, y);
            y = Health(sheet, unit, y + 18f);
            y = StatusRow(sheet, unitView, y + 12f);
            y = UltPoints(sheet, unit, unitView.MoxieCap, y + 14f);
            y = Stats(sheet, unit, y + 22f);
            Cards(sheet, unit, y + 40f);

            var hint = UIFactory.CreateText(root, "Hint", UIText.Get("inspect.hint"), 26, Muted, TextAnchor.MiddleCenter);
            hint.font = UIFactory.SerifFont;
            hint.raycastTarget = false;
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(sheetOnLeft ? -SheetInner - SheetW / 2f : SheetInner + SheetW / 2f, 34f), new Vector2(SheetW, 40f));
        }

        // ------------------------------------------------------------------ Sections

        /// <summary>The glowing element orb, the name in big serif, the level on the right, a hairline.</summary>
        static float Header(RectTransform sheet, BattleUnit unit, float y)
        {
            var def = unit.Definition;
            ElementBadge.Create(sheet, unit.Resonance, 60f, new Vector2(0f, 1f), new Vector2(40f, -y - 42f));

            var name = UIFactory.CreateText(sheet, "Name", def.DisplayName, 66, TextColor, TextAnchor.MiddleLeft).BestFit(36, 66);
            name.font = UIFactory.SerifFont;
            name.raycastTarget = false;
            name.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -y), new Vector2(SheetW - 300f, 84f));

            if (unit.Level > 0)
            {
                var lv = UIFactory.CreateText(sheet, "Level", UIText.Get("common.level") + "  " + UIFactory.Size(40) + unit.Level + "</size>", 26, Muted, TextAnchor.LowerRight);
                lv.font = UIFactory.SerifFont;
                lv.supportRichText = true;
                lv.raycastTarget = false;
                lv.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -y - 14f), new Vector2(200f, 60f));
            }

            y += 92f;
            var rule = UIFactory.CreateImage(sheet, "Rule", UIFactory.WithAlpha(Gold, 0.55f));
            rule.raycastTarget = false;
            rule.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -y), new Vector2(SheetW - 92f, 1f));
            return y + 1f;
        }

        /// <summary>HP: the label left and the numbers right on one line, over a thin bar.</summary>
        static float Health(RectTransform sheet, BattleUnit unit, float y)
        {
            float x = 92f, w = SheetW - 92f;
            var label = UIFactory.CreateText(sheet, "HP", UIText.Get("stat.hp"), 44, TextColor, TextAnchor.MiddleLeft);
            label.font = UIFactory.SerifFont;
            label.raycastTarget = false;
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(200f, 54f));
            var value = UIFactory.CreateText(sheet, "HPValue", unit.HP + " / " + unit.MaxHP, 42, TextColor, TextAnchor.MiddleRight);
            value.font = UIFactory.SerifFont;
            value.raycastTarget = false;
            value.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + w - 400f, -y), new Vector2(400f, 54f));
            y += 58f;
            // Slanted like the battle bars (Reverse: 1999 style).
            var bar = UIFactory.CreateRect("HPBar", sheet).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, 10f));
            SlantBar.Create(bar, "Back", new Color(1f, 1f, 1f, 0.12f), 8f);
            SlantBar.Create(bar, "Fill", unit.Team == Team.Player ? new Color(0.45f, 0.82f, 0.55f) : new Color(0.88f, 0.38f, 0.36f), 8f).Fill = Mathf.Clamp01(unit.HPPercent);
            return y + 10f;
        }

        /// <summary>The unit's statuses right under the HP bar, as the same icons as in battle (hover for details).</summary>
        static float StatusRow(RectTransform sheet, UnitView unitView, float y)
        {
            var statuses = unitView.Statuses;
            if (statuses == null || statuses.Count == 0) return y;
            const float size = 92f;
            var row = UIFactory.CreateRect("Statuses", sheet).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -y), new Vector2(SheetW - 92f, size));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            foreach (var s in statuses) StatusChip.Create(row, s, size);
            return y + size + 6f;
        }

        /// <summary>"ULT" and a row of Ult point pips (lit = earned), for units that have an Ult card.</summary>
        static float UltPoints(RectTransform sheet, BattleUnit unit, int cap, float y)
        {
            bool hasUlt = unit.Ultimate != null;
            if (!hasUlt || cap <= 0) return y;
            float x = 92f;
            var label = UIFactory.CreateText(sheet, "Ult", UIText.Get("inspect.ult"), 28, Muted, TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(90f, 36f));
            const float pip = 34f, gap = 8f;
            bool full = unit.Moxie >= cap;
            for (int i = 0; i < cap; i++)
            {
                bool on = i < unit.Moxie;
                var img = UIFactory.CreateImage(sheet, "UltPip" + i, Color.white);
                img.raycastTarget = false;
                img.sprite = on && CardIcons.PipFull != null ? CardIcons.PipFull : CardIcons.PipEmpty;
                img.color = on ? (full ? new Color(1f, 0.55f, 0.2f) : Gold) : new Color(0.42f, 0.4f, 0.46f);
                img.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 90f + i * (pip + gap), -y - 3f), new Vector2(pip, pip));
            }
            if (full)
            {
                var ready = UIFactory.CreateText(sheet, "UltReady", UIText.Get("inspect.ready"), 24, new Color(1f, 0.55f, 0.2f), TextAnchor.MiddleLeft);
                ready.raycastTarget = false;
                ready.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 100f + cap * (pip + gap), -y), new Vector2(160f, 30f));
            }
            return y + 44f;
        }

        /// <summary>Six stats as label / value strips in two columns (a +/-% tag when something changes one).</summary>
        static float Stats(RectTransform sheet, BattleUnit unit, float y)
        {
            var s = unit.Definition.stats;
            float spell = unit.GetStat(StatType.DamageBonus) + unit.GetStat(StatType.IncantationMight);
            var rows = new[]
            {
                Stat(UIText.Get("stat.atk"), unit.GetStat(StatType.Attack), s.attack, false), Stat(UIText.Get("stat.phys_def"), unit.GetStat(StatType.PhysicalDefense), s.physicalDefense, false),
                Stat(UIText.Get("stat.crit"), unit.GetStat(StatType.CritRate), s.critRate, true), Stat(UIText.Get("stat.spec_def"), unit.GetStat(StatType.SpecialDefense), s.specialDefense, false),
                Stat(UIText.Get("stat.crit_dmg"), unit.GetStat(StatType.CritDamage), s.critDamage, true), Stat(UIText.Get("stat.spell_dmg"), spell, 0f, true, true),
            };
            float x0 = 92f, gap = 12f, cw = (SheetW - 92f - gap) / 2f, ch = 54f;
            for (int i = 0; i < rows.Length; i++)
            {
                int col = i % 2, row = i / 2;
                var strip = UIFactory.CreateImage(sheet, rows[i].Label, Strip);
                strip.raycastTarget = false;
                strip.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0 + col * (cw + gap), -(y + row * (ch + 8f))), new Vector2(cw, ch));
                var label = UIFactory.CreateText(strip.transform, "Label", rows[i].Label, 30, Muted, TextAnchor.MiddleLeft);
                label.font = UIFactory.SerifFont;
                label.rectTransform.Fill();
                label.rectTransform.offsetMin = new Vector2(16f, 0f);
                var value = UIFactory.CreateText(strip.transform, "Value", rows[i].Value, 36, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
                value.supportRichText = true;
                value.rectTransform.Fill();
                value.rectTransform.offsetMax = new Vector2(-16f, 0f);
            }
            return y + 3 * (ch + 8f);
        }

        struct StatRow { public string Label, Value; }

        static StatRow Stat(string label, float current, float baseValue, bool percent, bool isBonus = false)
        {
            string value = isBonus
                ? "+" + Mathf.RoundToInt(current * 100f) + "%"
                : percent ? Mathf.RoundToInt(current * 100f) + "%" : Mathf.RoundToInt(current).ToString();
            float diff = isBonus ? 0f : (baseValue != 0f ? current / baseValue - 1f : 0f);
            if (Mathf.Abs(diff) >= 0.005f)
            {
                string c = ColorUtility.ToHtmlStringRGB(diff > 0 ? Good : Bad);
                value = UIFactory.Size(22) + "<color=#" + c + ">" + (diff > 0 ? "+" : "") + Mathf.RoundToInt(diff * 100f) + "%</color></size>  " + value;
            }
            return new StatRow { Label = label, Value = value };
        }

        const float CardScale = 0.8f;    // the pixel card at 4x (four fit across the sheet); hold one for the big view

        /// <summary>The cards (and the Ult card) in a hairline bracket across the whole sheet; hold one to open it.</summary>
        static void Cards(RectTransform sheet, BattleUnit unit, float y)
        {
            float x = 0f, h = CardView.Size.y * CardScale + 20f;
            var bracket = UIFactory.CreateRect("CardBracket", sheet).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(SheetW - x, h));
            InkStyle.Frame(bracket, UIFactory.WithAlpha(Gold, 0.4f), 1f);

            var row = UIFactory.CreateRect("Cards", bracket).Fill();
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Icon-only cards; hold one to open it (full text, scroll for upgrades).
            // The battle canvas itself (Transform.root would be the bootstrap object above it, outside any canvas,
            // so the popup was built but never drawn).
            var canvas = (RectTransform)sheet.GetComponentInParent<Canvas>().rootCanvas.transform;
            if (unit.Character != null)
            {
                bool first = true;
                foreach (var card in unit.Incantations)
                {
                    if (card == null) continue;
                    var innate = IconCard(row, canvas, unit, card, 1, first ? unit.Ultimate : null);
                    if (first) SoulFormPreview.Attach(innate.gameObject, unit, unit.Ultimate, CardScale * 1.4f);   // the Main card
                    first = false;
                }
                if (unit.Ultimate != null) IconCard(row, canvas, unit, unit.Ultimate, 1);
            }
            else if (unit.Enemy != null)
            {
                if (unit.Enemy.innate != null)
                    SoulFormPreview.Attach(IconCard(row, canvas, unit, unit.Enemy.innate, 1, unit.Enemy.ritual).gameObject, unit, unit.Enemy.ritual, CardScale * 1.4f);
                foreach (var skill in unit.Enemy.skills)
                    if (skill.card != null) IconCard(row, canvas, unit, skill.card, Mathf.Max(1, skill.rank));
                if (unit.Enemy.ritual != null) IconCard(row, canvas, unit, unit.Enemy.ritual, 1);
            }
        }

        static RectTransform IconCard(RectTransform row, RectTransform canvas, BattleUnit unit, CardDefinition card, int rank,
            CardDefinition soulForm = null)
        {
            var ability = AbilityCard.Create(row, unit, card, rank, CardScale, true);
            var rt = (RectTransform)ability.transform;
            // Click or hold: the same card popup as the party menu (flip, full text, live numbers with this unit's
            // current stats, scroll for the other ranks).
            Action open = () => { if (!CardDetailPopup.IsOpen) CardDetailPopup.Open(canvas, unit, card, rank, rt, null, soulForm); };
            ability.OnLongPress = open;
            ability.OnClick = open;
            return rt;
        }

        // ------------------------------------------------------------------ Building blocks

        static Color Clear(Color c)
        {
            c.a = 0f;
            return c;
        }

        /// <summary>A left-to-right gradient (left colour to right colour).</summary>
        static RawImage HorizontalShade(RectTransform parent, Color left, Color right)
        {
            var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 64; x++) tex.SetPixel(x, 0, Color.Lerp(left, right, x / 63f));
            tex.Apply();
            var img = new GameObject("Shade", typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(parent, false);
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        public static RectTransform Box(RectTransform content, Color accent)
        {
            var box = UIFactory.CreateImage(content, "Box", CardColor);
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 18, 12, 14);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var bar = UIFactory.CreateImage(box.transform, "Accent", accent);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = new Vector2(8f, 0f);
            return box.rectTransform;
        }

        public static Text Line(RectTransform parent, string text, int size, Color color, FontStyle style)
        {
            var t = UIFactory.CreateText(parent, "Line", text, size, color, TextAnchor.UpperLeft, style);
            t.supportRichText = true;
            t.lineSpacing = 1.1f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static string Target(TargetMode mode, bool enemyPerspective)
        {
            switch (mode)
            {
                case TargetMode.SingleEnemy: return enemyPerspective ? "1 of your party" : "1 enemy";
                case TargetMode.AllEnemies: return enemyPerspective ? "your whole party" : "all enemies";
                case TargetMode.RandomEnemy: return "random enemy";
                case TargetMode.Self: return "self";
                case TargetMode.SingleAlly: return "weakest ally";
                case TargetMode.AllAllies: return "all allies";
                default: return "random ally";
            }
        }
    }
}
