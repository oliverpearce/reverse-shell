using System.Collections.Generic;
using UnityEngine;

namespace Combat.Tests
{
    static class TestFactory
    {
        public static CardDefinition Card(string name, float multiplier = 1f, bool ritual = false,
            TargetMode target = TargetMode.SingleEnemy)
        {
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.name = name;
            card.displayName = name;
            card.isRitual = ritual;
            card.targetMode = target;
            card.ranks = new List<CardRank>();
            for (int i = 1; i <= 3; i++)
                card.ranks.Add(new CardRank { effects = new List<CardEffect> { new DamageEffect { multiplier = multiplier * i } } });
            return card;
        }

        public static CharacterDefinition Character(string name, Afflatus afflatus = Afflatus.Volt, float attack = 100f,
            CardDefinition ritual = null, params CardDefinition[] incantations)
        {
            var c = ScriptableObject.CreateInstance<CharacterDefinition>();
            c.name = name;
            c.displayName = name;
            c.afflatus = afflatus;
            c.stats = new Stats { maxHP = 1000, attack = attack, physicalDefense = 0f, specialDefense = 0f, critRate = 0f };
            c.incantations = new List<CardDefinition>(incantations);
            c.ritual = ritual;
            return c;
        }

        public static EnemyDefinition Enemy(string name, int hp = 100000, Afflatus afflatus = Afflatus.Light,
            float defense = 0f, params EnemySkill[] skills)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.name = name;
            e.displayName = name;
            e.afflatus = afflatus;
            e.stats = new Stats { maxHP = hp, attack = 100f, physicalDefense = defense, specialDefense = defense, critRate = 0f };
            e.skills = new List<EnemySkill>(skills);
            return e;
        }

        public static StatusDefinition Status(string name, params StatModifier[] modifiers)
        {
            var s = ScriptableObject.CreateInstance<StatusDefinition>();
            s.name = name;
            s.modifiers = new List<StatModifier>(modifiers);
            s.maxStacks = 3;
            return s;
        }

        public static BattleUnit Unit(UnitDefinition def, Team team)
        {
            return new BattleUnit(def, team);
        }
    }
}
