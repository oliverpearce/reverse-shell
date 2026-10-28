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
    ///      only one that can be rare. Click / Enter hurries it along.
    ///   3. each flip plays its rarity:
    ///        Common  a soft white-gold sparkle burst
    ///        Rare    an orange glow behind the card, orange sparks and a warm pulse (Sun, Moon, Star)
    ///        SOUL    the screen dims, the card shakes inside a growing rainbow glow with spinning rays, then a ring
    ///                shockwave, a rainbow fountain, a flash and the unit's Soul title
    ///   4. under each card: NEW / rank up / Soul level; click a revealed card to zoom in and tilt it
    ///   5. Done (or Open Another while packs are left)
    /// The cards are rolled and added to the collection (and saved) the moment the pack opens, so closing early loses
    /// nothing. The pack art is the user's pick from the argametina packs (Resources/Cards/Pack.png); until it's there a
    /// plain ink pack stands in. Unscaled time throughout.
    /// </summary>
    public class PackOpening : MonoBehaviour
    {
        const float CardScale = 0.85f, CardStep = 310f;
        const float FocusScale = 1.4f, FocusY = 60f, RowY = 40f;   // the card being revealed: big, in the middle
        const float RiseSeconds = 0.3f, BeatSeconds = 0.15f, LastBeatSeconds = 0.35f, SuspenseSeconds = 0.9f;
        const float ShowSeconds = 0.9f, SettleSeconds = 0.3f;
        static readonly Color Gold = InkStyle.GoldBright;

        class Slot
        {
            public CardPacks.Pull Pull;
            public RectTransform Rect, Back, Face;
            public Text Under;
            public bool Revealed, Revealing;
            /// <summary>Its size while it's being revealed (FocusScale) or in the row (1); the flips scale around it.</summary>
            public float Scale = 1f;
        }

        static PackOpening current;
        public static bool IsOpen { get { return current != null; } }
        /// <summary>The frame it last closed (so the Esc that closed it isn't read again underneath).</summary>
        public static int ClosedFrame { get; private set; }

        RectTransform root, stage, fx, pack;
        Image flash, dimMore;
        Text hint;
        Button done, again;
        UiParticles particles;
        readonly List<Slot> slots = new List<Slot>();
        List<CharacterDefinition> roster;
        Action onDone;
        bool torn, tearing, finished, hurry;
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
            hint.rectTransform.Place(bottom, bottom, new Vector2(0f, 190f), new Vector2(1400f, 50f));
            done = HudView.GoStyleButton(root, "Done", UIText.Get("pack.done"), Finish);
            done.GetComponent<RectTransform>().Place(bottom, bottom, new Vector2(-200f, 80f), new Vector2(340f, HudView.GoSize.y));
            again = InkStyle.Button(root, "Again", "", NextPack, false, 38);
            again.GetComponent<RectTransform>().Place(bottom, bottom, new Vector2(200f, 80f), new Vector2(340f, HudView.GoSize.y));
        }

        // PACK OPENING > A NEW PACK

        void NextPack()
        {
            if (!CardPacks.TakePack()) return;
            UIFactory.ClearChildren(stage);
            slots.Clear();
            torn = tearing = finished = hurry = false;
            openedFrame = Time.frameCount;
            // Roll and claim straight away (and save), so leaving early never loses the cards.
            var pulls = CardPacks.Roll(roster, new System.Random(Environment.TickCount));
            foreach (var pull in pulls) CardPacks.Claim(pull);
            if (!CardCollection.InSession) SaveGame.Save();   // Card Pack mode keeps nothing
            foreach (var pull in pulls) slots.Add(new Slot { Pull = pull });

            pack = UIFactory.CreateRect("Pack", stage).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 900f), new Vector2(300f, 440f));
            BuildPack(pack);
            var hit = pack.gameObject.AddComponent<Button>();
            hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(Tear);
            hint.text = UIText.Get("pack.hint.open");
            done.gameObject.SetActive(false);
            again.gameObject.SetActive(false);
            StartCoroutine(DropIn());
        }

        /// <summary>The pack: this area's (Resources/Cards/Packs/Pack_&lt;area id&gt;.png, from the argametina packs), else one
        /// of the Pack_any_* colours at random, else a plain ink pack.</summary>
        static void BuildPack(RectTransform pack)
        {
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
            yield return CardZoom.Unscaled(0.45f, p => { if (pack != null) pack.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(900f, 40f, Tween.OutBack(p))); });
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
            yield return CardZoom.Unscaled(0.25f, p => pack.anchoredPosition = home + new Vector2(Mathf.Sin(p * 60f) * 8f * (1f - p), 0f));
            var top = UIFactory.CreateRect("Strip", stage).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), home + new Vector2(0f, 150f), new Vector2(300f, 70f));
            var strip = UIFactory.CreateImage(top, "Edge", new Color(0.95f, 0.9f, 0.8f, 1f));
            strip.rectTransform.Fill();
            FlashTo(0.8f);
            particles.Burst(home + new Vector2(0f, 180f), 30, 900f, 0.8f, 18f, t => Color.Lerp(Color.white, Gold, t));
            yield return CardZoom.Unscaled(0.35f, p =>
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

        /// <summary>The cards one at a time: up into the middle (big, face down), a beat (longer and trembling for the
        /// last), the flip and its rarity, a moment to look, then down into the row.</summary>
        IEnumerator RevealOneByOne()
        {
            hint.text = UIText.Get("pack.hint.reveal");
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool last = i == slots.Count - 1;
                slot.Rect = UIFactory.CreateRect("Card" + i, stage).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), CardView.Size * CardScale);
                var hit = slot.Rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                slot.Back = UIFactory.CreateRect("Back", slot.Rect).Fill();
                BuildBack(slot.Back);
                var button = slot.Rect.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var captured = slot;
                button.onClick.AddListener(() => Clicked(captured));

                var focus = new Vector2(0f, FocusY);
                yield return CardZoom.Unscaled(RiseSeconds, p =>
                {
                    float e = Tween.OutBack(p);
                    slot.Rect.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f, -260f), focus, e);
                    slot.Rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, FocusScale, e);
                });
                slot.Scale = FocusScale;
                slot.Rect.localScale = Vector3.one * FocusScale;
                yield return Beat(last ? LastBeatSeconds : BeatSeconds);
                if (last && slot.Pull.Tier != CardPacks.Tier.Soul) yield return Suspense(slot);   // Soul has its own
                yield return Reveal(slot);
                yield return Beat(ShowSeconds);

                // Down into its place in the row.
                var spot = new Vector2((i - (slots.Count - 1) / 2f) * CardStep, RowY);
                var from = slot.Rect.anchoredPosition;
                yield return CardZoom.Unscaled(SettleSeconds, p =>
                {
                    float e = Smooth(p);
                    slot.Rect.anchoredPosition = Vector2.Lerp(from, spot, e);
                    slot.Rect.localScale = Vector3.one * Mathf.Lerp(FocusScale, 1f, e);
                });
                slot.Scale = 1f;
                slot.Rect.localScale = Vector3.one;
                Label(slot);
            }
            Finished();
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        /// <summary>Waits <paramref name="seconds"/>, or less if the player clicks / presses Enter.</summary>
        IEnumerator Beat(float seconds)
        {
            hurry = false;
            for (float t = 0f; t < seconds && !hurry; t += Time.unscaledDeltaTime) yield return null;
            hurry = false;
        }

        /// <summary>The last card before it flips: it trembles harder and harder inside a growing glow (warm if it's a
        /// rare). Not skippable: it's the moment.</summary>
        IEnumerator Suspense(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            var tint = slot.Pull.Tier == CardPacks.Tier.Rare ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.95f, 0.8f);
            var glow = UiParticles.Glow(fx, at, 160f, new Color(tint.r, tint.g, tint.b, 0f));
            yield return CardZoom.Unscaled(SuspenseSeconds, p =>
            {
                glow.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(160f, 520f, p);
                glow.color = new Color(tint.r, tint.g, tint.b, 0.7f * p);
                float shake = 7f * p * p;
                slot.Rect.anchoredPosition = at + new Vector2(Mathf.Sin(p * 95f) * shake, Mathf.Cos(p * 77f) * shake * 0.6f);
            });
            slot.Rect.anchoredPosition = at;
            StartCoroutine(FadeOut(glow, 0.5f));
        }

        static IEnumerator MoveTo(RectTransform rt, Vector2 to, float seconds)
        {
            var from = rt.anchoredPosition;
            yield return CardZoom.Unscaled(seconds, p => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Tween.OutBack(p)); });
        }

        /// <summary>A card's back: dark, a gold frame and the card-back mark.</summary>
        static void BuildBack(RectTransform back)
        {
            var plate = UIFactory.CreateImage(back, "Plate", new Color(0.1f, 0.09f, 0.12f, 1f));
            plate.rectTransform.Fill();
            plate.raycastTarget = false;
            InkStyle.Frame(plate.rectTransform, Gold, 3f);
            var mark = UIFactory.CreateImage(back, "Mark", Color.white);
            mark.sprite = CardIcons.CardBack;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            mark.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
        }

        // PACK OPENING > REVEALING

        /// <summary>While the cards come out a click hurries the next one along; afterwards it zooms the card.</summary>
        void Clicked(Slot slot)
        {
            if (!finished) hurry = true;
            else Zoom(slot);
        }

        IEnumerator Reveal(Slot slot)
        {
            slot.Revealing = true;
            var tier = slot.Pull.Tier;
            if (tier == CardPacks.Tier.Soul) yield return SoulBuildUp(slot);

            // The flip: edge-on, swap to the face, face-on again (slower for rarer cards).
            float half = tier == CardPacks.Tier.Soul ? 0.22f : tier == CardPacks.Tier.Rare ? 0.16f : 0.11f;
            float k = slot.Scale;
            yield return CardZoom.Unscaled(half, p => slot.Rect.localScale = new Vector3(1f - p, 1f + 0.06f * p, 1f) * k);
            Destroy(slot.Back.gameObject);
            slot.Face = UIFactory.CreateRect("Face", slot.Rect).Fill();
            BuildFace(slot.Face, slot.Pull, CardScale);
            foreach (var g in slot.Face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            yield return CardZoom.Unscaled(half, p => slot.Rect.localScale = new Vector3(p, 1.06f - 0.06f * p, 1f) * k);
            slot.Rect.localScale = Vector3.one * k;
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
                default:
                    var owner = OwnerOf(pull.Card);
                    CardView.BuildFace(face, Instance(owner, pull.Card, CardCollection.StartRank(pull.Card)), scale);
                    break;
            }
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
            Afflatus element = member.afflatus;
            foreach (var kit in member.resonances)
                if (kit != null && (kit.cards.Contains(card) || kit.ultimate == card)) element = kit.element;
            unit.ApplyLoadout(new Loadout { Element = element });
            return new CardInstance(card, unit, rank);
        }

        /// <summary>SOUL: the screen dims, the card trembles in a growing rainbow glow, rays spin up.</summary>
        IEnumerator SoulBuildUp(Slot slot)
        {
            var at = slot.Rect.anchoredPosition;
            var glow = UiParticles.Glow(fx, at, 200f, UiParticles.Rainbow(0f, 0f));
            var rays = UiParticles.Rays(fx, at, 12, 520f, 46f);
            var rayImages = rays.GetComponentsInChildren<Image>();
            yield return CardZoom.Unscaled(1.1f, p =>
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
                    particles.Burst(at, 16, 650f, 0.7f, 16f, t => Color.Lerp(Color.white, Gold, t));
                    break;
                case CardPacks.Tier.Rare:
                {
                    var glow = UiParticles.Glow(fx, at, 520f, new Color(1f, 0.55f, 0.15f, 0.8f));
                    glow.transform.SetAsFirstSibling();
                    particles.Burst(at, 34, 950f, 1.0f, 20f, t => Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f), t), 300f);
                    StartCoroutine(PulseOut(glow, 1.4f));
                    yield return Punch(slot.Rect, 0.12f, 0.25f);
                    break;
                }
                case CardPacks.Tier.Soul:
                {
                    // A ring shockwave, a rainbow fountain, a white flash, sparkles on the card and the Soul title.
                    var ring = UiParticles.Glow(fx, at, 80f, Color.white);
                    StartCoroutine(CardZoom.Unscaled(0.6f, p =>
                    {
                        if (ring == null) return;
                        ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(80f, 1500f, p);
                        ring.color = UiParticles.Rainbow(0.3f, 0.7f * (1f - p));
                        if (p >= 1f) Destroy(ring.gameObject);
                    }));
                    FlashTo(1f);
                    particles.Burst(at, 80, 1500f, 1.6f, 24f, t => UiParticles.Rainbow(t * 0.8f), 700f);
                    CardSparkles.Add(slot.Face, Color.white);
                    StartCoroutine(CardZoom.Unscaled(0.6f, p => dimMore.color = new Color(0f, 0f, 0f, 0.55f * (1f - p))));
                    var title = UIFactory.CreateRect("SoulTitle", root).Fill();
                    yield return UltTitle.Play(title, UIText.Get("pack.soul_title", "name", slot.Pull.Member.DisplayName,
                        "n", slot.Pull.SoulLevelAfter.ToString()), UiParticles.Rainbow(0f));
                    if (title != null) Destroy(title.gameObject);
                    break;
                }
            }
        }

        /// <summary>The line under a revealed card: what it did for you.</summary>
        void Label(Slot slot)
        {
            var pull = slot.Pull;
            string text;
            switch (pull.Tier)
            {
                case CardPacks.Tier.Rare: text = UIText.Get("pack.rare", "name", ArcanaCards.Name(pull.Binding)); break;
                case CardPacks.Tier.Soul: text = UIText.Get("pack.soul_up", "n", pull.SoulLevelAfter.ToString()); break;
                default:
                    text = pull.RankedUp ? UIText.Get("pack.rank_up", "rank", UIFactory.Roman(CardCollection.StartRank(pull.Card)))
                         : UIText.Get("pack.copy", "n", CardCollection.Copies(pull.Card).ToString());
                    break;
            }
            slot.Under = UIFactory.CreateText(slot.Rect, "Under", text, 28, pull.Tier == CardPacks.Tier.Common ? InkStyle.Paper : Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            slot.Under.supportRichText = true;
            slot.Under.horizontalOverflow = HorizontalWrapMode.Overflow;
            slot.Under.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(230f, 40f));
        }

        void Finished()
        {
            finished = true;
            hint.text = UIText.Get("pack.hint.zoom");
            done.gameObject.SetActive(true);
            again.gameObject.SetActive(CardCollection.InSession || WorldSession.Packs > 0);
            again.GetComponentInChildren<Text>().text = CardCollection.InSession ? UIText.Get("pack.again.free")
                : UIText.Get("pack.again", "n", WorldSession.Packs.ToString());
        }

        void Zoom(Slot slot)
        {
            var pull = slot.Pull;
            var holo = pull.Tier == CardPacks.Tier.Soul ? CardHolo.Kind.Soul : pull.Tier == CardPacks.Tier.Rare ? CardHolo.Kind.Rare : CardHolo.Kind.Common;
            CardZoom.Open(root, (face, s) => BuildFace(face, pull, s), holo);
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
            StartCoroutine(CardZoom.Unscaled(0.5f, p => flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, alpha * (1f - p))));
        }

        static IEnumerator Punch(RectTransform rt, float amount, float seconds)
        {
            var start = rt.localScale;
            yield return CardZoom.Unscaled(seconds, p => { if (rt != null) rt.localScale = start * (1f + amount * Mathf.Sin(p * Mathf.PI)); });
        }

        static IEnumerator FadeOut(Image img, float seconds)
        {
            var start = img.color;
            yield return CardZoom.Unscaled(seconds, p => { if (img != null) img.color = new Color(start.r, start.g, start.b, start.a * (1f - p)); });
            if (img != null) Destroy(img.gameObject);
        }

        static IEnumerator PulseOut(Image img, float seconds)
        {
            yield return CardZoom.Unscaled(seconds, p =>
            {
                if (img == null) return;
                var c = img.color;
                c.a = 0.8f * (1f - p) * (0.75f + 0.25f * Mathf.Sin(p * 18f));
                img.color = c;
            });
            if (img != null) Destroy(img.gameObject);
        }

        static IEnumerator SpinOut(RectTransform rays, Image[] images, float seconds)
        {
            float start = rays.localEulerAngles.z;
            yield return CardZoom.Unscaled(seconds, p =>
            {
                if (rays == null) return;
                rays.localRotation = Quaternion.Euler(0f, 0f, start - 260f * p);
                for (int i = 0; i < images.Length; i++) images[i].color = UiParticles.Rainbow(i / (float)images.Length, 0.45f * (1f - p));
            });
            if (rays != null) Destroy(rays.gameObject);
        }

        void Update()
        {
            if (CardZoom.IsOpen || CardZoom.ClosedFrame == Time.frameCount || Time.frameCount == openedFrame) return;
            if (pack != null && !torn && !tearing)
            {
                // The waiting pack sways gently.
                pack.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2f) * 3f);
            }
            bool enter = UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space);
            if (enter)
            {
                if (!torn) Tear();
                else
                {
                    if (!finished) hurry = true;
                    else if (done.gameObject.activeSelf) Finish();
                }
            }
            if (UIKeys.Back && !tearing) Finish();   // the cards are already in the collection
        }
    }
}
