using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [Serializable]
    public class DamageEffect : CardEffect
    {
        [Tooltip("Total damage as a multiple of ATK (1.8 = 180%). Split evenly across hits.")]
        public float multiplier = 1.8f;
        [Min(1)] public int hits = 1;

        public override void Apply(EffectContext ctx)
        {
            var targets = ctx.Resolve(target);
            int hitCount = Mathf.Max(1, hits);
            for (int h = 0; h < hitCount; h++)
            {
                foreach (var t in targets)
                    if (t.IsAlive) ctx.Battle.DealDamage(ctx.Caster, t, multiplier / hitCount, ctx.IsRitual);
                if (ctx.Battle.IsOver) return;
            }
        }

        public override EffectText Text()
        {
            string pct = Mathf.RoundToInt(multiplier * 100f) + "%";
            return hits > 1
                ? new EffectText("Deal {0} ATK damage to {1} over {2} hits.", pct, TargetPhrase(), hits.ToString())
                : new EffectText("Deal {0} ATK damage to {1}.", pct, TargetPhrase());
        }

        public override bool Joinable { get { return true; } }

        public override EffectText Clause(bool withTarget)
        {
            string pct = Mathf.RoundToInt(multiplier * 100f) + "%";
            if (hits > 1)
                return withTarget
                    ? new EffectText("deal {0} ATK damage to {1} over {2} hits", pct, TargetPhrase(), hits.ToString())
                    : new EffectText("deal {0} ATK damage over {1} hits", pct, hits.ToString());
            return withTarget ? new EffectText("deal {0} ATK damage to {1}", pct, TargetPhrase()) : new EffectText("deal {0} ATK damage", pct);
        }
    }

    [Serializable]
    public class HealEffect : CardEffect
    {
        [Tooltip("Healing as a multiple of the caster's ATK.")]
        public float multiplier = 1.2f;

        public HealEffect() { target = EffectTarget.SingleAlly; }

        public override void Apply(EffectContext ctx)
        {
            float amount = ctx.Caster.GetStat(StatType.Attack) * multiplier * (1f + ctx.Caster.GetStat(StatType.HealingBonus));
            foreach (var t in ctx.Resolve(target))
                ctx.Battle.Heal(ctx.Caster, t, Mathf.RoundToInt(amount));
        }

        public override EffectText Text()
        {
            return new EffectText("Heal {0} for {1} of ATK.", TargetPhrase(), Mathf.RoundToInt(multiplier * 100f) + "%");
        }

        public override bool Joinable { get { return true; } }

        public override EffectText Clause(bool withTarget)
        {
            string pct = Mathf.RoundToInt(multiplier * 100f) + "%";
            return withTarget ? new EffectText("heal {0} for {1} of ATK", TargetPhrase(), pct) : new EffectText("restore {0} of ATK as HP", pct);
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

        public override EffectText Text()
        {
            if (status == null) return new EffectText("(No status set.)");
            string chance100 = Mathf.RoundToInt(chance * 100f) + "%";
            // Stackable statuses always show their stack count so it can be compared across ranks.
            if (status.maxStacks > 1)
                return chance < 1f
                    ? new EffectText("{4} chance to give {0} {1} {2} for {3} turns.", TargetPhrase(), status.DisplayName, "x" + stacks, duration.ToString(), chance100)
                    : new EffectText("Give {0} {1} {2} for {3} turns.", TargetPhrase(), status.DisplayName, "x" + stacks, duration.ToString());
            return chance < 1f
                ? new EffectText("{3} chance to give {0} {1} for {2} turns.", TargetPhrase(), status.DisplayName, duration.ToString(), chance100)
                : new EffectText("Give {0} {1} for {2} turns.", TargetPhrase(), status.DisplayName, duration.ToString());
        }

        public override bool Joinable { get { return status != null; } }

        /// <summary>"poison 1 enemy (x1, 2 turns)" with a status verb, else "give 1 enemy ATK Up (2 turns)".</summary>
        public override EffectText Clause(bool withTarget)
        {
            if (status == null) return Text();
            bool stacking = status.maxStacks > 1;
            string prefix = chance < 1f ? "{c} chance to " : "";
            var args = new List<string>();
            string template;
            if (!withTarget)
            {
                template = "apply " + status.DisplayName;
            }
            else if (!string.IsNullOrEmpty(status.verb))
            {
                template = status.verb + " {t}";
            }
            else
            {
                template = "give {t} " + status.DisplayName;
            }
            template = prefix + template + (stacking ? " ({s}, {d} turns)" : " ({d} turns)");
            // Number the placeholders in reading order so ranks can be merged value by value.
            foreach (var token in new[] { "{c}", "{t}", "{s}", "{d}" })
            {
                if (!template.Contains(token)) continue;
                template = template.Replace(token, "{" + args.Count + "}");
                switch (token)
                {
                    case "{c}": args.Add(Mathf.RoundToInt(this.chance * 100f) + "%"); break;
                    case "{t}": args.Add(TargetPhrase()); break;
                    case "{s}": args.Add("x" + stacks); break;
                    default: args.Add(duration.ToString()); break;
                }
            }
            return new EffectText(template, args.ToArray());
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
