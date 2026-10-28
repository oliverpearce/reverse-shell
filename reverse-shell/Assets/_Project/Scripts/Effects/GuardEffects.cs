using System;
using UnityEngine;

namespace Combat
{
    /// <summary>Defense: a shield that soaks card damage before HP until it runs out or the round ends.</summary>
    [Serializable]
    public class ShieldEffect : CardEffect, IPoweredEffect
    {
        [Tooltip("The shield status (Status_Shield, special = Shield).")]
        public StatusDefinition status;
        [Tooltip("This shield's share of the card's power: shield HP = ATK x the card level's power x this share.")]
        public float multiplier = 1f;
        [Tooltip("Rounds. 1 = through this round's enemy turn.")]
        [Min(1)] public int duration = 1;

        public ShieldEffect() { target = EffectTarget.CardTargets; }

        public float Multiplier { get { return multiplier; } }

        public override void Apply(EffectContext ctx)
        {
            if (status == null) return;
            float amount = ctx.Caster.GetStat(StatType.Attack) * CardLevels.Of(ctx.Card, this);
            if (ctx.Card != null && ctx.Card.IsRitual) amount *= 1f + Mathf.Max(0f, ctx.Caster.GetStat(StatType.RitualMight));
            foreach (var t in ctx.Resolve(target))
                ctx.Battle.GiveShield(ctx.Caster, t, status, Mathf.RoundToInt(amount), duration);
        }

        public override EffectText Text()
        {
            return new EffectText("Shield {0} for {1} of ATK.", TargetPhrase(), Mathf.RoundToInt(CardLevels.Of(CardLevels.Describing, this) * 100f) + "%");
        }
    }

    /// <summary>Counter: the holder hits back whenever an enemy card hits it this round.</summary>
    [Serializable]
    public class CounterEffect : CardEffect, IPoweredEffect
    {
        [Tooltip("The counter status (Status_Counter, special = Counter).")]
        public StatusDefinition status;
        [Tooltip("The counter hit's share of the card's power (a multiple of the holder's ATK).")]
        public float multiplier = 1f;
        [Tooltip("How many times it can hit back before it's spent.")]
        [Min(1)] public int triggers = 1;
        [Tooltip("Extra share while the holder is below half HP (1 = double).")]
        public float lowHPBonus;
        [Tooltip("Optional status the counter hit also gives the attacker.")]
        public StatusDefinition onHit;
        [Min(1)] public int onHitTurns = 2;
        [Min(1)] public int duration = 1;

        public CounterEffect() { target = EffectTarget.CardTargets; }

        public float Multiplier { get { return multiplier; } }

        public override void Apply(EffectContext ctx)
        {
            if (status == null) return;
            float power = CardLevels.Of(ctx.Card, this);
            foreach (var t in ctx.Resolve(target))
                ctx.Battle.GiveCounter(ctx.Caster, t, status, power, triggers, lowHPBonus, onHit, onHitTurns, duration);
        }

        public override EffectText Text()
        {
            string pct = Mathf.RoundToInt(CardLevels.Of(CardLevels.Describing, this) * 100f) + "%";
            string text = triggers > 1 ? "{0} hits back for {1} ATK when attacked (up to {2} times)" : "{0} hits back for {1} ATK when attacked";
            if (lowHPBonus > 0f) text += ", +" + Mathf.RoundToInt(lowHPBonus * 100f) + "% below half HP";
            if (onHit != null) text += ", giving " + onHit.DisplayName;
            string who = TargetPhrase();
            if (who.Length > 0) who = char.ToUpperInvariant(who[0]) + who.Substring(1);
            return new EffectText(text + ".", who, pct, triggers.ToString());
        }
    }

    /// <summary>Defense: the caster steps in front of the target; enemy card hits on it go to the caster this round.</summary>
    [Serializable]
    public class CoverEffect : CardEffect
    {
        [Tooltip("The cover status (Status_Covered, special = Cover).")]
        public StatusDefinition status;
        [Min(1)] public int duration = 1;

        public CoverEffect() { target = EffectTarget.CardTargets; }

        public override void Apply(EffectContext ctx)
        {
            if (status == null) return;
            foreach (var t in ctx.Resolve(target))
                if (t != ctx.Caster) ctx.Battle.ApplyStatus(ctx.Caster, t, status, duration, 1);
        }

        public override EffectText Text() { return new EffectText("Take the hits aimed at {0} this turn.", TargetPhrase()); }
    }

    /// <summary>Debuff (disruption): cancels the target's shown intent, so it does nothing this round.</summary>
    [Serializable]
    public class InterruptEffect : CardEffect
    {
        [Tooltip("Only cancels Attack intents (the timing requirement).")]
        public bool attacksOnly;

        public override void Apply(EffectContext ctx)
        {
            foreach (var t in ctx.Resolve(target))
            {
                if (t.Intent == null) continue;
                if (attacksOnly && t.Intent.Definition.kind != CardKind.Attack) continue;
                ctx.Battle.CancelIntent(t);
            }
        }

        public override EffectText Text()
        {
            return new EffectText(attacksOnly ? "Cancel {0}'s attack." : "Cancel {0}'s move.", TargetPhrase());
        }
    }

    /// <summary>Removes every debuff from the targets.</summary>
    [Serializable]
    public class CleanseEffect : CardEffect
    {
        public override void Apply(EffectContext ctx)
        {
            foreach (var t in ctx.Resolve(target))
                ctx.Battle.Cleanse(t);
        }

        public override EffectText Text() { return new EffectText("Clear {0}'s debuffs.", TargetPhrase()); }
    }
}
