using System.Collections.Generic;

namespace Combat
{
    public class BattleState
    {
        /// <summary>Most party members on the field at once; the next ones wait as backup.</summary>
        public const int MaxActive = 3;
        /// <summary>How many members can wait in reserve (they step in when an active member falls).</summary>
        public const int MaxBackup = 1;
        /// <summary>Hand cap = this turn's AP + this (3 AP = 7 cards, solo 1 AP = 5).</summary>
        public const int HandBonus = 4;

        /// <summary>The active members on the field (up to <see cref="MaxActive"/>). A fallen member is swapped for the backup.</summary>
        public readonly List<BattleUnit> Party = new List<BattleUnit>();
        /// <summary>Members waiting in reserve, in order (up to <see cref="MaxBackup"/>).</summary>
        public readonly List<BattleUnit> Backup = new List<BattleUnit>();
        /// <summary>Everyone who came to the fight (active, backup and fallen), for loadouts, levels and the results.</summary>
        public readonly List<BattleUnit> Roster = new List<BattleUnit>();
        public readonly List<BattleUnit> Enemies = new List<BattleUnit>();
        public readonly List<CardInstance> Queue = new List<CardInstance>();
        public readonly Hand Hand;
        public readonly CombatConfig Config;
        public readonly System.Random Rng;

        public int Round;
        public int ActionPoints;
        public int MaxActionPoints;
        public int BonusActionPointsNextRound;
        /// <summary>AP spent moving cards this turn (each locks a queue slot that reads "Card Moved").</summary>
        public int MovesThisRound;
        public BattleUnit SelectedEnemy;
        public BattlePhase Phase = BattlePhase.NotStarted;

        public BattleState(CombatConfig config, System.Random rng)
        {
            Config = config;
            Rng = rng;
            Hand = new Hand(config.handSize);
        }

        public IEnumerable<BattleUnit> AllUnits
        {
            get
            {
                foreach (var u in Party) yield return u;
                foreach (var u in Enemies) yield return u;
            }
        }

        public List<BattleUnit> AlliesOf(BattleUnit unit) { return unit.Team == Team.Player ? Party : Enemies; }
        public List<BattleUnit> OpponentsOf(BattleUnit unit) { return unit.Team == Team.Player ? Enemies : Party; }
    }
}
