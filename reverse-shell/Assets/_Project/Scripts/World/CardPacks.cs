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
        public enum Tier { Common, Rare, Soul }

        public class Pull
        {
            public Tier Tier;
            public CardDefinition Card;          // Common: the ability card
            public CardBinding Binding;          // Rare: Sun, Moon or Star
            public CharacterDefinition Member;   // Soul: whose Soul card
            /// <summary>Set by Claim: the card's start rank went up (Common), or the new Soul level (Soul).</summary>
            public bool RankedUp;
            public int SoulLevelAfter;
        }

        static readonly CardBinding[] Arcana = { CardBinding.Sun, CardBinding.Moon, CardBinding.Star };

        /// <summary>One pack's cards for this roster. Caps count the pack's own earlier cards, so one pack can't give a
        /// third Sun or push a unit past the Soul cap.</summary>
        public static List<Pull> Roll(IList<CharacterDefinition> roster, System.Random rng)
        {
            var rates = PackRates.Current;
            var pulls = new List<Pull>();
            var commons = new List<CardDefinition>();
            foreach (var member in roster)
                foreach (var card in CardCollection.AbilitiesOf(member))
                    if (!commons.Contains(card)) commons.Add(card);

            var taken = new List<CardDefinition>();
            for (int i = 0; i < rates.cardsPerPack; i++)
            {
                Pull pull = null;
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
                    int before = CardCollection.StartRank(pull.Card);
                    CardCollection.AddCopy(pull.Card);
                    pull.RankedUp = CardCollection.StartRank(pull.Card) > before;
                    break;
                case Tier.Rare:
                    CardCollection.AddFound(pull.Binding);
                    break;
                case Tier.Soul:
                    CardCollection.AddSoul(pull.Member);
                    pull.SoulLevelAfter = CardCollection.SoulLevel(pull.Member);
                    break;
            }
        }

        /// <summary>Uses up one unopened pack. False if there are none. Card Pack mode never runs out.</summary>
        public static bool TakePack()
        {
            if (CardCollection.InSession)
            {
                CardCollection.Current.PacksOpened++;
                return true;
            }
            if (WorldSession.Packs <= 0) return false;
            WorldSession.Packs--;
            return true;
        }
    }
}
