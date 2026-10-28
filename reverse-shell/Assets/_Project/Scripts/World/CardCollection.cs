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
        static void HookTranslation() { Combat.View.CardTranslation.Check = () => Translated; }

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

        public static void AddCopy(CardDefinition card, int count = 1)
        {
            if (card == null || count <= 0) return;
            CopiesStore[card.name] = Copies(card) + count;
        }

        /// <summary>The rank this card starts battles at (I-III), from its copies.</summary>
        public static int StartRank(CardDefinition card)
        {
            return card == null ? 1 : UnityEngine.Mathf.Clamp(Rates.StartRank(Copies(card)), 1, card.MaxRank);
        }

        /// <summary>Copies still needed for the next start rank, or 0 at the top.</summary>
        public static int CopiesToNextRank(CardDefinition card)
        {
            int copies = Copies(card), rank = StartRank(card);
            if (card == null || rank >= card.MaxRank) return 0;
            return (rank == 1 ? Rates.copiesForRankII : Rates.copiesForRankIII) - copies;
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
                var second = Rates.startWithSecondCard ? SecondCard(member) : null;
                if (second != null && Rates.extraDeckSlots > 0) list.Add(second);
                return list;
            }
            foreach (var name in progress.ExtraCards)
            {
                if (list.Count >= Rates.extraDeckSlots) break;
                var card = Find(member, name);
                if (card != null && card != locked && !list.Contains(card)) list.Add(card);
            }
            return list;
        }

        /// <summary>The extra card in slot <paramref name="slot"/>, or null if it's empty.</summary>
        public static CardDefinition ExtraIn(CharacterDefinition member, int slot)
        {
            var extras = Extras(member);
            return slot >= 0 && slot < extras.Count ? extras[slot] : null;
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

        /// <summary>A card you have: someone's locked card (or default second card), or at least one copy from packs.</summary>
        public static bool Owned(CardDefinition card, IList<CharacterDefinition> roster)
        {
            if (card == null) return false;
            if (Copies(card) > 0) return true;
            foreach (var m in roster)
                if (m != null && (LockedCard(m) == card || (Rates.startWithSecondCard && SecondCard(m) == card))) return true;
            return false;
        }

        /// <summary>What can go in a unit's extra slot: every party ability card you own except its locked card, its own
        /// cards first.</summary>
        public static List<CardDefinition> Eligible(CharacterDefinition member, IList<CharacterDefinition> roster)
        {
            var list = new List<CardDefinition>();
            var locked = LockedCard(member);
            System.Action<CardDefinition> add = c => { if (c != locked && !list.Contains(c) && Owned(c, roster)) list.Add(c); };
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

        /// <summary>Fills a loadout's start ranks, bindings and Soul level for this member.</summary>
        public static void FillLoadout(CharacterDefinition member, Loadout loadout)
        {
            foreach (var card in AbilitiesOf(member))
            {
                int rank = StartRank(card);
                if (rank > 1) loadout.StartRanks[card] = rank;
                var binding = BindingOf(card);
                if (binding != CardBinding.None) loadout.Bindings[card] = binding;
            }
            loadout.SoulLevel = SoulLevel(member);
        }
    }
}
