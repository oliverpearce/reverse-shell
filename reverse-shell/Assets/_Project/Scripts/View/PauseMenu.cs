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

        BattleView view;
        GameObject overlay;

        public bool IsOpen { get { return overlay.activeSelf; } }

        public static PauseMenu Create(RectTransform root, BattleView view)
        {
            var go = UIFactory.CreateRect("Pause", root).Fill();
            var menu = go.gameObject.AddComponent<PauseMenu>();
            menu.view = view;
            menu.Build(go);
            return menu;
        }

        void Build(RectTransform root)
        {
            var topRight = new Vector2(1f, 1f);

            // Pause button: a small black slant with lilac "II".
            var button = PersonaStyle.Button(root, "PauseButton", "II", view.TogglePause, false, 44);
            var brt = button.GetComponent<RectTransform>();
            brt.Place(topRight, topRight, new Vector2(-24f, -24f), new Vector2(110f, 80f));
            brt.localRotation = Quaternion.Euler(0f, 0f, -3f);
            var esc = UIFactory.CreateText(brt, "Esc", "ESC", 14, PersonaStyle.Lilac, TextAnchor.LowerCenter, FontStyle.Bold);
            esc.rectTransform.Fill(4f);

            // Overlay: a lilac Persona page (halftone, turning burst, black slab) like the party menu.
            var dim = UIFactory.CreateImage(root, "Overlay", new Color(0.69f, 0.55f, 0.95f, 0.97f), true);
            dim.rectTransform.Fill();
            overlay = dim.gameObject;
            var burst = PersonaStyle.Burst(dim.rectTransform, new Vector2(300f, -60f), 3200f, new Color(0f, 0f, 0f, 0.16f));
            burst.gameObject.AddComponent<Spin>().degreesPerSecond = 6f;
            PersonaStyle.Halftone(dim.rectTransform, new Color(0f, 0f, 0f, 0.2f));
            var slab = PersonaStyle.Slant(dim.transform, "Slab", PersonaStyle.Black);
            slab.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, -40f), new Vector2(1500f, 560f));
            slab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            var title = PersonaStyle.Title(dim.transform, "Title", "PAUSED", 190, PersonaStyle.White, PersonaStyle.Black, TextAnchor.UpperLeft, 14f);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -30f), new Vector2(1000f, 230f));
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);

            // Buttons: plates stacked down the slab; RESUME is the lilac one.
            var column = UIFactory.CreateRect("Buttons", dim.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(-40f, 240f), new Vector2(620f, 520f));
            float y = 0f;
            MenuButton(column, "RESUME", true, view.TogglePause, ref y, 0);
            MenuButton(column, "RESTART FIGHT", false, view.RestartFight, ref y, 1);
            MenuButton(column, "LEAVE FIGHT", false, view.LeaveFight, ref y, 2);
            var rankToggle = MenuButton(column, RankLabel(), false, null, ref y, 3);
            var rankLabel = rankToggle.GetComponentInChildren<Text>();
            rankToggle.onClick.AddListener(() =>
            {
                CardSettings.RankStars = !CardSettings.RankStars;
                rankLabel.text = RankLabel();
            });

            var hintBar = PersonaStyle.Slant(dim.transform, "HintBar", PersonaStyle.Black);
            hintBar.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(40f, 40f), new Vector2(700f, 66f));
            hintBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            var hint = UIFactory.CreateText(hintBar.transform, "Hint", "ESC  RESUME", 30, PersonaStyle.White, TextAnchor.MiddleCenter);
            hint.font = UIFactory.DisplayFont;
            hint.rectTransform.Fill(10f);

            overlay.SetActive(false);
        }

        /// <summary>Card rank display: stacking stars or pips.</summary>
        static string RankLabel()
        {
            return "CARD RANK: " + (CardSettings.RankStars ? "STARS" : "PIPS");
        }

        static Button MenuButton(RectTransform column, string label, bool primary, UnityEngine.Events.UnityAction action, ref float y, int i)
        {
            var b = PersonaStyle.Button(column, label, label, action, primary, 46);
            var rt = b.GetComponent<RectTransform>();
            rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(580f, 92f));

            y += 118f;
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
