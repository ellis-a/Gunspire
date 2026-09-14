using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Live hazards a character should not want to stand in: a burning patch, a fire trail, a gate's
    /// centre. Enemies feel a soft push away from any hazard that would hurt them, summed with the push
    /// that keeps a pack apart - a nudge they can be shoved through, not a wall they refuse to cross.
    /// </summary>
    public static class Hazards
    {
        public sealed class Hazard
        {
            public Vector3 Position;
            public float Radius;
            public Team Owner;

            internal Transform Follow;
            internal bool Follows;

            public Vector3 Centre => Follows && Follow != null ? Follow.position : Position;

            /// <summary>A hazard hurts everyone but the side that made it, and a neutral one hurts everyone.</summary>
            public bool Hurts(Team team) => Owner == Team.Neutral || Owner != team;
        }

        /// <summary>How far past its edge a hazard starts to push.</summary>
        public const float Margin = 1.5f;

        /// <summary>The push at a hazard's very centre, falling to nothing at its edge plus the margin.</summary>
        public const float Strength = 1.2f;

        private static readonly List<Hazard> Live = new List<Hazard>();

        public static int Count => Live.Count;

        /// <summary>A hazard that moves with an object, such as a zone. Unregister it when the object goes.</summary>
        public static Hazard Register(Transform follow, float radius, Team owner)
        {
            var hazard = new Hazard
            {
                Follow = follow, Follows = true, Position = follow != null ? follow.position : Vector3.zero,
                Radius = radius, Owner = owner
            };
            Live.Add(hazard);
            return hazard;
        }

        public static Hazard Register(Vector3 position, float radius, Team owner)
        {
            var hazard = new Hazard { Position = position, Radius = radius, Owner = owner };
            Live.Add(hazard);
            return hazard;
        }

        public static void Unregister(Hazard hazard) => Live.Remove(hazard);

        /// <summary>Forgets every hazard, for a restart and for tooling.</summary>
        public static void Clear() => Live.Clear();

        /// <summary>The summed push on something of the given side standing here. Zero when nothing is near.</summary>
        public static Vector3 PushAt(Vector3 position, Team team)
        {
            Vector3 push = Vector3.zero;

            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Hazard hazard = Live[i];

                // Its object was destroyed without unregistering, so there is nothing left to avoid.
                if (hazard.Follows && hazard.Follow == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }

                if (!hazard.Hurts(team)) continue;

                Vector3 away = position - hazard.Centre;
                away.y = 0f;

                float reach = hazard.Radius + Margin;
                float distance = away.magnitude;
                if (distance >= reach) continue;

                Vector3 direction = distance > 0.01f ? away / distance : Vector3.right;
                push += direction * (Strength * (1f - distance / reach));
            }

            return push;
        }
    }
}
