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
        public bool Crit;
        public bool IsTick;
        public AfflatusRelation Relation;
        public int HPAfter;
        public int MaxHP;
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
