using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The tutorials (Settings → Tutorials on the title screen, in the Esc menu and in the battle pause menu): a
    /// full-screen ink page with two tabs, Controls (the keys) and Battle (how fights work). Each tab is a topic list on
    /// the left and the chosen topic on the right, in the game's normal text size (dialogue size, 40). The layout
    /// stretches with the window, so it fits at 4:3 too. All of its text lives in Resources/Story/Guide.txt.
    /// ← → (or A/D, or a click) switch tabs, ↑ ↓ (or W/S, the mouse) pick a topic, Esc / P / Backspace closes.
    /// It runs on unscaled time over the paused game; while it's open (and on the frame it closes) the menus under it
    /// ignore their keys (<see cref="Blocking"/>).
    /// </summary>
    public class GuideBook : MonoBehaviour
    {
        public const int ControlsTab = 0, BattleTab = 1;
        static string[] TabNames { get { return new[] { UIText.Get("guide.tab.controls"), UIText.Get("guide.tab.battle") }; } }
        static readonly string[] TabKeys = { "controls", "battle" };

        static GuideBook current;
        static int closedFrame = -1;

        /// <summary>The guide is open, or closed this frame (so the Esc that closed it isn't read again underneath).</summary>
        public static bool Blocking { get { return current != null || closedFrame == Time.frameCount; } }

        class Section
        {
            public string Heading;
            public string Body;
        }

        readonly List<Section>[] topics = { new List<Section>(), new List<Section>() };
        readonly int[] chosen = new int[2];   // the topic picked on each tab

        RectTransform list;
        readonly List<Image> tabPlates = new List<Image>();
        readonly List<RectTransform> tabFrames = new List<RectTransform>();
        readonly List<Image> topicPlates = new List<Image>();
        readonly List<RectTransform> topicFrames = new List<RectTransform>();
        Text topicTitle, topicBody;
        int tab, openedFrame;
        Action onClose;

        /// <summary>Opens the guide on top of everything on <paramref name="from"/>'s canvas.</summary>
        /// <param name="onClose">Runs after it closes (e.g. to give the keys back to the menu underneath).</param>
        public static GuideBook Open(Transform from, int tab = ControlsTab, Action onClose = null)
        {
            if (current != null) return current;
            var canvas = from.GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.rootCanvas.transform : from;
            var rt = UIFactory.CreateRect("Guide", parent).Fill();
            var book = rt.gameObject.AddComponent<GuideBook>();
            current = book;
            book.onClose = onClose;
            book.openedFrame = Time.frameCount;
            book.Load();
            book.Build(rt);
            book.ShowTab(tab);
            rt.SetAsLastSibling();
            return book;
        }

        public void Close()
        {
            closedFrame = Time.frameCount;
            if (current == this) current = null;
            Destroy(gameObject);
            if (onClose != null) onClose();
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        // ------------------------------------------------------------------ Text (Resources/Story/Guide.txt)

        const string GuidePath = "Story/Guide";

        /// <summary>"[controls] Topic" and "[battle] Topic" start a topic on that tab; the lines under it are its text.</summary>
        void Load()
        {
            var asset = Resources.Load<TextAsset>(GuidePath);
            if (asset == null)
            {
                Debug.LogWarning("Guide: Resources/" + GuidePath + ".txt is missing.");
                return;
            }
            List<Section> into = null;
            Section section = null;
            var body = new StringBuilder();
            Action finish = () =>
            {
                if (section == null) return;
                section.Body = UIText.Markup(body.ToString().Trim('\n', '\r', ' '));
                into.Add(section);
                section = null;
                body.Length = 0;
            };
            foreach (var raw in asset.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("#")) continue;
                if (line.StartsWith("["))
                {
                    int end = line.IndexOf(']');
                    if (end > 0)
                    {
                        finish();
                        int which = Array.IndexOf(TabKeys, line.Substring(1, end - 1).Trim().ToLowerInvariant());
                        into = which >= 0 ? topics[which] : null;
                        if (into != null) section = new Section { Heading = line.Substring(end + 1).Trim() };
                        continue;
                    }
                }
                if (section != null) body.Append(line).Append('\n');
            }
            finish();
        }

        // ------------------------------------------------------------------ Layout

        /// <summary>The normal text size (the dialogue box's); the fit only steps down a little if a topic runs long.</summary>
        const int TextSize = 40, MinTextSize = 34;
        const float Side = 60f, ContentTop = 196f, ContentBottom = 110f;
        const float TabWidth = 400f, TabHeight = 96f;
        const float ListWidth = 500f, TopicHeight = 80f, TopicStep = 90f, Gap = 30f;

        void Build(RectTransform root)
        {
            // Nearly opaque, so the menu underneath doesn't show through.
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.985f), true);
            dim.rectTransform.Fill();
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.035f));

            var top = new Vector2(0.5f, 1f);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int captured = i;
                float x = (i - (TabNames.Length - 1) * 0.5f) * (TabWidth + 20f);
                RectTransform frame;
                var plate = Plate(root, "Tab" + i, TabNames[i], 48, out frame, () => ShowTab(captured), null);
                plate.rectTransform.Place(top, top, new Vector2(x, -50f), new Vector2(TabWidth, TabHeight));
                tabPlates.Add(plate);
                tabFrames.Add(frame);
            }
            var rule = InkStyle.Line(root, "Rule", InkStyle.Rule);
            rule.rectTransform.TopStrip(166f, 2f, Side);

            // Everything below stretches with the window: the list keeps its width, the topic panel takes the rest.
            var content = UIFactory.CreateRect("Content", root);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(Side, ContentBottom);
            content.offsetMax = new Vector2(-Side, -ContentTop);

            var tl = new Vector2(0f, 1f);
            list = UIFactory.CreateRect("Topics", content).Fill();
            list.anchorMax = tl;
            list.offsetMax = new Vector2(ListWidth, 0f);

            var panel = InkStyle.Panel(content, "Topic", InkStyle.InkPanel);
            panel.rectTransform.Fill();
            panel.rectTransform.offsetMin = new Vector2(ListWidth + Gap, 0f);
            InkStyle.Frame(panel.rectTransform, InkStyle.Rule, 2f);
            topicTitle = InkStyle.Title(panel.transform, "Title", "", 56, InkStyle.Paper, TextAnchor.MiddleLeft);
            topicTitle.rectTransform.TopStrip(20f, 84f, 44f);
            var line = InkStyle.Line(panel.transform, "Rule", UIFactory.WithAlpha(InkStyle.Gold, 0.5f));
            line.rectTransform.TopStrip(108f, 2f, 44f);
            topicBody = UIFactory.CreateText(panel.transform, "Body", "", TextSize, InkStyle.Paper, TextAnchor.UpperLeft);
            topicBody.rectTransform.Fill();
            topicBody.rectTransform.offsetMin = new Vector2(44f, 28f);
            topicBody.rectTransform.offsetMax = new Vector2(-44f, -132f);

            var keys = UIFactory.CreateText(root, "Keys",
                UIText.Get("guide.keys"),
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 50f));
        }

        /// <summary>An ink plate with corner brackets (a tab or a topic): click runs it, hovering runs <paramref name="onHover"/>.</summary>
        static Image Plate(Transform parent, string name, string label, int size, out RectTransform frame, Action onClick, Action onHover)
        {
            var plate = UIFactory.CreateImage(parent, name, InkStyle.InkPanel, true);
            frame = InkStyle.Frame(plate.rectTransform, InkStyle.Rule, 2f);
            var t = InkStyle.Title(plate.transform, "Label", label, size, InkStyle.Paper, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(8f);
            t.BestFit(30, size);

            var trigger = plate.gameObject.AddComponent<EventTrigger>();
            if (onHover != null)
            {
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => onHover());
                trigger.triggers.Add(enter);
            }
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => onClick());
            trigger.triggers.Add(click);
            return plate;
        }

        static void Highlight(Image plate, RectTransform frame, bool on)
        {
            plate.color = on ? InkStyle.HoverGray : InkStyle.InkPanel;
            foreach (var edge in frame.GetComponentsInChildren<Image>())
                edge.color = on ? InkStyle.Paper : InkStyle.Rule;
        }

        // ------------------------------------------------------------------ Tabs and topics

        void ShowTab(int which)
        {
            tab = (which % TabNames.Length + TabNames.Length) % TabNames.Length;
            for (int i = 0; i < tabPlates.Count; i++) Highlight(tabPlates[i], tabFrames[i], i == tab);

            UIFactory.ClearChildren(list);
            topicPlates.Clear();
            topicFrames.Clear();
            var tl = new Vector2(0f, 1f);
            var page = topics[tab];
            for (int i = 0; i < page.Count; i++)
            {
                int captured = i;
                RectTransform frame;
                var plate = Plate(list, "Topic" + i, page[i].Heading, TextSize, out frame, () => SelectTopic(captured), () => SelectTopic(captured));
                plate.rectTransform.Place(tl, tl, new Vector2(0f, -i * TopicStep), new Vector2(ListWidth, TopicHeight));
                topicPlates.Add(plate);
                topicFrames.Add(frame);
            }
            SelectTopic(chosen[tab]);
        }

        void SelectTopic(int i)
        {
            var page = topics[tab];
            if (page.Count == 0)
            {
                topicTitle.text = "";
                topicBody.text = "";
                return;
            }
            int topic = (i % page.Count + page.Count) % page.Count;
            chosen[tab] = topic;
            for (int r = 0; r < topicPlates.Count; r++) Highlight(topicPlates[r], topicFrames[r], r == topic);
            topicTitle.text = page[topic].Heading;
            string body = page[topic].Body;
            UIFactory.FitRich(topicBody, _ => body, MinTextSize, TextSize);
        }

        // ------------------------------------------------------------------ Input

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            if (UIKeys.Back || UIKeys.Pressed(KeyCode.Backspace))
            {
                Close();
                return;
            }
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) ShowTab(tab - 1);
            else if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) ShowTab(tab + 1);
            else if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) SelectTopic(chosen[tab] - 1);
            else if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) SelectTopic(chosen[tab] + 1);
        }
    }
}
