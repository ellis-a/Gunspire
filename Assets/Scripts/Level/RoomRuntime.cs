using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Tracks the live contents of the room the player is in and decides when it counts as
    /// cleared, which is what unseals the exit portal.
    /// </summary>
    public class RoomRuntime : MonoBehaviour
    {
        public RoomKind Kind;
        public Vector3 PlayerSpawn;

        /// <summary>Which way the player looks on arrival. A maze entrance can be on any side.</summary>
        public Quaternion PlayerFacing = Quaternion.identity;
        public ExitPortal Portal;

        /// <summary>The maze this room was built from, and the route to its exit. Both null on a rectangular room.</summary>
        public MazeLayout Maze;
        public ExitRouteMap ExitRoute;

        private readonly List<EnemyController> _enemies = new List<EnemyController>();
        private bool _cleared;

        public bool IsCleared => _cleared;
        public int EnemiesRemaining => _enemies.Count;
        public int EnemiesAtStart { get; private set; }

        public event Action Cleared;

        public void Register(EnemyController enemy)
        {
            if (enemy == null || _enemies.Contains(enemy)) return;
            _enemies.Add(enemy);
            EnemiesAtStart = Mathf.Max(EnemiesAtStart, _enemies.Count);

            Health health = enemy.Health;
            if (health != null) health.Died += info => Unregister(enemy);
        }

        /// <summary>
        /// Still counted by this room. A hidden enemy stays counted, which is what keeps a room with a
        /// banished enemy in it from clearing.
        /// </summary>
        public bool Contains(EnemyController enemy) => _enemies.Contains(enemy);

        /// <summary>The room an enemy is registered with, or null. Found by search, since only copies ask.</summary>
        public static RoomRuntime Holding(EnemyController enemy)
        {
            if (enemy == null) return null;

            foreach (RoomRuntime room in FindObjectsByType<RoomRuntime>(FindObjectsSortMode.None))
                if (room.Contains(enemy)) return room;

            return null;
        }

        private void Unregister(EnemyController enemy)
        {
            _enemies.Remove(enemy);
            CheckCleared();
        }

        private void Start()
        {
            // Rooms with nothing to fight are cleared the moment you walk in.
            CheckCleared();
        }

        private void Update()
        {
            // Cheap safety net for enemies destroyed without firing their death event.
            for (int i = _enemies.Count - 1; i >= 0; i--)
                if (_enemies[i] == null) _enemies.RemoveAt(i);

            if (!_cleared && _enemies.Count == 0) CheckCleared();
        }

        private void CheckCleared()
        {
            if (_cleared || _enemies.Count > 0) return;

            _cleared = true;
            if (Portal != null) Portal.SetOpen(true);
            Cleared?.Invoke();
            GameDirector.Instance?.OnRoomCleared(this);
        }
    }
}
