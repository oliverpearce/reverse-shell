using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public class BattleUnit
    {
        static int nextId;

        public readonly int Id;
        public readonly UnitDefinition Definition;
        public readonly Team Team;
        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();

        public int HP;
        public int Moxie;
        /// <summary>Enemy only: the card it will use this round.</summary>
        public CardInstance Intent;

        public BattleUnit(UnitDefinition definition, Team team)
        {
            Id = ++nextId;
            Definition = definition;
            Team = team;
            HP = MaxHP;
        }

        public string Name { get { return Definition.DisplayName; } }
        public bool IsAlive { get { return HP > 0; } }
        public CharacterDefinition Character { get { return Definition as CharacterDefinition; } }
        public EnemyDefinition Enemy { get { return Definition as EnemyDefinition; } }
        public int MaxHP { get { return Mathf.Max(1, Mathf.RoundToInt(GetStat(StatType.MaxHP))); } }
        public float HPPercent { get { return (float)HP / MaxHP; } }
        public bool IsStunned { get { return Statuses.Exists(s => s.Definition.preventsAction); } }

        /// <summary>(base + flat) * (1 + percent), summed over all active statuses and stacks.</summary>
        public float GetStat(StatType stat)
        {
            float flat = 0f, percent = 0f;
            foreach (var status in Statuses)
            {
                foreach (var mod in status.Definition.modifiers)
                {
                    if (mod.stat != stat) continue;
                    if (mod.kind == ModifierKind.Flat) flat += mod.value * status.Stacks;
                    else percent += mod.value * status.Stacks;
                }
            }
            return (Definition.stats.Get(stat) + flat) * (1f + percent);
        }

        public StatusInstance FindStatus(StatusDefinition definition)
        {
            return Statuses.Find(s => s.Definition == definition);
        }

        public List<StatusSnapshot> SnapshotStatuses()
        {
            return Statuses.ConvertAll(s => new StatusSnapshot(s.Definition, s.Remaining, s.Stacks));
        }

        public override string ToString() { return Name; }
    }

    public class StatusInstance
    {
        public readonly StatusDefinition Definition;
        public readonly BattleUnit Source;
        public int Remaining;
        public int Stacks;

        public StatusInstance(StatusDefinition definition, BattleUnit source, int remaining, int stacks)
        {
            Definition = definition;
            Source = source;
            Remaining = remaining;
            Stacks = stacks;
        }
    }

    public struct StatusSnapshot
    {
        public readonly StatusDefinition Definition;
        public readonly int Remaining;
        public readonly int Stacks;

        public StatusSnapshot(StatusDefinition definition, int remaining, int stacks)
        {
            Definition = definition;
            Remaining = remaining;
            Stacks = stacks;
        }
    }
}
