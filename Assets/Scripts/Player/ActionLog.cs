using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// What the player has done recently, and the replay of it that Echo is made of: every round from
    /// their own gun with its direction, every cast with its level and aim, every melee attack.
    ///
    /// Echoes play back from where the player stands at the time, along the original direction, for no
    /// ammo, no cost and no cooldown, and are never recorded themselves. Toggles, stances and spells marked
    /// never to echo, such as Rewind, are not recorded at all.
    /// </summary>
    public class ActionLog : MonoBehaviour
    {
        public enum Kind { Shot, Spell, Melee }

        public struct Entry
        {
            public Kind Kind;
            public float Time;
            public ShotSpec Shot;
            public Vector3 Direction;
            public Spell Spell;
            public int Level;
            public float Charge;
            public int SoulsSpent;
        }

        private struct Pending
        {
            public float At;
            public Entry Entry;
        }

        /// <summary>How far back the log reaches.</summary>
        public const float KeepSeconds = 10f;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<Entry> _due = new List<Entry>();

        private float _clock;
        private float _echoUntil = float.NegativeInfinity;
        private float _echoDelay;

        private Weapon _weapon;
        private SpellBook _book;
        private PlayerCombat _combat;
        private bool _bound;

        public PlayerRig Rig { get; private set; }
        public IReadOnlyList<Entry> Entries => _entries;
        public float Now => _clock;
        public bool IsEchoing => _clock < _echoUntil;
        public int PendingCount => _pending.Count;

        public static bool Echoable(Spell spell)
            => spell != null && !spell.NeverEchoes && !spell.IsSustained && !spell.IsStance;

        public void Bind(PlayerRig rig)
        {
            Unbind();
            Rig = rig;
            if (rig == null) return;

            _weapon = rig.Weapon;
            _book = rig.Book;
            _combat = rig.CombatInput;

            if (_weapon != null) _weapon.Fired += OnFired;
            if (_book != null) _book.SpellCast += OnCast;
            if (_combat != null) _combat.MeleeFinished += OnMelee;
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            if (_weapon != null) _weapon.Fired -= OnFired;
            if (_book != null) _book.SpellCast -= OnCast;
            if (_combat != null) _combat.MeleeFinished -= OnMelee;
        }

        private void OnDestroy() => Unbind();

        // ---------------------------------------------------------------- recording

        private void OnFired(WeaponShot shot)
        {
            if (this == null || shot.IsEcho || shot.IsPhantom || shot.Weapon != _weapon) return;
            Add(new Entry { Kind = Kind.Shot, Shot = shot.Spec, Direction = shot.Direction });
        }

        private void OnCast(Spell spell, int slot)
        {
            if (this == null || !Echoable(spell)) return;

            SpellBook.CastRecord record = _book.LastCast;
            if (record.Spell != spell) return;

            Add(new Entry
            {
                Kind = Kind.Spell, Spell = spell, Level = record.Level, Direction = record.Forward,
                Charge = record.Charge, SoulsSpent = record.SoulsSpent
            });
        }

        private void OnMelee(Spell spell, bool cast)
        {
            if (this == null || !cast || spell == null) return;
            Add(new Entry { Kind = Kind.Melee, Spell = spell, Level = _combat.MeleeLevel, Direction = _combat.LastMeleeForward });
        }

        private void Add(Entry entry)
        {
            entry.Time = _clock;
            _entries.Add(entry);

            if (IsEchoing) _pending.Add(new Pending { At = _clock + _echoDelay, Entry = entry });
            Trim();
        }

        private void Trim()
        {
            while (_entries.Count > 0 && _clock - _entries[0].Time > KeepSeconds) _entries.RemoveAt(0);
        }

        // ---------------------------------------------------------------- echoing

        /// <summary>For the next few seconds, everything recorded is repeated after a delay.</summary>
        public void BeginEchoing(float seconds, float delay)
        {
            _echoUntil = _clock + Mathf.Max(0f, seconds);
            _echoDelay = Mathf.Max(0f, delay);
        }

        /// <summary>Stops recording new echoes. Those already waiting still play unless dropped.</summary>
        public void StopEchoing(bool dropPending = false)
        {
            _echoUntil = float.NegativeInfinity;
            if (dropPending) _pending.Clear();
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>One frame on the player's clock: plays whatever echoes are due. Public so tooling can step time.</summary>
        public void Tick(float dt)
        {
            _clock += dt;
            Trim();
            if (_pending.Count == 0) return;

            _due.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].At > _clock) continue;
                _due.Insert(0, _pending[i].Entry);
                _pending.RemoveAt(i);
            }

            for (int i = 0; i < _due.Count; i++) Replay(_due[i]);
        }

        /// <summary>Repeats one recorded action now. Returns whether anything happened.</summary>
        public bool Replay(Entry entry)
        {
            if (Rig == null) return false;

            switch (entry.Kind)
            {
                case Kind.Shot:
                    if (_weapon == null || _weapon.Definition == null) return false;
                    _weapon.FireEcho(entry.Shot, entry.Direction);
                    return true;

                case Kind.Spell:
                    return _book != null && _book.CastEcho(entry.Spell, entry.Level, entry.Direction, entry.Charge, entry.SoulsSpent);

                default:
                    return _combat != null && _combat.CastMeleeEcho(entry.Spell, entry.Level, entry.Direction);
            }
        }

        /// <summary>Everything recorded in the last so many seconds, oldest first.</summary>
        public void Collect(float secondsAgo, List<Entry> into)
        {
            into.Clear();
            for (int i = 0; i < _entries.Count; i++)
                if (_clock - _entries[i].Time <= secondsAgo) into.Add(_entries[i]);
        }

        public void Clear()
        {
            _entries.Clear();
            _pending.Clear();
            _echoUntil = float.NegativeInfinity;
        }
    }
}
