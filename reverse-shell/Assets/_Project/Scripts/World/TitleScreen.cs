using System;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The title screen, shown over the first area when the game starts (Reverse: 1999 look, <see cref="InkStyle"/>):
    /// the game's name in big serif over an ink wash, and a short menu: Continue (the latest save), New Game, Load,
    /// Training Mode (a debug shortcut, also under Settings), Settings, Quit. New Game and Load pick one of <see cref="SaveGame.SlotCount"/> save slots (each shows its leader,
    /// level and when it was saved; overwriting asks first). Choosing a game reloads the area through the ink fade.
    /// Up/Down + Enter or the mouse; Esc goes back a page. The world is paused while it's open. The menu uses the same
    /// ink plates with gold hairlines as the pause menu (the selected one is the gold plate), over the party's cards
    /// drifting down in the background (<see cref="FallingCards"/>).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        /// <summary>The game's name (a working title; change it here).</summary>
        public const string GameTitle = "REVERSE SHELL";
        public const string Subtitle = "Demo";

        RectTransform menu;
        readonly List<Text> rows = new List<Text>();
        readonly List<Image> plates = new List<Image>();
        readonly List<RectTransform> frames = new List<RectTransform>();
        readonly List<Action> actions = new List<Action>();
        readonly List<bool> enabledRows = new List<bool>();
        readonly List<string> helps = new List<string>();
        Text help;
        int index;
        Action back;
        int openedFrame;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <param name="party">Whose cards fall in the background.</param>
        public static TitleScreen Create(RectTransform parent, IEnumerable<CharacterDefinition> party = null)
        {
            var rt = UIFactory.CreateRect("TitleScreen", parent).Fill();
            var title = rt.gameObject.AddComponent<TitleScreen>();
            title.Build(rt, party);
            title.ShowMain();
            title.openedFrame = Time.frameCount;
            rt.SetAsLastSibling();
            Time.timeScale = 0f;
            return title;
        }

        void OnDisable()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        void Build(RectTransform root, IEnumerable<CharacterDefinition> party)
        {
            var bg = UIFactory.CreateImage(root, "Wash", new Color(0.07f, 0.065f, 0.06f, 0.94f), true);
            bg.rectTransform.Fill();
            FallingCards.Create(root, party);
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.04f));

            var center = new Vector2(0.5f, 0.5f);
            var name = InkStyle.Title(root, "Title", GameTitle, 150, InkStyle.Paper, TextAnchor.MiddleCenter);
            name.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            name.rectTransform.Place(center, center, new Vector2(0f, 300f), new Vector2(1600f, 180f));
            var rule = InkStyle.Line(root, "Rule", InkStyle.Gold);
            rule.rectTransform.Place(center, center, new Vector2(0f, 196f), new Vector2(520f, 2f));
            var sub = InkStyle.Label(root, "Subtitle", Subtitle, 30, InkStyle.Gold, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(center, center, new Vector2(0f, 160f), new Vector2(600f, 44f));

            menu = UIFactory.CreateRect("Menu", root).Place(center, new Vector2(0.5f, 1f), new Vector2(0f, 96f), new Vector2(RowWidth, 520f));

            // Under the settings list: what the selected (or "?"-hovered) setting does.
            help = InkStyle.HelpPanel(root, new Vector2(0f, 96f - 3f * RowStep - 12f), new Vector2(RowWidth, 170f));

            var keys = UIFactory.CreateText(root, "Keys", "<color=#C2A061>↑ ↓</color>  Select      <color=#C2A061>Enter</color>  Confirm      <color=#C2A061>Esc</color>  Back",
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 44f));
        }

        // ------------------------------------------------------------------ Pages

        void ShowMain()
        {
            int latest = SaveGame.LatestSlot();
            Page(null);
            Row("Continue", () => Begin(latest, false), latest > 0);
            Row("New Game", () => ShowSlots(true), true);
            Row("Load", () => ShowSlots(false), latest > 0);
            Row("Training Mode", StartTraining, true);   // debug shortcut, for now
            Row("Settings", ShowSettings, true);
            Row("Quit", Quit, true);
            Select(latest > 0 ? 0 : 1);
        }

        /// <summary>The save slots: for a new game every slot (occupied ones ask before overwriting), for Load only saved ones.</summary>
        void ShowSlots(bool newGame)
        {
            Page(ShowMain);
            for (int slot = 1; slot <= SaveGame.SlotCount; slot++)
            {
                var summary = SaveGame.Peek(slot);
                string label = SaveGame.SlotLabel(slot, false);
                int captured = slot;
                if (newGame)
                    Row(label, () => { if (summary != null) Confirm(captured); else Begin(captured, true); }, true);
                else
                    Row(label, () => Begin(captured, false), summary != null);
            }
            Row("Back", ShowMain, true);
            Select(0);
        }

        void Confirm(int slot)
        {
            Page(() => ShowSlots(true));
            Row("Overwrite slot " + slot + "?", null, false);
            Row("Yes, start a new game", () => Begin(slot, true), true);
            Row("No", () => ShowSlots(true), true);
            Select(2);
        }

        void ShowSettings()
        {
            Page(ShowMain);
            // Every setting (shared with the battle pause menu), each with a "?" that explains it.
            foreach (var option in GameSettings.Options)
            {
                var opt = option;
                int row = rows.Count;
                Row(opt.Label(), () =>
                {
                    opt.Toggle();
                    rows[row].text = opt.Label();
                }, true, opt.Help);
            }
            Row("Enter Training Mode", StartTraining, true, TrainingHelp);   // debug shortcut, for now
            Row("Back", ShowMain, true);
            Select(0);
        }

        const string TrainingHelp = "Go straight to the Training Center with your latest save: practice fights at any " +
                                    "enemy level, and the whole test crew. Leave Training Mode (Esc, Settings) takes you back.";

        /// <summary>Training Mode: the latest save (or a new game), starting in the Training Center.</summary>
        void StartTraining()
        {
            WorldSession.StartTraining();
            Time.timeScale = 1f;
            enabled = false;   // no more input; the fade reloads the area without the title
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }

        void Begin(int slot, bool newGame)
        {
            if (slot <= 0) return;
            WorldSession.StartSlot(slot, newGame);
            Time.timeScale = 1f;
            enabled = false;   // no more input; the fade reloads the area without the title
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Rows

        void Page(Action onBack)
        {
            UIFactory.ClearChildren(menu);
            rows.Clear();
            plates.Clear();
            frames.Clear();
            actions.Clear();
            enabledRows.Clear();
            helps.Clear();
            ShowHelp(null);
            back = onBack;
        }

        void ShowHelp(string text)
        {
            if (help == null) return;
            help.text = text ?? "";
            help.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <param name="helpText">A settings row's explanation: adds the "?" badge and shows it when selected.</param>
        void Row(string label, Action action, bool isEnabled, string helpText = null)
        {
            int i = rows.Count;
            // An ink plate with corner brackets; the selected row is a hover: a lighter grey plate that grows a little.
            var plate = UIFactory.CreateImage(menu, "Row" + i, InkStyle.InkPanel, true);
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * RowStep), new Vector2(RowWidth, RowHeight));
            var frame = InkStyle.Frame(plate.rectTransform, InkStyle.Rule, 2f);
            var t = InkStyle.Title(plate.transform, "Label", label, 40, InkStyle.Paper, TextAnchor.MiddleCenter);
            t.supportRichText = true;
            t.rectTransform.Fill(8f);
            rows.Add(t);
            plates.Add(plate);
            frames.Add(frame);
            actions.Add(action);
            enabledRows.Add(isEnabled && action != null);
            helps.Add(helpText);
            if (helpText != null)
                InkStyle.HelpBadge(plate.rectTransform, () => ShowHelp(helpText), () => ShowHelp(helps.Count > index ? helps[index] : null));

            var trigger = plate.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (enabledRows[i]) Select(i); });
            trigger.triggers.Add(enter);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => { if (enabledRows[i]) { Select(i); actions[i](); } });
            trigger.triggers.Add(click);
        }

        const float RowWidth = 1100f, RowHeight = 74f, RowStep = 88f;

        void Select(int i)
        {
            if (rows.Count == 0) return;
            index = Mathf.Clamp(i, 0, rows.Count - 1);
            for (int r = 0; r < rows.Count; r++)
            {
                bool on = r == index && enabledRows[r];
                rows[r].color = !enabledRows[r] ? InkStyle.PaperDim * new Color(1f, 1f, 1f, 0.6f) : InkStyle.Paper;
                plates[r].color = on ? InkStyle.HoverGray : !enabledRows[r] ? UIFactory.WithAlpha(InkStyle.InkPanel, 0.5f) : InkStyle.InkPanel;
                foreach (var edge in frames[r].GetComponentsInChildren<Image>())
                    edge.color = on ? InkStyle.Paper : InkStyle.Rule;
            }
            ShowHelp(helps.Count > index ? helps[index] : null);
        }

        void Move(int step)
        {
            if (rows.Count == 0) return;
            for (int n = 1; n <= rows.Count; n++)
            {
                int next = ((index + step * n) % rows.Count + rows.Count) % rows.Count;
                if (enabledRows[next])
                {
                    Select(next);
                    return;
                }
            }
        }

        const float SelectedScale = 1.04f;

        /// <summary>The selected row grows a little (eased; unscaled, so it works over a paused world).</summary>
        void GrowSelected()
        {
            float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            for (int r = 0; r < plates.Count; r++)
            {
                if (plates[r] == null) continue;
                float target = r == index && enabledRows[r] ? SelectedScale : 1f;
                var rt = plates[r].rectTransform;
                rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * target, k);
            }
        }

        void Update()
        {
            GrowSelected();
            if (Time.frameCount == openedFrame || ScreenFade.Busy) return;
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Move(-1);
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Move(1);
            bool confirm = UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F);
            if (confirm && index < actions.Count && enabledRows[index]) actions[index]();
            else if (Input.GetKeyDown(KeyCode.Escape) && back != null) back();
        }
    }
}
