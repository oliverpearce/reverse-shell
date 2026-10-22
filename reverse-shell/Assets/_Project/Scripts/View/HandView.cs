using System.Collections.Generic;
using UnityEngine;

namespace Combat.View
{
    /// <summary>
    /// Lays out CardViews to match a hand snapshot. Views are keyed by CardInstance, so any
    /// snapshot (draw, merge, undo...) can be applied and missing/extra views are reconciled.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        public const float Spacing = 162f;
        public const float PreviewScale = 1.9f;

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
            CardSettings.Changed += RefreshFaces;
        }

        void OnDestroy()
        {
            CardSettings.Changed -= RefreshFaces;
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

        public Vector2 SlotPosition(int index, int count)
        {
            return new Vector2((index - (count - 1) / 2f) * Spacing, 0f);
        }

        /// <summary>Spawn position for newly drawn cards: just right of the last slot.</summary>
        public Vector2 IncomingPosition(int count)
        {
            return SlotPosition(count, count + 1) + new Vector2(Spacing * 2f, 0f);
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

            for (int i = 0; i < order.Count; i++)
            {
                var cv = CreateView(order[i], IncomingPosition(order.Count));
                cv.Rect.SetSiblingIndex(i);
                cv.AnimateTo(SlotPosition(i, order.Count), duration);
            }
        }

        public int IndexFromWorldX(float worldX)
        {
            if (Order.Count == 0) return 0;
            float localX = Rect.InverseTransformPoint(new Vector3(worldX, Rect.position.y, 0f)).x;
            int index = Mathf.RoundToInt(localX / Spacing + (Order.Count - 1) / 2f);
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
                if (other != null) other.AnimateTo(SlotPosition(i, preview.Count), 0.12f);
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
