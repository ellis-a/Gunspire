using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Keeps the player out of enemies' notice: out of sight, optionally out of hearing, and optionally
    /// out of targeting altogether. Requests are keyed, so Invisibility, Flicker, Rewind's playback and the
    /// body left behind by Assume Identity can overlap without one switching off another's.
    ///
    /// It writes onto the player's own entry in <see cref="TargetRegistry"/>, which enemy perception
    /// already reads. A request can break itself when the player shoots, casts or swings.
    /// </summary>
    public class PlayerConcealment : MonoBehaviour
    {
        public sealed class Request
        {
            public bool FromSight;
            public bool FromHearing;
            public bool Untargetable;
            public bool BreakOnShoot;
            public bool BreakOnCast;
            public bool BreakOnMelee;

            /// <summary>Casting this spell does not break the request, for a spell that hides you as it is cast.</summary>
            public Spell IgnoreSpell;
        }

        private readonly Dictionary<object, Request> _requests = new Dictionary<object, Request>();
        private readonly List<object> _scratch = new List<object>();

        public PlayerRig Rig { get; private set; }

        public bool HiddenFromSight { get; private set; }
        public bool HiddenFromHearing { get; private set; }
        public bool Untargetable { get; private set; }

        /// <summary>Raised with the key of a request that broke itself.</summary>
        public event Action<object> Broken;

        private Weapon _weapon;
        private SpellBook _book;
        private PlayerCombat _combat;
        private bool _bound;

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

        public Request Hide(object key, bool fromSight = true, bool fromHearing = false, bool untargetable = false)
        {
            if (key == null) return null;

            var request = new Request { FromSight = fromSight, FromHearing = fromHearing, Untargetable = untargetable };
            _requests[key] = request;
            Apply();
            return request;
        }

        public void Release(object key)
        {
            if (key != null && _requests.Remove(key)) Apply();
        }

        public bool Holds(object key) => key != null && _requests.ContainsKey(key);

        public void Clear()
        {
            _requests.Clear();
            Apply();
        }

        /// <summary>
        /// Writes the combined state onto the player's registry entry. Every frame as well as on change,
        /// since the registry forgets its flags whenever a room is cleared.
        /// </summary>
        public void Apply()
        {
            bool sight = false, hearing = false, untargetable = false;
            foreach (Request request in _requests.Values)
            {
                sight |= request.FromSight;
                hearing |= request.FromHearing;
                untargetable |= request.Untargetable;
            }

            HiddenFromSight = sight;
            HiddenFromHearing = hearing;
            Untargetable = untargetable;

            TargetRegistry.Entry body = TargetRegistry.RigEntry;
            body.HiddenFromSight = sight;
            body.HiddenFromHearing = hearing;
            body.Untargetable = untargetable;
        }

        private void LateUpdate() => Apply();

        // ---------------------------------------------------------------- breaking

        private void OnFired(WeaponShot shot)
        {
            if (!shot.IsEcho && !shot.IsPhantom) BreakWhere(r => r.BreakOnShoot);
        }

        private void OnCast(Spell spell, int slot) => BreakWhere(r => r.BreakOnCast && r.IgnoreSpell != spell);

        private void OnMelee(Spell spell, bool cast)
        {
            if (cast) BreakWhere(r => r.BreakOnMelee);
        }

        private void BreakWhere(Predicate<Request> breaks)
        {
            if (this == null) return;

            _scratch.Clear();
            foreach (KeyValuePair<object, Request> pair in _requests)
                if (breaks(pair.Value)) _scratch.Add(pair.Key);

            if (_scratch.Count == 0) return;

            for (int i = 0; i < _scratch.Count; i++) _requests.Remove(_scratch[i]);
            Apply();

            for (int i = 0; i < _scratch.Count; i++) Broken?.Invoke(_scratch[i]);
        }
    }
}
