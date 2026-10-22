namespace Combat
{
    /// <summary>An immutable card in hand/queue. Merging creates a new instance one rank higher.</summary>
    public class CardInstance
    {
        static int nextId;

        public readonly int Id;
        public readonly CardDefinition Definition;
        public readonly BattleUnit Owner;
        public readonly int Rank;

        public CardInstance(CardDefinition definition, BattleUnit owner, int rank)
        {
            Id = ++nextId;
            Definition = definition;
            Owner = owner;
            Rank = rank;
        }

        public string Name { get { return Definition.DisplayName; } }
        public bool IsRitual { get { return Definition.isRitual; } }
        public CardRank RankData { get { return Definition.GetRank(Rank); } }

        public bool CanMergeWith(CardInstance other)
        {
            return other != null
                   && !IsRitual && !other.IsRitual
                   && other.Definition == Definition
                   && other.Owner == Owner
                   && other.Rank == Rank
                   && Rank < Definition.MaxRank;
        }

        public string Describe()
        {
            return CardText.ForRank(Definition, Rank);
        }

        public override string ToString() { return Name + " R" + Rank + " (" + Owner + ")"; }
    }
}
