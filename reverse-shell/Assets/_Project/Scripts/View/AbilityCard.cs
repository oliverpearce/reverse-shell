using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// One ability shown as the real card (same face as in the hand, at a larger size).
    /// Detailed cards (the card popup): hover and scroll the wheel to step between ranks I / II / III (it stops at the
    /// first and last rank rather than wrapping). Icon-only cards (the party menu, the inspect sheet) show no text and
    /// don't change on the wheel; holding one can open it (<see cref="OnLongPress"/>).
    /// </summary>
    public class AbilityCard : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public const float Scale = 1.6f;
        public static Vector2 FaceSize { get { return CardView.Size * Scale; } }
        const float LongPressSeconds = 0.45f;

        BattleUnit unit;
        CardDefinition card;
        float scale = Scale;
        bool iconOnly;
        RectTransform face;
        int rank = 1, maxRank = 1;
        float hover, hoverTarget;
        // Hold-to-open, polled in Update (like the hand cards) so a small wobble or leaving the card edge doesn't cancel it.
        bool pressing;
        float pressTime;
        Vector2 pressPosition;

        /// <summary>Called when the card is held (icon-only cards in the inspect sheet open the card popup).</summary>
        public Action OnLongPress;
        /// <summary>Called on a plain click (the battle info screen opens the same popup as the party menu does on click).</summary>
        public Action OnClick;
        bool openedByHold;   // the hold already opened it: the release isn't also a click

        public CardDefinition Card { get { return card; } }
        public int Rank { get { return rank; } }

        public static AbilityCard Create(RectTransform row, BattleUnit unit, CardDefinition card, int fixedRank)
        {
            return Create(row, unit, card, fixedRank, Scale);
        }

        /// <summary>The same, at another size; <paramref name="iconOnly"/> shows no text and ignores the wheel.</summary>
        public static AbilityCard Create(RectTransform row, BattleUnit unit, CardDefinition card, int fixedRank, float scale, bool iconOnly = false)
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
            ability.iconOnly = iconOnly;
            ability.maxRank = card.isRitual || iconOnly ? 1 : Mathf.Max(1, card.MaxRank);
            ability.rank = 1;
            ability.Select(fixedRank > 0 ? fixedRank : 1, true);
            return ability;
        }

        public void Select(int newRank)
        {
            Select(newRank, false);
        }

        void Select(int newRank, bool force)
        {
            int clamped = iconOnly ? Mathf.Max(1, newRank) : Mathf.Clamp(newRank, 1, maxRank);
            if (!force && clamped == rank && face != null) return;
            rank = clamped;
            if (face != null) Destroy(face.gameObject);
            face = UIFactory.CreateRect("Face", transform);
            face.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardView.Size * scale);
            CardView.BuildFace(face, new CardInstance(card, unit, rank), scale, iconOnly);
            foreach (var g in face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false; // the holder handles input
            foreach (var tip in face.GetComponentsInChildren<StatusTooltip>())                  // except status chips (hover)
            {
                var g = tip.GetComponent<Graphic>();
                if (g != null) g.raycastTarget = true;
            }
        }

        /// <summary>Wheel down = next rank, wheel up = previous; it stops at the first / last rank.</summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (maxRank <= 1)
            {
                var scroll = GetComponentInParent<ScrollRect>();
                if (scroll != null) scroll.OnScroll(eventData);
                return;
            }
            int step = eventData.scrollDelta.y < 0 ? 1 : -1;
            int next = Mathf.Clamp(rank + step, 1, maxRank);
            if (next == rank) return;
            Select(next);
            hover = 0.6f; // small pop so the flip reads
        }

        public void OnPointerEnter(PointerEventData eventData) { if (iconOnly) hoverTarget = 1f; }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverTarget = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (OnLongPress == null || eventData.button != PointerEventData.InputButton.Left) return;
            openedByHold = false;
            pressing = true;
            pressTime = 0f;
            pressPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressing = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (openedByHold)
            {
                openedByHold = false;
                return;
            }
            if (OnClick != null) OnClick();
        }

        /// <summary>The hovered card lifts slightly; a held card presses in, then opens.</summary>
        void Update()
        {
            if (pressing)
            {
                float slop = Mathf.Max(12f, Screen.height * 0.03f);
                if (!Input.GetMouseButton(0) || ((Vector2)Input.mousePosition - pressPosition).magnitude > slop)
                    pressing = false;
                else
                {
                    pressTime += Time.unscaledDeltaTime;
                    if (face != null) face.localScale = Vector3.one * (1f - 0.05f * Mathf.Clamp01(pressTime / LongPressSeconds));
                    if (pressTime >= LongPressSeconds)
                    {
                        pressing = false;
                        openedByHold = true;
                        if (OnLongPress != null) OnLongPress();
                    }
                    return;
                }
            }
            hover = Mathf.MoveTowards(hover, hoverTarget, Time.unscaledDeltaTime * 8f);
            if (face != null) face.localScale = Vector3.one * (1f + 0.04f * hover);
        }
    }
}
