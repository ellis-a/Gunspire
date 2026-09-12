using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
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

        /// <summary>Drives how often this gun turns up on a plinth. See <see cref="Rarities"/>.</summary>
        public Rarity Rarity = Rarity.Common;

        [Header("Delivery")]
        public DeliveryKind Delivery = DeliveryKind.Hitscan;
        public FireMode Mode = FireMode.Semi;
        public DamageType DamageType = DamageType.Kinetic;

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

        [Header("Wind-up")]
        /// <summary>
        /// Seconds of held trigger before the first shot. Zero fires immediately, which is
        /// every gun but the minigun. The barrels keep spinning briefly after you let go, so
        /// short bursts do not pay the full cost twice.
        /// </summary>
        public float SpinUpSeconds = 0f;

        /// <summary>How much faster the spin unwinds than it wound up.</summary>
        public float SpinDownMultiplier = 1.6f;

        [Header("Accuracy")]
        public float SpreadDegrees = 0.8f;

        /// <summary>
        /// How loud a shot is to something listening, as a multiple of the listener's hearing
        /// range. One is "audible as far as anything can hear"; a silenced weapon would sit well
        /// under it, a cannon over. Nothing here knows who is listening or how well.
        /// </summary>
        public float NoiseMultiplier = 1f;
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

        /// <summary>
        /// Shown on the HUD, the loadout card and the pickup plinth. Leave empty and the UI
        /// draws a tinted placeholder, so nothing breaks while the art is being made.
        ///
        /// Assign it on the weapon asset - a definition built in code has no way to reference
        /// one, so the built-in roster always leaves this null.
        /// </summary>
        public Texture2D Icon;

        [Header("Cost and flavour")]
        public float ManaPerShot = 0f;
        public Color Tint = Color.white;

        /// <summary>Status effects every hit applies. Boons append to the runtime copy, not this list.</summary>
        public List<StatusApplication> OnHitStatuses = new List<StatusApplication>();

        [Header("Alt fire")]
        /// <summary>What right click does. Kind None means this gun has no alt fire.</summary>
        public AltFireProfile AltFire = new AltFireProfile();

        public float SecondsBetweenShots => 60f / Mathf.Max(1f, RoundsPerMinute);

        public WeaponDefinition Clone()
        {
            var copy = (WeaponDefinition)MemberwiseClone();
            copy.OnHitStatuses = new List<StatusApplication>(OnHitStatuses);

            // MemberwiseClone copies the reference, so without this every clone of a gun would
            // share one profile and a boon that tuned an alt fire would tune it on the roster.
            copy.AltFire = AltFire != null ? AltFire.Clone() : new AltFireProfile();
            return copy;
        }

        public bool HasAltFire => AltFire != null && AltFire.Exists;

        /// <summary>
        /// Rounds actually loosed per pull of the trigger: pellets in a shotgun shell, shots
        /// in a burst, one otherwise.
        /// </summary>
        public int RoundsPerTrigger => Mathf.Max(1, PelletsPerShot) *
                                       (Mode == FireMode.Burst ? Mathf.Max(1, BurstCount) : 1);

        public string StatLine()
        {
            // RoundsPerMinute paces the trigger, not the individual round, so a burst weapon
            // delivers BurstCount rounds per cycle.
            string dps = (Damage * RoundsPerTrigger * (RoundsPerMinute / 60f)).ToString("0");
            string kind = Delivery == DeliveryKind.Hitscan ? "hitscan" : "projectile";
            string shape = Mode == FireMode.Burst ? BurstCount + "-round burst" : kind;

            return string.Format("{0} {1} dmg x{2}  {3} rpm  mag {4}  {5}  ~{6} dps",
                DamageTypes.Name(DamageType), Damage.ToString("0.#"), RoundsPerTrigger,
                RoundsPerMinute.ToString("0"), MagazineSize, shape, dps);
        }

        /// <summary>
        /// The alt fire, for pickup prompts. Whether a gun has a second trigger, and whether
        /// it is available yet, is worth knowing before deciding to swap.
        /// </summary>
        public string AltLine(int unlockedTier)
        {
            if (!HasAltFire) return "no alt fire";

            string locked = AltFire.UnlockTier > unlockedTier ? " (locked)" : string.Empty;
            return "RMB " + AltFire.Name + locked + ": " + AltFire.Summary();
        }
    }
}
