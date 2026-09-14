using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A time rate for the world that the player does not share. Unity's own time scale freezes the
    /// player along with everything else, so Stop Time, and any slow-motion effect after it, sets
    /// this instead. Projectiles, enemies, minions, familiars, statuses, zones, blasts and telegraphs
    /// advance by <see cref="DeltaTime"/>; the player's body, gun and spells keep real time.
    ///
    /// Requests are keyed and multiply together, so two slows stack, any stop wins, and each effect
    /// releases only its own. While the world is stopped, every hit on anything but the player is held
    /// and lands in one burst on resume, the knockback in those hits included.
    /// </summary>
    public static class WorldClock
    {
        private struct HeldHit
        {
            public Health Target;
            public DamageInfo Info;
        }

        private sealed class TimedRequest
        {
            public object Key;
            public float SecondsLeft;
        }

        private static readonly Dictionary<object, float> Requests = new Dictionary<object, float>();
        private static readonly List<TimedRequest> Timed = new List<TimedRequest>();
        private static readonly List<HeldHit> Held = new List<HeldHit>();
        private static float _rate = 1f;

        public static float Rate => _rate;
        public static bool IsStopped => _rate <= 0f;

        /// <summary>World seconds since the last reset, advancing at the world rate.</summary>
        public static float Now { get; private set; }

        /// <summary>This frame's time for everything that is not the player.</summary>
        public static float DeltaTime => Time.deltaTime * _rate;

        public static int HeldCount => Held.Count;

        /// <summary>This frame's time for one object: real time for the player's body, world time for everything else.</summary>
        public static float DeltaFor(GameObject go) => IsPlayer(go) ? Time.deltaTime : DeltaTime;

        /// <summary>The player rig, or whichever body the player currently occupies.</summary>
        public static bool IsPlayer(GameObject go)
        {
            if (go == null) return false;

            PlayerRig rig = PlayerRig.Instance;
            if (rig != null && go == rig.gameObject) return true;

            Transform body = TargetRegistry.PlayerBody.Transform;
            return body != null && go.transform == body;
        }

        /// <summary>Slows or stops the world under a key. Setting the same key again replaces it.</summary>
        public static void Set(object key, float rate)
        {
            if (key == null) return;
            Requests[key] = Mathf.Max(0f, rate);
            Recompute();
        }

        public static void Release(object key)
        {
            if (key == null) return;

            Timed.RemoveAll(t => t.Key == key);
            if (!Requests.Remove(key)) return;

            bool wasStopped = IsStopped;
            Recompute();
            if (wasStopped && !IsStopped) FlushHeld();
        }

        /// <summary>Stops the world for a length of real time - the player's time, the one still running.</summary>
        public static void StopFor(object key, float realSeconds)
        {
            Set(key, 0f);
            Timed.RemoveAll(t => t.Key == key);
            Timed.Add(new TimedRequest { Key = key, SecondsLeft = realSeconds });
        }

        /// <summary>Advances world time and runs timed requests out. The director calls this each frame with real time.</summary>
        public static void Tick(float realDelta)
        {
            Now += realDelta * _rate;
            if (Timed.Count == 0) return;

            var expired = new List<object>();
            for (int i = 0; i < Timed.Count; i++)
            {
                Timed[i].SecondsLeft -= realDelta;
                if (Timed[i].SecondsLeft <= 0f) expired.Add(Timed[i].Key);
            }

            for (int i = 0; i < expired.Count; i++) Release(expired[i]);
        }

        /// <summary>Whether a hit on this should wait for the world to resume.</summary>
        public static bool ShouldHold(Health target) => IsStopped && target != null && !IsPlayer(target.gameObject);

        public static void Hold(Health target, in DamageInfo info)
            => Held.Add(new HeldHit { Target = target, Info = info });

        /// <summary>Normal time with nothing requested and nothing held, for a new room and for tooling.</summary>
        public static void Reset()
        {
            Requests.Clear();
            Timed.Clear();
            Held.Clear();
            _rate = 1f;
            Now = 0f;
        }

        private static void Recompute()
        {
            float rate = 1f;
            foreach (float requested in Requests.Values) rate *= requested;
            _rate = rate;
        }

        private static void FlushHeld()
        {
            if (Held.Count == 0) return;

            var hits = new List<HeldHit>(Held);
            Held.Clear();

            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Target != null && hits[i].Target.IsAlive) hits[i].Target.TakeDamage(hits[i].Info);
        }
    }
}
