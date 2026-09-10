namespace Gunspire
{
    /// <summary>Who an entity fights for. Damage is only applied across teams.</summary>
    public enum Team { Player, Enemy, Neutral }

    /// <summary>
    /// Damage schools. Resistances, boons and VFX key off these.
    /// To add one: add a member here and a case in <see cref="DamageTypes"/>. Nothing else
    /// needs to change - everything reads the registry rather than switching on the enum.
    /// </summary>
    public enum DamageType
    {
        Normal,   // plain kinetic rounds
        Fire,
        Frost,
        Nature,
        Shadow,
        Astral,
        True      // special: ignores resistance and invulnerability, never rolled as an element
    }

    /// <summary>What a spell is for. Boons can buff a whole category at once.</summary>
    public enum SpellType { Attack, Mobility, Control, Ward }

    /// <summary>
    /// Which slot a spell can be bound to. A spell only ever appears in offers for its own
    /// slot, so the Shift slot cannot be filled with a fireball and Q cannot be filled with a
    /// dash.
    ///
    /// Cast is deliberately first. Every spell asset authored before slots existed
    /// deserialises this field to zero, and zero has to mean "an ordinary Q/E spell" or the
    /// whole existing roster would silently move slots.
    /// </summary>
    public enum SpellSlot { Cast, Movement, Melee }

    /// <summary>
    /// What an enemy does before it has noticed the player. Once something activates it, this
    /// stops mattering - nothing goes back to idling.
    /// </summary>
    public enum IdleActivity
    {
        /// <summary>Holds its ground, watching whatever direction it was placed facing.</summary>
        Stand,

        /// <summary>Drifts to nearby spots at random, which makes a room look inhabited.</summary>
        Wander,

        /// <summary>Never idles at all - comes for you from the moment the floor loads.</summary>
        Hunt,

        /// <summary>Walks a fixed round of points picked when it spawned, and keeps to it.</summary>
        Patrol
    }

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

    /// <summary>
    /// What right click does on a gun. Only three shapes, because they are the three that
    /// behave differently rather than the three that sound different: Shot fires one modified
    /// round, Salvo empties what is left in the magazine, and Focus is a state held down
    /// rather than a shot at all. A slug and an underbarrel grenade are both Shot.
    /// </summary>
    public enum AltFireKind { None, Shot, Salvo, Focus }

    public enum RoomKind { Combat, Elite, Treasure, Shrine, Forge, Boss }

    /// <summary>Top level flow of a run.</summary>
    public enum GameStateKind
    {
        Loading, ChoosingLoadout, Playing, ChoosingBoon, ChoosingSpell, ChoosingRoom, Paused, Dead, Victory
    }
}
