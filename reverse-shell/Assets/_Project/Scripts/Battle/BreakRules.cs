using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Break and Stagger (bible v0.8 §5, §13). An enemy has a Break value (<see cref="EnemyDefinition.breakPoints"/>)
    /// and weaknesses (<see cref="EnemyDefinition.weaknesses"/>). A card whose attack type or resonance matches a
    /// weakness takes its Break damage (<see cref="Value"/>: the card's base, times the rank's multiplier) off that
    /// value, once per target per card, on its first hit. At 0 the enemy is Staggered: its intent is cancelled and every
    /// later hit on it does +<see cref="PackRates.staggerBonus"/> (DoT ticks included). It stays Staggered for
    /// <see cref="PackRates.staggerTurns"/> rounds (no moves meanwhile), counted down after each round's DoT ticks; then
    /// its Break value refills. Numbers: the card sheet's
    /// Settings tab, imported into PackRates.
    /// </summary>
    public static class BreakRules
    {
        /// <summary>The weaknesses in the order they're shown (attack types, then the resonances by chapter).</summary>
        public static readonly Weakness[] All =
        {
            Weakness.Physical, Weakness.Incantation, Weakness.Arc, Weakness.Root, Weakness.Cryo, Weakness.Cinder, Weakness.Prism
        };

        public static Weakness Of(AttackType type)
        {
            return type == AttackType.Physical ? Weakness.Physical : Weakness.Incantation;
        }

        /// <summary>A resonance as a weakness (Basic has none).</summary>
        public static Weakness Of(Resonance resonance)
        {
            switch (resonance)
            {
                case Resonance.Arc: return Weakness.Arc;
                case Resonance.Cinder: return Weakness.Cinder;
                case Resonance.Root: return Weakness.Root;
                case Resonance.Cryo: return Weakness.Cryo;
                case Resonance.Prism: return Weakness.Prism;
            }
            return Weakness.None;
        }

        /// <summary>What a card's hits count as: its attack type and its pool's resonance.</summary>
        public static Weakness HitsOf(CardDefinition card)
        {
            return card == null ? Weakness.None : Of(card.attackType) | Of(card.pool);
        }

        /// <summary>The weaknesses this card hits on <paramref name="target"/> (None = no Break damage).</summary>
        public static Weakness Matches(CardDefinition card, BattleUnit target)
        {
            return target == null ? Weakness.None : HitsOf(card) & target.Weaknesses;
        }

        /// <summary>The card's Break damage at its rank: its base x the sheet's rank II / III multiplier.</summary>
        public static int Value(CardInstance card)
        {
            if (card == null || card.Definition == null) return 0;
            return Value(card.Definition, card.IsRitual ? 1 : card.Rank);
        }

        public static int Value(CardDefinition card, int rank)
        {
            if (card == null || card.breakValue <= 0) return 0;
            var rates = PackRates.Current;
            float mult = rank >= 3 ? rates.breakRankIII : rank == 2 ? rates.breakRankII : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(card.breakValue * mult));
        }

        /// <summary>The damage multiplier on a Staggered unit (1.5 by default).</summary>
        public static float StaggerMultiplier { get { return 1f + PackRates.Current.staggerBonus; } }

        public static string Label(Weakness w)
        {
            return w.ToString();
        }
    }

    /// <summary>
    /// The weaknesses the player has found, per enemy asset (bible v0.8: discovered by experimentation, recorded in the
    /// bestiary). The overworld hooks <see cref="Lookup"/> and <see cref="Store"/> up to the save's story flags; without
    /// them (tests, the battle scene on its own) it remembers for the session.
    /// </summary>
    public static class WeaknessBook
    {
        /// <summary>Weaknesses shown without being found (none: even Physical is found by hitting an enemy with it).</summary>
        public static Weakness AlwaysKnown = Weakness.None;

        public static Func<string, bool> Lookup;
        public static Action<string> Store;

        static readonly HashSet<string> session = new HashSet<string>();

        public static string Key(string enemyAsset, Weakness w)
        {
            return "weak." + enemyAsset + "." + w;
        }

        public static bool Knows(string enemyAsset, Weakness w)
        {
            if ((AlwaysKnown & w) != 0) return true;
            string key = Key(enemyAsset, w);
            return session.Contains(key) || (Lookup != null && Lookup(key));
        }

        public static void Learn(string enemyAsset, Weakness w)
        {
            string key = Key(enemyAsset, w);
            if (!session.Add(key)) return;
            if (Store != null) Store(key);
        }

        /// <summary>The weaknesses of this enemy found so far (out of its real ones).</summary>
        public static Weakness Known(EnemyDefinition enemy)
        {
            if (enemy == null) return Weakness.None;
            var known = Weakness.None;
            foreach (var w in BreakRules.All)
                if ((enemy.weaknesses & w) != 0 && Knows(enemy.name, w)) known |= w;
            return known;
        }

        /// <summary>Forgets the session's finds (a new game; tests).</summary>
        public static void Clear()
        {
            session.Clear();
        }
    }
}
