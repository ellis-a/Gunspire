using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Anything loud enough to give the player away. Guns, spells and footsteps raise one of
    /// these; enemies listen and decide for themselves whether it was loud enough to reach them.
    ///
    /// A static event rather than a manager, matching <see cref="Health.AnyDamaged"/>: there is
    /// never more than one listener set and nothing needs to own the wiring.
    /// </summary>
    public static class Noise
    {
        /// <summary>Where it happened, and how far it carries before it stops being worth hearing.</summary>
        public static event Action<Vector3, float> Heard;

        /// <summary>
        /// Loudness is a multiplier on the listener's own hearing range, not a distance. A gun
        /// at 1.0 is audible exactly as far as an enemy can hear; footsteps at 0.35 mean you
        /// have to be a third as close before walking gives you away.
        /// </summary>
        public static void Emit(Vector3 position, float loudness)
        {
            if (loudness <= 0f) return;
            Heard?.Invoke(position, loudness);
        }
    }
}
