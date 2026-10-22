using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>A card in the hand. Click to queue it; drag to move it; hover for a large preview.</summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public static readonly Vector2 Size = new Vector2(150f, 214f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        static readonly Color PastelText = new Color(0.22f, 0.18f, 0.28f);

        public CardInstance Card { get; private set; }
        public RectTransform Rect { get; private set; }
        public CanvasGroup Group { get; private set; }

        HandView hand;
        Coroutine moveRoutine, scaleRoutine;
        bool dragging;
        Vector3 dragOffset;

        public static CardView Create(Transform parent, CardInstance card, HandView hand)
        {
            var rt = UIFactory.CreateRect("Card_" + card.Name, parent);
            rt.sizeDelta = Size;
            var cv = rt.gameObject.AddComponent<CardView>();
            cv.Card = card;
            cv.hand = hand;
            cv.Rect = rt;
            cv.Group = rt.gameObject.AddComponent<CanvasGroup>();
            BuildFace(rt, card, 1f);
            return cv;
        }

        /// <summary>Soft pastel version of the owner's color, used for Ritual cards.</summary>
        public static Color RitualPastel(Color owner)
        {
            return Color.Lerp(owner, Color.white, 0.62f);
        }

        /// <summary>
        /// Builds the card's visuals into <paramref name="root"/> (sized Size * s). Used for both the
        /// hand card and the hover preview, so the preview is a crisp re-layout rather than a blurry upscale.
        /// </summary>
        public static void BuildFace(RectTransform root, CardInstance card, float s)
        {
            var owner = card.Owner.Definition.color;
            bool ritual = card.IsRitual;
            var pastel = RitualPastel(owner);
            int F(float size) { return Mathf.RoundToInt(size * s); }

            var bg = root.gameObject.AddComponent<Image>();
            bg.color = ritual ? pastel : UIFactory.Darken(owner, 0.62f);
            var outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = ritual ? Color.white : UIFactory.Darken(owner, 0.1f);
            outline.effectDistance = (ritual ? new Vector2(4f, -4f) : new Vector2(2f, -2f)) * s;

            var header = UIFactory.CreateImage(root, "Header", ritual ? Color.Lerp(pastel, owner, 0.25f) : owner);
            header.rectTransform.TopStrip(0f, 38f * s);
            var title = UIFactory.CreateText(header.transform, "Title", card.Name, F(17), ritual ? PastelText : Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(F(10), F(17));
            title.rectTransform.Fill(4f * s);

            if (ritual)
            {
                var label = UIFactory.CreateText(root, "Ritual", "RITUAL", F(15), PastelText, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.rectTransform.TopStrip(42f * s, 18f * s);
            }
            else
            {
                int max = card.Definition.MaxRank;
                float pipW = 26f * s, gap = 6f * s;
                float total = max * pipW + (max - 1) * gap;
                for (int i = 0; i < max; i++)
                {
                    var pip = UIFactory.CreateImage(root, "Rank" + i, i < card.Rank ? Gold : new Color(0f, 0f, 0f, 0.5f));
                    pip.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-total / 2f + i * (pipW + gap), -46f * s), new Vector2(pipW, 10f * s));
                }
            }

            var kindColor = ritual ? PastelText : Color.Lerp(UnitView.KindColor(card.Definition.kind), Color.white, 0.4f);
            var kind = UIFactory.CreateText(root, "Kind", card.Owner.Name + " · " + UnitView.KindLabel(card.Definition.kind), F(13), kindColor);
            kind.rectTransform.TopStrip(62f * s, 18f * s, 4f * s);

            var descColor = ritual ? PastelText : new Color(0.92f, 0.92f, 0.95f);
            var desc = UIFactory.CreateText(root, "Description", card.Describe(), F(14), descColor, TextAnchor.UpperLeft).BestFit(F(9), F(14));
            var drt = desc.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(9f, 8f) * s;
            drt.offsetMax = new Vector2(-9f, -86f) * s;
        }

        public void AnimateTo(Vector2 anchored, float duration)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(Tween.MoveAnchored(Rect, anchored, duration));
        }

        public void ScaleTo(float scale, float duration)
        {
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(Tween.Scale(Rect, Vector3.one * scale, duration, Tween.OutBack));
        }

        GameObject mergeGlow;
        Text mergeLabel;

        /// <summary>Gold frame plus a "MERGE -> II" tag, shown while a dragged card would merge here.</summary>
        public void SetMergeHighlight(bool on, int resultRank)
        {
            if (on && mergeGlow == null)
            {
                var glowRoot = UIFactory.CreateRect("MergeGlow", transform).Fill(-5f);
                mergeGlow = glowRoot.gameObject;
                const float t = 5f;
                var edges = new[]
                {
                    new[] { 0f, 1f, 1f, 1f, 0f, -t, 0f, 0f },   // top
                    new[] { 0f, 0f, 1f, 0f, 0f, 0f, 0f, t },    // bottom
                    new[] { 0f, 0f, 0f, 1f, 0f, 0f, t, 0f },    // left
                    new[] { 1f, 0f, 1f, 1f, -t, 0f, 0f, 0f },   // right
                };
                foreach (var e in edges)
                {
                    var rt = UIFactory.CreateImage(glowRoot, "Edge", Gold).rectTransform;
                    rt.anchorMin = new Vector2(e[0], e[1]);
                    rt.anchorMax = new Vector2(e[2], e[3]);
                    rt.offsetMin = new Vector2(e[4], e[5]);
                    rt.offsetMax = new Vector2(e[6], e[7]);
                }
                var tag = UIFactory.CreateImage(glowRoot, "Tag", Gold);
                tag.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(110f, 26f));
                mergeLabel = UIFactory.CreateText(tag.transform, "Label", "", 15, new Color(0.15f, 0.1f, 0.05f), TextAnchor.MiddleCenter, FontStyle.Bold);
                mergeLabel.rectTransform.Fill();
            }
            if (mergeGlow == null) return;

            bool wasOn = mergeGlow.activeSelf;
            mergeGlow.SetActive(on);
            if (!on) return;
            mergeLabel.text = "MERGE -> " + UIFactory.Roman(resultRank);
            mergeGlow.transform.SetAsLastSibling();
            if (!wasOn) StartCoroutine(Tween.Punch(mergeGlow.transform, 0.08f, 0.18f));
        }

        public void StopAnimations()
        {
            StopAllCoroutines();
            moveRoutine = scaleRoutine = null;
        }

        public IEnumerator FlyAndVanish(Vector3 worldTarget, float duration)
        {
            hand.HidePreview(this);
            StopAnimations();
            Group.alpha = 1f;
            Group.blocksRaycasts = false;
            StartCoroutine(Tween.Scale(Rect, Vector3.one * 0.45f, duration));
            StartCoroutine(Tween.Fade(Group, 0f, duration));
            yield return Tween.MoveWorld(Rect, worldTarget, duration);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (hand != null) hand.HidePreview(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            hand.HidePreview(this);
            hand.View.OnCardClicked(Card);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!hand.View.InputEnabled || !hand.View.Controller.CanAct) return; // moving costs 1 AP
            hand.HidePreview(this);
            dragging = true;
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            dragOffset = Rect.position - (Vector3)eventData.position;
            Group.blocksRaycasts = false;
            Rect.SetAsLastSibling();
            ScaleTo(1.1f, 0.12f);
            hand.BeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            Rect.position = (Vector3)eventData.position + dragOffset;
            hand.UpdateDrag(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            Group.blocksRaycasts = true;
            ScaleTo(1f, 0.12f);
            hand.EndDrag(this);
            hand.OnCardDropped(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!dragging && hand.View.InputEnabled) hand.ShowPreview(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hand.HidePreview(this);
        }
    }
}
