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
        static readonly Color NumberGold = new Color(1f, 0.84f, 0.38f);

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
        /// Builds the card's visuals into <paramref name="root"/> (sized Size * s). Used for the hand card, the hover
        /// preview and the inspect screen, so bigger versions are crisp re-layouts rather than blurry upscales.
        /// The small hand card (s <= 1) is just the rank in the top-left corner and one huge ATTACK / BUFF / DEBUFF /
        /// HEAL word along the bottom. Bigger faces add the name header, owner and full text.
        /// Rank is shown as stacking stars (CardStars) or as pips, per CardSettings.RankStars.
        /// </summary>
        public static void BuildFace(RectTransform root, CardInstance card, float s)
        {
            var owner = card.Owner.Definition.color;
            bool ritual = card.IsRitual;
            bool detailed = s > 1f;
            bool stars = CardSettings.RankStars;
            var pastel = RitualPastel(owner);
            int F(float size) { return Mathf.RoundToInt(size * s); }

            // Transparent hit area on the root (click / drag / hover), then the face.
            var hit = root.GetComponent<Image>();
            if (hit == null) hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var face = UIFactory.CreateImage(root, "Face", ritual ? pastel : UIFactory.Darken(owner, 0.62f));
            face.rectTransform.Fill();
            var edge = face.gameObject.AddComponent<Outline>();
            edge.effectColor = ritual ? Color.white : UIFactory.Darken(owner, 0.1f);
            edge.effectDistance = new Vector2(2f, -2f) * s;

            float top = 0f; // where the content under the header starts
            if (detailed)
            {
                var header = UIFactory.CreateImage(root, "Header", ritual ? Color.Lerp(pastel, owner, 0.25f) : owner);
                header.rectTransform.TopStrip(0f, 38f * s);
                var title = UIFactory.CreateText(header.transform, "Title", card.Name, F(17), ritual ? PastelText : Color.white,
                    TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(F(10), F(17));
                title.rectTransform.Fill(6f * s);
                top = 38f * s;
            }

            // Rank: stars from the top-left corner, or pips across the top.
            if (stars)
            {
                int count = CardStars.CountFor(card);
                if (count > 0) CardStars.Build(root, count, 22f * s, new Vector2(0f, 1f), new Vector2(7f * s, -top - 6f * s));
            }
            else if (!ritual)
            {
                int max = card.Definition.MaxRank;
                float pipW = 26f * s, gap = 6f * s;
                float total = max * pipW + (max - 1) * gap;
                for (int i = 0; i < max; i++)
                {
                    var pip = UIFactory.CreateImage(root, "Rank" + i, i < card.Rank ? Gold : new Color(0f, 0f, 0f, 0.5f));
                    pip.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-total / 2f + i * (pipW + gap), -top - 8f * s), new Vector2(pipW, 10f * s));
                }
            }

            if (detailed)
            {
                string ownerText = ritual ? card.Owner.Name + "  \u00b7  RITUAL" : card.Owner.Name;
                var ownerName = UIFactory.CreateText(root, "Owner", ownerText, F(13), ritual ? PastelText : new Color(0.85f, 0.85f, 0.9f),
                    TextAnchor.MiddleRight, FontStyle.Bold);
                ownerName.rectTransform.TopStrip(top + 6f * s, 22f * s, 9f * s);
            }

            // Reverse 1999-style: the bottom of the card is one huge word saying what it does.
            const float band = 58f;
            var kindColor = UnitView.KindColor(card.Definition.kind);
            var bandImg = UIFactory.CreateImage(root, "KindBand", ritual ? Color.Lerp(kindColor, Color.white, 0.35f) : UIFactory.Darken(kindColor, 0.3f));
            var brt = bandImg.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = new Vector2(0f, band * s);
            var word = UIFactory.CreateText(bandImg.transform, "Kind", UnitView.KindLabel(card.Definition.kind).ToUpperInvariant(), F(40),
                ritual ? PastelText : Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic).BestFit(F(14), F(40));
            word.font = UIFactory.DisplayFont; // Anton: punchy, Persona-style
            word.fontStyle = FontStyle.Normal;
            word.rectTransform.Fill(6f * s);
            if (!ritual) word.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            if (detailed)
            {
                var middle = UIFactory.CreateRect("Middle", root);
                middle.anchorMin = Vector2.zero;
                middle.anchorMax = Vector2.one;
                middle.offsetMin = new Vector2(12f, band + 8f) * s;
                middle.offsetMax = new Vector2(-12f, -top - 36f * s);
                var descColor = ritual ? PastelText : new Color(0.92f, 0.92f, 0.95f);
                // Every number / percentage is bigger, bold and gold so the values pop.
                var desc = UIFactory.CreateText(middle, "Description", "", F(15), descColor, TextAnchor.UpperLeft);
                desc.font = UIFactory.BodyFont;
                desc.lineSpacing = 1.05f;
                desc.rectTransform.Fill();
                string text = card.Describe();
                var numberColor = ritual ? new Color(0.55f, 0.25f, 0.6f) : NumberGold;
                UIFactory.FitRich(desc, size => UIFactory.EmphasizeNumbers(text, Mathf.RoundToInt(size * 1.45f), numberColor), F(9), F(15));
            }
        }

        /// <summary>Rebuilds this card's face (e.g. after the rank display setting changes).</summary>
        public void RebuildFace()
        {
            for (int i = Rect.childCount - 1; i >= 0; i--)
            {
                var child = Rect.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            mergeGlow = null;
            BuildFace(Rect, Card, 1f);
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

        /// <summary>Gold frame (no text) shown while a dragged card would merge here.</summary>
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
            }
            if (mergeGlow == null) return;

            bool wasOn = mergeGlow.activeSelf;
            mergeGlow.SetActive(on);
            if (!on) return;
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
