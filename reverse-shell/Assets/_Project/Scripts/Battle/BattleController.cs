using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Runs the battle rules. Has no UI dependencies: everything that happens is pushed to
    /// <see cref="Events"/> for a view (or a test) to consume.
    ///
    /// Round flow: StartRound (AP refill, enemy intents, draw + merge) -> Planning (player queues /
    /// moves cards) -> EndPlanning (queued cards resolve, enemies act, statuses tick) -> StartRound...
    /// </summary>
    public class BattleController
    {
        public readonly BattleState State;
        public readonly Queue<BattleEvent> Events = new Queue<BattleEvent>();

        /// <summary>Raised once when the battle ends; true = victory.</summary>
        public event Action<bool> BattleEnded;
        public event Action<BattleEvent> EventEmitted;

        readonly Stack<PlanSnapshot> undoStack = new Stack<PlanSnapshot>();

        CombatConfig Config { get { return State.Config; } }

        public BattleController(IEnumerable<CharacterDefinition> party, IEnumerable<EnemyDefinition> enemies,
            CombatConfig config = null, int seed = 0)
        {
            if (config == null) config = CombatConfig.Default;
            State = new BattleState(config, new System.Random(seed != 0 ? seed : Environment.TickCount));
            foreach (var c in party) if (c != null) State.Party.Add(new BattleUnit(c, Team.Player));
            foreach (var e in enemies) if (e != null) State.Enemies.Add(new BattleUnit(e, Team.Enemy));
        }

        public static BattleController FromSetup(BattleSetup setup)
        {
            return new BattleController(setup.party, setup.enemies, setup.config, setup.seed);
        }

        /// <summary>
        /// Carries level and HP over from earlier fights. Call before StartBattle. Members not listed start at
        /// level 1 with full HP; knocked-out members come back with 1 HP so every character can still act.
        /// </summary>
        public void ApplyParty(IDictionary<CharacterDefinition, MemberProgress> party)
        {
            if (party == null) return;
            foreach (var unit in State.Party)
            {
                MemberProgress progress;
                if (unit.Character == null || !party.TryGetValue(unit.Character, out progress) || progress == null) continue;
                unit.SetLevel(progress.Level);
                if (progress.HP >= 0) unit.HP = Mathf.Clamp(progress.HP, 1, unit.MaxHP);
            }
        }

        /// <summary>
        /// Each member's progress as it stands now: level and XP from <paramref name="start"/> (or level 1),
        /// HP from the battlefield (full HP is stored as -1).
        /// </summary>
        public Dictionary<CharacterDefinition, MemberProgress> PartySnapshot(IDictionary<CharacterDefinition, MemberProgress> start)
        {
            var snapshot = new Dictionary<CharacterDefinition, MemberProgress>();
            foreach (var unit in State.Party)
            {
                if (unit.Character == null) continue;
                MemberProgress before;
                var progress = start != null && start.TryGetValue(unit.Character, out before) && before != null ? before.Clone() : new MemberProgress();
                progress.HP = unit.HP >= unit.MaxHP ? -1 : Mathf.Max(0, unit.HP);
                snapshot[unit.Character] = progress;
            }
            return snapshot;
        }

        /// <summary>XP every party member earns for winning this fight.</summary>
        public int XpReward()
        {
            int xp = 0;
            foreach (var enemy in State.Enemies)
                if (enemy.Enemy != null) xp += enemy.Enemy.xpReward;
            return xp;
        }

        public bool IsOver { get { return State.Phase == BattlePhase.Victory || State.Phase == BattlePhase.Defeat; } }
        public bool CanAct { get { return State.Phase == BattlePhase.Planning && State.ActionPoints > 0; } }

        public void StartBattle()
        {
            if (State.Phase != BattlePhase.NotStarted) return;
            Emit(new BattleStartedEvent());
            EnsureSelectedEnemy();
            StartRound();
        }

        // ------------------------------------------------------------------ Planning API

        /// <summary>Moves a card from the hand to the cast queue. Costs 1 AP.</summary>
        public bool QueueCard(CardInstance card)
        {
            if (!CanAct) return false;
            int index = State.Hand.IndexOf(card);
            if (index < 0) return false;

            PushUndo();
            State.Hand.RemoveAt(index);
            State.Queue.Add(card);
            State.ActionPoints--;
            if (card.IsRitual) SetMoxie(card.Owner, 0);

            Emit(new CardQueuedEvent { Card = card, HandAfter = State.Hand.Snapshot(), QueueAfter = new List<CardInstance>(State.Queue) });
            EmitActionPoints();
            ResolveMerges();
            return true;
        }

        /// <summary>Moves a card to a new slot in the hand (to set up merges). Costs 1 AP.</summary>
        public bool MoveCard(CardInstance card, int toIndex)
        {
            if (!CanAct) return false;
            int from = State.Hand.IndexOf(card);
            if (from < 0) return false;
            toIndex = Mathf.Clamp(toIndex, 0, State.Hand.Count - 1);
            if (toIndex == from) return false;

            PushUndo();
            State.Hand.Move(from, toIndex);
            State.ActionPoints--;

            Emit(new CardMovedEvent { Card = card, HandAfter = State.Hand.Snapshot() });
            EmitActionPoints();
            ResolveMerges();
            return true;
        }

        public bool CanUndo { get { return State.Phase == BattlePhase.Planning && undoStack.Count > 0; } }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var snap = undoStack.Pop();
            State.Hand.Restore(snap.Hand);
            State.Queue.Clear();
            State.Queue.AddRange(snap.Queue);
            State.ActionPoints = snap.ActionPoints;

            Emit(new HandRestoredEvent { HandAfter = State.Hand.Snapshot(), QueueAfter = new List<CardInstance>(State.Queue) });
            EmitActionPoints();
            for (int i = 0; i < State.Party.Count; i++)
                SetMoxie(State.Party[i], snap.Moxie[i]);
            return true;
        }

        public void SelectEnemy(BattleUnit enemy)
        {
            if (enemy == null || enemy.Team != Team.Enemy || !enemy.IsAlive) return;
            if (State.SelectedEnemy == enemy) return;
            State.SelectedEnemy = enemy;
            Emit(new TargetSelectedEvent { Target = enemy });
        }

        /// <summary>Resolves the queue, runs the enemy turn and round end, then starts the next round.</summary>
        public void EndPlanning()
        {
            if (State.Phase != BattlePhase.Planning) return;
            State.Phase = BattlePhase.Resolving;
            undoStack.Clear();

            var queued = new List<CardInstance>(State.Queue);
            foreach (var card in queued)
            {
                if (IsOver) break;
                State.Queue.Remove(card);
                var caster = card.Owner;
                if (!caster.IsAlive) continue;
                if (caster.IsStunned)
                {
                    Log(caster.Name + " can't act!");
                    continue;
                }

                Cast(caster, card);
                if (!card.IsRitual && !IsOver) AddMoxie(caster, Config.moxiePerCast);
            }
            State.Queue.Clear();

            if (!IsOver) EnemyTurn();
            if (!IsOver) EndRound();
            if (!IsOver) StartRound();
        }

        // ------------------------------------------------------------------ Effect API

        public List<BattleUnit> SelectTargets(BattleUnit caster, TargetMode mode)
        {
            var allies = State.AlliesOf(caster).FindAll(u => u.IsAlive);
            var foes = State.OpponentsOf(caster).FindAll(u => u.IsAlive);
            var result = new List<BattleUnit>();

            switch (mode)
            {
                case TargetMode.Self:
                    if (caster.IsAlive) result.Add(caster);
                    break;
                case TargetMode.SingleEnemy:
                    if (foes.Count == 0) break;
                    if (caster.Team == Team.Player)
                        result.Add(State.SelectedEnemy != null && State.SelectedEnemy.IsAlive ? State.SelectedEnemy : foes[0]);
                    else
                        result.Add(foes[State.Rng.Next(foes.Count)]);
                    break;
                case TargetMode.AllEnemies:
                    result.AddRange(foes);
                    break;
                case TargetMode.RandomEnemy:
                    if (foes.Count > 0) result.Add(foes[State.Rng.Next(foes.Count)]);
                    break;
                case TargetMode.SingleAlly:
                    if (allies.Count == 0) break;
                    var weakest = allies[0];
                    foreach (var a in allies) if (a.HPPercent < weakest.HPPercent) weakest = a;
                    result.Add(weakest);
                    break;
                case TargetMode.AllAllies:
                    result.AddRange(allies);
                    break;
                case TargetMode.RandomAlly:
                    if (allies.Count > 0) result.Add(allies[State.Rng.Next(allies.Count)]);
                    break;
            }
            return result;
        }

        public void DealDamage(BattleUnit source, BattleUnit target, float multiplier, bool isRitual)
        {
            if (!target.IsAlive || IsOver) return;
            var result = DamageCalculator.Compute(source, target, multiplier, isRitual, Config, State.Rng);
            ApplyDamage(source, target, result.Amount, result.Crit, result.Relation, false);
        }

        public void Heal(BattleUnit source, BattleUnit target, int amount)
        {
            if (!target.IsAlive || amount <= 0) return;
            int actual = Mathf.Min(amount, target.MaxHP - target.HP);
            target.HP += actual;
            Emit(new HealEvent { Source = source, Target = target, Amount = actual, HPAfter = target.HP, MaxHP = target.MaxHP });
        }

        public void ApplyStatus(BattleUnit source, BattleUnit target, StatusDefinition status, int duration, int stacks)
        {
            if (!target.IsAlive || status == null) return;
            int maxStacks = Mathf.Max(1, status.maxStacks);
            var existing = target.FindStatus(status);
            if (existing == null)
            {
                target.Statuses.Add(new StatusInstance(status, source, duration, Mathf.Clamp(stacks, 1, maxStacks)));
            }
            else
            {
                switch (status.stackRule)
                {
                    case StackRule.RefreshDuration:
                        existing.Remaining = Mathf.Max(existing.Remaining, duration);
                        break;
                    case StackRule.AddStacks:
                        existing.Stacks = Mathf.Min(maxStacks, existing.Stacks + stacks);
                        existing.Remaining = Mathf.Max(existing.Remaining, duration);
                        break;
                    case StackRule.Replace:
                        existing.Stacks = Mathf.Clamp(stacks, 1, maxStacks);
                        existing.Remaining = duration;
                        break;
                }
            }
            target.HP = Mathf.Min(target.HP, target.MaxHP);
            EmitStatuses(target);
        }

        public void AddMoxie(BattleUnit unit, int amount)
        {
            if (unit.Team != Team.Player || !unit.IsAlive || amount == 0) return;
            SetMoxie(unit, unit.Moxie + amount);
            TrySpawnRitual(unit);
        }

        public void AddBonusActionPoints(int amount)
        {
            State.BonusActionPointsNextRound += amount;
            Log(string.Format("{0}{1} AP next round", amount >= 0 ? "+" : "", amount));
        }

        // ------------------------------------------------------------------ Internals

        void StartRound()
        {
            State.Round++;
            undoStack.Clear();
            State.MaxActionPoints = Mathf.Max(1, Config.actionPointsPerRound + State.BonusActionPointsNextRound);
            State.BonusActionPointsNextRound = 0;
            State.ActionPoints = State.MaxActionPoints;

            Emit(new RoundStartedEvent { Round = State.Round });
            EmitActionPoints();

            foreach (var enemy in State.Enemies)
            {
                if (!enemy.IsAlive) continue;
                enemy.Intent = EnemyAI.ChooseIntent(enemy, State.Rng);
                Emit(new IntentChosenEvent { Unit = enemy, Intent = enemy.Intent });
            }

            FillHand();
            foreach (var u in State.Party) TrySpawnRitual(u);
            EnsureSelectedEnemy();
            State.Phase = BattlePhase.Planning;
        }

        void FillHand()
        {
            int safety = Config.handSize * 8;
            while (State.Hand.HasRoomForDraw && safety-- > 0)
            {
                var card = DeckGenerator.Draw(State.Party, State.Rng);
                if (card == null) break;
                State.Hand.Add(card);
                Emit(new CardDrawnEvent { Card = card, HandAfter = State.Hand.Snapshot() });
                ResolveMerges();
            }
        }

        void ResolveMerges()
        {
            MergeResult merge;
            while ((merge = State.Hand.TryMergeOnce()) != null)
            {
                Emit(new CardsMergedEvent { Merge = merge, HandAfter = State.Hand.Snapshot() });
                AddMoxie(merge.Result.Owner, Config.moxiePerMerge);
            }
        }

        void TrySpawnRitual(BattleUnit unit)
        {
            var character = unit.Character;
            if (character == null || character.ritual == null || !unit.IsAlive) return;
            if (unit.Moxie < Config.moxieCap) return;
            if (State.Hand.HasRitualFor(unit) || State.Queue.Exists(c => c.IsRitual && c.Owner == unit)) return;

            var card = new CardInstance(character.ritual, unit, 1);
            State.Hand.Add(card);
            Emit(new RitualSpawnedEvent { Card = card, HandAfter = State.Hand.Snapshot() });
        }

        void Cast(BattleUnit caster, CardInstance card)
        {
            var targets = SelectTargets(caster, card.Definition.targetMode);
            Emit(new CardCastEvent
            {
                Caster = caster,
                Card = card,
                Targets = new List<BattleUnit>(targets),
                QueueAfter = new List<CardInstance>(State.Queue)
            });

            var rank = card.RankData;
            if (rank == null) return;
            var ctx = new EffectContext(this, caster, card, targets);
            foreach (var effect in rank.effects)
            {
                if (effect == null) continue;
                effect.Apply(ctx);
                if (IsOver) return;
            }
        }

        void EnemyTurn()
        {
            foreach (var enemy in new List<BattleUnit>(State.Enemies))
            {
                if (!enemy.IsAlive || enemy.Intent == null) continue;
                var intent = enemy.Intent;
                enemy.Intent = null;
                if (enemy.IsStunned)
                {
                    Log(enemy.Name + " can't act!");
                    continue;
                }
                Cast(enemy, intent);
                if (IsOver) return;
            }
        }

        void EndRound()
        {
            foreach (var unit in new List<BattleUnit>(State.AllUnits))
            {
                if (!unit.IsAlive || unit.Statuses.Count == 0) continue;

                foreach (var status in new List<StatusInstance>(unit.Statuses))
                {
                    var def = status.Definition;
                    if (def.damagePerRoundPercentMaxHP > 0f && unit.IsAlive)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.MaxHP * def.damagePerRoundPercentMaxHP * status.Stacks));
                        ApplyDamage(status.Source, unit, dmg, false, AfflatusRelation.Neutral, true);
                        if (IsOver) return;
                    }
                    if (def.healPerRoundPercentMaxHP > 0f && unit.IsAlive)
                        Heal(status.Source, unit, Mathf.RoundToInt(unit.MaxHP * def.healPerRoundPercentMaxHP * status.Stacks));
                    status.Remaining--;
                }

                if (!unit.IsAlive) continue;
                unit.Statuses.RemoveAll(s => s.Remaining <= 0);
                unit.HP = Mathf.Min(unit.HP, unit.MaxHP);
                EmitStatuses(unit);
            }
        }

        void ApplyDamage(BattleUnit source, BattleUnit target, int amount, bool crit, AfflatusRelation relation, bool isTick)
        {
            target.HP = Mathf.Max(0, target.HP - amount);
            Emit(new DamageEvent
            {
                Source = source,
                Target = target,
                Amount = amount,
                Crit = crit,
                IsTick = isTick,
                Relation = relation,
                HPAfter = target.HP,
                MaxHP = target.MaxHP
            });
            if (target.HP == 0) KillUnit(target);
        }

        void KillUnit(BattleUnit unit)
        {
            unit.Statuses.Clear();
            unit.Intent = null;
            Emit(new UnitDiedEvent { Unit = unit });

            if (unit.Team == Team.Player)
            {
                SetMoxie(unit, 0);
                int removed = State.Hand.RemoveAllOwnedBy(unit) + State.Queue.RemoveAll(c => c.Owner == unit);
                if (removed > 0)
                    Emit(new HandRestoredEvent { HandAfter = State.Hand.Snapshot(), QueueAfter = new List<CardInstance>(State.Queue) });
            }
            else if (State.SelectedEnemy == unit)
            {
                State.SelectedEnemy = null;
                EnsureSelectedEnemy();
            }

            CheckBattleEnd();
        }

        void CheckBattleEnd()
        {
            if (IsOver) return;
            bool victory = !State.Enemies.Exists(e => e.IsAlive);
            bool defeat = !State.Party.Exists(p => p.IsAlive);
            if (!victory && !defeat) return;

            State.Phase = victory ? BattlePhase.Victory : BattlePhase.Defeat;
            Emit(new BattleEndedEvent { Victory = victory });
            if (BattleEnded != null) BattleEnded(victory);
        }

        void EnsureSelectedEnemy()
        {
            if (State.SelectedEnemy != null && State.SelectedEnemy.IsAlive) return;
            var first = State.Enemies.Find(e => e.IsAlive);
            if (first == null) return;
            State.SelectedEnemy = first;
            Emit(new TargetSelectedEvent { Target = first });
        }

        void SetMoxie(BattleUnit unit, int value)
        {
            value = Mathf.Clamp(value, 0, Config.moxieCap);
            if (value == unit.Moxie) return;
            int delta = value - unit.Moxie;
            unit.Moxie = value;
            Emit(new MoxieChangedEvent { Unit = unit, Value = value, Delta = delta });
        }

        void PushUndo()
        {
            undoStack.Push(new PlanSnapshot
            {
                Hand = State.Hand.Snapshot(),
                Queue = new List<CardInstance>(State.Queue),
                ActionPoints = State.ActionPoints,
                Moxie = State.Party.ConvertAll(p => p.Moxie)
            });
        }

        void EmitActionPoints()
        {
            Emit(new ActionPointsChangedEvent { Current = State.ActionPoints, Max = State.MaxActionPoints });
        }

        void EmitStatuses(BattleUnit unit)
        {
            Emit(new StatusesChangedEvent { Unit = unit, Statuses = unit.SnapshotStatuses() });
        }

        void Log(string message)
        {
            Emit(new LogEvent { Message = message });
        }

        void Emit(BattleEvent e)
        {
            Events.Enqueue(e);
            if (EventEmitted != null) EventEmitted(e);
        }

        class PlanSnapshot
        {
            public List<CardInstance> Hand;
            public List<CardInstance> Queue;
            public int ActionPoints;
            public List<int> Moxie;
        }
    }
}
