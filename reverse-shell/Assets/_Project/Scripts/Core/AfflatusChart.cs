namespace Combat
{
    /// <summary>
    /// Afflatus advantage wheel: Beast > Plant > Star > Mineral > Beast.
    /// Spirit and Intellect are each strong against the other.
    /// </summary>
    public static class AfflatusChart
    {
        public static bool Beats(Afflatus attacker, Afflatus defender)
        {
            switch (attacker)
            {
                case Afflatus.Beast: return defender == Afflatus.Plant;
                case Afflatus.Plant: return defender == Afflatus.Star;
                case Afflatus.Star: return defender == Afflatus.Mineral;
                case Afflatus.Mineral: return defender == Afflatus.Beast;
                case Afflatus.Spirit: return defender == Afflatus.Intellect;
                case Afflatus.Intellect: return defender == Afflatus.Spirit;
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
