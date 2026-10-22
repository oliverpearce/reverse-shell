using System.Collections;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>Overlay UI for the overworld: interact prompt, message box, area title, controls hint, fades.</summary>
    public class OverworldUI : MonoBehaviour
    {
        static readonly Color Panel = WorldArt.UiPanel;

        RectTransform promptRect;
        Text promptText;
        GameObject messageBox;
        Text speakerText, messageText;
        CanvasGroup titleGroup;
        Text titleText, subtitleText;
        Image fade;
        Coroutine messageRoutine;
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

        public bool MessageVisible { get { return messageBox.activeSelf; } }

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
            var ui = go.AddComponent<OverworldUI>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            var bottomLeft = Vector2.zero;
            var bottomCenter = new Vector2(0.5f, 0f);
            var center = new Vector2(0.5f, 0.5f);

            // Interact prompt (follows a world point)
            var promptBg = UIFactory.CreateImage(root, "Prompt", Panel);
            promptRect = promptBg.rectTransform;
            promptRect.sizeDelta = new Vector2(360f, 52f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            var outline = promptBg.gameObject.AddComponent<Outline>();
            outline.effectColor = WorldArt.UiEdge;
            outline.effectDistance = new Vector2(2f, -2f);
            promptText = UIFactory.CreateText(promptRect, "Text", "", 24, WorldArt.UiInk, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(12, 24);
            promptText.supportRichText = true;
            promptText.rectTransform.Fill(6f);
            promptBg.gameObject.SetActive(false);

            // Message box
            var box = UIFactory.CreateImage(root, "MessageBox", Panel);
            box.rectTransform.Place(bottomCenter, bottomCenter, new Vector2(0f, 40f), new Vector2(1100f, 170f));
            var boxOutline = box.gameObject.AddComponent<Outline>();
            boxOutline.effectColor = WorldArt.UiEdge;
            boxOutline.effectDistance = new Vector2(2f, -2f);
            speakerText = UIFactory.CreateText(box.transform, "Speaker", "", 26, WorldArt.UiInk, TextAnchor.UpperLeft, FontStyle.Bold);
            speakerText.rectTransform.TopStrip(14f, 32f, 24f);
            messageText = UIFactory.CreateText(box.transform, "Text", "", 26, WorldArt.UiInk, TextAnchor.UpperLeft);
            var mrt = messageText.rectTransform;
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(24f, 16f);
            mrt.offsetMax = new Vector2(-24f, -52f);
            messageBox = box.gameObject;
            messageBox.SetActive(false);

            // Area title
            var title = UIFactory.CreateRect("Title", root).Place(center, center, new Vector2(0f, 260f), new Vector2(1200f, 160f));
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleText = UIFactory.CreateText(title, "Text", "", 72, new Color(0.95f, 0.93f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
            titleText.rectTransform.TopStrip(0f, 96f);
            var titleOutline = titleText.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.2f, 0.19f, 0.15f, 0.85f);
            titleOutline.effectDistance = new Vector2(3f, -3f);
            subtitleText = UIFactory.CreateText(title, "Subtitle", "", 28, WorldArt.UiPanel, TextAnchor.MiddleCenter);
            subtitleText.rectTransform.TopStrip(100f, 40f);
            subtitleText.gameObject.AddComponent<Outline>().effectColor = new Color(0.2f, 0.19f, 0.15f, 0.85f);

            var hint = UIFactory.CreateText(root, "Hint", "WASD / Arrows: move    Shift: run    F: interact", 20,
                new Color(0.95f, 0.93f, 0.85f, 0.85f), TextAnchor.LowerLeft);
            hint.rectTransform.Place(bottomLeft, bottomLeft, new Vector2(24f, 16f), new Vector2(800f, 30f));
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0.15f, 0.14f, 0.11f, 0.8f); // readable over bright ground

            // Party HP (top left): name, big HP number, thin bar.
            partyPanel = UIFactory.CreateRect("Party", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(420f, 0f));

            fade = UIFactory.CreateImage(root, "Fade", Color.black);
            fade.rectTransform.Fill();
            fade.color = new Color(0f, 0f, 0f, 0f);
        }

        static readonly Color HudText = new Color(0.96f, 0.94f, 0.86f);
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
                float nameSize = leader ? 26f : 20f, barHeight = leader ? 18f : 11f, lvSize = leader ? 16f : 14f;
                float width = leader ? 360f : 280f;
                float height = nameSize + 6f + barHeight + 4f + lvSize + 2f;
                const float lineX = 4f, contentX = 18f;

                var row = UIFactory.CreateRect(m.Name, partyPanel);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(0f, -y);
                row.sizeDelta = new Vector2(width + contentX, height);

                if (leader)
                {
                    var glow = UIFactory.CreateImage(row, "LeaderGradient", new Color(1f, 0.93f, 0.72f, 0.32f));
                    glow.sprite = FadeRightSprite();
                    glow.rectTransform.anchorMin = Vector2.zero;
                    glow.rectTransform.anchorMax = Vector2.one;
                    glow.rectTransform.offsetMin = new Vector2(-8f, -6f);
                    glow.rectTransform.offsetMax = new Vector2(60f, 6f);
                }

                var line = UIFactory.CreateImage(row, "Color", m.Color);
                line.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(lineX, 0f), new Vector2(leader ? 6f : 5f, height - 4f));

                var name = UIFactory.CreateText(row, "Name", m.Name, (int)nameSize, HudText, TextAnchor.LowerLeft, FontStyle.Bold);
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(contentX, 0f), new Vector2(width, nameSize + 4f));
                name.gameObject.AddComponent<Outline>().effectColor = HudShadow;

                bool low = m.HP <= m.MaxHP / 4;
                var track = UIFactory.CreateImage(row, "Bar", new Color(0f, 0f, 0f, 0.5f));
                track.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(contentX, -(nameSize + 6f)), new Vector2(width, barHeight));
                track.gameObject.AddComponent<Outline>().effectColor = HudShadow;
                var fill = UIFactory.CreateImage(track.transform, "Fill", low ? new Color(0.92f, 0.36f, 0.24f) : new Color(0.93f, 0.91f, 0.8f));
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(m.HP / (float)Mathf.Max(1, m.MaxHP)), 1f);
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;

                var lv = UIFactory.CreateText(row, "Level", "Lv. " + m.Level, (int)lvSize, HudText, TextAnchor.UpperLeft, FontStyle.Bold);
                lv.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(contentX, -(nameSize + 6f + barHeight + 4f)), new Vector2(width, lvSize + 4f));
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
            promptText.text = "<color=#A5432C>[" + key + "]</color>  " + text;
            promptRect.position = screen;
            if (!promptRect.gameObject.activeSelf) promptRect.gameObject.SetActive(true);
        }

        public void HidePrompt()
        {
            if (promptRect.gameObject.activeSelf) promptRect.gameObject.SetActive(false);
        }

        public void ShowMessage(string speaker, string text, float duration = 6f)
        {
            speakerText.text = speaker;
            messageText.text = text;
            messageBox.SetActive(true);
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(HideMessageAfter(duration));
        }

        public void HideMessage()
        {
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageBox.SetActive(false);
        }

        IEnumerator HideMessageAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            messageBox.SetActive(false);
        }

        public IEnumerator ShowTitle(string title, string subtitle)
        {
            titleText.text = title;
            subtitleText.text = subtitle;
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
