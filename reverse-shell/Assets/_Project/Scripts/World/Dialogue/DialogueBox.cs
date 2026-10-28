using System;
using System.Collections;
using System.Collections.Generic;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// Conversation box, one default look for every character (Reverse: 1999 style, <see cref="InkStyle"/>): a dark
    /// ink panel with a gold hairline frame and a thin bar in the speaker's accent colour, the speaker's name in gold
    /// serif with a short accent underline, animated text (<see cref="DynamicText"/>) in paper white, a bobbing gold
    /// "next" diamond and per-character voice blips. No portraits. Everything animates on unscaled time.
    ///
    /// Advance(): finishes the current line if it's still typing, otherwise moves on (closing after the last).
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        static readonly Color Panel = new Color(0.075f, 0.07f, 0.065f, 0.94f);
        const float BoxW = 1320f, BoxH = 236f;

        RectTransform root, label, portraitFrame, nextArrow;
        Image accentBar, tagAccent;
        Text speakerText, bodyText;
        DynamicText dynamic;
        CanvasGroup group;
        AudioSource voice;
        const float VoiceVolume = 0.22f;   // the blips at full Sound Effects volume
        static AudioClip blip;

        readonly List<DialogueLine> queue = new List<DialogueLine>();
        int index;
        Action onDone;

        // Choices (yes / no style) shown after the last line; Up/Down (or W/S, Left/Right) pick, F/Space/Enter confirm.
        string[] choices;
        Action<int> onChoice;
        int choiceIndex, pickedChoice = -1;
        RectTransform choiceRoot;
        readonly List<Image> choiceBacks = new List<Image>();
        readonly List<Text> choiceTexts = new List<Text>();
        int lastBlipCount;
        float openT, labelPop;
        bool closing;

        public bool IsOpen { get { return gameObject.activeSelf && !closing; } }

        public static DialogueBox Create(RectTransform parent)
        {
            var rt = UIFactory.CreateRect("Dialogue", parent).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(BoxW, BoxH));
            var box = rt.gameObject.AddComponent<DialogueBox>();
            box.Build(rt);
            rt.gameObject.SetActive(false);
            return box;
        }

        void Build(RectTransform rt)
        {
            root = rt;
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            // The ink panel with a gold hairline frame.
            var paper = UIFactory.CreateImage(rt, "Panel", Panel);
            paper.rectTransform.Fill();
            InkStyle.Frame(paper.rectTransform, UIFactory.WithAlpha(InkStyle.Gold, 0.55f), 1f, 6f);

            accentBar = UIFactory.CreateImage(paper.transform, "Accent", InkStyle.Gold);
            accentBar.rectTransform.anchorMin = Vector2.zero;
            accentBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            accentBar.rectTransform.offsetMin = Vector2.zero;
            accentBar.rectTransform.offsetMax = new Vector2(5f, 0f);

            // No portraits any more (kept as an always-hidden frame so line data with a portrait still works).
            portraitFrame = UIFactory.CreateRect("Portrait", rt);
            portraitFrame.gameObject.SetActive(false);

            bodyText = UIFactory.CreateText(paper.transform, "Text", "", 40, InkStyle.Paper, TextAnchor.UpperLeft);
            bodyText.font = UIFactory.MediumFont;
            bodyText.lineSpacing = 1.1f;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            dynamic = bodyText.gameObject.AddComponent<DynamicText>();

            // Speaker: the name in gold serif inside the top-left of the panel, with a short underline in their accent.
            label = UIFactory.CreateImage(rt, "Speaker", new Color(0f, 0f, 0f, 0f)).rectTransform;
            label.pivot = new Vector2(0f, 1f);
            label.anchorMin = label.anchorMax = new Vector2(0f, 1f);
            speakerText = UIFactory.CreateText(label, "Name", "", 44, InkStyle.GoldBright, TextAnchor.MiddleLeft);
            speakerText.font = UIFactory.SerifFont;
            speakerText.rectTransform.Fill();
            tagAccent = UIFactory.CreateImage(label, "Underline", Color.white);
            tagAccent.rectTransform.anchorMin = Vector2.zero;
            tagAccent.rectTransform.anchorMax = new Vector2(1f, 0f);
            tagAccent.rectTransform.pivot = new Vector2(0.5f, 1f);
            tagAccent.rectTransform.offsetMin = new Vector2(0f, -2f);
            tagAccent.rectTransform.offsetMax = new Vector2(0f, 0f);

            // "Next": a small gold diamond bobbing in the bottom-right corner.
            nextArrow = UIFactory.CreateImage(paper.transform, "Next", InkStyle.Gold).rectTransform;
            nextArrow.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-40f, 34f), new Vector2(20f, 20f));
            nextArrow.localRotation = Quaternion.Euler(0f, 0f, 45f);

            voice = rt.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = VoiceVolume * GameSettings.SfxVolume;
            voice.clip = Blip();
        }

        // ------------------------------------------------------------------ API

        /// <summary>
        /// Shows one line, then a list of options. <paramref name="chosen"/> gets the picked index once the box has
        /// closed. Esc / Backspace pick the last option (treat it as "cancel").
        /// </summary>
        public void Ask(DialogueLine line, string[] options, Action<int> chosen)
        {
            Play(new[] { line }, null);
            choices = options;
            onChoice = chosen;
            choiceIndex = 0;
            BuildChoices();
        }

        public void Play(IList<DialogueLine> lines, Action done)
        {
            choices = null;
            onChoice = null;
            pickedChoice = -1;
            if (choiceRoot != null) choiceRoot.gameObject.SetActive(false);
            queue.Clear();
            // Long lines become several boxes (DialoguePager), so nothing runs off the bottom of the box.
            gameObject.SetActive(true);
            foreach (var l in lines)
            {
                if (l == null) continue;
                SizeTextArea(!string.IsNullOrEmpty(l.Speaker));
                foreach (var page in DialoguePager.Split(l.Text, Fits))
                    queue.Add(new DialogueLine(l.Speaker, page, l.Voice, l.Portrait));
            }
            onDone = done;
            if (queue.Count == 0)
            {
                Finish();
                return;
            }
            StopAllCoroutines(); // a new conversation cancels a box that's still closing
            closing = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openT = 0f;
            index = 0;
            ShowLine(true);
        }

        public void Advance()
        {
            if (!IsOpen) return;
            if (!dynamic.FullyRevealed)
            {
                dynamic.Complete();
                return;
            }
            if (choices != null && index == queue.Count - 1)
            {
                pickedChoice = choiceIndex;
                StartCoroutine(CloseRoutine());
                return;
            }
            index++;
            if (index >= queue.Count)
            {
                StartCoroutine(CloseRoutine());
                return;
            }
            ShowLine(false);
        }

        /// <summary>The text area inside the box (smaller under a speaker name).</summary>
        void SizeTextArea(bool hasSpeaker)
        {
            var trt = bodyText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(48f, 30f);
            trt.offsetMax = new Vector2(-70f, hasSpeaker ? -76f : -30f);
        }

        /// <summary>Whether plain text fits the box's text area at the current size.</summary>
        bool Fits(string plain)
        {
            var rect = bodyText.rectTransform.rect;
            if (rect.width <= 1f || rect.height <= 1f) return true;
            var settings = bodyText.GetGenerationSettings(new Vector2(rect.width, 0f));
            float needed = bodyText.cachedTextGeneratorForLayout.GetPreferredHeight(plain, settings) / bodyText.pixelsPerUnit;
            return needed <= rect.height + 0.5f;
        }

        void BuildChoices()
        {
            if (choiceRoot == null)
            {
                choiceRoot = UIFactory.CreateRect("Choices", root);
                choiceRoot.anchorMin = choiceRoot.anchorMax = new Vector2(1f, 1f);
                choiceRoot.pivot = new Vector2(1f, 0f);
                choiceRoot.anchoredPosition = new Vector2(-30f, 16f);
            }
            UIFactory.ClearChildren(choiceRoot);
            choiceBacks.Clear();
            choiceTexts.Clear();
            const float rowH = 58f, width = 300f;
            choiceRoot.sizeDelta = new Vector2(width, choices.Length * (rowH + 10f));
            for (int i = 0; i < choices.Length; i++)
            {
                // Stacked ink rows (the last one on the bottom) with a gold hairline.
                float y = (choices.Length - 1 - i) * (rowH + 10f);
                var back = UIFactory.CreateImage(choiceRoot, choices[i], Panel);
                back.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, y), new Vector2(width, rowH));
                InkStyle.Frame(back.rectTransform, UIFactory.WithAlpha(InkStyle.Gold, 0.55f), 1f);
                var label = UIFactory.CreateText(back.transform, "Label", choices[i], 38, InkStyle.Paper, TextAnchor.MiddleCenter);
                label.font = UIFactory.SerifFont;
                label.rectTransform.Fill();
                choiceBacks.Add(back);
                choiceTexts.Add(label);
            }
            choiceRoot.gameObject.SetActive(false);
        }

        void UpdateChoices()
        {
            if (choiceRoot == null) return;
            bool show = choices != null && !closing && index == queue.Count - 1 && dynamic.FullyRevealed;
            if (choiceRoot.gameObject.activeSelf != show) choiceRoot.gameObject.SetActive(show);
            if (!show) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                choiceIndex = (choiceIndex - 1 + choices.Length) % choices.Length;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                choiceIndex = (choiceIndex + 1) % choices.Length;
            if (UIKeys.Back || Input.GetKeyDown(KeyCode.Backspace))
            {
                choiceIndex = choices.Length - 1;
                Advance();
                return;
            }
            for (int i = 0; i < choiceBacks.Count; i++)
            {
                bool on = i == choiceIndex;
                choiceBacks[i].color = on ? UIFactory.WithAlpha(InkStyle.Gold, 0.35f) : Panel;
                choiceTexts[i].color = on ? InkStyle.GoldBright : InkStyle.Paper;
                choiceBacks[i].rectTransform.localScale = Vector3.one * (on ? 1.05f : 1f);
            }
        }

        void ShowLine(bool first)
        {
            var line = queue[index];
            bool newSpeaker = first || queue[index - 1].Speaker != line.Speaker;

            accentBar.color = line.Voice.Accent;
            tagAccent.color = line.Voice.Accent;
            speakerText.text = line.Speaker ?? "";
            bool hasSpeaker = !string.IsNullOrEmpty(line.Speaker);
            label.gameObject.SetActive(hasSpeaker);
            label.sizeDelta = new Vector2(Mathf.Max(80f, speakerText.preferredWidth + 4f), 48f);
            label.anchoredPosition = new Vector2(48f, -16f);
            if (newSpeaker) labelPop = 0f;

            SizeTextArea(hasSpeaker);

            dynamic.SetLine(DialogueMarkup.Parse(line.Text, line.Voice.Speed));
            lastBlipCount = 0;
        }

        // ------------------------------------------------------------------ Animation

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            openT = Mathf.Min(1f, openT + dt / 0.24f);
            labelPop = Mathf.Min(1f, labelPop + dt / 0.18f);

            if (!closing)
            {
                // A gentle rise and fade in (no tilt, no bounce).
                float e = 1f - (1f - openT) * (1f - openT) * (1f - openT);
                root.anchoredPosition = new Vector2(0f, Mathf.Lerp(30f, 70f, e));
                group.alpha = e;
            }
            label.localScale = Vector3.one;
            var nameColor = speakerText.color;
            nameColor.a = Mathf.Clamp01(labelPop);
            speakerText.color = nameColor;

            bool done = dynamic.FullyRevealed;
            nextArrow.gameObject.SetActive(done && !closing && choices == null);
            UpdateChoices();
            if (done) nextArrow.anchoredPosition = new Vector2(-40f, 34f + Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 3f));

            // Voice: a blip every other letter as it appears.
            if (dynamic.RevealedCount > lastBlipCount && index < queue.Count)
            {
                string s = dynamic.PlainText;
                for (int i = lastBlipCount; i < dynamic.RevealedCount && i < s.Length; i++)
                {
                    if (!char.IsLetterOrDigit(s[i]) || i % 2 != 0) continue;
                    voice.pitch = queue[index].Voice.Pitch * UnityEngine.Random.Range(0.94f, 1.06f);
                    voice.volume = VoiceVolume * GameSettings.SfxVolume;
                    voice.Play();
                    break;
                }
                lastBlipCount = dynamic.RevealedCount;
            }
        }

        IEnumerator CloseRoutine()
        {
            closing = true;
            float t = 0f;
            var start = root.anchoredPosition;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.14f);
                root.anchoredPosition = Vector2.Lerp(start, start + new Vector2(0f, -50f), t);
                group.alpha = 1f - t;
                yield return null;
            }
            gameObject.SetActive(false);
            closing = false;
            Finish();
        }

        void Finish()
        {
            var done = onDone;
            onDone = null;
            if (done != null) done();
            var chosen = onChoice;
            int pick = pickedChoice;
            onChoice = null;
            choices = null;
            if (chosen != null && pick >= 0) chosen(pick);
        }

        /// <summary>A short, soft square-wave blip; each speaker plays it at their own pitch.</summary>
        static AudioClip Blip()
        {
            if (blip != null) return blip;
            const int rate = 22050;
            const float seconds = 0.05f, frequency = 520f;
            int n = Mathf.RoundToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t));
                float sine = Mathf.Sin(2f * Mathf.PI * frequency * 2f * t);
                float envelope = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 55f);
                data[i] = (square * 0.6f + sine * 0.4f) * envelope * 0.5f;
            }
            blip = AudioClip.Create("DialogueBlip", n, 1, rate, false);
            blip.SetData(data, 0);
            return blip;
        }
    }
}
