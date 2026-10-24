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
        /// <summary>Party members scale their base stats with level; enemies stay at their definition's stats.</summary>
        public int Level { get; private set; }
        /// <summary>Enemy only: the card it will use this round.</summary>
        public CardInstance Intent;
        /// <summary>Party only: the Parts fitted for this fight (their modifiers always apply).</summary>
        public readonly List<PartDefinition> Parts = new List<PartDefinition>();

        List<CardDefinition> incantations;

        public BattleUnit(UnitDefinition definition, Team team)
        {
            Id = ++nextId;
            Definition = definition;
            Team = team;
            Level = definition is EnemyDefinition ? DefaultEnemyLevel : 1;
            HP = MaxHP;
        }

        /// <summary>Enemy definitions hold their stats at this level.</summary>
        public const int DefaultEnemyLevel = 5;
        public const int MaxEnemyLevel = 99;
        /// <summary>Enemy stat growth per level (relative to <see cref="DefaultEnemyLevel"/>).</summary>
        public const float EnemyGrowth = 0.06f;
        public const float BossHP = 3f, BossAttack = 1.25f;

        /// <summary>A boss: 3x HP, 1.25x ATK (and the battle draws it bigger with a BOSS tag).</summary>
        public bool IsBoss { get; private set; }

        /// <summary>An enemy's stat multiplier at a level: 1 at Lv 5, smaller below, larger above.</summary>
        public static float EnemyScale(int level)
        {
            return (1f + EnemyGrowth * (Mathf.Clamp(level, 1, MaxEnemyLevel) - 1)) / (1f + EnemyGrowth * (DefaultEnemyLevel - 1));
        }

        /// <summary>Sets an enemy's level (1-99) and boss flag, and refills its HP.</summary>
        public void SetEnemyLevel(int level, bool boss)
        {
            Level = Mathf.Clamp(level, 1, MaxEnemyLevel);
            IsBoss = boss;
            HP = MaxHP;
        }

        public string Name { get { return Definition.DisplayName; } }
        public bool IsAlive { get { return HP > 0; } }
        public CharacterDefinition Character { get { return Definition as CharacterDefinition; } }
        public EnemyDefinition Enemy { get { return Definition as EnemyDefinition; } }
        public int MaxHP { get { return Mathf.Max(1, Mathf.RoundToInt(GetStat(StatType.MaxHP))); } }
        /// <summary>Sets the level and refills HP to the new max (set HP afterwards to carry damage over).</summary>
        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 1, Leveling.MaxLevel);
            HP = MaxHP;
        }

        /// <summary>
        /// The incantations this unit deals into the hand: its loadout's cards, or the character's defaults.
        /// </summary>
        public List<CardDefinition> Incantations
        {
            get
            {
                if (incantations != null && incantations.Count > 0) return incantations;
                return Character != null ? Character.incantations : new List<CardDefinition>();
            }
        }

        /// <summary>Fits a loadout's cards and Parts. HP is refilled to the (possibly new) max; set HP afterwards to carry damage.</summary>
        Afflatus? elementOverride;
        CardDefinition ultimateOverride;

        /// <summary>This unit's element in battle: its active resonance, else its definition's.</summary>
        public Afflatus Afflatus { get { return elementOverride ?? Definition.afflatus; } }

        /// <summary>This unit's colour (card swirls, queue chips, flashes): its resonance's, else its definition's.</summary>
        public Color Color { get { return AfflatusInfo.ResonanceColor(Definition.color, elementOverride); } }

        /// <summary>The Ult card it can spawn: its active resonance's, else its definition's ritual.</summary>
        public CardDefinition Ultimate
        {
            get
            {
                if (ultimateOverride != null) return ultimateOverride;
                if (Character != null) return Character.ritual;
                return Enemy != null ? Enemy.ritual : null;
            }
        }

        public void ApplyLoadout(Loadout loadout)
        {
            Parts.Clear();
            incantations = null;
            elementOverride = null;
            ultimateOverride = null;
            if (loadout == null) return;
            elementOverride = loadout.Element;
            ultimateOverride = loadout.Ultimate;
            foreach (var part in loadout.Parts) if (part != null) Parts.Add(part);
            if (loadout.Cards != null && loadout.Cards.Exists(c => c != null)) incantations = loadout.Cards.FindAll(c => c != null);
            HP = MaxHP;
        }

        public float HPPercent { get { return (float)HP / MaxHP; } }
        public bool IsStunned { get { return Statuses.Exists(s => s.Definition.preventsAction); } }

        /// <summary>(base + flat) * (1 + percent), summed over all active statuses (times stacks) and fitted Parts.</summary>
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
            foreach (var part in Parts)
            {
                if (!part.AppliesTo(Afflatus)) continue;
                foreach (var mod in part.modifiers)
                {
                    if (mod.stat != stat) continue;
                    if (mod.kind == ModifierKind.Flat) flat += mod.value;
                    else percent += mod.value;
                }
            }
            float baseValue = Definition.stats.Get(stat);
            var character = Character;
            if (character != null && Leveling.Grows(stat)) baseValue *= Leveling.StatScale(Level, character.growthPerLevel);
            if (Enemy != null)
            {
                if (Leveling.Grows(stat)) baseValue *= EnemyScale(Level);
                if (IsBoss && stat == StatType.MaxHP) baseValue *= BossHP;
                if (IsBoss && stat == StatType.Attack) baseValue *= BossAttack;
            }
            return (baseValue + flat) * (1f + percent);
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
