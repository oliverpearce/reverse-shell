using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Base class for everything a card does. To add a new mechanic, subclass this with
    /// [Serializable]; it shows up automatically in the card inspector's "Add Effect" dropdown.
    /// Effects only change battle state through BattleController, which records BattleEvents
    /// for the view to animate.
    /// </summary>
    [Serializable]
    public abstract class CardEffect
    {
        [Tooltip("Who this effect hits. CardTargets = the card's own Target Mode.")]
        public EffectTarget target = EffectTarget.CardTargets;

        public abstract void Apply(EffectContext ctx);

        /// <summary>
        /// Description as a template + values, so ranks can be merged ("Deal (120% I / 180% II) ...").
        /// Override this for new effects; keep numbers that change between ranks in the args.
        /// </summary>
        public virtual EffectText Text() { return new EffectText(GetType().Name + "."); }

        /// <summary>Plain description of this effect (used for card text when a rank has no description).</summary>
        public virtual string Describe() { return Text().Format(); }

        protected string TargetPhrase()
        {
            switch (target)
            {
                case EffectTarget.Self: return "self";
                case EffectTarget.SingleEnemy: return "1 enemy";
                case EffectTarget.AllEnemies: return "all enemies";
                case EffectTarget.RandomEnemy: return "a random enemy";
                case EffectTarget.SingleAlly: return "the weakest ally";
                case EffectTarget.AllAllies: return "all allies";
                case EffectTarget.RandomAlly: return "a random ally";
                default: return CardText.CardTargetToken; // replaced with the card's own target by CardText
            }
        }
    }

    public class EffectContext
    {
        public readonly BattleController Battle;
        public readonly BattleUnit Caster;
        public readonly CardInstance Card;
        public readonly List<BattleUnit> CardTargets;

        public EffectContext(BattleController battle, BattleUnit caster, CardInstance card, List<BattleUnit> cardTargets)
        {
            Battle = battle;
            Caster = caster;
            Card = card;
            CardTargets = cardTargets;
        }

        public int Rank { get { return Card.Rank; } }
        public bool IsRitual { get { return Card.IsRitual; } }

        public List<BattleUnit> Resolve(EffectTarget target)
        {
            if (target == EffectTarget.CardTargets)
                return CardTargets.FindAll(u => u.IsAlive);
            return Battle.SelectTargets(Caster, ToTargetMode(target));
        }

        static TargetMode ToTargetMode(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.Self: return TargetMode.Self;
                case EffectTarget.AllEnemies: return TargetMode.AllEnemies;
                case EffectTarget.RandomEnemy: return TargetMode.RandomEnemy;
                case EffectTarget.SingleAlly: return TargetMode.SingleAlly;
                case EffectTarget.AllAllies: return TargetMode.AllAllies;
                case EffectTarget.RandomAlly: return TargetMode.RandomAlly;
                default: return TargetMode.SingleEnemy;
            }
        }
    }
}
