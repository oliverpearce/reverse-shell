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
        Text titleText, subtitleText;
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
        }

        void BuildObjective(RectTransform root)
        {
            var topRight = new Vector2(1f, 1f);
            var panel = InkStyle.Panel(root, "Objective", UIFactory.WithAlpha(InkStyle.Ink, 0.78f));
            objectivePanel = panel.rectTransform;
            objectivePanel.Place(topRight, topRight, new Vector2(-28f, -28f), new Vector2(640f, 118f));
            InkStyle.Frame(objectivePanel, InkStyle.Rule, 2f);
            var label = InkStyle.Label(objectivePanel, "Label", UIText.Get("overworld.objective"), 24, InkStyle.Gold, TextAnchor.UpperLeft);
            label.rectTransform.TopStrip(10f, 34f, 18f);
            objectiveText = UIFactory.CreateText(objectivePanel, "Text", "", 30, InkStyle.Paper, TextAnchor.UpperLeft);
            objectiveText.rectTransform.TopStrip(44f, 66f, 18f);
            objectiveText.gameObject.AddComponent<Outline>().effectColor = HudShadow;
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
            // An ink box with a gold hairline and a gold key: simple and readable.
            var promptBg = InkStyle.Panel(root, "Prompt", InkStyle.InkPanel);
            promptRect = promptBg.rectTransform;
            promptRect.sizeDelta = new Vector2(320f, 76f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            InkStyle.Frame(promptRect, InkStyle.Rule, 2f);
            promptText = UIFactory.CreateText(promptRect, "Text", "", 40, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(18, 40);
            promptText.supportRichText = true;
            promptText.rectTransform.Fill(10f);
            promptBg.gameObject.SetActive(false);


            // Area title
            var title = UIFactory.CreateRect("Title", root).Place(center, center, new Vector2(0f, 260f), new Vector2(1200f, 160f));
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            // Area title: a big serif name between two gold rules, a spaced-caps subtitle beneath.
            var band = InkStyle.Panel(title, "Band", InkStyle.Smoke);
            band.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(1100f, 172f));
            var ruleTop = InkStyle.Line(title, "RuleTop", InkStyle.Gold);
            ruleTop.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(760f, 2f));
            titleText = InkStyle.Title(title, "Text", "", 96, InkStyle.Paper, TextAnchor.MiddleCenter);
            titleText.rectTransform.TopStrip(6f, 112f);
            var ruleBottom = InkStyle.Line(title, "RuleBottom", InkStyle.Gold);
            ruleBottom.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(760f, 2f));
            subtitleText = UIFactory.CreateText(title, "Subtitle", "", 32, InkStyle.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            subtitleText.rectTransform.TopStrip(128f, 40f);

            // Party HP (top left): name, big HP number, thin bar.
            partyPanel = UIFactory.CreateRect("Party", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(420f, 0f));

            Dialogue = DialogueBox.Create(root);

            fade = UIFactory.CreateImage(root, "Fade", Color.black);
            fade.rectTransform.Fill();
            fade.color = new Color(0f, 0f, 0f, 0f);
        }

        static readonly Color HudText = InkStyle.Paper;
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
                    (int)(nameSize * 1.35f), InkStyle.GoldBright, TextAnchor.LowerRight);
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

        public void ShowPrompt(string key, string text, Vector3 worldPosition, Camera cam)
        {
            if (cam == null) return;
            var screen = cam.WorldToScreenPoint(worldPosition);
            if (screen.z < 0f)
            {
                HidePrompt();
                return;
            }
            promptText.text = "<color=#C2A061>[" + key + "]</color>  " + text;
            promptRect.position = screen;
            if (!promptRect.gameObject.activeSelf) promptRect.gameObject.SetActive(true);
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

        public IEnumerator ShowTitle(string title, string subtitle)
        {
            titleText.text = title;
            subtitleText.text = InkStyle.Spaced(subtitle);
            yield return Tween.Fade(titleGroup, 1f, 0.6f);
            yield return new WaitForSeconds(1.8f);
            yield return Tween.Fade(titleGroup, 0f, 0.8f);
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
