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

        /// <param name="backup">Waits in reserve (otherwise a 4th party member becomes the backup).</param>
        public BattleController(IEnumerable<CharacterDefinition> party, IEnumerable<EnemyDefinition> enemies,
            CombatConfig config = null, int seed = 0, CharacterDefinition backup = null)
        {
            if (config == null) config = CombatConfig.Default;
            State = new BattleState(config, new System.Random(seed != 0 ? seed : Environment.TickCount));
            // The first MaxActive members fight; the next MaxBackup wait in reserve.
            foreach (var c in party)
            {
                if (c == null) continue;
                if (State.Party.Count + State.Backup.Count >= BattleState.MaxActive + BattleState.MaxBackup) break;
                var unit = new BattleUnit(c, Team.Player);
                State.Roster.Add(unit);
                if (State.Party.Count < BattleState.MaxActive) State.Party.Add(unit);
                else State.Backup.Add(unit);
            }
            if (backup != null && State.Backup.Count < BattleState.MaxBackup && !State.Roster.Exists(u => u.Definition == backup))
            {
                var reserve = new BattleUnit(backup, Team.Player);
                State.Roster.Add(reserve);
                State.Backup.Add(reserve);
            }
            foreach (var e in enemies) if (e != null) State.Enemies.Add(new BattleUnit(e, Team.Enemy));
        }

        public static BattleController FromSetup(BattleSetup setup)
        {
            var controller = new BattleController(setup.party, setup.enemies, setup.config, setup.seed, setup.backup);
            foreach (var enemy in controller.State.Enemies) enemy.SetEnemyLevel(setup.enemyLevel, setup.boss);
            return controller;
        }

        /// <summary>
        /// Carries level and HP over from earlier fights. Call before StartBattle. Members not listed start at
        /// level 1 with full HP; knocked-out members come back with 1 HP so every character can still act.
        /// </summary>
        public void ApplyParty(IDictionary<CharacterDefinition, MemberProgress> party)
        {
            if (party == null) return;
            foreach (var unit in State.Roster)
            {
                MemberProgress progress;
                if (unit.Character == null || !party.TryGetValue(unit.Character, out progress) || progress == null) continue;
                unit.SetLevel(progress.Level);
                if (progress.HP >= 0) unit.HP = Mathf.Clamp(progress.HP, 1, unit.MaxHP);
            }
        }

        /// <summary>
        /// Fits each member's deck and Parts. Call before <see cref="ApplyParty"/> (Parts can change max HP)
        /// and before StartBattle. Members not listed use their default cards and no Parts.
        /// </summary>
        public void ApplyLoadouts(IDictionary<CharacterDefinition, Loadout> loadouts)
        {
            if (loadouts == null) return;
            foreach (var unit in State.Roster)
            {
                Loadout loadout;
                if (unit.Character != null && loadouts.TryGetValue(unit.Character, out loadout)) unit.ApplyLoadout(loadout);
            }
        }

        /// <summary>
        /// Each member's progress as it stands now: level and XP from <paramref name="start"/> (or level 1),
        /// HP from the battlefield (full HP is stored as -1).
        /// </summary>
        public Dictionary<CharacterDefinition, MemberProgress> PartySnapshot(IDictionary<CharacterDefinition, MemberProgress> start)
        {
            var snapshot = new Dictionary<CharacterDefinition, MemberProgress>();
            foreach (var unit in State.Roster)
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
            float xp = 0f;
            foreach (var enemy in State.Enemies)
                if (enemy.Enemy != null) xp += enemy.Enemy.xpReward * BattleUnit.EnemyScale(enemy.Level) * (enemy.IsBoss ? 3f : 1f);
            return Mathf.RoundToInt(xp);
        }

        public bool IsOver { get { return State.Phase == BattlePhase.Victory || State.Phase == BattlePhase.Defeat; } }
        public bool CanAct { get { return State.Phase == BattlePhase.Planning && State.ActionPoints > 0; } }

        public void StartBattle()
        {
            if (State.Phase != BattlePhase.NotStarted) return;
            Emit(new BattleStartedEvent());
            // Parts like the Ult Chip start a member with some Ult points already.
            foreach (var unit in State.Party) GiveStartingMoxie(unit);
            foreach (var unit in State.Backup) GiveStartingMoxie(unit);
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
            // Lock the aim now: the enemy under the target cursor when the card is played.
            if (card.Definition.targetMode == TargetMode.SingleEnemy && State.SelectedEnemy != null && State.SelectedEnemy.IsAlive)
                card.Target = State.SelectedEnemy;
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
            State.MovesThisRound++;   // locks a queue slot ("Card Moved")

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
            State.MovesThisRound = snap.Moves;

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

        /// <summary>
        /// Everyone a card would affect if played now, for the hover highlight. Covers the card's own target and
        /// every effect that picks its own (e.g. a self or team buff on an attack). Random picks show every
        /// candidate. Never touches the RNG, so previews can't change the battle.
        /// </summary>
        public List<BattleUnit> PreviewTargets(CardInstance card)
        {
            var result = new List<BattleUnit>();
            if (card == null || card.Owner == null) return result;
            AddPreview(result, card.Owner, card.Definition.targetMode);
            var rank = card.RankData;
            if (rank != null && rank.effects != null)
                foreach (var effect in rank.effects)
                    if (effect != null && effect.target != EffectTarget.CardTargets) AddPreview(result, card.Owner, ToMode(effect.target));
            return result;
        }

        void AddPreview(List<BattleUnit> into, BattleUnit caster, TargetMode mode)
        {
            var allies = State.AlliesOf(caster).FindAll(u => u.IsAlive);
            var foes = State.OpponentsOf(caster).FindAll(u => u.IsAlive);
            var picked = new List<BattleUnit>();
            switch (mode)
            {
                case TargetMode.RandomEnemy: picked.AddRange(foes); break;
                case TargetMode.RandomAlly: picked.AddRange(allies); break;
                default: picked = SelectTargetsNoRandom(caster, mode, allies, foes); break;
            }
            foreach (var u in picked)
                if (!into.Contains(u)) into.Add(u);
        }

        List<BattleUnit> SelectTargetsNoRandom(BattleUnit caster, TargetMode mode, List<BattleUnit> allies, List<BattleUnit> foes)
        {
            var result = new List<BattleUnit>();
            switch (mode)
            {
                case TargetMode.Self:
                    if (caster.IsAlive) result.Add(caster);
                    break;
                case TargetMode.SingleEnemy:
                    if (foes.Count == 0) break;
                    if (caster.Team == Team.Player) result.Add(State.SelectedEnemy != null && State.SelectedEnemy.IsAlive ? State.SelectedEnemy : foes[0]);
                    else result.AddRange(foes); // enemies pick at random: any of them
                    break;
                case TargetMode.AllEnemies:
                    result.AddRange(foes);
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
            }
            return result;
        }

        static TargetMode ToMode(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.Self: return TargetMode.Self;
                case EffectTarget.SingleEnemy: return TargetMode.SingleEnemy;
                case EffectTarget.AllEnemies: return TargetMode.AllEnemies;
                case EffectTarget.RandomEnemy: return TargetMode.RandomEnemy;
                case EffectTarget.SingleAlly: return TargetMode.SingleAlly;
                case EffectTarget.AllAllies: return TargetMode.AllAllies;
                default: return TargetMode.RandomAlly;
            }
        }

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

        /// <summary>A card hit. A Cover on the target sends it to the coverer; a Shield soaks it before HP; a Counter on
        /// whoever got hit hits back (counter hits never trigger counters).</summary>
        public void DealDamage(BattleUnit source, BattleUnit target, float multiplier, bool isRitual)
        {
            DealDamage(source, target, multiplier, isRitual, false, null, null);
        }

        /// <summary>A hit from <paramref name="card"/>: as above, then its Break damage when the target is weak to it
        /// (once per target per card: <paramref name="breakDealt"/> holds who it already broke into).</summary>
        public void DealDamage(BattleUnit source, BattleUnit target, float multiplier, bool isRitual, CardInstance card,
            HashSet<BattleUnit> breakDealt)
        {
            DealDamage(source, target, multiplier, isRitual, false, card, breakDealt);
        }

        void DealDamage(BattleUnit source, BattleUnit target, float multiplier, bool isRitual, bool isCounter,
            CardInstance card, HashSet<BattleUnit> breakDealt)
        {
            if (!target.IsAlive || IsOver) return;
            target = CoveredBy(source, target);
            // Damage first, so the hit that breaks the enemy doesn't get the Stagger bonus itself.
            var result = DamageCalculator.Compute(source, target, multiplier, isRitual, Config, State.Rng);
            int amount = result.Amount, blocked = 0;
            var shield = target.Statuses.Find(s => s.Definition.special == StatusSpecial.Shield && s.Power > 0f);
            if (shield != null)
            {
                blocked = Mathf.Min(amount, Mathf.RoundToInt(shield.Power));
                shield.Power -= blocked;
                amount -= blocked;
                if (shield.Power <= 0f) target.Statuses.Remove(shield);
                EmitStatuses(target);
            }
            ApplyDamage(source, target, amount, result.Crit, result.StaggerBonus, false, blocked, isCounter);
            if (card != null && !IsOver) ApplyBreak(source, target, card, breakDealt);
            if (!isCounter && !IsOver) TryCounter(target, source);
        }

        /// <summary>A card hit on a weakness: shows the player the weaknesses it hit, then takes the card's Break damage
        /// off the target's Break value (not while it's already Staggered). At 0 it's Staggered.</summary>
        void ApplyBreak(BattleUnit source, BattleUnit target, CardInstance card, HashSet<BattleUnit> breakDealt)
        {
            if (!target.IsAlive || !target.Breakable || source == null || source.Team == target.Team) return;
            var match = BreakRules.Matches(card.Definition, target);
            if (match == Weakness.None) return;
            RevealWeaknesses(target, match);
            if (target.Staggered || (breakDealt != null && !breakDealt.Add(target))) return;
            int amount = BreakRules.Value(card);
            if (amount <= 0) return;
            target.Break = Mathf.Max(0, target.Break - amount);
            if (target.Break == 0) Stagger(target);
            else EmitBreak(target);
        }

        void RevealWeaknesses(BattleUnit target, Weakness match)
        {
            if (target.Enemy == null) return;
            string asset = target.Enemy.name;
            foreach (var w in BreakRules.All)
            {
                if ((match & w) == 0 || WeaknessBook.Knows(asset, w)) continue;
                WeaknessBook.Learn(asset, w);
                Log(target.Name + " is weak to " + BreakRules.Label(w) + "!");
                Emit(new WeaknessRevealedEvent { Unit = target, Weakness = w });
            }
        }

        /// <summary>Breaks a unit: Staggered for PackRates.staggerTurns rounds (this one included), its shown intent
        /// cancelled, and no new intent until it recovers.</summary>
        void Stagger(BattleUnit unit)
        {
            unit.Break = 0;
            unit.StaggerTurns = Mathf.Max(1, PackRates.Current.staggerTurns);
            Log(unit.Name + " is Staggered!");
            Emit(new StaggeredEvent { Unit = unit });
            EmitBreak(unit);
            if (unit.Intent != null)
            {
                unit.Intent = null;
                Emit(new IntentChosenEvent { Unit = unit, Intent = null });
            }
        }

        void EmitBreak(BattleUnit unit)
        {
            Emit(new BreakChangedEvent { Unit = unit, Break = unit.Break, MaxBreak = unit.MaxBreak, Staggered = unit.Staggered });
        }

        /// <summary>Who really takes a hit on <paramref name="target"/>: its living coverer on the other side from the
        /// attacker, else the target.</summary>
        BattleUnit CoveredBy(BattleUnit source, BattleUnit target)
        {
            var cover = target.Statuses.Find(s => s.Definition.special == StatusSpecial.Cover);
            if (cover == null || cover.Source == null || cover.Source == target || !cover.Source.IsAlive) return target;
            if (source != null && cover.Source.Team == source.Team) return target;
            return cover.Source;
        }

        void TryCounter(BattleUnit holder, BattleUnit attacker)
        {
            if (holder == null || attacker == null || !holder.IsAlive || !attacker.IsAlive || holder.Team == attacker.Team) return;
            var counter = holder.Statuses.Find(s => s.Definition.special == StatusSpecial.Counter && s.Stacks > 0);
            if (counter == null) return;
            float power = counter.Power * (holder.HPPercent < 0.5f ? 1f + counter.Bonus : 1f);
            counter.Stacks--;
            if (counter.Stacks <= 0) holder.Statuses.Remove(counter);
            EmitStatuses(holder);
            Log(holder.Name + " counters!");
            DealDamage(holder, attacker, power, false, true, null, null);
            if (counter.Payload != null && attacker.IsAlive && !IsOver) ApplyStatus(holder, attacker, counter.Payload, counter.PayloadTurns, 1);
        }

        /// <summary>Adds <paramref name="amount"/> shield HP to the target (shields stack and refresh).</summary>
        public void GiveShield(BattleUnit source, BattleUnit target, StatusDefinition status, int amount, int duration)
        {
            if (!target.IsAlive || status == null || amount <= 0) return;
            var existing = target.FindStatus(status);
            if (existing == null) target.Statuses.Add(existing = new StatusInstance(status, source, duration, 1));
            existing.Power += amount;
            existing.Remaining = Mathf.Max(existing.Remaining, duration);
            Log(target.Name + " shields " + amount);
            Emit(new FxCueEvent { Unit = target, Cue = FxCueEvent.Shield });
            EmitStatuses(target);
        }

        /// <summary>Readies a counter on the target: <paramref name="triggers"/> hits back of <paramref name="power"/> ATK.
        /// Counters add their triggers; the stronger one's numbers win.</summary>
        public void GiveCounter(BattleUnit source, BattleUnit target, StatusDefinition status, float power, int triggers,
            float lowHPBonus, StatusDefinition onHit, int onHitTurns, int duration)
        {
            if (!target.IsAlive || status == null) return;
            var existing = target.FindStatus(status);
            if (existing == null) target.Statuses.Add(existing = new StatusInstance(status, source, duration, 0));
            existing.Stacks += Mathf.Max(1, triggers);
            existing.Remaining = Mathf.Max(existing.Remaining, duration);
            if (power >= existing.Power)
            {
                existing.Power = power;
                existing.Bonus = lowHPBonus;
                existing.Payload = onHit;
                existing.PayloadTurns = onHitTurns;
            }
            EmitStatuses(target);
        }

        /// <summary>Cancels an enemy's shown intent: it does nothing this round.</summary>
        public void CancelIntent(BattleUnit unit)
        {
            if (unit == null || unit.Intent == null) return;
            unit.Intent = null;
            Emit(new IntentChosenEvent { Unit = unit, Intent = null });
            Emit(new FxCueEvent { Unit = unit, Cue = FxCueEvent.Interrupt });
            Log(unit.Name + " is interrupted!");
        }

        /// <summary>Clears a unit's debuffs (with the cleanse effect when there were any).</summary>
        public void Cleanse(BattleUnit unit)
        {
            if (unit == null || !unit.Statuses.Exists(s => s.Definition.isDebuff)) return;
            Emit(new FxCueEvent { Unit = unit, Cue = FxCueEvent.Cleanse });
            RemoveStatuses(unit, s => s.Definition.isDebuff);
        }

        /// <summary>Removes the matching statuses from a unit.</summary>
        public void RemoveStatuses(BattleUnit unit, Predicate<StatusInstance> match)
        {
            if (unit == null || unit.Statuses.RemoveAll(match) == 0) return;
            unit.HP = Mathf.Min(unit.HP, unit.MaxHP);
            EmitStatuses(unit);
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
            if (status.special == StatusSpecial.Cover && source != null && source != target)
                Emit(new FxCueEvent { Unit = source, Cue = FxCueEvent.Cover });
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
            // Chill at 3 stacks becomes Frozen, and so on.
            var now = target.FindStatus(status);
            if (status.becomesAtMaxStacks != null && now != null && now.Stacks >= maxStacks)
            {
                target.Statuses.Remove(now);
                ApplyStatus(source, target, status.becomesAtMaxStacks, status.becomesTurns, 1);
                Log(target.Name + ": " + status.becomesAtMaxStacks.DisplayName + "!");
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
            // AP = living active members (3 / 2 / 1), plus any bonus earned last turn; the hand holds AP + 4.
            int alive = State.Party.FindAll(p => p.IsAlive).Count;
            State.MaxActionPoints = Mathf.Max(1, alive + State.BonusActionPointsNextRound);
            State.BonusActionPointsNextRound = 0;
            State.ActionPoints = State.MaxActionPoints;
            State.MovesThisRound = 0;
            State.Hand.Capacity = State.MaxActionPoints + BattleState.HandBonus;

            Emit(new RoundStartedEvent { Round = State.Round });
            EmitActionPoints();

            foreach (var enemy in State.Enemies)
            {
                if (!enemy.IsAlive) continue;
                // Still Staggered: no move this round either.
                if (enemy.Staggered)
                {
                    enemy.Intent = null;
                    Emit(new IntentChosenEvent { Unit = enemy, Intent = null });
                    continue;
                }
                // Recovered: its Break value refills in full.
                if (enemy.Breakable && enemy.Break <= 0)
                {
                    enemy.Break = enemy.MaxBreak;
                    EmitBreak(enemy);
                }
                enemy.Intent = EnemyAI.ChooseIntent(enemy, State.Rng, Config.moxieCap);
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

        void GiveStartingMoxie(BattleUnit unit)
        {
            int start = Mathf.Clamp(Mathf.RoundToInt(unit.GetStat(StatType.StartingMoxie)), 0, Config.moxieCap);
            if (start > unit.Moxie) SetMoxie(unit, start);
        }

        void TrySpawnRitual(BattleUnit unit)
        {
            var character = unit.Character;
            if (character == null || unit.Ultimate == null || !unit.IsAlive) return;
            if (unit.Moxie < Config.moxieCap) return;
            if (State.Hand.HasRitualFor(unit) || State.Queue.Exists(c => c.IsRitual && c.Owner == unit)) return;

            var card = new CardInstance(unit.Ultimate, unit, 1);
            State.Hand.Add(card);
            Emit(new RitualSpawnedEvent { Card = card, HandAfter = State.Hand.Snapshot() });
        }

        void Cast(BattleUnit caster, CardInstance card)
        {
            var targets = card.Target != null && card.Target.IsAlive && card.Definition.targetMode == TargetMode.SingleEnemy
                ? new List<BattleUnit> { card.Target }
                : SelectTargets(caster, card.Definition.targetMode);
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
            if (PackRates.Current.bindingOn && caster.Team == Team.Player && !card.IsRitual && caster.IsAlive)
                ApplyBinding(caster, caster.BindingOf(card.Definition));
        }

        /// <summary>A bound Major Arcana's bonus for the card's user, after the card itself (amounts in PackRates):
        /// Sun heals a share of max HP, Moon gives DEF Up, Star gives Ult points.</summary>
        void ApplyBinding(BattleUnit caster, CardBinding binding)
        {
            var rates = PackRates.Current;
            switch (binding)
            {
                case CardBinding.Sun:
                    Heal(caster, caster, Mathf.Max(1, Mathf.RoundToInt(caster.MaxHP * rates.sunHeal)));
                    break;
                case CardBinding.Moon:
                    ApplyStatus(caster, caster, rates.moonStatus, rates.moonTurns, 1);
                    break;
                case CardBinding.Star:
                    AddMoxie(caster, rates.starUlt);
                    break;
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
                if (enemy.Enemy != null && enemy.Enemy.ritual != null && enemy.IsAlive)
                {
                    if (intent.IsRitual) SetMoxie(enemy, 0);
                    else AddMoxie(enemy, Config.moxiePerCast);
                }
            }
        }

        /// <summary>End of the round, in the bible's order (v0.8 §5): 1. buffs and debuffs count down (and run out),
        /// 2. damage and healing over time tick (with the Stagger bonus on a Staggered unit), 3. Stagger counts down.</summary>
        void EndRound()
        {
            var units = new List<BattleUnit>(State.AllUnits);

            // 1. Buffs and debuffs.
            foreach (var unit in units)
            {
                if (!unit.IsAlive || unit.Statuses.Count == 0) continue;
                bool expired = false;
                foreach (var status in unit.Statuses)
                {
                    if (Ticks(status.Definition)) continue;
                    status.Remaining--;
                    expired |= status.Remaining <= 0;
                }
                if (!expired) continue;
                unit.Statuses.RemoveAll(s => s.Remaining <= 0);
                unit.HP = Mathf.Min(unit.HP, unit.MaxHP);
                EmitStatuses(unit);
            }

            // 2. Damage and healing over time.
            foreach (var unit in units)
            {
                if (!unit.IsAlive || unit.Statuses.Count == 0) continue;

                foreach (var status in new List<StatusInstance>(unit.Statuses))
                {
                    var def = status.Definition;
                    if (!Ticks(def)) continue;
                    float stagger = unit.Staggered ? BreakRules.StaggerMultiplier : 1f;
                    if (def.damagePerRoundPercentMaxHP > 0f && unit.IsAlive)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.MaxHP * def.damagePerRoundPercentMaxHP * status.Stacks * stagger));
                        ApplyDamage(status.Source, unit, dmg, false, unit.Staggered, true, tick: def);
                        if (IsOver) return;
                    }
                    if (def.damagePerRoundSourceATK > 0f && unit.IsAlive && status.Source != null)
                    {
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(status.Source.GetStat(StatType.Attack) * def.damagePerRoundSourceATK * status.Stacks * stagger));
                        ApplyDamage(status.Source, unit, dmg, false, unit.Staggered, true, tick: def);
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

            // 3. Stagger counts down last; when it wears off, the Break value refills as the next round starts.
            foreach (var unit in units)
            {
                if (!unit.Staggered) continue;
                unit.StaggerTurns--;
                if (!unit.Staggered && unit.IsAlive) EmitBreak(unit);
            }
        }

        /// <summary>A status that does something each round (damage or healing over time), rather than a buff or debuff.</summary>
        static bool Ticks(StatusDefinition def)
        {
            return def.damagePerRoundPercentMaxHP > 0f || def.damagePerRoundSourceATK > 0f || def.healPerRoundPercentMaxHP > 0f;
        }

        void ApplyDamage(BattleUnit source, BattleUnit target, int amount, bool crit, bool staggerBonus, bool isTick,
            int blocked = 0, bool isCounter = false, StatusDefinition tick = null)
        {
            target.HP = Mathf.Max(0, target.HP - amount);
            Emit(new DamageEvent
            {
                Source = source,
                Target = target,
                Amount = amount,
                Blocked = blocked,
                Crit = crit,
                IsTick = isTick,
                IsCounter = isCounter,
                TickStatus = tick,
                StaggerBonus = staggerBonus,
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
                // The backup steps into the fallen member's place; their cards join the draw pool from now on.
                if (State.Backup.Count > 0)
                {
                    var sub = State.Backup[0];
                    State.Backup.RemoveAt(0);
                    State.Party[State.Party.IndexOf(unit)] = sub;
                    Emit(new UnitEnteredEvent { Unit = sub, Replaces = unit });
                    Log(sub.Name + " steps in!");
                }
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
            bool defeat = !State.Party.Exists(p => p.IsAlive) && State.Backup.Count == 0;
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
                Moves = State.MovesThisRound,
                Moxie = State.Party.ConvertAll(p => p.Moxie)
            });
        }

        void EmitActionPoints()
        {
            Emit(new ActionPointsChangedEvent { Current = State.ActionPoints, Max = State.MaxActionPoints, Moves = State.MovesThisRound });
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
            public int Moves;
            public List<int> Moxie;
        }
    }
}
