namespace Combat
{
    public enum Afflatus { Star, Mineral, Beast, Plant, Spirit, Intellect }

    public enum DamageType { Physical, Special }

    public enum StatType
    {
        MaxHP,
        Attack,
        PhysicalDefense,
        SpecialDefense,
        CritRate,
        CritDamage,
        /// <summary>Generic damage bonus (0.1 = +10%).</summary>
        DamageBonus,
        /// <summary>Damage taken modifier on the defender (0.2 = takes 20% more).</summary>
        DamageTaken,
        /// <summary>Bonus to damage dealt by incantation (non-ritual) cards.</summary>
        IncantationMight,
        /// <summary>Bonus to damage dealt by ritual (ultimate) cards.</summary>
        RitualMight,
        HealingBonus
    }

    public enum ModifierKind { Flat, Percent }

    public enum TargetMode { SingleEnemy, AllEnemies, RandomEnemy, Self, SingleAlly, AllAllies, RandomAlly }

    public enum EffectTarget { CardTargets, Self, SingleEnemy, AllEnemies, RandomEnemy, SingleAlly, AllAllies, RandomAlly }

    public enum CardKind { Attack, Buff, Debuff, Heal }

    public enum Team { Player, Enemy }

    public enum AfflatusRelation { Neutral, Advantage, Disadvantage }

    public enum BattlePhase { NotStarted, Planning, Resolving, Victory, Defeat }

    public enum StackRule
    {
        /// <summary>Keep current stacks, refresh duration.</summary>
        RefreshDuration,
        /// <summary>Add stacks up to max, refresh duration.</summary>
        AddStacks,
        /// <summary>Overwrite stacks and duration.</summary>
        Replace
    }
}
