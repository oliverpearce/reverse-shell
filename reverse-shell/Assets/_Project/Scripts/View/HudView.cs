using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>Round counter, AP pips, cast queue, Go/Undo buttons, combat log, banner and result screen.</summary>
    public class HudView : MonoBehaviour
    {
        // Reverse: 1999 look (InkStyle): ink panels with gold hairlines, serif headings, gold numbers.
        static readonly Color PanelDark = InkStyle.InkPanel;
        static readonly Color Gold = InkStyle.Gold;
        const int MaxLogLines = 7;

        public RectTransform QueueRow { get; private set; }

        BattleView view;
        Text roundText, logText, bannerText, resultText;
        CanvasGroup bannerGroup;
        Image bannerBg;
        RectTransform apRow;
        GameObject resultPanel;
        RectTransform rewardsRect;
        Button goButton, undoButton;
        readonly List<string> logLines = new List<string>();
        Coroutine bannerRoutine;
        Text queueLabel, resultButtonLabel;
        List<CardInstance> queued = new List<CardInstance>();
        int currentAP;

        public void Build(BattleView battleView)
        {
            view = battleView;
            CardSettings.Changed += RebuildQueue;
            var root = (RectTransform)transform;
            root.Fill();

            var topLeft = new Vector2(0f, 1f);
            var bottomLeft = Vector2.zero;
            var bottomRight = new Vector2(1f, 0f);

            // Turn counter: a serif "Turn N" on an ink plate with the hero frame.
            var roundPlate = InkStyle.Panel(root, "RoundPlate", PanelDark);
            roundPlate.rectTransform.Place(topLeft, topLeft, new Vector2(24f, -18f), new Vector2(260f, 76f));
            InkStyle.HeroFrame(roundPlate.rectTransform, InkStyle.Gold, 6f);
            roundText = InkStyle.Title(roundPlate.transform, "Round", "", 50, InkStyle.Paper, TextAnchor.MiddleCenter);
            roundText.rectTransform.Fill(6f);

            var logBg = UIFactory.CreateImage(root, "Log", PanelDark);
            logBg.rectTransform.Place(topLeft, topLeft, new Vector2(24f, -108f), new Vector2(440f, 150f));
            InkStyle.Frame(logBg.rectTransform, InkStyle.Rule, 1f);
            logText = UIFactory.CreateText(logBg.transform, "Text", "", 15, new Color(0.88f, 0.88f, 0.92f), TextAnchor.LowerLeft);
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            logText.rectTransform.Fill(10f);

            // AP pips (bottom-left)
            var apLabel = UIFactory.CreateText(root, "APLabel", "AP", 40, Gold, TextAnchor.MiddleLeft);
            apLabel.font = UIFactory.SerifFont;
            apLabel.gameObject.AddComponent<Shadow>().effectColor = InkStyle.Ink;
            apLabel.rectTransform.Place(bottomLeft, bottomLeft, new Vector2(30f, 246f), new Vector2(70f, 50f));
            apRow = UIFactory.CreateRect("APRow", root).Place(bottomLeft, bottomLeft, new Vector2(96f, 254f), new Vector2(300f, 32f));

            var hint = UIFactory.CreateText(root, "Hint",
                "Click card: queue (1 AP)\nDrag card: move & merge (1 AP)\nClick enemy: target\nHold a unit: full details", 15,
                new Color(0.85f, 0.85f, 0.9f, 0.9f), TextAnchor.UpperLeft);
            hint.rectTransform.Place(bottomLeft, bottomLeft, new Vector2(30f, 160f), new Vector2(280f, 80f));
            hint.gameObject.AddComponent<Shadow>().effectColor = InkStyle.Ink;

            // Queue
            // The queue sits in the middle, right above the hand: the cards you're about to play, in order.
            var bottomCenter = new Vector2(0.5f, 0f);
            queueLabel = InkStyle.Title(root, "QueueLabel", "QUEUE", 30, InkStyle.Paper, TextAnchor.MiddleCenter);
            queueLabel.gameObject.AddComponent<Shadow>().effectColor = InkStyle.Ink;
            queueLabel.rectTransform.Place(bottomCenter, bottomCenter, new Vector2(-60f, 384f), new Vector2(440f, 36f));
            var queueBg = UIFactory.CreateImage(root, "Queue", PanelDark);
            queueBg.rectTransform.Place(bottomCenter, bottomCenter, new Vector2(-60f, 290f), new Vector2(440f, 88f));
            QueueRow = queueBg.rectTransform;
            InkStyle.Frame(UIFactory.CreateRect("QueueFrame", root).Place(bottomCenter, bottomCenter, new Vector2(-60f, 290f), new Vector2(440f, 88f)), InkStyle.Rule, 1f);
            var layout = queueBg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Buttons: straight ink plates with gold hairlines. GO is the gold one.
            undoButton = InkStyle.Button(root, "Undo", "Undo", view.OnUndo, false, 34);
            undoButton.GetComponent<RectTransform>().Place(bottomRight, bottomRight, new Vector2(-24f, 196f), new Vector2(220f, 66f));
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
            foreach (var y in new[] { 196f, -6f })
            {
                var rule = InkStyle.Line(result.transform, "Rule", InkStyle.Gold);
                rule.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(900f, 2f));
            }
            resultText = InkStyle.Title(result.transform, "Text", "", 150, InkStyle.Paper, TextAnchor.MiddleCenter);
            resultText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(1200f, 190f));
            var restart = InkStyle.Button(result.transform, "Restart", "Fight Again", view.OnRestart, true, 40);
            resultButtonLabel = restart.GetComponentInChildren<Text>();
            restart.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(360f, 86f));
            rewardsRect = UIFactory.CreateRect("Rewards", result.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(660f, 240f));
            resultPanel.SetActive(false);
        }

        void OnDestroy()
        {
            CardSettings.Changed -= RebuildQueue;
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
            if (undoButton.interactable != undo) undoButton.interactable = undo;
        }

        public void SetRound(int round)
        {
            roundText.text = "Turn " + round;
        }

        public void SetActionPoints(int current, int max)
        {
            currentAP = current;
            UIFactory.ClearChildren(apRow);
            for (int i = 0; i < max; i++)
            {
                var pip = UIFactory.CreateImage(apRow, "AP" + i, i < current ? Gold : new Color(0.25f, 0.24f, 0.3f));
                pip.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 40f, 0f), new Vector2(30f, 30f));
                pip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            RebuildQueue();
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
            int slots = queued.Count + currentAP;
            queueLabel.text = "AP: " + currentAP;

            const float rowWidth = 440f - 16f, gap = 8f;
            float chipWidth = slots > 0 ? Mathf.Min(100f, (rowWidth - gap * (slots - 1)) / slots) : 100f;
            var chipSize = new Vector2(chipWidth, 72f);

            foreach (var card in queued)
            {
                var owner = card.Owner.Definition.color;
                var chip = UIFactory.CreateImage(QueueRow, card.Name, card.IsRitual ? CardView.RitualPastel(owner) : UIFactory.Darken(owner, 0.35f));
                chip.rectTransform.sizeDelta = chipSize;
                var outline = chip.gameObject.AddComponent<Outline>();
                outline.effectColor = Gold;
                outline.effectDistance = new Vector2(2f, -2f);
                bool stars = CardSettings.RankStars;
                var label = card.IsRitual || stars ? card.Name : card.Name + "\n" + UIFactory.Roman(card.Rank);
                var textColor = card.IsRitual ? new Color(0.22f, 0.18f, 0.28f) : Color.white;
                var text = UIFactory.CreateText(chip.transform, "Label", label, 14, textColor, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(9, 14);
                text.rectTransform.Fill(4f);
                if (stars)
                {
                    int count = CardStars.CountFor(card);
                    if (count > 0)
                    {
                        text.rectTransform.offsetMax = new Vector2(-4f, -16f); // leave the top row for the stars
                        CardStars.Build((RectTransform)chip.transform, count, 13f, new Vector2(0f, 1f), new Vector2(4f, -3f));
                    }
                }
            }

            for (int i = 0; i < currentAP; i++)
            {
                var slot = UIFactory.CreateImage(QueueRow, "EmptySlot" + i, new Color(1f, 0.8f, 0.3f, 0.06f));
                slot.rectTransform.sizeDelta = chipSize;
                var outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = UIFactory.WithAlpha(Gold, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);
                var plus = UIFactory.CreateText(slot.transform, "Plus", "+", 34, UIFactory.WithAlpha(Gold, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
                plus.rectTransform.Fill();
            }
        }

        public void Log(string line)
        {
            logLines.Add(line);
            while (logLines.Count > MaxLogLines) logLines.RemoveAt(0);
            logText.text = string.Join("\n", logLines.ToArray());
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
            ShowRewards(result);
            resultText.text = victory ? "Victory" : "Defeat";
            resultButtonLabel.text = view.ResultButtonLabel;
            resultText.color = victory ? InkStyle.GoldBright : InkStyle.Crimson;
            resultPanel.transform.localScale = Vector3.one * 0.9f;
            StartCoroutine(Tween.Scale(resultPanel.transform, Vector3.one, 0.3f, Tween.OutBack));
        }

        /// <summary>"+XP" headline, then one row per member: name, big level number, XP toward the next level.</summary>
        void ShowRewards(BattleResult result)
        {
            UIFactory.ClearChildren(rewardsRect);
            if (result == null || result.XpGained <= 0) return;

            var xp = InkStyle.Title(rewardsRect, "Xp", "+" + result.XpGained + " XP", 56, Gold, TextAnchor.MiddleCenter);
            xp.rectTransform.TopStrip(0f, 52f);

            float y = 62f;
            foreach (var entry in result.Party)
            {
                var member = entry.Key;
                var progress = entry.Value;
                int levels;
                bool leveled = result.LevelsGained.TryGetValue(member, out levels);

                var row = UIFactory.CreateImage(rewardsRect, member.DisplayName, PanelDark);
                row.rectTransform.TopStrip(y, 52f);
                var swatch = UIFactory.CreateImage(row.transform, "Color", member.color);
                swatch.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(6f, 36f));

                var name = UIFactory.CreateText(row.transform, "Name", member.DisplayName, 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                name.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(170f, 44f));

                var level = UIFactory.CreateText(row.transform, "Level", "Lv " + progress.Level, 32,
                    leveled ? new Color(0.5f, 1f, 0.55f) : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                level.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(130f, 44f));

                string right = leveled ? "LEVEL UP!" : progress.Xp + " / " + Leveling.XpToNext(progress.Level) + " XP";
                var detail = UIFactory.CreateText(row.transform, "Detail", right, leveled ? 26 : 22,
                    leveled ? new Color(0.5f, 1f, 0.55f) : new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleRight, FontStyle.Bold);
                detail.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(260f, 44f));
                y += 58f;
            }
        }
    }
}
