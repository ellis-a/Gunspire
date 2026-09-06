namespace WizardGun
{
    /// <summary>Who an entity fights for. Damage is only applied across teams.</summary>
    public enum Team { Player, Enemy, Neutral }

    /// <summary>Damage schools. Resistances, status payloads and VFX key off these.</summary>
    public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Arcane, True }

    /// <summary>The character sheet's core attributes. Everything else is derived from these.</summary>
    public enum StatType { Strength, Intellect, Agility, Vitality, Luck }

    /// <summary>
    /// Derived values. Boons and status effects add flat/percent modifiers to these
    /// rather than touching the core stats, so buffs stack predictably.
    /// </summary>
    public enum Attr
    {
        MaxHealth,
        HealthRegen,
        MaxMana,
        ManaRegen,
        MoveSpeed,
        JumpHeight,
        AirControl,
        DashCharges,
        DashSpeed,
        DamageDealt,      // global outgoing multiplier
        GunDamage,
        SpellPower,
        DamageTaken,      // incoming multiplier; lower is better
        HealingReceived,  // Blight drives this toward zero
        CooldownRate,     // 1.0 = normal; 1.5 = cooldowns tick 50% faster
        AttackSpeed,
        ReloadSpeed,
        CritChance,
        CritDamage,
        SmashPower,       // compared against Smashable.hardness
        Lifesteal
    }

    /// <summary>Status effect identifiers. See <c>StatusLibrary</c> for behaviour.</summary>
    public enum StatusId
    {
        Chill,    // slows; stacks into Freeze
        Freeze,   // immobilised, takes shatter bonus damage
        Burn,     // fire damage over time, thaws Chill/Freeze
        Blight,   // poison damage over time, absorbs healing
        Shock,    // amplifies damage taken
        Weaken,   // reduces damage dealt
        Haste,    // move speed buff
        Fortify,  // damage taken reduction
        Mark      // takes extra crit damage
    }

    public enum FireMode { Semi, Auto, Burst }

    public enum DeliveryKind { Hitscan, Projectile }

    public enum RoomKind { Combat, Elite, Treasure, Shrine, Forge, Boss }

    /// <summary>Top level flow of a run.</summary>
    public enum GameStateKind { Loading, Playing, ChoosingBoon, ChoosingRoom, Paused, Dead, Victory }
}
