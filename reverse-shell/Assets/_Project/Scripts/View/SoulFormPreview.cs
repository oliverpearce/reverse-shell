using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Hovering a unit's innate card shows its Soul form (the Ult it powers up into) beside it, under a "SOUL FORM"
    /// label: on the Units page, in the Cards book and on a unit's battle sheet. The preview sits on the root canvas, to
    /// the card's left (so it doesn't cover the cards after it; right when there's no room), and never takes clicks.
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
            return Attach(target, (face, s) => CardView.BuildFace(face, new CardInstance(ult, unit, 1), s, true), scale);
        }

        /// <summary>A Soul form not known yet (a resonance not unlocked, a companion not met): a card back under "???".</summary>
        public static SoulFormPreview AttachHidden(GameObject target, float scale)
        {
            var preview = Attach(target, (face, s) => CardView.BuildBack(face, new Color(0.55f, 0.53f, 0.5f)), scale);
            if (preview != null) preview.hidden = true;
            return preview;
        }

        bool hidden, lost;

        /// <summary>A Soul Card that was destroyed (Erin, Chapter One): a dark, cracked back under "OBLITERATED".</summary>
        public static SoulFormPreview AttachLost(GameObject target, float scale)
        {
            var preview = Attach(target, (face, s) =>
            {
                CardView.BuildBack(face, new Color(0.35f, 0.08f, 0.06f));
                var wash = UIFactory.CreateImage(face, "Ash", new Color(0.05f, 0.03f, 0.03f, 0.55f));
                wash.rectTransform.Fill();
                for (int i = 0; i < 3; i++)
                {
                    var crack = UIFactory.CreateImage(face, "Crack" + i, new Color(1f, 0.55f, 0.3f, 0.75f));
                    crack.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2((i - 1) * 14f * s, (i - 1) * -10f * s), new Vector2(3f * s, 90f * s));
                    crack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f - i * 22f);
                }
            }, scale);
            if (preview != null) preview.lost = true;
            return preview;
        }

        /// <param name="buildFace">Draws the Soul card into the rect it's given (CardView.Size * scale), as a card only (its
        /// text and numbers are for the card popup's Swap to Soul form).</param>
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
            var label = InkStyle.Label(panel, "Label", UIText.Get(lost ? "card.soul_form_lost" : hidden ? "card.soul_form_hidden" : "card.soul_form"), UIFactory.TextSize.Small, InkStyle.Gold, TextAnchor.MiddleCenter);
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
            bool fitsLeft = left.x - Gap - box.x >= -half.x;
            float x = fitsLeft ? left.x - Gap - box.x / 2f : right.x + Gap + box.x / 2f;
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
