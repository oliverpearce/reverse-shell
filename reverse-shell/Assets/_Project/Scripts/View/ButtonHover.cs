using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Hover feedback for every UIFactory button: it grows a little and its plate turns a lighter grey
    /// (<see cref="InkStyle.HoverGray"/>; gold buttons just brighten). No gold border.
    /// Runs on unscaled time so it also works in the pause menu (Time.timeScale = 0).
    /// </summary>
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float HoverScale = 1.05f;
        const float Speed = 18f;

        Button button;
        Graphic plate;
        Color baseColor;
        bool hovered;
        float t;

        void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                var colors = button.colors;
                colors.highlightedColor = Color.white;   // the hover is drawn here (the plate's colour), not tinted
                colors.selectedColor = Color.white; // don't stay lit after a click
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
            plate = button != null ? button.targetGraphic : null;
            if (plate != null) baseColor = plate.color;
        }

        void OnDisable()
        {
            // Menus close while hovered; start un-hovered next time they open.
            hovered = false;
            t = 0f;
            transform.localScale = Vector3.one;
            if (plate != null) plate.color = baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = button == null || button.interactable;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }

        void Update()
        {
            if (hovered && button != null && !button.interactable) hovered = false;
            t = Mathf.MoveTowards(t, hovered ? 1f : 0f, Speed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, HoverScale, t);
            if (plate != null)
            {
                // Dark plates lift to the hover grey; light (gold) ones brighten a touch.
                bool dark = baseColor.r + baseColor.g + baseColor.b < 1.2f;
                var hover = dark ? InkStyle.HoverGray : Color.Lerp(baseColor, Color.white, 0.25f);
                plate.color = Color.Lerp(baseColor, hover, t);
            }
        }
    }
}
