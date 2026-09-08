using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Global hook fired whenever the player successfully uses an ability, spell or movement.
    /// Boons subscribe here rather than every ability having to know which upgrades exist.
    ///
    /// Identified by id rather than by type, because Blink is a movement ability and Firebolt
    /// is a spell, and an upgrade should be able to attach to either.
    /// </summary>
    public static class AbilityEvents
    {
        /// <summary>Ability id, the context it ran with, and its point of interest.</summary>
        public static event Action<string, AbilityContext, Vector3> Used;

        public static void RaiseCast(string abilityId, AbilityContext ctx, Vector3 position)
        {
            if (!string.IsNullOrEmpty(abilityId)) Used?.Invoke(abilityId, ctx, position);
        }

        /// <summary>Cleared between runs so subscriptions from a dead run cannot leak.</summary>
        public static void ClearSubscribers() => Used = null;
    }
}
