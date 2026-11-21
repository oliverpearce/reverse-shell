using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The battle tutorial's pointer: a pulsing gold frame around what to do next (a card, the enemy, Go), a second frame
    /// for a merge's matching card, and a short word above it. It follows its targets every frame and never takes a click.
    /// </summary>
    public class TutorialHighlight : MonoBehaviour
    {
        const float Pad = 10f, Pulse = 4f, LabelGap = 34f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        RectTransform root, frameA, frameB;
        RectTransform targetA, targetB;
        Text label;
        CanvasGroup group;
        readonly Vector3[] corners = new Vector3[4];

        public static TutorialHighlight Create(RectTransform parent)
        {
            var rt = UIFactory.CreateRect("TutorialHighlight", parent).Fill();
            var pointer = rt.gameObject.AddComponent<TutorialHighlight>();
            pointer.root = rt;
            pointer.group = rt.gameObject.AddComponent<CanvasGroup>();
            pointer.group.blocksRaycasts = false;
            pointer.group.interactable = false;
            pointer.frameA = pointer.NewFrame("FrameA");
            pointer.frameB = pointer.NewFrame("FrameB");
            pointer.label = InkStyle.Title(rt, "Label", "", UIFactory.TextSize.Large, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            pointer.label.raycastTarget = false;
            pointer.label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            pointer.label.rectTransform.anchorMin = pointer.label.rectTransform.anchorMax = Center;
            pointer.label.rectTransform.sizeDelta = new Vector2(320f, 56f);
            pointer.Hide();
            return pointer;
        }

        RectTransform NewFrame(string name)
        {
            var frame = UIFactory.CreateRect(name, root);
            frame.anchorMin = frame.anchorMax = frame.pivot = Center;
            InkStyle.Frame(frame, InkStyle.GoldBright, 4f);
            return frame;
        }

        /// <summary>Points at <paramref name="first"/> (and <paramref name="second"/>, if any), with a word above the first.</summary>
        public void Show(RectTransform first, RectTransform second, string word)
        {
            if (first == null) { Hide(); return; }
            targetA = first;
            targetB = second;
            if (label.text != word) label.text = word;
            if (!frameA.gameObject.activeSelf) root.SetAsLastSibling();
            frameA.gameObject.SetActive(true);
            frameB.gameObject.SetActive(second != null);
            label.gameObject.SetActive(true);
        }

        public void Hide()
        {
            targetA = targetB = null;
            frameA.gameObject.SetActive(false);
            frameB.gameObject.SetActive(false);
            label.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (targetA == null)
            {
                if (frameA.gameObject.activeSelf) Hide();
                return;
            }
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            group.alpha = 0.65f + 0.35f * wave;
            float pad = Pad + Pulse * wave;
            Vector2 center, size;
            Fit(frameA, targetA, pad, out center, out size);
            if (targetB != null)
            {
                Vector2 otherCenter, otherSize;
                Fit(frameB, targetB, pad, out otherCenter, out otherSize);
            }
            float top = root.rect.height * 0.5f - 36f;
            label.rectTransform.anchoredPosition = new Vector2(center.x, Mathf.Min(center.y + size.y * 0.5f + LabelGap, top));
        }

        void Fit(RectTransform frame, RectTransform target, float pad, out Vector2 center, out Vector2 size)
        {
            target.GetWorldCorners(corners);
            Vector2 min = root.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 p = root.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            center = (min + max) * 0.5f - root.rect.center;
            size = max - min + Vector2.one * pad * 2f;
            frame.anchoredPosition = center;
            frame.sizeDelta = size;
        }
    }
}
