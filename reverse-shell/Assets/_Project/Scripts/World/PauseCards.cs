using System;
using System.Collections;
using System.Collections.Generic;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The overworld Esc menu as a row of pixel cards (the same card art as battle, Tools/CardForge):
    /// Quit, Save, Resume (selected when it opens), Party, Units, Settings. Left/Right (or the mouse) picks, Enter / F /
    /// Space / a click plays the card, Esc resumes. Party opens the formation (3 active + backup), Units the character
    /// details; both come back here when closed (X or Esc). Save asks which slot to save to; Settings has Fullscreen and
    /// Enter / Leave Training Mode (a <see cref="RowMenu"/> over the cards). Quit saves and goes back to the title screen.
    /// </summary>
    public class PauseCards : MonoBehaviour
    {
        const float CardScale = 1.0f;              // 5x card pixels (six cards fit across a 4:3 screen)
        const float Gap = 44f;
        const float SelectedScale = 1.12f, SelectedLift = 36f;
        const int DefaultIndex = 2;                // Resume

        class Entry
        {
            public string Name, Icon;
            public Color Color;
            public Action Play;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Text Label;
        }

        OverworldBuilder world;
        CharacterMenu units;
        FormationScreen formation;
        RowMenu pages;                             // Save slots and Settings, over the cards
        readonly List<Entry> cards = new List<Entry>();
        int index = DefaultIndex;
        int openedFrame;
        bool busy;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>The frame it last opened or closed (so the Esc that closes it doesn't reopen it).</summary>
        public int LastToggleFrame { get; private set; }

        public static PauseCards Create(RectTransform parent, OverworldBuilder world, CharacterMenu units, FormationScreen formation)
        {
            var rt = UIFactory.CreateRect("PauseCards", parent).Fill();
            var menu = rt.gameObject.AddComponent<PauseCards>();
            menu.world = world;
            menu.units = units;
            menu.formation = formation;
            menu.pages = RowMenu.Create(parent);
            menu.Build(rt);
            rt.gameObject.SetActive(false);
            return menu;
        }

        void Build(RectTransform root)
        {
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.82f), true);
            dim.rectTransform.Fill();
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.035f));

            var title = InkStyle.Title(root, "Title", "PAUSED", 72, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 110f));

            cards.Add(new Entry { Name = "Quit", Icon = "Menu_Quit", Color = new Color(0.86f, 0.36f, 0.32f), Play = Quit });
            cards.Add(new Entry { Name = "Save", Icon = "Menu_Save", Color = new Color(0.4f, 0.7f, 0.95f), Play = Save });
            cards.Add(new Entry { Name = "Resume", Icon = "Menu_Resume", Color = new Color(0.95f, 0.8f, 0.45f), Play = Close });
            cards.Add(new Entry { Name = "Party", Icon = "Menu_Party", Color = new Color(0.72f, 0.62f, 0.95f), Play = () => OpenNext(3) });
            cards.Add(new Entry { Name = "Units", Icon = "Menu_Units", Color = new Color(0.5f, 0.82f, 0.62f), Play = () => OpenNext(4) });
            cards.Add(new Entry { Name = "Settings", Icon = "Menu_Settings", Color = new Color(0.62f, 0.66f, 0.72f), Play = ShowSettings });

            var size = CardView.Size * CardScale;
            float total = cards.Count * size.x + (cards.Count - 1) * Gap;
            for (int i = 0; i < cards.Count; i++)
            {
                var e = cards[i];
                e.Rect = UIFactory.CreateRect("Card_" + e.Name, root);
                e.Rect.sizeDelta = size;
                e.Rect.anchoredPosition = new Vector2(-total / 2f + size.x / 2f + i * (size.x + Gap), -20f);
                e.Group = e.Rect.gameObject.AddComponent<CanvasGroup>();
                e.Label = BuildFace(e.Rect, e, CardScale);
                int captured = i;
                var hit = e.Rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);   // on the card itself, so it sits behind the face
                var trigger = e.Rect.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => Select(captured));
                trigger.triggers.Add(enter);
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => { Select(captured); Play(); });
                trigger.triggers.Add(click);
            }

            var keys = UIFactory.CreateText(root, "Keys",
                "<color=#C2A061>← →</color>  Choose      <color=#C2A061>Enter</color>  Select      <color=#C2A061>Esc</color>  Resume",
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 50f));
        }

        /// <summary>A menu card in the battle cards' style: tinted swirl, frame, the icon, the name on the plate.</summary>
        static Text BuildFace(RectTransform root, Entry e, float s)
        {
            float u = CardArt.HandPixel * s;
            var swirl = UIFactory.CreateImage(root, "Swirl", e.Color);
            swirl.sprite = CardArt.Swirl;
            swirl.rectTransform.Fill();
            var frame = UIFactory.CreateImage(root, "Frame", Color.white);
            frame.sprite = CardArt.Frame;
            frame.rectTransform.Fill();
            var icon = UIFactory.CreateImage(root, "Icon", Color.white);
            icon.sprite = CardArt.Icon(e.Icon);
            icon.preserveAspect = true;
            CardArt.PlacePixels(icon.rectTransform, CardArt.ArtX, CardArt.ArtY, CardArt.ArtSize, CardArt.ArtSize, u);
            var plate = CardArt.NamePlate;
            var name = UIFactory.CreateText(root, "Name", e.Name.ToUpperInvariant(), Mathf.RoundToInt(26 * s), InkStyle.Paper, TextAnchor.MiddleCenter)
                .BestFit(Mathf.RoundToInt(8 * s), Mathf.RoundToInt(26 * s));
            CardArt.PlacePixels(name.rectTransform, plate.x, plate.y - 1, plate.width, plate.height + 2, u);
            return name;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            busy = false;
            foreach (var e in cards) e.Label.text = e.Name.ToUpperInvariant();
            Select(DefaultIndex);
            foreach (var e in cards) e.Rect.localScale = Vector3.one * (cards.IndexOf(e) == index ? SelectedScale : 1f);
        }

        public void Close()
        {
            LastToggleFrame = Time.frameCount;
            if (pages != null) pages.Hide();
            gameObject.SetActive(false);
        }

        void Select(int i)
        {
            if (busy) return;
            index = Mathf.Clamp(i, 0, cards.Count - 1);
        }

        void Play()
        {
            if (busy) return;
            cards[index].Play();
        }

        void Update()
        {
            if (Time.frameCount == openedFrame || busy) return;
            if (pages.IsOpen || pages.ClosedFrame == Time.frameCount) return;   // a page is over the cards
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Select(index - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Select(index + 1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F))
                Play();
            else if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void LateUpdate()
        {
            // The selected card rises, grows and is fully lit; the others sit back, slightly dimmed.
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < cards.Count; i++)
            {
                var e = cards[i];
                bool on = i == index;
                e.Rect.localScale = Vector3.Lerp(e.Rect.localScale, Vector3.one * (on ? SelectedScale : 1f), k);
                var p = e.Rect.anchoredPosition;
                p.y = Mathf.Lerp(p.y, on ? -20f + SelectedLift : -20f, k);
                e.Rect.anchoredPosition = p;
                e.Group.alpha = Mathf.Lerp(e.Group.alpha, on ? 1f : 0.6f, k);
                if (on) e.Rect.SetAsLastSibling();
            }
        }

        // ------------------------------------------------------------------ Card actions

        // ------------------------------------------------------------------ Save: pick a slot

        void Save() { ShowSlots(); }

        void ShowSlots()
        {
            var rows = new List<RowMenu.Row>();
            for (int slot = 1; slot <= SaveGame.SlotCount; slot++)
            {
                int captured = slot;
                rows.Add(new RowMenu.Row
                {
                    Label = SaveGame.SlotLabel(slot, true),
                    Action = () =>
                    {
                        if (captured != SaveGame.Slot && SaveGame.Exists(captured)) ConfirmOverwrite(captured);
                        else SaveTo(captured);
                    },
                });
            }
            rows.Add(new RowMenu.Row { Label = "Back", Action = pages.Hide });
            pages.Show("SAVE", rows, pages.Hide, SaveGame.Slot - 1);
        }

        void ConfirmOverwrite(int slot)
        {
            pages.Show("SAVE", new List<RowMenu.Row>
            {
                new RowMenu.Row { Label = "Overwrite slot " + slot + "?", Enabled = false },
                new RowMenu.Row { Label = "Yes, save over it", Action = () => SaveTo(slot) },
                new RowMenu.Row { Label = "No", Action = ShowSlots },
            }, ShowSlots, 2);
        }

        /// <summary>Saves to <paramref name="slot"/>, which becomes this game's slot (later saves go there too).</summary>
        void SaveTo(int slot)
        {
            SaveGame.Slot = slot;
            SaveGame.Save();
            pages.Hide();
            StartCoroutine(Flash(cards[1], "SAVED " + slot));
        }

        // ------------------------------------------------------------------ Settings

        void ShowSettings(int select = 0)
        {
            bool inTraining = WorldSession.InTraining;
            var rows = new List<RowMenu.Row>
            {
                new RowMenu.Row
                {
                    Label = GameSettings.FullscreenLabel,
                    Action = () => { GameSettings.ToggleFullscreen(); ShowSettings(0); },
                    Help = "Play in fullscreen or in a window. Changes right away.",
                },
                new RowMenu.Row
                {
                    Label = inTraining ? "Leave Training Mode" : "Enter Training Mode",
                    Action = () =>
                    {
                        Close();
                        if (inTraining) world.LeaveTraining();
                        else world.EnterTraining();
                    },
                    Help = inTraining
                        ? "Go back to exactly where you were before you entered the Training Center."
                        : "Go to the Training Center: practice fights with any enemy level, and the whole test crew. " +
                          "Leave Training Mode (here) brings you back to where you are now.",
                },
                new RowMenu.Row { Label = "Back", Action = pages.Hide },
            };
            pages.Show("SETTINGS", rows, pages.Hide, select);
        }

        void ShowSettings() { ShowSettings(0); }

        IEnumerator Flash(Entry e, string text)
        {
            e.Label.text = text;
            yield return Tween.Punch(e.Rect, 0.08f, 0.2f);
            yield return new WaitForSecondsRealtime(0.9f);
            if (e.Label != null) e.Label.text = e.Name.ToUpperInvariant();
        }

        /// <summary>Opens the Party (formation) or Units screen; closing it comes back here with that card selected.</summary>
        void OpenNext(int card)
        {
            // Next frame, so the key that played this card isn't also read by the next screen.
            busy = true;
            StartCoroutine(OpenNextFrame(card));
        }

        IEnumerator OpenNextFrame(int card)
        {
            yield return null;
            Close();
            busy = false;
            System.Action back = () => { Open(); Select(card); };
            if (card == 3 && formation != null) formation.OpenMenu(back);
            else if (card == 4 && units != null) units.Open(back);
        }

        /// <summary>Save, then back to the title screen (the area reloads under it).</summary>
        void Quit()
        {
            busy = true;
            SaveGame.Save();
            WorldSession.TitleDone = false;
            WorldSession.HasReturnPoint = false;
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
