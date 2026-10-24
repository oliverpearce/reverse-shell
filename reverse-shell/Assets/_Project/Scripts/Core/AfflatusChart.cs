namespace Combat
{
    /// <summary>
    /// Element advantage wheel: Volt > Fire > Toxin > Earth > Volt (a surge overloads the furnace, fire purges toxin,
    /// toxin blights the earth, earth grounds the current). Light and Void are each strong against the other.
    /// </summary>
    public static class AfflatusChart
    {
        public static bool Beats(Afflatus attacker, Afflatus defender)
        {
            switch (attacker)
            {
                case Afflatus.Toxin: return defender == Afflatus.Earth;
                case Afflatus.Earth: return defender == Afflatus.Volt;
                case Afflatus.Volt: return defender == Afflatus.Fire;
                case Afflatus.Fire: return defender == Afflatus.Toxin;
                case Afflatus.Light: return defender == Afflatus.Void;
                case Afflatus.Void: return defender == Afflatus.Light;
            }
            return false;
        }

        public static AfflatusRelation Relation(Afflatus attacker, Afflatus defender)
        {
            if (Beats(attacker, defender)) return AfflatusRelation.Advantage;
            if (Beats(defender, attacker)) return AfflatusRelation.Disadvantage;
            return AfflatusRelation.Neutral;
        }
    }
}
