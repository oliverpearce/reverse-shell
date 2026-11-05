using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Every Soul card glows softly in its owner's colour while the pointer is over it (bible: Soul cards). A soft
    /// halo behind the card (UiParticles.GlowSprite) that fades in on hover and out again, with a slow breath while
    /// it is lit. Runs on unscaled time, so it works in the paused menus too.
    /// </summary>
    public class SoulGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float Alpha = 0.55f;       // the halo at full glow
        const float Spread = 1.45f;      // halo size against the card
        const float FadeSpeed = 7f;

        Image halo;
        Color color;
        bool hovered, always;
        float t;

        /// <summary>Lit all the time (the member cards on the Units page and the pre-fight screen), brighter on hover.</summary>
        public const float RestingGlow = 0.7f;

        /// <summary>The glow colour for a unit's Soul card: its soulGlow (the palette editor's choice) if set, else its
        /// card colour.</summary>
        public static Color ColorOf(UnitDefinition unit)
        {
            // The protagonist glows in her resonance colour (white on Basic), like her hair and eyes.
            if (unit != null && UnitDefinition.AnimTint != null)
            {
                var tint = UnitDefinition.AnimTint(unit);
                if (tint.HasValue) return new Color(tint.Value.r, tint.Value.g, tint.Value.b, 1f);
            }
            var c = unit as CharacterDefinition;
            if (c != null && c.soulGlow.a > 0f) return new Color(c.soulGlow.r, c.soulGlow.g, c.soulGlow.b, 1f);
            return unit != null ? unit.color : Color.white;
        }

        /// <summary>Gives <paramref name="card"/> its hover glow in <paramref name="owner"/>'s colour. The card needs a
        /// raycast target (cards built by CardView and the party screens have one). Adding it twice is harmless.</summary>
        public static SoulGlow Add(RectTransform card, Color owner)
        {
            if (card == null) return null;
            var glow = card.GetComponent<SoulGlow>();
            if (glow == null) glow = card.gameObject.AddComponent<SoulGlow>();
            glow.color = owner;
            if (glow.halo == null)
            {
                glow.halo = UIFactory.CreateImage(card, "SoulGlow", new Color(owner.r, owner.g, owner.b, 0f));
                glow.halo.sprite = UiParticles.GlowSprite();
                glow.halo.raycastTarget = false;
                var rt = glow.halo.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.SetAsFirstSibling();   // behind the card's art
            }
            return glow;
        }

        /// <summary>Like <see cref="Add"/>, but always lit (brighter while hovered).</summary>
        public static SoulGlow AddStatic(RectTransform card, Color owner)
        {
            var glow = Add(card, owner);
            if (glow != null) glow.always = true;
            return glow;
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; }

        void OnDisable()
        {
            hovered = false;
            t = 0f;
            if (halo != null) halo.color = new Color(color.r, color.g, color.b, 0f);
        }

        void Update()
        {
            if (halo == null) return;
            t = Mathf.MoveTowards(t, hovered ? 1f : always ? RestingGlow : 0f, FadeSpeed * Time.unscaledDeltaTime);
            var size = ((RectTransform)transform).rect.size;
            float breath = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 2.4f);
            halo.rectTransform.sizeDelta = size * Spread * breath;
            halo.color = new Color(color.r, color.g, color.b, Alpha * t * t * (3f - 2f * t));
        }
    }
}
