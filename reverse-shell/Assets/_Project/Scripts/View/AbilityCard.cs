using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// One ability in the character sheet, shown as the real card (same face as in the hand, at a larger size).
    /// Hover it and scroll the mouse wheel to flip between ranks I / II / III: the border and the text update.
    /// Rituals and single-rank skills pass the wheel on so the page still scrolls.
    /// </summary>
    public class AbilityCard : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float Scale = 1.6f;
        public static Vector2 FaceSize { get { return CardView.Size * Scale; } }

        BattleUnit unit;
        CardDefinition card;
        float scale = Scale;
        RectTransform face;
        int rank = 1, maxRank = 1;
        float hover, hoverTarget;

        public static AbilityCard Create(RectTransform row, BattleUnit unit, CardDefinition card, int fixedRank)
        {
            return Create(row, unit, card, fixedRank, Scale);
        }

        /// <summary>The same, at another size (the crew menu shows a whole row of smaller cards).</summary>
        public static AbilityCard Create(RectTransform row, BattleUnit unit, CardDefinition card, int fixedRank, float scale)
        {
            var holder = UIFactory.CreateRect(card.DisplayName, row);
            var element = holder.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = CardView.Size.x * scale;
            element.preferredHeight = CardView.Size.y * scale;
            var hit = holder.gameObject.AddComponent<Image>(); // catches hover + wheel over the whole card
            hit.color = new Color(0f, 0f, 0f, 0f);

            var ability = holder.gameObject.AddComponent<AbilityCard>();
            ability.unit = unit;
            ability.card = card;
            ability.scale = scale;
            ability.maxRank = card.isRitual ? 1 : Mathf.Max(1, card.MaxRank);
            ability.Select(fixedRank > 0 ? fixedRank : 1);
            return ability;
        }

        public void Select(int newRank)
        {
            rank = Mathf.Clamp(newRank, 1, maxRank);
            if (face != null) Destroy(face.gameObject);
            face = UIFactory.CreateRect("Face", transform);
            face.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardView.Size * scale);
            CardView.BuildFace(face, new CardInstance(card, unit, rank), scale);
            foreach (var g in face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false; // the holder handles input
        }

        /// <summary>Wheel down = next rank, wheel up = previous (wraps around).</summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (maxRank <= 1)
            {
                var scroll = GetComponentInParent<ScrollRect>();
                if (scroll != null) scroll.OnScroll(eventData);
                return;
            }
            int step = eventData.scrollDelta.y < 0 ? 1 : -1;
            Select(((rank - 1 + step) % maxRank + maxRank) % maxRank + 1);
            hover = 0.6f; // small pop so the flip reads
        }

        public void OnPointerEnter(PointerEventData eventData) { hoverTarget = 1f; }

        public void OnPointerExit(PointerEventData eventData) { hoverTarget = 0f; }

        /// <summary>The hovered card lifts slightly: it's the one the wheel will flip.</summary>
        void Update()
        {
            hover = Mathf.MoveTowards(hover, hoverTarget, Time.unscaledDeltaTime * 8f);
            if (face != null) face.localScale = Vector3.one * (1f + 0.04f * hover);
        }
    }
}
