using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Esc menu's Items card: every Part you own (CHIPs and COREs, spare or fitted) laid out on a teal velvet mat
    /// (<see cref="PauseMat.ClothStyle.Velvet"/>) that rolls out when it opens, each icon floating and bobbing gently.
    /// Hovering one (or moving to it with the arrows) lights it up: a soft gold glow fades in behind it, it grows a little
    /// and gets corner brackets, and the panel under the mat says what it is: name, slot, how many, who has it fitted,
    /// and what it does. Enter / F / Space / a click on an item opens a picker of the party: choosing a member fits it
    /// (their old part goes back to the spares), or takes it off if they already have it. Esc / Backspace closes, back
    /// to the Esc cards. The Parts | Cards tabs at the top (or Tab) switch to the card collection (<see cref="CardsScreen"/>),
    /// which counts as part of this screen (IsOpen, LastToggleFrame).
    /// </summary>
    public class ItemsScreen : MonoBehaviour
    {
        const int MaxColumns = 6;
        const float StepX = 230f, StepY = 230f, MatPadX = 90f, MatPadY = 60f, MatY = 40f;
        const float IconSize = 128f, Unroll = 0.4f;

        class Spot
        {
            public PartDefinition Part;
            public RectTransform Rect, Icon;
            public RectTransform Frame;
            public Image Glow;
            public float Phase, Lift, Light;
        }

        OverworldBuilder world;
        RectTransform root, mask, roll, itemsRoot, tip;
        Text tipName, tipSlot, tipBody;
        CanvasGroup itemsGroup;
        readonly List<Spot> spots = new List<Spot>();
        float matW, matH, reveal;
        int index, columns;
        int openedFrame;
        System.Action onClosed;
        RowMenu picker;                            // who to fit the selected item on
        CardsScreen cards;                         // the Cards tab
        public CardsScreen Cards { get { return cards; } }
        int lastToggle;

        static Sprite glowSprite;

        /// <summary>Open on either tab (Parts or Cards).</summary>
        public bool IsOpen { get { return gameObject.activeSelf || (cards != null && cards.IsOpen); } }

        /// <summary>The frame it (or the Cards tab) last opened or closed (so the Esc that closes it doesn't reopen the
        /// pause menu).</summary>
        public int LastToggleFrame
        {
            get { return Mathf.Max(lastToggle, cards != null ? cards.LastToggleFrame : 0); }
            private set { lastToggle = value; }
        }

        public static ItemsScreen Create(RectTransform parent, OverworldBuilder world)
        {
            var rt = UIFactory.CreateRect("ItemsScreen", parent).Fill();
            var screen = rt.gameObject.AddComponent<ItemsScreen>();
            screen.world = world;
            screen.root = rt;
            var dim = UIFactory.CreateImage(rt, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.9f), true);
            dim.rectTransform.Fill();
            InkStyle.Grain(rt, new Color(1f, 0.95f, 0.85f, 0.035f));
            var title = InkStyle.Title(rt, "Title", UIText.Get("items.title"), UIFactory.TextSize.Title, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(700f, 90f));
            var keys = UIFactory.CreateText(rt, "Keys",
                UIText.Get("items.keys"), UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleCenter);
            keys.supportRichText = true;
            // Same place and size as the Cards page's key line.
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1360f, 44f));
            keys.FitOneLine(1360f);
            screen.picker = RowMenu.Create(rt);
            Tabs(rt, true, null, screen.ShowCards);
            screen.cards = CardsScreen.Create(parent, world, screen);
            rt.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>The Parts | Cards tabs at the top of both pages; the current one is gold and does nothing.</summary>
        public static void Tabs(RectTransform root, bool partsActive, System.Action onParts, System.Action onCards)
        {
            Tab(root, UIText.Get("items.tab.parts"), partsActive, onParts, new Vector2(-120f, -30f));
            Tab(root, UIText.Get("items.tab.cards"), !partsActive, onCards, new Vector2(120f, -30f));
        }

        static void Tab(RectTransform root, string label, bool active, System.Action onClick, Vector2 at)
        {
            var top = new Vector2(0.5f, 1f);
            // The same height, top line and text size as the Cards page's Resonance / Kind buttons.
            var b = InkStyle.Button(root, "Tab_" + label, label, () => { if (!active && onClick != null) onClick(); }, false, UIFactory.TextSize.Large);
            b.GetComponent<RectTransform>().Place(top, top, at, new Vector2(220f, 112f));
            var text = b.GetComponentInChildren<Text>();
            text.color = active ? InkStyle.GoldBright : InkStyle.PaperDim;
            if (active)
            {
                var rule = InkStyle.Line(b.transform, "Underline", InkStyle.Gold);
                rule.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(140f, 3f));
            }
        }

        /// <summary>The Cards book straight from the Esc menu (its binder); closing it calls <paramref name="whenClosed"/>.</summary>
        public void OpenCards(System.Action whenClosed)
        {
            if (cards != null) cards.Open(whenClosed);
        }

        /// <summary>The Cards tab: this page hides and the collection opens (closing it still goes back to the Esc menu).</summary>
        void ShowCards()
        {
            var then = onClosed;
            onClosed = null;
            picker.Hide();
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(false);
            cards.Open(then);
        }

        public void Open(System.Action whenClosed)
        {
            if (gameObject.activeSelf) return;
            onClosed = whenClosed;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            Build();
            index = 0;
            reveal = 0f;
            Apply();
            Select(0);
        }

        public void Close()
        {
            if (!IsOpen) return;
            picker.Hide();
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(false);
            var then = onClosed;
            onClosed = null;
            if (then != null) then();
        }

        // ------------------------------------------------------------------ Layout

        /// <summary>Every Part you own, spare or fitted, in catalog order.</summary>
        List<PartDefinition> Owned()
        {
            var list = new List<PartDefinition>();
            if (WorldSession.Catalog == null) return list;
            foreach (var part in WorldSession.Catalog.parts)
                if (part != null && (WorldSession.CountOf(part) > 0 || FittedOn(part).Count > 0)) list.Add(part);
            return list;
        }

        /// <summary>The names of the members who have <paramref name="part"/> fitted.</summary>
        List<string> FittedOn(PartDefinition part)
        {
            var names = new List<string>();
            foreach (var member in world.Roster)
                if (member != null && WorldSession.PartsOf(member).Contains(part)) names.Add(member.DisplayName);
            return names;
        }

        void Build()
        {
            if (mask != null) Destroy(mask.gameObject);
            if (roll != null) Destroy(roll.gameObject);
            if (tip != null) Destroy(tip.gameObject);
            spots.Clear();

            var owned = Owned();
            int count = Mathf.Max(1, owned.Count);
            columns = Mathf.Min(MaxColumns, count);
            int rows = Mathf.CeilToInt(count / (float)columns);
            matW = columns * StepX + MatPadX * 2f;
            matH = rows * StepY + MatPadY;

            var center = new Vector2(0.5f, 0.5f);
            var midLeft = new Vector2(0f, 0.5f);
            mask = UIFactory.CreateRect("ItemsMat", root).Place(center, midLeft, new Vector2(-matW / 2f, MatY), new Vector2(0f, matH));
            mask.gameObject.AddComponent<RectMask2D>();
            var shadow = UIFactory.CreateImage(mask, "Shadow", new Color(0f, 0f, 0f, 0.35f));
            shadow.rectTransform.Place(midLeft, midLeft, new Vector2(10f, -12f), new Vector2(matW, matH));
            var cloth = new GameObject("Cloth", typeof(RectTransform)).AddComponent<RawImage>();
            cloth.rectTransform.SetParent(mask, false);
            cloth.texture = PauseMat.ClothTexture(Mathf.Max(24, Mathf.RoundToInt(PauseMat.ClothHeight * matW / matH)), PauseMat.ClothStyle.Velvet);
            cloth.raycastTarget = false;
            cloth.rectTransform.Place(midLeft, midLeft, Vector2.zero, new Vector2(matW, matH));

            itemsRoot = UIFactory.CreateRect("Items", mask).Place(midLeft, center, new Vector2(matW / 2f, 0f), new Vector2(matW, matH));
            itemsGroup = itemsRoot.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < count; i++)
            {
                int col = i % columns, row = i / columns;
                var at = new Vector2(-(columns - 1) * StepX / 2f + col * StepX, (rows - 1) * StepY / 2f - row * StepY - 20f);
                if (i < owned.Count) AddSpot(owned[i], at, i);
            }

            var rollImage = new GameObject("Roll", typeof(RectTransform)).AddComponent<RawImage>();
            rollImage.rectTransform.SetParent(root, false);
            rollImage.texture = PauseMat.RollTexture(PauseMat.ClothStyle.Velvet);
            rollImage.raycastTarget = false;
            roll = rollImage.rectTransform;
            roll.Place(center, center, new Vector2(-matW / 2f, MatY), new Vector2(64f, matH + 30f));

            BuildTip();
        }

        void AddSpot(PartDefinition part, Vector2 at, int i)
        {
            var center = new Vector2(0.5f, 0.5f);
            var spot = new Spot { Part = part, Phase = i * 0.9f };
            spot.Rect = UIFactory.CreateRect(part != null ? part.name : "Empty", itemsRoot).Place(center, center, at, new Vector2(StepX - 20f, StepY - 20f));
            var hit = spot.Rect.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            // The hover glow: a soft gold light behind the icon, faded in while it's selected.
            spot.Glow = UIFactory.CreateImage(spot.Rect, "Glow", new Color(1f, 0.85f, 0.45f, 0f));
            spot.Glow.sprite = Glow();
            spot.Glow.rectTransform.Place(center, center, Vector2.zero, new Vector2(IconSize, IconSize) * 1.9f);

            if (part != null)
            {
                var icon = UIFactory.CreateImage(spot.Rect, "Icon", Color.white);
                icon.sprite = CardIcons.Part(part.slot);
                icon.preserveAspect = true;
                spot.Icon = icon.rectTransform;
                spot.Icon.Place(center, center, Vector2.zero, new Vector2(IconSize, IconSize));
                int spare = WorldSession.CountOf(part);
                int total = spare + FittedOn(part).Count;
                if (total > 1)
                {
                    var n = UIFactory.CreateText(spot.Rect, "Count", "x" + total, 26, InkStyle.Paper, TextAnchor.LowerRight);
                    n.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                    n.rectTransform.Place(center, center, new Vector2(72f, -64f), new Vector2(80f, 40f));
                }
            }

            spot.Frame = UIFactory.CreateRect("Selected", spot.Rect).Fill();
            InkStyle.Frame(spot.Frame, InkStyle.GoldBright, 3f);
            spot.Frame.gameObject.SetActive(false);

            int captured = i;
            var trigger = spot.Rect.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Select(captured));
            trigger.triggers.Add(enter);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => { Select(captured); OpenPicker(); });
            trigger.triggers.Add(click);
            spots.Add(spot);
        }

        /// <summary>The panel under the mat that says what the selected item is.</summary>
        void BuildTip()
        {
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            const float w = 1300f, h = 190f;
            var panel = InkStyle.Panel(root, "ItemInfo", new Color(0.06f, 0.055f, 0.05f, 0.94f));
            tip = panel.rectTransform;
            tip.Place(center, new Vector2(0.5f, 1f), new Vector2(0f, MatY - matH / 2f - 24f), new Vector2(w, h));
            InkStyle.Frame(tip, InkStyle.Rule, 2f);
            tipName = InkStyle.Title(tip, "Name", "", 36, InkStyle.Paper, TextAnchor.UpperLeft);
            tipName.rectTransform.Place(tl, tl, new Vector2(24f, -16f), new Vector2(w - 360f, 50f));
            tipSlot = UIFactory.CreateText(tip, "Slot", "", 26, InkStyle.Gold, TextAnchor.UpperRight);
            tipSlot.supportRichText = true;
            tipSlot.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(520f, 40f));
            tipBody = UIFactory.CreateText(tip, "Body", "", 26, InkStyle.GoldBright, TextAnchor.UpperLeft);
            tipBody.rectTransform.Place(tl, tl, new Vector2(24f, -72f), new Vector2(w - 48f, h - 84f));
        }

        void Select(int i)
        {
            if (spots.Count == 0)
            {
                tipName.text = UIText.Get("items.none");
                tipSlot.text = "";
                tipBody.text = UIText.Get("items.none.text");
                return;
            }
            index = Mathf.Clamp(i, 0, spots.Count - 1);
            for (int k = 0; k < spots.Count; k++) spots[k].Frame.gameObject.SetActive(k == index);
            var part = spots[index].Part;
            tipName.text = part.DisplayName;
            var fitted = FittedOn(part);
            int spare = WorldSession.CountOf(part);
            tipSlot.text = UIText.Get(part.slot == PartSlot.Core ? "item.core" : "item.chip") + "   x" + (spare + fitted.Count)
                           + (fitted.Count > 0 ? "   <color=#ECE5D3>" + UIText.Get("items.fitted") + " " + string.Join(", ", fitted.ToArray()) + "</color>" : "");
            tipBody.text = part.description;
        }

        // ------------------------------------------------------------------ Equip

        /// <summary>
        /// "EQUIP <item>": one row per party member saying what they have in that slot now. Choosing a member fits the
        /// item (their old part goes back to the spares); a member who already has it takes it off instead. Members
        /// whose slot holds the bound Core, or with no spare left to give, are greyed out.
        /// </summary>
        void OpenPicker()
        {
            if (spots.Count == 0 || reveal < 1f) return;
            var part = spots[index].Part;
            if (part == null) return;
            string slotName = UIText.Get(part.slot == PartSlot.Core ? "item.core" : "item.chip");
            int spare = WorldSession.CountOf(part);
            var rows = new List<RowMenu.Row>();
            foreach (var member in world.Roster)
            {
                if (member == null) continue;
                var current = WorldSession.PartIn(member, part.slot);
                var m = member;
                // Everything at the row's full size (no smaller secondary text): the name, then what they have now.
                string now = "<color=#C9C2B4>" + slotName + ": " + (current != null ? current.DisplayName : UIText.Get("items.empty_slot")) + "</color>";
                var row = new RowMenu.Row { Label = member.DisplayName + "    " + now };
                if (current == part)
                {
                    row.Label = member.DisplayName + "    <color=#C2A061>" + UIText.Get("items.take_off") + "</color>";
                    row.Action = () => { WorldSession.Unequip(m, part.slot); Equipped(); };
                }
                else if (WorldSession.IsBound(current))
                    row.Enabled = false;   // the protagonist's own Core never comes out
                else if (spare > 0)
                    row.Action = () => { WorldSession.Equip(m, part); Equipped(); };
                else
                    row.Enabled = false;   // every one of this item is fitted on someone else
                rows.Add(row);
            }
            rows.Add(new RowMenu.Row { Label = UIText.Get("menu.back"), Action = picker.Hide });
            picker.Show(UIText.Get("items.equip", "item", part.DisplayName.ToUpperInvariant()), rows, picker.Hide);   // starts on the first usable row
        }

        /// <summary>After fitting or removing: save, close the picker and redraw the mat (counts and "Fitted" change).</summary>
        void Equipped()
        {
            SaveGame.Save();
            picker.Hide();
            int keep = index;
            Build();
            Apply();
            Select(keep);
        }

        // ------------------------------------------------------------------ Input + animation

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (reveal < 1f)
            {
                reveal = Mathf.Min(1f, reveal + dt / Unroll);
                Apply();
            }
            // Items float and bob; the selected one rises, grows and glows (the glow breathes slowly).
            float t = Time.unscaledTime, ease = 1f - Mathf.Exp(-12f * dt);
            for (int k = 0; k < spots.Count; k++)
            {
                var s = spots[k];
                if (s.Icon == null) continue;
                bool on = k == index;
                s.Lift = Mathf.Lerp(s.Lift, on ? 14f : 0f, ease);
                s.Light = Mathf.Lerp(s.Light, on ? 1f : 0f, ease);
                s.Icon.anchoredPosition = new Vector2(0f, s.Lift + Mathf.Sin(t * 2.2f + s.Phase) * 6f);
                s.Icon.localScale = Vector3.one * (1f + 0.15f * s.Light);
                var g = s.Glow.color;
                g.a = s.Light * (0.55f + 0.15f * Mathf.Sin(t * 2.6f));
                s.Glow.color = g;
                s.Glow.rectTransform.localScale = Vector3.one * (0.85f + 0.2f * s.Light);
            }

            if (Time.frameCount == openedFrame) return;
            if (picker.IsOpen || picker.ClosedFrame == Time.frameCount) return;   // the picker takes the keys
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F))
            {
                OpenPicker();
                return;
            }
            if (UIKeys.Pressed(KeyCode.Tab))
            {
                ShowCards();
                return;
            }
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Select(index - 1);
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Select(index + 1);
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Select(index - columns);
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Select(index + columns);
            if (UIKeys.Back || UIKeys.Pressed(KeyCode.Backspace)) Close();
        }

        /// <summary>The mat for how far it's unrolled (eased); the items and the info fade in as it finishes.</summary>
        void Apply()
        {
            float k = reveal * reveal * (3f - 2f * reveal);
            mask.sizeDelta = new Vector2(matW * k, matH);
            roll.anchoredPosition = new Vector2(-matW / 2f + matW * k, MatY);
            roll.sizeDelta = new Vector2(Mathf.Lerp(64f, 30f, k) * (k > 0.97f ? (1f - k) / 0.03f : 1f), matH + 30f);
            roll.gameObject.SetActive(k < 0.999f);
            itemsGroup.alpha = Mathf.Clamp01((reveal - 0.6f) / 0.4f);
            if (tip != null) tip.gameObject.SetActive(reveal >= 1f);
        }

        /// <summary>A soft round glow (white, tinted by the image colour), brightest in the middle.</summary>
        static Sprite Glow()
        {
            if (glowSprite != null) return glowSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return glowSprite;
        }
    }
}
