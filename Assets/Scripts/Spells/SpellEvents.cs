using System;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Global spell hooks. Boons subscribe here instead of every spell having to know
    /// which upgrades exist. Position is whatever the spell considers its point of interest
    /// (impact point, blink destination, cone origin).
    /// </summary>
    public static class SpellEvents
    {
        public static event Action<Spell, SpellContext, Vector3> Cast;

        public static void RaiseCast(Spell spell, SpellContext ctx, Vector3 position)
            => Cast?.Invoke(spell, ctx, position);

        /// <summary>Cleared between runs so subscriptions from a dead run cannot leak.</summary>
        public static void ClearSubscribers() => Cast = null;
    }
}
