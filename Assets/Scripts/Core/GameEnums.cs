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
        True,      // special: ignores resistance and invulnerability, never rolled as an element

        /// <summary>
        /// What an execute reports its damage as: a frost shatter, a death mark, anything that
        /// removes a health bar outright. Kept apart from every other type so that effects keyed
        /// to damage type never mistake a whole health bar for an ordinary hit. Never resisted
        /// and never rolled as an element.
        /// </summary>
        Execute
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

        /// <summary>
        /// Reality and time warping: holding things out of reality, phasing, folding space and
        /// bending time. Took Artifice's position when it was renamed, so the stored integer
        /// kept its meaning.
        /// </summary>
        Aetherics,

        /// <summary>Filler and starter spells that belong to no school and count toward no mastery.</summary>
        Petty
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

    /// <summary>
    /// The character sheet's core attributes. Everything else is derived from these, and every
    /// stat's normal value is <see cref="CharacterSheet.Baseline"/>.
    ///
    /// Stored as integers in boon assets, so each new stat took the position of the old stat
    /// closest to it: Dexterity took Strength's, Power took Intellect's, Athletics took
    /// Agility's and Endurance took Vitality's. A boon that raised Intellect now raises Power
    /// with nothing in the asset needing to change. Do not reorder.
    /// </summary>
    public enum StatType
    {
        /// <summary>Bullet spread, recoil and reload speed.</summary>
        Dexterity,

        /// <summary>Spell damage and maximum mana.</summary>
        Power,

        /// <summary>Spell cooldowns, movement speed, jump height and air control.</summary>
        Athletics,

        /// <summary>Maximum health and mana regeneration.</summary>
        Endurance,

        /// <summary>Reward rarity and crit chance.</summary>
        Luck
    }

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
        SmashPower,       // unused since reinforced barriers were removed; kept so later members keep their numbers
        Lifesteal,

        /// <summary>Multiplies a gun's spread. Dexterity lowers it; 1 is the gun as authored.</summary>
        Spread,

        /// <summary>Multiplies a gun's recoil. Dexterity lowers it; 1 is the gun as authored.</summary>
        Recoil,

        /// <summary>Multiplies the gravity the player falls and jumps under. 1 is normal.</summary>
        GravityScale
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
        Ethereal,

        // Planned statuses. Registered with names and colours so every id resolves, but with no
        // behaviour yet - see PlannedStatus in StatusLibrary.

        /// <summary>Slows movement, and holds an ordinary enemy still at full strength.</summary>
        Snare,

        /// <summary>Cannot cast spells.</summary>
        Silence,

        /// <summary>Cannot shoot.</summary>
        Disarm,

        /// <summary>Moves faster and away from the source, and cannot attack.</summary>
        Fear,

        /// <summary>Cannot see the player, and aims where it last saw them.</summary>
        Blind,

        /// <summary>Cannot tell friend from foe, and attacks the nearest enemy.</summary>
        Confusion,

        /// <summary>Does nothing until it wakes, on damage or when the sleep wears off.</summary>
        Sleep,

        /// <summary>Rises as a zombie on death, and passes the plague on.</summary>
        Plague,

        /// <summary>Psychic damage over time. Intrusive Thoughts.</summary>
        Torment,

        /// <summary>Deals more damage. Soul Bargain, Consecrate.</summary>
        Empowered,

        /// <summary>Takes more damage. The price of Soul Bargain.</summary>
        Exposed,

        /// <summary>Attacks and reloads faster. Howl.</summary>
        Quickened,

        /// <summary>Dies outright below a share of its health, elites included. Wither.</summary>
        Withered,

        /// <summary>Grown large, and hits harder for it. Embiggen.</summary>
        Enlarged,

        /// <summary>Senses every enemy through walls. Blood Scent.</summary>
        Scenting,

        /// <summary>The next direct hit misses. Foretell.</summary>
        Foretold,

        /// <summary>Out of reality: untouchable and unseen. Flicker.</summary>
        Phased,

        /// <summary>Id incarnate: the gun fires itself and never misses. Superid.</summary>
        Unleashed
    }

    public enum FireMode { Semi, Auto, Burst }

    public enum DeliveryKind { Hitscan, Projectile }

    /// <summary>
    /// The family a gun belongs to, which class boons are gated on. Stored as an integer in assets,
    /// so append only. Unassigned is the default a new field deserialises to, and a verifier refuses it.
    /// </summary>
    public enum WeaponClass { Unassigned, Handgun, SMG, Shotgun, Rifle, Sniper, Heavy, Launcher }

    /// <summary>Which part of a build a boon hangs off. Drives the first roll of an offer. Stored as an integer; append only.</summary>
    public enum BoonFamily { Core, Arsenal, Slots, School, Spell, Pact }

    /// <summary>
    /// What right click does on a gun. Only three shapes, because they are the three that
    /// behave differently rather than the three that sound different: Shot fires one modified
    /// round, Salvo empties what is left in the magazine, and Focus is a state held down
    /// rather than a shot at all. A slug and an underbarrel grenade are both Shot.
    /// </summary>
    public enum AltFireKind { None, Shot, Salvo, Focus }

    public enum RoomKind { Combat, Elite, Treasure, Shrine, Forge, Boss, Training }

    /// <summary>Top level flow of a run.</summary>
    public enum GameStateKind
    {
        Loading, ChoosingLoadout, Playing, ChoosingBoon, ChoosingSpell, ChoosingRoom, Paused, Dead, Victory
    }
}
