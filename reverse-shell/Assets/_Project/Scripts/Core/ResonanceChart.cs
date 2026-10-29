namespace Combat
{
    /// <summary>
    /// Resonance advantage wheel (bible v0.6): Cinder > Cryo > Root > Arc > Cinder (fire melts ice, frost kills growth,
    /// roots ground the current, a surge overloads the furnace). Prism and Basic are neutral to everything; Prism's
    /// precision gives it bonus crit instead (CombatConfig.prismCritBonus).
    /// </summary>
    public static class ResonanceChart
    {
        public static bool Beats(Resonance attacker, Resonance defender)
        {
            switch (attacker)
            {
                case Resonance.Cinder: return defender == Resonance.Cryo;
                case Resonance.Cryo: return defender == Resonance.Root;
                case Resonance.Root: return defender == Resonance.Arc;
                case Resonance.Arc: return defender == Resonance.Cinder;
            }
            return false;
        }

        public static ResonanceRelation Relation(Resonance attacker, Resonance defender)
        {
            if (Beats(attacker, defender)) return ResonanceRelation.Advantage;
            if (Beats(defender, attacker)) return ResonanceRelation.Disadvantage;
            return ResonanceRelation.Neutral;
        }
    }
}
