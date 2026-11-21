using System;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// A mini-boss's reward: Sun, Moon and Star (whichever can still be found) face up, each with what it does. Hover or
    /// the arrow keys choose; a click or Enter takes one.
    /// </summary>
    public class EnchantReward : MonoBehaviour
    {
        const float CardScale = 1.5f, Spacing = 470f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        readonly List<RectTransform> cards = new List<RectTransform>();
        List<CardBinding> choices;
        Action<CardBinding> picked;
        int selected, openedFrame;

        /// <summary>The enchants that can still be found (each has a cap, PackRates.maxPerBinding).</summary>
        public static List<CardBinding> Available()
        {
            var open = new List<CardBinding>();
            foreach (var kind in new[] { CardBinding.Sun, CardBinding.Moon, CardBinding.Star })
                if (CardCollection.CanFind(kind)) open.Add(kind);
            return open;
        }

        public static EnchantReward Show(RectTransform canvas, List<CardBinding> choices, Action<CardBinding> picked)
        {
            var rt = UIFactory.CreateRect("EnchantReward", canvas).Fill();
            rt.SetAsLastSibling();
            var screen = rt.gameObject.AddComponent<EnchantReward>();
            screen.choices = choices;
            screen.picked = picked;
            screen.openedFrame = Time.frameCount;
            screen.Build(rt);
            return screen;
        }

        void Build(RectTransform rt)
        {
            UIFactory.CreateImage(rt, "Wash", new Color(0.03f, 0.028f, 0.025f, 0.9f), true).rectTransform.Fill();
            var title = InkStyle.Title(rt, "Title", UIText.Get("enchant.title"), UIFactory.TextSize.Title, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1600f, 100f));
            var hint = UIFactory.CreateText(rt, "Hint", UIText.Get("enchant.hint"), UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleCenter);
            hint.supportRichText = true;
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1400f, 50f));

            for (int i = 0; i < choices.Count; i++)
            {
                float x = (i - (choices.Count - 1) / 2f) * Spacing;
                var card = UIFactory.CreateRect("Enchant" + i, rt).Place(Center, Center, new Vector2(x, 70f), CardView.Size * CardScale);
                var hit = card.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var face = UIFactory.CreateRect("Face", card).Fill();
                ArcanaCards.BuildFace(face, choices[i], CardScale);
                foreach (var g in face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                InkStyle.Frame(UIFactory.CreateRect("Selected", card).Fill(-10f), InkStyle.GoldBright, 4f);
                cards.Add(card);

                var effect = UIFactory.CreateText(rt, "Effect" + i, UIText.Get("enchant.when", "effect", CardsScreen.BindingLine(choices[i])),
                    UIFactory.TextSize.Small, InkStyle.Paper, TextAnchor.UpperCenter);
                effect.rectTransform.Place(Center, new Vector2(0.5f, 1f), new Vector2(x, 70f - CardView.Size.y * CardScale * 0.5f - 26f), new Vector2(430f, 130f));

                int index = i;
                var trigger = card.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => Select(index));
                trigger.triggers.Add(enter);
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => { Select(index); Take(); });
                trigger.triggers.Add(click);
            }
            Select(0);
        }

        void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, cards.Count - 1);
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].localScale = Vector3.one * (i == selected ? 1.06f : 0.96f);
                cards[i].Find("Selected").gameObject.SetActive(i == selected);
            }
        }

        void Take()
        {
            if (picked == null || Time.frameCount <= openedFrame + 1) return;
            var then = picked;
            picked = null;
            var kind = choices[selected];
            Destroy(gameObject);
            then(kind);
        }

        void Update()
        {
            if (Time.frameCount <= openedFrame + 1) return;
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Select(selected - 1);
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Select(selected + 1);
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space)) Take();
        }
    }
}
