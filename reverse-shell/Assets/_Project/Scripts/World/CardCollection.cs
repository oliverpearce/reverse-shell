using System.Collections.Generic;
using Combat;

namespace Game.World
{
    /// <summary>
    /// The card collection's rules over the saved state in <see cref="WorldSession"/>: copies of ability cards (each
    /// card in a deck is owned already; copies are extras, and enough of them make the card start battles at a higher
    /// rank), Sun / Moon / Star bindings (found from packs, at most PackRates.maxPerBinding of each ever; one per card),
    /// unopened packs, and each unit's Soul level. Every number comes from <see cref="PackRates"/>.
    /// Card Pack mode (title screen) runs a <see cref="BeginSession"/>: until <see cref="EndSession"/>, everything here
    /// reads and writes a throwaway in-memory collection instead (no caps, unlimited packs, never saved).
    /// </summary>
    public static class CardCollection
    {
        /// <summary>The title screen's Cards book is open (it always reads, like Pack Mode).</summary>
        public static bool IndexOpen;

        /// <summary>Card words read with the language chip (Chapter One), in the Sandbox, in Pack Mode and in the title
        /// screen's Cards book; otherwise they're runes (<see cref="Combat.View.CardTranslation"/>).</summary>
        public static bool Translated
        {
            get { return IndexOpen || InSession || WorldSession.InSandbox || StoryState.Has(Chapter1.LanguageChip); }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookTranslation()
        {
            Combat.View.CardTranslation.Check = () => Translated;
            Combat.View.CardTranslation.NumbersCheck = () => IndexOpen || InSession || WorldSession.InSandbox || StoryState.Has(Chapter1.NumbersChip);
            Combat.View.CardTranslation.ColoursCheck = () => IndexOpen || InSession || WorldSession.InSandbox || StoryState.Has(Chapter1.ColoursChip);
        }

        static PackRates Rates { get { return PackRates.Current; } }

        // CARD COLLECTION > SESSION (Card Pack mode)

        /// <summary>A throwaway collection and what was pulled into it.</summary>
        public class Session
        {
            public readonly Dictionary<string, int> Copies = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Found = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Souls = new Dictionary<string, int>();
            public int PacksOpened;
            public readonly Dictionary<CharacterDefinition, int> SoulPulls = new Dictionary<CharacterDefinition, int>();
            public readonly Dictionary<CardBinding, int> RarePulls = new Dictionary<CardBinding, int>();
            public readonly Dictionary<CardDefinition, int> CommonPulls = new Dictionary<CardDefinition, int>();
            public int BlankPulls;

            public int CardsPulled
            {
                get
                {
                    int n = 0;
                    foreach (var v in SoulPulls.Values) n += v;
                    foreach (var v in RarePulls.Values) n += v;
                    foreach (var v in CommonPulls.Values) n += v;
                    return n;
                }
            }

            public void Record(CardPacks.Pull pull)
            {
                switch (pull.Tier)
                {
                    case CardPacks.Tier.Soul: Bump(SoulPulls, pull.Member); break;
                    case CardPacks.Tier.Rare: Bump(RarePulls, pull.Binding); break;
                    case CardPacks.Tier.Blank: BlankPulls++; break;
                    default: Bump(CommonPulls, pull.Card); break;
                }
            }

            static void Bump<T>(Dictionary<T, int> counts, T key)
            {
                int n;
                counts.TryGetValue(key, out n);
                counts[key] = n + 1;
            }
        }

        static Session session;

        /// <summary>Card Pack mode is on (the collection is a throwaway one).</summary>
        public static bool InSession { get { return session != null; } }
        /// <summary>The current session, or null.</summary>
        public static Session Current { get { return session; } }
        /// <summary>Soul / Sun / Moon / Star limits apply (everywhere but Card Pack mode).</summary>
        public static bool CapsApply { get { return session == null; } }

        public static void BeginSession() { session = new Session(); }
        public static void EndSession() { session = null; }

        /// <summary>The rarest pull of the session: a Soul card (the unit pulled most), else the rare pulled most, else
        /// the common with the most copies. False if nothing was pulled.</summary>
        public static bool Rarest(out CardPacks.Tier tier, out CharacterDefinition member, out CardBinding binding,
            out CardDefinition card, out int count)
        {
            tier = CardPacks.Tier.Common; member = null; binding = CardBinding.None; card = null; count = 0;
            if (session == null) return false;
            foreach (var e in session.SoulPulls) if (e.Value > count) { tier = CardPacks.Tier.Soul; member = e.Key; count = e.Value; }
            if (count > 0) return true;
            foreach (var e in session.RarePulls) if (e.Value > count) { tier = CardPacks.Tier.Rare; binding = e.Key; count = e.Value; }
            if (count > 0) return true;
            foreach (var e in session.CommonPulls) if (e.Value > count) { tier = CardPacks.Tier.Common; card = e.Key; count = e.Value; }
            return count > 0;
        }

        static Dictionary<string, int> CopiesStore { get { return session != null ? session.Copies : WorldSession.CardCopies; } }
        static Dictionary<string, int> FoundStore { get { return session != null ? session.Found : WorldSession.BindingsFound; } }

        // CARD COLLECTION > COPIES

        public static int Copies(CardDefinition card)
        {
            int n;
            return card != null && CopiesStore.TryGetValue(card.name, out n) ? n : 0;
        }

        /// <summary>Adds copies up to <see cref="MaxCopies"/>; copies past that become blank cards instead.</summary>
        public static void AddCopy(CardDefinition card, int count = 1)
        {
            if (card == null || count <= 0) return;
            int have = Copies(card);
            int add = UnityEngine.Mathf.Min(count, UnityEngine.Mathf.Max(0, MaxCopies - have));
            if (add > 0) CopiesStore[card.name] = have + add;
            if (count > add) CopiesStore[BlankKey] = Blanks + count - add;
            MarkNew(card.name);
        }

        // CARD COLLECTION > BLANK CARDS
        // Copies of a card past its top level (35) become blank cards; a blank can be imprinted with any card that isn't
        // at the top yet, as one more copy of it. Kept in the copies store under BlankKey, so they save with the cards.

        const string BlankKey = "#Blank";

        /// <summary>Copies that give every upgrade (35); more become blank cards.</summary>
        public static int MaxCopies { get { return Rates.CopiesFor(Rates.MaxUpgrades); } }

        public static int Blanks
        {
            get
            {
                int n;
                return CopiesStore.TryGetValue(BlankKey, out n) ? n : 0;
            }
        }

        /// <summary>One more blank card (a pack's second card, sometimes).</summary>
        public static void AddBlank() { CopiesStore[BlankKey] = Blanks + 1; }

        /// <summary>Whether a blank card can be imprinted with <paramref name="card"/> (one to spend, card not maxed).</summary>
        public static bool CanImprint(CardDefinition card) { return card != null && Blanks > 0 && Copies(card) < MaxCopies; }

        /// <summary>Spends a blank card on one more copy of <paramref name="card"/>. False if it can't.</summary>
        public static bool Imprint(CardDefinition card)
        {
            if (!CanImprint(card)) return false;
            CopiesStore[BlankKey] = Blanks - 1;
            CopiesStore[card.name] = Copies(card) + 1;
            return true;
        }

        // CARD COLLECTION > NEW (cards pulled but not looked at yet in the book; this play session only)

        static readonly HashSet<string> fresh = new HashSet<string>();

        public static string SoulKey(CharacterDefinition member) { return member == null ? "" : "Soul:" + member.name; }
        public static string ArcanaKey(CardBinding kind) { return "Arcana:" + kind; }

        public static void MarkNew(string key) { if (!string.IsNullOrEmpty(key)) fresh.Add(key); }
        public static bool IsNew(string key) { return !string.IsNullOrEmpty(key) && fresh.Contains(key); }
        public static void Seen(string key) { fresh.Remove(key); }

        /// <summary>This card's copy upgrades (0-3), from its copies: +2 card levels each in battle.</summary>
        public static int Upgrades(CardDefinition card)
        {
            return card == null ? 0 : Rates.Upgrades(Copies(card));
        }

        /// <summary>The card's upgrade level as the book shows it: Lv 1 (none) to Lv 4 (every upgrade).</summary>
        public static int UpgradeLevel(CardDefinition card) { return 1 + Upgrades(card); }

        /// <summary>The top <see cref="UpgradeLevel"/> (4).</summary>
        public static int MaxUpgradeLevel { get { return 1 + Rates.MaxUpgrades; } }

        /// <summary>Copies still needed for the next upgrade, or 0 at the top.</summary>
        public static int CopiesToNextUpgrade(CardDefinition card)
        {
            int n = Upgrades(card);
            if (card == null || n >= Rates.MaxUpgrades) return 0;
            return Rates.CopiesFor(n + 1) - Copies(card);
        }

        // CARD COLLECTION > BINDINGS (Sun, Moon, Star)

        /// <summary>How many of this kind have ever been found (never more than the cap).</summary>
        public static int Found(CardBinding kind)
        {
            int n;
            return FoundStore.TryGetValue(kind.ToString(), out n) ? n : 0;
        }

        public static bool CanFind(CardBinding kind) { return kind != CardBinding.None && (!CapsApply || Found(kind) < Rates.maxPerBinding); }

        /// <summary>A pack gave one of this kind. False (nothing changes) once the cap is reached.</summary>
        public static bool AddFound(CardBinding kind)
        {
            if (!CanFind(kind)) return false;
            FoundStore[kind.ToString()] = Found(kind) + 1;
            return true;
        }

        public static CardBinding BindingOf(CardDefinition card)
        {
            string kind;
            CardBinding parsed;
            if (session != null) return CardBinding.None;   // Card Pack mode has no bindings
            return card != null && WorldSession.Bindings.TryGetValue(card.name, out kind) && System.Enum.TryParse(kind, out parsed)
                ? parsed : CardBinding.None;
        }

        /// <summary>How many of this kind are on cards right now.</summary>
        public static int InUse(CardBinding kind)
        {
            int n = 0;
            foreach (var entry in WorldSession.Bindings) if (entry.Value == kind.ToString()) n++;
            return n;
        }

        public static int Free(CardBinding kind) { return Found(kind) - InUse(kind); }

        /// <summary>Binds <paramref name="kind"/> onto the card (replacing what it had). False if none of that kind is free.</summary>
        public static bool Bind(CardDefinition card, CardBinding kind)
        {
            if (card == null || card.isRitual || session != null) return false;
            if (kind == CardBinding.None) { Unbind(card); return true; }
            if (BindingOf(card) == kind) return true;
            if (Free(kind) <= 0) return false;
            WorldSession.Bindings[card.name] = kind.ToString();
            return true;
        }

        public static void Unbind(CardDefinition card)
        {
            if (card != null) WorldSession.Bindings.Remove(card.name);
        }

        // CARD COLLECTION > SOUL LEVELS

        public static int SoulLevel(CharacterDefinition member)
        {
            if (member == null) return 0;
            if (session != null)
            {
                int n;
                return session.Souls.TryGetValue(member.name, out n) ? n : 0;
            }
            return WorldSession.ProgressOf(member).SoulLevel;
        }

        public static bool CanAddSoul(CharacterDefinition member) { return member != null && (!CapsApply || SoulLevel(member) < Rates.maxSoulLevel); }

        /// <summary>A Soul card for this unit: +1 Soul level, up to the cap. False if already at the cap.</summary>
        public static bool AddSoul(CharacterDefinition member)
        {
            if (!CanAddSoul(member)) return false;
            if (session != null) session.Souls[member.name] = SoulLevel(member) + 1;
            else WorldSession.ProgressOf(member).SoulLevel++;
            return true;
        }

        // CARD COLLECTION > DECKS
        // A unit's deck: its locked card (its leftmost card, from the active resonance kit if it has cards) plus up to
        // PackRates.extraDeckSlots extra cards the player chooses (empty by default); its Soul card is separate and
        // always comes too.

        static readonly Dictionary<string, CardDefinition> known = new Dictionary<string, CardDefinition>();

        /// <summary>Teaches the collection every playable character's cards, so saved deck choices can be looked up by name
        /// (the overworld calls this with the whole cast).</summary>
        public static void Register(IEnumerable<CharacterDefinition> characters)
        {
            foreach (var c in characters)
                if (c != null) foreach (var card in AbilitiesOf(c)) known[card.name] = card;
        }

        static CardDefinition Find(CharacterDefinition member, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var own = AbilitiesOf(member).Find(c => c.name == name);
            if (own != null) return own;
            CardDefinition card;
            return known.TryGetValue(name, out card) ? card : null;
        }

        static List<CardDefinition> BaseCards(CharacterDefinition member)
        {
            var kit = WorldSession.ActiveKit(member);
            if (kit != null && kit.cards.Exists(c => c != null)) return kit.cards.FindAll(c => c != null);
            return member.incantations.FindAll(c => c != null);
        }

        /// <summary>The card a unit always brings: its leftmost card (the active resonance kit's, else its own).</summary>
        public static CardDefinition LockedCard(CharacterDefinition member)
        {
            if (member == null) return null;
            var cards = BaseCards(member);
            return cards.Count > 0 ? cards[0] : null;
        }

        static CardDefinition SecondCard(CharacterDefinition member)
        {
            var cards = BaseCards(member);
            return cards.Count > 1 ? cards[1] : null;
        }

        /// <summary>The extra cards in the deck: the player's choice once made; before that, nothing (or the second card,
        /// with PackRates.startWithSecondCard).</summary>
        public static List<CardDefinition> Extras(CharacterDefinition member)
        {
            var list = new List<CardDefinition>();
            if (member == null) return list;
            var progress = WorldSession.ProgressOf(member);
            var locked = LockedCard(member);
            if (!progress.ExtrasChosen)
            {
                // The starter cards fill the Alt slots until the player picks others.
                foreach (var starter in member.starterCards)
                    if (starter != null && starter != locked && !list.Contains(starter) && list.Count < Rates.extraDeckSlots && UsableNow(member, starter))
                        list.Add(starter);
                if (list.Count > 0) return list;
                var second = Rates.startWithSecondCard ? SecondCard(member) : null;
                if (second != null && Rates.extraDeckSlots > 0) list.Add(second);
                return list;
            }
            foreach (var name in progress.ExtraCards)
            {
                if (list.Count >= Rates.extraDeckSlots) break;
                var card = Find(member, name);
                if (card != null && card != locked && !list.Contains(card) && UsableNow(member, card)) list.Add(card);
            }
            return list;
        }

        /// <summary>
        /// Whether <paramref name="member"/> can bring <paramref name="card"/> under its active resonance: a resonance
        /// kit's cards only while that resonance is active (bible v0.6: decks are the active resonance plus Basic);
        /// Basic cards (the card pool) and other members' cards always. A saved Alt from another resonance just
        /// sits out until you switch back.
        /// </summary>
        public static bool UsableNow(CharacterDefinition member, CardDefinition card)
        {
            if (member == null || card == null) return true;
            if (member.cardPool.Contains(card) || member.incantations.Contains(card)) return true;
            var active = WorldSession.ActiveKit(member);
            if (active != null && active.cards.Contains(card)) return true;
            foreach (var kit in member.resonances)
                if (kit != null && kit != active && kit.cards.Contains(card)) return false;
            return true;
        }

        /// <summary>The extra card in slot <paramref name="slot"/>, or null if it's empty.</summary>
        public static CardDefinition ExtraIn(CharacterDefinition member, int slot)
        {
            var extras = Extras(member);
            return slot >= 0 && slot < extras.Count ? extras[slot] : null;
        }

        /// <summary>
        /// Puts <paramref name="card"/> in an extra slot for <paramref name="member"/>, taking it out of any other unit's
        /// deck in <paramref name="roster"/> first: a card is only ever in one unit's deck at a time.
        /// </summary>
        public static void SetExtra(CharacterDefinition member, int slot, CardDefinition card, IList<CharacterDefinition> roster)
        {
            if (card != null && roster != null)
                foreach (var other in roster)
                {
                    if (other == null || other == member || !Extras(other).Contains(card)) continue;
                    var keep = Extras(other).ConvertAll(c => c.name);
                    keep.Remove(card.name);
                    var progress = WorldSession.ProgressOf(other);
                    progress.ExtraCards = keep;
                    progress.ExtrasChosen = true;
                }
            SetExtra(member, slot, card);
        }

        /// <summary>
        /// Cleans up a loaded save so no card is in two decks: every unit keeps its innate card; going through
        /// <paramref name="cast"/> in party order, each keeps the Alts nobody before it has, and loses the rest (its slot
        /// goes empty). Safe to run every time (it changes nothing once the decks are clean).
        /// </summary>
        public static void EnforceOneDeck(IList<CharacterDefinition> cast)
        {
            if (cast == null) return;
            var taken = new HashSet<CardDefinition>();
            foreach (var m in cast) if (m != null && LockedCard(m) != null) taken.Add(LockedCard(m));
            foreach (var m in cast)
            {
                if (m == null) continue;
                var extras = Extras(m);
                var keep = extras.FindAll(c => !taken.Contains(c));
                foreach (var c in keep) taken.Add(c);
                if (keep.Count == extras.Count) continue;
                var progress = WorldSession.ProgressOf(m);
                progress.ExtraCards = keep.ConvertAll(c => c.name);
                progress.ExtrasChosen = true;
            }
        }

        /// <summary>Puts <paramref name="card"/> in an extra slot (null = Remove: the slot goes empty).</summary>
        public static void SetExtra(CharacterDefinition member, int slot, CardDefinition card)
        {
            if (member == null || slot < 0 || slot >= Rates.extraDeckSlots) return;
            var progress = WorldSession.ProgressOf(member);
            var current = Extras(member).ConvertAll(c => c.name);   // keep what's showing (the default) when first edited
            while (current.Count <= slot) current.Add("");
            current[slot] = card != null ? card.name : "";
            progress.ExtraCards = current.FindAll(n => !string.IsNullOrEmpty(n));
            progress.ExtrasChosen = true;
        }

        /// <summary>A card you have: a unit's innate card in use (its current Main), a starter card, or at least one copy
        /// from packs. Other innate cards (the other resonances', companions') come from packs like any card.</summary>
        public static bool Owned(CardDefinition card, IList<CharacterDefinition> roster)
        {
            if (card == null) return false;
            if (Copies(card) > 0) return true;
            foreach (var m in roster)
                if (m != null && (LockedCard(m) == card || m.starterCards.Contains(card) || (Rates.startWithSecondCard && SecondCard(m) == card))) return true;
            return false;
        }

        /// <summary>Who has <paramref name="card"/> in their deck right now (as their innate card or an Alt).</summary>
        public static List<CharacterDefinition> EquippedBy(CardDefinition card, IList<CharacterDefinition> roster)
        {
            var list = new List<CharacterDefinition>();
            if (card == null || roster == null) return list;
            foreach (var m in roster)
                if (m != null && !list.Contains(m) && (LockedCard(m) == card || Extras(m).Contains(card))) list.Add(m);
            return list;
        }

        /// <summary>
        /// Whether an innate card's Soul form can be seen yet: a resonance's innate once that resonance is unlocked (any
        /// in Sandbox Mode), a companion's once you've met them. Until then it shows as "???".
        /// </summary>
        public static bool SoulFormKnown(CharacterDefinition member, CardDefinition card)
        {
            if (member == null || card == null) return false;
            if (WorldSession.InSandbox) return true;
            foreach (var kit in member.resonances)
                if (kit != null && kit.cards.Count > 0 && kit.cards[0] == card) return WorldSession.AvailableResonances(member).Contains(kit.element);
            return WorldSession.Members.Count == 0 || WorldSession.Members.Contains(member.name);
        }

        /// <summary>What can go in a unit's extra slot: every party ability card you own except its locked card, its own
        /// cards first.</summary>
        public static List<CardDefinition> Eligible(CharacterDefinition member, IList<CharacterDefinition> roster)
        {
            var list = new List<CardDefinition>();
            var locked = LockedCard(member);
            // Another unit's innate card is theirs alone (an Alt in another deck can be taken: SetExtra moves it).
            System.Action<CardDefinition> add = c =>
            {
                if (c == locked || list.Contains(c) || !Owned(c, roster) || !UsableNow(member, c)) return;
                foreach (var m in roster) if (m != null && m != member && LockedCard(m) == c) return;
                list.Add(c);
            };
            foreach (var c in AbilitiesOf(member)) add(c);
            foreach (var m in roster)
                if (m != null && m != member) foreach (var c in AbilitiesOf(m)) add(c);
            return list;
        }

        // CARD COLLECTION > WHAT A UNIT BRINGS

        /// <summary>Every ability card a unit can use (its deck, card pool and every resonance kit), no Ults, no repeats.</summary>
        public static List<CardDefinition> AbilitiesOf(CharacterDefinition member)
        {
            var list = new List<CardDefinition>();
            if (member == null) return list;
            System.Action<CardDefinition> add = c => { if (c != null && !c.isRitual && !list.Contains(c)) list.Add(c); };
            foreach (var c in member.incantations) add(c);
            foreach (var c in member.cardPool) add(c);
            foreach (var kit in member.resonances)
                if (kit != null) foreach (var c in kit.cards) add(c);
            return list;
        }

        /// <summary>The Soul form of a unit's innate card: the Ult of the resonance kit whose Main card it is, or (a unit
        /// without kits) its own Ult when it's its first card. Null for every other card.</summary>
        public static CardDefinition UltFormOf(CharacterDefinition member, CardDefinition card)
        {
            if (member == null || card == null) return null;
            foreach (var kit in member.resonances)
                if (kit != null && kit.cards.Count > 0 && kit.cards[0] == card) return kit.ultimate;
            if (member.resonances.Count == 0 && member.incantations.Count > 0 && member.incantations[0] == card) return member.ritual;
            return null;
        }

        /// <summary>The ability cards a unit's packs can give: its card pool, its own cards and the kits of resonances it
        /// has unlocked (so locked resonances' cards don't drop early).</summary>
        public static List<CardDefinition> PackPoolOf(CharacterDefinition member)
        {
            var list = new List<CardDefinition>();
            if (member == null) return list;
            System.Action<CardDefinition> add = c => { if (c != null && !c.isRitual && !list.Contains(c)) list.Add(c); };
            foreach (var c in member.incantations) add(c);
            foreach (var c in member.cardPool) add(c);
            var open = WorldSession.AvailableResonances(member);
            foreach (var kit in member.resonances)
                if (kit != null && (open.Count == 0 || open.Contains(kit.element))) foreach (var c in kit.cards) add(c);
            return list;
        }

        /// <summary>Fills a loadout's copy upgrades, bindings and Soul level for this member.</summary>
        public static void FillLoadout(CharacterDefinition member, Loadout loadout)
        {
            foreach (var card in AbilitiesOf(member))
            {
                int upgrades = Upgrades(card);
                if (upgrades > 0) loadout.Upgrades[card] = upgrades;
                var binding = BindingOf(card);
                if (binding != CardBinding.None) loadout.Bindings[card] = binding;
            }
            loadout.SoulLevel = SoulLevel(member);
        }
    }
}
