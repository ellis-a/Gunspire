using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The knobs a held-on spell needs and an ordinary cast does not.
    ///
    /// Kept as its own object hanging off <see cref="Spell.Sustain"/>, rather than more fields
    /// on Spell itself, so a fireball's Inspector never shows a wall-zip tickbox. This is the
    /// same shape <see cref="WeaponDefinition.AltFire"/> uses for the parts of a gun that only
    /// some guns have.
    ///
    /// A spell with one of these is a toggle in whatever slot it sits in: pressing the key starts
    /// it, pressing again stops it, and it runs until what it drains dries up or its condition
    /// breaks. A spell without one fires once and goes on cooldown.
    /// </summary>
    [System.Serializable]
    public class SustainProfile
    {
        /// <summary>Mana drained every second it stays up.</summary>
        public float ManaPerSecond = 0f;

        /// <summary>
        /// Health drained every second it stays up, through the Blood Debt, and never below one hit
        /// point: the toggle ends instead. Bloodwake.
        /// </summary>
        public float HealthPerSecond = 0f;

        /// <summary>A toggle that drains nothing. Without it a free toggle would read as no toggle at all.</summary>
        public bool Toggle;

        /// <summary>
        /// Whether this profile is really here. Unity never leaves a plain serialisable class
        /// field null - it builds one on deserialise - so a null check would report every spell
        /// ever loaded from an asset as sustained. <see cref="WeaponDefinition.AltFire"/> has
        /// the same problem and answers it the same way.
        ///
        /// Read off the drains as well as the flag, so a spell asset written when a drain was the
        /// only switch still counts without a migration.
        /// </summary>
        public bool Exists => Toggle || ManaPerSecond > 0f || HealthPerSecond > 0f;

        /// <summary>Seconds it can stay up. Zero means until the drain runs out.</summary>
        public float MaxDuration = 0f;

        /// <summary>Percent bonus applied to move speed while active.</summary>
        public float MoveSpeedBonus = 0f;

        /// <summary>Ignores damage while active.</summary>
        public bool Invulnerable;

        /// <summary>Drops the moment the caster moves. Pairs with <see cref="Invulnerable"/>.</summary>
        public bool BreakOnMovement;

        /// <summary>Hidden from enemy sight while active. Footsteps are still heard. Invisibility.</summary>
        public bool HideFromSight;

        /// <summary>Drops the moment the caster fires a gun, casts another spell, or swings.</summary>
        public bool BreakOnShoot;
        public bool BreakOnCast;
        public bool BreakOnMelee;

        /// <summary>
        /// Fires a line at whatever the caster is looking at, hauls them to the first surface it
        /// hits, and makes that surface the floor. See <see cref="PlayerMotor.BeginWallZip"/>.
        /// </summary>
        public bool WallZip;

        /// <summary>Swims through the air instead of falling. See <see cref="PlayerMotor.Buoyant"/>.</summary>
        public bool Buoyant;

        /// <summary>Ground laid behind the caster while it runs. Burning Feet's fire, Bloodwake's wake.</summary>
        public TrailProfile Trail = new TrailProfile();

        /// <summary>One line for the HUD and the pedestal prompt.</summary>
        public string CostLine()
        {
            string duration = MaxDuration > 0f ? ", up to " + MaxDuration.ToString("0.#") + "s" : "";

            if (ManaPerSecond > 0f && HealthPerSecond > 0f)
                return ManaPerSecond.ToString("0") + " mana/s, " + HealthPerSecond.ToString("0") + " health/s" + duration;
            if (HealthPerSecond > 0f) return HealthPerSecond.ToString("0") + " health/s" + duration;
            if (ManaPerSecond > 0f) return ManaPerSecond.ToString("0") + " mana/s" + duration;
            return "toggle" + duration;
        }
    }

    /// <summary>
    /// Hold the key to charge, release to cast. The charge reached is on
    /// <see cref="AbilityContext.Charge"/> for the effects to scale by. Costs and the cooldown are
    /// paid on release, so letting go too early to count costs nothing.
    /// </summary>
    [System.Serializable]
    public class ChargeProfile
    {
        /// <summary>Seconds held to reach a full charge. Zero means the spell is not charged.</summary>
        public float SecondsToFull = 0f;

        /// <summary>Released below this fraction, nothing is cast and nothing is spent.</summary>
        [Range(0f, 1f)] public float MinimumFraction = 0f;

        public bool Exists => SecondsToFull > 0f;
    }

    /// <summary>
    /// One mode of a stance: what it is called, how it reads, and what it puts on every bullet while
    /// it is the active one.
    /// </summary>
    [System.Serializable]
    public class StanceMode
    {
        public string Name = "Mode";
        public Color Tint = Color.white;
        public System.Collections.Generic.List<StatusApplication> BulletStatuses =
            new System.Collections.Generic.List<StatusApplication>();

        /// <summary>Changes to the caster's attributes while this mode is the active one.</summary>
        public System.Collections.Generic.List<StanceModifier> Modifiers =
            new System.Collections.Generic.List<StanceModifier>();

        /// <summary>Damage a second to every enemy within the radius, while active. Elemental Form's fire.</summary>
        public float AuraDamagePerSecond;
        public float AuraRadius = 4f;
    }

    [System.Serializable]
    public class StanceModifier
    {
        public Attr Attr = Attr.MoveSpeed;

        /// <summary>A fraction: 0.2 is twenty percent more, -0.2 twenty percent less.</summary>
        public float Percent;
    }

    /// <summary>
    /// Hurting ground laid behind something moving, for whatever carries a <see cref="TrailEmitter"/>: a
    /// held spell's caster or a projectile. Inert until it has a radius.
    /// </summary>
    [System.Serializable]
    public class TrailProfile
    {
        public float Radius;
        public float Spacing = 1.5f;
        public float SegmentLifetime = 2.5f;
        public float DamagePerTick = 3f;
        public float TickInterval = 0.5f;
        public DamageType DamageType = DamageType.Energy;
        public Color Tint = new Color(1f, 0.5f, 0.15f);
        public System.Collections.Generic.List<StatusApplication> Statuses =
            new System.Collections.Generic.List<StatusApplication>();

        public bool Exists => Radius > 0f;

        /// <summary>Starts a trail behind the carrier, its damage scaled by the caster's outgoing multiplier.</summary>
        public TrailEmitter AttachTo(Transform carrier, Team team, GameObject source, float damageScale, DamageOrigin origin)
        {
            TrailEmitter trail = TrailEmitter.Attach(carrier, team, source);
            trail.Radius = Radius;
            trail.Spacing = Spacing;
            trail.SegmentLifetime = SegmentLifetime;
            trail.DamagePerTick = DamagePerTick * damageScale;
            trail.TickInterval = TickInterval;
            trail.DamageType = DamageType;
            trail.Origin = origin;
            trail.Tint = Tint;
            trail.Statuses = new System.Collections.Generic.List<StatusApplication>(Statuses);
            return trail;
        }
    }

    /// <summary>
    /// A spell that is a standing state rather than a cast: always on while bound, starting in its
    /// first mode, with each tap of its key stepping to the next and wrapping round. It never
    /// switches off, drains nothing and has no cooldown, so the only price is the slot. Elemental
    /// Form, whose modes are fire, ice and storm.
    /// </summary>
    [System.Serializable]
    public class StanceProfile
    {
        public System.Collections.Generic.List<StanceMode> Modes =
            new System.Collections.Generic.List<StanceMode>();

        public bool Exists => Modes != null && Modes.Count > 0;
    }
}
