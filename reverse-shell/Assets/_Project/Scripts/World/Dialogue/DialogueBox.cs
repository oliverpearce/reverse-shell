using System;
using System.Collections;
using System.Collections.Generic;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// Conversation box in the style of UNBEATABLE's white labels, with Celeste-style text:
    /// a tilted white label with a hard black drop shadow, a black speaker tag (Anton, the speaker's accent
    /// color underneath), an optional portrait, animated text (<see cref="DynamicText"/>), a bobbing "next"
    /// arrow and per-character voice blips. Everything animates on unscaled time.
    ///
    /// Advance(): finishes the current line if it's still typing, otherwise moves on (closing after the last).
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        static readonly Color Paper = new Color(0.98f, 0.98f, 0.96f);
        static readonly Color Ink = new Color(0.06f, 0.06f, 0.07f);
        const float Tilt = -1.6f;
        const float BoxW = 1320f, BoxH = 236f;

        RectTransform root, label, portraitFrame, nextArrow;
        Image accentBar, tagAccent;
        string posterFor;
        Text speakerText, bodyText;
        DynamicText dynamic;
        CanvasGroup group;
        AudioSource voice;
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
        float openT, labelPop, portraitBounce;
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

            // Hard drop shadow, then the white label.
            var shadow = UIFactory.CreateImage(rt, "Shadow", Ink);
            shadow.rectTransform.Fill();
            shadow.rectTransform.anchoredPosition = new Vector2(14f, -14f);
            var paper = UIFactory.CreateImage(rt, "Label", Paper);
            paper.rectTransform.Fill();

            accentBar = UIFactory.CreateImage(paper.transform, "Accent", Ink);
            accentBar.rectTransform.anchorMin = Vector2.zero;
            accentBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            accentBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            accentBar.rectTransform.offsetMin = Vector2.zero;
            accentBar.rectTransform.offsetMax = new Vector2(16f, 0f);

            // Portrait (left): a compact holographic profile poster, tilted and sticking up out of the box.
            portraitFrame = UIFactory.CreateRect("Portrait", rt).Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(152f, 34f), new Vector2(232f, 286f));
            portraitFrame.localRotation = Quaternion.Euler(0f, 0f, 3.5f);

            bodyText = UIFactory.CreateText(paper.transform, "Text", "", 34, Ink, TextAnchor.UpperLeft);
            bodyText.font = UIFactory.MediumFont;
            bodyText.lineSpacing = 1.1f;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            dynamic = bodyText.gameObject.AddComponent<DynamicText>();

            // Speaker tag: a black label stuck over the top-left corner.
            label = UIFactory.CreateImage(rt, "SpeakerTag", Ink).rectTransform;
            label.pivot = new Vector2(0f, 0f);
            label.anchorMin = label.anchorMax = new Vector2(0f, 1f);
            label.localRotation = Quaternion.Euler(0f, 0f, 3f);
            speakerText = UIFactory.CreateText(label, "Name", "", 40, Color.white, TextAnchor.MiddleCenter);
            speakerText.font = UIFactory.DisplayFont;
            speakerText.rectTransform.Fill();
            tagAccent = UIFactory.CreateImage(label, "TagAccent", Color.white);
            tagAccent.rectTransform.anchorMin = Vector2.zero;
            tagAccent.rectTransform.anchorMax = new Vector2(1f, 0f);
            tagAccent.rectTransform.pivot = new Vector2(0.5f, 1f);
            tagAccent.rectTransform.offsetMin = new Vector2(0f, -8f);
            tagAccent.rectTransform.offsetMax = new Vector2(0f, 0f);

            // "Next" arrow: a small black diamond bobbing in the bottom-right corner.
            nextArrow = UIFactory.CreateImage(paper.transform, "Next", Ink).rectTransform;
            nextArrow.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-40f, 34f), new Vector2(20f, 20f));
            nextArrow.localRotation = Quaternion.Euler(0f, 0f, 45f);

            voice = rt.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 0.22f;
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
            foreach (var l in lines) if (l != null) queue.Add(l);
            posterFor = null;
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
                // Stacked labels, the last one on the bottom, each with its own hard shadow.
                float y = (choices.Length - 1 - i) * (rowH + 10f);
                var shadow = UIFactory.CreateImage(choiceRoot, "Shadow", Ink);
                shadow.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, y - 8f), new Vector2(width, rowH));
                var back = UIFactory.CreateImage(choiceRoot, choices[i], Paper);
                back.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, y), new Vector2(width, rowH));
                var label = UIFactory.CreateText(back.transform, "Label", choices[i].ToUpperInvariant(), 34, Ink, TextAnchor.MiddleCenter);
                label.font = UIFactory.DisplayFont;
                label.rectTransform.Fill();
                choiceBacks.Add(back);
                choiceTexts.Add(label);
            }
            choiceRoot.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
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
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                choiceIndex = choices.Length - 1;
                Advance();
                return;
            }
            for (int i = 0; i < choiceBacks.Count; i++)
            {
                bool on = i == choiceIndex;
                choiceBacks[i].color = on ? Ink : Paper;
                choiceTexts[i].color = on ? Paper : Ink;
                choiceBacks[i].rectTransform.localScale = Vector3.one * (on ? 1.05f : 1f);
            }
        }

        void ShowLine(bool first)
        {
            var line = queue[index];
            bool newSpeaker = first || queue[index - 1].Speaker != line.Speaker;

            accentBar.color = line.Voice.Accent;
            tagAccent.color = line.Voice.Accent;
            speakerText.text = (line.Speaker ?? "").ToUpperInvariant();
            label.gameObject.SetActive(!string.IsNullOrEmpty(line.Speaker));
            float width = Mathf.Max(140f, speakerText.preferredWidth + 56f);
            label.sizeDelta = new Vector2(width, 64f);
            label.anchoredPosition = new Vector2(36f, -18f);
            if (newSpeaker) labelPop = 0f;

            bool hasPortrait = line.Portrait != null;
            portraitFrame.gameObject.SetActive(hasPortrait);
            if (hasPortrait && posterFor != line.Speaker)
            {
                posterFor = line.Speaker;
                for (int i = portraitFrame.childCount - 1; i >= 0; i--) Destroy(portraitFrame.GetChild(i).gameObject);
                var portrait = ProfilePoster.Build(portraitFrame, new PosterInfo { Name = line.Speaker, Accent = line.Voice.Accent }, true);
                portrait.sprite = line.Portrait;
                portrait.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -112f), new Vector2(320f, 320f)); // 64px frame at 5x: head and shoulders
            }
            var trt = bodyText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(hasPortrait ? 300f : 56f, 30f);
            trt.offsetMax = new Vector2(-70f, -58f);

            dynamic.SetLine(DialogueMarkup.Parse(line.Text, line.Voice.Speed));
            lastBlipCount = 0;
        }

        // ------------------------------------------------------------------ Animation

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            openT = Mathf.Min(1f, openT + dt / 0.24f);
            labelPop = Mathf.Min(1f, labelPop + dt / 0.18f);
            portraitBounce = Mathf.MoveTowards(portraitBounce, 0f, dt * 6f);

            if (!closing)
            {
                float e = BackOut(openT);
                root.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-60f, 70f, e));
                root.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-7f, Tilt, e));
                group.alpha = Mathf.Clamp01(openT * 2f);
            }
            label.localScale = Vector3.one * BackOut(labelPop);
            portraitFrame.localScale = Vector3.one * (1f + 0.05f * portraitBounce);

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
                    voice.Play();
                    portraitBounce = 1f;
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

        static float BackOut(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
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
