using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Everything enemies may choose to fight: the body the player currently occupies, and every
    /// registered minion. Enemies, their perception and the flow field ask this rather than reaching
    /// for <see cref="PlayerRig.Instance"/>, so possession moves their attention to the new body and
    /// a minion becomes a target just by registering.
    ///
    /// Familiars do not register. They stay collateral that never pulls aggro until the walking
    /// minion framework arrives.
    /// </summary>
    public static class TargetRegistry
    {
        /// <summary>One thing enemies may fight.</summary>
        public sealed class Entry
        {
            public Transform Transform;
            public Health Health;
            public bool IsPlayerBody;

            /// <summary>Out of every sight check, but still heard. Invisibility.</summary>
            public bool HiddenFromSight;

            /// <summary>Out of hearing as well. A body left behind by Assume Identity.</summary>
            public bool HiddenFromHearing;

            public bool IsAlive => Transform != null && (Health == null || Health.IsAlive);
        }

        /// <summary>How much closer another target must be before an enemy gives up the one it has.</summary>
        public const float StickyMargin = 2f;

        private static readonly List<Entry> MinionList = new List<Entry>();
        private static readonly Entry RigBody = new Entry { IsPlayerBody = true };
        private static Entry _takenBody;

        public static IReadOnlyList<Entry> Minions => MinionList;

        /// <summary>
        /// The body the player is in right now: the player rig, unless something has taken over
        /// another body. Never null; its Transform is null when there is no player at all.
        /// </summary>
        public static Entry PlayerBody
        {
            get
            {
                if (_takenBody != null && _takenBody.IsAlive) return _takenBody;

                PlayerRig rig = PlayerRig.Instance;
                RigBody.Transform = rig != null ? rig.transform : null;
                RigBody.Health = rig != null ? rig.Health : null;
                return RigBody;
            }
        }

        /// <summary>Moves the player's body somewhere else, as possession does. Null goes back to the rig.</summary>
        public static Entry SetPlayerBody(Transform body, Health health)
        {
            _takenBody = body == null ? null : new Entry { Transform = body, Health = health, IsPlayerBody = true };
            return PlayerBody;
        }

        public static Entry RegisterMinion(Transform minion, Health health)
        {
            var entry = new Entry { Transform = minion, Health = health };
            MinionList.Add(entry);
            return entry;
        }

        public static void Unregister(Entry entry) => MinionList.Remove(entry);

        /// <summary>Forgets every registration, for a restart and for tooling.</summary>
        public static void Clear()
        {
            MinionList.Clear();
            _takenBody = null;
            RigBody.HiddenFromSight = false;
            RigBody.HiddenFromHearing = false;
        }

        /// <summary>Everything currently worth fighting, the player's body first.</summary>
        public static void Collect(List<Entry> into)
        {
            into.Clear();

            Entry body = PlayerBody;
            if (body.IsAlive) into.Add(body);

            for (int i = MinionList.Count - 1; i >= 0; i--)
            {
                // Destroyed without unregistering: drop it rather than keep a dead reference.
                if (MinionList[i].Transform == null)
                {
                    MinionList.RemoveAt(i);
                    continue;
                }

                if (MinionList[i].IsAlive) into.Add(MinionList[i]);
            }
        }

        /// <summary>True when a noise at this point comes from something hidden from hearing.</summary>
        public static bool IsInaudibleAt(Vector3 point)
        {
            Entry body = PlayerBody;
            if (body.HiddenFromHearing && body.Transform != null && Near(body.Transform.position, point)) return true;

            for (int i = 0; i < MinionList.Count; i++)
            {
                Entry minion = MinionList[i];
                if (minion.HiddenFromHearing && minion.Transform != null && Near(minion.Transform.position, point))
                    return true;
            }
            return false;
        }

        private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 4f;

        /// <summary>
        /// What an enemy fights. The nearest candidate, except that the current target is kept unless
        /// another is at least <see cref="StickyMargin"/> closer, so an enemy halfway between two does
        /// not flip back and forth. An elite takes the player's body whenever it can reach it, and only
        /// turns on minions when it cannot. Pure, so the rules can be checked without a scene.
        /// </summary>
        public static Entry Choose(Vector3 from, Transform current, bool elite, IReadOnlyList<Entry> candidates,
            System.Func<Entry, bool> reachable)
        {
            Entry nearest = null;
            Entry body = null;
            Entry kept = null;
            float nearestDistance = float.MaxValue;
            float keptDistance = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                Entry candidate = candidates[i];
                if (candidate == null || !candidate.IsAlive) continue;

                float distance = Vector3.Distance(from, candidate.Transform.position);
                if (candidate.IsPlayerBody) body = candidate;

                if (current != null && candidate.Transform == current)
                {
                    kept = candidate;
                    keptDistance = distance;
                }

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            if (elite && body != null && (reachable == null || reachable(body))) return body;
            if (kept != null && nearestDistance > keptDistance - StickyMargin) return kept;
            return nearest;
        }
    }
}
