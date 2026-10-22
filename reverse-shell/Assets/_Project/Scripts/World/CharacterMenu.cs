using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Esc crew menu, in the Reverse: 1999 look (<see cref="InkStyle"/>). Every party member is a character card:
    /// pixel art on an ink card with a gold hero frame. Left/Right swap between cards, L makes the shown member the
    /// leader. Enter flips the card over and it opens on the left: the robot opened up, with its four item slots (Parts:
    /// WPN / ARM / CHIP / CORE) pinned onto the internals. Top right are the stats (Part bonuses in gold) and the fitted
    /// Parts; along the bottom, every ability card as the real card face: the deck, the Ult and spare cards to swap in.
    /// Up/Down move the gold focus over the slots and deck cards, Enter opens a picker, Esc goes back a step.
    /// The game is paused while it's open; everything animates on unscaled time.
    /// </summary>
    public class CharacterMenu : MonoBehaviour
    {
        // Front card.
        static readonly Vector2 CardSize = new Vector2(500f, 820f);
        const float CardSpacing = 600f, SideScale = 0.74f, PortraitScale = 4f;
        const float FlipTime = 0.38f;
        /// <summary>How long the carousel takes to settle on a new card (critically damped, so it never overshoots).</summary>
        const float SlideTime = 0.16f;

        /// <summary>The item slots shown on the card (weapon / armor slots are shelved for now).</summary>
        static readonly PartSlot[] Slots = { PartSlot.Chip, PartSlot.Core };

        static string SlotLabel(PartSlot slot)
        {
            return slot == PartSlot.Chip ? "CHIP" : "CORE";
        }
        /// <summary>Default socket spots on the shared robot's internals, indexed by slot number (art pixels from the
        /// top-left of the 96x144 portrait): weapon, armor (unused for now), chip, core.</summary>
        static readonly Vector2[] SocketSpots = { new Vector2(30f, 100f), new Vector2(70f, 70f), new Vector2(56f, 44f), new Vector2(52f, 94f) };
        // Every crew card is the reference knight (recoloured): CHIP on the head, CORE on the opened chest.

        OverworldBuilder world;
        CanvasGroup group;
        RectTransform page, carousel, back, backCard, keyBar, titleGroup;
        RawImage glowBackdrop;
        Text keyText;
        readonly List<RectTransform> cards = new List<RectTransform>();
        readonly List<Image> glows = new List<Image>();
        List<CharacterDefinition> party = new List<CharacterDefinition>();
        int selected;
        float openT;
        // The carousel position: a continuous card index that glides toward its target (see AnimateCards).
        float scroll, scrollTarget, scrollVelocity;

        // Flip: 0 = front, 1 = back.
        bool flipped;
        float flipT;

        // Back state. Focus order: the four sockets, then the two deck cards.
        int focus;
        int deckSlot;
        readonly List<RectTransform> focusTargets = new List<RectTransform>();
        RectTransform focusFrame, picker;
        readonly List<System.Action> pickerActions = new List<System.Action>();
        readonly List<Image> pickerRows = new List<Image>();
        int pickerIndex;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>Frame the menu last opened or closed: the same key press must not toggle it twice.</summary>
        public int LastToggleFrame { get; private set; }

        CharacterDefinition Member { get { return party.Count > 0 ? party[selected] : null; } }

        public static CharacterMenu Create(RectTransform parent, OverworldBuilder world)
        {
            var rt = UIFactory.CreateRect("CharacterMenu", parent).Fill();
            var menu = rt.gameObject.AddComponent<CharacterMenu>();
            menu.world = world;
            menu.group = rt.gameObject.AddComponent<CanvasGroup>();
            menu.BuildFrame(rt);
            rt.gameObject.SetActive(false);
            return menu;
        }

        // ------------------------------------------------------------------ Open / close

        public void Open()
        {
            if (IsOpen) return;
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Time.timeScale = 0f;
            openT = 0f;
            flipped = false;
            flipT = 0f;
            party = world.Party;
            selected = 0;
            scroll = scrollTarget = scrollVelocity = 0f;
            BuildCards();
            if (back != null) Destroy(back.gameObject);
            back = null;
            UpdateKeyBar();
            UpdateBackdrop();
        }

        public void Close()
        {
            if (!IsOpen) return;
            LastToggleFrame = Time.frameCount;
            ClosePicker();
            SaveGame.Save();
            world.RefreshPartyHud();
            Time.timeScale = 1f;
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f; // never leave the world paused
        }

        // ------------------------------------------------------------------ Input + animation

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            openT = Mathf.Min(1f, openT + dt / 0.25f);
            group.alpha = Ease(openT);
            page.anchoredPosition = new Vector2(0f, Mathf.Lerp(-30f, 0f, Ease(openT)));

            if (Time.frameCount != LastToggleFrame) HandleInput();
            AnimateCards(dt);
            AnimateFlip(dt);

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.4f);
            foreach (var g in glows)
            {
                if (g == null) continue;
                var c = g.color;
                c.a = pulse;
                g.color = c;
            }
        }

        void HandleInput()
        {
            bool cancel = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);

            if (picker != null)
            {
                if (cancel) ClosePicker();
                else if (up) MovePicker(-1);
                else if (down) MovePicker(1);
                else if (confirm && pickerIndex < pickerActions.Count) pickerActions[pickerIndex]();
                return;
            }
            if (cancel)
            {
                if (flipped) Flip(false);
                else Close();
                return;
            }
            if (party.Count == 0) return;
            if (left) Step(-1);
            if (right) Step(1);

            if (!flipped)
            {
                if (confirm) Flip(true);
                if (Input.GetKeyDown(KeyCode.L)) MakeLeader();
            }
            else if (focusTargets.Count > 0)
            {
                if (up) SetFocus((focus - 1 + focusTargets.Count) % focusTargets.Count);
                if (down) SetFocus((focus + 1) % focusTargets.Count);
                if (confirm) Activate(focus);
            }
        }

        /// <summary>Moves the carousel by whole cards (negative = left), the short way round.</summary>
        void Step(int cards)
        {
            if (party.Count == 0 || cards == 0) return;
            scrollTarget += cards;
            selected = Mod(Mathf.RoundToInt(scrollTarget), party.Count);
            if (flipped) BuildBack();
            UpdateBackdrop();
        }

        static int Mod(int a, int n)
        {
            return ((a % n) + n) % n;
        }

        void MakeLeader()
        {
            if (Member == null || selected == 0) return;
            world.SetLeader(Member);
            party = world.Party;
            selected = 0;
            scroll = scrollTarget = scrollVelocity = 0f;
            BuildCards();
            UpdateBackdrop();
        }

        void Flip(bool toBack)
        {
            if (flipped == toBack) return;
            flipped = toBack;
            if (toBack)
            {
                focus = 0;
                deckSlot = 0;
                BuildBack();
            }
            else ClosePicker();
            UpdateKeyBar();
        }

        /// <summary>
        /// The flip: the front card turns edge-on (scale X to 0), then the back card turns face-on from the same spot
        /// while the side panels fade in. Scaling reads as a Y rotation in a screen-space canvas and keeps pixels square.
        /// </summary>
        void AnimateFlip(float dt)
        {
            flipT = Mathf.MoveTowards(flipT, flipped ? 1f : 0f, dt / FlipTime);
            float t = Mathf.SmoothStep(0f, 1f, flipT);
            bool showBack = t > 0.5f && back != null;
            float squash = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
            carousel.gameObject.SetActive(!showBack);
            titleGroup.gameObject.SetActive(!showBack);
            if (back != null) back.gameObject.SetActive(showBack);
            if (!showBack)
            {
                if (cards.Count > 0)
                {
                    var center = cards[selected];
                    center.localScale = new Vector3(center.localScale.y * squash, center.localScale.y, 1f);
                }
                return;
            }
            backCard.localScale = new Vector3(squash, 1f, 1f);
            var cg = back.GetComponent<CanvasGroup>();
            cg.alpha = Mathf.Clamp01((t - 0.5f) * 2f);
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        // ------------------------------------------------------------------ Frame

        void BuildFrame(RectTransform root)
        {
            var bg = UIFactory.CreateImage(root, "Page", InkStyle.Ink, true);
            bg.rectTransform.Fill();
            InkStyle.Gradient(bg.rectTransform, new Color(0.16f, 0.15f, 0.14f), new Color(0.06f, 0.055f, 0.05f));

            // A soft glow in the shown member's color behind the card.
            glowBackdrop = new GameObject("Glow", typeof(RectTransform)).AddComponent<RawImage>();
            glowBackdrop.rectTransform.SetParent(root, false);
            glowBackdrop.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1600f, 1600f));
            glowBackdrop.texture = RadialTexture();
            glowBackdrop.raycastTarget = false;
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.04f));

            page = UIFactory.CreateRect("Content", root).Fill();
            titleGroup = UIFactory.CreateRect("Title", page).Fill();
            var title = InkStyle.Title(titleGroup, "Title", "Crew", 76, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -36f), new Vector2(500f, 100f));
            var rule = InkStyle.Line(titleGroup, "TitleRule", InkStyle.Gold);
            rule.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -138f), new Vector2(260f, 2f));

            carousel = UIFactory.CreateRect("Cards", page).Fill();

            keyBar = UIFactory.CreateRect("Keys", page).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 44f));
            keyText = UIFactory.CreateText(keyBar, "Text", "", 22, InkStyle.PaperDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            keyText.supportRichText = true;
            keyText.rectTransform.Fill();
        }

        void UpdateKeyBar()
        {
            const string gold = "<color=#C2A061>";
            keyText.text = flipped
                ? gold + "← →</color>  Switch      " + gold + "↑ ↓</color>  Select      " + gold + "Enter</color>  Change      " + gold + "Esc</color>  Back"
                : gold + "← →</color>  Switch      " + gold + "Enter</color>  Open      " + gold + "L</color>  Leader      " + gold + "Esc</color>  Close";
        }

        void UpdateBackdrop()
        {
            var c = AfflatusInfo.GlowFor(Member);
            c.a = 0.16f;
            glowBackdrop.color = c;
        }

        // ------------------------------------------------------------------ Front: the card carousel

        void BuildCards()
        {
            UIFactory.ClearChildren(carousel);
            cards.Clear();
            glows.Clear();
            for (int i = 0; i < party.Count; i++)
            {
                int captured = i;
                var card = BuildCard(party[i], i == 0);
                Clickable(card, () =>
                {
                    if (selected == captured) Flip(true);
                    else Step(Mathf.RoundToInt(Offset(captured, selected)));
                });
                cards.Add(card);
            }
            PlaceCards();
        }

        RectTransform BuildCard(CharacterDefinition member, bool leader)
        {
            var progress = WorldSession.ProgressOf(member);
            var glowColor = AfflatusInfo.GlowFor(member);

            var root = UIFactory.CreateRect(member.DisplayName, carousel).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardSize);
            root.gameObject.AddComponent<CanvasGroup>();
            CardFace(root, glowColor);
            Portrait(root, member.portraitFront, member.portraitGlow, glowColor, PortraitScale, new Vector2(0f, -26f), member);

            if (leader)
            {
                var tag = InkStyle.Label(root, "Leader", "Leader", 16, InkStyle.Gold, TextAnchor.UpperRight);
                tag.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -26f), new Vector2(240f, 30f));
            }

            // Name, element and class, level, a slim HP bar.
            var bottom = new Vector2(0.5f, 0f);
            var divider = InkStyle.Line(root, "Divider", InkStyle.Rule);
            divider.rectTransform.Place(bottom, bottom, new Vector2(0f, 214f), new Vector2(CardSize.x - 80f, 2f));
            var name = InkStyle.Title(root, "Name", member.DisplayName, 64, InkStyle.Paper, TextAnchor.LowerLeft).BestFit(30, 64);
            name.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(40f, 118f), new Vector2(300f, 90f));
            var meta = InkStyle.Label(root, "Meta", AfflatusInfo.LabelFor(member) + " · " + member.archetype, 15, UIFactory.WithAlpha(glowColor, 0.9f), TextAnchor.UpperLeft);
            meta.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(42f, 78f), new Vector2(330f, 36f));
            var lvLabel = InkStyle.Label(root, "LvLabel", "Lv", 15, InkStyle.Muted, TextAnchor.LowerRight);
            lvLabel.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 186f), new Vector2(120f, 26f));
            var lv = InkStyle.Title(root, "Lv", progress.Level.ToString(), 86, InkStyle.GoldBright, TextAnchor.UpperRight);
            lv.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 70f), new Vector2(150f, 116f));
            HpBar(root, member, new Vector2(0f, 46f), CardSize.x - 80f, 8f, bottom);
            return root;
        }

        /// <summary>The ink card face: a tinted wash from the top and the gold hero frame.</summary>
        static void CardFace(RectTransform root, Color glowColor)
        {
            var face = UIFactory.CreateImage(root, "Face", new Color(0.09f, 0.085f, 0.08f), true);
            face.rectTransform.Fill();
            var wash = InkStyle.Gradient(face.rectTransform, UIFactory.WithAlpha(glowColor, 0.2f), new Color(0f, 0f, 0f, 0f));
            wash.rectTransform.offsetMin = new Vector2(0f, root.sizeDelta.y * 0.3f);
            InkStyle.HeroFrame(root, InkStyle.Gold, 10f);
        }

        /// <summary>A portrait at a whole-number scale (crisp pixels) with its pulsing glow layer on top.</summary>
        RectTransform Portrait(RectTransform root, Sprite art, Sprite glowMask, Color glowColor, float scale, Vector2 position, CharacterDefinition member)
        {
            var top = new Vector2(0.5f, 1f);
            var size = new Vector2(96f, 144f) * scale;
            var holder = UIFactory.CreateRect("Portrait", root).Place(top, top, position, size);
            if (art == null)
            {
                var initial = InkStyle.Title(holder, "Initial", member.DisplayName.Substring(0, 1), 300, UIFactory.WithAlpha(glowColor, 0.6f), TextAnchor.MiddleCenter);
                initial.rectTransform.Fill();
                return holder;
            }
            var img = UIFactory.CreateImage(holder, "Art", Color.white);
            img.sprite = art;
            img.rectTransform.Fill();
            if (glowMask != null)
            {
                var glow = UIFactory.CreateImage(holder, "Glow", glowColor);
                glow.sprite = glowMask;
                glow.rectTransform.Fill();
                glows.Add(glow);
            }
            return holder;
        }

        static void HpBar(RectTransform root, CharacterDefinition member, Vector2 position, float width, float height, Vector2 anchor)
        {
            int hp = WorldSession.HPOf(member), max = WorldSession.MaxHPOf(member);
            var track = UIFactory.CreateImage(root, "HpTrack", new Color(1f, 1f, 1f, 0.1f));
            track.rectTransform.Place(anchor, anchor, position, new Vector2(width, height));
            Fill(track.rectTransform, hp / (float)Mathf.Max(1, max), hp <= max / 4 ? new Color(0.92f, 0.32f, 0.3f) : new Color(0.4f, 0.85f, 0.5f));
        }

        static void Fill(RectTransform track, float amount, Color color)
        {
            var fill = UIFactory.CreateImage(track, "Fill", color);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>Where card i sits relative to <paramref name="at"/> (a card index, fractional while gliding),
        /// wrapped so the cards either side of it are its neighbours: in (-n/2, n/2].</summary>
        float Offset(int i, float at)
        {
            int n = party.Count;
            float o = (i - at) % n;
            if (o < 0f) o += n;
            if (o > n * 0.5f) o -= n;
            return o;
        }

        /// <summary>
        /// Lays the cards out from the gliding scroll value: the centre card full size, neighbours smaller and dimmer,
        /// a gentle drop toward the sides. Cards fade out before they wrap round, so nothing visibly jumps.
        /// </summary>
        void PlaceCards()
        {
            var order = new List<KeyValuePair<float, RectTransform>>();
            for (int i = 0; i < cards.Count; i++)
            {
                float o = Offset(i, scroll);
                float d = Mathf.Abs(o);
                float near = Mathf.Clamp01(d);
                var card = cards[i];
                card.anchoredPosition = new Vector2(o * CardSpacing, -36f * near * near);
                float s = Mathf.Lerp(1f, SideScale, near);
                card.localScale = new Vector3(s, s, 1f);
                float alpha = d <= 1f ? Mathf.Lerp(1f, 0.4f, d) : Mathf.Max(0f, 0.4f * (1f - (d - 1f) * 2.5f));
                card.GetComponent<CanvasGroup>().alpha = alpha;
                order.Add(new KeyValuePair<float, RectTransform>(d, card));
            }
            order.Sort((a, b) => b.Key.CompareTo(a.Key)); // the nearest card draws on top
            foreach (var entry in order) entry.Value.SetAsLastSibling();
        }

        void AnimateCards(float dt)
        {
            if (!carousel.gameObject.activeSelf) return;
            scroll = Mathf.SmoothDamp(scroll, scrollTarget, ref scrollVelocity, SlideTime, Mathf.Infinity, dt);
            PlaceCards();
        }

        // ------------------------------------------------------------------ Back: the opened card, stats and cards

        // The back fits the narrowest view (4:3 shows ~1740 of the 2400 wide layout): everything sits within
        // x = -820..820 around the centre. The opened card on the left, stats top right, every card along the bottom.
        const float BackLeft = -820f, BackRight = 820f, BackTop = 615f, TopHeight = 760f, BottomRowTop = -175f;
        const float BackArtScale = 4f, RowCardScale = 1.5f;
        static readonly Vector2 BackCardSize = new Vector2(480f, TopHeight);

        void BuildBack()
        {
            ClosePicker();
            if (back != null) Destroy(back.gameObject);
            focusTargets.Clear();
            focusFrame = null;
            glows.RemoveAll(g => g == null); // glow layers of earlier rebuilds
            var member = Member;
            if (member == null) return;

            back = UIFactory.CreateRect("Back", page).Fill();
            back.gameObject.AddComponent<CanvasGroup>();
            back.gameObject.SetActive(flipT > 0.5f);
            var glowColor = AfflatusInfo.GlowFor(member);
            var progress = WorldSession.ProgressOf(member);
            var center = new Vector2(0.5f, 0.5f);

            // The opened card, on the left: the robot's internals with the item sockets pinned on.
            backCard = UIFactory.CreateRect("Card", back).Place(center, center,
                new Vector2(BackLeft + BackCardSize.x / 2f, BackTop - BackCardSize.y / 2f), BackCardSize);
            CardFace(backCard, glowColor);
            var art = Portrait(backCard, member.portraitInternals != null ? member.portraitInternals : member.portraitFront,
                member.portraitInternals != null ? member.portraitInternalsGlow : member.portraitGlow, glowColor, BackArtScale, new Vector2(0f, -40f), member);
            var caption = InkStyle.Label(backCard, "Caption", "Internals", 15, InkStyle.Gold, TextAnchor.MiddleCenter);
            caption.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(400f, 30f));
            var name = InkStyle.Title(backCard, "Name", member.DisplayName, 46, InkStyle.Paper, TextAnchor.MiddleCenter);
            name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(420f, 56f));
            for (int s = 0; s < Slots.Length; s++)
            {
                var socket = Socket(art, member, Slots[s]);
                int index = s;
                Clickable(socket, () => { SetFocus(index); Activate(index); });
                focusTargets.Add(socket);
            }

            BuildStats(member, progress, glowColor);
            BuildCardRow(member, progress);
            SetFocus(Mathf.Clamp(focus, 0, focusTargets.Count - 1));
        }

        /// <summary>An item socket on the internals art: a ring (gold when filled, in the Part's color) with its slot label.</summary>
        RectTransform Socket(RectTransform art, CharacterDefinition member, PartSlot slot)
        {
            var part = WorldSession.PartIn(member, slot);
            int i = (int)slot;
            var spot = (member.partSockets != null && i < member.partSockets.Count ? member.partSockets[i] : SocketSpots[i]) * BackArtScale;
            var socket = UIFactory.CreateRect(SlotLabel(slot), art).Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(spot.x, -spot.y), new Vector2(64f, 64f));
            var ring = UIFactory.CreateImage(socket, "Ring", part != null ? InkStyle.GoldBright : UIFactory.WithAlpha(InkStyle.Paper, 0.75f), true);
            ring.sprite = RingSprite();
            ring.rectTransform.Fill();
            var core = UIFactory.CreateImage(socket, "Core", part != null ? part.color : new Color(0f, 0f, 0f, 0.6f));
            core.sprite = CircleSprite();
            core.rectTransform.Fill(10f);
            var icon = UIFactory.CreateImage(socket, "Icon", part != null ? InkStyle.Ink : UIFactory.WithAlpha(InkStyle.Paper, 0.6f));
            icon.sprite = SlotIcon(i);
            icon.rectTransform.Fill(20f);
            var label = UIFactory.CreateText(socket, "Label", SlotLabel(slot), 15, InkStyle.Paper, TextAnchor.UpperCenter, FontStyle.Bold);
            label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(120f, 22f));
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return socket;
        }

        /// <summary>
        /// Top right: name, element and class, the level inside a ring that fills with XP, HP, then Persona-style stat
        /// bars (Part bonuses as a gold segment) and the CHIP / CORE slots. Bars are scaled to the reference
        /// character (MC) so that its level-30 stats fill 90% of them (<see cref="Leveling.BarMax"/>).
        /// </summary>
        void BuildStats(CharacterDefinition member, MemberProgress progress, Color glowColor)
        {
            float left = BackLeft + BackCardSize.x + 40f;
            var size = new Vector2(BackRight - left, TopHeight);
            var p = InkStyle.Panel(back, "Stats", InkStyle.InkPanel).rectTransform;
            p.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(left, BackTop), size);
            InkStyle.Frame(p, InkStyle.Rule, 2f);
            var tl = new Vector2(0f, 1f);
            float w = size.x - 80f;

            var name = InkStyle.Title(p, "Name", member.DisplayName, 64, InkStyle.Paper, TextAnchor.UpperLeft).BestFit(30, 64);
            name.rectTransform.Place(tl, tl, new Vector2(40f, -22f), new Vector2(w - 220f, 80f));
            var meta = InkStyle.Label(p, "Meta", AfflatusInfo.LabelFor(member) + " · " + member.archetype + " · " + member.damageType, 15, glowColor, TextAnchor.UpperLeft);
            meta.rectTransform.Place(tl, tl, new Vector2(42f, -102f), new Vector2(w - 220f, 30f));
            LevelRing(p, progress);

            // HP, with exact numbers (this is the details screen).
            int hp = WorldSession.HPOf(member), max = WorldSession.MaxHPOf(member);
            Meter(p, "HP", hp + " / " + max, hp / (float)Mathf.Max(1, max), hp <= max / 4 ? new Color(0.92f, 0.32f, 0.3f) : new Color(0.4f, 0.85f, 0.5f), -160f, w);

            // Stat bars.
            var reference = party.Find(m => m != null && m.statBarReference) ?? member;
            var with = UnitFor(member, progress, true);
            var without = UnitFor(member, progress, false);
            var stats = new[]
            {
                new KeyValuePair<string, StatType>("HP", StatType.MaxHP),
                new KeyValuePair<string, StatType>("ATK", StatType.Attack),
                new KeyValuePair<string, StatType>("P.DEF", StatType.PhysicalDefense),
                new KeyValuePair<string, StatType>("S.DEF", StatType.SpecialDefense),
                new KeyValuePair<string, StatType>("CRIT", StatType.CritRate),
                new KeyValuePair<string, StatType>("C.DMG", StatType.CritDamage),
            };
            // Persona 5-style: a white slab with the rows stacked tight.
            var slab = UIFactory.CreateImage(p, "StatSlab", InkStyle.Paper);
            slab.rectTransform.Place(tl, tl, new Vector2(24f, -204f), new Vector2(w + 32f, stats.Length * StatRow + 12f));
            for (int i = 0; i < stats.Length; i++)
            {
                var stat = stats[i].Value;
                bool percent = stat == StatType.CritRate || stat == StatType.CritDamage;
                float a = with.GetStat(stat), b = without.GetStat(stat);
                float barMax = Leveling.BarMax(reference, stat);
                StatBar(p, stats[i].Key, percent ? Mathf.RoundToInt(a * 100f) + "%" : Mathf.RoundToInt(a).ToString(),
                    b / barMax, a / barMax, -210f - i * StatRow, w);
            }

            // Item slots: CHIP and CORE side by side (click to change, same as the sockets on the card).
            const float gap = 30f;
            var head = InkStyle.Label(p, "PartsLabel", "Parts", 15, InkStyle.Gold, TextAnchor.LowerLeft);
            head.rectTransform.Place(tl, tl, new Vector2(40f, -558f), new Vector2(300f, 28f));
            float partW = (w - gap) / 2f;
            for (int s = 0; s < Slots.Length; s++)
            {
                var slot = Slots[s];
                var part = WorldSession.PartIn(member, slot);
                var row = InkStyle.Panel(p, SlotLabel(slot), new Color(1f, 1f, 1f, 0.04f), true).rectTransform;
                row.Place(tl, tl, new Vector2(40f + s * (partW + gap), -594f), new Vector2(partW, 130f));
                var icon = UIFactory.CreateImage(row, "Icon", part != null ? part.color : new Color(1f, 1f, 1f, 0.2f));
                icon.sprite = SlotIcon((int)slot);
                icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(56f, 56f));
                var slotText = UIFactory.CreateText(row, "Slot", SlotLabel(slot), 16, InkStyle.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
                slotText.rectTransform.Place(tl, tl, new Vector2(98f, -16f), new Vector2(120f, 24f));
                var partName = UIFactory.CreateText(row, "Name", part != null ? part.DisplayName : "Empty", 28,
                    part != null ? InkStyle.Paper : InkStyle.Muted, TextAnchor.UpperLeft, FontStyle.Bold).BestFit(14, 28);
                partName.rectTransform.Place(tl, tl, new Vector2(98f, -44f), new Vector2(partW - 116f, 38f));
                if (part != null)
                {
                    var desc = UIFactory.CreateText(row, "Desc", part.description, 20, InkStyle.GoldBright, TextAnchor.UpperLeft, FontStyle.Bold).BestFit(11, 20);
                    desc.rectTransform.Place(tl, tl, new Vector2(98f, -86f), new Vector2(partW - 116f, 30f));
                }
                int index = s;
                Clickable(row, () => { SetFocus(index); Activate(index); });
            }
        }

        /// <summary>The level number inside a ring that fills clockwise with XP toward the next level.</summary>
        static void LevelRing(RectTransform p, MemberProgress progress)
        {
            var corner = new Vector2(1f, 1f);
            var holder = UIFactory.CreateRect("Level", p).Place(corner, corner, new Vector2(-40f, -16f), new Vector2(118f, 118f));
            var track = UIFactory.CreateImage(holder, "Track", new Color(1f, 1f, 1f, 0.12f));
            track.sprite = RingSprite();
            track.rectTransform.Fill();
            var fill = UIFactory.CreateImage(holder, "Xp", InkStyle.Gold);
            fill.sprite = RingSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = progress.Level >= Leveling.MaxLevel ? 1f : progress.Xp / (float)Mathf.Max(1, Leveling.XpToNext(progress.Level));
            fill.rectTransform.Fill();
            var lv = InkStyle.Title(holder, "Lv", progress.Level.ToString(), 54, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            lv.rectTransform.Fill();
            lv.rectTransform.offsetMin = new Vector2(0f, -6f);
            var cap = InkStyle.Label(holder, "Cap", "Lv", 12, InkStyle.Muted, TextAnchor.UpperCenter);
            cap.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(80f, 20f));
        }

        const float StatRow = 54f;
        static readonly Color P5Red = new Color(0.84f, 0.06f, 0.1f);
        static readonly Color P5Track = new Color(0.86f, 0.8f, 0.78f);

        /// <summary>
        /// A Persona 5-style stat row on the white slab: a big condensed black label and value ("MAX" once the bar is
        /// full), then a long red bar. The Parts' bonus continues the bar in gold (a drop shows dark), and a thin mark
        /// shows the 90% level-cap point.
        /// </summary>
        static void StatBar(RectTransform p, string label, string value, float baseFill, float totalFill, float y, float w)
        {
            var tl = new Vector2(0f, 1f);
            var ink = new Color(0.08f, 0.04f, 0.06f);
            var l = UIFactory.CreateText(p, label, label, 40, ink, TextAnchor.MiddleLeft, FontStyle.Italic);
            l.font = UIFactory.DisplayFont;
            l.rectTransform.Place(tl, tl, new Vector2(40f, y), new Vector2(120f, StatRow - 4f));
            var v = UIFactory.CreateText(p, label + "Value", value, 44, ink, TextAnchor.MiddleRight, FontStyle.Italic);
            v.font = UIFactory.DisplayFont;
            v.rectTransform.Place(tl, tl, new Vector2(150f, y), new Vector2(120f, StatRow - 4f));
            if (totalFill >= 0.999f)
            {
                var max = UIFactory.CreateText(p, label + "Max", "MAX", 22, ink, TextAnchor.LowerLeft, FontStyle.Italic);
                max.font = UIFactory.DisplayFont;
                max.rectTransform.Place(tl, tl, new Vector2(274f, y - 4f), new Vector2(60f, StatRow - 12f));
            }

            float barX = 336f, barW = w + 40f - barX;
            var track = UIFactory.CreateImage(p, label + "Track", P5Track);
            track.rectTransform.Place(tl, tl, new Vector2(barX, y - 6f), new Vector2(barW, StatRow - 14f));
            float lo = Mathf.Clamp01(Mathf.Min(baseFill, totalFill)), hi = Mathf.Clamp01(Mathf.Max(baseFill, totalFill));
            Segment(track.rectTransform, 0f, lo, P5Red);
            if (hi - lo > 0.001f) Segment(track.rectTransform, lo, hi, totalFill >= baseFill ? InkStyle.GoldBright : ink);
            var tick = UIFactory.CreateImage(track.transform, "CapTick", new Color(0f, 0f, 0f, 0.35f));
            tick.rectTransform.anchorMin = new Vector2(Leveling.BarFillAtCap, 0f);
            tick.rectTransform.anchorMax = new Vector2(Leveling.BarFillAtCap, 1f);
            tick.rectTransform.offsetMin = new Vector2(-1.5f, 0f);
            tick.rectTransform.offsetMax = new Vector2(1.5f, 0f);
        }

        static void Segment(RectTransform track, float from, float to, Color color)
        {
            var seg = UIFactory.CreateImage(track, "Fill", color);
            seg.rectTransform.anchorMin = new Vector2(from, 0f);
            seg.rectTransform.anchorMax = new Vector2(to, 1f);
            seg.rectTransform.offsetMin = seg.rectTransform.offsetMax = Vector2.zero;
        }

        void Meter(RectTransform p, string label, string value, float amount, Color color, float y, float w)
        {
            var tl = new Vector2(0f, 1f);
            var l = InkStyle.Label(p, label, label, 15, InkStyle.Gold, TextAnchor.MiddleLeft);
            l.rectTransform.Place(tl, tl, new Vector2(40f, y), new Vector2(80f, 36f));
            var track = UIFactory.CreateImage(p, label + "Track", new Color(1f, 1f, 1f, 0.1f));
            track.rectTransform.Place(tl, tl, new Vector2(130f, y - 12f), new Vector2(w - 330f, 12f));
            Fill(track.rectTransform, amount, color);
            var v = UIFactory.CreateText(p, label + "Value", value, 24, InkStyle.Paper, TextAnchor.MiddleRight, FontStyle.Bold);
            v.rectTransform.Place(tl, tl, new Vector2(40f + w - 190f, y), new Vector2(190f, 36f));
        }

        /// <summary>
        /// Along the bottom: every card this member has, as the real card faces. Deck (click one to pick its slot),
        /// the Ult, and spare cards (click one to swap it into the picked deck slot). Scroll over a card to flip its rank.
        /// </summary>
        void BuildCardRow(CharacterDefinition member, MemberProgress progress)
        {
            var unit = UnitFor(member, progress, true);
            var deck = WorldSession.DeckOf(member);
            var spares = member.AllIncantations().FindAll(c => !deck.Contains(c));
            var groups = new List<KeyValuePair<string, List<CardDefinition>>>
            {
                new KeyValuePair<string, List<CardDefinition>>("Deck", deck.Count > 2 ? deck.GetRange(0, 2) : deck),
                new KeyValuePair<string, List<CardDefinition>>("Ult", member.ritual != null ? new List<CardDefinition> { member.ritual } : new List<CardDefinition>()),
                new KeyValuePair<string, List<CardDefinition>>(spares.Count > 0 ? "Spare · click to swap in" : "Spare", spares),
            };
            groups.RemoveAll(g => g.Value.Count == 0 && g.Key != "Deck");

            var cardSize = CardView.Size * RowCardScale;
            const float cardGap = 24f, groupGap = 70f;
            float total = 0f;
            foreach (var g in groups) total += g.Value.Count * cardSize.x + Mathf.Max(0, g.Value.Count - 1) * cardGap;
            total += (groups.Count - 1) * groupGap;

            var center = new Vector2(0.5f, 0.5f);
            float x = -total / 2f;
            foreach (var g in groups)
            {
                float groupW = g.Value.Count * cardSize.x + Mathf.Max(0, g.Value.Count - 1) * cardGap;
                var label = InkStyle.Label(back, g.Key, g.Key, 15, InkStyle.Gold, TextAnchor.LowerLeft);
                label.rectTransform.Place(center, new Vector2(0f, 1f), new Vector2(x, BottomRowTop), new Vector2(Mathf.Max(groupW, 360f), 28f));
                var rule = InkStyle.Line(back, "Rule", InkStyle.Rule);
                rule.rectTransform.Place(center, new Vector2(0f, 1f), new Vector2(x, BottomRowTop - 32f), new Vector2(groupW, 1f));
                for (int i = 0; i < g.Value.Count; i++)
                {
                    var card = g.Value[i];
                    var holder = UIFactory.CreateRect("Slot", back).Place(center, new Vector2(0f, 1f), new Vector2(x, BottomRowTop - 46f), cardSize);
                    var ability = AbilityCard.Create(holder, unit, card, 0, RowCardScale);
                    var rt = (RectTransform)ability.transform;
                    rt.Fill();
                    if (g.Key == "Deck")
                    {
                        int slot = i;
                        Clickable(rt, () => SetFocus(Slots.Length + slot));
                        focusTargets.Add(rt);
                    }
                    else if (!card.isRitual)
                    {
                        var captured = card;
                        Clickable(rt, () => SwapIn(captured));
                    }
                    x += cardSize.x + cardGap;
                }
                x += groupGap - cardGap;
            }
        }

        static BattleUnit UnitFor(CharacterDefinition member, MemberProgress progress, bool withParts)
        {
            var unit = new BattleUnit(member, Team.Player);
            unit.ApplyLoadout(new Loadout { Cards = WorldSession.DeckOf(member), Parts = withParts ? WorldSession.PartsOf(member) : new List<PartDefinition>() });
            unit.SetLevel(progress.Level);
            return unit;
        }

        // ------------------------------------------------------------------ Back: focus, pickers and changes

        /// <summary>Moves the gold focus frame. Focusing a deck card also makes it the one spare cards swap into.</summary>
        void SetFocus(int index)
        {
            if (focusTargets.Count == 0) return;
            focus = Mathf.Clamp(index, 0, focusTargets.Count - 1);
            if (focus >= Slots.Length) deckSlot = focus - Slots.Length;
            if (focusFrame != null) Destroy(focusFrame.gameObject);
            focusFrame = UIFactory.CreateRect("Focus", focusTargets[focus]).Fill(-10f);
            InkStyle.Frame(focusFrame, InkStyle.GoldBright, 4f);
        }

        void Activate(int index)
        {
            var member = Member;
            if (member == null) return;
            if (index < Slots.Length) OpenPartPicker(member, Slots[index]);
            else OpenCardPicker(member, index - Slots.Length);
        }

        void SwapIn(CardDefinition card)
        {
            WorldSession.SetDeckCard(Member, deckSlot, card);
            SaveGame.Save();
            BuildBack();
        }

        void OpenCardPicker(CharacterDefinition member, int slot)
        {
            deckSlot = slot;
            var deck = WorldSession.DeckOf(member);
            var options = new List<KeyValuePair<string, System.Action>>();
            foreach (var card in member.AllIncantations())
            {
                var captured = card;
                int at = deck.IndexOf(card);
                string label = card.DisplayName + (at == slot ? "   (here)" : at >= 0 ? "   (swap places)" : "");
                options.Add(new KeyValuePair<string, System.Action>(label, () => SwapIn(captured)));
            }
            OpenPicker("Deck card " + (slot + 1), options);
        }

        void OpenPartPicker(CharacterDefinition member, PartSlot slot)
        {
            var options = new List<KeyValuePair<string, System.Action>>();
            if (WorldSession.Catalog != null)
                foreach (var part in WorldSession.Catalog.parts)
                {
                    if (part == null || part.slot != slot) continue;
                    int count = WorldSession.CountOf(part);
                    if (count <= 0) continue;
                    var captured = part;
                    options.Add(new KeyValuePair<string, System.Action>(part.DisplayName + "  ×" + count + "    <color=#E6C784>" + part.description + "</color>", () =>
                    {
                        WorldSession.Equip(member, captured);
                        SaveGame.Save();
                        BuildBack();
                    }));
                }
            if (WorldSession.PartIn(member, slot) != null)
                options.Add(new KeyValuePair<string, System.Action>("Remove", () =>
                {
                    WorldSession.Unequip(member, slot);
                    SaveGame.Save();
                    BuildBack();
                }));
            if (options.Count == 0) options.Add(new KeyValuePair<string, System.Action>("No spare parts", ClosePicker));
            OpenPicker(SlotLabel(slot), options);
        }

        /// <summary>A small modal list over the page: Up/Down + Enter or click; Esc or a click outside closes it.</summary>
        void OpenPicker(string title, List<KeyValuePair<string, System.Action>> options)
        {
            ClosePicker();
            const float rowH = 70f, width = 820f;
            picker = UIFactory.CreateImage(back, "Picker", new Color(0f, 0f, 0f, 0.55f), true).rectTransform;
            picker.Fill();
            Clickable(picker, ClosePicker);
            float height = 100f + options.Count * (rowH + 10f) + 20f;
            var box = InkStyle.Panel(picker, "Box", new Color(0.09f, 0.085f, 0.08f, 0.98f), true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            InkStyle.HeroFrame(box, InkStyle.Gold, 8f);

            var head = InkStyle.Label(box, "Title", title, 18, InkStyle.Gold, TextAnchor.MiddleLeft);
            head.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -24f), new Vector2(width - 68f, 50f));
            pickerActions.Clear();
            pickerRows.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                var row = InkStyle.Panel(box, "Option" + i, new Color(1f, 1f, 1f, 0.05f), true);
                row.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -90f - i * (rowH + 10f)), new Vector2(width - 56f, rowH));
                var t = UIFactory.CreateText(row.transform, "Text", options[i].Key, 24, InkStyle.Paper, TextAnchor.MiddleLeft, FontStyle.Bold).BestFit(12, 24);
                t.supportRichText = true;
                t.rectTransform.Fill(20f);
                var action = options[i].Value;
                int index = i;
                Clickable(row.rectTransform, () => { pickerIndex = index; action(); });
                pickerActions.Add(action);
                pickerRows.Add(row);
            }
            pickerIndex = 0;
            HighlightPicker();
        }

        void MovePicker(int step)
        {
            if (pickerRows.Count == 0) return;
            pickerIndex = (pickerIndex + step + pickerRows.Count) % pickerRows.Count;
            HighlightPicker();
        }

        void HighlightPicker()
        {
            for (int i = 0; i < pickerRows.Count; i++)
                pickerRows[i].color = i == pickerIndex ? UIFactory.WithAlpha(InkStyle.Gold, 0.3f) : new Color(1f, 1f, 1f, 0.05f);
        }

        void ClosePicker()
        {
            if (picker != null) Destroy(picker.gameObject);
            picker = null;
            pickerActions.Clear();
            pickerRows.Clear();
        }

        // ------------------------------------------------------------------ Helpers

        static void Clickable(RectTransform rt, UnityEngine.Events.UnityAction onClick)
        {
            var graphic = rt.GetComponent<Graphic>();
            if (graphic == null)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = Color.clear;
                graphic = img;
            }
            graphic.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
        }

        static Sprite circle, ring;
        static Texture2D radial;
        static readonly Sprite[] slotIcons = new Sprite[4];

        static Sprite CircleSprite()
        {
            if (circle == null) circle = Disc(0f);
            return circle;
        }

        static Sprite RingSprite()
        {
            if (ring == null) ring = Disc(0.78f);
            return ring;
        }

        /// <summary>An anti-aliased disc; inner > 0 cuts a hole (a ring with that inner radius fraction).</summary>
        static Sprite Disc(float inner)
        {
            const int size = 96;
            float r = size / 2f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d);
                    if (inner > 0f) a *= Mathf.Clamp01(d - r * inner);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D RadialTexture()
        {
            if (radial != null) return radial;
            const int size = 128;
            radial = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    radial.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            radial.Apply();
            return radial;
        }

        /// <summary>Tiny pixel icons for the Part slots: blade, shield, chip, core (12 px, drawn in code).</summary>
        static Sprite SlotIcon(int slot)
        {
            if (slotIcons[slot] != null) return slotIcons[slot];
            string[][] art =
            {
                new[] { "..........##", ".........###", "........###.", ".......###..", "......###...", ".#...###....", "..#.###.....", "...###......", "..###.......", ".##.#.......", "##...#......", "#..........." },
                new[] { "############", "#..........#", "#.########.#", "#.########.#", "#.########.#", "#.########.#", ".#.######.#.", ".#.######.#.", "..#.####.#..", "...#.##.#...", "....#..#....", ".....##....." },
                new[] { ".#.#.#.#.#..", "############", "#..........#", "#.########.#", "#.#......#.#", "#.#.####.#.#", "#.#.####.#.#", "#.#......#.#", "#.########.#", "#..........#", "############", ".#.#.#.#.#.." },
                new[] { "....####....", "..##....##..", ".#..####..#.", ".#.#....#.#.", "#.#..##..#.#", "#.#.####.#.#", "#.#.####.#.#", "#.#..##..#.#", ".#.#....#.#.", ".#..####..#.", "..##....##..", "....####...." },
            };
            var rows = art[slot];
            var tex = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    tex.SetPixel(x, 11 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            return slotIcons[slot] = Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
