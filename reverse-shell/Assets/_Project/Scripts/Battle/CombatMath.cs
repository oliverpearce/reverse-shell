using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public struct DamageResult
    {
        public int Amount;
        public bool Crit;
        /// <summary>The defender was Staggered, so the hit got the Stagger bonus.</summary>
        public bool StaggerBonus;
    }

    public static class DamageCalculator
    {
        /// <summary>
        /// ATK x multiplier x (1 - mitigation) x stagger x (1 + might) x (1 + damage taken) x crit. Stagger is
        /// <see cref="BreakRules.StaggerMultiplier"/> on a Staggered defender, else 1 (there's no resonance wheel).
        /// Pass rng = null to disable crits (handy for tests and previews).
        /// </summary>
        public static DamageResult Compute(BattleUnit attacker, BattleUnit defender, float multiplier, bool isRitual,
            CombatConfig config, System.Random rng)
        {
            float atk = attacker.GetStat(StatType.Attack);
            var defStat = attacker.Definition.damageType == DamageType.Physical ? StatType.PhysicalDefense : StatType.SpecialDefense;
            float def = Mathf.Max(0f, defender.GetStat(defStat));
            float mitigation = def / (def + Mathf.Max(1f, config.defenseScale));

            bool staggered = defender.Staggered;
            float stagger = staggered ? BreakRules.StaggerMultiplier : 1f;

            float might = 1f + attacker.GetStat(StatType.DamageBonus)
                             + attacker.GetStat(isRitual ? StatType.RitualMight : StatType.IncantationMight);
            float taken = 1f + defender.GetStat(StatType.DamageTaken);

            float critRate = Mathf.Clamp01(attacker.GetStat(StatType.CritRate));
            bool crit = rng != null && rng.NextDouble() < critRate;
            float critMult = crit ? Mathf.Max(1f, attacker.GetStat(StatType.CritDamage)) : 1f;

            float total = atk * multiplier * (1f - mitigation) * stagger * Mathf.Max(0f, might) * Mathf.Max(0f, taken) * critMult;
            return new DamageResult
            {
                Amount = Mathf.Max(1, Mathf.RoundToInt(total)),
                Crit = crit,
                StaggerBonus = staggered
            };
        }
    }

    public static class DeckGenerator
    {
        /// <summary>A random incantation from a random living party member, at rank I (copies add levels, not ranks).</summary>
        public static CardInstance Draw(IList<BattleUnit> party, System.Random rng)
        {
            var owners = new List<BattleUnit>();
            foreach (var u in party)
                if (u.IsAlive && u.Character != null && u.Incantations.Exists(c => c != null))
                    owners.Add(u);
            if (owners.Count == 0) return null;

            var owner = owners[rng.Next(owners.Count)];
            var cards = owner.Incantations.FindAll(c => c != null);
            var card = cards[rng.Next(cards.Count)];
            return new CardInstance(card, owner, 1);
        }
    }

    public static class EnemyAI
    {
        /// <summary>The enemy's move this round: its Ult when its Ult points are full, else a weighted random skill.</summary>
        public static CardInstance ChooseIntent(BattleUnit enemy, System.Random rng, int moxieCap)
        {
            var def = enemy.Enemy;
            if (def == null) return null;
            if (def.ritual != null && enemy.Moxie >= moxieCap) return new CardInstance(def.ritual, enemy, 1);

            // The skills plus the innate card, weighted.
            var moves = new List<EnemySkill>(def.skills);
            if (def.innate != null) moves.Add(new EnemySkill { card = def.innate, rank = 1, weight = def.innateWeight });
            float total = 0f;
            foreach (var s in moves)
                if (s.card != null) total += s.weight;
            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            EnemySkill pick = null;
            foreach (var s in moves)
            {
                if (s.card == null) continue;
                pick = s;
                roll -= s.weight;
                if (roll < 0) break;
            }
            return new CardInstance(pick.card, enemy, Mathf.Clamp(pick.rank, 1, pick.card.MaxRank));
        }
    }
}
