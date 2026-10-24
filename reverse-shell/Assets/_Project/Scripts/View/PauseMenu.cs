using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Pause button (top-right) and menu: Resume, Settings, Restart Fight, Leave Fight. Esc toggles it. Settings swaps
    /// the buttons for the settings list (the same options as the title screen, <see cref="GameSettings"/>).
    /// Pausing sets Time.timeScale to 0, which freezes battle animations; the menu itself runs on unscaled time.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {

        BattleView view;
        GameObject overlay;
        RectTransform mainPage, settingsPage;
        readonly System.Collections.Generic.List<Text> optionLabels = new System.Collections.Generic.List<Text>();
        Text help;

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
            var esc = UIFactory.CreateText(brt, "Esc", "ESC", 16, InkStyle.Gold, TextAnchor.LowerCenter, FontStyle.Bold);
            esc.rectTransform.Fill(4f);

            // Overlay: an ink page with paper grain and a centered card with the hero frame.
            var dim = UIFactory.CreateImage(root, "Overlay", new Color(0.07f, 0.065f, 0.06f, 0.94f), true);
            dim.rectTransform.Fill();
            overlay = dim.gameObject;
            InkStyle.Grain(dim.rectTransform, new Color(1f, 0.95f, 0.85f, 0.035f));
            var card = InkStyle.Panel(dim.transform, "Card", InkStyle.InkPanel);
            // Sized to its content: the title, then four buttons, with an even margin below.
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 700f));
            InkStyle.HeroFrame(card.rectTransform, InkStyle.Gold, 10f);

            var title = InkStyle.Title(card.transform, "Title", "Paused", 110, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.TopStrip(40f, 130f);
            var rule = InkStyle.Line(card.transform, "Rule", InkStyle.Gold);
            rule.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(420f, 2f));

            // Buttons stacked down the card; Resume is the gold one.
            mainPage = UIFactory.CreateRect("Buttons", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(580f, 440f));
            float y = 0f;
            MenuButton(mainPage, "Resume", true, view.TogglePause, ref y);
            MenuButton(mainPage, "Settings", false, () => ShowSettings(true), ref y);
            MenuButton(mainPage, "Restart Fight", false, view.RestartFight, ref y);
            MenuButton(mainPage, "Leave Fight", false, view.LeaveFight, ref y);

            settingsPage = UIFactory.CreateRect("Settings", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(580f, 440f));
            y = 0f;
            // Every setting (shared with the title screen), each with a "?" whose help shows in the box under the card.
            help = InkStyle.HelpPanel(dim.transform, new Vector2(0f, -364f), new Vector2(760f, 200f));
            foreach (var option in GameSettings.Options)
            {
                var opt = option;
                Text label = null;
                var b = MenuButton(settingsPage, opt.Label(), false, () =>
                {
                    opt.Toggle();
                    label.text = opt.Label();
                }, ref y);
                label = b.GetComponentInChildren<Text>();
                label.rectTransform.offsetMax = new Vector2(-70f, label.rectTransform.offsetMax.y);   // room for the "?"
                optionLabels.Add(label);
                InkStyle.HelpBadge((RectTransform)b.transform, () => ShowHelp(opt.Help), () => ShowHelp(null));
            }
            MenuButton(settingsPage, "Back", true, () => ShowSettings(false), ref y);
            settingsPage.gameObject.SetActive(false);

            var hint = UIFactory.CreateText(dim.transform, "Hint", "Esc  Resume", 30, InkStyle.Paper, TextAnchor.LowerCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 60f));

            overlay.SetActive(false);
        }

        void ShowSettings(bool on)
        {
            mainPage.gameObject.SetActive(!on);
            settingsPage.gameObject.SetActive(on);
            ShowHelp(null);
            if (on)
                for (int i = 0; i < optionLabels.Count; i++) optionLabels[i].text = GameSettings.Options[i].Label();
        }

        void ShowHelp(string text)
        {
            help.text = text ?? "";
            help.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        static Button MenuButton(RectTransform column, string label, bool primary, UnityEngine.Events.UnityAction action, ref float y)
        {
            var b = InkStyle.Button(column, label, label, action, primary, 44);
            var rt = b.GetComponent<RectTransform>();
            rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(580f, 88f));

            y += 112f;
            return b;
        }

        public void Open()
        {
            ShowSettings(false);
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
