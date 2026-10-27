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

        /// <summary>The Go button's size and font; the formation's Start button uses the same, so they match exactly.</summary>
        public static readonly Vector2 GoSize = new Vector2(300f, 106f);
        public const int GoFont = 68;

        /// <summary>A Go-sized button with the chevrons (every InkStyle button has the hover glow and the punch).</summary>
        public static Button GoStyleButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            return InkStyle.Button(parent, name, label, onClick, false, GoFont, true);
        }

        public RectTransform QueueRow { get; private set; }

        /// <summary>One queue slot, the strip's inner padding and the gap between slots.</summary>
        static readonly Vector2 ChipSize = new Vector2(120f, 84f);
        const float QueuePad = 8f, QueueGap = 10f, MaxQueueWidth = 760f;
        static readonly Color MovedGray = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        static readonly Color MovedRim = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        static readonly Color MovedIcon = new Color(0.62f, 0.62f, 0.62f, 1f);

        BattleView view;
        Text bannerText, turnText;
        CanvasGroup bannerGroup;
        Image bannerBg;
        GameObject resultPanel;
        RectTransform resultContent;
        GameObject resultButton;
        Button goButton, queueButton;
        GoButtonFx goFx;
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
            // Queue: a row of slots top-centre on an ink strip with a gold hairline, under the banner and above the
            // units: the cards you're about to play, in order, then a bright gold slot per AP left. The strip sizes
            // itself to its slots (RebuildQueue).
            var topCenter = new Vector2(0.5f, 1f);
            var queueBg = UIFactory.CreateImage(root, "Queue", UIFactory.WithAlpha(InkStyle.InkPanel, 0.88f));
            queueBg.rectTransform.Place(topCenter, topCenter, new Vector2(0f, -124f), new Vector2(440f, ChipSize.y + QueuePad * 2f));
            InkStyle.Frame(queueBg.rectTransform, InkStyle.Rule, 2f);
            QueueRow = queueBg.rectTransform;
            queueGroup = queueBg.gameObject.AddComponent<CanvasGroup>();
            queueGroup.alpha = QueueAlpha;
            var layout = queueBg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)QueuePad, (int)QueuePad, (int)QueuePad, (int)QueuePad);
            layout.spacing = QueueGap;
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

            // Go: an ink plate with corner brackets like every other button; hovering slides gold chevrons out, pressing
            // (click or Enter) punches and flashes it (GoButtonFx).
            goButton = GoStyleButton(root, "Go", UIText.Get("battle.go"), view.OnGo);
            goButton.GetComponent<RectTransform>().Place(bottomRight, bottomRight, new Vector2(-24f, 92f), GoSize);
            goFx = goButton.GetComponent<GoButtonFx>();

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
            // Continue / Fight Again: the same button as Go (ink plate, chevrons on hover, punch on press).
            var restart = GoStyleButton(result.transform, "Restart", UIText.Get("battle.fight_again"), view.OnRestart);
            resultButtonLabel = restart.GetComponentInChildren<Text>();
            restart.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(440f, GoSize.y));
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
            turnText.text = UIText.Get("battle.turn", "n", round.ToString());
        }

        /// <summary>AP shows as the empty queue slots; AP spent on moves shows as locked "Card Moved" slots.</summary>
        public void SetActionPoints(int current, int max, int moves = 0)
        {
            currentAP = current;
            movesUsed = moves;
            RebuildQueue();
        }

        int movesUsed;

        /// <summary>What you did this turn, in order: a queued card, or null for a card move. Undo always takes back the
        /// latest action, so this works like a stack; the slots show it left to right.</summary>
        readonly List<CardInstance> actions = new List<CardInstance>();
        /// <summary>The holders of the queued cards (where casts fly from).</summary>
        readonly List<RectTransform> cardHolders = new List<RectTransform>();

        /// <summary>Where the next card to be cast sits in the queue (world position), for the channel animation.</summary>
        public Vector3 FirstSlotPosition
        {
            get
            {
                var first = cardHolders.Count > 0 ? cardHolders[0] : QueueRow.childCount > 0 ? (RectTransform)QueueRow.GetChild(0) : QueueRow;
                return first.TransformPoint(first.rect.center);
            }
        }

        /// <summary>How many cards are in the queue as shown.</summary>
        public int QueuedCount { get { return queued.Count; } }

        public void SetQueue(List<CardInstance> queue)
        {
            queued = new List<CardInstance>(queue);
            RebuildQueue();
        }

        /// <summary>Brings the action list in line with the controller: cards no longer queued and moves taken back
        /// (the latest first) drop out; new cards and new moves join the end, in the order they happened.</summary>
        void SyncActions()
        {
            actions.RemoveAll(a => a != null && !queued.Contains(a));
            int moves = 0;
            foreach (var a in actions) if (a == null) moves++;
            for (int i = actions.Count - 1; i >= 0 && moves > movesUsed; i--)
                if (actions[i] == null) { actions.RemoveAt(i); moves--; }
            foreach (var card in queued) if (!actions.Contains(card)) actions.Add(card);
            for (; moves < movesUsed; moves++) actions.Add(null);
        }

        /// <summary>
        /// One slot per action this turn, in the order you took them (a queued card, or the gray ring for a card move),
        /// then a gold slot per AP left.
        /// </summary>
        void RebuildQueue()
        {
            SyncActions();
            UIFactory.ClearChildren(QueueRow);
            cardHolders.Clear();
            int slots = actions.Count + currentAP;
            var chipSize = ChipSize;
            // Many slots (a long turn) shrink to stay within MaxQueueWidth.
            float fit = (MaxQueueWidth - QueuePad * 2f - QueueGap * Mathf.Max(0, slots - 1)) / Mathf.Max(1, slots);
            if (fit < chipSize.x) chipSize.x = fit;
            QueueRow.sizeDelta = new Vector2(Mathf.Max(1, slots) * chipSize.x + QueueGap * Mathf.Max(0, slots - 1) + QueuePad * 2f,
                ChipSize.y + QueuePad * 2f);

            foreach (var card in actions)
            {
                if (card == null)
                {
                    // A move used up an action slot: it stays locked for the turn, a gray plate with the ring.
                    var locked = Slot("Moved", chipSize, MovedGray);
                    InkStyle.Frame(locked.rectTransform, MovedRim, 2f, -2f);
                    var swap = CardIcons.Swap;
                    if (swap != null)
                    {
                        var ring = UIFactory.CreateImage(locked.transform, "Swap", MovedIcon);
                        ring.sprite = swap;
                        ring.preserveAspect = true;
                        ring.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
                    }
                    continue;
                }
                var owner = card.Owner.Color;
                var chip = Slot("Card", chipSize, card.IsRitual ? CardView.RitualPastel(owner) : UIFactory.Darken(owner, 0.35f));
                cardHolders.Add((RectTransform)chip.rectTransform.parent);
                InkStyle.Frame(chip.rectTransform, Gold, 2f, -2f);
                // The card's own pixel icon (or the Ult art) instead of words.
                var art = card.IsRitual ? CardArt.Ult(card.Definition, card.Owner.Definition) : CardArt.Icon(card.Definition);
                if (art == null) art = CardIcons.Glyph(CardIcons.TypeOf(card.Definition));
                var icon = UIFactory.CreateImage(chip.transform, "Icon", Color.white);
                icon.sprite = art;
                icon.preserveAspect = true;
                icon.rectTransform.Fill(4f);
                int count = card.IsRitual ? 0 : CardStars.CountFor(card);
                if (count > 0) CardStars.Build((RectTransform)chip.transform, count, 13f, new Vector2(0f, 1f), new Vector2(4f, -3f));
            }

            // AP left: a warm gold slot with a bright rim and a diamond in the middle, one per AP.
            for (int i = 0; i < currentAP; i++)
            {
                var slot = Slot("EmptySlot", chipSize, new Color(1f, 0.8f, 0.3f, 0.16f));
                InkStyle.Frame(slot.rectTransform, InkStyle.GoldBright, 3f, -3f);
                var diamond = UIFactory.CreateImage(slot.transform, "AP", Color.white);
                diamond.sprite = CardIcons.Diamond;
                diamond.preserveAspect = true;
                diamond.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            }
        }

        /// <summary>A slot: a holder the row lays out, with the plate inside it.</summary>
        Image Slot(string name, Vector2 size, Color color)
        {
            var holder = UIFactory.CreateRect(name, QueueRow);
            holder.sizeDelta = size;
            var plate = UIFactory.CreateImage(holder, "Plate", color);
            plate.rectTransform.Fill();
            return plate;
        }

        void Update()
        {
            if (queueGroup != null && !Mathf.Approximately(queueGroup.alpha, queueAlphaTarget))
                queueGroup.alpha = Mathf.MoveTowards(queueGroup.alpha, queueAlphaTarget, Time.unscaledDeltaTime * 3f);
        }

        // HUD > QUEUE STRIP VISIBILITY
        // The strip shows (at 80%, so the battle shows through) while you plan, and fades out once your last card has
        // been used, until the next turn starts.

        const float QueueAlpha = 0.8f;
        CanvasGroup queueGroup;
        float queueAlphaTarget = QueueAlpha;

        public void ShowQueue(bool show)
        {
            queueAlphaTarget = show ? QueueAlpha : 0f;
            if (queueGroup != null) queueGroup.blocksRaycasts = show;
        }

        /// <summary>The battle log was removed (damage numbers and the banner say what happened); kept as a no-op so
        /// callers don't change.</summary>
        /// <summary>Go from the keyboard (Enter): the same press flourish as a click.</summary>
        public void PressGo()
        {
            if (goButton == null || !goButton.interactable) return;
            goButton.onClick.Invoke();
        }

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
