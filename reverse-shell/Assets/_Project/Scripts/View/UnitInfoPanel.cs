using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Hover menu for a unit: name/class, HP, the enemy's intent, and every buff and debuff with
    /// stacks, rounds left and what it does. Auto-sizes to its content and stays on screen.
    /// </summary>
    public class UnitInfoPanel : MonoBehaviour
    {
        static readonly Color Panel = new Color(0.07f, 0.06f, 0.1f, 0.96f);
        static readonly Color Muted = new Color(0.7f, 0.7f, 0.78f);
        static readonly Color BuffColor = new Color(0.4f, 0.85f, 1f);
        static readonly Color DebuffColor = new Color(1f, 0.38f, 0.42f);
        const float PanelWidth = 340f;

        RectTransform rect, content;
        UnitView shownFor;

        public static UnitInfoPanel Create(Transform parent)
        {
            var bg = UIFactory.CreateImage(parent, "UnitInfo", Panel);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.8f, 0.3f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var panel = bg.gameObject.AddComponent<UnitInfoPanel>();
            panel.rect = bg.rectTransform;
            panel.rect.pivot = new Vector2(0f, 1f);
            panel.rect.anchorMin = panel.rect.anchorMax = new Vector2(0.5f, 0.5f);
            panel.rect.sizeDelta = new Vector2(PanelWidth, 100f);

            var layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            panel.content = panel.rect;
            bg.gameObject.SetActive(false);
            return panel;
        }

        public void Show(UnitView view)
        {
            shownFor = view;
            UIFactory.ClearChildren(content);
            var unit = view.Unit;
            var def = unit.Definition;
            var character = def as CharacterDefinition;

            string title = def.DisplayName;
            if (character != null && !string.IsNullOrEmpty(character.archetype)) title += "  <size=16><color=#B3B3C7>" + character.archetype + "</color></size>";
            Line(title, 22, Color.white, FontStyle.Bold);
            Line(def.afflatus + " · " + def.damageType + "     HP " + unit.HP + " / " + unit.MaxHP, 15, Muted, FontStyle.Normal);

            if (unit.Team == Team.Enemy && view.Intent != null)
            {
                Divider();
                Line("<color=#FFCC4D>INTENT</color>  " + view.Intent.Name + " " + UIFactory.Roman(view.Intent.Rank), 16, Color.white, FontStyle.Bold);
                Line(view.Intent.Describe(), 14, Muted, FontStyle.Normal);
            }

            Divider();
            if (view.Statuses == null || view.Statuses.Count == 0)
            {
                Line("No active buffs or debuffs.", 14, Muted, FontStyle.Italic);
            }
            else
            {
                foreach (var s in view.Statuses)
                {
                    var d = s.Definition;
                    var c = d.isDebuff ? DebuffColor : BuffColor;
                    string tag = d.isDebuff ? "DEBUFF" : "BUFF";
                    string stacks = s.Stacks > 1 ? "  x" + s.Stacks : "";
                    string rounds = s.Remaining == 1 ? "last turn" : s.Remaining + " turns left";
                    Line("<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + tag + "</color>  " + d.DisplayName + stacks
                         + "  <size=13><color=#B3B3C7>(" + rounds + ")</color></size>", 16, Color.white, FontStyle.Bold);
                    if (!string.IsNullOrEmpty(d.description)) Line(d.description, 14, Muted, FontStyle.Normal);
                }
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Position(view);
        }

        public void Hide(UnitView view)
        {
            if (view != null && view != shownFor) return;
            shownFor = null;
            gameObject.SetActive(false);
        }

        /// <summary>Beside the unit, toward the screen center, clamped inside the canvas.</summary>
        void Position(UnitView view)
        {
            var parent = (RectTransform)rect.parent;
            var unitRect = view.Rect;
            Vector3 local = parent.InverseTransformPoint(unitRect.position);
            float half = UnitView.Width / 2f + 12f;
            bool placeRight = view.Unit.Team == Team.Player;

            float x = placeRight ? local.x + half : local.x - half - rect.rect.width;
            float y = local.y + UnitView.Height / 2f - 20f;

            var bounds = parent.rect;
            x = Mathf.Clamp(x, bounds.xMin + 8f, bounds.xMax - rect.rect.width - 8f);
            y = Mathf.Clamp(y, bounds.yMin + rect.rect.height + 8f, bounds.yMax - 8f);
            rect.anchoredPosition = new Vector2(x, y);
        }

        void Line(string text, int size, Color color, FontStyle style)
        {
            var t = UIFactory.CreateText(content, "Line", text, size, color, TextAnchor.UpperLeft, style);
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void Divider()
        {
            var d = UIFactory.CreateImage(content, "Divider", new Color(1f, 1f, 1f, 0.12f));
            var le = d.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 2f;
            le.minHeight = 2f;
        }
    }
}
