using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The right-click behaviour of one gun. A gun with <see cref="AltFireKind.None"/> has no
    /// alt fire and says so when you press the button, rather than doing nothing silently.
    ///
    /// Which fields matter depends on Kind. That is a little untidy in the Inspector, but the
    /// alternative is three parallel profile types and a polymorphic reference on every gun,
    /// which is a lot of machinery for something with three shapes.
    /// </summary>
    [System.Serializable]
    public class AltFireProfile
    {
        public AltFireKind Kind = AltFireKind.None;
        public string Name = "Alt Fire";
        public string Description = string.Empty;

        /// <summary>
        /// 0 is available from the moment you pick the gun up. Higher tiers stay locked until
        /// the run's alt fire tier reaches them - see the Gunsmith boon. The point is that a
        /// gun can be worth carrying before its alt fire is online.
        /// </summary>
        public int UnlockTier = 0;

        [Header("Cost")]
        public float Cooldown = 1.2f;
        public int AmmoCost = 1;
        public float ManaCost = 0f;

        [Header("Shot: one modified round")]
        public DeliveryKind Delivery = DeliveryKind.Projectile;
        public float Damage = 40f;
        public int Pellets = 1;
        public float SpreadDegrees = 0f;
        public float Knockback = 0f;
        public int MaxPierce = 0;
        public float SplashRadius = 0f;
        public float SplashDamage = 0f;
        public float ProjectileSpeed = 40f;
        public float ProjectileRadius = 0.2f;
        public float ProjectileGravity = 0f;
        public float ProjectileLifetime = 5f;
        public float RecoilPitch = 3.2f;

        /// <summary>Statuses this round applies. Empty means it inherits the gun's own.</summary>
        public List<StatusApplication> OnHitStatuses = new List<StatusApplication>();

        [Header("Salvo: empty the magazine")]
        public float SalvoRateMultiplier = 3f;
        public float SalvoDamageMultiplier = 0.85f;

        /// <summary>Cap so a 34-round magazine does not produce a six second salvo.</summary>
        public int SalvoMaxRounds = 12;

        [Header("Focus: held down")]
        public float FocusFov = 50f;
        public float FocusSpreadMultiplier = 0.15f;
        public float FocusDamageMultiplier = 1.25f;
        public float FocusMoveMultiplier = 0.55f;

        /// <summary>Rate penalty while focused, so zoom is a trade rather than a free upgrade.</summary>
        public float FocusRateMultiplier = 0.8f;

        public bool Exists => Kind != AltFireKind.None;

        /// <summary>Held rather than pressed, which changes how input is read and shown.</summary>
        public bool IsHeld => Kind == AltFireKind.Focus;

        public AltFireProfile Clone()
        {
            var copy = (AltFireProfile)MemberwiseClone();
            copy.OnHitStatuses = new List<StatusApplication>(OnHitStatuses);
            return copy;
        }

        /// <summary>One line for the HUD and the pickup card.</summary>
        public string Summary()
        {
            switch (Kind)
            {
                case AltFireKind.Shot:
                    string shot = Damage.ToString("0") + " dmg";
                    if (SplashDamage > 0f) shot += " +" + SplashDamage.ToString("0") + " splash";
                    if (MaxPierce > 0) shot += ", pierces " + MaxPierce;
                    return shot + "  costs " + AmmoCost + " ammo";

                case AltFireKind.Salvo:
                    return "empties up to " + SalvoMaxRounds + " rounds at "
                           + SalvoRateMultiplier.ToString("0.#") + "x rate";

                case AltFireKind.Focus:
                    return "hold to zoom: " + Mathf.RoundToInt((1f - FocusSpreadMultiplier) * 100f)
                           + "% tighter, " + Mathf.RoundToInt((FocusDamageMultiplier - 1f) * 100f)
                           + "% damage";

                default:
                    return "none";
            }
        }
    }
}
