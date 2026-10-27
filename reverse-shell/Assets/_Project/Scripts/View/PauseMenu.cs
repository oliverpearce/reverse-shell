using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Pause button (top-right) and menu: Resume, Settings, Restart Fight, Leave Fight. Esc toggles it. Settings swaps
    /// the buttons for the settings list (the same options as the title screen, <see cref="GameSettings"/>) and
    /// Tutorials, which opens the guide (<see cref="GuideBook"/>: the keys and the battle tutorials) over the fight.
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

            // Pause button: a small ink plate with a gold hairline and the pause icon; its key (Esc) on a paper key-cap
            // badge hanging under it.
            var button = InkStyle.Button(root, "PauseButton", "", view.TogglePause, false, 40);
            var brt = button.GetComponent<RectTransform>();
            brt.Place(topRight, topRight, new Vector2(-24f, -24f), new Vector2(124f, 104f));
            var pauseIcon = UIFactory.CreateImage(brt, "Icon", Color.white);
            pauseIcon.sprite = CardIcons.Pause;
            pauseIcon.preserveAspect = true;
            pauseIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            Caption(brt, UIText.Get("battle.key.pause"));

            // Battle speed, left of the pause button: click (or Tab) cycles 1x / 2x / 4x.
            var speed = InkStyle.Button(root, "SpeedButton", "", CycleSpeed, false, 48);
            speed.GetComponent<RectTransform>().Place(topRight, topRight, new Vector2(-164f, -24f), new Vector2(136f, 104f));
            speedLabel = speed.GetComponentInChildren<Text>();
            Caption((RectTransform)speed.transform, UIText.Get("battle.key.speed"));
            ShowSpeed();

            // Overlay: an ink page with paper grain and a centered card with the hero frame.
            var dim = UIFactory.CreateImage(root, "Overlay", new Color(0.07f, 0.065f, 0.06f, 0.94f), true);
            dim.rectTransform.Fill();
            overlay = dim.gameObject;
            InkStyle.Grain(dim.rectTransform, new Color(1f, 0.95f, 0.85f, 0.035f));
            var card = InkStyle.Panel(dim.transform, "Card", InkStyle.InkPanel);
            // Sized to its content: the title, then four buttons, with an even margin below (taller for Settings).
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 760f));
            cardRect = card.rectTransform;
            InkStyle.HeroFrame(card.rectTransform, InkStyle.Gold, 10f);

            var title = InkStyle.Title(card.transform, "Title", UIText.Get("battle.pause.title"), 110, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.TopStrip(40f, 130f);
            var rule = InkStyle.Line(card.transform, "Rule", InkStyle.Gold);
            rule.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(420f, 2f));

            // Buttons stacked down the card; hovering one grows it and lifts it to a lighter grey (ButtonHover).
            mainPage = UIFactory.CreateRect("Buttons", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(640f, 480f));
            float y = 0f;
            MenuButton(mainPage, UIText.Get("battle.pause.resume"), false, view.TogglePause, ref y);
            MenuButton(mainPage, UIText.Get("battle.pause.settings"), false, () => ShowSettings(true), ref y);
            MenuButton(mainPage, UIText.Get("battle.pause.restart"), false, view.RestartFight, ref y);
            MenuButton(mainPage, UIText.Get("battle.pause.leave"), false, view.LeaveFight, ref y);

            settingsPage = UIFactory.CreateRect("Settings", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(640f, 480f));
            y = 0f;
            // Every setting (shared with the title screen), each with a "?" whose help shows in the box under the card.
            help = InkStyle.HelpPanel(dim.transform, new Vector2(0f, SettingsCardY - SettingsCardHeight * 0.5f - 14f), new Vector2(760f, 200f));
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
            var guide = MenuButton(settingsPage, GameSettings.GuideLabel, false, () => GuideBook.Open(transform), ref y);
            InkStyle.HelpBadge((RectTransform)guide.transform, () => ShowHelp(GameSettings.GuideHelp), () => ShowHelp(null));
            MenuButton(settingsPage, UIText.Get("menu.back"), false, () => ShowSettings(false), ref y);
            settingsPage.gameObject.SetActive(false);

            var hint = UIFactory.CreateText(dim.transform, "Hint", UIText.Get("battle.pause.hint"), 30, InkStyle.Paper, TextAnchor.LowerCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 60f));

            overlay.SetActive(false);
        }

        Text speedLabel;

        /// <summary>The button's key on a small badge hanging just under it, in the same translucent ink plate and rule
        /// frame as the buttons (the whole HUD stays see-through so the art leads).</summary>
        static void Caption(RectTransform button, string key)
        {
            var badge = UIFactory.CreateImage(button, "KeyBadge", InkStyle.InkPanel);
            badge.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(92f, 42f));
            InkStyle.Frame(badge.rectTransform, InkStyle.Rule, 2f);
            var t = UIFactory.CreateText(badge.rectTransform, "Key", key, 26, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.Fill();
        }

        public void CycleSpeed()
        {
            GameSettings.CycleBattleSpeed();
            ShowSpeed();
        }

        void ShowSpeed()
        {
            if (speedLabel != null) speedLabel.text = GameSettings.BattleSpeed + "x";
        }

        /// <summary>The Settings page has five buttons, so the card grows and moves up to make room (help box under it).</summary>
        const float CardHeight = 760f, SettingsCardHeight = 880f, SettingsCardY = 70f;
        RectTransform cardRect;

        void ShowSettings(bool on)
        {
            cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, on ? SettingsCardHeight : CardHeight);
            cardRect.anchoredPosition = new Vector2(0f, on ? SettingsCardY : 0f);
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
            var b = InkStyle.Button(column, label, label, action, primary, 50);
            var rt = b.GetComponent<RectTransform>();
            rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(640f, 100f));

            y += 120f;
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
