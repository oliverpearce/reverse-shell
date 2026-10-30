using System.Collections.Generic;

namespace Combat
{
    /// <summary>
    /// Battle logic runs instantly and records what happened as events. The view plays them
    /// back one by one, so events carry the values *at that moment* (HP after, hand order after).
    /// </summary>
    public abstract class BattleEvent { }

    public class BattleStartedEvent : BattleEvent { }

    public class RoundStartedEvent : BattleEvent
    {
        public int Round;
    }

    public class LogEvent : BattleEvent
    {
        public string Message;
    }

    public abstract class HandEvent : BattleEvent
    {
        public List<CardInstance> HandAfter;
    }

    public class CardDrawnEvent : HandEvent
    {
        public CardInstance Card;
    }

    public class CardsMergedEvent : HandEvent
    {
        public MergeResult Merge;
    }

    public class RitualSpawnedEvent : HandEvent
    {
        public CardInstance Card;
    }

    public class CardMovedEvent : HandEvent
    {
        public CardInstance Card;
    }

    public class CardQueuedEvent : HandEvent
    {
        public CardInstance Card;
        public List<CardInstance> QueueAfter;
    }

    /// <summary>Hand/queue replaced wholesale (undo, or a dead unit's cards removed).</summary>
    public class HandRestoredEvent : HandEvent
    {
        public List<CardInstance> QueueAfter;
    }

    public class ActionPointsChangedEvent : BattleEvent
    {
        public int Current;
        public int Max;
        /// <summary>AP spent on moves this turn (shown as locked "Card Moved" slots).</summary>
        public int Moves;
    }

    /// <summary>A backup member steps onto the field in place of a fallen one.</summary>
    public class UnitEnteredEvent : BattleEvent
    {
        public BattleUnit Unit;
        public BattleUnit Replaces;
    }

    public class MoxieChangedEvent : BattleEvent
    {
        public BattleUnit Unit;
        public int Value;
        public int Delta;
    }

    public class IntentChosenEvent : BattleEvent
    {
        public BattleUnit Unit;
        public CardInstance Intent;
    }

    public class TargetSelectedEvent : BattleEvent
    {
        public BattleUnit Target;
    }

    public class CardCastEvent : BattleEvent
    {
        public BattleUnit Caster;
        public CardInstance Card;
        public List<BattleUnit> Targets;
        public List<CardInstance> QueueAfter;
    }

    public class DamageEvent : BattleEvent
    {
        public BattleUnit Source;
        public BattleUnit Target;
        public int Amount;
        /// <summary>Damage a shield soaked up before HP (Amount is what got through).</summary>
        public int Blocked;
        public bool Crit;
        public bool IsTick;
        /// <summary>A Counter's hit back.</summary>
        public bool IsCounter;
        /// <summary>An end-of-turn tick: the status that dealt it.</summary>
        public StatusDefinition TickStatus;
        /// <summary>The target was Staggered: this hit got the Stagger bonus.</summary>
        public bool StaggerBonus;
        public int HPAfter;
        public int MaxHP;
    }

    /// <summary>An enemy's Break value changed (a weakness hit, or the refill at the start of a round).</summary>
    public class BreakChangedEvent : BattleEvent
    {
        public BattleUnit Unit;
        public int Break;
        public int MaxBreak;
        public bool Staggered;
    }

    /// <summary>An enemy's Break value hit 0: it's Staggered this round (its intent is cancelled).</summary>
    public class StaggeredEvent : BattleEvent
    {
        public BattleUnit Unit;
    }

    /// <summary>A hit showed the player one of an enemy's weaknesses for the first time.</summary>
    public class WeaknessRevealedEvent : BattleEvent
    {
        public BattleUnit Unit;
        public Weakness Weakness;
    }

    /// <summary>A moment with an effect but no number: a shield going up, a cover, an interrupt, a cleanse.</summary>
    public class FxCueEvent : BattleEvent
    {
        public const string Shield = "shield", Cover = "cover", Interrupt = "interrupt", Cleanse = "cleanse";
        public BattleUnit Unit;
        public string Cue;
    }

    public class HealEvent : BattleEvent
    {
        public BattleUnit Source;
        public BattleUnit Target;
        public int Amount;
        public int HPAfter;
        public int MaxHP;
    }

    public class StatusesChangedEvent : BattleEvent
    {
        public BattleUnit Unit;
        public List<StatusSnapshot> Statuses;
    }

    public class UnitDiedEvent : BattleEvent
    {
        public BattleUnit Unit;
    }

    public class BattleEndedEvent : BattleEvent
    {
        public bool Victory;
    }
}
