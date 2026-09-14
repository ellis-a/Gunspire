using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Where things died recently. Enemies are destroyed the moment they die, so anything that works
    /// with corpses - Corpse Explosion bouncing between them - reads these records instead. Each is kept
    /// for a few seconds of world time and dropped when a floor ends.
    ///
    /// Every death is recorded, the player's own minions included, with the side it fell on; whether
    /// your own dead count as corpses is the spell's call.
    /// </summary>
    public static class DeathRecords
    {
        public struct Record
        {
            public Vector3 Position;
            public float Time;
            public Team Team;
            public bool WasElite;
        }

        /// <summary>World seconds a record is kept.</summary>
        public const float Lifetime = 5f;

        private static readonly List<Record> Records = new List<Record>();
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SubscribeOnLoad()
        {
            _subscribed = false;
            Records.Clear();
            EnsureSubscribed();
        }

        /// <summary>Hooks the death and floor events. Safe to call again; tooling calls it because edit mode never loads.</summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;

            Health.AnyDied += OnAnyDied;
            LevelEvents.FloorLeaving += floor => Clear();
        }

        public static int Count
        {
            get
            {
                Purge();
                return Records.Count;
            }
        }

        /// <summary>Recent deaths, optionally only those on one side, newest last.</summary>
        public static void Collect(List<Record> into, Team? team = null)
        {
            into.Clear();
            Purge();

            for (int i = 0; i < Records.Count; i++)
                if (team == null || Records[i].Team == team.Value) into.Add(Records[i]);
        }

        public static void Clear() => Records.Clear();

        private static void OnAnyDied(Health victim, DamageInfo info)
        {
            if (victim == null) return;

            Records.Add(new Record
            {
                Position = victim.transform.position,
                Time = WorldClock.Now,
                Team = victim.Team,
                WasElite = victim.IsElite
            });
        }

        private static void Purge() => Records.RemoveAll(r => WorldClock.Now - r.Time > Lifetime);
    }
}
