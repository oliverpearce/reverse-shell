namespace Combat
{
    /// <summary>
    /// The five resonances plus Basic (bible v0.8 §3). Values are serialized as numbers, so keep
    /// the order: renaming is safe, reordering is not. There is no advantage wheel: a resonance only matters in a
    /// fight when an enemy is weak to it (<see cref="Weakness"/>). Basic: no element (the protagonist's starting Core).
    /// </summary>
    public enum Resonance { Arc, Cinder, Root, Cryo, Prism, Basic }

    /// <summary>A card's attack type, for Break weaknesses (bible v0.8 §5): Physical or Incantation.</summary>
    public enum AttackType { Physical, Incantation }

    /// <summary>What an enemy's Break value is weak to: attack types and resonances (bible v0.8 §5). A hit from a card
    /// with a matching attack type or resonance deals that card's Break damage.</summary>
    [System.Flags]
    public enum Weakness
    {
        None = 0,
        Physical = 1 << 0,
        Incantation = 1 << 1,
        Arc = 1 << 2,
        Cinder = 1 << 3,
        Root = 1 << 4,
        Cryo = 1 << 5,
        Prism = 1 << 6
    }

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
    /// <summary>A card's rarity: how often packs give it. Legendary is each resonance's one or two strongest cards, with
    /// a gold shine (CardHolo).</summary>
    public enum CardRarity { Common, Rare, Epic, Legendary }

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
