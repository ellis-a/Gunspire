using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A second gun that is a copy of the one in hand: Divine Assistance's mirrored gun, the Phantasmal
    /// Mimic's. It fires with no ammo, no reloads and no recoil, and follows every swap, so it always
    /// copies whatever the holster has drawn.
    ///
    /// Whether it gets the owner's stats and bullet infusions differs by spell. The mimic gets neither;
    /// Divine Assistance's copy needs the infusions or Viper's Sting works on half your shots. A shared
    /// infusion is carried without being spent, so a one-round charge is not used up by the copy.
    /// </summary>
    public class PhantomWeapon : MonoBehaviour
    {
        public Weapon Weapon { get; private set; }
        public Holster Follow { get; private set; }

        public Weapon Source => Follow != null ? Follow.Weapon : null;

        /// <summary>Copies the gun in the other hand rather than the one drawn. Divine Assistance summons your other weapon.</summary>
        public bool FollowsOtherHand { get; private set; }

        public WeaponDefinition OtherHand =>
            Follow != null && Follow.NextIndex != Follow.ActiveIndex ? Follow.GetSlot(Follow.NextIndex) : null;

        public static PhantomWeapon Create(Holster follow, Transform aimOrigin, GameObject owner,
            bool useOwnerStats, bool shareInfusions, bool followOtherHand)
        {
            PhantomWeapon phantom = Create(follow, aimOrigin, owner, useOwnerStats, shareInfusions);
            phantom.FollowsOtherHand = followOtherHand;
            phantom.Refresh();
            return phantom;
        }

        public static PhantomWeapon Create(Holster follow, Transform aimOrigin, GameObject owner,
            bool useOwnerStats, bool shareInfusions)
        {
            var go = new GameObject("PhantomWeapon");
            if (aimOrigin != null)
            {
                // Mirrored to the other hand from the real viewmodel.
                go.transform.SetParent(aimOrigin, false);
                go.transform.localPosition = new Vector3(-0.26f, -0.20f, 0.30f);
            }

            Weapon source = follow != null ? follow.Weapon : null;

            var weapon = go.AddComponent<Weapon>();
            weapon.IsPhantom = true;
            weapon.InfiniteAmmo = true;
            weapon.OwnerTeam = source != null ? source.OwnerTeam : Team.Player;
            weapon.Owner = owner;
            weapon.AimOrigin = aimOrigin;

            if (useOwnerStats && source != null)
            {
                weapon.OwnerSheet = source.OwnerSheet;
                weapon.ExtraStatuses = source.ExtraStatuses;
            }

            if (shareInfusions) weapon.InfusionSource = source;

            var phantom = go.AddComponent<PhantomWeapon>();
            phantom.Weapon = weapon;
            phantom.Follow = follow;

            if (follow != null) follow.Changed += phantom.Refresh;
            phantom.Refresh();
            return phantom;
        }

        /// <summary>Equips whatever the followed holster has in hand, when that has changed.</summary>
        public void Refresh()
        {
            if (this == null || Weapon == null) return;

            WeaponDefinition current = FollowsOtherHand ? OtherHand : Source != null ? Source.Definition : null;
            if (current != null && Weapon.Definition != current) Weapon.Equip(current);
        }

        public void Dismiss()
        {
            if (Follow != null) Follow.Changed -= Refresh;
            Follow = null;

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            if (Follow != null) Follow.Changed -= Refresh;
        }
    }
}
