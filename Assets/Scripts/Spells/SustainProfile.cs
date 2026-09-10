using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The knobs a held-down movement spell needs and an ordinary cast does not.
    ///
    /// Kept as its own object hanging off <see cref="Spell.Sustain"/>, rather than six more
    /// fields on Spell itself, so a fireball's Inspector never shows a wall-zip tickbox. This
    /// is the same shape <see cref="WeaponDefinition.AltFire"/> uses for the parts of a gun
    /// that only some guns have.
    ///
    /// A spell with one of these is a toggle: pressing the key starts it, pressing again stops
    /// it, and it runs until the mana dries up or its condition breaks. A spell without one
    /// fires once and goes on cooldown, whichever slot it sits in.
    /// </summary>
    [System.Serializable]
    public class SustainProfile
    {
        /// <summary>
        /// Drained every second it stays up. This is what makes holding it a cost, and it
        /// doubles as the switch for whether the profile counts at all - see
        /// <see cref="Exists"/>.
        /// </summary>
        public float ManaPerSecond = 0f;

        /// <summary>
        /// Whether this profile is really here. Unity never leaves a plain serialisable class
        /// field null - it builds one on deserialise - so a null check would report every spell
        /// ever loaded from an asset as sustained. <see cref="WeaponDefinition.AltFire"/> has
        /// the same problem and answers it the same way.
        ///
        /// Read off the drain rather than a separate tickbox, so the two cannot disagree: a
        /// held mode that costs nothing per second is not a held mode.
        /// </summary>
        public bool Exists => ManaPerSecond > 0f;

        /// <summary>Seconds it can stay up. Zero means until the mana runs out.</summary>
        public float MaxDuration = 0f;

        /// <summary>Percent bonus applied to move speed while active.</summary>
        public float MoveSpeedBonus = 0f;

        /// <summary>Ignores damage while active.</summary>
        public bool Invulnerable;

        /// <summary>Drops the moment the caster moves. Pairs with <see cref="Invulnerable"/>.</summary>
        public bool BreakOnMovement;

        /// <summary>
        /// Fires a line at whatever the caster is looking at, hauls them to the first surface it
        /// hits, and makes that surface the floor. See <see cref="PlayerMotor.BeginWallZip"/>.
        /// </summary>
        public bool WallZip;

        /// <summary>One line for the HUD and the pedestal prompt.</summary>
        public string CostLine()
        {
            string duration = MaxDuration > 0f ? ", up to " + MaxDuration.ToString("0.#") + "s" : "";
            return ManaPerSecond.ToString("0") + " mana/s" + duration;
        }
    }
}
