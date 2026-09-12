using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The two guns the player is carrying, and which of them is in their hands.
    ///
    /// There is still only one <see cref="Weapon"/> component. It always reflects the active
    /// slot, and the holstered slot is just a definition plus the rounds it was put away with -
    /// so everything that already talks to the live weapon keeps working, and none of the
    /// runtime state a gun carries (cooldowns, reloads, wind-up, focus) has to exist twice.
    ///
    /// The consequence of that choice is that the magazine count for the active slot lives on
    /// the weapon, not in this array, and has to be written back before a swap. Nothing else
    /// may read <see cref="_ammo"/> for the active slot directly; ask <see cref="AmmoIn"/>.
    /// </summary>
    public class Holster : MonoBehaviour
    {
        public const int SlotCount = 2;

        /// <summary>Keys that swap, alongside the mouse wheel.</summary>
        public static readonly KeyCode SwapKey = KeyCode.X;

        public Weapon Weapon;

        private readonly WeaponDefinition[] _slots = new WeaponDefinition[SlotCount];
        private readonly int[] _ammo = new int[SlotCount];

        public int ActiveIndex { get; private set; }

        /// <summary>Raised when the pair changes, so the HUD can stay out of Update.</summary>
        public event Action Changed;

        public WeaponDefinition GetSlot(int index)
            => index >= 0 && index < SlotCount ? _slots[index] : null;

        /// <summary>
        /// Rounds in a slot. The active one is read off the live weapon, because that is the
        /// only copy that is being spent.
        /// </summary>
        public int AmmoIn(int index)
        {
            if (index < 0 || index >= SlotCount) return 0;
            if (index == ActiveIndex && Weapon != null && Weapon.Definition != null)
                return Weapon.AmmoInMagazine;
            return _ammo[index];
        }

        public int FilledSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < SlotCount; i++) if (_slots[i] != null) n++;
                return n;
            }
        }

        public bool CanSwap => FilledSlots > 1;

        private int OtherIndex => (ActiveIndex + 1) % SlotCount;

        // ---------------------------------------------------------------- equipping

        /// <summary>
        /// Puts a gun straight into a slot, holstered or not. Used by the starting loadout, and
        /// by anything that wants to set the pair up without trading.
        /// </summary>
        public void SetSlot(int index, WeaponDefinition definition, int ammo = -1)
        {
            if (index < 0 || index >= SlotCount) return;

            _slots[index] = definition;
            _ammo[index] = ammo < 0 && definition != null ? definition.MagazineSize : Mathf.Max(0, ammo);

            if (index == ActiveIndex) Draw(index);
            Changed?.Invoke();
        }

        /// <summary>
        /// Trades a gun into the pair. An empty slot is filled and drawn - so a second gun is
        /// picked up rather than swapped for - and only once both hands are full does this hand
        /// something back. Returns null when nothing was given up.
        /// </summary>
        public WeaponDefinition Take(WeaponDefinition incoming, int incomingAmmo, out int outgoingAmmo)
        {
            outgoingAmmo = -1;
            if (incoming == null) return null;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null) continue;

                _slots[i] = incoming;
                _ammo[i] = incomingAmmo < 0 ? incoming.MagazineSize : Mathf.Max(0, incomingAmmo);

                // Drawn on pickup, so a new gun can be tried out on the spot rather than
                // discovered later in a fight.
                SetActive(i);
                Changed?.Invoke();
                return null;
            }

            WeaponDefinition outgoing = _slots[ActiveIndex];
            if (outgoing != null) outgoingAmmo = AmmoIn(ActiveIndex);

            _slots[ActiveIndex] = incoming;
            _ammo[ActiveIndex] = incomingAmmo < 0 ? incoming.MagazineSize : Mathf.Max(0, incomingAmmo);
            Draw(ActiveIndex);

            Changed?.Invoke();
            return outgoing;
        }

        // ---------------------------------------------------------------- swapping

        public void Swap()
        {
            if (!CanSwap) return;
            SetActive(OtherIndex);
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            if (_slots[index] == null) return;
            if (index == ActiveIndex && Weapon != null && Weapon.Definition == _slots[index]) return;

            // Bank what the outgoing gun has left before the live weapon is overwritten, or a
            // half-spent magazine comes back full.
            if (Weapon != null && Weapon.Definition != null && _slots[ActiveIndex] != null)
                _ammo[ActiveIndex] = Weapon.AmmoInMagazine;

            ActiveIndex = index;
            Draw(index);
            Changed?.Invoke();
        }

        /// <summary>
        /// Hands the live weapon a slot. Equipping cancels an in-flight reload by design: a gun
        /// put away mid-reload comes back out still needing one.
        /// </summary>
        private void Draw(int index)
        {
            if (Weapon == null) return;

            WeaponDefinition definition = _slots[index];
            if (definition == null) return;

            Weapon.Equip(definition, _ammo[index]);
        }

        // ---------------------------------------------------------------- run upkeep

        /// <summary>Tops up both guns, including the one that is put away.</summary>
        public void RefillAll()
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] != null) _ammo[i] = _slots[i].MagazineSize;

            if (Weapon != null) Weapon.RefillMagazine();
            Changed?.Invoke();
        }

        /// <summary>Empties the pair, for a restart.</summary>
        public void Clear()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = null;
                _ammo[i] = 0;
            }

            ActiveIndex = 0;
            Changed?.Invoke();
        }
    }
}
