namespace Combat
{
    /// <summary>
    /// The five resonances plus Physical (bible v0.6 §3). Values are serialized as numbers, so keep
    /// the order: renaming is safe, reordering is not. Wheel: Cinder > Cryo > Root > Arc > Cinder; Prism is neutral to
    /// all but crits more (see ResonanceChart). Physical: no element, neutral in every matchup (the protagonist's
    /// starting Core).
    /// </summary>
    public enum Resonance { Arc, Cinder, Root, Cryo, Prism, Physical }

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

    /// <summary>What a card does, shown by its type icon and kind word (bible v0.6 §7). Counter: hits back when its
    /// holder is attacked. Defense: shields or covers. Serialized as numbers: add new kinds at the end.</summary>
    public enum CardKind { Attack, Buff, Debuff, Heal, Counter, Defense }

    /// <summary>How often packs give a card (each pack's odds per tier are in PackRates.packs).</summary>
    public enum CardRarity { Common, Rare, Epic }

    /// <summary>A status the battle rules treat specially (its numbers live on the StatusInstance).</summary>
    public enum StatusSpecial
    {
        None,
        /// <summary>Soaks card damage before HP (StatusInstance.Power = shield HP left).</summary>
        Shield,
        /// <summary>Hits back when the holder takes a card hit (Power = the hit's ATK multiple, Stacks = hits left).</summary>
        Counter,
        /// <summary>Enemy card hits on the holder go to the status's Source instead.</summary>
        Cover
    }

    public enum Team { Player, Enemy }

    public enum ResonanceRelation { Neutral, Advantage, Disadvantage }

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
