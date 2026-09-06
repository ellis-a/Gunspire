using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Data for one gun. Plain class rather than a ScriptableObject so the whole roster can
    /// live in code; swap to a SO later if you want to tune these in the inspector.
    /// </summary>
    [System.Serializable]
    public class WeaponDefinition
    {
        public string Id = "weapon";
        public string DisplayName = "Weapon";
        public string Flavor = string.Empty;

        [Header("Delivery")]
        public DeliveryKind Delivery = DeliveryKind.Hitscan;
        public FireMode Mode = FireMode.Semi;
        public DamageType DamageType = DamageType.Physical;

        [Header("Damage")]
        public float Damage = 12f;
        public int PelletsPerShot = 1;
        public float Knockback = 0f;

        [Header("Rate and magazine")]
        public float RoundsPerMinute = 400f;
        public int MagazineSize = 12;
        public float ReloadTime = 1.2f;
        public int BurstCount = 1;
        public float BurstInterval = 0.06f;

        [Header("Accuracy")]
        public float SpreadDegrees = 0.8f;
        public float MovingSpreadDegrees = 1.6f;
        public float RecoilPitch = 1.2f;
        public float RecoilYaw = 0.25f;

        [Header("Hitscan")]
        public float Range = 120f;
        public int MaxPierce = 0;

        [Header("Projectile")]
        public float ProjectileSpeed = 45f;
        public float ProjectileRadius = 0.14f;
        public float ProjectileGravity = 0f;
        public float ProjectileLifetime = 4f;
        public bool ProjectileHoming = false;

        [Header("Splash")]
        public float SplashRadius = 0f;
        public float SplashDamage = 0f;

        [Header("Cost and flavour")]
        public float ManaPerShot = 0f;
        public Color Tint = Color.white;

        /// <summary>Status effects every hit applies. Boons append to the runtime copy, not this list.</summary>
        public List<StatusApplication> OnHitStatuses = new List<StatusApplication>();

        public float SecondsBetweenShots => 60f / Mathf.Max(1f, RoundsPerMinute);

        public WeaponDefinition Clone()
        {
            var copy = (WeaponDefinition)MemberwiseClone();
            copy.OnHitStatuses = new List<StatusApplication>(OnHitStatuses);
            return copy;
        }

        public string StatLine()
        {
            string dps = (Damage * PelletsPerShot * (RoundsPerMinute / 60f)).ToString("0");
            string kind = Delivery == DeliveryKind.Hitscan ? "hitscan" : "projectile";
            return string.Format("{0} dmg x{1}  {2} rpm  mag {3}  {4}  ~{5} dps",
                Damage.ToString("0.#"), PelletsPerShot, RoundsPerMinute.ToString("0"),
                MagazineSize, kind, dps);
        }
    }
}
