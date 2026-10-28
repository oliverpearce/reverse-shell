using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// CARDS > LIFT
    /// A card in a grid (the card book) that you pick up by hovering it: it rises, grows, casts a soft shadow, comes in
    /// front of its neighbours and leans toward the mouse in 3D (<see cref="CardTilt"/>, text kept flat). Selecting it
    /// with the keyboard lifts it too (no lean). Eases back down when left. Unscaled time.
    /// </summary>
    public class CardLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float LiftScale = 1.15f, LiftHeight = 18f, Speed = 14f, MaxLean = 12f;

        RectTransform face;
        Image shadow;
        float lift;
        bool hovered;

        /// <summary>Keyboard selection (lifts without leaning).</summary>
        public bool Selected;
        /// <summary>Raised when the mouse starts hovering it.</summary>
        public Action Hovered;

        public bool IsUp { get { return hovered || Selected; } }
        /// <summary>The mouse is over it right now.</summary>
        public bool IsHovered { get { return hovered; } }

        /// <param name="holder">The grid cell (takes the pointer; needs a raycast graphic).</param>
        /// <param name="face">The card inside it (it moves, grows and leans).</param>
        public static CardLift Add(RectTransform holder, RectTransform face)
        {
            var lift = holder.gameObject.AddComponent<CardLift>();
            lift.face = face;
            lift.shadow = UIFactory.CreateImage(holder, "LiftShadow", new Color(0f, 0f, 0f, 0f));
            lift.shadow.sprite = UiParticles.GlowSprite();
            lift.shadow.raycastTarget = false;
            lift.shadow.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f),
                new Vector2(holder.rect.width > 0 ? holder.rect.width : 150f, 40f));
            lift.shadow.transform.SetAsFirstSibling();
            var tilt = CardTilt.Add(face);
            tilt.MaxAngle = MaxLean;
            tilt.HoverMax = MaxLean;
            tilt.HoverWhen = () => lift.hovered && !Input.GetMouseButton(0);
            return lift;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            transform.SetAsLastSibling();   // in front of its neighbours
            if (Hovered != null) Hovered();
        }

        public void OnPointerExit(PointerEventData e) { hovered = false; }

        void OnDisable()
        {
            hovered = false;
            lift = 0f;
            Apply();
        }

        void Update()
        {
            float target = IsUp ? 1f : 0f;
            if (Mathf.Approximately(lift, target)) return;
            lift = Mathf.MoveTowards(lift, target, Time.unscaledDeltaTime * Speed * Mathf.Max(0.15f, Mathf.Abs(target - lift)));
            if (Selected && !hovered && lift > 0.01f && transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
            Apply();
        }

        void Apply()
        {
            if (face == null) return;
            float e = lift * lift * (3f - 2f * lift);
            face.anchoredPosition = new Vector2(0f, LiftHeight * e);
            face.localScale = Vector3.one * Mathf.Lerp(1f, LiftScale, e);
            if (shadow != null)
            {
                shadow.color = new Color(0f, 0f, 0f, 0.45f * e);
                shadow.rectTransform.localScale = new Vector3(1f + 0.15f * e, 1f, 1f);
            }
        }
    }
}
