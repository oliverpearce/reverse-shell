using System.Collections.Generic;
using Combat;

namespace Game.World
{
    /// <summary>
    /// Opening card packs: <see cref="PackRates.cardsPerPack"/> cards (3), never the same card twice in one pack. Only
    /// the last card can be special; it rolls a tier (odds in <see cref="PackRates"/>):
    ///   SOUL    a Soul card for a unit below the Soul cap (raises its Ult power)
    ///   Rare    Sun, Moon or Star, of a kind found fewer times than the cap
    ///   Common  a copy of an ability card one of your units can use (every other card is one of these)
    /// A tier with nothing left to give falls through to the next. <see cref="Roll"/> only decides; <see cref="Claim"/>
    /// adds a card to the collection (the pack opening claims each card as it's revealed).
    /// </summary>
    public static class CardPacks
    {
        public enum Tier { Common, Rare, Soul, Blank }

        public class Pull
        {
            public Tier Tier;
            public CardDefinition Card;          // Common: the ability card
            public CardBinding Binding;          // Rare: Sun, Moon or Star
            public CharacterDefinition Member;   // Soul: whose Soul card
            /// <summary>Set by Claim: the card's start rank went up (Common), or the new Soul level (Soul).</summary>
            public bool RankedUp;
            /// <summary>Set by Claim: the card was already at its top level, so it became a blank card.</summary>
            public bool Blank;
            public int SoulLevelAfter;
        }

        static readonly CardBinding[] Arcana = { CardBinding.Sun, CardBinding.Moon, CardBinding.Star };

        /// <summary>One pack's cards for this roster. Caps count the pack's own earlier cards, so one pack can't give a
        /// third Sun or push a unit past the Soul cap.</summary>
        public static List<Pull> Roll(IList<CharacterDefinition> roster, System.Random rng, PackKind kind = null)
        {
            if (kind != null) return RollKind(roster, rng, kind);
            var rates = PackRates.Current;
            var pulls = new List<Pull>();
            var commons = new List<CardDefinition>();
            foreach (var member in roster)
                foreach (var card in CardCollection.PackPoolOf(member))
                    if (!commons.Contains(card)) commons.Add(card);

            var taken = new List<CardDefinition>();
            for (int i = 0; i < rates.cardsPerPack; i++)
            {
                Pull pull = null;
                // The second card: sometimes a blank card (imprint it with any card in the Cards book).
                if (i == 1 && i < rates.cardsPerPack - 1 && rng.NextDouble() < rates.blankChance) pull = new Pull { Tier = Tier.Blank };
                if (i == rates.cardsPerPack - 1)
                {
                    // The last card: the only one that can be a SOUL or rare card.
                    double roll = rng.NextDouble();
                    if (roll < rates.soulChance) pull = RollSoul(roster, pulls, rng);
                    if (pull == null && roll < rates.soulChance + rates.rareChance) pull = RollRare(pulls, rng);
                }
                if (pull == null && commons.Count > 0)
                {
                    // A card not already in this pack (repeats only if there aren't enough different cards).
                    var fresh = commons.FindAll(c => !taken.Contains(c));
                    var from = fresh.Count > 0 ? fresh : commons;
                    var card = from[rng.Next(from.Count)];
                    taken.Add(card);
                    pull = new Pull { Tier = Tier.Common, Card = card };
                }
                if (pull != null) pulls.Add(pull);
            }
            return pulls;
        }

        /// <summary>
        /// A chapter pack: each common card is from the pack's resonance (<see cref="PackKind.poolShare"/>) or the
        /// Physical cards, then of a rarity rolled with the pack's tier odds (an empty tier or pool falls back to the
        /// others). Resonance cards come even before the resonance is unlocked: collecting them is part of the chapter.
        /// </summary>
        static List<Pull> RollKind(IList<CharacterDefinition> roster, System.Random rng, PackKind kind)
        {
            var pulls = new List<Pull>();
            var resonance = new List<CardDefinition>();
            var physical = new List<CardDefinition>();
            foreach (var member in roster)
                foreach (var card in CardCollection.AbilitiesOf(member))
                {
                    if (card.pool == Resonance.Physical) { if (!physical.Contains(card)) physical.Add(card); }
                    else if (card.pool == kind.pool && !resonance.Contains(card)) resonance.Add(card);
                }

            var taken = new List<CardDefinition>();
            int count = UnityEngine.Mathf.Max(1, kind.cardsPerPack);
            for (int i = 0; i < count; i++)
            {
                Pull pull = null;
                if (i == 1 && i < count - 1 && rng.NextDouble() < kind.blankChance) pull = new Pull { Tier = Tier.Blank };
                if (i == count - 1)
                {
                    double roll = rng.NextDouble();
                    if (roll < kind.soulChance) pull = RollSoul(roster, pulls, rng);
                    if (pull == null && roll < kind.soulChance + kind.arcanaChance) pull = RollRare(pulls, rng);
                }
                if (pull == null)
                {
                    bool fromResonance = resonance.Count > 0 && (physical.Count == 0 || rng.NextDouble() < kind.poolShare);
                    var card = PickCommon(fromResonance ? resonance : physical, fromResonance ? physical : resonance, taken, RollRarity(kind, rng));
                    if (card != null)
                    {
                        taken.Add(card);
                        pull = new Pull { Tier = Tier.Common, Card = card };
                    }
                }
                if (pull != null) pulls.Add(pull);
            }
            return pulls;
        }

        static CardRarity RollRarity(PackKind kind, System.Random rng)
        {
            float total = kind.commonShare + kind.rareShare + kind.epicShare;
            if (total <= 0f) return CardRarity.Common;
            double roll = rng.NextDouble() * total;
            if (roll < kind.commonShare) return CardRarity.Common;
            return roll < kind.commonShare + kind.rareShare ? CardRarity.Rare : CardRarity.Epic;
        }

        /// <summary>A card of <paramref name="rarity"/> not in the pack yet, from <paramref name="pool"/>; falling back to
        /// any rarity, then the other pool, then repeats.</summary>
        static CardDefinition PickCommon(List<CardDefinition> pool, List<CardDefinition> other, List<CardDefinition> taken, CardRarity rarity)
        {
            foreach (var from in new[] { pool, other })
            {
                var fresh = from.FindAll(c => !taken.Contains(c));
                var tier = fresh.FindAll(c => c.rarity == rarity);
                var list = tier.Count > 0 ? tier : fresh;
                if (list.Count > 0) return list[Rng.Next(list.Count)];
            }
            var any = pool.Count > 0 ? pool : other;
            return any.Count > 0 ? any[Rng.Next(any.Count)] : null;
        }

        static readonly System.Random Rng = new System.Random();

        static Pull RollSoul(IList<CharacterDefinition> roster, List<Pull> already, System.Random rng)
        {
            var open = new List<CharacterDefinition>();
            foreach (var member in roster)
            {
                if (member == null || member.ritual == null) continue;
                int pending = already.FindAll(p => p.Tier == Tier.Soul && p.Member == member).Count;
                if (!CardCollection.CapsApply || CardCollection.SoulLevel(member) + pending < PackRates.Current.maxSoulLevel) open.Add(member);
            }
            return open.Count == 0 ? null : new Pull { Tier = Tier.Soul, Member = open[rng.Next(open.Count)] };
        }

        static Pull RollRare(List<Pull> already, System.Random rng)
        {
            var open = new List<CardBinding>();
            foreach (var kind in Arcana)
            {
                int pending = already.FindAll(p => p.Tier == Tier.Rare && p.Binding == kind).Count;
                if (!CardCollection.CapsApply || CardCollection.Found(kind) + pending < PackRates.Current.maxPerBinding) open.Add(kind);
            }
            return open.Count == 0 ? null : new Pull { Tier = Tier.Rare, Binding = open[rng.Next(open.Count)] };
        }

        /// <summary>Adds a pulled card to the collection (and notes what it changed on the pull).</summary>
        public static void Claim(Pull pull)
        {
            if (CardCollection.InSession) CardCollection.Current.Record(pull);
            switch (pull.Tier)
            {
                case Tier.Common:
                    int before = CardCollection.Upgrades(pull.Card), copies = CardCollection.Copies(pull.Card);
                    CardCollection.AddCopy(pull.Card);
                    pull.RankedUp = CardCollection.Upgrades(pull.Card) > before;
                    pull.Blank = CardCollection.Copies(pull.Card) == copies;   // already at the top: it became a blank card
                    break;
                case Tier.Rare:
                    CardCollection.AddFound(pull.Binding);
                    CardCollection.MarkNew(CardCollection.ArcanaKey(pull.Binding));
                    break;
                case Tier.Blank:
                    CardCollection.AddBlank();
                    break;
                case Tier.Soul:
                    CardCollection.MarkNew(CardCollection.SoulKey(pull.Member));
                    CardCollection.AddSoul(pull.Member);
                    pull.SoulLevelAfter = CardCollection.SoulLevel(pull.Member);
                    break;
            }
        }

        /// <summary>Uses up one unopened pack and says which kind it is (null = no packs set up). False if there are none.
        /// Card Pack mode never runs out and opens a random kind each time.</summary>
        public static bool TakePack(out PackKind kind)
        {
            var rates = PackRates.Current;
            kind = null;
            if (CardCollection.InSession)
            {
                CardCollection.Current.PacksOpened++;
                if (rates.packs.Count > 0) kind = rates.packs[Rng.Next(rates.packs.Count)];
                return true;
            }
            var id = WorldSession.TakePack();
            if (id == null) return false;
            kind = rates.Pack(id) ?? rates.PackForArea(WorldSession.CurrentArea);
            return true;
        }
    }
}
