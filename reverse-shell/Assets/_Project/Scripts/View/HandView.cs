using System.Collections.Generic;
using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// Lays out CardViews to match a hand snapshot. Views are keyed by CardInstance, so any
    /// snapshot (draw, merge, undo...) can be applied and missing/extra views are reconciled.
    /// Slay the Spire-style fan: the cards overlap along a gentle arc, tilted outward, squeezed so the whole hand
    /// always fits between the screen's left edge and the Go button. The hovered card rises in front and its
    /// neighbours lean aside.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        /// <summary>Widest gap between card centres (cards slightly overlap even in a small hand).</summary>
        public static float MaxSpacing { get { return CardView.Size.x * 0.82f; } }
        public const float PreviewScale = 1.9f;
        /// <summary>Tilt per card from the middle (degrees), capped so big hands don't over-curve.</summary>
        const float TiltPerCard = 4f, MaxTilt = 14f;
        /// <summary>How far the arc drops per card from the middle, squared (canvas units).</summary>
        const float ArcDrop = 5f;
        /// <summary>How far the hovered card's neighbours lean aside (canvas units, halving per card).</summary>
        const float HoverPush = 70f;
        /// <summary>Room kept clear on the left edge and for the Go button on the right (canvas units).</summary>
        const float LeftMargin = 24f, RightReserve = 340f;

        CardView hoveredCard;

        public BattleView View { get; private set; }
        public RectTransform Rect { get; private set; }
        public List<CardInstance> Order { get; private set; }

        readonly Dictionary<CardInstance, CardView> views = new Dictionary<CardInstance, CardView>();
        RectTransform previewLayer;
        RectTransform preview;
        CardView previewOwner;

        public void Init(BattleView battleView, RectTransform previewLayer)
        {
            View = battleView;
            Rect = (RectTransform)transform;
            Order = new List<CardInstance>();
            this.previewLayer = previewLayer;
        }

        void OnDestroy()
        {
        }

        /// <summary>Rebuilds every card in hand, e.g. after switching between rank pips and rank borders.</summary>
        void RefreshFaces()
        {
            HideAnyPreview();
            foreach (var cv in views.Values)
                if (cv != null) cv.RebuildFace();
        }

        /// <summary>Slay the Spire-style hover: a large, crisp copy of the card rising from its slot.</summary>
        public void ShowPreview(CardView cv)
        {
            HideAnyPreview();
            var size = CardView.Size * PreviewScale;
            preview = UIFactory.CreateRect("CardPreview", previewLayer);
            preview.sizeDelta = size;
            preview.pivot = new Vector2(0.5f, 0f);
            CardView.BuildFace(preview, cv.Card, PreviewScale);
            var group = preview.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // Bottom of the preview sits just above the bottom of the hand card, clamped on screen.
            Vector3 bottom = cv.Rect.TransformPoint(new Vector3(0f, -CardView.Size.y / 2f + 10f, 0f));
            Vector3 local = previewLayer.InverseTransformPoint(bottom);
            float halfRoom = previewLayer.rect.width / 2f - size.x / 2f - 12f;
            local.x = Mathf.Clamp(local.x, -halfRoom, halfRoom);
            preview.localPosition = local;

            preview.localScale = Vector3.one * 0.75f;
            StartCoroutine(Tween.Scale(preview, Vector3.one, 0.1f));

            previewOwner = cv;
            cv.Group.alpha = 0f;
            View.ShowTargetPreview(cv.Card);
        }

        public void HidePreview(CardView cv)
        {
            if (cv == previewOwner) HideAnyPreview();
        }

        public void HideAnyPreview()
        {
            if (preview != null) Destroy(preview.gameObject);
            preview = null;
            if (View != null) View.ClearTargetPreview();
            if (previewOwner != null && previewOwner.Group != null) previewOwner.Group.alpha = 1f;
            previewOwner = null;
        }

        /// <summary>The hand's usable strip in this rect's local x: from the left edge to just before the Go button.</summary>
        void Bounds(out float left, out float right)
        {
            var parent = Rect.parent as RectTransform;
            float half = parent != null && parent.rect.width > 0f ? parent.rect.width / 2f : 1200f;
            float offset = Rect.anchoredPosition.x;
            left = -half + LeftMargin + CardView.Size.x / 2f - offset;
            right = half - RightReserve - CardView.Size.x / 2f - offset;
        }

        /// <summary>Distance between card centres for a hand of <paramref name="count"/>: as wide as fits, up to MaxSpacing.</summary>
        public float SpacingFor(int count)
        {
            if (count <= 1) return MaxSpacing;
            float left, right;
            Bounds(out left, out right);
            return Mathf.Clamp((right - left) / (count - 1), 30f, MaxSpacing);
        }

        float Centre()
        {
            float left, right;
            Bounds(out left, out right);
            return (left + right) / 2f;
        }

        public Vector2 SlotPosition(int index, int count)
        {
            float t = index - (count - 1) / 2f;
            return new Vector2(Centre() + t * SpacingFor(count), -t * t * ArcDrop);
        }

        /// <summary>The fan tilt for a slot: the left side leans left, the right side right.</summary>
        public float SlotAngle(int index, int count)
        {
            float t = index - (count - 1) / 2f;
            return Mathf.Clamp(-t * TiltPerCard, -MaxTilt, MaxTilt);
        }

        /// <summary>Spawn position for newly drawn cards: just right of the last slot.</summary>
        public Vector2 IncomingPosition(int count)
        {
            return SlotPosition(count, count + 1) + new Vector2(SpacingFor(count + 1) * 2f, 0f);
        }

        /// <summary>Re-poses every card in <see cref="Order"/> (with the hovered card's neighbours leaning aside).</summary>
        void ApplyPoses(List<CardInstance> order)
        {
            int hoverIndex = hoveredCard != null ? order.IndexOf(hoveredCard.Card) : -1;
            for (int i = 0; i < order.Count; i++)
            {
                var cv = Get(order[i]);
                if (cv == null) continue;
                var pos = SlotPosition(i, order.Count);
                if (hoverIndex >= 0 && i != hoverIndex)
                {
                    int d = i - hoverIndex;
                    pos.x += Mathf.Sign(d) * HoverPush / Mathf.Abs(d);
                }
                cv.SetPose(pos, SlotAngle(i, order.Count));
                if (cv != hoveredCard) cv.Rect.SetSiblingIndex(i);
            }
            if (hoveredCard != null) hoveredCard.Rect.SetAsLastSibling();
        }

        /// <summary>Called by a card when the pointer enters or leaves it.</summary>
        public void OnCardHover(CardView cv, bool on)
        {
            if (on) hoveredCard = cv;
            else if (hoveredCard == cv) hoveredCard = null;
            if (dragging == null) ApplyPoses(Order);
        }

        public CardView Get(CardInstance card)
        {
            CardView cv;
            return card != null && views.TryGetValue(card, out cv) && cv != null ? cv : null;
        }

        public CardView CreateView(CardInstance card, Vector2 anchoredStart)
        {
            var existing = Get(card);
            if (existing != null) return existing;
            var cv = CardView.Create(Rect, card, this);
            cv.Rect.anchoredPosition = anchoredStart;
            views[card] = cv;
            return cv;
        }

        /// <summary>Removes the view from tracking without destroying it (for fly-away animations).</summary>
        public CardView Detach(CardInstance card)
        {
            var cv = Get(card);
            views.Remove(card);
            return cv;
        }

        public void Layout(List<CardInstance> order, float duration)
        {
            Order = new List<CardInstance>(order);
            var keep = new HashSet<CardInstance>(order);

            var stale = new List<CardInstance>();
            foreach (var pair in views)
                if (!keep.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var card in stale)
            {
                var cv = Get(card);
                views.Remove(card);
                if (cv != null) Destroy(cv.gameObject);
            }

            if (hoveredCard != null && !keep.Contains(hoveredCard.Card)) hoveredCard = null;
            for (int i = 0; i < order.Count; i++)
                CreateView(order[i], IncomingPosition(order.Count));
            ApplyPoses(order);
        }

        public int IndexFromWorldX(float worldX)
        {
            if (Order.Count == 0) return 0;
            float localX = Rect.InverseTransformPoint(new Vector3(worldX, Rect.position.y, 0f)).x;
            int index = Mathf.RoundToInt((localX - Centre()) / SpacingFor(Order.Count) + (Order.Count - 1) / 2f);
            return Mathf.Clamp(index, 0, Order.Count - 1);
        }

        // ------------------------------------------------------------------ Drag preview

        CardView dragging;
        int dragIndex = -1;
        readonly List<CardView> highlighted = new List<CardView>();

        public void BeginDrag(CardView cv)
        {
            dragging = cv;
            dragIndex = -1;
            UpdateDrag(cv);
        }

        /// <summary>
        /// While dragging, the other cards slide apart to open a gap where the card would land, and
        /// any neighbor it would merge with there is highlighted.
        /// </summary>
        public void UpdateDrag(CardView cv)
        {
            if (cv != dragging) return;
            int index = IndexFromWorldX(cv.Rect.position.x);
            if (index == dragIndex) return;
            dragIndex = index;

            var preview = new List<CardInstance>(Order);
            preview.Remove(cv.Card);
            preview.Insert(Mathf.Clamp(index, 0, preview.Count), cv.Card);

            for (int i = 0; i < preview.Count; i++)
            {
                if (preview[i] == cv.Card) continue;
                var other = Get(preview[i]);
                if (other != null) other.SetPose(SlotPosition(i, preview.Count), SlotAngle(i, preview.Count));
            }

            ClearHighlights();
            int at = preview.IndexOf(cv.Card);
            bool merges = false;
            foreach (int n in new[] { at - 1, at + 1 })
            {
                if (n < 0 || n >= preview.Count || !cv.Card.CanMergeWith(preview[n])) continue;
                var neighbor = Get(preview[n]);
                if (neighbor == null) continue;
                neighbor.SetMergeHighlight(true, cv.Card.Rank + 1);
                highlighted.Add(neighbor);
                merges = true;
            }
            cv.SetMergeHighlight(merges, cv.Card.Rank + 1);
            if (merges) highlighted.Add(cv);
        }

        public void EndDrag(CardView cv)
        {
            if (cv != dragging) return;
            dragging = null;
            dragIndex = -1;
            ClearHighlights();
        }

        void ClearHighlights()
        {
            foreach (var cv in highlighted)
                if (cv != null) cv.SetMergeHighlight(false, 0);
            highlighted.Clear();
        }

        public void OnCardDropped(CardView cv)
        {
            int to = IndexFromWorldX(cv.Rect.position.x);
            if (!View.RequestMove(cv.Card, to))
                Layout(Order, 0.18f);
        }
    }
}
