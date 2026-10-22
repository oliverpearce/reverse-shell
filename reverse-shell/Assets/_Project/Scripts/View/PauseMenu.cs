using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Pause button (top-right) and menu: Resume, Restart Fight, Leave Fight. Esc toggles it.
    /// Pausing sets Time.timeScale to 0, which freezes battle animations; the menu itself runs on unscaled time.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        static readonly Color Dim = new Color(0.01f, 0.01f, 0.03f, 0.82f);
        static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.1f, 0.99f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f);

        BattleView view;
        GameObject overlay;

        public bool IsOpen { get { return overlay.activeSelf; } }

        public static PauseMenu Create(RectTransform root, BattleView view, bool canLeave)
        {
            var go = UIFactory.CreateRect("Pause", root).Fill();
            var menu = go.gameObject.AddComponent<PauseMenu>();
            menu.view = view;
            menu.Build(go, canLeave);
            return menu;
        }

        void Build(RectTransform root, bool canLeave)
        {
            var topRight = new Vector2(1f, 1f);

            // Pause button
            var button = UIFactory.CreateButton(root, "PauseButton", "II", new Color(0.16f, 0.14f, 0.22f, 0.95f), view.TogglePause, 34);
            var brt = button.GetComponent<RectTransform>();
            brt.Place(topRight, topRight, new Vector2(-24f, -24f), new Vector2(88f, 88f));
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2f, -2f);
            var esc = UIFactory.CreateText(brt, "Esc", "ESC", 14, new Color(0.8f, 0.8f, 0.88f), TextAnchor.LowerCenter, FontStyle.Bold);
            esc.rectTransform.Fill(4f);

            // Overlay
            var dim = UIFactory.CreateImage(root, "Overlay", Dim, true);
            dim.rectTransform.Fill();
            overlay = dim.gameObject;

            var panel = UIFactory.CreateImage(dim.transform, "Panel", PanelColor, true);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, canLeave ? 470f : 520f));
            var panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = Gold;
            panelOutline.effectDistance = new Vector2(3f, -3f);

            var title = UIFactory.CreateText(panel.transform, "Title", "PAUSED", 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.TopStrip(28f, 60f);

            float y = 112f;
            MenuButton(panel.rectTransform, "RESUME", new Color(0.85f, 0.45f, 0.2f), view.TogglePause, ref y);
            MenuButton(panel.rectTransform, "RESTART FIGHT", new Color(0.3f, 0.32f, 0.48f), view.RestartFight, ref y);
            var leave = MenuButton(panel.rectTransform, "LEAVE FIGHT", new Color(0.55f, 0.2f, 0.28f), view.LeaveFight, ref y);
            if (!canLeave)
            {
                leave.interactable = false;
                var note = UIFactory.CreateText(panel.transform, "Note", "Leaving returns to the overworld.\nStart this fight from the overworld to use it.", 20,
                    new Color(0.75f, 0.75f, 0.82f), TextAnchor.UpperCenter);
                note.fontSize = 18;
                note.rectTransform.TopStrip(y - 8f, 50f, 30f);
            }

            var hint = UIFactory.CreateText(panel.transform, "Hint", "Press Esc to resume", 20, new Color(0.7f, 0.7f, 0.78f), TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            hint.rectTransform.sizeDelta = new Vector2(0f, 30f);

            overlay.SetActive(false);
        }

        static Button MenuButton(RectTransform panel, string label, Color color, UnityEngine.Events.UnityAction action, ref float y)
        {
            var b = UIFactory.CreateButton(panel, label, label, color, action, 26);
            b.GetComponent<RectTransform>().TopStrip(y, 70f, 60f);
            y += 86f;
            return b;
        }

        public void Open()
        {
            overlay.SetActive(true);
            transform.SetAsLastSibling();
            Time.timeScale = 0f;
        }

        public void Close()
        {
            overlay.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
