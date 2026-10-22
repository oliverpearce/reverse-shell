using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Hover feedback for every UIFactory button: it grows slightly, brightens and gets a gold border.
    /// Runs on unscaled time so it also works in the pause menu (Time.timeScale = 0).
    /// </summary>
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color Gold = new Color(0.9f, 0.78f, 0.52f); // InkStyle.GoldBright
        const float HoverScale = 1.03f;
        const float Speed = 18f;

        Button button;
        Outline border;
        bool hovered;
        float t;

        void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
                colors.selectedColor = Color.white; // don't stay lit after a click
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
            border = gameObject.AddComponent<Outline>();
            border.effectColor = Gold;
            border.effectDistance = new Vector2(2f, -2f);
            border.enabled = false;
        }

        void OnDisable()
        {
            // Menus close while hovered; start un-hovered next time they open.
            hovered = false;
            t = 0f;
            transform.localScale = Vector3.one;
            if (border != null) border.enabled = false;
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
            border.enabled = t > 0.01f;
            var c = Gold;
            c.a = t;
            border.effectColor = c;
        }
    }
}
