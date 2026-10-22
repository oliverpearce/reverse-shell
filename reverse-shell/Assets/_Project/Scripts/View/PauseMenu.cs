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

            // Pause button: a small ink plate with a gold hairline.
            var button = InkStyle.Button(root, "PauseButton", "II", view.TogglePause, false, 40);
            var brt = button.GetComponent<RectTransform>();
            brt.Place(topRight, topRight, new Vector2(-24f, -24f), new Vector2(100f, 80f));
            var esc = UIFactory.CreateText(brt, "Esc", "ESC", 13, InkStyle.Gold, TextAnchor.LowerCenter, FontStyle.Bold);
            esc.rectTransform.Fill(4f);

            // Overlay: an ink page with paper grain and a centered card with the hero frame.
            var dim = UIFactory.CreateImage(root, "Overlay", new Color(0.07f, 0.065f, 0.06f, 0.94f), true);
            dim.rectTransform.Fill();
            overlay = dim.gameObject;
            InkStyle.Grain(dim.rectTransform, new Color(1f, 0.95f, 0.85f, 0.035f));
            var card = InkStyle.Panel(dim.transform, "Card", InkStyle.InkPanel);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 820f));
            InkStyle.HeroFrame(card.rectTransform, InkStyle.Gold, 10f);

            var title = InkStyle.Title(card.transform, "Title", "Paused", 110, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.TopStrip(40f, 130f);
            var rule = InkStyle.Line(card.transform, "Rule", InkStyle.Gold);
            rule.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(420f, 2f));

            // Buttons stacked down the card; Resume is the gold one.
            var column = UIFactory.CreateRect("Buttons", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(580f, 520f));
            float y = 0f;
            MenuButton(column, "Resume", true, view.TogglePause, ref y, 0);
            MenuButton(column, "Restart Fight", false, view.RestartFight, ref y, 1);
            MenuButton(column, "Leave Fight", false, view.LeaveFight, ref y, 2);
            var rankToggle = MenuButton(column, RankLabel(), false, null, ref y, 3);
            var rankLabel = rankToggle.GetComponentInChildren<Text>();
            rankToggle.onClick.AddListener(() =>
            {
                CardSettings.RankStars = !CardSettings.RankStars;
                rankLabel.text = RankLabel();
            });

            var hint = InkStyle.Label(dim.transform, "Hint", "Esc  resume", 18, InkStyle.PaperDim, TextAnchor.LowerCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(700f, 40f));

            overlay.SetActive(false);
        }

        /// <summary>Card rank display: stacking stars or pips.</summary>
        static string RankLabel()
        {
            return "Card Rank: " + (CardSettings.RankStars ? "Stars" : "Pips");
        }

        static Button MenuButton(RectTransform column, string label, bool primary, UnityEngine.Events.UnityAction action, ref float y, int i)
        {
            var b = InkStyle.Button(column, label, label, action, primary, 44);
            var rt = b.GetComponent<RectTransform>();
            rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(580f, 88f));

            y += 112f;
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
