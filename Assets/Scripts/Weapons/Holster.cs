using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The guns the player is carrying, two unless Third Hand opens a third, and which of them is in their hands.
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
        /// <summary>Hands a player can ever carry guns in. Third Hand opens the last.</summary>
        public const int MaxSlots = 3;

        /// <summary>Hands a player starts with.</summary>
        public const int BaseSlots = 2;

        /// <summary>Hands open right now.</summary>
        public int SlotCount { get; private set; } = BaseSlots;

        /// <summary>Keys that swap, alongside the mouse wheel.</summary>
        public static readonly KeyCode SwapKey = KeyCode.C;

        public Weapon Weapon;

        private readonly WeaponDefinition[] _slots = new WeaponDefinition[MaxSlots];
        private readonly int[] _ammo = new int[MaxSlots];

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

        /// <summary>A full magazine of a gun in this holster's owner's hands.</summary>
        public int FullMagazine(WeaponDefinition definition)
            => definition == null ? 0 : Weapon != null ? Weapon.MagazineFor(definition) : definition.MagazineSize;

        /// <summary>A full magazine of the gun in a slot.</summary>
        public int MagazineIn(int index) => FullMagazine(GetSlot(index));

        public int FilledSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < SlotCount; i++) if (_slots[i] != null) n++;
                return n;
            }
        }

        public bool CanSwap => FilledSlots > 1 && _swapLocks.Count == 0;

        private readonly System.Collections.Generic.HashSet<object> _swapLocks =
            new System.Collections.Generic.HashSet<object>();

        /// <summary>Something is holding the gun in hand: Divine Assistance's mirror, or Superid.</summary>
        public bool SwapLocked => _swapLocks.Count > 0;

        /// <summary>Keyed, so two spells locking the swap do not unlock each other.</summary>
        public void LockSwap(object key)
        {
            if (key != null && _swapLocks.Add(key)) Changed?.Invoke();
        }

        public void UnlockSwap(object key)
        {
            if (key != null && _swapLocks.Remove(key)) Changed?.Invoke();
        }

        /// <summary>The next filled slot after the one in hand, wrapping round: what a swap draws. The active slot when it is the only one.</summary>
        public int NextIndex
        {
            get
            {
                for (int step = 1; step <= SlotCount; step++)
                {
                    int index = (ActiveIndex + step) % SlotCount;
                    if (_slots[index] != null) return index;
                }
                return ActiveIndex;
            }
        }

        /// <summary>
        /// Opens or closes hands. Guns in a closed hand are dropped, and if the gun in hand was one of them, the first
        /// hand is drawn. Third Hand.
        /// </summary>
        public void SetSlotCount(int count)
        {
            count = Mathf.Clamp(count, BaseSlots, MaxSlots);
            if (count == SlotCount) return;

            for (int i = count; i < MaxSlots; i++)
            {
                _slots[i] = null;
                _ammo[i] = 0;
            }

            SlotCount = count;
            if (ActiveIndex >= count) SetActive(0);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- equipping

        /// <summary>
        /// Puts a gun straight into a slot, holstered or not. Used by the starting loadout, and
        /// by anything that wants to set the pair up without trading.
        /// </summary>
        public void SetSlot(int index, WeaponDefinition definition, int ammo = -1)
        {
            if (index < 0 || index >= SlotCount) return;

            _slots[index] = definition;
            _ammo[index] = ammo < 0 && definition != null ? FullMagazine(definition) : Mathf.Max(0, ammo);

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
                _ammo[i] = incomingAmmo < 0 ? FullMagazine(incoming) : Mathf.Max(0, incomingAmmo);

                // Drawn on pickup, so a new gun can be tried out on the spot rather than
                // discovered later in a fight.
                SetActive(i);
                if (Weapon != null) Weapon.BeginDraw();
                Changed?.Invoke();
                return null;
            }

            WeaponDefinition outgoing = _slots[ActiveIndex];
            if (outgoing != null) outgoingAmmo = AmmoIn(ActiveIndex);

            _slots[ActiveIndex] = incoming;
            _ammo[ActiveIndex] = incomingAmmo < 0 ? FullMagazine(incoming) : Mathf.Max(0, incomingAmmo);
            Draw(ActiveIndex);
            if (Weapon != null) Weapon.BeginDraw();

            Changed?.Invoke();
            return outgoing;
        }

        // ---------------------------------------------------------------- swapping

        public void Swap()
        {
            if (!CanSwap) return;
            SetActive(NextIndex);
            if (Weapon != null) Weapon.BeginDraw();
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

        /// <summary>Puts a slot's rounds at a count, the drawn gun's included. Rewind restores ammo through this.</summary>
        public void SetAmmo(int index, int rounds)
        {
            if (index < 0 || index >= SlotCount || _slots[index] == null) return;

            if (index == ActiveIndex && Weapon != null && Weapon.Definition != null) Weapon.SetAmmo(rounds);
            else _ammo[index] = Mathf.Clamp(rounds, 0, FullMagazine(_slots[index]));

            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- run upkeep

        /// <summary>Tops up both guns, including the one that is put away.</summary>
        public void RefillAll()
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] != null) _ammo[i] = FullMagazine(_slots[i]);

            if (Weapon != null) Weapon.RefillMagazine();
            Changed?.Invoke();
        }

        /// <summary>Empties the pair, for a restart.</summary>
        public void Clear()
        {
            SlotCount = BaseSlots;
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = null;
                _ammo[i] = 0;
            }

            ActiveIndex = 0;
            _swapLocks.Clear();
            Changed?.Invoke();
        }
    }
}
