using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Tracks the live contents of the room the player is in and decides when it counts as
    /// cleared, which is what unseals the exit portal.
    /// </summary>
    public class RoomRuntime : MonoBehaviour
    {
        public RoomKind Kind;
        public Vector3 PlayerSpawn;
        public ExitPortal Portal;

        private readonly List<EnemyController> _enemies = new List<EnemyController>();
        private bool _cleared;

        public bool IsCleared => _cleared;
        public int EnemiesRemaining => _enemies.Count;
        public int EnemiesAtStart { get; private set; }

        public event Action Cleared;

        public void Register(EnemyController enemy)
        {
            if (enemy == null) return;
            _enemies.Add(enemy);
            EnemiesAtStart = Mathf.Max(EnemiesAtStart, _enemies.Count);

            Health health = enemy.Health;
            if (health != null) health.Died += info => Unregister(enemy);
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
