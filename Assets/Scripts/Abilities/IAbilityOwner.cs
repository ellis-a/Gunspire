using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// What an <see cref="AbilityAttack"/> needs from whatever is using it.
    ///
    /// Enemies and familiars run identical attack chains against opposite teams, so the attack
    /// component asks for this rather than for a concrete controller. The only thing that
    /// differs between a Cultist's volley and a familiar's is which Team the context carries
    /// and what Target points at.
    /// </summary>
    public interface IAbilityOwner
    {
        GameObject GameObject { get; }
        Transform Transform { get; }

        /// <summary>Where shots leave from. Null falls back to the transform.</summary>
        Transform Muzzle { get; }

        /// <summary>What this is currently fighting, or null.</summary>
        Transform Target { get; }

        CharacterSheet Sheet { get; }
        Health Health { get; }
        StatusController Status { get; }

        /// <summary>Side of the fight. Decides who the chain can hit.</summary>
        Team Team { get; }
    }
}
