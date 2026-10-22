using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Character sheet opened by long-pressing a unit. Kept simple and number-first: big auto-fitting
    /// numbers for HP and stats, and abilities you can flip between ranks by scrolling over them.
    /// Esc / the X / click outside closes.
    /// </summary>
    public class UnitInspectView : MonoBehaviour
    {
        static readonly Color Dim = new Color(0.01f, 0.01f, 0.03f, 0.88f);
        static readonly Color PanelColor = new Color(0.06f, 0.055f, 0.09f, 0.99f);
        public static readonly Color CardColor = new Color(0.11f, 0.1f, 0.16f, 1f);
        static readonly Color TextColor = new Color(0.96f, 0.96f, 0.98f);
        public static readonly Color Muted = new Color(0.72f, 0.72f, 0.8f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        static readonly Color Good = new Color(0.45f, 1f, 0.6f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.45f);

        const float PanelW = 1440f, PanelH = 860f, Pad = 32f, LeftW = 400f;

        Action onClosed;
        RectTransform panel;
        CanvasGroup group;
        bool closing;

        public static UnitInspectView Open(RectTransform root, UnitView unitView, Action onClosed)
        {
            // The dim backdrop is a sibling of the panel (not its parent) so clicks on the panel
            // don't bubble up to the backdrop's close button.
            var container = UIFactory.CreateRect("Inspect", root).Fill();
            var view = container.gameObject.AddComponent<UnitInspectView>();
            view.onClosed = onClosed;
            view.group = container.gameObject.AddComponent<CanvasGroup>();
            var dim = UIFactory.CreateImage(container, "Dim", Dim, true);
            dim.rectTransform.Fill();
            var closeOnDim = dim.gameObject.AddComponent<Button>();
            closeOnDim.transition = Selectable.Transition.None;
            closeOnDim.onClick.AddListener(view.Close);
            view.Build(root, unitView);
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
            yield return Animate(1f, 0f, 0.12f, 1f, 0.92f);
            if (onClosed != null) onClosed();
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ Layout

        void Build(RectTransform root, UnitView unitView)
        {
            var def = unitView.Unit.Definition;
            var panelImg = UIFactory.CreateImage(transform, "Panel", PanelColor, true);
            panel = panelImg.rectTransform;
            panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));
            var outline = panelImg.gameObject.AddComponent<Outline>();
            outline.effectColor = UIFactory.WithAlpha(def.color, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            BuildLeft(unitView);
            BuildRight(unitView);

            var close = UIFactory.CreateButton(panel, "Close", "X", new Color(0.3f, 0.28f, 0.38f), Close, 28);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(46f, 46f));

            Vector3 local = root.InverseTransformPoint(unitView.Center);
            StartCoroutine(Animate(0f, 1f, 0.2f, 0.25f, 1f, new Vector2(local.x, local.y)));
        }

        void BuildLeft(UnitView unitView)
        {
            var unit = unitView.Unit;
            var def = unit.Definition;
            var character = def as CharacterDefinition;
            var left = UIFactory.CreateRect("Left", panel);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 1f);
            left.offsetMin = new Vector2(Pad, Pad);
            left.offsetMax = new Vector2(Pad + LeftW, -Pad);

            // Psycho-Pass-style holographic profile: name, ID, stamp and threat index live on the poster.
            var stage = UIFactory.CreateRect("Profile", left);
            stage.TopStrip(0f, 400f);
            var poster = ProfilePoster.Build(stage, new PosterInfo
            {
                Name = def.DisplayName,
                Hostile = unit.Team == Team.Enemy,
                Index = ThreatIndex(unit),
                Accent = def.color,
            }, false);
            if (def.animations != null && def.animations.Has(UISpriteAnimator.Idle))
            {
                poster.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -100f), new Vector2(320f, 320f)); // 64px at 5x: head and torso
                poster.gameObject.AddComponent<UISpriteAnimator>().Init(def.animations, Facing.Down, poster);
            }
            else
            {
                poster.enabled = false;
                string letter = def.DisplayName.Length > 0 ? def.DisplayName.Substring(0, 1) : "?";
                UIFactory.CreateText(poster.transform.parent, "Initial", letter, 160, UIFactory.WithAlpha(ProfilePoster.Holo, 0.7f), TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
            }

            float y = 416f;
            string cls = character != null && !string.IsNullOrEmpty(character.archetype) ? character.archetype : (unit.Team == Team.Enemy ? "Enemy" : "Ally");
            Fit(left, cls + "  ·  " + def.afflatus + "  ·  " + def.damageType, Muted, FontStyle.Bold, ref y, 30f, 14, 22);

            y += 12f;
            Fit(left, "HP", Muted, FontStyle.Bold, ref y, 24f, 14, 20);
            Fit(left, unit.HP + " / " + unit.MaxHP, TextColor, FontStyle.Bold, ref y, 86f, 30, 84); // as big as fits
            var barBg = UIFactory.CreateImage(left, "HPBar", new Color(0.16f, 0.15f, 0.22f));
            barBg.rectTransform.TopStrip(y + 4f, 22f);
            var fill = UIFactory.CreateImage(barBg.transform, "Fill", unit.Team == Team.Player ? new Color(0.35f, 0.85f, 0.5f) : new Color(0.9f, 0.35f, 0.35f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(unit.HPPercent), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            y += 44f;

            if (unit.Team != Team.Player) return;
            int cap = Mathf.Max(1, unitView.MoxieCap);
            bool ready = unit.Moxie >= cap;
            Fit(left, ready ? "ULT  ·  READY" : "ULT", ready ? Gold : Muted, FontStyle.Bold, ref y, 24f, 14, 20);
            var row = UIFactory.CreateRect("Moxie", left);
            row.TopStrip(y + 4f, 28f);
            float pipW = (LeftW - (cap - 1) * 8f) / cap;
            for (int i = 0; i < cap; i++)
            {
                var pip = UIFactory.CreateImage(row, "Pip", i < unit.Moxie ? Gold : new Color(0.2f, 0.19f, 0.27f));
                pip.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (pipW + 8f), 0f), new Vector2(pipW, 28f));
            }
        }

        /// <summary>The poster's big number: a rough read of how dangerous the unit is (ATK and toughness).</summary>
        static int ThreatIndex(BattleUnit unit)
        {
            return Mathf.RoundToInt(unit.GetStat(StatType.Attack) * 0.5f + unit.MaxHP * 0.02f);
        }

        void BuildRight(UnitView unitView)
        {
            var unit = unitView.Unit;
            var right = UIFactory.CreateRect("Right", panel);
            right.anchorMin = Vector2.zero;
            right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(Pad + LeftW + Pad, Pad);
            right.offsetMax = new Vector2(-Pad, -Pad);

            var scroll = right.gameObject.AddComponent<ScrollRect>();
            var viewport = UIFactory.CreateRect("Viewport", right).Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(4, 16, 4, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            if (unit.Team == Team.Enemy && unitView.Intent != null)
            {
                Header(content, "NEXT MOVE");
                var box = Box(content, UnitView.KindColor(unitView.Intent.Definition.kind));
                Line(box, unitView.Intent.Name + " " + CardText.Roman(unitView.Intent.Rank), 28, TextColor, FontStyle.Bold);
                Line(box, UIFactory.EmphasizeNumbers(unitView.Intent.Describe(), 30, Gold), 22, Muted, FontStyle.Normal).font = UIFactory.BodyFont;
            }

            if (unitView.Statuses != null && unitView.Statuses.Count > 0)
            {
                Header(content, "EFFECTS");
                foreach (var s in unitView.Statuses)
                {
                    var d = s.Definition;
                    var box = Box(content, d.isDebuff ? Bad : Good);
                    string stacks = s.Stacks > 1 ? "  x" + s.Stacks : "";
                    string turns = s.Remaining == 1 ? "last turn" : s.Remaining + " turns";
                    Line(box, d.DisplayName + stacks + "  <size=20><color=#B8B8C8>" + turns + "</color></size>", 26, TextColor, FontStyle.Bold);
                    if (!string.IsNullOrEmpty(d.description)) Line(box, UIFactory.EmphasizeNumbers(d.description, 27, Gold), 20, Muted, FontStyle.Normal).font = UIFactory.BodyFont;
                }
            }

            Header(content, "STATS");
            Stats(content, unit);

            Header(content, unit.Team == Team.Player ? "ABILITIES" : "SKILLS");
            Abilities(content, unit);
        }

        // ------------------------------------------------------------------ Sections

        void Stats(RectTransform content, BattleUnit unit)
        {
            var gridGo = UIFactory.CreateRect("StatGrid", content);
            var grid = gridGo.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(294f, 112f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            gridGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 112f * 2 + 12f;

            var s = unit.Definition.stats;
            StatBox(gridGo, "ATK", unit.GetStat(StatType.Attack), s.attack, false);
            StatBox(gridGo, "PHYS DEF", unit.GetStat(StatType.PhysicalDefense), s.physicalDefense, false);
            StatBox(gridGo, "SPEC DEF", unit.GetStat(StatType.SpecialDefense), s.specialDefense, false);
            StatBox(gridGo, "CRIT", unit.GetStat(StatType.CritRate), s.critRate, true);
            StatBox(gridGo, "CRIT DMG", unit.GetStat(StatType.CritDamage), s.critDamage, true);
            float spell = unit.GetStat(StatType.DamageBonus) + unit.GetStat(StatType.IncantationMight);
            StatBox(gridGo, "SPELL DMG", spell, 0f, true, true);
        }

        /// <summary>Label on top, number filling the rest of the box, "+15%" badge only when effects change it.</summary>
        static void StatBox(RectTransform grid, string label, float current, float baseValue, bool percent, bool isBonus = false)
        {
            var box = UIFactory.CreateImage(grid, label, CardColor);
            var name = UIFactory.CreateText(box.transform, "Label", label, 18, Muted, TextAnchor.UpperLeft, FontStyle.Bold);
            name.rectTransform.TopStrip(10f, 24f, 14f);

            string value = isBonus
                ? "+" + Mathf.RoundToInt(current * 100f) + "%"
                : percent ? Mathf.RoundToInt(current * 100f) + "%" : Mathf.RoundToInt(current).ToString();
            var val = UIFactory.CreateText(box.transform, "Value", value, 72, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold).BestFit(24, 72);
            var vrt = val.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(14f, 6f);
            vrt.offsetMax = new Vector2(-14f, -34f);

            float diff = isBonus ? current : (baseValue != 0f ? current / baseValue - 1f : 0f);
            if (Mathf.Abs(diff) < 0.005f || isBonus) return;
            var badge = UIFactory.CreateText(box.transform, "Badge", (diff > 0 ? "+" : "") + Mathf.RoundToInt(diff * 100f) + "%", 20,
                diff > 0 ? Good : Bad, TextAnchor.UpperRight, FontStyle.Bold);
            badge.rectTransform.TopStrip(10f, 24f, 14f);
        }

        /// <summary>The unit's cards, side by side as real card faces (scroll over one to flip its rank).</summary>
        void Abilities(RectTransform content, BattleUnit unit)
        {
            var row = UIFactory.CreateRect("Cards", content);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.padding = new RectOffset(30, 30, 30, 30); // room for the diamond glow
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = AbilityCard.FaceSize.y + 60f;

            if (unit.Character != null)
            {
                foreach (var card in unit.Character.incantations)
                    if (card != null) AbilityCard.Create(row, unit, card, 0);
                if (unit.Character.ritual != null) AbilityCard.Create(row, unit, unit.Character.ritual, 0);
            }
            else if (unit.Enemy != null)
            {
                foreach (var skill in unit.Enemy.skills)
                    if (skill.card != null) AbilityCard.Create(row, unit, skill.card, skill.rank);
            }
        }

        // ------------------------------------------------------------------ Building blocks

        static void Header(RectTransform content, string text)
        {
            var t = UIFactory.CreateText(content, "Header", text, 40, Gold, TextAnchor.LowerLeft);
            t.font = UIFactory.DisplayFont;
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            t.gameObject.AddComponent<LayoutElement>().minHeight = 50f;
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

        /// <summary>A single line that shrinks its font to fit the column width.</summary>
        static void Fit(RectTransform parent, string text, Color color, FontStyle style, ref float y, float height, int min, int max)
        {
            var t = UIFactory.CreateText(parent, "Fit", text, max, color, TextAnchor.MiddleLeft, style).BestFit(min, max);
            t.rectTransform.TopStrip(y, height);
            y += height;
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

        IEnumerator Animate(float fromAlpha, float toAlpha, float duration, float fromScale, float toScale, Vector2? fromPos = null)
        {
            Vector2 start = fromPos ?? Vector2.zero;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // works while paused
                float p = Tween.OutCubic(Mathf.Clamp01(t / duration));
                group.alpha = Mathf.Lerp(fromAlpha, toAlpha, p);
                panel.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, p);
                if (fromPos.HasValue) panel.anchoredPosition = Vector2.Lerp(start, Vector2.zero, p);
                yield return null;
            }
            group.alpha = toAlpha;
            panel.localScale = Vector3.one * toScale;
        }
    }
}
