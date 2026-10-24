using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>Turn number, AP pips, the cast queue (click it to take every card back), Go, banner and result screen.</summary>
    public class HudView : MonoBehaviour
    {
        // Reverse: 1999 look (InkStyle): ink panels with gold hairlines, serif headings, gold numbers.
        static readonly Color PanelDark = InkStyle.InkPanel;
        static readonly Color Gold = InkStyle.Gold;

        public RectTransform QueueRow { get; private set; }

        BattleView view;
        Text bannerText, turnText;
        CanvasGroup bannerGroup;
        Image bannerBg;
        GameObject resultPanel;
        RectTransform resultContent;
        GameObject resultButton;
        Button goButton, queueButton;
        Coroutine bannerRoutine;
        Text resultButtonLabel;
        List<CardInstance> queued = new List<CardInstance>();
        int currentAP;

        public void Build(BattleView battleView)
        {
            view = battleView;
            var root = (RectTransform)transform;
            root.Fill();

            var bottomRight = new Vector2(1f, 0f);

            // Turn counter, top-left: one line ("TURN 3") with room to spare so it never wraps. No AP pips (the
            // empty queue slots are the AP left).
            var topLeft = new Vector2(0f, 1f);
            turnText = UIFactory.CreateText(root, "Turn", "", 44, InkStyle.Paper, TextAnchor.UpperLeft, FontStyle.Bold);
            turnText.supportRichText = true;
            turnText.horizontalOverflow = HorizontalWrapMode.Overflow;
            turnText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            turnText.rectTransform.Place(topLeft, topLeft, new Vector2(32f, -24f), new Vector2(420f, 80f));
            // Queue: a slim row of slots right above the hand (no panel, no labels): the cards you're about to play,
            // in order, then an empty frame per AP left.
            var bottomCenter = new Vector2(0.5f, 0f);
            var queueBg = UIFactory.CreateImage(root, "Queue", new Color(0f, 0f, 0f, 0f));
            queueBg.rectTransform.Place(bottomCenter, bottomCenter, new Vector2(-60f, 384f), new Vector2(440f, 76f));
            QueueRow = queueBg.rectTransform;
            var layout = queueBg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // Clicking the queue sends every queued card back to the hand (this replaces the Undo button).
            queueBg.raycastTarget = true;   // CreateImage makes images click-through by default; the chips stay click-through
            queueButton = queueBg.gameObject.AddComponent<Button>();
            queueButton.transition = Selectable.Transition.None;
            queueButton.onClick.AddListener(view.OnReturnAll);

            // Go: a straight ink plate with gold hairlines, the gold one.
            goButton = InkStyle.Button(root, "Go", "Go", view.OnGo, true, 60);
            goButton.GetComponent<RectTransform>().Place(bottomRight, bottomRight, new Vector2(-24f, 92f), new Vector2(260f, 92f));

            // Banner (top-center): ink band, serif.
            bannerBg = UIFactory.CreateImage(root, "Banner", PanelDark);
            bannerBg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(720f, 76f));
            bannerGroup = bannerBg.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            InkStyle.Frame(bannerBg.rectTransform, InkStyle.Rule, 1f);
            bannerText = InkStyle.Title(bannerBg.transform, "Text", "", 46, InkStyle.Paper, TextAnchor.MiddleCenter).BestFit(20, 46);
            bannerText.rectTransform.Fill(10f);

            // Result overlay: an ink wash with paper grain, a big serif title between gold rules, a gold button.
            var result = UIFactory.CreateImage(root, "Result", new Color(0.07f, 0.065f, 0.06f, 0.9f), true);
            result.rectTransform.Fill();
            resultPanel = result.gameObject;
            InkStyle.Grain(result.rectTransform, new Color(1f, 0.95f, 0.85f, 0.035f));
            // The animated results (ResultScreen) build into this; the button shows when they finish.
            resultContent = UIFactory.CreateRect("Content", result.transform).Fill();
            var restart = InkStyle.Button(result.transform, "Restart", "Fight Again", view.OnRestart, true, 44);
            resultButtonLabel = restart.GetComponentInChildren<Text>();
            restart.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(420f, 96f));
            resultButton = restart.gameObject;
            resultPanel.SetActive(false);
        }

        void OnDestroy()
        {
        }

        public void BringOverlaysToFront()
        {
            bannerBg.transform.SetAsLastSibling();
            resultPanel.transform.SetAsLastSibling();
        }

        public void SetInteractable(bool interactable)
        {
            if (goButton.interactable != interactable) goButton.interactable = interactable;
            bool undo = interactable && view.Controller.CanUndo;
            if (queueButton.interactable != undo) queueButton.interactable = undo;
        }

        public void SetRound(int round)
        {
            turnText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Gold) + ">TURN</color>  " + round;
        }

        /// <summary>AP shows as the empty queue slots; AP spent on moves shows as locked "Card Moved" slots.</summary>
        public void SetActionPoints(int current, int max, int moves = 0)
        {
            currentAP = current;
            movesUsed = moves;
            RebuildQueue();
        }

        int movesUsed;

        /// <summary>Where the next card to be cast sits in the queue (world position), for the channel animation.</summary>
        public Vector3 FirstSlotPosition
        {
            get
            {
                if (QueueRow.childCount > 0)
                {
                    var first = (RectTransform)QueueRow.GetChild(0);
                    return first.TransformPoint(first.rect.center);
                }
                return QueueRow.TransformPoint(QueueRow.rect.center);
            }
        }

        public void SetQueue(List<CardInstance> queue)
        {
            queued = new List<CardInstance>(queue);
            RebuildQueue();
        }

        /// <summary>
        /// Shows one bordered slot per card you can still play this round: queued cards fill slots,
        /// empty frames show remaining AP.
        /// </summary>
        void RebuildQueue()
        {
            UIFactory.ClearChildren(QueueRow);
            int slots = queued.Count + movesUsed + currentAP;

            const float rowWidth = 440f - 8f, gap = 8f;
            float chipWidth = slots > 0 ? Mathf.Min(100f, (rowWidth - gap * (slots - 1)) / slots) : 100f;
            var chipSize = new Vector2(chipWidth, 68f);

            foreach (var card in queued)
            {
                var owner = card.Owner.Definition.color;
                var chip = UIFactory.CreateImage(QueueRow, card.Name, card.IsRitual ? CardView.RitualPastel(owner) : UIFactory.Darken(owner, 0.35f));
                chip.rectTransform.sizeDelta = chipSize;
                var outline = chip.gameObject.AddComponent<Outline>();
                outline.effectColor = Gold;
                outline.effectDistance = new Vector2(2f, -2f);
                // The card's own pixel icon (or the Ult art) instead of words.
                var art = card.IsRitual ? CardArt.Ult(card.Owner.Definition) : CardArt.Icon(card.Definition);
                if (art == null) art = CardIcons.Glyph(CardIcons.TypeOf(card.Definition));
                var icon = UIFactory.CreateImage(chip.transform, "Icon", Color.white);
                icon.sprite = art;
                icon.preserveAspect = true;
                icon.rectTransform.Fill(4f);
                int count = card.IsRitual ? 0 : CardStars.CountFor(card);
                if (count > 0) CardStars.Build((RectTransform)chip.transform, count, 13f, new Vector2(0f, 1f), new Vector2(4f, -3f));
            }

            // A move used up an action slot: it stays locked for the turn and says so.
            for (int i = 0; i < movesUsed; i++)
            {
                var locked = UIFactory.CreateImage(QueueRow, "Moved" + i, new Color(0.12f, 0.11f, 0.1f, 0.92f));
                locked.rectTransform.sizeDelta = chipSize;
                var lockedEdge = locked.gameObject.AddComponent<Outline>();
                lockedEdge.effectColor = UIFactory.WithAlpha(InkStyle.PaperDim, 0.7f);
                lockedEdge.effectDistance = new Vector2(2f, -2f);
                var text = UIFactory.CreateText(locked.transform, "Label", "CARD\nMOVED", 16, InkStyle.PaperDim, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(9, 16);
                text.lineSpacing = 0.85f;
                text.rectTransform.Fill(4f);
            }

            for (int i = 0; i < currentAP; i++)
            {
                var slot = UIFactory.CreateImage(QueueRow, "EmptySlot" + i, new Color(1f, 0.8f, 0.3f, 0.06f));
                slot.rectTransform.sizeDelta = chipSize;
                var outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = UIFactory.WithAlpha(Gold, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        /// <summary>The battle log was removed (damage numbers and the banner say what happened); kept as a no-op so
        /// callers don't change.</summary>
        public void Log(string line)
        {
        }

        public void ShowBanner(string text, Color color, float hold = 0.7f)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerText.text = text;
            bannerBg.color = Color.Lerp(PanelDark, UIFactory.WithAlpha(color, 0.9f), 0.5f);
            bannerRoutine = StartCoroutine(BannerRoutine(hold));
        }

        IEnumerator BannerRoutine(float hold)
        {
            yield return Tween.Fade(bannerGroup, 1f, 0.1f);
            yield return Tween.Wait(hold);
            yield return Tween.Fade(bannerGroup, 0f, 0.25f);
        }

        public void ShowResult(bool victory, BattleResult result)
        {
            resultPanel.SetActive(true);
            resultButtonLabel.text = view.ResultButtonLabel;
            ResultScreen.Host = this;
            StartCoroutine(ResultScreen.Play(resultContent, victory, result, resultButton));
        }
    }
}
