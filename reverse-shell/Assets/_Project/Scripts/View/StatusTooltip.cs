using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Hover a status icon under a unit: a small ink box above it with the status's name (in its colour), what it does
    /// and how many turns (and stacks) are left. One box at a time, drawn on top of the battle canvas.
    /// </summary>
    public class StatusTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float BoxWidth = 640f;
        static RectTransform shown;
        static StatusTooltip owner;

        StatusSnapshot status;
        bool onCard;   // a card's chip: "Lasts N turns" rather than "N turns left"

        /// <param name="fromCard">The chip is on a card (what the card applies), not on a unit.</param>
        public static void Attach(GameObject chip, StatusSnapshot snapshot, bool fromCard = false)
        {
            var tip = chip.AddComponent<StatusTooltip>();
            tip.status = snapshot;
            tip.onCard = fromCard;
        }

        public void OnPointerEnter(PointerEventData eventData) { Show(); }

        public void OnPointerExit(PointerEventData eventData) { if (owner == this) Hide(); }

        void OnDisable() { if (owner == this) Hide(); }

        void Show()
        {
            Hide();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || status.Definition == null) return;
            var root = (RectTransform)canvas.rootCanvas.transform;
            var def = status.Definition;

            string turns = status.Remaining == 1 ? "1 turn" : status.Remaining + " turns";
            string level = StatusChip.Arrows(def, status.Stacks);
            if (level.Length > 0 && status.Stacks >= def.maxStacks) level += "  MAX";
            string body = string.IsNullOrEmpty(def.description) ? "" : def.description;

            var box = InkStyle.Panel(root, "StatusTooltip", InkStyle.InkPanel).rectTransform;
            InkStyle.Frame(box, UIFactory.WithAlpha(def.color, 0.8f), 2f);
            const float pad = 22f, nameH = 58f, turnsH = 44f;
            string arrowColor = ColorUtility.ToHtmlStringRGB(StatusChip.ArrowColor(def));
            string title = level.Length > 0 ? def.DisplayName + "  <color=#" + arrowColor + ">" + level + "</color>" : def.DisplayName;
            var name = UIFactory.CreateText(box, "Name", title, 36, def.color, TextAnchor.UpperLeft);
            name.supportRichText = true;
            name.rectTransform.TopStrip(pad, nameH, pad);
            var text = UIFactory.CreateText(box, "Text", body, 28, InkStyle.Paper, TextAnchor.UpperLeft);
            // Measure the wrapped text at the box width to size the box.
            var settings = text.GetGenerationSettings(new Vector2(BoxWidth - pad * 2f, 0f));
            float textH = text.cachedTextGeneratorForLayout.GetPreferredHeight(body, settings) / text.pixelsPerUnit;
            text.rectTransform.TopStrip(pad + nameH, textH + 4f, pad);
            var left = UIFactory.CreateText(box, "Turns", turns, 28, InkStyle.GoldBright, TextAnchor.UpperLeft);
            left.rectTransform.TopStrip(pad + nameH + textH + 10f, turnsH, pad);
            float height = pad * 2f + nameH + textH + 10f + turnsH;

            // Above the icon, kept on screen.
            box.pivot = new Vector2(0.5f, 0f);
            box.sizeDelta = new Vector2(BoxWidth, height);
            var self = (RectTransform)transform;
            Vector3 top = root.InverseTransformPoint(self.TransformPoint(new Vector3(0f, self.rect.yMax + 10f, 0f)));
            var half = root.rect.size / 2f;
            box.anchoredPosition = new Vector2(Mathf.Clamp(top.x, -half.x + BoxWidth / 2f + 8f, half.x - BoxWidth / 2f - 8f),
                                               Mathf.Min(top.y, half.y - height - 8f));
            box.SetAsLastSibling();
            foreach (var g in box.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            shown = box;
            owner = this;
        }

        static void Hide()
        {
            if (shown != null) Destroy(shown.gameObject);
            shown = null;
            owner = null;
        }
    }

    /// <summary>
    /// One status as an icon with the turns left in its corner ("2x3" = 2 stacks, 3 turns), or a short label plate when
    /// the status has no icon; hovering it shows the <see cref="StatusTooltip"/>. Used under units in battle and by the
    /// HP bar on the inspect (zoom-in) screen.
    /// </summary>
    public static class StatusChip
    {
        public static RectTransform Create(Transform parent, StatusSnapshot s, float size)
        {
            float k = size / 52f;
            var iconSprite = CardIcons.Status(s.Definition);
            var chip = UIFactory.CreateRect(s.Definition.DisplayName, parent);
            chip.sizeDelta = iconSprite != null ? new Vector2(size, size) : new Vector2(size * 1.7f, size * 0.8f);
            Graphic hit;
            if (iconSprite != null)
            {
                var icon = UIFactory.CreateImage(chip, "Icon", Color.white, true);
                icon.sprite = iconSprite;
                icon.rectTransform.Fill();
                hit = icon;
            }
            else
            {
                var plate = UIFactory.CreateImage(chip, "Plate", UIFactory.Darken(s.Definition.color, 0.35f), true);
                plate.rectTransform.Fill();
                UIFactory.CreateText(chip, "Label", s.Definition.shortLabel, Mathf.RoundToInt(20f * k), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
                hit = plate;
            }
            var turns = UIFactory.CreateText(chip, "Turns", s.Remaining.ToString(), Mathf.RoundToInt(22f * k), InkStyle.Paper, TextAnchor.LowerRight, FontStyle.Bold);
            turns.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            turns.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(6f, -6f) * k, new Vector2(60f, 28f) * k);
            AddArrows(chip, s.Definition, s.Stacks, k);
            StatusTooltip.Attach(hit.gameObject, s);
            return chip;
        }

        /// <summary>
        /// Persona-style level marks for stacking statuses: one arrow at level 1, two at max (green ▲ for buffs,
        /// red ▼ for debuffs). Empty for statuses that don't stack.
        /// </summary>
        public static string Arrows(StatusDefinition def, int stacks)
        {
            if (def == null || def.maxStacks <= 1 || stacks <= 0) return "";
            string a = def.isDebuff ? "\u25BC" : "\u25B2";
            return stacks >= def.maxStacks ? a + a : a;
        }

        public static Color ArrowColor(StatusDefinition def)
        {
            return def != null && def.isDebuff ? new Color(1f, 0.38f, 0.32f) : new Color(0.45f, 1f, 0.5f);
        }

        /// <summary>The level arrows in a chip's top-left corner (nothing for statuses that don't stack).</summary>
        public static void AddArrows(RectTransform chip, StatusDefinition def, int stacks, float k)
        {
            string arrows = Arrows(def, stacks);
            if (arrows.Length == 0) return;
            var t = UIFactory.CreateText(chip, "Level", arrows, Mathf.RoundToInt(20f * k), ArrowColor(def), TextAnchor.UpperLeft, FontStyle.Bold);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            t.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-6f, 6f) * k, new Vector2(60f, 28f) * k);
        }
    }
}
