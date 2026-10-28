using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [Serializable]
    public class DamageEffect : CardEffect
    {
        [Tooltip("This hit's share of the card's power: the rank's biggest multiplier is the card level's full power " +
                 "(CardLevels), others are that fraction of it. Split evenly across hits.")]
        public float multiplier = 1.8f;
        [Min(1)] public int hits = 1;

        public override void Apply(EffectContext ctx)
        {
            var targets = ctx.Resolve(target);
            int hitCount = Mathf.Max(1, hits);
            for (int h = 0; h < hitCount; h++)
            {
                foreach (var t in targets)
                    if (t.IsAlive) ctx.Battle.DealDamage(ctx.Caster, t, CardLevels.Damage(ctx.Card, this) / hitCount, ctx.IsRitual);
                if (ctx.Battle.IsOver) return;
            }
        }

        public override EffectText Text()
        {
            string pct = Mathf.RoundToInt(CardLevels.Damage(CardLevels.Describing, this) * 100f) + "%";
            return hits > 1
                ? new EffectText("Deal {0} ATK damage to {1} over {2} hits.", pct, TargetPhrase(), hits.ToString())
                : new EffectText("Deal {0} ATK damage to {1}.", pct, TargetPhrase());
        }
    }

    [Serializable]
    public class HealEffect : CardEffect
    {
        [Tooltip("This heal's share of the card's power: the rank's biggest heal is the card level's full power (CardLevels).")]
        public float multiplier = 1.2f;

        public HealEffect() { target = EffectTarget.SingleAlly; }

        public override void Apply(EffectContext ctx)
        {
            float amount = ctx.Caster.GetStat(StatType.Attack) * CardLevels.Heal(ctx.Card, this) * (1f + ctx.Caster.GetStat(StatType.HealingBonus));
            // An Ult's healing grows with Ult power (Soul levels), like its damage does.
            if (ctx.Card != null && ctx.Card.IsRitual) amount *= 1f + Mathf.Max(0f, ctx.Caster.GetStat(StatType.RitualMight));
            foreach (var t in ctx.Resolve(target))
                ctx.Battle.Heal(ctx.Caster, t, Mathf.RoundToInt(amount));
        }

        public override EffectText Text()
        {
            return new EffectText("Heal {0} for {1} of ATK.", TargetPhrase(), Mathf.RoundToInt(CardLevels.Heal(CardLevels.Describing, this) * 100f) + "%");
        }
    }

    [Serializable]
    public class ApplyStatusEffect : CardEffect
    {
        public StatusDefinition status;
        [Tooltip("Rounds. Counts down at round end, so 2 = this round and next.")]
        [Min(1)] public int duration = 2;
        [Min(1)] public int stacks = 1;
        [Range(0f, 1f)] public float chance = 1f;

        public override void Apply(EffectContext ctx)
        {
            if (status == null) return;
            foreach (var t in ctx.Resolve(target))
            {
                if (chance < 1f && ctx.Battle.State.Rng.NextDouble() >= chance) continue;
                ctx.Battle.ApplyStatus(ctx.Caster, t, status, duration, stacks);
            }
        }

        /// <summary>"Poison 1 enemy." with a status verb, else "Give all allies ATK Up." (a chance in front when it
        /// isn't certain). Turns and stacks aren't written: the card shows them as a status chip.</summary>
        public override EffectText Text()
        {
            if (status == null) return new EffectText("(No status set.)");
            string chance100 = Mathf.RoundToInt(chance * 100f) + "%";
            bool verb = !string.IsNullOrEmpty(status.verb);
            if (chance < 1f)
                return verb
                    ? new EffectText("{1} chance to " + status.verb + " {0}.", TargetPhrase(), chance100)
                    : new EffectText("{2} chance to give {0} {1}.", TargetPhrase(), status.DisplayName, chance100);
            return verb
                ? new EffectText(char.ToUpperInvariant(status.verb[0]) + status.verb.Substring(1) + " {0}.", TargetPhrase())
                : new EffectText("Give {0} {1}.", TargetPhrase(), status.DisplayName);
        }
    }

    [Serializable]
    public class GainMoxieEffect : CardEffect
    {
        public int amount = 1;

        public GainMoxieEffect() { target = EffectTarget.Self; }

        public override void Apply(EffectContext ctx)
        {
            foreach (var t in ctx.Resolve(target))
                ctx.Battle.AddMoxie(t, amount);
        }

        public override EffectText Text()
        {
            string who = TargetPhrase();
            who = who.Length > 0 ? char.ToUpper(who[0]) + who.Substring(1) : who;
            return new EffectText("{0} gains {1} Ult.", who, amount.ToString());
        }
    }

    [Serializable]
    public class ModifyAPEffect : CardEffect
    {
        [Tooltip("Extra action points next round (player casters only).")]
        public int amount = 1;

        public override void Apply(EffectContext ctx)
        {
            if (ctx.Caster.Team == Team.Player) ctx.Battle.AddBonusActionPoints(amount);
        }

        public override EffectText Text()
        {
            return new EffectText("+{0} AP next turn.", amount.ToString());
        }
    }
}
