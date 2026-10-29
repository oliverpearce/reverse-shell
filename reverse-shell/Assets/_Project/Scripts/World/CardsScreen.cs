using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
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
            public Resonance Element;
            public CardBinding Arcana;             // Arcana
            public RectTransform Rect;
            public CardLift Lift;
        }

        // The book's layout: 3 x 2 cards per page, at this scale (a hand card is 1).
        const int Columns = 3, Rows = 2, PerPage = Columns * Rows, PerSpread = PerPage * 2;
        const float FaceScale = 0.66f, CellX = 182f, CellY = 272f, BookScale = 1.35f, BookY = 20f;
        /// <summary>Space kept between any two pieces of text or UI on this page.</summary>
        const float Margin = 16f;
        /// <summary>The counters line under the title: as wide as it can be without reaching the Resonance button.</summary>
        const float CountersWidth = 690f;
        static readonly CardKind?[] Kinds = { null, CardKind.Attack, CardKind.Debuff, CardKind.Buff, CardKind.Defense, CardKind.Heal, CardKind.Counter };

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
            var title = titleText = InkStyle.Title(root, "Title", UIText.Get(titleKey), UIFactory.TextSize.Title, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(topLeft, topLeft, new Vector2(60f, -40f), new Vector2(700f, 90f));
            if (mode == Mode.Collection) ItemsScreen.Tabs(root, false, SwitchToParts, null);

            // Top right: Sort, then the two filters, each a caption over its current choice in big gold; a click opens a dropdown.
            sortLabel = TopButton("Sort", -478f, () => OpenDropdown(sortLabel, SortNames(), sortMode, i => { sortMode = i; Rebuild(0); SaveSort(); }));
            resonanceLabel = TopButton("Resonance", -259f, () => OpenDropdown(resonanceLabel, ResonanceNames(), resonanceFilter, i => { resonanceFilter = i; Rebuild(0); SaveSort(); }));
            kindLabel = TopButton("Kind", -40f, () => OpenDropdown(kindLabel, KindNames(), kindFilter, i => { kindFilter = i; Rebuild(0); SaveSort(); }));
            if (mode == Mode.Session)   // Pack Mode: the shelf picks the set the book shows, so no filters (the timer goes there)
                foreach (var label in new[] { sortLabel, resonanceLabel, kindLabel }) label.transform.parent.gameObject.SetActive(false);

            bookHolder = UIFactory.CreateRect("Book", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, BookY), CardBook.Size);
            bookHolder.localScale = Vector3.one * BookScale;
            book = CardBook.Create(bookHolder);
            book.BuildPage = BuildPage;

            // Bottom, laid out to fit a 4:3 screen with Margin between everything: the key hints on one line at the
            // very bottom; above them the selected card's panel (two lines) and Open Pack beside it, the same height,
            // flush with the book's left and right edges (the panel alone spans the book when there's no Open Pack).
            var bottom = new Vector2(0.5f, 0f);
            const float keysY = 14f, keysH = 44f, panelY = keysY + keysH + Margin, panelH = 124f, packW = 420f;
            float bookHalf = CardBook.Size.x * BookScale / 2f;
            if (mode == Mode.Session) shelfWidth = 2f * bookHalf - SessionInfoWidth - Margin;
            float rightW = mode == Mode.Session ? shelfWidth : packW;   // Pack Mode: the wide pack shelf instead of Open Pack
            float panelW = 2f * bookHalf - (mode == Mode.Index ? 0f : rightW + Margin);
            var panel = InkStyle.Panel(root, "Info", InkStyle.InkPanel);
            panel.rectTransform.Place(bottom, bottom, new Vector2(-bookHalf + panelW / 2f, panelY), new Vector2(panelW, panelH));
            InkStyle.Frame(panel.rectTransform, InkStyle.Rule, 2f);
            info = UIFactory.CreateText(panel.rectTransform, "Text", "", UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.MiddleLeft)
                .BestFit(UIFactory.TextSize.Min, UIFactory.TextSize.Large);   // shrinks to fit the panel (a deck-check note's third line)
            info.supportRichText = true;
            info.lineSpacing = 1.05f;
            // Centred top to bottom with a little room, so the last line isn't clipped.
            info.rectTransform.Fill();
            info.rectTransform.offsetMin = new Vector2(Margin + 4f, 10f);   // room under the last line
            info.rectTransform.offsetMax = new Vector2(-(Margin + 4f), -8f);
            infoRunes = UIFactory.CreateRect("NameRunes", panel.rectTransform).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(Margin + 4f, 22f), new Vector2(700f, 36f));   // over the first of the two centred lines
            // Top: the counters on one line under the title, clear of it and of the book.
            counters = UIFactory.CreateText(root, "Counters", "", UIFactory.TextSize.Medium, InkStyle.PaperDim, TextAnchor.MiddleLeft);
            counters.supportRichText = true;
            counters.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -136f), new Vector2(CountersWidth, 32f));
            packButton = HudView.GoStyleButton(root, "OpenPack", "", OpenPack);
            packButton.GetComponent<RectTransform>().Place(bottom, bottom, new Vector2(bookHalf - packW / 2f, panelY), new Vector2(packW, panelH));
            packLabel = packButton.GetComponentInChildren<Text>();
            packLabel.fontSize = UIFactory.Px(UIFactory.TextSize.XLarge);
            packButton.gameObject.SetActive(mode == Mode.Collection || (mode == Mode.Session && PackModeRun.Packs.Count == 0));
            if (mode == Mode.Session)
            {
                BuildSessionStats();
                BuildShelf(new Vector2(bookHalf - shelfWidth / 2f, panelY), panelH);
                BuildTimer();
            }
            if (mode == Mode.Collection)
            {
                // Copy to Blank: inside the info panel's right end, while you have blank cards.
                imprintButton = InkStyle.Button(panel.rectTransform, "Imprint", "", ImprintSelected, true, UIFactory.TextSize.Medium);
                imprintButton.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Margin, 0f), new Vector2(ImprintWidth, panelH - 24f));
                imprintButton.gameObject.SetActive(false);
            }
            bindButton = InkStyle.Button(root, "Bind", UIText.Get("cards.bind"), OpenBindPicker, false, 40);
            bindButton.gameObject.SetActive(false);   // binding is off for now (PackRates.bindingOn)

            string keysKey = mode == Mode.Session ? "cards.keys.session" : mode == Mode.Index ? "cards.keys.index" : "cards.keys";
            var keys = keysText = UIFactory.CreateText(root, "Keys", UIText.Get(keysKey), UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleCenter);
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
            b.GetComponent<RectTransform>().Place(topRight, topRight, new Vector2(x, -30f), new Vector2(205f, 112f));
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.lineSpacing = 1.1f;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        /// <summary>The Units screen's extra slot: Enter or a click on one of <paramref name="choices"/> uses it
        /// (<paramref name="pick"/>, then the book closes), and Open Pack's place is Remove (pick(null)) when
        /// <paramref name="canRemove"/>. With <paramref name="member"/> (the deck check) the book shows every ability
        /// card: the ones the slot can't take are greyed and the info panel says why, and a card in another unit's deck
        /// shows their name and "Moves from …'s deck". Without it, only the choices show.</summary>
        public void OpenToPick(List<CardDefinition> choices, bool canRemove, System.Action<CardDefinition> pick, System.Action whenClosed,
            CharacterDefinition member = null, CardDefinition current = null)
        {
            pickFrom = choices ?? new List<CardDefinition>();
            pickMember = member;
            pickCurrent = current;
            onPick = pick;
            pickCanRemove = canRemove;
            Show(whenClosed);
        }

        public void Open(System.Action whenClosed)
        {
            pickFrom = null;
            pickMember = null;
            onPick = null;
            Show(whenClosed);
        }

        bool Picking { get { return pickFrom != null; } }
        /// <summary>Picking a unit's Alt card with the deck check: every ability card shows, the others greyed.</summary>
        bool DeckPicking { get { return Picking && pickMember != null; } }
        /// <summary>A card the slot can't take (deck check only).</summary>
        bool Blocked(Entry e) { return DeckPicking && e.Kind == EntryKind.Ability && !pickFrom.Contains(e.Card); }
        List<CardDefinition> pickFrom;
        CharacterDefinition pickMember;
        CardDefinition pickCurrent;
        System.Action<CardDefinition> onPick;
        bool pickCanRemove;
        Text titleText, keysText;

        /// <summary>Picking: uses <paramref name="card"/> (null = Remove) and closes the book.</summary>
        void Pick(CardDefinition card)
        {
            var then = onPick;
            bool stay = useReturn;
            if (stay) EndUse();
            else
            {
                pickFrom = null;
                onPick = null;
            }
            if (then != null) then(card);
            if (stay) Rebuild(0);
            else Close();
        }

        // ITEMS > CARDS > USING SUN / MOON / STAR
        // Under Special, a Sun / Moon / Star card has Use: the book switches to the ability cards you own; clicking one
        // binds it (a flare on that card); Esc goes back. Right-clicking an ability card does the same from its side.

        bool useReturn;          // picking a card for Use: back to the book afterwards, not closed
        int kindBeforeUse;

        void UseArcana(CardBinding kind)
        {
            if (Picking || CardCollection.Free(kind) <= 0) return;
            var owned = new List<CardDefinition>();
            foreach (var member in Roster)
                foreach (var card in CardCollection.AbilitiesOf(member))
                    if (!owned.Contains(card) && CardCollection.Owned(card, Roster)) owned.Add(card);
            pickFrom = owned;
            pickMember = null;
            pickCanRemove = false;
            useReturn = true;
            kindBeforeUse = kindFilter;
            kindFilter = 0;
            onPick = card =>
            {
                if (CardCollection.Bind(card, kind)) SaveGame.Save();
            };
            titleText.text = UIText.Get("cards.title.use", "name", ArcanaCards.Name(kind));
            keysText.text = UIText.Get("cards.keys.use");
            keysText.FitOneLine(1360f);
            Rebuild(0);
        }

        /// <summary>Leaves Use: back to the book as it was (the Special page).</summary>
        void EndUse()
        {
            useReturn = false;
            pickFrom = null;
            onPick = null;
            kindFilter = kindBeforeUse;
            titleText.text = UIText.Get("cards.title");
            keysText.text = UIText.Get("cards.keys");
            keysText.FitOneLine(1360f);
        }

        void Show(System.Action whenClosed)
        {
            if (mode == Mode.Collection)
            {
                titleText.text = UIText.Get(Picking ? "cards.title.pick" : "cards.title");
                keysText.text = UIText.Get(Picking ? "cards.keys.pick" : "cards.keys");
                keysText.FitOneLine(1360f);
            }
            if (mode == Mode.Session && !CardCollection.InSession) { CardCollection.BeginSession(); PackModeRun.Reset(); }
            if (mode == Mode.Session) PackModeRun.Select(SelectedPack);   // the selected tab's race is the one on the clock
            if (mode == Mode.Index) CardCollection.IndexOpen = true;
            // From the title no game is loaded yet: the index shows the latest save's copies and levels.
            if (mode == Mode.Index && !WorldSession.TitleDone) SaveGame.Preview(SaveGame.LatestSlot());
            onClosed = whenClosed;
            closing = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            opening = true;
            LoadSort();
            if (Picking && SpecialShown) kindFilter = 0;   // the picker only holds ability cards
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
            if (closing || Picking) return;
            var then = onClosed;
            onClosed = () => parts.Open(then);
            Close();
        }

        /// <summary>Closes without going back (switching to the Parts tab).</summary>
        void Hide()
        {
            if (mode == Mode.Index) CardCollection.IndexOpen = false;
            if (mode == Mode.Collection && CardCollection.SeenChanged)
            {
                CardCollection.SeenChanged = false;   // the NEW tags looked at stay gone after a reload
                SaveGame.Save();
            }
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
            // Pack Mode shows every card the packs can give (the race is to fill them all in), missing ones faded.
            foreach (var member in session ? PackModeRun.KitOwners(Roster) : Roster)
            {
                if (member == null) continue;
                foreach (var card in CardCollection.AbilitiesOf(member))
                    if (!all.Exists(e => e.Card == card) && (!Picking || DeckPicking || pickFrom.Contains(card)))
                        all.Add(new Entry { Kind = EntryKind.Ability, Card = card, Owner = member, Element = CardCollection.ElementOf(member, card) });
            }
            if (Picking) return;   // ability cards only
            foreach (var kind in new[] { CardBinding.Sun, CardBinding.Moon, CardBinding.Star })
                if (index || session || CardCollection.Found(kind) > 0) all.Add(new Entry { Kind = EntryKind.Arcana, Arcana = kind });
            foreach (var member in Roster)
            {
                if (member == null || member.ritual == null) continue;
                if (index)
                {
                    // Every Soul card: the character's own and each resonance's Ult.
                    all.Add(new Entry { Kind = EntryKind.Soul, Card = member.ritual, Owner = member, Element = member.resonance });
                    foreach (var kit in member.resonances)
                        if (kit != null && kit.ultimate != null && !all.Exists(e => e.Card == kit.ultimate))
                            all.Add(new Entry { Kind = EntryKind.Soul, Card = kit.ultimate, Owner = member, Element = kit.element });
                }
                else
                    all.Add(new Entry { Kind = EntryKind.Soul, Card = WorldSession.UltimateOf(member) ?? member.ritual, Owner = member, Element = WorldSession.ElementOf(member) });
            }
        }

        List<Resonance?> ResonanceChoices()
        {
            var list = new List<Resonance?> { null };
            foreach (var e in all)
                if (e.Kind != EntryKind.Arcana && !list.Contains(e.Element)) list.Add(e.Element);
            return list;
        }

        /// <summary>The Kind filter's last choice: the Special cards (Soul cards and Sun / Moon / Star), which live only
        /// there, apart from the ability cards.</summary>
        bool SpecialShown { get { return kindFilter >= Kinds.Length; } }

        bool Passes(Entry e)
        {
            // Pack Mode: exactly the selected shelf tab's cards (a pack's set, or the Soul tab's Soul / Sun / Moon / Star).
            if (mode == Mode.Session && PackModeRun.Packs.Count > 0)
            {
                var pack = SelectedPack;
                return pack == null ? e.Kind != EntryKind.Ability : e.Kind == EntryKind.Ability && e.Card.pool == pack.pool;
            }
            if (SpecialShown) return e.Kind != EntryKind.Ability;
            if (e.Kind != EntryKind.Ability) return false;   // Special cards are kept under Kind: Special
            var element = ResonanceChoices()[Mathf.Clamp(resonanceFilter, 0, ResonanceChoices().Count - 1)];
            var kind = Kinds[kindFilter];
            if (element.HasValue && (e.Kind == EntryKind.Arcana || e.Element != element.Value)) return false;
            if (kind.HasValue && (e.Kind != EntryKind.Ability || e.Card.kind != kind.Value)) return false;
            return true;
        }

        /// <summary>Opens a filter / sort dropdown under its top-bar button.</summary>
        void OpenDropdown(Text label, List<string> choices, int selected, System.Action<int> pick)
        {
            if (book.Busy || closing || opening) return;
            InkDropdown.Open((RectTransform)label.transform.parent, choices, selected, pick, 300f);
        }

        List<string> ResonanceNames()
        {
            return ResonanceChoices().ConvertAll(e => e.HasValue ? ResonanceName(e.Value) : UIText.Get("cards.filter.all"));
        }

        /// <summary>Resonances whose every card you own (worked out on each <see cref="Rebuild"/>).</summary>
        HashSet<Resonance> completeSets = new HashSet<Resonance>();

        /// <summary>A resonance's name for the filter: gold with a tick once you own its whole set (not in Pack Mode,
        /// whose shelf shows its own sets).</summary>
        string ResonanceName(Resonance element)
        {
            string name = ResonanceInfo.Display(element);
            return mode != Mode.Session && completeSets.Contains(element) ? UIText.Get("cards.filter.complete", "name", name) : name;
        }

        List<string> KindNames()
        {
            var list = new List<string>();
            foreach (var k in Kinds) list.Add(k.HasValue ? UnitView.KindLabel(k.Value) : UIText.Get("cards.filter.all"));
            list.Add(UIText.Get("cards.special"));
            return list;
        }

        // ITEMS > CARDS > SORTING (the Sort dropdown: name is the default; the rest break ties by name)

        enum SortBy { Resonance, Pack, Kind, Name, LevelHigh, LevelLow, CopiesMost, CopiesFewest }
        int sortMode = (int)SortBy.Name;
        Text sortLabel;

        static readonly string[] SortKeys = { "resonance", "pack", "kind", "name", "level_high", "level_low", "copies_most", "copies_fewest" };

        List<string> SortNames()
        {
            var list = new List<string>();
            foreach (var k in SortKeys) list.Add(UIText.Get("cards.sort." + k));
            return list;
        }

        /// <summary>By the Sort choice first (it always wins), then the deck check (cards the slot can take first), then
        /// by name. Sun / Moon / Star and Soul cards come after the abilities and sort among themselves the same way
        /// (copies = how many found / the Soul level).</summary>
        int CompareEntries(Entry a, Entry b)
        {
            if (a.Kind != b.Kind) return a.Kind.CompareTo(b.Kind);   // abilities, then Sun / Moon / Star, then Soul cards
            bool ability = a.Kind == EntryKind.Ability;
            int c = 0;
            switch ((SortBy)sortMode)
            {
                case SortBy.Resonance: c = a.Element.CompareTo(b.Element); break;
                case SortBy.Kind: if (ability) c = a.Card.kind.CompareTo(b.Card.kind); break;
                case SortBy.Pack: if (ability) c = PackOrder(a.Card).CompareTo(PackOrder(b.Card)); break;
                case SortBy.LevelHigh: c = LevelOf(b).CompareTo(LevelOf(a)); break;
                case SortBy.LevelLow: c = LevelOf(a).CompareTo(LevelOf(b)); break;
                case SortBy.CopiesMost: c = HaveOf(b).CompareTo(HaveOf(a)); break;
                case SortBy.CopiesFewest: c = HaveOf(a).CompareTo(HaveOf(b)); break;
            }
            if (c != 0) return c;
            if (Blocked(a) != Blocked(b)) return Blocked(a) ? 1 : -1;   // the deck check: cards the slot can take first
            if (a.Kind == EntryKind.Arcana) return a.Arcana.CompareTo(b.Arcana);   // Sun, Moon, Star
            return NameOrder(a.Card.DisplayName, b.Card.DisplayName);
        }

        /// <summary>How many you have, for the Copies sorts: an ability's copies, Sun / Moon / Star found, a Soul
        /// card's level. Doubled, plus one if you own it, so a starter / innate card with no copies yet still sorts
        /// above the cards you don't have at all.</summary>
        int HaveOf(Entry e)
        {
            switch (e.Kind)
            {
                case EntryKind.Ability: return CardCollection.Copies(e.Card) * 2 + (CardCollection.Owned(e.Card, Roster) ? 1 : 0);
                case EntryKind.Arcana: return CardCollection.Found(e.Arcana) * 2;
                default: return CardCollection.SoulLevel(e.Owner) * 2;
            }
        }

        /// <summary>Alphabetical, ignoring case.</summary>
        static int NameOrder(string a, string b) { return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase); }

        /// <summary>Which pack a card comes from, in the packs' order (Basic, Arc, Root, Cryo, Cinder, Prism).</summary>
        static int PackOrder(CardDefinition card)
        {
            var packs = PackModeRun.Packs;
            for (int i = 0; i < packs.Count; i++) if (packs[i] != null && packs[i].pool == card.pool) return i;
            return packs.Count;
        }

        /// <summary>The level as the book shows it (an ability's copy upgrades, a Soul card's Soul level; Sun / Moon /
        /// Star by how many found); 0 for a card you don't have, so those sort below Lv 1.</summary>
        int LevelOf(Entry e)
        {
            switch (e.Kind)
            {
                case EntryKind.Ability: return CardCollection.Owned(e.Card, Roster) ? CardCollection.UpgradeLevel(e.Card) : 0;
                case EntryKind.Arcana: return CardCollection.Found(e.Arcana);
                default: return CardCollection.SoulLevel(e.Owner);
            }
        }

        // The filters are remembered between visits (and games), per page: the resonance by its element, so it still
        // finds it when the list of resonances changes.
        string SortKey(string what) { return "Cards." + mode + "." + what; }

        void SaveSort()
        {
            try
            {
                var choices = ResonanceChoices();
                var element = choices[Mathf.Clamp(resonanceFilter, 0, choices.Count - 1)];
                PlayerPrefs.SetInt(SortKey("Resonance"), element.HasValue ? (int)element.Value : -1);
                PlayerPrefs.SetInt(SortKey("Kind"), kindFilter);
                PlayerPrefs.SetInt(SortKey("Sort"), sortMode);
                PlayerPrefs.Save();
            }
            catch (System.Exception) { }   // no storage (e.g. a blocked browser): just not remembered
        }

        void LoadSort()
        {
            try
            {
                Gather();
                int saved = PlayerPrefs.GetInt(SortKey("Resonance"), -1);
                resonanceFilter = Mathf.Max(0, ResonanceChoices().FindIndex(c => saved >= 0 ? c.HasValue && (int)c.Value == saved : !c.HasValue));
                kindFilter = Mathf.Clamp(PlayerPrefs.GetInt(SortKey("Kind"), 0), 0, Kinds.Length);
                sortMode = Mathf.Clamp(PlayerPrefs.GetInt(SortKey("Sort"), (int)SortBy.Name), 0, SortKeys.Length - 1);
            }
            catch (System.Exception) { }
        }

        // ITEMS > CARDS > LAYOUT

        void Rebuild(int select)
        {
            Gather();
            completeSets = CardCollection.CompleteSets(Roster);
            if (resonanceFilter >= ResonanceChoices().Count) resonanceFilter = 0;
            shown.Clear();
            foreach (var e in all) if (Passes(e)) shown.Add(e);
            // Abilities by the Sort choice (ties by name); Sun / Moon / Star and Soul cards after them.
            int sortBefore = sortMode;
            if (mode == Mode.Session) sortMode = (int)SortBy.Kind;   // a set reads by kind, then name
            shown.Sort(CompareEntries);
            sortMode = sortBefore;

            book.PageCount = Mathf.Max(2, Mathf.CeilToInt(shown.Count / (float)PerPage));
            index = Mathf.Clamp(select, 0, Mathf.Max(0, shown.Count - 1));
            book.ShowSpread(index / PerSpread);
            var element = ResonanceChoices()[resonanceFilter];
            resonanceLabel.text = UIText.Get("cards.filter.resonance", "value",
                element.HasValue ? ResonanceName(element.Value) : UIText.Get("cards.filter.all"));
            sortLabel.text = UIText.Get("cards.filter.sort", "value", UIText.Get("cards.sort." + SortKeys[Mathf.Clamp(sortMode, 0, SortKeys.Length - 1)]));
            var kind = SpecialShown ? null : Kinds[kindFilter];
            kindLabel.text = UIText.Get("cards.filter.kind", "value", SpecialShown ? UIText.Get("cards.special")
                : kind.HasValue ? UnitView.KindLabel(kind.Value) : UIText.Get("cards.filter.all"));
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
            bool missing = RaceMissing(e);
            if (missing) Unowned(face);   // faded, like an unowned card in the collection
            else if (e.Kind == EntryKind.Ability && (mode == Mode.Session ? CardCollection.Copies(e.Card) <= 0 : !CardCollection.Owned(e.Card, Roster))) Unowned(face);
            else if (Blocked(e)) Unowned(face);   // the deck check: owned, but this slot can't take it
            if (mode == Mode.Session && e.Kind == EntryKind.Soul && CardCollection.SoulLevel(e.Owner) <= 0) Unowned(face);
            if (mode == Mode.Session && e.Kind == EntryKind.Arcana && CardCollection.Found(e.Arcana) <= 0) Unowned(face);
            e.Lift = CardLift.Add(rt, face);
            if (CardCollection.IsNew(KeyOf(e))) NewBadge(rt);
            e.Lift.Selected = i == index && !opening;   // no selection while the book opens
            int hovered = i;
            e.Lift.Hovered = () => { if (!book.Busy) Select(hovered); };
            var holo = CardHolo.Add(face, face.GetComponent<CardTilt>(),
                e.Kind == EntryKind.Soul ? CardHolo.Kind.Soul : e.Kind == EntryKind.Arcana ? CardHolo.Kind.Rare : CardHolo.KindFor(e.Card),
                null, e.Kind == EntryKind.Ability ? e.Card.kind : (CardKind?)null);
            holo.Strength = 0.35f;
            GameObject ownerTag = null;
            if (e.Kind == EntryKind.Ability)
            {
                var ultForm = CardCollection.UltFormOf(e.Owner, e.Card);
                if (ultForm != null && CardCollection.SoulFormKnown(e.Owner, e.Card))
                {
                    var soul = new Entry { Kind = EntryKind.Soul, Card = ultForm, Owner = e.Owner, Element = e.Element };
                    SoulFormPreview.Attach(rt.gameObject, (f, s) => CardView.BuildFace(f, Instance(soul), s, true), FaceScale);
                }
                else if (ultForm != null) SoulFormPreview.AttachHidden(rt.gameObject, FaceScale);   // not met / unlocked yet
                if (mode == Mode.Collection)
                {
                    var who = CardCollection.EquippedBy(e.Card, Roster);
                    OwnerBorder(face, who);
                    ownerTag = OwnerTag(face, who);
                    if (DeckPicking && ownerTag != null) ownerTag.SetActive(true);   // picking an Alt: whose deck it's in, always
                }
            }

            // Under the card: copies (×N) for abilities, found for Sun / Moon / Star, the Soul level for Soul cards; in the
            // session how many were pulled. The title's index shows the latest save's, like the collection.
            string under;
            if (mode == Mode.Session)
                under = UIText.Get("cards.copies", "n", (e.Kind == EntryKind.Ability ? CardCollection.Copies(e.Card)
                    : e.Kind == EntryKind.Arcana ? CardCollection.Found(e.Arcana) : CardCollection.SoulLevel(e.Owner)).ToString());
            else
                under = e.Kind == EntryKind.Ability ? UIText.Get("cards.copies", "n", CardCollection.Copies(e.Card).ToString())
                    : e.Kind == EntryKind.Arcana ? UIText.Get("cards.found", "n", CardCollection.Found(e.Arcana).ToString(), "max", PackRates.Current.maxPerBinding.ToString())
                    : UIText.Get("cards.soul", "n", CardCollection.SoulLevel(e.Owner).ToString(), "max", PackRates.Current.maxSoulLevel.ToString());
            // In the collection and index: copies on the left, the level as stars on the right (one per level: an
            // ability card's upgrade level 1-4, a Soul card's Soul level).
            int stars = 0;
            if (mode != Mode.Session)
            {
                if (e.Kind == EntryKind.Ability) stars = CardCollection.UpgradeLevel(e.Card);
                else if (e.Kind == EntryKind.Soul) { stars = CardCollection.SoulLevel(e.Owner); under = e.Owner.DisplayName; }
                else under = UIText.Get("cards.copies", "n", CardCollection.Found(e.Arcana).ToString());
            }
            if (missing) under = UIText.Get("cards.copies", "n", "0");
            UnderRow(rt, under, stars, e.Kind == EntryKind.Ability && mode != Mode.Session
                ? (CardCollection.Copies(e.Card) > 0 ? Mathf.Max(0.001f, NextLevelProgress(e.Card)) : 0f) : -1f);   // no copies: an empty bar
            var binding = e.Kind == EntryKind.Ability && PackRates.Current.bindingOn ? CardCollection.BindingOf(e.Card) : CardBinding.None;
            if (binding != CardBinding.None) BindingFlare(rt, face, binding);

            int captured = i;
            var trigger = rt.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var click = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
            click.callback.AddListener(data =>
            {
                if (book.Busy) return;
                var pointer = data as UnityEngine.EventSystems.PointerEventData;
                if (pointer != null && pointer.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;   // right click: Update
                if (captured == index) Zoom(); else Select(captured);
            });
            trigger.triggers.Add(click);
            if (ownerTag != null && !DeckPicking)
            {
                // Hover: who has it equipped.
                var tag = ownerTag;
                var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => { if (tag != null) { tag.transform.SetAsLastSibling(); tag.SetActive(true); } });
                trigger.triggers.Add(enter);
                var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => { if (tag != null) tag.SetActive(false); });
                trigger.triggers.Add(exit);
            }
        }

        static readonly Color UnderInk = new Color(0.2f, 0.12f, 0.07f);

        /// <summary>How far a card's copies are toward its next star (0-1); 1 once it has every star.</summary>
        static float NextLevelProgress(CardDefinition card)
        {
            var rates = PackRates.Current;
            int n = CardCollection.Upgrades(card);
            if (n >= rates.MaxUpgrades) return 1f;
            int from = rates.CopiesFor(n), to = rates.CopiesFor(n + 1);
            return Mathf.Clamp01((CardCollection.Copies(card) - from) / (float)Mathf.Max(1, to - from));
        }

        /// <summary>A thin bar along the top of the line under a card, right below the card: copies toward the next star
        /// (gold when full) on a solid track. No copies yet: an empty bar (only copies fill it), so it never looks like
        /// progress was lost.</summary>
        const float BarGap = 9f, BarHeight = 5f, MinBarFill = 0.2f;

        static void ProgressBar(RectTransform row, float progress)
        {
            var back = UIFactory.CreateImage(row, "Progress", Color.Lerp(UnderInk, Color.white, 0.6f));   // a solid, pale track
            back.raycastTarget = false;
            back.rectTransform.anchorMin = new Vector2(0f, 1f);
            back.rectTransform.anchorMax = new Vector2(1f, 1f);
            back.rectTransform.pivot = new Vector2(0.5f, 1f);
            back.rectTransform.sizeDelta = new Vector2(0f, BarHeight);
            back.rectTransform.anchoredPosition = new Vector2(0f, -BarGap);
            var fill = UIFactory.CreateImage(back.rectTransform, "Fill", progress >= 1f ? InkStyle.Gold : UnderInk);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            // Some copies: at least 20% filled so a little progress still shows. None: empty.
            float filled = progress <= 0f ? 0f : Mathf.Max(MinBarFill, Mathf.Clamp01(progress));
            fill.rectTransform.anchorMax = new Vector2(filled, 1f);
            fill.gameObject.SetActive(filled > 0f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>The key a card's NEW mark is kept under (CardCollection.IsNew).</summary>
        static string KeyOf(Entry e)
        {
            return e.Kind == EntryKind.Ability ? e.Card.name : e.Kind == EntryKind.Soul ? CardCollection.SoulKey(e.Owner)
                : CardCollection.ArcanaKey(e.Arcana);
        }

        /// <summary>A small gold NEW tag on a card's top-left corner, until the card is hovered.</summary>
        static void NewBadge(RectTransform card)
        {
            var badge = InkStyle.Panel(card, "New", InkStyle.Gold);
            badge.raycastTarget = false;
            badge.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(10f, -6f), new Vector2(76f, 34f));
            badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var t = UIFactory.CreateText(badge.rectTransform, "Text", UIText.Get("cards.new"), UIFactory.TextSize.Small, InkStyle.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.Fill();
            badge.transform.SetAsLastSibling();
        }

        /// <summary>"14 / 18 cards" (ability cards you own of every one this page could hold), and blank cards.</summary>
        string CollectionLine()
        {
            int owned = 0, total = 0;
            foreach (var e in all)
            {
                if (e.Kind != EntryKind.Ability) continue;
                total++;
                if (CardCollection.Owned(e.Card, Roster)) owned++;
            }
            string line = UIText.Get("cards.count", "have", owned.ToString(), "all", total.ToString());
            line += "      " + UIText.Get("cards.blanks", "n", CardCollection.Blanks.ToString());   // always shown
            return line;
        }

        /// <summary>The colour each binding glows in: Sun warm gold, Moon pale blue, Star bright yellow.</summary>
        public static Color BindingColor(CardBinding kind)
        {
            return kind == CardBinding.Sun ? new Color(1f, 0.66f, 0.25f) : kind == CardBinding.Moon ? new Color(0.7f, 0.82f, 1f)
                : new Color(1f, 0.92f, 0.45f);
        }

        /// <summary>A bound card's flare: a glowing rim in the binding's colour, sparkles on the face and the Sun / Moon /
        /// Star icon pinned to the top-right corner.</summary>
        static void BindingFlare(RectTransform card, RectTransform face, CardBinding kind)
        {
            var color = BindingColor(kind);
            var glow = UiParticles.Glow(card, Vector2.zero, 10f, UIFactory.WithAlpha(color, 0.45f));
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = new Vector2(-26f, -26f);
            glow.rectTransform.offsetMax = new Vector2(26f, 26f);
            glow.transform.SetAsFirstSibling();   // behind the card
            // On the face, so they lift and lean with the card.
            InkStyle.Frame(face, UIFactory.WithAlpha(color, 0.9f), 3f);
            CardSparkles.Add(face, color);
            var icon = UIFactory.CreateImage(face, "BindingIcon", Color.white);
            icon.sprite = ArcanaCards.Art(kind);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-4f, -4f), new Vector2(50f, 50f));
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }

        /// <summary>
        /// A card in someone's deck wears a border in that unit's colour: their resonance's (the protagonist's
        /// current one; Basic is grey). It sits on the card face, so it lifts and tilts with it.
        /// </summary>
        static void OwnerBorder(RectTransform face, List<CharacterDefinition> who)
        {
            if (who.Count == 0) return;
            var color = ResonanceInfo.Color(WorldSession.ElementOf(who[0]));
            var border = UIFactory.CreateRect("OwnerBorder", face);
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(-9f, -9f);
            border.offsetMax = new Vector2(9f, 9f);
            border.SetAsFirstSibling();   // behind the card's own art
            InkStyle.Frame(border, color, 5f, 2f);
            foreach (var g in border.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }

        /// <summary>A small name tag over an equipped card's top edge (who has it in their deck), shown on hover; null
        /// when no one has it.</summary>
        static GameObject OwnerTag(RectTransform face, List<CharacterDefinition> who)
        {
            if (who.Count == 0) return null;
            var names = new List<string>();
            foreach (var m in who) if (m != null) names.Add(m.DisplayName);
            var tag = InkStyle.Panel(face, "OwnerTag", InkStyle.InkPanel);
            InkStyle.Frame(tag.rectTransform, ResonanceInfo.Color(WorldSession.ElementOf(who[0])), 2f);
            var t = UIFactory.CreateText(tag.rectTransform, "Text", string.Join(", ", names.ToArray()), UIFactory.TextSize.Small, InkStyle.Paper, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.Fill();
            tag.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(t.preferredWidth + 28f, 38f));
            foreach (var g in tag.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        /// <summary>How see-through a card you have no copies of is in the book (its art and text still show).</summary>
        const float UnownedAlpha = 0.5f;

        /// <summary>A card with no copies: the whole face, art and all, at <see cref="UnownedAlpha"/>.</summary>
        static void Unowned(RectTransform face)
        {
            var group = face.gameObject.AddComponent<CanvasGroup>();
            group.alpha = UnownedAlpha;
            group.blocksRaycasts = false;
        }

        /// <summary>Pack Mode: a card of the selected pack's set that this race hasn't pulled yet.</summary>
        bool RaceMissing(Entry e)
        {
            var pack = mode == Mode.Session ? SelectedPack : null;
            return pack != null && e.Kind == EntryKind.Ability && e.Card.pool == pack.pool && !PackModeRun.Has(pack, e.Card);
        }

        /// <summary>"#RRGGBB" for rich text.</summary>
        static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        /// <summary>The line under a card in the book: <paramref name="left"/> (copies) at the left edge and
        /// <paramref name="stars"/> (the level, one star each) at the right, or <paramref name="left"/> alone centred
        /// when there are none. The number shrinks until both fit the cell with a margin between them.</summary>
        void UnderRow(RectTransform rt, string left, int stars, float progress)
        {
            float width = CellX - Margin;
            // Just under the card: the progress bar, then one tidy line with the copies (Medium) and the stars, both
            // centred on that line.
            // The bar sits midway between the card's bottom edge and the copies / stars line (BarGap above and below).
            var row = UIFactory.CreateRect("Under", rt).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, 50f));
            row.SetSiblingIndex(0);   // under the lifted card
            if (progress >= 0f) ProgressBar(row, progress);
            var line = UIFactory.CreateRect("Line", row).Fill();
            line.offsetMax = new Vector2(0f, -(BarGap * 2f + BarHeight - 6f));   // the text's glyphs start ~6 px into its line
            var l = UIFactory.CreateText(line, "Left", left ?? "", UIFactory.TextSize.Medium, UnderInk,
                stars > 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontStyle.Bold);
            l.supportRichText = true;
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.rectTransform.Fill();
            if (stars <= 0) return;
            const float star = 24f;
            float starsWidth = stars * star * 1.08f;
            CardStars.Build(line, stars, star, new Vector2(1f, 0.5f), new Vector2(-starsWidth, star / 2f));
            for (float k = 1f; l.preferredWidth + Margin + starsWidth > width && k >= 0.5f; k -= 0.08f)
                l.fontSize = UIFactory.Px(Mathf.RoundToInt(UIFactory.TextSize.Medium * k));
        }

        /// <summary>The card as its owner would play it: their resonance's colour, at the rank its copies give it.</summary>
        CardInstance Instance(Entry e)
        {
            var unit = new BattleUnit(e.Owner, Team.Player);
            var loadout = new Loadout { Element = e.Element };
            if (e.Kind == EntryKind.Ability) loadout.Upgrades[e.Card] = CardCollection.Upgrades(e.Card);   // numbers at its upgraded level
            unit.ApplyLoadout(loadout);
            return new CardInstance(e.Card, unit, 1);
        }

        void Select(int i)
        {
            if (shown.Count == 0) return;
            index = Mathf.Clamp(i, 0, shown.Count - 1);
            int spread = index / PerSpread;
            if (spread != book.Spread && !book.Busy) StartCoroutine(TurnTo(spread));
            foreach (var e in shown) if (e.Lift != null) e.Lift.Selected = false;
            var picked = shown[index];
            if (picked.Lift != null) picked.Lift.Selected = true;
            if (CardCollection.IsNew(KeyOf(picked)))
            {
                CardCollection.Seen(KeyOf(picked));
                var badge = picked.Rect != null ? picked.Rect.Find("New") : null;
                if (badge != null) Destroy(badge.gameObject);
            }
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
            if (Picking)
            {
                packLabel.text = UIText.Get("units.extra.remove");
                packButton.interactable = pickCanRemove;
            }
            counters.text = Picking ? "" : CollectionLine();   // Sun / Moon / Star found and Soul levels show under their cards
            RefreshImprint();

            if (shown.Count == 0)
            {
                info.text = UIText.Get("cards.empty");
                bindButton.gameObject.SetActive(false);
                return;
            }
            var e = shown[index];

            // Without the language chip the card's name is runes (the info line keeps its place, blank).
            UIFactory.ClearChildren(infoRunes);
            // Two lines, or three when picking an Alt and the deck check has something to say.
            string deckLine = DeckPicking && e.Kind == EntryKind.Ability ? DeckLine(e.Card) : null;
            infoRunes.anchoredPosition = new Vector2(infoRunes.anchoredPosition.x, deckLine != null ? 34f : 22f);
            bool read = CardCollection.Translated;
            if (!read && e.Kind != EntryKind.Arcana)
                RuneText.Build(infoRunes, e.Card.DisplayName, InkStyle.GoldBright, 700f, 40f, 24f, true);
            string cardName = read && e.Card != null ? e.Card.DisplayName : "";   // Sun / Moon / Star have no CardDefinition
            switch (e.Kind)
            {
                case EntryKind.Ability:
                    int next = CardCollection.CopiesToNextUpgrade(e.Card);
                    info.text = UIText.Get("cards.info.ability",
                        "name", cardName,
                        "copies", CardCollection.Copies(e.Card).ToString(),
                        "next", next > 0 ? UIText.Get("cards.info.next", "n", next.ToString(), "level", (CardCollection.UpgradeLevel(e.Card) + 1).ToString()) : UIText.Get("cards.info.top"),
                        "power", deckLine != null ? "\n" + deckLine : PowerSuffix(e.Card));
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
            RefreshShelf();
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
            counters.text = "";   // the hoverable stats line instead (BuildSessionStats)
            stats[0].text = UIText.Get("cards.stat.packs", "n", (s != null ? s.PacksOpened : 0).ToString());
            stats[1].text = UIText.Get("cards.stat.souls", "n", souls.ToString());
            stats[2].text = UIText.Get("cards.stat.rares", "n", rares.ToString());
            stats[3].text = UIText.Get("cards.stat.cards", "n", commons.ToString());
            stats[4].text = UIText.Get("cards.stat.blanks", "n", (s != null ? s.BlankPulls : 0).ToString());
            if (shown.Count == 0) { info.text = UIText.Get("cards.session.empty"); return; }
            var e = shown[index];
            string name = e.Kind == EntryKind.Arcana ? ArcanaCards.Name(e.Arcana) : e.Kind == EntryKind.Soul
                ? UIText.Get("cards.rarest.soul", "name", e.Owner.DisplayName) : e.Card.DisplayName;
            int n = RaceMissing(e) ? 0 : e.Kind == EntryKind.Ability ? CardCollection.Copies(e.Card) : e.Kind == EntryKind.Arcana
                ? CardCollection.Found(e.Arcana) : CardCollection.SoulLevel(e.Owner);
            info.text = UIText.Get("cards.info.session", "name", name, "n", n.ToString());
        }

        void RefreshIndex()
        {
            counters.text = CollectionLine();
            if (shown.Count == 0) { info.text = UIText.Get("cards.empty"); return; }
            var e = shown[index];
            switch (e.Kind)
            {
                case EntryKind.Arcana:
                    info.text = UIText.Get("cards.info.index.arcana", "name", ArcanaCards.Name(e.Arcana),
                        "max", PackRates.Current.maxPerBinding.ToString());
                    break;
                case EntryKind.Soul:
                    // Who uses it, its class and resonance beside the name, then the latest save's copies / level; the
                    // card text is only in the inspect view.
                    info.text = UIText.Get("cards.info.index.soul", "name", e.Card.DisplayName, "owner", e.Owner.DisplayName,
                        "element", ResonanceInfo.Display(e.Element), "owner_c", Hex(e.Owner.color), "element_c", Hex(ResonanceInfo.Color(e.Element)),
                        "level", CardCollection.SoulLevel(e.Owner).ToString(), "max", PackRates.Current.maxSoulLevel.ToString());
                    break;
                default:
                    int next = CardCollection.CopiesToNextUpgrade(e.Card);
                    info.text = UIText.Get("cards.info.index.ability", "name", e.Card.DisplayName, "owner", e.Owner.DisplayName,
                        "kind", UnitView.KindLabel(e.Card.kind), "element", ResonanceInfo.Display(e.Element),
                        "owner_c", Hex(e.Owner.color), "kind_c", Hex(UnitView.KindColor(e.Card.kind)), "element_c", Hex(ResonanceInfo.Color(e.Element)),
                        "copies", CardCollection.Copies(e.Card).ToString(),
                        "next", next > 0 ? UIText.Get("cards.info.next", "n", next.ToString(), "level", (CardCollection.UpgradeLevel(e.Card) + 1).ToString()) : UIText.Get("cards.info.top"),
                        "power", PowerSuffix(e.Card));
                    break;
            }
        }

        /// <summary>Picking an Alt (the deck check): why the slot can't take <paramref name="card"/>, or that taking it
        /// moves it out of another unit's deck; null when neither (the level preview shows instead).</summary>
        string DeckLine(CardDefinition card)
        {
            if (pickFrom.Contains(card))
            {
                foreach (var m in Roster)
                    if (m != null && m != pickMember && CardCollection.Extras(m).Contains(card))
                        return UIText.Get("cards.pick.moves", "name", m.DisplayName);
                return null;
            }
            if (card == pickCurrent) return UIText.Get("cards.pick.current");
            foreach (var m in Roster)
                if (m != null && CardCollection.LockedCard(m) == card) return UIText.Get("cards.pick.innate", "name", m.DisplayName);
            if (!CardCollection.Owned(card, Roster)) return UIText.Get("cards.pick.unowned");
            if (!CardCollection.UsableNow(pickMember, card))
                return UIText.Get("cards.pick.resonance", "element", ResonanceInfo.Display(CardCollection.ElementOf(card, Roster)));
            return UIText.Get("cards.pick.cant");
        }

        /// <summary>What the next star changes: the card's main hit / heal (a multiple of ATK), now and after the next
        /// upgrade ("150% → 200% ATK"); at the top, just today's. The card's level in battle stays hidden; cards with no
        /// hit or heal show nothing.</summary>
        static string PowerLine(CardDefinition card)
        {
            int upgrades = CardCollection.Upgrades(card);
            int from = CardLevels.WithUpgrades(card, upgrades);
            float now = CardLevels.MainPower(card, from);
            if (now <= 0f) return "";
            if (upgrades >= PackRates.Current.MaxUpgrades) return UIText.Get("cards.info.power", "pct", AtkPercent(now));
            int to = CardLevels.WithUpgrades(card, upgrades + 1);
            return UIText.Get("cards.info.power.up", "pf", AtkPercent(now), "pt", AtkPercent(CardLevels.MainPower(card, to)));
        }

        /// <summary><see cref="PowerLine"/> to follow "N more for Lv X" on the same line (nothing if the card has none).</summary>
        static string PowerSuffix(CardDefinition card)
        {
            string power = PowerLine(card);
            return power.Length > 0 ? "      " + power : "";
        }

        static string AtkPercent(float multiple) { return Mathf.RoundToInt(multiple * 100f) + "%"; }

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
            if (Picking) { if (!Blocked(e)) Pick(e.Card); return; }   // a greyed card only shows why in the info panel
            if (e.Kind == EntryKind.Arcana)
            {
                var kind = e.Arcana;
                CardZoom.Open(root, (face, s) => ArcanaCards.BuildFace(face, kind, s), CardHolo.Kind.Rare);
                return;
            }
            var instance = Instance(e);
            CardDetailPopup.Open(root, instance.Owner, e.Card, instance.Rank, e.Rect, null,
                e.Kind == EntryKind.Ability && CardCollection.SoulFormKnown(e.Owner, e.Card) ? CardCollection.UltFormOf(e.Owner, e.Card) : null);
        }

        void OpenBindPicker()
        {
            if (Picking || shown.Count == 0 || shown[index].Kind != EntryKind.Ability) return;
            var card = shown[index].Card;
            var rows = new List<RowMenu.Row>();
            if (mode != Mode.Collection)
            {
                // Pack Mode keeps nothing and the title's book is only a look: binding happens in your game's book.
                rows.Add(new RowMenu.Row { Label = UIText.Get("cards.bind.elsewhere"), Enabled = false });
                rows.Add(new RowMenu.Row { Label = UIText.Get("menu.back"), Action = picker.Hide });
                picker.Show(UIText.Get("cards.bind.title", "name", card.DisplayName.ToUpperInvariant()), rows, picker.Hide, 1);
                return;
            }
            if (CardCollection.Found(CardBinding.Sun) + CardCollection.Found(CardBinding.Moon) + CardCollection.Found(CardBinding.Star) == 0)
                rows.Add(new RowMenu.Row { Label = UIText.Get("cards.bind.none"), Enabled = false });
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

        const float OddsWidth = 680f;
        Text odds;
        Text[] stats;   // Pack Mode's line under the title: Packs, Soul, Rare, Cards

        /// <summary>Pack Mode's stats line, one piece per count, laid out left to right; hovering Soul, Rare or Cards
        /// shows that kind's pack odds just below.</summary>
        void BuildSessionStats()
        {
            var topLeft = new Vector2(0f, 1f);
            var row = UIFactory.CreateRect("Stats", root).Place(topLeft, topLeft, new Vector2(60f, -136f), new Vector2(CountersWidth, 32f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            stats = new Text[5];
            for (int i = 0; i < stats.Length; i++)
            {
                var t = stats[i] = UIFactory.CreateText(row, "Stat" + i, "", UIFactory.TextSize.Medium, InkStyle.PaperDim, TextAnchor.MiddleLeft);
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (i == 0) continue;   // Packs has no odds
                t.raycastTarget = true;
                int kind = i;
                var trigger = t.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowOdds(kind));
                trigger.triggers.Add(enter);
                var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => ShowOdds(0));
                trigger.triggers.Add(exit);
            }
            odds = InkStyle.HelpPanel(root, Vector2.zero, new Vector2(OddsWidth, 100f));
            odds.supportRichText = true;
            ((RectTransform)odds.transform.parent).Place(topLeft, topLeft, new Vector2(60f, -176f), new Vector2(OddsWidth, 100f));
        }

        /// <summary>The pack odds for one stat (1 Soul, 2 Sun / Moon / Star, 3 Cards, 4 Blank; 0 hides them), from the
        /// pack the selected shelf tab opens (<see cref="OddsOf"/>). Only a pack's last card can be a Soul card or Sun /
        /// Moon / Star; each ability card's chance is its chance to be in a pack at all.</summary>
        void ShowOdds(int kind)
        {
            var panel = odds.transform.parent.gameObject;
            panel.SetActive(kind > 0);
            if (kind <= 0) return;
            var o = OddsOf(SelectedPack);
            switch (kind)
            {
                case 1: odds.text = UIText.Get("cards.odds.soul", "pct", Percent(o.Soul)); break;
                case 2: odds.text = UIText.Get("cards.odds.arcana", "pct", Percent(o.Arcana), "each", Percent(o.Arcana / 3f)); break;
                case 4: odds.text = UIText.Get("cards.odds.blank", "pct", Percent(o.Blank)); break;
                default:
                    // A pack's set (its resonance's cards, or the Basic cards) gets PackKind.poolShare of the ability
                    // slots; the Soul tab opens any pack, so every card shares every slot.
                    var pack = SelectedPack;
                    var cards = new List<CardDefinition>();
                    float share = 1f;
                    if (pack != null)
                    {
                        cards = PackModeRun.SetOf(pack, Roster);
                        if (pack.pool != Resonance.Basic) share = pack.poolShare;
                    }
                    else
                        foreach (var member in PackModeRun.KitOwners(Roster))
                            foreach (var card in CardCollection.AbilitiesOf(member))
                                if (!cards.Contains(card)) cards.Add(card);
                    odds.text = UIText.Get("cards.odds.cards", "n", cards.Count.ToString(),
                        "pct", Percent(Mathf.Min(1f, o.Slots * share / Mathf.Max(1, cards.Count))));
                    if (o.Legendary > 0f)   // each ability card rolls it; at least one in the pack
                        odds.text += "\n" + UIText.Get("cards.odds.legendary",
                            "pct", Percent(1f - Mathf.Pow(1f - o.Legendary, o.Slots)), "each", Percent(o.Legendary));
                    break;
            }
            panel.transform.SetAsLastSibling();
        }

        struct PackOdds { public float Soul, Arcana, Blank, Slots, Legendary; }

        /// <summary>The odds a pack really rolls with (CardPacks.RollKind): <paramref name="pack"/>'s own; no pack (the
        /// Soul tab, which opens a random one) the average over every pack; no packs set up at all, PackRates' own.
        /// Slots = the ability cards a pack holds on average.</summary>
        static PackOdds OddsOf(PackKind pack)
        {
            var rates = PackRates.Current;
            var list = new List<PackKind>();
            if (pack != null) list.Add(pack);
            else list.AddRange(rates.packs.FindAll(p => p != null));
            if (list.Count == 0)
            {
                var plain = Odds(rates.cardsPerPack, rates.soulChance, rates.arcanaChance, rates.blankChance);
                plain.Legendary = rates.legendaryChance;
                return plain;
            }
            var sum = new PackOdds();
            foreach (var p in list)
            {
                var one = Odds(p.cardsPerPack, p.soulChance, p.arcanaChance, p.blankChance);
                sum.Soul += one.Soul; sum.Arcana += one.Arcana; sum.Blank += one.Blank; sum.Slots += one.Slots;
                float tiers = p.commonShare + p.rareShare + p.epicShare + p.legendaryShare;
                sum.Legendary += tiers > 0f ? p.legendaryShare / tiers : 0f;   // each ability card's chance
            }
            return new PackOdds { Soul = sum.Soul / list.Count, Arcana = sum.Arcana / list.Count, Blank = sum.Blank / list.Count,
                Slots = sum.Slots / list.Count, Legendary = sum.Legendary / list.Count };
        }

        static PackOdds Odds(int cardsPerPack, float soul, float arcana, float blank)
        {
            int count = Mathf.Max(1, cardsPerPack);
            // Only the second card can be blank (when it isn't also the last); only the last can be Soul / Sun / Moon / Star.
            float blankChance = count >= 3 ? blank : 0f;
            float last = Mathf.Clamp01(soul + arcana);
            return new PackOdds
            {
                Soul = Mathf.Min(soul, 1f), Arcana = Mathf.Clamp01(last - soul), Blank = blankChance,
                Slots = count - 1 - blankChance + (1f - last),
            };
        }

        /// <summary>0.05 as "5%", 0.0167 as "1.7%".</summary>
        static string Percent(float chance)
        {
            float pct = chance * 100f;
            return (pct < 10f ? (Mathf.Round(pct * 10f) / 10f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                : Mathf.RoundToInt(pct).ToString()) + "%";
        }

        const float ImprintWidth = 330f;
        Button imprintButton;

        /// <summary>Copy to Blank shows while you have blank cards (and you're not picking an alt card); usable on an
        /// ability card that isn't at its top level yet. The info text makes room for it.</summary>
        void RefreshImprint()
        {
            if (imprintButton == null) return;
            var e = shown.Count > 0 ? shown[index] : null;
            bool arcana = !Picking && e != null && e.Kind == EntryKind.Arcana;
            bool show = arcana || (!Picking && CardCollection.Blanks > 0);
            imprintButton.gameObject.SetActive(show);
            info.rectTransform.offsetMax = new Vector2(-(Margin + 4f) - (show ? ImprintWidth + Margin : 0f), info.rectTransform.offsetMax.y);
            if (!show) return;
            var label = imprintButton.GetComponentInChildren<Text>();
            if (arcana)
            {
                int free = CardCollection.Free(e.Arcana);
                imprintButton.interactable = free > 0;
                label.text = UIText.Get("cards.use", "n", free.ToString());
                return;
            }
            imprintButton.interactable = e != null && e.Kind == EntryKind.Ability && CardCollection.CanImprint(e.Card);
            label.text = UIText.Get("cards.imprint", "n", CardCollection.Blanks.ToString());
        }

        /// <summary>Spends a blank card on one more copy of the selected card (C, or the button), then saves.</summary>
        void ImprintSelected()
        {
            if (Picking || shown.Count == 0) return;
            var e = shown[index];
            if (e.Kind == EntryKind.Arcana) { UseArcana(e.Arcana); return; }   // the button reads Use here
            if (e.Kind != EntryKind.Ability || !CardCollection.Imprint(e.Card)) return;
            SaveGame.Save();
            Rebuild(index);   // its number, stars and bar under it change
        }

        void OpenPack()
        {
            if (Picking) { if (pickCanRemove) Pick(null); return; }
            if (mode == Mode.Index || (mode == Mode.Collection && WorldSession.Packs <= 0)) return;
            if (mode == Mode.Session && PackModeRun.Packs.Count > 0)
            {
                OpenSelected();   // O: the selected tab's pack (the Soul tab: a random one)
                return;
            }
            PackOpening.Open(root, Roster, () => Rebuild(index));
        }

        // ITEMS > CARDS > PACK MODE (a race per pack: a shelf of packs, the selected one's clock, results)

        const float SessionInfoWidth = 440f;
        float shelfWidth = 960f;
        /// <summary>The selected shelf tab: a pack's index, or Packs.Count for the Soul tab.</summary>
        int shelfTab;
        readonly List<Text> shelfLines = new List<Text>();
        readonly List<Image> shelfPlates = new List<Image>();
        /// <summary>Each tab's extra line (OPEN, its best, the packs it took, "random pack"), shown above it on hover.</summary>
        readonly List<string> shelfTips = new List<string>();
        RectTransform shelfTip;
        int shelfTipFor = -1;
        Text timerText, bestText;
        Button restartButton;

        PackKind SelectedPack { get { var packs = PackModeRun.Packs; return shelfTab >= 0 && shelfTab < packs.Count ? packs[shelfTab] : null; } }

        /// <summary>A shelf tab clicked: the first click selects it (the book shows that set), a click on the selected
        /// one opens its pack.</summary>
        void ClickShelf(int tab)
        {
            if (book.Busy || closing || opening || PackOpening.IsOpen) return;
            if (tab == shelfTab) { OpenSelected(); return; }
            shelfTab = tab;
            PackModeRun.Select(SelectedPack);   // the old pack's clock pauses
            Rebuild(0);
        }

        void OpenSelected()
        {
            if (book.Busy || closing || opening || PackOpening.IsOpen) return;
            HideShelfTip();
            var race = PackModeRun.Of(SelectedPack);
            if (race != null && race.Finished) { ShowResults(SelectedPack); return; }   // done: its results, with Retry
            // The Soul tab opens a random pack: every pack has the same Soul chance.
            PackOpening.Open(root, Roster, AfterSessionPack, SelectedPack ?? PackRates.Current.RandomKind());
        }

        /// <summary>Back from a Pack Mode opening: the book catches up, then a race that just finished shows its results.</summary>
        void AfterSessionPack()
        {
            Rebuild(index);
            var finished = PackModeRun.JustFinished;
            PackModeRun.JustFinished = null;
            if (finished != null) ShowResults(finished);
        }

        /// <summary>The results card of a finished race: its time and packs against the records, then Retry, the next
        /// pack still to race, or back to the shelf.</summary>
        void ShowResults(PackKind kind)
        {
            var race = PackModeRun.Of(kind);
            if (race == null || !race.Finished) return;
            string timeLine = UIText.Get("packmode.result.time", "time", PackModeRun.Format(race.Elapsed))
                + (race.PrevBestTime >= 0f ? "  " + UIText.Get(race.TimeRecord ? "packmode.result.ahead" : "packmode.result.behind",
                    "delta", PackModeRun.Delta(race.Elapsed, race.PrevBestTime)) : "")
                + (race.TimeRecord ? "  " + UIText.Get("packmode.record") : "");
            string packsLine = UIText.Get("packmode.result.packs", "n", race.Packs.ToString())
                + (race.PacksRecord ? "  " + UIText.Get("packmode.record") : "");
            string bestLine = UIText.Get("packmode.result.best", "time", PackModeRun.Format(PackModeRun.Records.BestTime(kind.id)),
                "packs", PackModeRun.Records.BestPacks(kind.id).ToString());
            var rows = new List<RowMenu.Row>
            {
                new RowMenu.Row { Label = UIText.Markup(timeLine), Enabled = false },
                new RowMenu.Row { Label = UIText.Markup(packsLine), Enabled = false },
                new RowMenu.Row { Label = UIText.Markup(bestLine), Enabled = false },
                new RowMenu.Row { Label = UIText.Get("packmode.result.retry"), Action = () => { picker.Hide(); RestartSelected(true); } },
            };
            int next = NextUnfinishedTab();
            if (next >= 0)
                rows.Add(new RowMenu.Row { Label = UIText.Get("packmode.result.next", "pack", PackModeRun.Packs[next].displayName),
                    Action = () => { picker.Hide(); ClickShelf(next); } });
            rows.Add(new RowMenu.Row { Label = UIText.Get("packmode.result.back"), Action = picker.Hide });
            picker.Show(UIText.Get("packmode.result.title", "pack", kind.displayName.ToUpperInvariant()), rows, picker.Hide, 3);
        }

        /// <summary>The first pack after the selected one (wrapping) whose race isn't finished; -1 if every one is.</summary>
        int NextUnfinishedTab()
        {
            var packs = PackModeRun.Packs;
            for (int k = 1; k < packs.Count; k++)
            {
                int i = (shelfTab + k) % packs.Count;
                if (packs[i] != null && !PackModeRun.Of(packs[i]).Finished) return i;
            }
            return -1;
        }

        /// <summary>R / Restart: the selected pack's race from zero, its set's cards gone from the book; with
        /// <paramref name="open"/> its first pack opens straight away.</summary>
        void RestartSelected(bool open)
        {
            var pack = SelectedPack;
            if (pack == null || book.Busy || closing || opening || PackOpening.IsOpen) return;
            if (!PackModeRun.Of(pack).Started && !open) return;   // nothing to restart
            PackModeRun.Restart(pack, Roster);
            Rebuild(0);
            if (open) OpenSelected();
        }

        /// <summary>The pack shelf: every pack in its own art with its race's progress (14/20), then once complete its
        /// time in green (a new best) or red and the packs it took. Click one to select it, again to open it.</summary>
        void BuildShelf(Vector2 at, float height)
        {
            var bottom = new Vector2(0.5f, 0f);
            var shelf = InkStyle.Panel(root, "Shelf", InkStyle.InkPanel).rectTransform;
            shelf.Place(bottom, bottom, at, new Vector2(shelfWidth, height));
            InkStyle.Frame(shelf, InkStyle.Rule, 2f);
            var packs = PackModeRun.Packs;
            int tabs = packs.Count + 1;   // every pack, then the Soul tab
            float w = shelfWidth / Mathf.Max(1, tabs);
            shelfLines.Clear();
            shelfPlates.Clear();
            shelfTips.Clear();
            for (int i = 0; i < tabs; i++)
            {
                var kind = i < packs.Count ? packs[i] : null;
                var tl = new Vector2(0f, 1f);
                var slot = UIFactory.CreateImage(shelf, kind != null ? "Pack_" + kind.id : "Soul", new Color(1f, 1f, 1f, 0f), true);
                slot.rectTransform.Place(tl, tl, new Vector2(i * w + 3f, -3f), new Vector2(w - 6f, height - 6f));
                var b = slot.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                int captured = i;
                b.onClick.AddListener(() => ClickShelf(captured));
                slot.gameObject.AddComponent<ButtonHover>();
                var hover = slot.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowShelfTip(captured));
                hover.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => HideShelfTip());
                hover.triggers.Add(exit);
                var art = UIFactory.CreateImage(slot.rectTransform, "Art", Color.white);
                art.sprite = CardArt.Picture(kind != null ? "Cards/Packs/Pack_" + kind.id : "Cards/CardBack");
                art.preserveAspect = true;
                art.raycastTarget = false;
                art.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(44f, height - 24f));
                var text = UIFactory.CreateText(slot.rectTransform, "Progress", "", UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleLeft);
                text.supportRichText = true;
                text.raycastTarget = false;
                text.lineSpacing = 1.1f;
                text.rectTransform.Fill();
                text.rectTransform.offsetMin = new Vector2(54f, 0f);
                text.rectTransform.offsetMax = new Vector2(-4f, 0f);
                // Shrinks to fit the tab ("Cinder" is the longest), never wrapping into a third line.
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = UIFactory.Px(UIFactory.TextSize.Min);
                text.resizeTextMaxSize = UIFactory.Px(UIFactory.TextSize.Medium);
                shelfLines.Add(text);
                shelfPlates.Add(slot);
                shelfTips.Add("");
            }
            RefreshShelf();
        }

        void RefreshShelf()
        {
            var packs = PackModeRun.Packs;
            string open = "{gold}" + UIText.Get("packmode.open") + "{/c}";
            for (int i = 0; i < shelfPlates.Count; i++)
            {
                bool on = i == shelfTab;
                shelfPlates[i].color = on ? UIFactory.WithAlpha(InkStyle.Gold, 0.3f) : new Color(1f, 1f, 1f, 0.03f);
                var frame = shelfPlates[i].transform.Find("SelectedFrame");
                if (on && frame == null) InkStyle.Frame(shelfPlates[i].rectTransform, InkStyle.GoldBright, 2f).name = "SelectedFrame";
                else if (!on && frame != null) Destroy(frame.gameObject);
            }
            if (shelfLines.Count > packs.Count)
            {
                // The Soul tab: Soul cards pulled; a random pack opens from it.
                int souls = 0;
                foreach (var m in Roster) if (m != null) souls += CardCollection.SoulLevel(m);
                shelfLines[packs.Count].text = UIText.Markup(UIText.Get("packmode.soul"));
                shelfTips[packs.Count] = UIText.Markup((shelfTab == packs.Count ? open + "  " : "") + UIText.Get("packmode.souls", "n", souls.ToString())
                    + "  " + UIText.Get("packmode.random"));
            }
            for (int i = 0; i < packs.Count && i < shelfLines.Count; i++)
            {
                var kind = packs[i];
                int have = PackModeRun.Have(kind, Roster), all = PackModeRun.SetOf(kind, Roster).Count;
                var race = PackModeRun.Of(kind);
                float best = PackModeRun.Records.BestTime(kind.id);
                string head = "<color=#" + ColorUtility.ToHtmlStringRGB(ResonanceInfo.Color(kind.pool)) + ">" + kind.pool + "</color>";
                string bests = best >= 0f ? UIText.Get("packmode.best", "time", PackModeRun.Format(best),
                    "packs", PackModeRun.Records.BestPacks(kind.id).ToString()) : UIText.Get("packmode.no_best");
                if (race.Finished)
                {
                    // Done: its time in green (a new best) or red, and the results on a click.
                    shelfLines[i].text = head + "\n<color=#" + ColorUtility.ToHtmlStringRGB(PackModeRun.PaceColor(kind)) + ">"
                        + PackModeRun.Format(race.Elapsed) + "</color>";
                    shelfTips[i] = UIText.Markup((i == shelfTab ? "{gold}" + UIText.Get("packmode.results") + "{/c}  " : "")
                        + "{gold}" + UIText.Get("packmode.done") + "{/c}  " + UIText.Get("packmode.packs", "n", race.Packs.ToString()) + "   " + bests);
                }
                else
                {
                    shelfLines[i].text = head + "\n" + have + "/" + all;
                    shelfTips[i] = UIText.Markup((i == shelfTab ? open + "  " : "") + bests);
                }
                shelfLines[i].text = UIText.Markup(shelfLines[i].text);
            }
            if (shelfTipFor >= 0) ShowShelfTip(shelfTipFor);   // keep a showing tip up to date
        }

        /// <summary>A tab's extra line in a small ink box just above it.</summary>
        void ShowShelfTip(int i)
        {
            HideShelfTip();
            if (i < 0 || i >= shelfPlates.Count || string.IsNullOrEmpty(shelfTips[i])) return;
            shelfTipFor = i;
            var tab = shelfPlates[i].rectTransform;
            Vector3 top = root.InverseTransformPoint(tab.TransformPoint(new Vector3(tab.rect.center.x, tab.rect.yMax, 0f)));
            shelfTip = InkStyle.Panel(root, "ShelfTip", InkStyle.InkPanel).rectTransform;
            InkStyle.Frame(shelfTip, InkStyle.Gold, 2f);
            shelfTip.anchorMin = shelfTip.anchorMax = new Vector2(0.5f, 0.5f);
            shelfTip.pivot = new Vector2(0.5f, 0f);
            var t = UIFactory.CreateText(shelfTip, "Text", shelfTips[i], UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.MiddleCenter);
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.rectTransform.Fill();
            float w = t.preferredWidth + 40f;
            var half = root.rect.size / 2f;
            shelfTip.sizeDelta = new Vector2(w, 52f);
            shelfTip.anchoredPosition = new Vector2(Mathf.Clamp(top.x, -half.x + w / 2f, half.x - w / 2f), top.y + 8f);
            foreach (var g in shelfTip.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }

        void HideShelfTip()
        {
            if (shelfTip != null) Destroy(shelfTip.gameObject);
            shelfTip = null;
            shelfTipFor = -1;
        }

        /// <summary>The selected race's clock at the top, its two records under it, and Restart beside it.</summary>
        void BuildTimer()
        {
            var tr = new Vector2(1f, 1f);
            timerText = InkStyle.Title(root, "Timer", "0:00.0", UIFactory.TextSize.Title, InkStyle.GoldBright, TextAnchor.UpperRight);
            timerText.rectTransform.Place(tr, tr, new Vector2(-60f, -34f), new Vector2(560f, 90f));
            timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            bestText = UIFactory.CreateText(root, "Best", "", UIFactory.TextSize.Medium, InkStyle.Paper, TextAnchor.UpperRight);
            bestText.rectTransform.Place(tr, tr, new Vector2(-60f, -126f), new Vector2(900f, 40f));
            bestText.horizontalOverflow = HorizontalWrapMode.Overflow;
            bestText.supportRichText = true;
            // Restart, left of the clock: the selected pack's race from zero (R).
            restartButton = InkStyle.Button(root, "Restart", UIText.Get("packmode.restart"), () => RestartSelected(false), false, UIFactory.TextSize.Medium);
            restartButton.GetComponent<RectTransform>().Place(tr, tr, new Vector2(-640f, -48f), new Vector2(220f, 60f));
        }

        void UpdateTimer()
        {
            if (timerText == null) return;
            // The selected pack's race: its clock in pace colour (green at or under its best, red past it), and just
            // its two records under it (blank dashes before the first finish). The Soul tab races nothing.
            var pack = SelectedPack;
            var race = PackModeRun.Of(pack);
            if (restartButton != null) restartButton.gameObject.SetActive(race != null && race.Started);
            if (race == null)
            {
                timerText.text = "";
                bestText.text = "";
                return;
            }
            timerText.text = PackModeRun.Format(race.Elapsed);
            timerText.color = race.Started ? PackModeRun.PaceColor(pack) : InkStyle.GoldBright;
            float bestTime = PackModeRun.Records.BestTime(pack.id);
            int bestPacks = PackModeRun.Records.BestPacks(pack.id);
            bestText.text = UIText.Get("packmode.best_pack", "time", bestTime >= 0f ? PackModeRun.Format(bestTime) : "-:--.-",
                "packs", bestPacks >= 0 ? bestPacks.ToString() : "--");
        }

        // ITEMS > CARDS > INPUT + MOTION

        void Update()
        {
            if (mode == Mode.Session) UpdateTimer();
            if (book.Busy || closing || opening) return;   // opening, turning or closing
            // A card the mouse came to rest on while the book was busy (it can't report the hover then) gets selected now.
            // Only when the hovered card changes, so the arrow keys still work with the mouse resting on a card.
            int over = HoveredIndex();
            if (over != lastHovered && over >= 0) Select(over);
            lastHovered = over;
            if (Time.frameCount == openedFrame || picker.IsOpen || picker.ClosedFrame == Time.frameCount) return;
            if (InkDropdown.IsOpen || InkDropdown.ClosedFrame == Time.frameCount) return;   // the dropdown reads its own keys
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
            if (UIKeys.MouseOn && Input.GetMouseButtonDown(1))
            {
                int under = HoveredIndex();
                if (under >= 0)
                {
                    Select(under);
                    OpenBindPicker();
                    return;
                }
            }
            if (UIKeys.Pressed(KeyCode.O)) OpenPack();
            if (mode == Mode.Session && UIKeys.Pressed(KeyCode.R)) RestartSelected(false);
            if (mode == Mode.Collection && UIKeys.Pressed(KeyCode.C)) ImprintSelected();
            if (mode == Mode.Collection && UIKeys.Pressed(KeyCode.Tab)) { SwitchToParts(); return; }
            if (UIKeys.Back || UIKeys.Pressed(KeyCode.Backspace))
            {
                if (useReturn) { EndUse(); Rebuild(0); return; }   // back out of Use to the book
                if (mode == Mode.Session) ConfirmLeave();
                else Close();
            }
        }
    }
}
