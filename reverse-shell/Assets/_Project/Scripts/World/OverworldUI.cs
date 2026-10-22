using System.Collections;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>Overlay UI for the overworld: interact prompt, message box, area title, controls hint, fades.</summary>
    public class OverworldUI : MonoBehaviour
    {
        static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.88f);

        RectTransform promptRect;
        Text promptText;
        GameObject messageBox;
        Text speakerText, messageText;
        CanvasGroup titleGroup;
        Text titleText, subtitleText;
        Image fade;
        Coroutine messageRoutine;

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
            outline.effectColor = WorldArt.NeonMagenta;
            outline.effectDistance = new Vector2(2f, -2f);
            promptText = UIFactory.CreateText(promptRect, "Text", "", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(12, 24);
            promptText.supportRichText = true;
            promptText.rectTransform.Fill(6f);
            promptBg.gameObject.SetActive(false);

            // Message box
            var box = UIFactory.CreateImage(root, "MessageBox", Panel);
            box.rectTransform.Place(bottomCenter, bottomCenter, new Vector2(0f, 40f), new Vector2(1100f, 170f));
            var boxOutline = box.gameObject.AddComponent<Outline>();
            boxOutline.effectColor = WorldArt.NeonCyan;
            boxOutline.effectDistance = new Vector2(2f, -2f);
            speakerText = UIFactory.CreateText(box.transform, "Speaker", "", 26, WorldArt.NeonCyan, TextAnchor.UpperLeft, FontStyle.Bold);
            speakerText.rectTransform.TopStrip(14f, 32f, 24f);
            messageText = UIFactory.CreateText(box.transform, "Text", "", 26, Color.white, TextAnchor.UpperLeft);
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
            titleText = UIFactory.CreateText(title, "Text", "", 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            titleText.rectTransform.TopStrip(0f, 96f);
            var titleOutline = titleText.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = WorldArt.NeonMagenta;
            titleOutline.effectDistance = new Vector2(3f, -3f);
            subtitleText = UIFactory.CreateText(title, "Subtitle", "", 28, WorldArt.NeonCyan, TextAnchor.MiddleCenter);
            subtitleText.rectTransform.TopStrip(100f, 40f);

            var hint = UIFactory.CreateText(root, "Hint", "WASD / Arrows: move    Shift: run    F: interact", 20,
                new Color(0.75f, 0.75f, 0.85f, 0.8f), TextAnchor.LowerLeft);
            hint.rectTransform.Place(bottomLeft, bottomLeft, new Vector2(24f, 16f), new Vector2(800f, 30f));

            fade = UIFactory.CreateImage(root, "Fade", Color.black);
            fade.rectTransform.Fill();
            fade.color = new Color(0f, 0f, 0f, 0f);
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
            promptText.text = "<color=#FF2E88>[" + key + "]</color>  " + text;
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
