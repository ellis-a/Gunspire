using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The last three seconds of the player, for Rewind: ten snapshots a second of position, health, the
    /// rounds in both holster slots, which gun is out, and the wall-walking up direction. A fixed rate
    /// keeps the history the same length at any frame rate.
    ///
    /// Recorded only while alive, so a rewind can never return to a dead moment, and never while paused,
    /// so a rewind's own playback records nothing. Cleared on every teleport and floor change, or a
    /// rewind would carry the player back across one.
    /// </summary>
    public class RewindRecorder : MonoBehaviour
    {
        public const float Interval = 0.1f;
        public const float Window = 3f;

        public struct Snapshot
        {
            public float Time;
            public Vector3 Position;
            public float Health;
            public int AmmoFirst;
            public int AmmoSecond;
            public int ActiveGun;
            public Vector3 UpAxis;
        }

        private readonly List<Snapshot> _history = new List<Snapshot>();
        private float _clock;
        private float _sinceLast = Interval;
        private PlayerMotor _motor;
        private bool _bound;

        public PlayerRig Rig { get; private set; }

        /// <summary>Stops recording without forgetting, for a rewind's playback.</summary>
        public bool Paused { get; set; }

        public int Count => _history.Count;

        /// <summary>The recorder's own clock, which snapshot times are measured on.</summary>
        public float Now => _clock;

        /// <summary>Oldest first.</summary>
        public Snapshot this[int index] => _history[index];

        public void Bind(PlayerRig rig)
        {
            Unbind();
            Rig = rig;
            _motor = rig != null ? rig.Motor : null;

            if (_motor != null) _motor.Teleported += Clear;
            LevelEvents.FloorLeaving += OnFloorLeaving;
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            if (_motor != null) _motor.Teleported -= Clear;
            LevelEvents.FloorLeaving -= OnFloorLeaving;
        }

        private void OnDestroy() => Unbind();

        private void OnFloorLeaving(int floor)
        {
            if (this != null) Clear();
        }

        private void Update() => Record(Time.deltaTime);

        /// <summary>One frame of recording. Update calls it on the player's clock; public so tooling can step time.</summary>
        public void Record(float dt)
        {
            _clock += dt;
            if (Paused || Rig == null || Rig.Health == null || !Rig.Health.IsAlive) return;

            _sinceLast += dt;
            if (_sinceLast + 0.0001f >= Interval)
            {
                _sinceLast = Mathf.Repeat(_sinceLast, Interval);
                _history.Add(Take());
            }

            while (_history.Count > 0 && _clock - _history[0].Time > Window + 0.0001f) _history.RemoveAt(0);
        }

        public bool TryOldest(out Snapshot snapshot)
        {
            snapshot = default;
            if (_history.Count == 0) return false;

            snapshot = _history[0];
            return true;
        }

        public void Clear()
        {
            if (this == null) return;

            _history.Clear();
            _sinceLast = Interval;
        }

        private Snapshot Take()
        {
            Holster holster = Rig.Holster;
            return new Snapshot
            {
                Time = _clock,
                Position = Rig.transform.position,
                Health = Rig.Health.Current,
                AmmoFirst = holster != null ? holster.AmmoIn(0) : 0,
                AmmoSecond = holster != null ? holster.AmmoIn(1) : 0,
                ActiveGun = holster != null ? holster.ActiveIndex : 0,
                UpAxis = _motor != null ? _motor.UpAxis : Vector3.up
            };
        }
    }
}
