namespace Combat
{
    /// <summary>
    /// The six elements (code name Afflatus). Values are serialized as numbers, so keep the order: renaming is safe,
    /// reordering is not. Wheel: Volt > Fire > Toxin > Earth > Volt; Light and Void beat each other (see AfflatusChart).
    /// Each one is drawn with effects from the Super Pixel Effects Gigapack (see AfflatusInfo).
    /// None = Physical: no element, neutral in every matchup (the protagonist's starting Core; bible §3). Volt is the
    /// story's Electric resonance.
    /// </summary>
    public enum Afflatus { Volt, Fire, Toxin, Earth, Light, Void, None }

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
        HealingBonus,
        /// <summary>Ult points (Moxie) a unit starts every battle with (Flat, whole points).</summary>
        StartingMoxie
    }

    public enum ModifierKind { Flat, Percent }

    public enum TargetMode { SingleEnemy, AllEnemies, RandomEnemy, Self, SingleAlly, AllAllies, RandomAlly }

    public enum EffectTarget { CardTargets, Self, SingleEnemy, AllEnemies, RandomEnemy, SingleAlly, AllAllies, RandomAlly }

    /// <summary>What a card does, shown by its type icon and kind word. Counter: triggers under certain conditions.</summary>
    public enum CardKind { Attack, Buff, Debuff, Heal, Counter }

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
