using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// One ability in the character sheet. Hover it (it lights up gold) and scroll the mouse wheel to flip
    /// between ranks I / II / III, or click a rank tab. Rituals and enemy skills show a single rank.
    /// </summary>
    public class AbilityCard : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color HoverColor = new Color(0.17f, 0.15f, 0.25f, 1f);
        static readonly Color TabOff = new Color(0.2f, 0.19f, 0.28f);
        static readonly Color TabOffText = new Color(0.85f, 0.85f, 0.92f);
        static readonly Color TabOnText = new Color(0.12f, 0.08f, 0.02f);

        CardDefinition card;
        Image background;
        Outline glow;
        Text description;
        readonly List<Image> tabs = new List<Image>();
        readonly List<Text> tabLabels = new List<Text>();
        int rank = 1, maxRank = 1;

        public static AbilityCard Create(RectTransform content, BattleUnit unit, CardDefinition card, int fixedRank)
        {
            bool ritual = card.isRitual;
            var accent = ritual ? CardView.RitualPastel(unit.Definition.color) : UnitView.KindColor(card.kind);
            var box = UnitInspectView.Box(content, accent);
            var ability = box.gameObject.AddComponent<AbilityCard>();
            ability.card = card;
            ability.background = box.GetComponent<Image>();
            ability.background.raycastTarget = true; // catch hover + wheel over the whole card
            ability.glow = box.gameObject.AddComponent<Outline>();
            ability.glow.effectColor = UnitInspectView.Gold;
            ability.glow.effectDistance = new Vector2(3f, -3f);
            ability.glow.enabled = false;
            ability.maxRank = fixedRank > 0 || ritual ? 1 : Mathf.Max(1, card.MaxRank);

            // Header row: name, then type and target.
            var header = UIFactory.CreateRect("Header", box);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            string tag = ritual ? "RITUAL" : UnitView.KindLabel(card.kind).ToUpper();
            var tagColor = ritual ? UnitInspectView.Gold : UnitView.KindColor(card.kind);
            var title = UIFactory.CreateText(header, "Title", card.DisplayName + "   <size=20><color=#" + ColorUtility.ToHtmlStringRGB(tagColor) + ">" + tag
                + "</color>  <color=#B8B8CC>" + UnitInspectView.Target(card.targetMode, unit.Team == Team.Enemy) + "</color></size>",
                28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.supportRichText = true;
            title.rectTransform.Fill();

            if (ability.maxRank > 1)
            {
                var row = UIFactory.CreateRect("Tabs", box);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 8f;
                rowLayout.childControlWidth = false;
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                for (int r = 1; r <= ability.maxRank; r++)
                {
                    int captured = r;
                    var tab = UIFactory.CreateButton(row, "Rank" + r, CardText.Roman(r), TabOff, () => ability.Select(captured), 24);
                    tab.GetComponent<RectTransform>().sizeDelta = new Vector2(76f, 44f);
                    tab.transition = Selectable.Transition.None;
                    ability.tabs.Add(tab.GetComponent<Image>());
                    ability.tabLabels.Add(tab.GetComponentInChildren<Text>());
                }
            }
            else if (ritual)
            {
                UnitInspectView.Line(box, "Appears at full Ult", 20, UnitInspectView.Gold, FontStyle.Italic);
            }

            ability.description = UnitInspectView.Line(box, "", 24, Color.white, FontStyle.Normal);
            ability.Select(fixedRank > 0 ? fixedRank : 1);
            return ability;
        }

        public void Select(int newRank)
        {
            rank = maxRank > 1 ? Mathf.Clamp(newRank, 1, maxRank) : Mathf.Max(1, newRank);
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i + 1 == rank;
                tabs[i].color = on ? UnitInspectView.Gold : TabOff;
                tabLabels[i].color = on ? TabOnText : TabOffText;
            }
            description.text = CardText.ForRank(card, rank);
        }

        /// <summary>Wheel down = next rank, wheel up = previous (wraps around).</summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (maxRank <= 1)
            {
                // Nothing to flip: pass the wheel on so the page still scrolls.
                var scroll = GetComponentInParent<ScrollRect>();
                if (scroll != null) scroll.OnScroll(eventData);
                return;
            }
            int step = eventData.scrollDelta.y < 0 ? 1 : -1;
            Select(((rank - 1 + step) % maxRank + maxRank) % maxRank + 1);
        }

        public void OnPointerEnter(PointerEventData eventData) { SetHover(true); }

        public void OnPointerExit(PointerEventData eventData) { SetHover(false); }

        void SetHover(bool on)
        {
            background.color = on ? HoverColor : UnitInspectView.CardColor;
            glow.enabled = on;
        }
    }
}
