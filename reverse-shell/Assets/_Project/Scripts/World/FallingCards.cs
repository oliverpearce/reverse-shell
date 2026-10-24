using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The title screen's background: the party's cards (deck, pool and Ult cards, which keep their shine) drifting down
    /// the screen at a few sizes and speeds, gently swaying and turning, dimmed so the menu stays readable. Runs on
    /// unscaled time (the world is paused under the title).
    /// </summary>
    public class FallingCards : MonoBehaviour
    {
        const float SpawnEvery = 0.55f;          // seconds between new cards
        const float Dim = 0.38f;                 // card opacity behind the menu
        static readonly float[] Scales = { 0.6f, 0.8f, 1f };   // 3x, 4x, 5x card pixels

        class Faller
        {
            public RectTransform Rect;
            public float Speed, Sway, Phase, Spin;
            public float X;
        }

        readonly List<CardInstance> deck = new List<CardInstance>();
        readonly List<Faller> fallers = new List<Faller>();
        RectTransform area;
        float timer;
        int next;

        public static FallingCards Create(RectTransform parent, IEnumerable<CharacterDefinition> party)
        {
            var rt = UIFactory.CreateRect("FallingCards", parent).Fill();
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = Dim;
            group.blocksRaycasts = false;
            group.interactable = false;
            var falling = rt.gameObject.AddComponent<FallingCards>();
            falling.area = rt;
            if (party != null)
                foreach (var member in party)
                {
                    if (member == null) continue;
                    var owner = new BattleUnit(member, Team.Player);
                    foreach (var card in member.AllIncantations())
                        falling.deck.Add(new CardInstance(card, owner, Random.Range(1, Mathf.Max(1, card.MaxRank) + 1)));
                    var ult = WorldSession.UltimateOf(member);
                    if (ult != null) falling.deck.Add(new CardInstance(ult, owner, 1));
                }
            Shuffle(falling.deck);
            // Start with the screen already part-filled rather than empty.
            for (int i = 0; i < 6; i++) falling.Spawn(Random.Range(0.1f, 0.9f));
            return falling;
        }

        static void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        /// <param name="startHeight">Where it starts, 1 = just above the top edge, 0 = the bottom.</param>
        void Spawn(float startHeight)
        {
            if (deck.Count == 0) return;
            var card = deck[next++ % deck.Count];
            float s = Scales[Random.Range(0, Scales.Length)];
            var rt = UIFactory.CreateRect("Card_" + card.Name, area);
            rt.sizeDelta = CardView.Size * s;
            CardView.BuildFace(rt, card, s, true);
            foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            // Smaller cards are further away: slower and further back.
            rt.SetSiblingIndex(s < 0.7f ? 0 : rt.GetSiblingIndex());

            var size = area.rect.size;
            if (size.x < 400f || size.y < 400f) size = UIFactory.ReferenceResolution;   // before the first layout
            float x = Random.Range(-size.x / 2f + 80f, size.x / 2f - 80f);
            float top = size.y / 2f + CardView.Size.y * s;
            float y = Mathf.Lerp(-top, top, startHeight);
            rt.anchoredPosition = new Vector2(x, y);
            fallers.Add(new Faller
            {
                Rect = rt,
                X = x,
                Speed = Mathf.Lerp(70f, 150f, s) * Random.Range(0.85f, 1.15f),
                Sway = Random.Range(18f, 40f),
                Phase = Random.Range(0f, 6.28f),
                Spin = Random.Range(-12f, 12f),
            });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            timer += dt;
            if (timer >= SpawnEvery)
            {
                timer = 0f;
                Spawn(1f);
            }
            float bottom = -Mathf.Max(area.rect.height, 400f) / 2f;
            float t = Time.unscaledTime;
            for (int i = fallers.Count - 1; i >= 0; i--)
            {
                var f = fallers[i];
                if (f.Rect == null)
                {
                    fallers.RemoveAt(i);
                    continue;
                }
                var p = f.Rect.anchoredPosition;
                p.y -= f.Speed * dt;
                p.x = f.X + Mathf.Sin(t * 0.8f + f.Phase) * f.Sway;
                f.Rect.anchoredPosition = p;
                f.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.6f + f.Phase) * f.Spin);
                if (p.y < bottom - f.Rect.rect.height)
                {
                    Destroy(f.Rect.gameObject);
                    fallers.RemoveAt(i);
                }
            }
        }
    }
}
