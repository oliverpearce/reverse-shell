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
    /// the game's name typed out terminal-style over a gold echo (<see cref="TerminalTitle"/>) on an ink wash, and the
    /// menu in two groups (a "/" is two half-width buttons side by side, Left / Right between them):
    ///   Continue / Load, New Game;  Sandbox Mode / Pack Mode, Cards, Settings, Credits, Quit (not in a browser). New Game and Load pick one of <see cref="SaveGame.SlotCount"/> save slots (each shows its leader,
    /// level and when it was saved; overwriting asks first). Choosing a game reloads the area through the ink fade.
    /// Settings → Tutorials opens the guide (<see cref="GuideBook"/>: the keys and the battle tutorials). The version shows
    /// bottom-right, so feedback says which build it's about. Up/Down + Enter or the mouse; Esc / P goes back a page. The world is paused while it's open. The menu uses the same
    /// ink plates with gold hairlines as the pause menu (the selected one is the gold plate), over the party's cards
    /// drifting down in the background (<see cref="FallingCards"/>).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        /// <summary>The game's name, subtitle and version (bottom-right; raise it with every build you hand out) are the
        /// title.* lines of Resources/Story/UI.txt, like every word on this screen.</summary>
        public static string GameTitle { get { return UIText.Get("title.name"); } }
        public static string Subtitle { get { return UIText.Get("title.subtitle"); } }
        public static string Version { get { return UIText.Get("title.version"); } }

        RectTransform menu;
        readonly List<Text> rows = new List<Text>();
        readonly List<Image> plates = new List<Image>();
        readonly List<RectTransform> frames = new List<RectTransform>();
        readonly List<Action> actions = new List<Action>();
        readonly List<bool> enabledRows = new List<bool>();
        readonly List<string> helps = new List<string>();
        readonly List<int> lineOf = new List<int>();   // which line each row is on (a pair shares one)
        readonly List<int> colOf = new List<int>();    // 0 = full width or left half, 1 = right half
        readonly List<VolumeSlider> sliders = new List<VolumeSlider>();   // a volume row's slider, else null
        int line;
        float cursor;   // how far below rowTop the next line goes
        Text help;
        int index;
        Action back;
        int openedFrame;
        float rowTop;   // where this page's rows start below the menu's top (under an info panel)

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>The Card Packs and Card Index pages (set by the overworld); each takes what to do when it closes.</summary>
        public Action<Action> OpenCardPacks, OpenCardIndex;

        /// <summary>Adds the Card Packs and Card Index rows (the menu is rebuilt with them).</summary>
        public void SetCardModes(Action<Action> packs, Action<Action> index)
        {
            OpenCardPacks = packs;
            OpenCardIndex = index;
            ShowMain();
        }

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
            TerminalTitle.Create(root, GameTitle, 150, new Vector2(0f, 440f), 180f);
            var rule = InkStyle.Line(root, "Rule", InkStyle.Gold);
            rule.rectTransform.Place(center, center, new Vector2(0f, 336f), new Vector2(520f, 2f));
            var sub = InkStyle.Label(root, "Subtitle", Subtitle, 30, InkStyle.Gold, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(center, center, new Vector2(0f, 300f), new Vector2(600f, 44f));

            menu = UIFactory.CreateRect("Menu", root).Place(center, new Vector2(0.5f, 1f), new Vector2(0f, MenuTop), new Vector2(RowWidth, 600f));

            // Under the settings list: what the selected (or "?"-hovered) setting does (moved under the last row).
            help = InkStyle.HelpPanel(root, new Vector2(0f, MenuTop), new Vector2(RowWidth, 150f));

            var version = UIFactory.CreateText(root, "Version", Version, 24, InkStyle.PaperDim, TextAnchor.LowerRight);
            version.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(400f, 40f));

            var keys = UIFactory.CreateText(root, "Keys", UIText.Get("title.keys"),
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 44f));
        }

        // ------------------------------------------------------------------ Pages

        void ShowMain()
        {
            int latest = SaveGame.LatestSlot();
            Page(null);
            Pair(UIText.Get("title.continue"), () => Begin(latest, false), latest > 0,
                 UIText.Get("title.load"), () => ShowSlots(false), latest > 0);
            Row(UIText.Get("title.new_game"), () => ShowSlots(true), true);
            Gap();
            if (OpenCardPacks != null)
                Pair(UIText.Get("title.sandbox"), StartSandbox, true,
                     UIText.Get("title.card_packs"), () => OpenCardPacks(ShowMain), true);   // "Pack Mode"
            else
                Row(UIText.Get("title.sandbox"), StartSandbox, true);
            if (OpenCardIndex != null) Row(UIText.Get("title.card_index"), () => OpenCardIndex(ShowMain), true);   // "Cards"
            Row(UIText.Get("title.settings"), ShowSettings, true);
            Row(UIText.Get("title.credits"), ShowCredits, true);
            if (Application.platform != RuntimePlatform.WebGLPlayer) Row(UIText.Get("title.quit"), Quit, true);   // a browser tab can't quit
            Select(latest > 0 ? 0 : 2);   // Continue, else New Game
        }

        /// <summary>The save slots: for a new game every slot (occupied ones ask before overwriting), for Load only saved ones.</summary>
        void ShowSlots(bool newGame)
        {
            Page(ShowMain);
            for (int slot = 1; slot <= SaveGame.SlotCount; slot++)
            {
                var summary = SaveGame.Peek(slot);
                int captured = slot;
                if (newGame)
                    SlotRow(slot, () => { if (summary != null) Confirm(captured); else Begin(captured, true); }, true);
                else
                    SlotRow(slot, () => Begin(captured, false), summary != null);
            }
            Row(UIText.Get("menu.back"), ShowMain, true);
            Select(0);
        }

        void Confirm(int slot)
        {
            Page(() => ShowSlots(true));
            Row(UIText.Get("title.overwrite", "slot", slot.ToString()), null, false);
            Row(UIText.Get("title.overwrite.yes"), () => Begin(slot, true), true);
            Row(UIText.Get("menu.no"), () => ShowSlots(true), true);
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
            SliderRow(GameSettings.SfxLabel, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, GameSettings.SfxHelp);
            SliderRow(GameSettings.MusicLabel, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, GameSettings.MusicHelp);
            Row(GameSettings.GuideLabel, () => GuideBook.Open(transform), true, GameSettings.GuideHelp);
            Row(UIText.Get("menu.back"), ShowMain, true);
            Select(0);
        }

        // ------------------------------------------------------------------ Credits

        void ShowCredits()
        {
            Page(ShowMain);
            InfoPanel(UIText.Get("title.credits.text"), 680f);   // tall enough for every credit line
            Row(UIText.Get("menu.back"), ShowMain, true);
            Select(0);
        }

        /// <summary>A page's text panel at the menu's top; the page's rows go under it.</summary>
        void InfoPanel(string text, float height)
        {
            var panel = InkStyle.Panel(menu, "Info", InkStyle.InkPanel);
            panel.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(RowWidth + 200f, height));
            InkStyle.Frame(panel.rectTransform, InkStyle.Rule, 2f);
            var t = UIFactory.CreateText(panel.transform, "Text", text, 28, InkStyle.Paper, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.rectTransform.Fill(24f);
            rowTop = height + 16f;
        }

        /// <summary>Sandbox Mode: its own save with the whole crew (a fresh one the first time), never a story slot.</summary>
        void StartSandbox()
        {
            WorldSession.StartSandbox();
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
            lineOf.Clear();
            colOf.Clear();
            sliders.Clear();
            line = 0;
            cursor = 0f;
            rowTop = 0f;
            ShowHelp(null);
            back = onBack;
        }

        void ShowHelp(string text)
        {
            if (help == null) return;
            help.text = text ?? "";
            var panel = (RectTransform)help.transform.parent;
            panel.anchoredPosition = new Vector2(0f, MenuTop - rowTop - cursor - 12f);
            panel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <param name="helpText">A settings row's explanation: shown under the list while the row is hovered / selected.</param>
        void Row(string label, Action action, bool isEnabled, string helpText = null)
        {
            AddRow(label, action, isEnabled, helpText, 0f, RowWidth, 0);
            NextLine();
        }

        /// <summary>
        /// A save slot's row, laid out in columns so nothing wraps: "Slot 2" on the left, then the leader's name, the
        /// level in gold and the area on one line, and the save time at the right (dimmer). An empty slot says so.
        /// </summary>
        void SlotRow(int slot, Action action, bool isEnabled)
        {
            int i = rows.Count;
            Row(UIText.Get("save.slot", "slot", slot.ToString()), action, isEnabled);
            var head = rows[i];
            head.alignment = TextAnchor.MiddleLeft;
            head.rectTransform.offsetMin = new Vector2(36f, head.rectTransform.offsetMin.y);
            var plate = plates[i].transform;
            Func<string, int, Color, TextAnchor, Text> part = (text, size, color, anchor) =>
            {
                // Plain text (not the small-caps label style), big enough to read from the couch.
                var t = UIFactory.CreateText(plate, "Part", text, size, isEnabled ? color : UIFactory.WithAlpha(color, 0.45f), anchor);
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                return t;
            };
            string who, area, when;
            int level;
            if (!SaveGame.SlotParts(slot, out who, out level, out area, out when))
            {
                var empty = part(UIText.Get("save.empty"), UIFactory.TextSize.Large, InkStyle.PaperDim, TextAnchor.MiddleLeft);
                empty.rectTransform.Fill();
                empty.rectTransform.offsetMin = new Vector2(250f, 0f);
                return;
            }
            string line = who + "    <color=#C2A061>" + UIText.Get("common.lv") + " " + level + "</color>    " + area;
            var details = part(line, UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.MiddleLeft);
            details.rectTransform.Fill();
            details.rectTransform.offsetMin = new Vector2(250f, 0f);
            details.rectTransform.offsetMax = new Vector2(-250f, 0f);
            details.horizontalOverflow = HorizontalWrapMode.Wrap;   // clipped by its width rather than running into the time
            details.verticalOverflow = VerticalWrapMode.Truncate;
            details.resizeTextForBestFit = true;
            details.resizeTextMinSize = UIFactory.Px(UIFactory.TextSize.Medium);   // only shrinks for a very long name or area
            details.resizeTextMaxSize = UIFactory.Px(UIFactory.TextSize.Large);
            var time = part(when, UIFactory.TextSize.Medium, InkStyle.PaperDim, TextAnchor.MiddleRight);
            time.rectTransform.Fill();
            time.rectTransform.offsetMax = new Vector2(-36f, 0f);
        }

        /// <summary>A volume row: the name on the left, a slider on the right (drag it, or Left / Right when selected).</summary>
        void SliderRow(string label, Func<float> get, Action<float> set, string helpText)
        {
            int i = rows.Count;
            Row(label, () => { }, true, helpText);
            rows[i].alignment = TextAnchor.MiddleLeft;
            rows[i].rectTransform.offsetMin = new Vector2(40f, rows[i].rectTransform.offsetMin.y);
            sliders[i] = VolumeSlider.Add(plates[i].rectTransform, get, set);
        }

        /// <summary>Two half-width buttons side by side on one line ("Continue / Load").</summary>
        void Pair(string left, Action leftAction, bool leftEnabled, string right, Action rightAction, bool rightEnabled)
        {
            float half = (RowWidth - PairGap) / 2f, x = (half + PairGap) / 2f;
            AddRow(left, leftAction, leftEnabled, null, -x, half, 0);
            AddRow(right, rightAction, rightEnabled, null, x, half, 1);
            NextLine();
        }

        /// <summary>A space between groups of rows.</summary>
        void Gap()
        {
            cursor += GroupGap;
        }

        void NextLine()
        {
            line++;
            cursor += RowStep;
        }

        void AddRow(string label, Action action, bool isEnabled, string helpText, float x, float width, int col)
        {
            int i = rows.Count;
            // An ink plate with corner brackets; the selected row is a hover: a lighter grey plate that grows a little.
            var plate = UIFactory.CreateImage(menu, "Row" + i, InkStyle.InkPanel, true);
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -rowTop - cursor), new Vector2(width, RowHeight));
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
            lineOf.Add(line);
            colOf.Add(col);
            sliders.Add(null);   // a setting's help shows under the list while its row is hovered or selected (no "?")

            var trigger = plate.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (enabledRows[i]) Select(i); });
            trigger.triggers.Add(enter);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => { if (enabledRows[i]) { Select(i); actions[i](); } });
            trigger.triggers.Add(click);
        }

        const float RowWidth = 1100f, RowHeight = 74f, RowStep = 84f, MenuTop = 250f, PairGap = 16f, GroupGap = 36f;

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

        /// <summary>Up / Down: the next line with an enabled row (wrapping), the same side of a pair if it can.</summary>
        void Move(int step)
        {
            if (rows.Count == 0) return;
            int lines = lineOf[rows.Count - 1] + 1;
            for (int n = 1; n <= lines; n++)
            {
                int next = ((lineOf[index] + step * n) % lines + lines) % lines;
                int pick = -1;
                for (int r = 0; r < rows.Count; r++)
                {
                    if (lineOf[r] != next || !enabledRows[r]) continue;
                    if (pick < 0 || colOf[r] == colOf[index]) pick = r;
                }
                if (pick >= 0)
                {
                    Select(pick);
                    return;
                }
            }
        }

        /// <summary>Left / Right: the other half of a pair.</summary>
        void MoveSide(int step)
        {
            for (int r = 0; r < rows.Count; r++)
                if (lineOf[r] == lineOf[index] && colOf[r] == colOf[index] + step && enabledRows[r])
                {
                    Select(r);
                    return;
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
            if (Time.frameCount == openedFrame || ScreenFade.Busy || GuideBook.Blocking) return;   // the guide is over it
            if (CardsScreen.AnyOpen || PackOpening.IsOpen || CardZoom.IsOpen || CardDetailPopup.IsOpen) return;   // Card Packs / Index
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Move(-1);
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Move(1);
            // Left / Right: a selected volume slider moves; otherwise across a pair.
            var slider = index < sliders.Count ? sliders[index] : null;
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) { if (slider != null) slider.Step(-1); else MoveSide(-1); }
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) { if (slider != null) slider.Step(1); else MoveSide(1); }
            bool confirm = UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F);
            if (confirm && index < actions.Count && enabledRows[index]) actions[index]();
            else if (UIKeys.Back && back != null) back();
        }
    }
}
