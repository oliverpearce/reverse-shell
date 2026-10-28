using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// ITEMS > CARDS
    /// The card collection (Esc → Items → Cards), laid out in a book (<see cref="CardBook"/>): the character kneels and
    /// opens a book (the "read" pose), the book UI rises from it and its cover swings open. Each page holds 3 x 2 cards
    /// (12 per spread); the wheel or the arrows turn the pages with a curl. Hovering a card picks it up and leans it in 3D
    /// (<see cref="CardLift"/>).
    ///   ability cards   with their copies (×N) and the rank they start battles at; their Sun / Moon / Star if bound
    ///   Sun, Moon, Star the ones you've found (each can be bound onto an ability card)
    ///   Soul cards      each unit's Ult card with its Soul level
    /// Two buttons filter the strip: Resonance (All, then each element) and Kind (All, Attack, Debuff, Buff, Heal,
    /// Counter). The panel under it says what the selected card does for you, with Bind for ability cards. Click the
    /// selected card (or Enter) to zoom in and tilt it. Open Pack opens a card pack (<see cref="PackOpening"/>). Tab or
    /// the Parts tab goes back to the Parts page; Esc closes.
    /// The same page has two more modes, from the title screen:
    ///   Session   Card Pack mode: unlimited packs into a throwaway collection (CardCollection.BeginSession); the strip
    ///             shows this session's pulls and a stats line; leaving warns that they're lost and names the rarest
    ///   Index     every card players can get (every character's abilities and Soul cards, Sun / Moon / Star) with
    ///             what each does; no counts or buttons
    /// </summary>
    public class CardsScreen : MonoBehaviour
    {
        public enum Mode { Collection, Session, Index }

        enum EntryKind { Ability, Arcana, Soul }

        class Entry
        {
            public EntryKind Kind;
            public CardDefinition Card;            // Ability / Soul
            public CharacterDefinition Owner;      // Ability / Soul
            public Afflatus Element;
            public CardBinding Arcana;             // Arcana
            public RectTransform Rect;
            public CardLift Lift;
        }

        // The book's layout: 3 x 2 cards per page, at this scale (a hand card is 1).
        const int Columns = 3, Rows = 2, PerPage = Columns * Rows, PerSpread = PerPage * 2;
        const float FaceScale = 0.66f, CellX = 182f, CellY = 272f, BookScale = 1.1f, BookY = 10f;
        /// <summary>Space kept between any two pieces of text or UI on this page.</summary>
        const float Margin = 16f;
        /// <summary>The counters line under the title: as wide as it can be without reaching the Resonance button.</summary>
        const float CountersWidth = 690f;
        static readonly CardKind?[] Kinds = { null, CardKind.Attack, CardKind.Debuff, CardKind.Buff, CardKind.Heal, CardKind.Counter };

        OverworldBuilder world;
        ItemsScreen parts;
        Mode mode;
        static readonly List<CardsScreen> instances = new List<CardsScreen>();

        /// <summary>Any Cards page is open (the title screen waits while one is).</summary>
        public static bool AnyOpen { get { return instances.Exists(s => s != null && s.IsOpen); } }

        /// <summary>Whose cards: the joined members for the collection, every playable character for the title modes.</summary>
        List<CharacterDefinition> Roster { get { return mode == Mode.Collection ? world.Roster : world.AllCharacters; } }
        RectTransform root, bookHolder;
        CardBook book;
        bool closing;
        Text info, counters, resonanceLabel, kindLabel, packLabel;
        RectTransform infoRunes;   // the selected card's name in runes, over the info panel's first line (no language chip)
        Button bindButton, packButton;
        RowMenu picker;
        readonly List<Entry> all = new List<Entry>();
        readonly List<Entry> shown = new List<Entry>();
        int index, openedFrame, resonanceFilter, kindFilter;
        System.Action onClosed;

        public bool IsOpen { get { return gameObject.activeSelf; } }
        public int LastToggleFrame { get; private set; }

        public static CardsScreen Create(RectTransform parent, OverworldBuilder world, ItemsScreen parts, Mode mode = Mode.Collection)
        {
            var rt = UIFactory.CreateRect("CardsScreen_" + mode, parent).Fill();
            var screen = rt.gameObject.AddComponent<CardsScreen>();
            screen.world = world;
            screen.parts = parts;
            screen.mode = mode;
            instances.Add(screen);
            screen.root = rt;
            screen.Build();
            rt.gameObject.SetActive(false);
            return screen;
        }

        void Build()
        {
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.9f), true);
            dim.rectTransform.Fill();
            InkStyle.Grain(root, new Color(1f, 0.95f, 0.85f, 0.035f));
            var topLeft = new Vector2(0f, 1f);
            string titleKey = mode == Mode.Session ? "cards.title.session" : mode == Mode.Index ? "cards.title.index" : "cards.title";
            var title = InkStyle.Title(root, "Title", UIText.Get(titleKey), UIFactory.TextSize.Title, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(topLeft, topLeft, new Vector2(60f, -40f), new Vector2(700f, 90f));
            if (mode == Mode.Collection) ItemsScreen.Tabs(root, false, SwitchToParts, null);

            // Top right: the two sort filters, each a caption over its current choice in big gold; a click moves to the next.
            resonanceLabel = TopButton("Resonance", -370f, () => CycleFilter(ref resonanceFilter, ResonanceChoices().Count));
            kindLabel = TopButton("Kind", -50f, () => CycleFilter(ref kindFilter, Kinds.Length));

            bookHolder = UIFactory.CreateRect("Book", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, BookY), CardBook.Size);
            bookHolder.localScale = Vector3.one * BookScale;
            book = CardBook.Create(bookHolder);
            book.BuildPage = BuildPage;

            // Bottom, laid out to fit a 4:3 screen (about 1400 wide) with Margin between everything: the key hints on
            // one line at the very bottom; above them the selected card's panel (two lines) and Open Pack beside it.
            var bottom = new Vector2(0.5f, 0f);
            const float keysY = 14f, keysH = 44f, panelY = keysY + keysH + Margin, panelH = 112f;
            var panel = InkStyle.Panel(root, "Info", InkStyle.InkPanel);
            panel.rectTransform.Place(bottom, bottom, new Vector2(-170f, panelY), new Vector2(1020f, panelH));
            InkStyle.Frame(panel.rectTransform, InkStyle.Rule, 2f);
            info = UIFactory.CreateText(panel.rectTransform, "Text", "", UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.UpperLeft)
                .BestFit(UIFactory.TextSize.Small, UIFactory.TextSize.Large);   // shrinks to fit the panel, never below Small
            info.supportRichText = true;
            info.lineSpacing = 1.15f;
            info.rectTransform.Fill(Margin + 4f);
            infoRunes = UIFactory.CreateRect("NameRunes", panel.rectTransform).Place(new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin + 4f, -(Margin + 2f)), new Vector2(700f, 36f));
            // Top: the counters on one line under the title, clear of it and of the book.
            counters = UIFactory.CreateText(root, "Counters", "", UIFactory.TextSize.Medium, InkStyle.PaperDim, TextAnchor.MiddleLeft);
            counters.supportRichText = true;
            counters.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -136f), new Vector2(CountersWidth, 32f));
            packButton = HudView.GoStyleButton(root, "OpenPack", "", OpenPack);
            packButton.GetComponent<RectTransform>().Place(bottom, bottom, new Vector2(520f, panelY), new Vector2(300f, panelH));
            packLabel = packButton.GetComponentInChildren<Text>();
            packLabel.fontSize = 40;
            packButton.gameObject.SetActive(mode != Mode.Index);
            bindButton = InkStyle.Button(root, "Bind", UIText.Get("cards.bind"), OpenBindPicker, false, 40);
            bindButton.gameObject.SetActive(false);   // binding is off for now (PackRates.bindingOn)

            string keysKey = mode == Mode.Session ? "cards.keys.session" : mode == Mode.Index ? "cards.keys.index" : "cards.keys";
            var keys = UIFactory.CreateText(root, "Keys", UIText.Get(keysKey), UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleCenter);
            keys.supportRichText = true;
            keys.rectTransform.Place(bottom, bottom, new Vector2(0f, keysY), new Vector2(1360f, keysH));
            keys.FitOneLine(1360f);   // one line, never under the panel
            picker = RowMenu.Create(root);
        }

        /// <summary>A top-bar button: a two-line label (caption, then the value in gold), easy to read at a glance.</summary>
        Text TopButton(string name, float x, UnityEngine.Events.UnityAction onClick)
        {
            var topRight = new Vector2(1f, 1f);
            var b = InkStyle.Button(root, name, "", onClick, false, UIFactory.TextSize.Large);
            b.GetComponent<RectTransform>().Place(topRight, topRight, new Vector2(x, -30f), new Vector2(300f, 112f));
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.lineSpacing = 1.1f;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        public void Open(System.Action whenClosed)
        {
            if (mode == Mode.Session && !CardCollection.InSession) CardCollection.BeginSession();
            if (mode == Mode.Index) CardCollection.IndexOpen = true;
            onClosed = whenClosed;
            closing = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            opening = true;
            Rebuild(0);
            // The collection: the character opens a book and the book UI rises out of it. The title's modes: the
            // book comes up from the bottom of the screen.
            var sprite = mode == Mode.Collection && world != null ? world.PlayerSprite : null;
            if (sprite != null) sprite.PlayPose(sprite.HasClip("read") ? "read" : "sit");
            StartCoroutine(OpenBook(BookOrigin(sprite)));
        }

        bool opening;
        int lastHovered = -1;

        /// <summary>Opens the book; the selection shows only once it's fully open: on the card under the mouse, else the
        /// keyboard's.</summary>
        System.Collections.IEnumerator OpenBook(Vector2 from)
        {
            yield return book.Open(from);
            opening = false;
            int hovered = HoveredIndex();
            lastHovered = hovered;
            Select(hovered >= 0 ? hovered : index);
        }

        /// <summary>The card the mouse is over (on the open pages), or -1.</summary>
        int HoveredIndex()
        {
            for (int i = 0; i < shown.Count; i++)
                if (shown[i].Lift != null && shown[i].Lift.IsHovered) return i;
            return -1;
        }

        /// <summary>Where the closed book comes from / goes back to, in the book holder's space: the character's hands, or
        /// below the screen.</summary>
        Vector2 BookOrigin(CharacterSprite sprite)
        {
            var cam = Camera.main;
            if (sprite != null && cam != null)
            {
                var screen = cam.WorldToScreenPoint(sprite.transform.position + Vector3.up * 0.5f);
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(bookHolder, screen, null, out local)) return local;
            }
            return new Vector2(0f, -900f);
        }

        /// <summary>The Parts tab: the book closes into the character's hands (back to the kneel), then the Parts page.</summary>
        void SwitchToParts()
        {
            if (closing) return;
            var then = onClosed;
            onClosed = () => parts.Open(then);
            Close();
        }

        /// <summary>Closes without going back (switching to the Parts tab).</summary>
        void Hide()
        {
            if (mode == Mode.Index) CardCollection.IndexOpen = false;
            picker.Hide();
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        /// <summary>Closes the book (the cover swings shut and it sinks back into the character's hands, or off the
        /// bottom), then goes back.</summary>
        public void Close()
        {
            if (!IsOpen || closing) return;
            closing = true;
            StartCoroutine(CloseRoutine());
        }

        System.Collections.IEnumerator CloseRoutine()
        {
            var sprite = mode == Mode.Collection && world != null ? world.PlayerSprite : null;
            yield return book.Close(BookOrigin(sprite));
            if (sprite != null) sprite.PlayPose("sit", true);   // back to the Esc menu's kneel
            closing = false;
            Hide();
            var then = onClosed;
            onClosed = null;
            if (then != null) then();
        }

        // ITEMS > CARDS > WHAT'S IN THE COLLECTION

        void Gather()
        {
            all.Clear();
            bool session = mode == Mode.Session, index = mode == Mode.Index;
            foreach (var member in Roster)
            {
                if (member == null) continue;
                foreach (var card in CardCollection.AbilitiesOf(member))
                    if (!all.Exists(e => e.Card == card) && (!session || CardCollection.Copies(card) > 0))
                        all.Add(new Entry { Kind = EntryKind.Ability, Card = card, Owner = member, Element = ElementOf(member, card) });
            }
            foreach (var kind in new[] { CardBinding.Sun, CardBinding.Moon, CardBinding.Star })
                if (index || CardCollection.Found(kind) > 0) all.Add(new Entry { Kind = EntryKind.Arcana, Arcana = kind });
            foreach (var member in Roster)
            {
                if (member == null || member.ritual == null) continue;
                if (index)
                {
                    // Every Soul card: the character's own and each resonance's Ult.
                    all.Add(new Entry { Kind = EntryKind.Soul, Card = member.ritual, Owner = member, Element = member.afflatus });
                    foreach (var kit in member.resonances)
                        if (kit != null && kit.ultimate != null && !all.Exists(e => e.Card == kit.ultimate))
                            all.Add(new Entry { Kind = EntryKind.Soul, Card = kit.ultimate, Owner = member, Element = kit.element });
                }
                else if (!session || CardCollection.SoulLevel(member) > 0)
                    all.Add(new Entry { Kind = EntryKind.Soul, Card = WorldSession.UltimateOf(member) ?? member.ritual, Owner = member, Element = WorldSession.ElementOf(member) });
            }
        }

        /// <summary>A card's resonance: the kit it belongs to, else its owner's own element.</summary>
        static Afflatus ElementOf(CharacterDefinition member, CardDefinition card)
        {
            foreach (var kit in member.resonances)
                if (kit != null && kit.cards.Contains(card)) return kit.element;
            return member.afflatus;
        }

        List<Afflatus?> ResonanceChoices()
        {
            var list = new List<Afflatus?> { null };
            foreach (var e in all)
                if (e.Kind != EntryKind.Arcana && !list.Contains(e.Element)) list.Add(e.Element);
            return list;
        }

        bool Passes(Entry e)
        {
            var element = ResonanceChoices()[Mathf.Clamp(resonanceFilter, 0, ResonanceChoices().Count - 1)];
            var kind = Kinds[kindFilter];
            if (element.HasValue && (e.Kind == EntryKind.Arcana || e.Element != element.Value)) return false;
            if (kind.HasValue && (e.Kind != EntryKind.Ability || e.Card.kind != kind.Value)) return false;
            return true;
        }

        void CycleFilter(ref int filter, int count)
        {
            filter = (filter + 1) % Mathf.Max(1, count);
            Rebuild(0);
        }

        // ITEMS > CARDS > LAYOUT

        void Rebuild(int select)
        {
            Gather();
            if (resonanceFilter >= ResonanceChoices().Count) resonanceFilter = 0;
            shown.Clear();
            foreach (var e in all) if (Passes(e)) shown.Add(e);
            // Abilities by resonance, then kind, then name; Sun / Moon / Star and Soul cards after them.
            shown.Sort((a, b) =>
            {
                if (a.Kind != b.Kind) return a.Kind.CompareTo(b.Kind);
                if (a.Element != b.Element) return a.Element.CompareTo(b.Element);
                if (a.Kind == EntryKind.Ability && a.Card.kind != b.Card.kind) return a.Card.kind.CompareTo(b.Card.kind);
                return a.Kind == EntryKind.Arcana ? a.Arcana.CompareTo(b.Arcana) : string.CompareOrdinal(a.Card.DisplayName, b.Card.DisplayName);
            });

            book.PageCount = Mathf.Max(2, Mathf.CeilToInt(shown.Count / (float)PerPage));
            index = Mathf.Clamp(select, 0, Mathf.Max(0, shown.Count - 1));
            book.ShowSpread(index / PerSpread);
            var element = ResonanceChoices()[resonanceFilter];
            resonanceLabel.text = UIText.Get("cards.filter.resonance", "value",
                element.HasValue ? AfflatusInfo.Display(element.Value) : UIText.Get("cards.filter.all"));
            var kind = Kinds[kindFilter];
            kindLabel.text = UIText.Get("cards.filter.kind", "value", kind.HasValue ? UnitView.KindLabel(kind.Value) : UIText.Get("cards.filter.all"));
            Refresh();
        }

        /// <summary>One page of the book: up to 3 x 2 cards from the filtered list.</summary>
        void BuildPage(int page, RectTransform container)
        {
            for (int slot = 0; slot < PerPage; slot++)
            {
                int i = page * PerPage + slot;
                if (i >= shown.Count) break;
                int col = slot % Columns, row = slot / Columns;
                var at = new Vector2((col - (Columns - 1) / 2f) * CellX, ((Rows - 1) / 2f - row) * CellY + 22f);
                BuildCard(shown[i], i, container, at);
            }
        }

        void BuildCard(Entry e, int i, RectTransform container, Vector2 at)
        {
            var rt = UIFactory.CreateRect("Card" + i, container).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, CardView.Size * FaceScale);
            e.Rect = rt;
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var face = UIFactory.CreateRect("Face", rt).Fill();
            if (e.Kind == EntryKind.Arcana) ArcanaCards.BuildFace(face, e.Arcana, FaceScale);
            else CardView.BuildFace(face, Instance(e), FaceScale);
            foreach (var g in face.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            // Hover picks it up and leans it; a soft glint by rarity.
            e.Lift = CardLift.Add(rt, face);
            e.Lift.Selected = i == index && !opening;   // no selection while the book opens
            int hovered = i;
            e.Lift.Hovered = () => { if (!book.Busy) Select(hovered); };
            var holo = CardHolo.Add(face, face.GetComponent<CardTilt>(),
                e.Kind == EntryKind.Soul ? CardHolo.Kind.Soul : e.Kind == EntryKind.Arcana ? CardHolo.Kind.Rare : CardHolo.Kind.Common);
            holo.Strength = 0.35f;

            // Under the card: copies (×N) for abilities, found for Sun / Moon / Star, the Soul level for Soul cards; in the
            // session how many were pulled; in the index whose card it is.
            string under;
            if (mode == Mode.Index)
                under = e.Kind == EntryKind.Arcana ? UIText.Get("cards.index.rare") : e.Owner.DisplayName;
            else if (mode == Mode.Session)
                under = UIText.Get("cards.copies", "n", (e.Kind == EntryKind.Ability ? CardCollection.Copies(e.Card)
                    : e.Kind == EntryKind.Arcana ? CardCollection.Found(e.Arcana) : CardCollection.SoulLevel(e.Owner)).ToString());
            else
                under = e.Kind == EntryKind.Ability ? UIText.Get("cards.copies", "n", CardCollection.Copies(e.Card).ToString())
                    : e.Kind == EntryKind.Arcana ? UIText.Get("cards.found", "n", CardCollection.Found(e.Arcana).ToString(), "max", PackRates.Current.maxPerBinding.ToString())
                    : UIText.Get("cards.soul", "n", CardCollection.SoulLevel(e.Owner).ToString(), "max", PackRates.Current.maxSoulLevel.ToString());
            // In the collection: copies on the left, the level (start rank) on the right as "Lv 1/3" with a big 1.
            string level = null;
            if (mode == Mode.Collection)
            {
                if (e.Kind == EntryKind.Ability) level = LevelText(CardCollection.StartRank(e.Card), e.Card.MaxRank);
                else if (e.Kind == EntryKind.Soul) { level = LevelText(CardCollection.SoulLevel(e.Owner), PackRates.Current.maxSoulLevel); under = ""; }
                else under = UIText.Get("cards.copies", "n", CardCollection.Found(e.Arcana).ToString());
            }
            UnderRow(rt, under, level);
            var binding = e.Kind == EntryKind.Ability && PackRates.Current.bindingOn ? CardCollection.BindingOf(e.Card) : CardBinding.None;
            if (binding != CardBinding.None)
            {
                var badge = InkStyle.Panel(rt, "Binding", UIFactory.WithAlpha(ArcanaCards.Orange, 0.9f));
                badge.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(150f, 40f));
                var t = UIFactory.CreateText(badge.rectTransform, "Text", ArcanaCards.Name(binding), 26, InkStyle.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.Fill();
            }

            int captured = i;
            var trigger = rt.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var click = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
            click.callback.AddListener(_ => { if (book.Busy) return; if (captured == index) Zoom(); else Select(captured); });
            trigger.triggers.Add(click);
        }

        static readonly Color UnderInk = new Color(0.2f, 0.12f, 0.07f);

        /// <summary>"Lv 1/3": the level big, "Lv" and "/3" smaller (sizes filled in by <see cref="UnderRow"/>).</summary>
        static string LevelText(int level, int max)
        {
            return "{s}Lv {/s}{b}" + level + "{/b}{s}/" + Mathf.Max(1, max) + "{/s}";
        }

        /// <summary>The line under a card in the book: <paramref name="left"/> (copies) at the left edge and
        /// <paramref name="right"/> (the level) at the right, or <paramref name="left"/> alone centred. Both shrink
        /// together until they fit the cell with a margin between them, so they never touch.</summary>
        void UnderRow(RectTransform rt, string left, string right)
        {
            float width = CellX - Margin;
            var row = UIFactory.CreateRect("Under", rt).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(width, 50f));
            row.SetSiblingIndex(0);   // under the lifted card
            var l = UIFactory.CreateText(row, "Left", left ?? "", UIFactory.TextSize.Large, UnderInk,
                string.IsNullOrEmpty(right) ? TextAnchor.UpperCenter : TextAnchor.UpperLeft, FontStyle.Bold);
            var r = UIFactory.CreateText(row, "Right", "", UIFactory.TextSize.Large, UnderInk, TextAnchor.UpperRight, FontStyle.Bold);
            foreach (var t in new[] { l, r })
            {
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.Fill();
            }
            for (float k = 1f; ; k -= 0.08f)
            {
                int big = UIFactory.Px(Mathf.RoundToInt(UIFactory.TextSize.Large * k));
                int small = UIFactory.Px(Mathf.RoundToInt(UIFactory.TextSize.Small * k));
                l.fontSize = big;
                r.fontSize = big;
                r.text = string.IsNullOrEmpty(right) ? "" : right.Replace("{s}", "<size=" + small + ">").Replace("{/s}", "</size>")
                    .Replace("{b}", "<size=" + big + ">").Replace("{/b}", "</size>");
                float used = l.preferredWidth + (r.text.Length > 0 ? r.preferredWidth + Margin : 0f);
                if (used <= width || k < 0.5f) break;
            }
        }

        /// <summary>The card as its owner would play it: their resonance's colour, at the rank its copies give it.</summary>
        CardInstance Instance(Entry e)
        {
            var unit = new BattleUnit(e.Owner, Team.Player);
            unit.ApplyLoadout(new Loadout { Element = e.Element });
            int rank = e.Kind == EntryKind.Ability && mode != Mode.Index ? CardCollection.StartRank(e.Card) : 1;
            return new CardInstance(e.Card, unit, rank);
        }

        void Select(int i)
        {
            if (shown.Count == 0) return;
            index = Mathf.Clamp(i, 0, shown.Count - 1);
            int spread = index / PerSpread;
            if (spread != book.Spread && !book.Busy) StartCoroutine(TurnTo(spread));
            foreach (var e in shown) if (e.Lift != null) e.Lift.Selected = false;
            if (shown[index].Lift != null) shown[index].Lift.Selected = true;
            Refresh();
        }

        System.Collections.IEnumerator TurnTo(int spread)
        {
            while (book.Spread != spread)
            {
                yield return book.Turn(spread > book.Spread ? 1 : -1);
                if (index < shown.Count && shown[index].Lift != null) shown[index].Lift.Selected = true;
            }
        }

        /// <summary>The arrows move around the open spread: left / right along a row (crossing the gutter, and turning the
        /// page at either edge), up / down between the two rows.</summary>
        public static int Neighbour(int from, int dx, int dy)
        {
            int page = from / PerPage, slot = from % PerPage, col = slot % Columns, row = slot / Columns;
            if (dy != 0)
            {
                int r = row + dy;
                return r < 0 || r >= Rows ? from : page * PerPage + r * Columns + col;
            }
            int c = col + dx;
            if (c >= 0 && c < Columns) return page * PerPage + row * Columns + c;
            int nextPage = page + dx;
            if (nextPage < 0) return from;
            return nextPage * PerPage + row * Columns + (dx > 0 ? 0 : Columns - 1);
        }

        /// <summary>The info panel, counters and buttons for the selected card.</summary>
        void Refresh()
        {
            if (mode == Mode.Session) { RefreshSession(); return; }
            if (mode == Mode.Index) { RefreshIndex(); return; }
            var rates = PackRates.Current;
            packLabel.text = UIText.Get("cards.open_pack", "n", WorldSession.Packs.ToString());
            packButton.interactable = WorldSession.Packs > 0;
            counters.text = "";   // Sun / Moon / Star found and Soul levels show under their cards

            if (shown.Count == 0)
            {
                info.text = UIText.Get("cards.empty");
                bindButton.gameObject.SetActive(false);
                return;
            }
            var e = shown[index];

            // Without the language chip the card's name is runes (the info line keeps its place, blank).
            UIFactory.ClearChildren(infoRunes);
            bool read = CardCollection.Translated;
            if (!read && e.Kind != EntryKind.Arcana)
                RuneText.Build(infoRunes, e.Card.DisplayName, InkStyle.GoldBright, 700f, 40f, 24f, true);
            string cardName = read ? e.Card.DisplayName : "";
            switch (e.Kind)
            {
                case EntryKind.Ability:
                    int next = CardCollection.CopiesToNextRank(e.Card);
                    info.text = UIText.Get("cards.info.ability",
                        "name", cardName,
                        "copies", CardCollection.Copies(e.Card).ToString(),
                        "next", next > 0 ? UIText.Get("cards.info.next", "n", next.ToString(), "level", (CardCollection.StartRank(e.Card) + 1).ToString()) : UIText.Get("cards.info.top"));
                    break;
                case EntryKind.Arcana:
                    info.text = UIText.Get("cards.info.arcana", "name", ArcanaCards.Name(e.Arcana),
                        "found", CardCollection.Found(e.Arcana).ToString(), "max", PackRates.Current.maxPerBinding.ToString());
                    break;
                default:
                    info.text = UIText.Get("cards.info.soul", "name", read ? e.Owner.DisplayName : "", "card", cardName,
                        "level", CardCollection.SoulLevel(e.Owner).ToString(), "max", rates.maxSoulLevel.ToString(),
                        "pct", Mathf.RoundToInt(CardCollection.SoulLevel(e.Owner) * rates.ultPowerPerSoul * 100f).ToString());
                    break;
            }
        }

        void RefreshSession()
        {
            var s = CardCollection.Current;
            packLabel.text = UIText.Get("cards.open_pack.free");
            packButton.interactable = true;
            int souls = 0, rares = 0, commons = 0;
            if (s != null)
            {
                foreach (var v in s.SoulPulls.Values) souls += v;
                foreach (var v in s.RarePulls.Values) rares += v;
                foreach (var v in s.CommonPulls.Values) commons += v;
            }
            counters.text = UIText.Get("cards.session.stats", "packs", (s != null ? s.PacksOpened : 0).ToString(),
                "souls", souls.ToString(), "rares", rares.ToString(), "commons", commons.ToString());
            counters.fontSize = UIFactory.Px(UIFactory.TextSize.Medium);
            counters.FitOneLine(CountersWidth, UIFactory.TextSize.Min);   // one line, clear of the buttons on the right
            if (shown.Count == 0) { info.text = UIText.Get("cards.session.empty"); return; }
            var e = shown[index];
            string name = e.Kind == EntryKind.Arcana ? ArcanaCards.Name(e.Arcana) : e.Kind == EntryKind.Soul
                ? UIText.Get("cards.rarest.soul", "name", e.Owner.DisplayName) : e.Card.DisplayName;
            int n = e.Kind == EntryKind.Ability ? CardCollection.Copies(e.Card) : e.Kind == EntryKind.Arcana
                ? CardCollection.Found(e.Arcana) : CardCollection.SoulLevel(e.Owner);
            info.text = UIText.Get("cards.info.session", "name", name, "n", n.ToString());
        }

        void RefreshIndex()
        {
            counters.text = "";
            if (shown.Count == 0) { info.text = UIText.Get("cards.empty"); return; }
            var e = shown[index];
            switch (e.Kind)
            {
                case EntryKind.Arcana:
                    info.text = UIText.Get("cards.info.index.arcana", "name", ArcanaCards.Name(e.Arcana),
                        "max", PackRates.Current.maxPerBinding.ToString());
                    break;
                case EntryKind.Soul:
                    info.text = UIText.Get("cards.info.index.soul", "name", e.Card.DisplayName, "owner", e.Owner.DisplayName,
                        "element", AfflatusInfo.Display(e.Element), "text", Plain(CardText.ForRank(e.Card, 1)),
                        "pct", Mathf.RoundToInt(PackRates.Current.ultPowerPerSoul * 100f).ToString(), "max", PackRates.Current.maxSoulLevel.ToString());
                    break;
                default:
                    info.text = UIText.Get("cards.info.index.ability", "name", e.Card.DisplayName, "owner", e.Owner.DisplayName,
                        "kind", UnitView.KindLabel(e.Card.kind), "element", AfflatusInfo.Display(e.Element), "text", Plain(CardText.ForRank(e.Card, 1)));
                    break;
            }
        }

        /// <summary>Card text without its "@target" tokens or line breaks (one readable line in the panel).</summary>
        static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace(CardText.CardTargetToken, UIText.Get("cards.index.target")).Replace("\n", " ");
        }

        /// <summary>Card Pack mode's Esc: with cards pulled, a warning that they'll be lost and the session's rarest.</summary>
        void ConfirmLeave()
        {
            var s = CardCollection.Current;
            if (s == null || s.CardsPulled == 0) { Leave(); return; }
            CardPacks.Tier tier;
            CharacterDefinition member;
            CardBinding binding;
            CardDefinition card;
            int count;
            string rarest = "";
            if (CardCollection.Rarest(out tier, out member, out binding, out card, out count))
            {
                string name = tier == CardPacks.Tier.Soul ? UIText.Get("cards.rarest.soul", "name", member.DisplayName)
                    : tier == CardPacks.Tier.Rare ? ArcanaCards.Name(binding) : card.DisplayName;
                rarest = UIText.Get("cards.leave.rarest", "name", name) + (count > 1 ? " " + UIText.Get("cards.rarest.count", "n", count.ToString()) : "");
            }
            picker.Show(UIText.Get("cards.leave.title"), new List<RowMenu.Row>
            {
                new RowMenu.Row { Label = UIText.Get("cards.leave.warning", "n", s.CardsPulled.ToString()), Enabled = false },
                new RowMenu.Row { Label = rarest, Enabled = false },
                new RowMenu.Row { Label = UIText.Get("cards.leave.yes"), Action = Leave },
                new RowMenu.Row { Label = UIText.Get("cards.leave.stay"), Action = picker.Hide },
            }, picker.Hide, 3);
        }

        void Leave()
        {
            picker.Hide();
            CardCollection.EndSession();
            Close();
        }

        /// <summary>What a binding does, in words with its numbers from PackRates.</summary>
        public static string BindingLine(CardBinding kind)
        {
            var rates = PackRates.Current;
            switch (kind)
            {
                case CardBinding.Sun: return UIText.Get("arcana.sun.effect", "pct", Mathf.RoundToInt(rates.sunHeal * 100f).ToString());
                case CardBinding.Moon: return UIText.Get("arcana.moon.effect", "turns", rates.moonTurns.ToString());
                case CardBinding.Star: return UIText.Get("arcana.star.effect", "n", rates.starUlt.ToString());
                default: return "";
            }
        }

        // ITEMS > CARDS > ACTIONS

        void Zoom()
        {
            if (shown.Count == 0) return;
            var e = shown[index];
            if (e.Kind == EntryKind.Arcana)
            {
                var kind = e.Arcana;
                CardZoom.Open(root, (face, s) => ArcanaCards.BuildFace(face, kind, s), CardHolo.Kind.Rare);
                return;
            }
            var instance = Instance(e);
            CardDetailPopup.Open(root, instance.Owner, e.Card, instance.Rank, e.Rect);
        }

        void OpenBindPicker()
        {
            if (mode != Mode.Collection || shown.Count == 0 || shown[index].Kind != EntryKind.Ability) return;
            var card = shown[index].Card;
            var rows = new List<RowMenu.Row>();
            foreach (var kind in new[] { CardBinding.Sun, CardBinding.Moon, CardBinding.Star })
            {
                var k = kind;
                bool on = CardCollection.BindingOf(card) == k;
                int free = CardCollection.Free(k);
                rows.Add(new RowMenu.Row
                {
                    Label = UIText.Get("cards.bind.row", "name", ArcanaCards.Name(k), "free", free.ToString()) + (on ? "  " + UIText.Get("cards.bind.on") : ""),
                    Help = BindingLine(k),
                    Enabled = on || free > 0,
                    Action = () => { CardCollection.Bind(card, k); Bound(); },
                });
            }
            if (CardCollection.BindingOf(card) != CardBinding.None)
                rows.Add(new RowMenu.Row { Label = UIText.Get("cards.unbind"), Action = () => { CardCollection.Unbind(card); Bound(); } });
            rows.Add(new RowMenu.Row { Label = UIText.Get("menu.back"), Action = picker.Hide });
            picker.Show(UIText.Get("cards.bind.title", "name", card.DisplayName.ToUpperInvariant()), rows, picker.Hide);
        }

        void Bound()
        {
            SaveGame.Save();
            picker.Hide();
            Rebuild(index);
        }

        void OpenPack()
        {
            if (mode == Mode.Index || (mode == Mode.Collection && WorldSession.Packs <= 0)) return;
            PackOpening.Open(root, Roster, () => Rebuild(index));
        }

        // ITEMS > CARDS > INPUT + MOTION

        void Update()
        {
            if (book.Busy || closing || opening) return;   // opening, turning or closing
            // A card the mouse came to rest on while the book was busy (it can't report the hover then) gets selected now.
            // Only when the hovered card changes, so the arrow keys still work with the mouse resting on a card.
            int over = HoveredIndex();
            if (over != lastHovered && over >= 0) Select(over);
            lastHovered = over;
            if (Time.frameCount == openedFrame || picker.IsOpen || picker.ClosedFrame == Time.frameCount) return;
            if (CardDetailPopup.IsOpen || CardZoom.IsOpen || PackOpening.IsOpen) return;
            if (CardZoom.ClosedFrame == Time.frameCount || PackOpening.ClosedFrame == Time.frameCount) return;
            // The wheel turns a whole spread; the arrows move around the open pages.
            float wheel = Input.mouseScrollDelta.y;
            if (wheel < -0.01f && book.Spread + 1 < book.SpreadCount) Select(Mathf.Min(shown.Count - 1, (book.Spread + 1) * PerSpread));
            if (wheel > 0.01f && book.Spread > 0) Select((book.Spread - 1) * PerSpread);
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Select(Neighbour(index, -1, 0));
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Select(Mathf.Min(shown.Count - 1, Neighbour(index, 1, 0)));
            if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) Select(Neighbour(index, 0, -1));
            if (UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) Select(Mathf.Min(shown.Count - 1, Neighbour(index, 0, 1)));
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space)) Zoom();
            if (PackRates.Current.bindingOn && UIKeys.Pressed(KeyCode.B)) OpenBindPicker();
            if (UIKeys.Pressed(KeyCode.O)) OpenPack();
            if (mode == Mode.Collection && UIKeys.Pressed(KeyCode.Tab)) { SwitchToParts(); return; }
            if (UIKeys.Back || UIKeys.Pressed(KeyCode.Backspace))
            {
                if (mode == Mode.Session) ConfirmLeave();
                else Close();
            }
        }
    }
}
