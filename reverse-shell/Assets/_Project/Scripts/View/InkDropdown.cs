using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A dropdown in the ink style: a list of choices that opens under a button (or above it when there's no room
    /// below), the current one in gold. A click picks one; a click anywhere else, or Esc, closes it. ↑ / ↓ and Enter
    /// work too. Screens should ignore their own keys while <see cref="IsOpen"/>.
    /// </summary>
    public class InkDropdown : MonoBehaviour
    {
        const float RowH = 58f, Pad = 8f;

        static InkDropdown current;
        public static bool IsOpen { get { return current != null; } }
        /// <summary>The frame it last closed (so the Esc / click that closed it isn't read again underneath).</summary>
        public static int ClosedFrame { get; private set; }

        readonly List<Image> plates = new List<Image>();
        readonly List<Text> labels = new List<Text>();
        Action<int> onPick;
        int index, openedFrame;

        /// <summary>Opens the choices under <paramref name="anchor"/>, <paramref name="width"/> wide.</summary>
        public static InkDropdown Open(RectTransform anchor, IList<string> choices, int selected, Action<int> onPick, float width = 0f)
        {
            Close();
            var canvas = anchor.GetComponentInParent<Canvas>();
            if (canvas == null || choices == null || choices.Count == 0) return null;
            var root = (RectTransform)canvas.rootCanvas.transform;
            var rt = UIFactory.CreateRect("Dropdown", root).Fill();
            rt.SetAsLastSibling();
            var dd = rt.gameObject.AddComponent<InkDropdown>();
            current = dd;
            dd.onPick = onPick;
            dd.openedFrame = Time.frameCount;

            // A clear blocker: a click outside closes it.
            var blocker = UIFactory.CreateImage(rt, "Blocker", new Color(0f, 0f, 0f, 0.001f), true);
            blocker.rectTransform.Fill();
            var close = blocker.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(Close);

            float w = width > 0f ? width : anchor.rect.width * anchor.lossyScale.x / root.lossyScale.x;
            float h = choices.Count * RowH + Pad * 2f;
            var panel = InkStyle.Panel(rt, "Panel", new Color(0.08f, 0.075f, 0.07f, 0.97f), true).rectTransform;
            InkStyle.Frame(panel, InkStyle.Gold, 2f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(w, h);
            Vector3 bottom = root.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMin, 0f)));
            Vector3 top = root.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMax, 0f)));
            var half = root.rect.size / 2f;
            float x = Mathf.Clamp(bottom.x, -half.x + w / 2f, half.x - w / 2f);
            bool below = bottom.y - 6f - h >= -half.y;
            panel.anchoredPosition = below ? new Vector2(x, bottom.y - 6f) : new Vector2(x, top.y + 6f + h);

            for (int i = 0; i < choices.Count; i++)
            {
                int captured = i;
                var row = UIFactory.CreateImage(panel, "Choice" + i, new Color(1f, 1f, 1f, 0f), true);
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.offsetMin = new Vector2(Pad, 0f);
                row.rectTransform.offsetMax = new Vector2(-Pad, 0f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -Pad - i * RowH);
                row.rectTransform.sizeDelta = new Vector2(row.rectTransform.sizeDelta.x, RowH);
                var b = row.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => dd.Pick(captured));
                var hover = row.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => dd.Highlight(captured));
                hover.triggers.Add(enter);
                var t = UIFactory.CreateText(row.rectTransform, "Label", choices[i], UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleLeft);
                t.rectTransform.Fill();
                t.rectTransform.offsetMin = new Vector2(16f, 0f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                dd.plates.Add(row);
                dd.labels.Add(t);
            }
            dd.Highlight(Mathf.Clamp(selected, 0, choices.Count - 1), selected);
            return dd;
        }

        public static void Close()
        {
            if (current == null) return;
            Destroy(current.gameObject);
            current = null;
            ClosedFrame = Time.frameCount;
        }

        int chosen = -1;

        void Highlight(int i, int selected = -2)
        {
            if (selected != -2) chosen = selected;
            index = i;
            for (int r = 0; r < plates.Count; r++)
            {
                plates[r].color = r == index ? InkStyle.HoverGray : new Color(1f, 1f, 1f, 0f);
                labels[r].color = r == chosen ? InkStyle.GoldBright : InkStyle.Paper;
            }
        }

        void Pick(int i)
        {
            var then = onPick;
            Close();
            if (then != null) then(i);
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            if (UIKeys.Back) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) Highlight((index + 1) % plates.Count);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Highlight((index - 1 + plates.Count) % plates.Count);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Pick(index);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { current = null; }
    }
}
