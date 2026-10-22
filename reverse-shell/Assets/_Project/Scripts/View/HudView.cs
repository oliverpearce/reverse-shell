using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>Round counter, AP pips, cast queue, Go/Undo buttons, combat log, banner and result screen.</summary>
    public class HudView : MonoBehaviour
    {
        static readonly Color PanelDark = new Color(0.1f, 0.09f, 0.13f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
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

            roundText = UIFactory.CreateText(root, "Round", "", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            roundText.rectTransform.Place(topLeft, topLeft, new Vector2(30f, -20f), new Vector2(400f, 44f));

            var logBg = UIFactory.CreateImage(root, "Log", PanelDark);
            logBg.rectTransform.Place(topLeft, topLeft, new Vector2(24f, -72f), new Vector2(430f, 150f));
            logText = UIFactory.CreateText(logBg.transform, "Text", "", 15, new Color(0.85f, 0.85f, 0.9f), TextAnchor.LowerLeft);
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            logText.rectTransform.Fill(8f);

            // AP pips (bottom-left)
            var apLabel = UIFactory.CreateText(root, "APLabel", "AP", 26, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            apLabel.rectTransform.Place(bottomLeft, bottomLeft, new Vector2(30f, 250f), new Vector2(60f, 36f));
            apRow = UIFactory.CreateRect("APRow", root).Place(bottomLeft, bottomLeft, new Vector2(80f, 252f), new Vector2(300f, 32f));

            var hint = UIFactory.CreateText(root, "Hint",
                "Click card: queue (1 AP)\nDrag card: move & merge (1 AP)\nClick enemy: target\nHold a unit: full details", 15,
                new Color(0.7f, 0.7f, 0.75f), TextAnchor.UpperLeft);
            hint.rectTransform.Place(bottomLeft, bottomLeft, new Vector2(30f, 160f), new Vector2(280f, 80f));

            // Queue (bottom-right, above buttons)
            queueLabel = UIFactory.CreateText(root, "QueueLabel", "QUEUE", 18, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            queueLabel.rectTransform.Place(bottomRight, bottomRight, new Vector2(-24f, 382f), new Vector2(440f, 24f));
            var queueBg = UIFactory.CreateImage(root, "Queue", PanelDark);
            queueBg.rectTransform.Place(bottomRight, bottomRight, new Vector2(-24f, 290f), new Vector2(440f, 88f));
            QueueRow = queueBg.rectTransform;
            var layout = queueBg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            undoButton = UIFactory.CreateButton(root, "Undo", "UNDO", new Color(0.35f, 0.35f, 0.42f), view.OnUndo, 22);
            undoButton.GetComponent<RectTransform>().Place(bottomRight, bottomRight, new Vector2(-24f, 200f), new Vector2(200f, 64f));
            goButton = UIFactory.CreateButton(root, "Go", "GO", new Color(0.85f, 0.45f, 0.2f), view.OnGo, 34);
            goButton.GetComponent<RectTransform>().Place(bottomRight, bottomRight, new Vector2(-24f, 100f), new Vector2(200f, 84f));

            // Banner (top-center)
            bannerBg = UIFactory.CreateImage(root, "Banner", PanelDark);
            bannerBg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(720f, 72f));
            bannerGroup = bannerBg.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerText = UIFactory.CreateText(bannerBg.transform, "Text", "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).BestFit(16, 34);
            bannerText.rectTransform.Fill(8f);

            // Result overlay
            var result = UIFactory.CreateImage(root, "Result", new Color(0f, 0f, 0f, 0.75f), true);
            result.rectTransform.Fill();
            resultPanel = result.gameObject;
            resultText = UIFactory.CreateText(result.transform, "Text", "", 96, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1000f, 140f));
            var restart = UIFactory.CreateButton(result.transform, "Restart", "FIGHT AGAIN", new Color(0.85f, 0.45f, 0.2f), view.OnRestart, 28);
            resultButtonLabel = restart.GetComponentInChildren<Text>();
            restart.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(300f, 80f));
            rewardsRect = UIFactory.CreateRect("Rewards", result.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(620f, 240f));
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
            roundText.text = "TURN " + round;
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
            resultText.text = victory ? "VICTORY" : "DEFEAT";
            resultButtonLabel.text = view.ResultButtonLabel;
            resultText.color = victory ? Gold : new Color(0.85f, 0.3f, 0.3f);
            resultPanel.transform.localScale = Vector3.one * 0.9f;
            StartCoroutine(Tween.Scale(resultPanel.transform, Vector3.one, 0.3f, Tween.OutBack));
        }

        /// <summary>"+XP" headline, then one row per member: name, big level number, XP toward the next level.</summary>
        void ShowRewards(BattleResult result)
        {
            UIFactory.ClearChildren(rewardsRect);
            if (result == null || result.XpGained <= 0) return;

            var xp = UIFactory.CreateText(rewardsRect, "Xp", "+" + result.XpGained + " XP", 44, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
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
