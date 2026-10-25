using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Esc party menu, Reverse: 1999-style (<see cref="InkStyle"/>): one detail page per member.
    /// Member tabs run along the top (the first is the leader). The member's idle animation stands large on the left;
    /// on the right are the name, element and level (with an XP hairline), HP, the stats as label / value strips (Part
    /// bonuses in gold), the two item slots (CHIP, CORE) and the cards: the deck and the Ult. Clicking a card opens it
    /// (<see cref="CardDetailPopup"/>: the full text, scroll for upgrades). Left/Right (or the tabs) switch member,
    /// Up/Down move the gold focus over the item slots and deck cards, Enter opens the item picker or the card,
    /// L makes the shown member the leader, Esc closes (an open card first). The game is paused while it's open; everything
    /// animates on unscaled time. Everything sits within x = -820..820 so it fits a 4:3 view.
    /// </summary>
    public class CharacterMenu : MonoBehaviour
    {
        const float Left = -820f, Right = 820f, Top = 600f;
        const float InfoX = -60f;                   // the info column's left edge
        const float SpriteScale = 12f;              // 64 px frames at 12x (whole pixels)
        const float RowCardScale = 1f;      // the pixel card at 5x, like the hand

        /// <summary>The item slots shown (weapon / armor slots are shelved for now).</summary>
        static readonly PartSlot[] Slots = { PartSlot.Chip, PartSlot.Core };

        static string SlotLabel(PartSlot slot)
        {
            return slot == PartSlot.Chip ? "CHIP" : "CORE";
        }

        OverworldBuilder world;
        CanvasGroup group;
        RectTransform page, tabs, detail, keyBar;
        RawImage glowBackdrop;
        Text keyText;
        List<CharacterDefinition> party = new List<CharacterDefinition>();
        int selected;
        float openT, detailT;

        // Focus order: the item slots, then the deck cards.
        int focus;
        readonly List<RectTransform> focusTargets = new List<RectTransform>();
        RectTransform focusFrame, picker;
        readonly List<System.Action> pickerActions = new List<System.Action>();
        readonly List<Image> pickerRows = new List<Image>();
        int pickerIndex;
        // Each item slot's description: hidden until that slot is selected (index = slot, null when empty).
        readonly List<GameObject> itemDescs = new List<GameObject>();

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

        /// <summary>Runs after the menu closes (the Escape card menu reopens itself this way).</summary>
        System.Action onClosed;

        public void Open()
        {
            Open(null);
        }

        /// <param name="whenClosed">Called once the menu closes, by X or Esc (e.g. back to the Escape menu).</param>
        public void Open(System.Action whenClosed)
        {
            if (IsOpen) return;
            onClosed = whenClosed;
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Time.timeScale = 0f;
            openT = 0f;
            party = world.Roster;   // the Units screen shows everyone who has joined
            selected = 0;
            focus = 0;
            Rebuild();
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
            var then = onClosed;
            onClosed = null;
            if (then != null) then();
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
            // Switching member: the detail slides in from the right and fades up.
            detailT = Mathf.Min(1f, detailT + dt / 0.22f);
            if (detail != null)
            {
                detail.anchoredPosition = new Vector2(Mathf.Lerp(40f, 0f, Ease(detailT)), 0f);
                detail.GetComponent<CanvasGroup>().alpha = Ease(detailT);
            }
            if (Time.frameCount != LastToggleFrame) HandleInput();
        }

        void HandleInput()
        {
            if (CardDetailPopup.IsOpen) return;   // the open card takes Esc / clicks first
            bool cancel = UIKeys.Back || UIKeys.Pressed(KeyCode.Backspace);
            bool confirm = UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.F) || UIKeys.Pressed(KeyCode.Space);
            bool left = UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A);
            bool right = UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D);
            bool up = UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W);
            bool down = UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S);

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
                Close();
                return;
            }
            if (party.Count == 0) return;
            if (left) Select(Mod(selected - 1, party.Count));
            if (right) Select(Mod(selected + 1, party.Count));
            if (UIKeys.Pressed(KeyCode.L)) MakeLeader();
            if (focusTargets.Count > 0)
            {
                if (up) SetFocus((focus - 1 + focusTargets.Count) % focusTargets.Count);
                if (down) SetFocus((focus + 1) % focusTargets.Count);
                if (confirm) Activate(focus);
            }
        }

        static int Mod(int a, int n)
        {
            return ((a % n) + n) % n;
        }

        void Select(int index)
        {
            if (index == selected) return;
            selected = index;
            focus = 0;
            Rebuild();
        }

        void MakeLeader()
        {
            if (Member == null || selected == 0) return;
            world.SetLeader(Member);
            party = world.Roster;   // the Units screen shows everyone who has joined
            selected = 0;
            Rebuild();
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

            // A soft glow in the shown member's color behind the sprite.
            glowBackdrop = new GameObject("Glow", typeof(RectTransform)).AddComponent<RawImage>();
            glowBackdrop.rectTransform.SetParent(root, false);
            glowBackdrop.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((Left + InfoX) / 2f, -40f), new Vector2(1100f, 1100f));
            glowBackdrop.texture = RadialTexture();
            glowBackdrop.raycastTarget = false;
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.04f));

            page = UIFactory.CreateRect("Content", root).Fill();
            var center = new Vector2(0.5f, 0.5f);
            var title = InkStyle.Title(page, "Title", "Units", 56, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(center, new Vector2(0f, 1f), new Vector2(Left, Top), new Vector2(260f, 70f));
            tabs = UIFactory.CreateRect("Tabs", page).Place(center, new Vector2(0f, 1f), new Vector2(Left + 230f, Top - 4f), new Vector2(Right - Left - 230f - 110f, 60f));

            // A visible X (top-right): closes the menu, back to wherever it was opened from.
            // On the root, after the page, so nothing on the page can ever cover it.
            var x = InkStyle.CloseButton(root, Close);
            x.GetComponent<RectTransform>().Place(center, new Vector2(1f, 1f), new Vector2(Right, Top + 6f), new Vector2(InkStyle.CloseSize, InkStyle.CloseSize));

            keyBar = UIFactory.CreateRect("Keys", page).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 44f));
            keyText = UIFactory.CreateText(keyBar, "Text", "", 28, InkStyle.PaperDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            keyText.supportRichText = true;
            keyText.rectTransform.Fill();
            const string gold = "<color=#C2A061>";
            keyText.text = gold + "← →</color>  Member      " + gold + "↑ ↓</color>  Select      " + gold + "Enter</color>  Open      "
                           + gold + "L</color>  Leader      " + gold + "Esc / P</color>  Close";
        }

        void Rebuild()
        {
            BuildTabs();
            BuildDetail();
            var c = AfflatusInfo.GlowFor(Member);
            c.a = 0.16f;
            glowBackdrop.color = c;
        }

        /// <summary>One tab per member: the name in serif, gold when shown, with a small "Leader" over the first.</summary>
        void BuildTabs()
        {
            UIFactory.ClearChildren(tabs);
            float x = 0f;
            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                bool on = i == selected;
                float w = Mathf.Max(150f, member.DisplayName.Length * 22f + 40f);
                var tab = UIFactory.CreateRect(member.DisplayName, tabs).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(w, 60f));
                var name = InkStyle.Title(tab, "Name", member.DisplayName, 40, on ? InkStyle.GoldBright : InkStyle.PaperDim, TextAnchor.LowerCenter);
                name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(w, 44f));
                if (on)
                {
                    var rule = InkStyle.Line(tab, "Underline", InkStyle.Gold);
                    rule.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(w - 30f, 2f));
                }
                if (i == 0)
                {
                    var lead = InkStyle.Label(tab, "Leader", "Leader", 24, InkStyle.Gold, TextAnchor.LowerCenter);
                    lead.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(w, 34f));
                }
                int index = i;
                Clickable(tab, () => Select(index));
                x += w + 20f;
            }
        }

        // ------------------------------------------------------------------ The detail page

        void BuildDetail()
        {
            ClosePicker();
            if (detail != null) Destroy(detail.gameObject);
            focusTargets.Clear();
            itemDescs.Clear();
            focusFrame = null;
            var member = Member;
            if (member == null) return;

            detail = UIFactory.CreateRect("Detail", page).Fill();
            detail.gameObject.AddComponent<CanvasGroup>();
            detailT = 0f;
            var progress = WorldSession.ProgressOf(member);

            BuildSprite(member);
            float y = Top - 110f;
            y = BuildHeader(member, progress, y);
            y = BuildHealth(member, y - 14f);
            y = BuildStats(member, progress, y - 22f);
            y = BuildItems(member, y - 26f);
            BuildCardRow(member, progress, y - 30f);
            SetFocus(Mathf.Clamp(focus, 0, Mathf.Max(0, focusTargets.Count - 1)));
        }

        /// <summary>The member's idle animation, large, standing on a soft shadow on the left.</summary>
        void BuildSprite(CharacterDefinition member)
        {
            var center = new Vector2(0.5f, 0.5f);
            float cx = (Left + InfoX) / 2f, ground = -420f;
            var shadow = UIFactory.CreateImage(detail, "Shadow", new Color(0f, 0f, 0f, 0.4f));
            shadow.sprite = CircleSprite();
            shadow.rectTransform.Place(center, center, new Vector2(cx, ground + 10f), new Vector2(300f, 50f));
            if (member.animations == null || !member.animations.Has(UISpriteAnimator.Idle)) return;
            float size = 64f * SpriteScale;
            var img = UIFactory.CreateImage(detail, "Sprite", Color.white);
            img.preserveAspect = true;
            // The frame's feet sit ~2 px above its bottom edge.
            img.rectTransform.Place(center, new Vector2(0.5f, 0f), new Vector2(cx, ground - 2f * SpriteScale), new Vector2(size, size));
            var anim = img.gameObject.AddComponent<UISpriteAnimator>();
            anim.UnscaledTime = true;
            anim.Init(member.animations, Facing.Right, img);
        }

        /// <summary>The glowing element orb, the name in big serif and the level with an XP hairline.</summary>
        float BuildHeader(CharacterDefinition member, MemberProgress progress, float y)
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            ElementBadge.Create(detail, WorldSession.ElementOf(member), 64f, center, new Vector2(InfoX + 40f, y - 46f));
            var name = InkStyle.Title(detail, "Name", member.DisplayName, 76, InkStyle.Paper, TextAnchor.MiddleLeft).BestFit(40, 76);
            name.rectTransform.Place(center, tl, new Vector2(InfoX + 96f, y + 4f), new Vector2(Right - InfoX - 330f, 92f));

            var lv = UIFactory.CreateText(detail, "Level", "Level  " + UIFactory.Size(48) + progress.Level + "</size>", 30, InkStyle.PaperDim, TextAnchor.LowerRight);
            lv.font = UIFactory.SerifFont;
            lv.supportRichText = true;
            lv.rectTransform.Place(center, new Vector2(1f, 1f), new Vector2(Right, y - 6f), new Vector2(240f, 70f));

            y -= 96f;
            var rule = InkStyle.Line(detail, "Rule", UIFactory.WithAlpha(InkStyle.Gold, 0.55f));
            rule.rectTransform.Place(center, tl, new Vector2(InfoX + 96f, y), new Vector2(Right - InfoX - 96f, 1f));
            // XP toward the next level: a gold hairline over the rule.
            float xp = progress.Level >= Leveling.MaxLevel ? 1f : progress.Xp / (float)Mathf.Max(1, Leveling.XpToNext(progress.Level));
            var xpLine = UIFactory.CreateImage(detail, "Xp", InkStyle.GoldBright);
            xpLine.rectTransform.Place(center, tl, new Vector2(InfoX + 96f, y + 1f), new Vector2((Right - InfoX - 96f) * Mathf.Clamp01(xp), 3f));
            return y - 3f;
        }

        /// <summary>HP: the label left and the numbers right on one line, over a thin bar.</summary>
        float BuildHealth(CharacterDefinition member, float y)
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            float x = InfoX + 96f, w = Right - x;
            int hp = WorldSession.HPOf(member), max = WorldSession.MaxHPOf(member);
            var label = InkStyle.Title(detail, "HP", "HP", 40, InkStyle.Paper, TextAnchor.MiddleLeft);
            label.rectTransform.Place(center, tl, new Vector2(x, y), new Vector2(200f, 50f));
            var value = InkStyle.Title(detail, "HPValue", hp + " / " + max, 36, InkStyle.Paper, TextAnchor.MiddleRight);
            value.rectTransform.Place(center, tl, new Vector2(Right - 400f, y), new Vector2(400f, 50f));
            y -= 54f;
            var track = UIFactory.CreateImage(detail, "HPTrack", new Color(1f, 1f, 1f, 0.1f));
            track.rectTransform.Place(center, tl, new Vector2(x, y), new Vector2(w, 6f));
            Fill(track.rectTransform, hp / (float)Mathf.Max(1, max), hp <= max / 4 ? new Color(0.92f, 0.32f, 0.3f) : new Color(0.45f, 0.82f, 0.55f));
            return y - 6f;
        }

        /// <summary>The stats as label / value strips in two columns; a Part's bonus shows as a small gold "+N".</summary>
        float BuildStats(CharacterDefinition member, MemberProgress progress, float y)
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            var with = UnitFor(member, progress, true);
            var without = UnitFor(member, progress, false);
            var stats = new[]
            {
                new KeyValuePair<string, StatType>("ATK", StatType.Attack),
                new KeyValuePair<string, StatType>("PHYS DEF", StatType.PhysicalDefense),
                new KeyValuePair<string, StatType>("CRIT", StatType.CritRate),
                new KeyValuePair<string, StatType>("SPEC DEF", StatType.SpecialDefense),
                new KeyValuePair<string, StatType>("CRIT DMG", StatType.CritDamage),
                new KeyValuePair<string, StatType>("MAX HP", StatType.MaxHP),
            };
            float x0 = InfoX + 96f, gap = 14f, cw = (Right - x0 - gap) / 2f, ch = 48f;
            for (int i = 0; i < stats.Length; i++)
            {
                var stat = stats[i].Value;
                bool percent = stat == StatType.CritRate || stat == StatType.CritDamage;
                float a = with.GetStat(stat), b = without.GetStat(stat);
                string value = percent ? Mathf.RoundToInt(a * 100f) + "%" : Mathf.RoundToInt(a).ToString();
                float bonus = percent ? (a - b) * 100f : a - b;
                if (Mathf.Abs(bonus) >= 0.5f)
                    value = UIFactory.Size(20) + "<color=#E6C784>" + (bonus > 0 ? "+" : "") + Mathf.RoundToInt(bonus) + (percent ? "%" : "") + "</color></size>  " + value;

                int col = i % 2, row = i / 2;
                var strip = UIFactory.CreateImage(detail, stats[i].Key, new Color(0.06f, 0.055f, 0.05f, 0.62f));
                strip.rectTransform.Place(center, tl, new Vector2(x0 + col * (cw + gap), y - row * (ch + 8f)), new Vector2(cw, ch));
                var l = UIFactory.CreateText(strip.transform, "Label", stats[i].Key, 30, InkStyle.PaperDim, TextAnchor.MiddleLeft);
                l.font = UIFactory.SerifFont;
                l.rectTransform.Fill();
                l.rectTransform.offsetMin = new Vector2(18f, 0f);
                var v = UIFactory.CreateText(strip.transform, "Value", value, 34, InkStyle.Paper, TextAnchor.MiddleRight, FontStyle.Bold);
                v.supportRichText = true;
                v.rectTransform.Fill();
                v.rectTransform.offsetMax = new Vector2(-18f, 0f);
            }
            return y - 3f * (ch + 8f);
        }

        /// <summary>The two item slots, side by side: icon, slot name, the fitted Part and what it does (click to change).</summary>
        float BuildItems(CharacterDefinition member, float y)
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            float x0 = InfoX + 96f, gap = 14f, w = (Right - x0 - gap) / 2f, h = 110f;
            var head = InkStyle.Label(detail, "ItemsLabel", "Items", 30, InkStyle.Gold, TextAnchor.LowerLeft);
            head.rectTransform.Place(center, tl, new Vector2(x0, y), new Vector2(300f, 40f));
            y -= 46f;
            for (int s = 0; s < Slots.Length; s++)
            {
                var slot = Slots[s];
                var part = WorldSession.PartIn(member, slot);
                var row = UIFactory.CreateImage(detail, SlotLabel(slot), new Color(0.06f, 0.055f, 0.05f, 0.62f)).rectTransform;
                row.Place(center, tl, new Vector2(x0 + s * (w + gap), y), new Vector2(w, h));
                var icon = UIFactory.CreateImage(row, "Icon", part != null ? Color.white : new Color(1f, 1f, 1f, 0.3f));
                icon.sprite = SlotIcon((int)slot);
                icon.preserveAspect = true;
                icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(64f, 64f));
                var slotText = InkStyle.Label(row, "Slot", SlotLabel(slot), 26, InkStyle.Gold, TextAnchor.UpperLeft);
                slotText.rectTransform.Place(tl, tl, new Vector2(96f, -8f), new Vector2(160f, 34f));
                var partName = InkStyle.Title(row, "Name", part != null ? part.DisplayName : "Empty", 36,
                    part != null ? InkStyle.Paper : InkStyle.PaperDim, TextAnchor.UpperLeft).BestFit(18, 36);
                partName.rectTransform.Place(tl, tl, new Vector2(96f, -42f), new Vector2(w - 112f, 38f));
                GameObject descObject = null;
                if (part != null)
                {
                    // Only shown while this slot is selected (SetFocus): an ink tooltip just above the item slots, as
                    // wide as both, so the page stays uncluttered.
                    var tip = InkStyle.Panel(row, "DescTip", new Color(0.05f, 0.045f, 0.04f, 0.97f));
                    tip.rectTransform.Place(tl, new Vector2(0f, 0f), new Vector2(-s * (w + gap), 10f), new Vector2(w * 2f + gap, 76f));
                    InkStyle.Frame(tip.rectTransform, InkStyle.Gold, 2f);
                    var desc = UIFactory.CreateText(tip.rectTransform, "Desc", part.description, 24, InkStyle.GoldBright, TextAnchor.MiddleLeft);
                    desc.rectTransform.Fill(16f);
                    descObject = tip.gameObject;
                    descObject.SetActive(false);
                }
                itemDescs.Add(descObject);
                int index = s;
                Clickable(row, () => { SetFocus(index); Activate(index); });
                focusTargets.Add(row);
            }
            return y - h;
        }

        /// <summary>
        /// The deck and the Ult as the real card faces in a hairline bracket. Click a card to open it (full text, scroll
        /// for upgrades); deck cards are also focus targets (Enter swaps one for another card).
        /// </summary>
        void BuildCardRow(CharacterDefinition member, MemberProgress progress, float y)
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            var unit = UnitFor(member, progress, true);
            var deck = WorldSession.DeckOf(member);
            var groups = new List<KeyValuePair<string, List<CardDefinition>>>
            {
                new KeyValuePair<string, List<CardDefinition>>("Deck", deck.Count > 2 ? deck.GetRange(0, 2) : deck),
                new KeyValuePair<string, List<CardDefinition>>("Ult", WorldSession.UltimateOf(member) != null ? new List<CardDefinition> { WorldSession.UltimateOf(member) } : new List<CardDefinition>()),
            };
            groups.RemoveAll(g => g.Value.Count == 0 && g.Key != "Deck");

            float x0 = InfoX + 96f;
            var cardSize = CardView.Size * RowCardScale;
            const float cardGap = 16f, groupGap = 40f, pad = 18f, labelH = 26f;
            var bracket = UIFactory.CreateRect("Cards", detail).Place(center, tl, new Vector2(x0, y), new Vector2(Right - x0, cardSize.y + labelH + pad * 2f));
            InkStyle.Frame(bracket, UIFactory.WithAlpha(InkStyle.Gold, 0.4f), 1f);

            float x = pad;
            foreach (var g in groups)
            {
                float groupW = g.Value.Count * cardSize.x + Mathf.Max(0, g.Value.Count - 1) * cardGap;
                var label = InkStyle.Label(bracket, g.Key, g.Key, 28, InkStyle.Gold, TextAnchor.LowerLeft);
                label.rectTransform.Place(tl, tl, new Vector2(x, -pad + 6f), new Vector2(Mathf.Max(groupW, 120f), labelH));
                for (int i = 0; i < g.Value.Count; i++)
                {
                    var card = g.Value[i];
                    var holder = UIFactory.CreateRect("Slot", bracket).Place(tl, tl, new Vector2(x, -pad - labelH), cardSize);
                    var ability = AbilityCard.Create(holder, unit, card, 0, RowCardScale, true);   // icon only: the text is in the popup
                    var rt = (RectTransform)ability.transform;
                    rt.Fill();
                    var captured = card;
                    if (g.Key == "Deck")
                    {
                        int slot = i;
                        Clickable(rt, () => { SetFocus(Slots.Length + slot); OpenCard(captured, rt); });
                        focusTargets.Add(rt);
                    }
                    else Clickable(rt, () => OpenCard(captured, rt));
                    x += cardSize.x + cardGap;
                }
                x += groupGap - cardGap;
            }
        }

        static BattleUnit UnitFor(CharacterDefinition member, MemberProgress progress, bool withParts)
        {
            var unit = new BattleUnit(member, Team.Player);
            var loadout = WorldSession.LoadoutsFor(new[] { member })[member];   // the active resonance's cards, Ult and colour
            if (!withParts) loadout.Parts = new List<PartDefinition>();
            unit.ApplyLoadout(loadout);
            unit.SetLevel(progress.Level);
            return unit;
        }

        static void Fill(RectTransform track, float amount, Color color)
        {
            var fill = UIFactory.CreateImage(track, "Fill", color);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>Opens a card's details (the flip-over popup; scroll for upgrades).</summary>
        void OpenCard(CardDefinition card, RectTransform from)
        {
            var member = Member;
            if (member == null) return;
            CardDetailPopup.Open((RectTransform)transform, UnitFor(member, WorldSession.ProgressOf(member), true), card, 1, from);
        }

        // ------------------------------------------------------------------ Focus, pickers and changes

        /// <summary>Moves the gold focus frame (item slots, then deck cards).</summary>
        void SetFocus(int index)
        {
            if (focusTargets.Count == 0) return;
            focus = Mathf.Clamp(index, 0, focusTargets.Count - 1);
            if (focusFrame != null) Destroy(focusFrame.gameObject);
            focusFrame = UIFactory.CreateRect("Focus", focusTargets[focus]).Fill(-6f);
            InkStyle.Frame(focusFrame, InkStyle.GoldBright, 3f);
            for (int i = 0; i < itemDescs.Count; i++)
                if (itemDescs[i] != null) itemDescs[i].SetActive(i == focus);
        }

        void Activate(int index)
        {
            var member = Member;
            if (member == null) return;
            if (index < Slots.Length) OpenPartPicker(member, Slots[index]);
            else
            {
                // A deck card: open it (swapping deck cards is shelved for now, along with spare cards).
                var deck = WorldSession.DeckOf(member);
                int slot = index - Slots.Length;
                if (slot < deck.Count) OpenCard(deck[slot], focusTargets[index]);
            }
        }

        /// <summary>One row of the item picker: a name, an optional description under it, and what choosing it does.</summary>
        class PickerOption
        {
            public string Title, Detail;
            public System.Action Action;

            public PickerOption(string title, string detail, System.Action action)
            {
                Title = title;
                Detail = detail;
                Action = action;
            }
        }

        void OpenPartPicker(CharacterDefinition member, PartSlot slot)
        {
            var options = new List<PickerOption>();
            var fitted = WorldSession.PartIn(member, slot);
            if (WorldSession.IsBound(fitted))
            {
                // Your own Core: it holds the Soul Card, nothing replaces it.
                options.Add(new PickerOption(fitted.DisplayName, fitted.description, ClosePicker));
                OpenPicker(SlotLabel(slot), options);
                return;
            }
            if (WorldSession.Catalog != null)
                foreach (var part in WorldSession.Catalog.parts)
                {
                    if (part == null || part.slot != slot) continue;
                    int count = WorldSession.CountOf(part);
                    if (count <= 0) continue;
                    var captured = part;
                    options.Add(new PickerOption(part.DisplayName + "  ×" + count, part.description, () =>
                    {
                        WorldSession.Equip(member, captured);
                        SaveGame.Save();
                        BuildDetail();
                    }));
                }
            if (WorldSession.PartIn(member, slot) != null)
                options.Add(new PickerOption("Remove", null, () =>
                {
                    WorldSession.Unequip(member, slot);
                    SaveGame.Save();
                    BuildDetail();
                }));
            if (options.Count == 0) options.Add(new PickerOption("No spare items", null, ClosePicker));
            OpenPicker(SlotLabel(slot), options);
        }

        /// <summary>A small modal list over the page: Up/Down + Enter or click; Esc or a click outside closes it.</summary>
        /// <summary>The item picker: a big box over the page, one row per option with the name large and its
        /// description under it (wrapping, never shrunk), so it reads easily at 4:3.</summary>
        void OpenPicker(string title, List<PickerOption> options)
        {
            ClosePicker();
            const float titleH = 52f, detailLine = 42f, gap = 12f, width = 1180f, pad = 22f;
            const int detailChars = 52;   // about how many detail characters fit on one line at this width
            picker = UIFactory.CreateImage(page, "Picker", new Color(0f, 0f, 0f, 0.6f), true).rectTransform;
            picker.Fill();
            Clickable(picker, ClosePicker);
            var heights = new List<float>();
            float height = 110f;
            foreach (var o in options)
            {
                int lines = string.IsNullOrEmpty(o.Detail) ? 0 : Mathf.CeilToInt(o.Detail.Length / (float)detailChars);
                float h = pad * 2f + titleH + lines * detailLine;
                heights.Add(h);
                height += h + gap;
            }
            var box = InkStyle.Panel(picker, "Box", new Color(0.09f, 0.085f, 0.08f, 0.98f), true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            InkStyle.HeroFrame(box, InkStyle.Gold, 8f);

            var tl = new Vector2(0f, 1f);
            var head = InkStyle.Label(box, "Title", title, 40, InkStyle.Gold, TextAnchor.MiddleLeft);
            head.rectTransform.Place(tl, tl, new Vector2(34f, -24f), new Vector2(width - 68f, 60f));
            pickerActions.Clear();
            pickerRows.Clear();
            float y = -100f;
            for (int i = 0; i < options.Count; i++)
            {
                var row = InkStyle.Panel(box, "Option" + i, new Color(1f, 1f, 1f, 0.05f), true);
                row.rectTransform.Place(tl, tl, new Vector2(28f, y), new Vector2(width - 56f, heights[i]));
                y -= heights[i] + gap;
                var name = UIFactory.CreateText(row.transform, "Name", options[i].Title, 34, InkStyle.Paper, TextAnchor.UpperLeft);
                name.rectTransform.Place(tl, tl, new Vector2(pad, -pad), new Vector2(width - 56f - pad * 2f, titleH));
                if (!string.IsNullOrEmpty(options[i].Detail))
                {
                    var detail = UIFactory.CreateText(row.transform, "Detail", options[i].Detail, 26, new Color(0.9f, 0.78f, 0.52f), TextAnchor.UpperLeft);
                    detail.rectTransform.Place(tl, tl, new Vector2(pad, -pad - titleH), new Vector2(width - 56f - pad * 2f, heights[i] - pad * 2f - titleH));
                }
                var action = options[i].Action;
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

        static Sprite circle;
        static Texture2D radial;
        static readonly Sprite[] slotIcons = new Sprite[4];

        static Sprite CircleSprite()
        {
            if (circle == null) circle = Disc(0f);
            return circle;
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

        /// <summary>Icons for the Part slots: CHIP and CORE come from the icon atlas (Raven pack); otherwise tiny pixel
        /// icons drawn in code: blade, shield, chip, core (12 px).</summary>
        static Sprite SlotIcon(int slot)
        {
            if (slot == (int)PartSlot.Chip || slot == (int)PartSlot.Core)
            {
                var fromAtlas = CardIcons.Part((PartSlot)slot);
                if (fromAtlas != null) return fromAtlas;
            }
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
