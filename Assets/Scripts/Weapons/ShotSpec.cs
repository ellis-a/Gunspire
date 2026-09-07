using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// One round leaving the barrel, independent of which trigger asked for it.
    ///
    /// This exists because alt fire needs the whole firing pipeline - spread, pellets, pierce,
    /// splash, statuses, impacts, tracers - with different numbers. Reading those off
    /// <see cref="WeaponDefinition"/> directly, as <see cref="Weapon"/> used to, meant alt fire
    /// would have had to duplicate the pipeline or thread overrides through every method.
    /// Describing a round as data instead means both triggers run the same code.
    /// </summary>
    public struct ShotSpec
    {
        public DeliveryKind Delivery;
        public DamageType DamageType;
        public float Damage;
        public int Pellets;
        public float SpreadDegrees;
        public float Knockback;
        public float Range;
        public int MaxPierce;

        public float ProjectileSpeed;
        public float ProjectileRadius;
        public float ProjectileGravity;
        public float ProjectileLifetime;
        public bool ProjectileHoming;

        public float SplashRadius;
        public float SplashDamage;

        public Color Tint;
        public float RecoilPitch;
        public float RecoilYaw;

        public List<StatusApplication> Statuses;

        /// <summary>The gun's ordinary round. <paramref name="spread"/> comes from movement and focus.</summary>
        public static ShotSpec Primary(WeaponDefinition def, float spread, float damageMultiplier = 1f)
        {
            return new ShotSpec
            {
                Delivery = def.Delivery,
                DamageType = def.DamageType,
                Damage = def.Damage * damageMultiplier,
                Pellets = Mathf.Max(1, def.PelletsPerShot),
                SpreadDegrees = spread,
                Knockback = def.Knockback,
                Range = def.Range,
                MaxPierce = def.MaxPierce,
                ProjectileSpeed = def.ProjectileSpeed,
                ProjectileRadius = def.ProjectileRadius,
                ProjectileGravity = def.ProjectileGravity,
                ProjectileLifetime = def.ProjectileLifetime,
                ProjectileHoming = def.ProjectileHoming,
                SplashRadius = def.SplashRadius,
                SplashDamage = def.SplashDamage,
                Tint = def.Tint,
                RecoilPitch = def.RecoilPitch,
                RecoilYaw = def.RecoilYaw,
                Statuses = def.OnHitStatuses
            };
        }

        /// <summary>
        /// The alt fire's round. Range, homing and tint stay with the gun - an alt fire is a
        /// different round out of the same weapon, not a different weapon.
        /// </summary>
        public static ShotSpec Alt(WeaponDefinition def, AltFireProfile alt)
        {
            return new ShotSpec
            {
                Delivery = alt.Delivery,
                DamageType = def.DamageType,
                Damage = alt.Damage,
                Pellets = Mathf.Max(1, alt.Pellets),
                SpreadDegrees = alt.SpreadDegrees,
                Knockback = alt.Knockback,
                Range = def.Range,
                MaxPierce = alt.MaxPierce,
                ProjectileSpeed = alt.ProjectileSpeed,
                ProjectileRadius = alt.ProjectileRadius,
                ProjectileGravity = alt.ProjectileGravity,
                ProjectileLifetime = alt.ProjectileLifetime,
                ProjectileHoming = def.ProjectileHoming,
                SplashRadius = alt.SplashRadius,
                SplashDamage = alt.SplashDamage,
                Tint = def.Tint,
                RecoilPitch = alt.RecoilPitch,
                RecoilYaw = def.RecoilYaw,

                // An empty list on the profile means "whatever this gun normally applies",
                // so a frost gun's alt round still chills without restating it.
                Statuses = alt.OnHitStatuses != null && alt.OnHitStatuses.Count > 0
                    ? alt.OnHitStatuses
                    : def.OnHitStatuses
            };
        }
    }
}
