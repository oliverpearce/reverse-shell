using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// The card details popup, shared by the party menu (click a card) and battle (long-press a hand card): the card
    /// lifts from where it sits, glides to the centre while growing, then flips over (scale X to 0 and back) to its
    /// big detailed face with the full text. Scroll over it to see the upgrades (ranks I / II / III; a hint says so).
    /// Click anywhere or Esc closes it (it shrinks back to where it came from). Runs on unscaled time.
    /// </summary>
    public class CardDetailPopup : MonoBehaviour
    {
        const float BigScale = 2f;        // 10x card pixels
        /// <summary>Where the open card rests: left of centre, so the card and its text panel (CardView.DetailPanel) are centred together.</summary>
        static Vector2 Rest { get { return new Vector2(-CardView.Size.x * BigScale * 0.62f, 0f); } }
        const float GlideSeconds = 0.2f, FlipSeconds = 0.18f, CloseSeconds = 0.16f;

        static CardDetailPopup current;

        /// <summary>True while a card is open (other input should wait; Esc closes the card first).</summary>
        public static bool IsOpen { get { return current != null; } }

        BattleUnit unit;
        CardDefinition card;
        int rank;
        RectTransform root, holder, smallFace;
        AbilityCard detailed;
        CanvasGroup dimGroup;
        Text hint;
        Vector2 fromPos;
        float fromScale;
        Action onClosed;
        bool closing;

        /// <param name="from">The card that was clicked (the popup starts and ends there); may be null.</param>
        public static CardDetailPopup Open(RectTransform canvasRoot, BattleUnit unit, CardDefinition card, int rank, RectTransform from, Action onClosed = null)
        {
            if (current != null) current.CloseNow();
            var rt = UIFactory.CreateRect("CardDetail", canvasRoot).Fill();
            rt.SetAsLastSibling();
            var popup = rt.gameObject.AddComponent<CardDetailPopup>();
            current = popup;
            popup.root = rt;
            popup.unit = unit;
            popup.card = card;
            popup.rank = Mathf.Max(1, rank);
            popup.onClosed = onClosed;
            popup.Build(from);
            popup.StartCoroutine(popup.OpenRoutine());
            return popup;
        }

        public static void CloseCurrent()
        {
            if (current != null) current.Close();
        }

        void Build(RectTransform from)
        {
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.03f, 0.028f, 0.025f, 0.8f), true);
            dim.rectTransform.Fill();
            dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(Close);

            holder = UIFactory.CreateRect("Card", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardView.Size * BigScale);
            // Start where the clicked card is, at its size.
            fromPos = Vector2.zero;
            fromScale = 0.5f;
            if (from != null)
            {
                Vector3 local = root.InverseTransformPoint(from.TransformPoint(from.rect.center));
                fromPos = new Vector2(local.x, local.y);
                fromScale = Mathf.Clamp(from.rect.width * from.lossyScale.x / (CardView.Size.x * BigScale * root.lossyScale.x), 0.1f, 1f);
            }
            holder.anchoredPosition = fromPos;
            holder.localScale = Vector3.one * fromScale;

            // The closed (small) face first; it flips over to the detailed one.
            smallFace = UIFactory.CreateRect("Small", holder).Fill();
            var inner = UIFactory.CreateRect("Face", smallFace).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardView.Size);
            inner.localScale = Vector3.one * BigScale;
            CardView.BuildFace(inner, new CardInstance(card, unit, rank), 1f);
            foreach (var g in smallFace.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;

            bool upgrades = !card.isRitual && card.MaxRank > 1;
            // Plain (not letter-spaced) and big enough to read at a glance.
            hint = UIFactory.CreateText(root, "Hint", UIText.Get(upgrades ? "card.hint.upgrades" : "card.hint.close"), 30, InkStyle.Paper, TextAnchor.MiddleCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(Rest.x, -CardView.Size.y * BigScale / 2f - 18f), new Vector2(900f, 56f));
            hint.color = Clear(hint.color);
        }

        IEnumerator OpenRoutine()
        {
            // Lift and glide to the centre while growing.
            yield return Animate(GlideSeconds, p =>
            {
                holder.anchoredPosition = Vector2.Lerp(fromPos, Rest, p);
                holder.localScale = new Vector3(1f, 1f, 1f) * Mathf.Lerp(fromScale, 0.92f, p);
                dimGroup.alpha = p;
            });
            // Flip: edge-on, swap to the detailed face, face-on again.
            yield return Animate(FlipSeconds / 2f, p => holder.localScale = new Vector3(0.92f * (1f - p), 0.92f + 0.04f * p, 1f));
            Destroy(smallFace.gameObject);
            detailed = AbilityCard.Create(holder, unit, card, rank, BigScale);
            ((RectTransform)detailed.transform).Fill();
            yield return Animate(FlipSeconds / 2f, p => holder.localScale = new Vector3(Mathf.Lerp(0f, 1f, p), Mathf.Lerp(0.96f, 1f, p), 1f));
            yield return Animate(0.2f, p => hint.color = Color.Lerp(Clear(InkStyle.Paper), InkStyle.Paper, p));
        }

        void Update()
        {
            if (!closing && UIKeys.Back) Close();
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            StopAllCoroutines();
            StartCoroutine(CloseRoutine());
        }

        IEnumerator CloseRoutine()
        {
            Vector3 startScale = holder.localScale;
            Vector2 startPos = holder.anchoredPosition;
            float startDim = dimGroup.alpha;
            yield return Animate(CloseSeconds, p =>
            {
                holder.anchoredPosition = Vector2.Lerp(startPos, fromPos, p);
                holder.localScale = Vector3.Lerp(startScale, Vector3.one * fromScale, p);
                dimGroup.alpha = Mathf.Lerp(startDim, 0f, p);
                hint.color = Clear(hint.color);
            });
            CloseNow();
        }

        void CloseNow()
        {
            if (current == this) current = null;
            if (onClosed != null) onClosed();
            onClosed = null;
            Destroy(gameObject);
        }

        static IEnumerator Animate(float duration, Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                step(1f - (1f - p) * (1f - p) * (1f - p));
                yield return null;
            }
            step(1f);
        }

        static Color Clear(Color c)
        {
            c.a = 0f;
            return c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }
    }
}
