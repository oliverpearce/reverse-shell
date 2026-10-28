using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Hovering a unit's innate card shows its Soul form (the Ult it powers up into) beside it, under a "SOUL FORM"
    /// label: on the Units page, in the Cards book and on a unit's battle sheet. The preview sits on the root canvas, to
    /// the card's right (or left when that runs off screen), and never takes clicks.
    /// </summary>
    public class SoulFormPreview : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float Gap = 18f, LabelH = 40f, Pad = 12f;

        Action<RectTransform, float> buildFace;
        float scale;
        RectTransform shown;

        /// <summary>Shows <paramref name="ult"/> as <paramref name="unit"/> would play it when the target is hovered.</summary>
        public static SoulFormPreview Attach(GameObject target, BattleUnit unit, CardDefinition ult, float scale)
        {
            if (ult == null) return null;
            return Attach(target, (face, s) => CardView.BuildFace(face, new CardInstance(ult, unit, 1), s), scale);
        }

        /// <param name="buildFace">Draws the Soul card into the rect it's given (CardView.Size * scale).</param>
        public static SoulFormPreview Attach(GameObject target, Action<RectTransform, float> buildFace, float scale)
        {
            if (target == null || buildFace == null) return null;
            var preview = target.AddComponent<SoulFormPreview>();
            preview.buildFace = buildFace;
            preview.scale = scale;
            return preview;
        }

        public void OnPointerEnter(PointerEventData eventData) { Show(); }
        public void OnPointerExit(PointerEventData eventData) { Hide(); }
        void OnDisable() { Hide(); }
        void OnDestroy() { Hide(); }

        void Show()
        {
            Hide();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = (RectTransform)canvas.rootCanvas.transform;
            var self = (RectTransform)transform;
            var size = CardView.Size * scale;
            var box = new Vector2(size.x + Pad * 2f, size.y + LabelH + Pad * 2f);

            var panel = InkStyle.Panel(root, "SoulFormPreview", InkStyle.InkPanel).rectTransform;
            InkStyle.Frame(panel, UIFactory.WithAlpha(InkStyle.Gold, 0.8f), 2f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = box;
            var label = InkStyle.Label(panel, "Label", UIText.Get("card.soul_form"), UIFactory.TextSize.Small, InkStyle.Gold, TextAnchor.MiddleCenter);
            label.rectTransform.TopStrip(Pad, LabelH, Pad);
            var face = UIFactory.CreateRect("Face", panel);
            face.anchorMin = face.anchorMax = new Vector2(0.5f, 0f);
            face.pivot = new Vector2(0.5f, 0f);
            face.sizeDelta = size;
            face.anchoredPosition = new Vector2(0f, Pad);
            buildFace(face, scale);
            foreach (var g in panel.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;

            // Beside the card, kept on screen.
            Vector3 right = root.InverseTransformPoint(self.TransformPoint(new Vector3(self.rect.xMax, self.rect.center.y, 0f)));
            Vector3 left = root.InverseTransformPoint(self.TransformPoint(new Vector3(self.rect.xMin, self.rect.center.y, 0f)));
            var half = root.rect.size / 2f;
            bool fitsRight = right.x + Gap + box.x <= half.x;
            float x = fitsRight ? right.x + Gap + box.x / 2f : left.x - Gap - box.x / 2f;
            float y = Mathf.Clamp(right.y, -half.y + box.y / 2f, half.y - box.y / 2f);
            panel.anchoredPosition = new Vector2(x, y);
            panel.SetAsLastSibling();
            shown = panel;
        }

        void Hide()
        {
            if (shown != null) Destroy(shown.gameObject);
            shown = null;
        }
    }
}
