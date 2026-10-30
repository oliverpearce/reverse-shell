using System.Collections;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>Overlay UI for the overworld: interact prompt, dialogue box, area title, party HUD, controls hint, fades.</summary>
    public class OverworldUI : MonoBehaviour
    {
        static readonly Color Panel = WorldArt.UiPanel;

        RectTransform promptRect;
        Text promptText;
        CanvasGroup titleGroup;
        RectTransform titleRoot;
        Image fade;
        RectTransform partyPanel;

        public struct PartyLine
        {
            public readonly string Name;
            public readonly int Level, HP, MaxHP;
            public readonly Color Color;

            public PartyLine(string name, int level, int hp, int maxHP, Color color)
            {
                Name = name;
                Level = level;
                HP = hp;
                MaxHP = maxHP;
                Color = color;
            }
        }

        public DialogueBox Dialogue { get; private set; }
        public bool DialogueOpen { get { return Dialogue != null && Dialogue.IsOpen; } }

        public static OverworldUI Create(Transform parent)
        {
            var go = new GameObject("OverworldUI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIFactory.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            go.AddComponent<GraphicRaycaster>(); // the party menu takes mouse clicks
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(ControlInputModule));
            var ui = go.AddComponent<OverworldUI>();
            ui.Build();
            return ui;
        }

        RectTransform objectivePanel;
        Text objectiveText;

        /// <summary>The current story objective, top-right ("" hides it).</summary>
        /// <summary>Fades the party list and the objective (the Esc menu hides them while it zooms in, like the battle
        /// inspect hides the battle HUD). 1 = shown.</summary>
        public void SetHudAlpha(float alpha)
        {
            foreach (var rt in new[] { partyPanel, objectivePanel })
            {
                if (rt == null) continue;
                var group = rt.GetComponent<CanvasGroup>();
                if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
                group.alpha = alpha;
            }
        }

        public void SetObjective(string text)
        {
            if (objectivePanel == null) return;
            objectivePanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            objectiveText.text = text ?? "";
            // Long objectives wrap and the box grows downward to fit them (never shorter than one line's box).
            Canvas.ForceUpdateCanvases();
            float textHeight = Mathf.Max(ObjectiveLineHeight, objectiveText.preferredHeight);
            objectiveText.rectTransform.TopStrip(ObjectiveTextTop, textHeight, 18f);
            objectivePanel.sizeDelta = new Vector2(objectivePanel.sizeDelta.x, ObjectiveTextTop + textHeight + 18f);
        }

        /// <summary>The objective text's top inside its box, and one line's height.</summary>
        const float ObjectiveTextTop = 46f, ObjectiveLineHeight = 54f;

        void BuildObjective(RectTransform root)
        {
            var topRight = new Vector2(1f, 1f);
            // NieR-style: a dark grey plate with thin rules, an accent caption, light text.
            var panel = NierStyle.Panel(root, "Objective", NierStyle.PanelGray);
            objectivePanel = panel.rectTransform;
            objectivePanel.Place(topRight, topRight, new Vector2(-28f, -28f), new Vector2(640f, 118f));
            var label = InkStyle.Label(objectivePanel, "Label", UIText.Get("overworld.objective"), 24, NierStyle.Accent, TextAnchor.UpperLeft);
            label.rectTransform.TopStrip(12f, 34f, 18f);
            objectiveText = UIFactory.CreateText(objectivePanel, "Text", "", 30, NierStyle.Text, TextAnchor.UpperLeft);
            objectiveText.rectTransform.TopStrip(ObjectiveTextTop, ObjectiveLineHeight, 18f);
            objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectiveText.verticalOverflow = VerticalWrapMode.Overflow;
            objectiveText.resizeTextForBestFit = false;
            objectivePanel.gameObject.SetActive(false);
        }

        void Build()
        {
            var root = (RectTransform)transform;
            BuildObjective(root);
            var bottomLeft = Vector2.zero;
            var bottomCenter = new Vector2(0.5f, 0f);
            var center = new Vector2(0.5f, 0.5f);

            // Interact prompt (follows a world point)
            // NieR-style: a dark grey plate with thin rules, light words and an accent key.
            var promptBg = NierStyle.Panel(root, "Prompt", NierStyle.PanelGray);
            promptRect = promptBg.rectTransform;
            // One line, "[F]  Talk": the plate is sized to the words each time (ShowPrompt).
            promptRect.sizeDelta = new Vector2(PromptPadX * 2f, PromptHeight);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptText = UIFactory.CreateText(promptRect, "Text", "", UIFactory.TextSize.Medium, NierStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            promptText.supportRichText = true;
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptText.rectTransform.Fill();
            promptBg.gameObject.SetActive(false);


            // Area title: just the name, in the title screen's doubled terminal type (TerminalTitle), made per arrival.
            titleRoot = UIFactory.CreateRect("Title", root).Place(center, center, new Vector2(0f, 260f), new Vector2(1200f, 160f));
            titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleGroup.blocksRaycasts = false;

            // Party HP (top left): name, big HP number, thin bar.
            partyPanel = UIFactory.CreateRect("Party", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(420f, 0f));

            Dialogue = DialogueBox.Create(root);

            fade = UIFactory.CreateImage(root, "Fade", Color.black);
            fade.rectTransform.Fill();
            fade.color = new Color(0f, 0f, 0f, 0f);
        }

        static readonly Color HudText = NierStyle.HudText;
        static readonly Color HudShadow = new Color(0.08f, 0.07f, 0.05f, 0.85f);
        static Sprite fadeRight;

        /// <summary>
        /// Party rows, top left. Each row: the member's color line, name, an HP bar (the biggest element) and a
        /// small "Lv. N" beneath. The first member is the leader you're controlling: a larger row with a soft
        /// gradient behind it.
        /// </summary>
        public void SetParty(System.Collections.Generic.IList<PartyLine> members)
        {
            UIFactory.ClearChildren(partyPanel);
            float y = 0f;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                bool leader = i == 0;
                float nameSize = leader ? 34f : 27f, barHeight = leader ? 20f : 13f;
                float width = leader ? 420f : 330f;
                float height = nameSize + 6f + barHeight + 4f;
                const float lineX = 4f, contentX = 18f;

                var row = UIFactory.CreateRect(m.Name, partyPanel);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(0f, -y);
                row.sizeDelta = new Vector2(width + contentX, height);

                if (leader)
                {
                    var glow = UIFactory.CreateImage(row, "LeaderGradient", UIFactory.WithAlpha(InkStyle.Ink, 0.7f));
                    glow.sprite = FadeRightSprite();
                    glow.rectTransform.anchorMin = Vector2.zero;
                    glow.rectTransform.anchorMax = Vector2.one;
                    glow.rectTransform.offsetMin = new Vector2(-8f, -6f);
                    glow.rectTransform.offsetMax = new Vector2(60f, 6f);
                }

                var line = UIFactory.CreateImage(row, "Color", m.Color);
                line.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(lineX, 0f), new Vector2(leader ? 6f : 5f, height - 4f));

                var name = InkStyle.Title(row, "Name", m.Name, (int)(nameSize * 1.35f), HudText, TextAnchor.LowerLeft);
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(contentX, 0f), new Vector2(width, nameSize + 4f));
                name.gameObject.AddComponent<Outline>().effectColor = HudShadow;

                bool low = m.HP <= m.MaxHP / 4;
                var track = UIFactory.CreateImage(row, "Bar", new Color(0f, 0f, 0f, 0.5f));
                track.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(contentX, -(nameSize + 6f)), new Vector2(width, barHeight));
                track.gameObject.AddComponent<Outline>().effectColor = HudShadow;
                var fill = UIFactory.CreateImage(track.transform, "Fill", low ? new Color(0.92f, 0.32f, 0.3f) : new Color(0.4f, 0.85f, 0.5f));
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(m.HP / (float)Mathf.Max(1, m.MaxHP)), 1f);
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;

                // The level on the name line, right-aligned, as big as the name and in gold so it reads at a glance.
                var lv = InkStyle.Title(row, "Level", "<size=" + UIFactory.Px((int)(nameSize * 0.8f)) + ">" + UIText.Get("common.lv") + "</size> " + m.Level,
                    (int)(nameSize * 1.35f), NierStyle.HudText, TextAnchor.LowerRight);
                lv.supportRichText = true;
                lv.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(contentX, 0f), new Vector2(width, nameSize + 4f));
                lv.gameObject.AddComponent<Outline>().effectColor = HudShadow;

                y += height + (leader ? 18f : 10f);
            }
        }

        /// <summary>Horizontal alpha ramp: solid on the left, clear on the right.</summary>
        static Sprite FadeRightSprite()
        {
            if (fadeRight != null) return fadeRight;
            const int w = 64;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - t * t));
            }
            tex.Apply();
            fadeRight = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 16f);
            return fadeRight;
        }

        const float PromptPadX = 22f, PromptHeight = 56f;

        public void ShowPrompt(string key, string text, Vector3 worldPosition, Camera cam)
        {
            if (cam == null) return;
            var screen = PixelCamera.ToScreen(cam, cam.WorldToScreenPoint(worldPosition));   // the world may be drawn small (Track C)
            if (screen.z < 0f)
            {
                HidePrompt();
                return;
            }
            if (!promptRect.gameObject.activeSelf) promptRect.gameObject.SetActive(true);   // first, so it can measure
            string line = "<color=#B85C3D>[" + key + "]</color>  " + text;   // NieR accent
            if (promptText.text != line)
            {
                promptText.text = line;
                promptRect.sizeDelta = new Vector2(promptText.preferredWidth + PromptPadX * 2f, PromptHeight);
            }
            promptRect.position = screen;
        }

        public void HidePrompt()
        {
            if (promptRect.gameObject.activeSelf) promptRect.gameObject.SetActive(false);
        }

        /// <summary>Plays a conversation; <paramref name="done"/> runs after the last line closes.</summary>
        public void Talk(System.Collections.Generic.IList<DialogueLine> lines, System.Action done = null)
        {
            HidePrompt();
            Dialogue.Play(lines, done);
        }

        /// <summary>A single line from one speaker.</summary>
        public void Say(string speaker, string text, DialogueVoice voice, Sprite portrait = null, System.Action done = null)
        {
            Talk(new[] { new DialogueLine(speaker, text, voice, portrait) }, done);
        }

        /// <summary>The area title's type size, and the widest it may be (long names shrink to fit).</summary>
        const int TitleSize = 96;
        const float TitleWidth = 1100f;

        /// <summary>The area's name on arrival, like the game's title on the title screen: it types itself in behind a
        /// blinking cursor over its gold echo, holds, and fades. Just the name (the subtitle isn't shown).</summary>
        public IEnumerator ShowTitle(string title, string subtitle)
        {
            UIFactory.ClearChildren(titleRoot);
            var card = TerminalTitle.Create(titleRoot, title, TitleSize, Vector2.zero, 140f);
            if (card.Width > TitleWidth)
            {
                // Too long for the screen: the same card a size smaller, so it fits.
                int size = Mathf.Max(48, Mathf.FloorToInt(TitleSize * TitleWidth / card.Width));
                card.gameObject.SetActive(false);   // gone at the end of the frame; hidden until then
                UIFactory.ClearChildren(titleRoot);
                card = TerminalTitle.Create(titleRoot, title, size, Vector2.zero, 140f);
            }
            titleGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(card.TypingSeconds + 1.6f);
            yield return Tween.Run(0.8f, p => titleGroup.alpha = 1f - p, Tween.InOutQuad);
            UIFactory.ClearChildren(titleRoot);
        }

        public void SetFade(Color color)
        {
            fade.color = color;
        }

        public IEnumerator FadeTo(Color color, float duration)
        {
            yield return Tween.ColorTo(fade, color, duration);
        }
    }
}
