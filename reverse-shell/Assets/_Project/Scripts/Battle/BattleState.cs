using System.Collections.Generic;

namespace Combat
{
    public class BattleState
    {
        public readonly List<BattleUnit> Party = new List<BattleUnit>();
        public readonly List<BattleUnit> Enemies = new List<BattleUnit>();
        public readonly List<CardInstance> Queue = new List<CardInstance>();
        public readonly Hand Hand;
        public readonly CombatConfig Config;
        public readonly System.Random Rng;

        public int Round;
        public int ActionPoints;
        public int MaxActionPoints;
        public int BonusActionPointsNextRound;
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
