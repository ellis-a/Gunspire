namespace Gunspire
{
    /// <summary>Who an entity fights for. Damage is only applied across teams.</summary>
    public enum Team { Player, Enemy, Neutral }

    /// <summary>
    /// What damage is made of. Resistances, boons and VFX key off these.
    ///
    /// Four kinds rather than one per element, so an enemy's affinity is a real decision
    /// instead of a lottery over seven near-identical options. Which element something looks
    /// like is now the spell's school; this is only what it does on contact.
    ///
    /// The order is deliberate and must not be rearranged. These are stored as plain integers
    /// in every asset, so the members that kept an old member's index kept its meaning too -
    /// Kinetic reads the old Normal, Energy the old Fire, Necrotic the old Nature. Reordering
    /// would silently re-school 46 assets with nothing to show it had happened.
    /// </summary>
    public enum DamageType
    {
        Kinetic,   // bullets, thrown ice, anything that arrives as an object
        Energy,    // most magic: fire, lightning, lasers
        Psychic,   // attacks on the mind
        Necrotic,  // disease, poison, shadow
        True       // special: ignores resistance and invulnerability, never rolled as an element
    }

    /// <summary>
    /// The tradition a spell belongs to. Thematic rather than mechanical - what a spell is
    /// made of and what it is for are <see cref="DamageType"/> and <see cref="SpellType"/>.
    /// Schools are what a run builds an identity out of, and what a boon can favour wholesale.
    /// </summary>
    public enum SpellSchool
    {
        /// <summary>Fire, ice and storms. The school that simply deals damage well.</summary>
        Elemental,

        /// <summary>Furred things called to fight beside you.</summary>
        Bestial,

        /// <summary>Demons and the deep. Buys power with your own life.</summary>
        Abyssal,

        /// <summary>Heavenly sight. Sharpens the wizard rather than the spell.</summary>
        Divination,

        /// <summary>Zombies, skeletons and spirits, raised and spent.</summary>
        Death,

        /// <summary>The mind as a weapon - and as something to be turned.</summary>
        Psionic,

        /// <summary>Turrets, charged ammunition and things bolted onto a gun.</summary>
        Artifice
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
    /// <summary>
    /// Like <see cref="DamageType"/>, these are stored as integers in assets, so the order is
    /// fixed. Frost took the old Chill's slot and Poison the old Blight's because they are the
    /// same effect renamed; Bleed took Freeze's because removing a member from the middle would
    /// have shifted every id after it and silently re-pointed every authored status payload.
    /// </summary>
    public enum StatusId
    {
        /// <summary>Slows 1% per stack. At full stacks, kinetic damage finishes the target.</summary>
        Frost,

        /// <summary>Bleeds until healed, and never wears off on its own.</summary>
        Bleed,

        /// <summary>Burns for the amount applied, then halves.</summary>
        Burn,

        /// <summary>Ruins aim. Falls off faster if the victim holds still.</summary>
        Poison,

        /// <summary>Amplifies damage taken and deadens hearing.</summary>
        Shock,

        Weaken,   // reduces damage dealt
        Haste,    // move speed buff
        Fortify,  // damage taken reduction
        Mark,     // takes extra crit damage

        /// <summary>The next hit finishes the target. Short, and rare.</summary>
        Deathmark,

        /// <summary>Untouchable by kinetic damage, and doubly hurt by everything else.</summary>
        Ethereal
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
