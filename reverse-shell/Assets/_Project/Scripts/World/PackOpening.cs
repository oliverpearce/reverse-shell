using System;
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// ITEMS > CARDS > PACK OPENING
    /// Opening a card pack (Items → Cards → Open Pack):
    ///   1. the pack drops in and sways; click / Enter tears the top off (a flash, the strip flies away)
    ///   2. its cards come out one at a time: each rises big into the middle face down, flips, plays its rarity and
    ///      then slides down into the row. The last (third) card waits longer and trembles before it flips; it's the
    ///      only one that can be rare.
    ///   3. each flip plays its rarity:
    ///        Common  a soft white-gold sparkle burst
    ///        Rare    an orange glow behind the card, orange sparks and a warm pulse (Sun, Moon, Star)
    ///        SOUL    the screen dims, the card shakes inside a growing rainbow glow with spinning rays, then a ring
    ///                shockwave, a rainbow fountain, a flash and the Soul card's name
    ///   4. under each card: its name; click a revealed card to zoom in and tilt it
    ///   5. Done, or Open (bottom right, where Skip was) while packs are left
    /// Skip (bottom right): holding it, or holding Enter, fills it; full, it jumps straight to step 5.
    /// The cards are rolled and added to the collection (and saved) the moment the pack opens, so closing early loses
    /// nothing. The pack art is the user's pick from the argametina packs (Resources/Cards/Pack.png); until it's there a
    /// plain ink pack stands in. Unscaled time throughout.
    /// </summary>
    public class PackOpening : MonoBehaviour
    {
        const float CardScale = 0.85f, CardStep = 310f;
        const float FocusScale = 1.4f, FocusY = 60f, RowY = 40f;   // the card being revealed: big, up in its own column
        const float RiseSeconds = 0.3f, BeatSeconds = 0.15f, LastBeatSeconds = 0.35f, SuspenseSeconds = 0.9f;
        const float ShowSeconds = 0.9f, SettleSeconds = 0.3f, SkipHoldSeconds = 0.6f;
        const float DealSeconds = 0.55f, DealGap = 0.09f, SweepSeconds = 0.3f;
        static readonly Color Gold = InkStyle.GoldBright;

        class Slot
        {
            public CardPacks.Pull Pull;
            public RectTransform Rect, Back, Face;
            public Text Under;
            public bool Revealed, Revealing;
            /// <summary>Out of the pack and in the row, face down (it bobs, and lifts while hovered).</summary>
            public bool Dealt;
            public int Index;
            public float Lift;
            /// <summary>Fades the card while another one is being revealed.</summary>
            public CanvasGroup Group;
            /// <summary>Its size while it's being revealed (FocusScale) or in the row (1); the flips scale around it.</summary>
            public float Scale = 1f;
        }

        static PackOpening current;
        public static bool IsOpen { get { return current != null; } }
        /// <summary>The frame it last closed (so the Esc that closed it isn't read again underneath).</summary>
        public static int ClosedFrame { get; private set; }

        RectTransform root, stage, fx, pack;
        /// <summary>The open pack's resonance colour, for the gem on the card backs.</summary>
        Color backGem = Color.white;
        Image flash, dimMore;
        Text hint;
        Button done, again;
        RectTransform skip, skipFill;
        CanvasGroup skipGroup;
        float skipHeld;
        bool mouseOnSkip, mouseOverSkip;
        Slot revealing;              // the card being revealed
        bool skipCard, fastForward;  // skip the current card / every card left (Soul cards always play)
        const float SkipIdleAlpha = 0.2f;
        UiParticles particles;
        readonly List<Slot> slots = new List<Slot>();
        List<CharacterDefinition> roster;
        Action onDone;
        bool torn, tearing, finished;
        int openedFrame;

        public static PackOpening Open(RectTransform canvasRoot, List<CharacterDefinition> roster, Action onDone)
        {
            if (current != null) return current;
            var rt = UIFactory.CreateRect("PackOpening", canvasRoot).Fill();
            rt.SetAsLastSibling();
            var opening = rt.gameObject.AddComponent<PackOpening>();
            current = opening;
            opening.root = rt;
            opening.roster = roster;
            opening.onDone = onDone;
            opening.Build();
            opening.NextPack();
            return opening;
        }

        void Build()
        {
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.03f, 0.028f, 0.025f, 0.94f), true);
            dim.rectTransform.Fill();
            dimMore = UIFactory.CreateImage(root, "DimMore", new Color(0f, 0f, 0f, 0f));
            dimMore.rectTransform.Fill();
            stage = UIFactory.CreateRect("Stage", root).Fill();
            fx = UIFactory.CreateRect("Fx", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            particles = UiParticles.On(fx);
            flash = UIFactory.CreateImage(root, "Flash", new Color(1f, 0.97f, 0.9f, 0f));
            flash.rectTransform.Fill();
            flash.raycastTarget = false;

            var bottom = new Vector2(0.5f, 0f);
            hint = UIFactory.CreateText(root, "Hint", "", 32, InkStyle.Paper, TextAnchor.MiddleCenter);
            hint.rectTransform.Place(bottom, bottom, new Vector2(0f, 300f), new Vector2(1400f, 50f));   // between the cards' names and the buttons
            done = InkStyle.Button(root, "Done", UIText.Get("pack.done"), Finish, false, 38);
            done.GetComponent<RectTransform>().Place(bottom, bottom, new Vector2(0f, 80f), new Vector2(340f, HudView.GoSize.y));

            // Bottom right: Skip while the pack opens, then Open (the next pack) in the same place.
            var bottomRight = new Vector2(1f, 0f);
            var spot = new Vector2(-60f, 80f);
            var size = new Vector2(420f, HudView.GoSize.y);
            again = HudView.GoStyleButton(root, "Again", "", NextPack);
            again.GetComponent<RectTransform>().Place(bottomRight, bottomRight, spot, size);
            again.GetComponentInChildren<Text>().fontSize = UIFactory.Px(UIFactory.TextSize.XLarge);
            BuildSkip(bottomRight, spot, size);
        }

        /// <summary>The Skip button: an ink plate that fills with gold from the left while it's held (mouse or Enter).
        /// Faint (<see cref="SkipIdleAlpha"/>) until the mouse is over it or it's being held, so it doesn't distract.</summary>
        void BuildSkip(Vector2 corner, Vector2 spot, Vector2 size)
        {
            var plate = InkStyle.Panel(root, "Skip", InkStyle.InkPanel, true);   // takes the mouse
            skip = plate.rectTransform;
            skipGroup = plate.gameObject.AddComponent<CanvasGroup>();
            skipGroup.alpha = SkipIdleAlpha;
            skip.Place(corner, corner, spot, size);
            InkStyle.Frame(skip, InkStyle.Rule, 2f);
            var fill = UIFactory.CreateImage(skip, "Fill", UIFactory.WithAlpha(InkStyle.Gold, 0.6f));
            fill.raycastTarget = false;
            skipFill = fill.rectTransform;
            skipFill.anchorMin = Vector2.zero;
            skipFill.anchorMax = new Vector2(0f, 1f);
            skipFill.offsetMin = skipFill.offsetMax = Vector2.zero;
            var label = InkStyle.Title(skip, "Label", UIText.Get("pack.skip"), UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            label.rectTransform.Fill(8f);

            var trigger = skip.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.PointerDown, true);
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.PointerUp, false);
            AddTrigger(trigger, UnityEngine.EventSystems.EventTriggerType.PointerExit, false);
            var click = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
            click.callback.AddListener(_ => SkipCard());
            trigger.triggers.Add(click);
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => mouseOverSkip = true);
            trigger.triggers.Add(enter);
            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => mouseOverSkip = false);
            trigger.triggers.Add(exit);
        }

        void AddTrigger(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, bool held)
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => mouseOnSkip = held);
            trigger.triggers.Add(entry);
        }

        /// <summary>Fills Skip while it (or Enter) is held; empties when let go. Full: <see cref="FastForward"/>.</summary>
        void UpdateSkip()
        {
            bool enterHeld = UIKeys.KeyboardOn && (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space));
            bool held = !finished && skip.gameObject.activeSelf && (mouseOnSkip || enterHeld);
            skipHeld = held ? skipHeld + Time.unscaledDeltaTime : 0f;
            skipFill.anchorMax = new Vector2(Mathf.Clamp01(skipHeld / SkipHoldSeconds), 1f);
            float alpha = mouseOverSkip || skipHeld > 0f ? 1f : SkipIdleAlpha;
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, alpha, 6f * Time.unscaledDeltaTime);
            if (skipHeld >= SkipHoldSeconds) FastForward();
        }

        /// <summary>A tap on Skip: the card being revealed jumps to its end (into the row) and the next one starts.
        /// A Soul card can't be skipped. Before the pack is torn, it tears it.</summary>
        void SkipCard()
        {
            if (finished) return;
            if (!torn)
            {
                if (!tearing) Tear();
                return;
            }
            if (revealing != null && revealing.Pull.Tier == CardPacks.Tier.Soul) return;
            skipCard = true;
        }

        /// <summary>A full hold on Skip: every card left jumps to its end, except a Soul card, which still plays.</summary>
        void FastForward()
        {
            skipHeld = 0f;
            mouseOnSkip = false;
            fastForward = true;
            if (!torn && !tearing) Tear();
            skipCard = revealing == null || revealing.Pull.Tier != CardPacks.Tier.Soul;
        }

        /// <summary>Card <paramref name="i"/>'s place in the row once it's out.</summary>
        Vector2 RowSpot(int i)
        {
            return new Vector2((i - (slots.Count - 1) / 2f) * CardStep, RowY);
        }

        /// <summary>A card's clickable holder on the stage (click: zoom once the pack is done).</summary>
        void CreateCard(Slot slot, int i, Vector2 at)
        {
            slot.Rect = UIFactory.CreateRect("Card" + i, stage).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, CardView.Size * CardScale);
            slot.Scale = 1f;
            var hit = slot.Rect.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            slot.Group = slot.Rect.gameObject.AddComponent<CanvasGroup>();
            var button = slot.Rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var captured = slot;
            button.onClick.AddListener(() => Clicked(captured));
        }

        // PACK OPENING > A NEW PACK

        void NextPack()
        {
            PackKind kind;
            if (!CardPacks.TakePack(out kind)) return;
            UIFactory.ClearChildren(stage);
            slots.Clear();
            torn = tearing = finished = false;
            skipCard = fastForward = false;
            revealing = null;
            openedFrame = Time.frameCount;
            // Roll and claim straight away (and save), so leaving early never loses the cards.
            var pulls = CardPacks.Roll(roster, new System.Random(Environment.TickCount), kind);
            backGem = ResonanceInfo.Color(kind != null ? kind.pool : Resonance.Physical);
            foreach (var pull in pulls) CardPacks.Claim(pull);
            if (!CardCollection.InSession) SaveGame.Save();   // Card Pack mode keeps nothing
            foreach (var pull in pulls) slots.Add(new Slot { Pull = pull });

            pack = UIFactory.CreateRect("Pack", stage).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 900f), new Vector2(300f, 440f));
            BuildPack(pack, kind);
            var hit = pack.gameObject.AddComponent<Button>();
            hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(Tear);
            hint.text = UIText.Get("pack.hint.open");
            done.gameObject.SetActive(false);
            again.gameObject.SetActive(false);
            skip.gameObject.SetActive(true);
            skipHeld = 0f;
            mouseOnSkip = mouseOverSkip = false;
            skipGroup.alpha = SkipIdleAlpha;
            StartCoroutine(DropIn());
        }

        /// <summary>The pack: its kind's picture (Resources/Cards/Packs/Pack_&lt;pack id&gt;.png, cut from the card sheet's
        /// Art cell), else this area's (Pack_&lt;area id&gt;.png), else one of the Pack_any_* colours at random, else a plain
        /// ink pack.</summary>
        static void BuildPack(RectTransform pack, PackKind kind)
        {
            var own = kind != null ? CardArt.Picture("Cards/Packs/Pack_" + kind.id) : null;
            if (own != null)
            {
                var ownImg = UIFactory.CreateImage(pack, "Art", Color.white);
                ownImg.sprite = own;
                ownImg.preserveAspect = true;
                ownImg.rectTransform.Fill();
                ownImg.raycastTarget = true;
                return;
            }
            // Card Pack mode: a random area's pack each time.
            string area = WorldSession.CurrentArea;
            if (CardCollection.InSession)
            {
                var areas = WorldAreas.All;
                if (areas.Count > 0) area = areas[UnityEngine.Random.Range(0, areas.Count)].Id;
            }
            var art = CardArt.Picture("Cards/Packs/Pack_" + area);
            if (art == null) art = CardArt.Picture("Cards/Packs/Pack_any_" + "ABCDEFGHI"[UnityEngine.Random.Range(0, 9)]);
            if (art != null)
            {
                var img = UIFactory.CreateImage(pack, "Art", Color.white);
                img.sprite = art;
                img.preserveAspect = true;
                img.rectTransform.Fill();
                img.raycastTarget = true;
                return;
            }
            var plate = UIFactory.CreateImage(pack, "Plate", new Color(0.16f, 0.12f, 0.2f, 1f), true);
            plate.rectTransform.Fill();
            InkStyle.Frame(plate.rectTransform, Gold, 4f);
            var name = InkStyle.Title(pack, "Name", UIText.Get("pack.name"), 44, Gold, TextAnchor.MiddleCenter);
            name.rectTransform.Fill(20f);
        }

        IEnumerator DropIn()
        {
            yield return Anim(0.45f, p => { if (pack != null) pack.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(900f, 40f, Tween.OutBack(p))); });
        }

        // PACK OPENING > TEARING IT OPEN

        void Tear()
        {
            if (torn || tearing || pack == null) return;
            StartCoroutine(TearRoutine());
        }

        IEnumerator TearRoutine()
        {
            tearing = true;
            hint.text = "";
            // A quick shake, then the top strip flies off and the rest drops away in a flash.
            var home = pack.anchoredPosition;
            yield return Anim(0.25f, p => pack.anchoredPosition = home + new Vector2(Mathf.Sin(p * 60f) * 8f * (1f - p), 0f));
            var top = UIFactory.CreateRect("Strip", stage).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), home + new Vector2(0f, 150f), new Vector2(300f, 70f));
            var strip = UIFactory.CreateImage(top, "Edge", new Color(0.95f, 0.9f, 0.8f, 1f));
            strip.rectTransform.Fill();
            FlashTo(0.8f);
            Burst(home + new Vector2(0f, 180f), 30, 900f, 0.8f, 18f, t => Color.Lerp(Color.white, Gold, t));
            yield return Anim(0.35f, p =>
            {
                top.anchoredPosition = home + new Vector2(260f * p, 150f + 300f * p);
                top.localRotation = Quaternion.Euler(0f, 0f, -40f * p);
                pack.anchoredPosition = home + new Vector2(0f, -900f * p * p);
            });
            Destroy(top.gameObject);
            Destroy(pack.gameObject);
            torn = true;
            tearing = false;
            yield return RevealOneByOne();
        }

        /// <summary>The deal, then the cards one at a time: every card bursts out of the torn pack face down and fans into
        /// the row; then each lifts up into the middle (big), a beat (longer and trembling for the last), a shine sweeps
        /// its back, the flip and its rarity, a moment to look, then down into its place.</summary>
        IEnumerator RevealOneByOne()
        {
            hint.text = "";   // the Skip button speaks for itself
            var mouth = new Vector2(0f, 40f);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                CreateCard(slot, i, mouth);
                slot.Index = i;
                slot.Back = UIFactory.CreateRect("Back", slot.Rect).Fill();
                CardView.BuildBack(slot.Back, backGem);
                slot.Rect.localScale = Vector3.one * 0.3f;
                StartCoroutine(DealIn(slot, mouth));
                yield return Beat(DealGap);
            }
            yield return Beat(DealSeconds);

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                revealing = slot;
                slot.Revealing = true;   // it stops bobbing
                slot.Rect.SetAsLastSibling();   // drawn over the cards still in the row
                skipCard = fastForward && slot.Pull.Tier != CardPacks.Tier.Soul;
                bool last = i == slots.Count - 1;

                // Up in its own column (the row's spacing leaves room for it at its big size), so it never covers the others.
                var focus = new Vector2(RowSpot(i).x, FocusY);
                var start = slot.Rect.anchoredPosition;
                var startRot = slot.Rect.localRotation;
                yield return Anim(RiseSeconds, p =>
                {
                    float e = Tween.OutBack(p);
                    slot.Rect.anchoredPosition = Vector2.LerpUnclamped(start, focus, e);
                    slot.Rect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, FocusScale, e);
                    slot.Rect.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, p);
                });
                slot.Rect.localRotation = Quaternion.identity;
                slot.Scale = FocusScale;
                slot.Rect.localScale = Vector3.one * FocusScale;
                yield return Beat(last ? LastBeatSeconds : BeatSeconds);
                if (last && slot.Pull.Tier != CardPacks.Tier.Soul) yield return Suspense(slot);   // Soul has its own
                yield return Reveal(slot);
                yield return Beat(ShowSeconds);

                // Down into its place in the row.
                var spot = RowSpot(i);
                var from = slot.Rect.anchoredPosition;
                yield return Anim(SettleSeconds, p =>
                {
                    float e = Smooth(p);
                    slot.Rect.anchoredPosition = Vector2.Lerp(from, spot, e);
                    slot.Rect.localScale = Vector3.one * Mathf.Lerp(FocusScale, 1f, e);
                });
                slot.Scale = 1f;
                slot.Rect.localScale = Vector3.one;
                Label(slot, true);
            }
            revealing = null;
            skipCard = false;
            Finished();
        }

        /// <summary>One card dealt: out of the pack's mouth in an arc to its place in the row, spinning upright and
        /// growing, a bounce and a few sparks as it lands.</summary>
        IEnumerator DealIn(Slot slot, Vector2 mouth)
        {
            var to = RowSpot(slot.Index);
            float side = slot.Index - (slots.Count - 1) / 2f;
            float spin = side * 32f + (slot.Index % 2 == 0 ? 160f : -160f);
            yield return Anim(DealSeconds, p =>
            {
                float e = Tween.OutBack(p);
                slot.Rect.anchoredPosition = Vector2.LerpUnclamped(mouth, to, e) + new Vector2(0f, 150f * Mathf.Sin(p * Mathf.PI));
                slot.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(spin, 0f, Smooth(p)));
                slot.Rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, e);
            });
            slot.Rect.anchoredPosition = to;
            slot.Rect.localRotation = Quaternion.identity;
            slot.Rect.localScale = Vector3.one;
            Burst(to + new Vector2(0f, -CardView.Size.y * CardScale * 0.45f), 10, 420f, 0.45f, 12f, t => Color.Lerp(Color.white, backGem, t));
            slot.Dealt = true;
        }

        /// <summary>Face-down cards in the row bob gently and lift while the mouse is over them.</summary>
        void BobDealtCards()
        {
            float now = Time.unscaledTime;
            foreach (var s in slots)
            {
                // The card being revealed has the stage: the rest fade back behind it.
                if (s.Group != null)
                    s.Group.alpha = Mathf.MoveTowards(s.Group.alpha, revealing != null && s != revealing ? 0.55f : 1f, 4f * Time.unscaledDeltaTime);
                if (!s.Dealt || s.Revealed || s.Revealing || s.Rect == null) continue;
                bool over = RectTransformUtility.RectangleContainsScreenPoint(s.Rect, Input.mousePosition, null);
                s.Lift = Mathf.MoveTowards(s.Lift, over ? 1f : 0f, 6f * Time.unscaledDeltaTime);
                s.Rect.anchoredPosition = RowSpot(s.Index) + new Vector2(0f, Mathf.Sin(now * 2.6f + s.Index * 1.3f) * 5f + 26f * s.Lift);
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * 1.9f + s.Index * 1.7f) * (2f + 3f * s.Lift));
                s.Rect.localScale = Vector3.one * (1f + 0.06f * s.Lift);
            }
        }

        /// <summary>A bright band sweeping across a card's back (masked to the card), just before it flips.</summary>
        IEnumerator SweepBack(Slot slot)
        {
            if (slot.Back == null) yield break;
            var maskRt = UIFactory.CreateRect("Sweep", slot.Back).Fill();
            var mask = maskRt.gameObject.AddComponent<Image>();
            mask.sprite = CardArt.Picture("Cards/CardBack");
            mask.raycastTarget = false;
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var size = slot.Back.rect.size;
            var band = UIFactory.CreateImage(maskRt, "Band", new Color(1f, 1f, 1f, 0.7f));
            band.raycastTarget = false;
            band.rectTransform.sizeDelta = new Vector2(size.x * 0.22f, size.y * 1.6f);
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            yield return Anim(SweepSeconds, p =>
            {
                if (band == null) return;
                band.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-size.x, size.x, p), 0f);
                band.color = new Color(1f, 1f, 1f, 0.75f * Mathf.Sin(p * Mathf.PI));
            });
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        /// <summary>Waits <paramref name="seconds"/> (real time); over at once while the card is being skipped.</summary>
        IEnumerator Beat(float seconds)
        {
            for (float t = 0f; t < seconds && !skipCard; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>A real-time animation (like CardZoom.Unscaled: <paramref name="step"/> gets 0..1) that jumps to its
        /// end while the current card is being skipped.</summary>
        IEnumerator Anim(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds && !skipCard; t += Time.unscaledDeltaTime)
            {
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        /// <summary>Sparks, except while the card is being skipped.</summary>
        void Burst(Vector2 at, int count, float speed, float life, float size, Func<float, Color> color, float upward = 0f)
        {
            if (!skipCard) particles.Burst(at, count, speed, life, size, color, upward);
        }

        /// <summary>The last card before it flips: it trembles harder and harder inside a growing glow (warm if it's a
        /// rare). Not skippable: it's the moment.</summary>
        IEnumerator Suspense(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            var tint = slot.Pull.Tier == CardPacks.Tier.Rare ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.95f, 0.8f);
            var glow = UiParticles.Glow(fx, at, 160f, new Color(tint.r, tint.g, tint.b, 0f));
            yield return Anim(SuspenseSeconds, p =>
            {
                glow.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(160f, 520f, p);
                glow.color = new Color(tint.r, tint.g, tint.b, 0.7f * p);
                float shake = 7f * p * p;
                slot.Rect.anchoredPosition = at + new Vector2(Mathf.Sin(p * 95f) * shake, Mathf.Cos(p * 77f) * shake * 0.6f);
            });
            slot.Rect.anchoredPosition = at;
            StartCoroutine(FadeOut(glow, 0.5f));
        }

        IEnumerator MoveTo(RectTransform rt, Vector2 to, float seconds)
        {
            var from = rt.anchoredPosition;
            yield return Anim(seconds, p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Tween.OutBack(p)); });
        }

        // PACK OPENING > REVEALING

        /// <summary>Once every card is out, a click zooms the card (before that clicks do nothing: Skip skips).</summary>
        void Clicked(Slot slot)
        {
            if (finished) Zoom(slot);
        }

        IEnumerator Reveal(Slot slot)
        {
            slot.Revealing = true;
            var tier = slot.Pull.Tier;
            if (tier == CardPacks.Tier.Soul) yield return SoulBuildUp(slot);
            if (tier == CardPacks.Tier.Blank) yield return BlankBuildUp(slot);

            yield return SweepBack(slot);
            // The flip: it tips as it turns edge-on, swaps to the face, and rights itself face-on (slower for rarer cards).
            float half = tier == CardPacks.Tier.Soul ? 0.22f : tier == CardPacks.Tier.Rare || tier == CardPacks.Tier.Blank ? 0.16f : 0.11f;
            float k = slot.Scale;
            yield return Anim(half, p =>
            {
                slot.Rect.localScale = new Vector3(1f - p, 1f + 0.06f * p, 1f) * k;
                slot.Rect.localRotation = Quaternion.Euler(0f, 0f, 9f * p);
            });
            Destroy(slot.Back.gameObject);
            slot.Face = UIFactory.CreateRect("Face", slot.Rect).Fill();
            BuildFace(slot.Face, slot.Pull, CardScale);
            foreach (var g in slot.Face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            yield return Anim(half, p =>
            {
                slot.Rect.localScale = new Vector3(p, 1.06f - 0.06f * p, 1f) * k;
                slot.Rect.localRotation = Quaternion.Euler(0f, 0f, 9f * (1f - Tween.OutBack(p)));
            });
            slot.Rect.localScale = Vector3.one * k;
            slot.Rect.localRotation = Quaternion.identity;
            slot.Revealed = true;
            slot.Revealing = false;
            yield return Celebrate(slot);
        }

        void BuildFace(RectTransform face, CardPacks.Pull pull, float scale)
        {
            switch (pull.Tier)
            {
                case CardPacks.Tier.Rare:
                    ArcanaCards.BuildFace(face, pull.Binding, scale);
                    break;
                case CardPacks.Tier.Soul:
                    CardView.BuildFace(face, Instance(pull.Member, WorldSession.UltimateOf(pull.Member) ?? pull.Member.ritual, 1), scale);
                    break;
                case CardPacks.Tier.Blank:
                    ArcanaCards.BuildBlankFace(face, scale);
                    CardHolo.Add(face, null, CardHolo.Kind.Blank);   // shines on the table too
                    break;
                default:
                    var owner = OwnerOf(pull.Card);
                    CardView.BuildFace(face, Instance(owner, pull.Card, 1), scale);
                    break;
            }
        }

        static readonly Color Pearl = new Color(0.9f, 0.92f, 0.96f), Silver = new Color(0.72f, 0.82f, 0.95f);

        /// <summary>BLANK: before it flips, the card lifts a little inside a cool silver glow.</summary>
        IEnumerator BlankBuildUp(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            var glow = UiParticles.Glow(fx, at, 180f, new Color(Silver.r, Silver.g, Silver.b, 0f));
            yield return Anim(0.7f, p =>
            {
                glow.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(180f, 560f, p);
                glow.color = new Color(Silver.r, Silver.g, Silver.b, 0.75f * p);
                slot.Rect.anchoredPosition = at + new Vector2(0f, 18f * Mathf.Sin(p * Mathf.PI * 0.5f));
            });
            slot.Rect.anchoredPosition = at;
            StartCoroutine(FadeOut(glow, 0.8f));
        }

        /// <summary>BLANK: a silver ring, white and blue sparks, and a shine sweeping across the card.</summary>
        IEnumerator BlankShine(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            var ring = UiParticles.Glow(fx, at, 80f, Color.white);
            StartCoroutine(Anim(0.5f, p =>
            {
                if (ring == null) return;
                ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(80f, 900f, p);
                ring.color = new Color(Silver.r, Silver.g, Silver.b, 0.7f * (1f - p));
                if (p >= 1f) Destroy(ring.gameObject);
            }));
            Burst(at, 30, 900f, 1.0f, 18f, t => Color.Lerp(Color.white, Silver, t), 250f);
            CardSparkles.Add(slot.Face, Silver);
            // The shine: a slanted white band crossing the card, clipped to it.
            var clip = UIFactory.CreateRect("Shine", slot.Face).Fill();
            clip.gameObject.AddComponent<RectMask2D>();
            var band = UIFactory.CreateImage(clip, "Band", new Color(1f, 1f, 1f, 0.55f));
            band.raycastTarget = false;
            float w = slot.Face.rect.width;
            band.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-w, 0f), new Vector2(60f, slot.Face.rect.height * 1.6f));
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            yield return Anim(0.55f, p => { if (band != null) band.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-w, w, p), 0f); });
            if (clip != null) Destroy(clip.gameObject);
            yield return Punch(slot.Rect, 0.08f, 0.2f);
        }

        CharacterDefinition OwnerOf(CardDefinition card)
        {
            foreach (var member in roster)
                if (CardCollection.AbilitiesOf(member).Contains(card)) return member;
            return roster.Count > 0 ? roster[0] : null;
        }

        static CardInstance Instance(CharacterDefinition member, CardDefinition card, int rank)
        {
            var unit = new BattleUnit(member, Team.Player);
            // The protagonist's card pool cards (Physical) show in their own pool's colour, not the base element's.
            Resonance element = member.resonances.Count > 0 ? card.pool : member.resonance;
            foreach (var kit in member.resonances)
                if (kit != null && (kit.cards.Contains(card) || kit.ultimate == card)) element = kit.element;
            var loadout = new Loadout { Element = element };
            loadout.Upgrades[card] = CardCollection.Upgrades(card);   // numbers at its upgraded level
            unit.ApplyLoadout(loadout);
            return new CardInstance(card, unit, rank);
        }

        /// <summary>SOUL: the screen dims, the card trembles in a growing rainbow glow, rays spin up.</summary>
        IEnumerator SoulBuildUp(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            var glow = UiParticles.Glow(fx, at, 200f, UiParticles.Rainbow(0f, 0f));
            var rays = UiParticles.Rays(fx, at, 12, 520f, 46f);
            var rayImages = rays.GetComponentsInChildren<Image>();
            yield return Anim(1.1f, p =>
            {
                dimMore.color = new Color(0f, 0f, 0f, 0.55f * p);
                glow.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(200f, 760f, p);
                glow.color = UiParticles.Rainbow(0f, 0.85f * p);
                rays.localRotation = Quaternion.Euler(0f, 0f, -200f * p * p);
                for (int i = 0; i < rayImages.Length; i++) rayImages[i].color = UiParticles.Rainbow(i / (float)rayImages.Length, 0.45f * p);
                slot.Rect.anchoredPosition = at + new Vector2(Mathf.Sin(p * 90f) * 10f * p, Mathf.Cos(p * 70f) * 6f * p);
                slot.Rect.localScale = Vector3.one * slot.Scale * (1f + 0.12f * p);
            });
            slot.Rect.anchoredPosition = at;
            StartCoroutine(FadeOut(glow, 1.2f));
            StartCoroutine(SpinOut(rays, rayImages, 1.4f));
        }

        IEnumerator Celebrate(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            switch (slot.Pull.Tier)
            {
                case CardPacks.Tier.Common:
                    Burst(at, 16, 650f, 0.7f, 16f, t => Color.Lerp(Color.white, Gold, t));
                    break;
                case CardPacks.Tier.Blank:
                    yield return BlankShine(slot);
                    break;
                case CardPacks.Tier.Rare:
                {
                    var glow = UiParticles.Glow(fx, at, 520f, new Color(1f, 0.55f, 0.15f, 0.8f));
                    glow.transform.SetAsFirstSibling();
                    Burst(at, 34, 950f, 1.0f, 20f, t => Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f), t), 300f);
                    StartCoroutine(PulseOut(glow, 1.4f));
                    yield return Punch(slot.Rect, 0.12f, 0.25f);
                    break;
                }
                case CardPacks.Tier.Soul:
                {
                    // A ring shockwave, a rainbow fountain, a white flash, sparkles on the card and the Soul title.
                    var ring = UiParticles.Glow(fx, at, 80f, Color.white);
                    StartCoroutine(Anim(0.6f, p =>
                    {
                        if (ring == null) return;
                        ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(80f, 1500f, p);
                        ring.color = UiParticles.Rainbow(0.3f, 0.7f * (1f - p));
                        if (p >= 1f) Destroy(ring.gameObject);
                    }));
                    FlashTo(1f);
                    Burst(at, 80, 1500f, 1.6f, 24f, t => UiParticles.Rainbow(t * 0.8f), 700f);
                    CardSparkles.Add(slot.Face, Color.white);
                    StartCoroutine(Anim(0.6f, p => dimMore.color = new Color(0f, 0f, 0f, 0.55f * (1f - p))));
                    var title = UIFactory.CreateRect("SoulTitle", root).Fill();
                    yield return UltTitle.Play(title, CardName(slot.Pull), UiParticles.Rainbow(0f), true);   // real time: the title pauses the world
                    if (title != null) Destroy(title.gameObject);
                    break;
                }
            }
        }

        /// <summary>The pulled card's name: the ability, Sun / Moon / Star, or the Soul card (its Ult, "Aegis Down").</summary>
        static string CardName(CardPacks.Pull pull)
        {
            switch (pull.Tier)
            {
                case CardPacks.Tier.Rare: return ArcanaCards.Name(pull.Binding);
                case CardPacks.Tier.Blank: return UIText.Get("pack.blank_card");
                case CardPacks.Tier.Soul:
                    var soul = WorldSession.UltimateOf(pull.Member) ?? pull.Member.ritual;
                    return soul != null ? soul.DisplayName : pull.Member.DisplayName;
                default: return pull.Card.DisplayName;
            }
        }

        /// <summary>The line under a revealed card: its name (in runes until the language chip is found), and for an
        /// ability card its level as stars under that, or "Blank card!" when it was already at the top. A card that just
        /// levelled up pops its new star in with a burst (<paramref name="celebrate"/>; not after Skip).</summary>
        void Label(Slot slot, bool celebrate)
        {
            var pull = slot.Pull;
            var color = pull.Tier == CardPacks.Tier.Common ? InkStyle.Paper : pull.Tier == CardPacks.Tier.Blank ? Silver : Gold;
            bool read = CardCollection.Translated || pull.Tier == CardPacks.Tier.Rare || pull.Tier == CardPacks.Tier.Blank;   // Sun / Moon / Star and blanks always read
            slot.Under = UIFactory.CreateText(slot.Rect, "Under", read ? CardName(pull) : "", 28, color, TextAnchor.UpperCenter, FontStyle.Bold);
            slot.Under.supportRichText = true;
            slot.Under.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(CardStep - 20f, 40f));
            slot.Under.FitOneLine(CardStep - 20f, UIFactory.TextSize.Min);
            if (!read) RuneText.Build(slot.Under.rectTransform, CardName(pull), color, CardStep - 20f, 30f, 20f);
            if (pull.Tier != CardPacks.Tier.Common) return;

            var bottom = new Vector2(0.5f, 0f);
            if (pull.Blank)
            {
                var blank = UIFactory.CreateText(slot.Rect, "Blank", UIText.Get("pack.blank"), UIFactory.TextSize.Small, Gold, TextAnchor.UpperCenter, FontStyle.Bold);
                blank.rectTransform.Place(bottom, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(CardStep - 20f, 34f));
                return;
            }
            int stars = CardCollection.UpgradeLevel(pull.Card);
            const float star = 26f;
            float width = stars * star * 1.08f;
            var row = CardStars.Build(slot.Rect, stars, star, bottom, new Vector2(-width / 2f, -58f));
            if (pull.RankedUp && celebrate && row.childCount > 0) StartCoroutine(PopStar(slot, (RectTransform)row.GetChild(row.childCount - 1)));
        }

        /// <summary>A level up: the new star grows in with an overshoot, sparks burst from it and "Level up!" shows.</summary>
        IEnumerator PopStar(Slot slot, RectTransform star)
        {
            star.localScale = Vector3.zero;
            yield return Beat(0.15f);
            if (star == null) yield break;
            Vector2 at = fx.InverseTransformPoint(star.position);
            Burst(at, 22, 700f, 0.8f, 16f, t => Color.Lerp(Color.white, Gold, t), 200f);
            var text = UIFactory.CreateText(slot.Rect, "LevelUp", UIText.Get("pack.level_up"), UIFactory.TextSize.Small, Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            text.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(CardStep - 20f, 34f));
            yield return Anim(0.35f, p => { if (star != null) star.localScale = Vector3.one * Tween.OutBack(p); });
            if (star != null) star.localScale = Vector3.one;
        }

        void Finished()
        {
            finished = true;
            hint.text = UIText.Get("pack.hint.zoom");
            skip.gameObject.SetActive(false);   // Open takes its place
            done.gameObject.SetActive(true);
            again.gameObject.SetActive(CardCollection.InSession || WorldSession.Packs > 0);
            again.GetComponentInChildren<Text>().text = CardCollection.InSession ? UIText.Get("pack.again.free")
                : UIText.Get("pack.again", "n", WorldSession.Packs.ToString());
        }

        void Zoom(Slot slot)
        {
            var pull = slot.Pull;
            var holo = pull.Tier == CardPacks.Tier.Soul ? CardHolo.Kind.Soul : pull.Tier == CardPacks.Tier.Rare ? CardHolo.Kind.Rare
                : pull.Tier == CardPacks.Tier.Blank ? CardHolo.Kind.Blank : CardHolo.Kind.Common;
            CardZoom.Open(root, (face, s) => BuildFace(face, pull, s), holo, null, null,
                pull.Tier == CardPacks.Tier.Common && pull.Card != null ? pull.Card.kind : (CardKind?)null);
        }

        void Finish()
        {
            if (!CardCollection.InSession) SaveGame.Save();
            if (current == this) current = null;
            ClosedFrame = Time.frameCount;
            var then = onDone;
            Destroy(gameObject);
            if (then != null) then();
        }

        // PACK OPENING > LITTLE EFFECTS

        void FlashTo(float alpha)
        {
            if (skipCard) return;
            StartCoroutine(Anim(0.5f, p => flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, alpha * (1f - p))));
        }

        IEnumerator Punch(RectTransform rt, float amount, float seconds)
        {
            var start = rt.localScale;
            yield return Anim(seconds, p => { if (rt != null) rt.localScale = start * (1f + amount * Mathf.Sin(p * Mathf.PI)); });
        }

        IEnumerator FadeOut(Image img, float seconds)
        {
            var start = img.color;
            yield return Anim(seconds, p => { if (img != null) img.color = new Color(start.r, start.g, start.b, start.a * (1f - p)); });
            if (img != null) Destroy(img.gameObject);
        }

        IEnumerator PulseOut(Image img, float seconds)
        {
            yield return Anim(seconds, p =>
            {
                if (img == null) return;
                var c = img.color;
                c.a = 0.8f * (1f - p) * (0.75f + 0.25f * Mathf.Sin(p * 18f));
                img.color = c;
            });
            if (img != null) Destroy(img.gameObject);
        }

        IEnumerator SpinOut(RectTransform rays, Image[] images, float seconds)
        {
            float start = rays.localEulerAngles.z;
            yield return Anim(seconds, p =>
            {
                if (rays == null) return;
                rays.localRotation = Quaternion.Euler(0f, 0f, start - 260f * p);
                for (int i = 0; i < images.Length; i++) images[i].color = UiParticles.Rainbow(i / (float)images.Length, 0.45f * (1f - p));
            });
            if (rays != null) Destroy(rays.gameObject);
        }

        void Update()
        {
            BobDealtCards();
            if (CardZoom.IsOpen || CardZoom.ClosedFrame == Time.frameCount || Time.frameCount == openedFrame) return;
            if (pack != null && !torn && !tearing)
            {
                // The waiting pack sways gently.
                pack.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2f) * 3f);
            }
            // Enter tears the pack and, once the cards are out, closes; held, it fills Skip.
            bool enter = UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space);
            if (enter)
            {
                if (!torn) Tear();
                else if (finished && done.gameObject.activeSelf) Finish();
            }
            UpdateSkip();
            if (UIKeys.Back && !tearing) Finish();   // the cards are already in the collection
        }
    }
}
