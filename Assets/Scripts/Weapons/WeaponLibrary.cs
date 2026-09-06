using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>The gun roster. <see cref="Starter"/> is what the player spawns holding.</summary>
    public static class WeaponLibrary
    {
        private static List<WeaponDefinition> _all;

        public static IReadOnlyList<WeaponDefinition> All
        {
            get
            {
                if (_all == null) BuildRoster();
                return _all;
            }
        }

        public static WeaponDefinition Get(string id)
        {
            if (_all == null) BuildRoster();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Id == id) return _all[i].Clone();
            return Starter();
        }

        /// <summary>Arcanum .38 - the hitscan sidearm every run begins with.</summary>
        public static WeaponDefinition Starter() => Get("arcanum");

        public static WeaponDefinition RandomDrop(Rng rng)
        {
            if (_all == null) BuildRoster();
            var pool = new List<WeaponDefinition>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Id != "arcanum") pool.Add(_all[i]);
            return rng.Pick(pool).Clone();
        }

        private static void BuildRoster()
        {
            _all = new List<WeaponDefinition>
            {
                new WeaponDefinition
                {
                    Id = "arcanum",
                    DisplayName = "Arcanum .38",
                    Flavor = "Enchanted sidearm. Reliable, unglamorous, always loaded.",
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Physical,
                    Damage = 14f,
                    RoundsPerMinute = 320f,
                    MagazineSize = 12,
                    ReloadTime = 1.1f,
                    SpreadDegrees = 0.4f,
                    MovingSpreadDegrees = 1.1f,
                    RecoilPitch = 1.3f,
                    RecoilYaw = 0.3f,
                    Range = 140f,
                    Tint = new Color(1f, 0.92f, 0.7f)
                },

                new WeaponDefinition
                {
                    Id = "ember_repeater",
                    DisplayName = "Ember Repeater",
                    Flavor = "Spits burning slag. Hold the trigger and let it cook.",
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Auto,
                    DamageType = DamageType.Fire,
                    Damage = 7f,
                    RoundsPerMinute = 620f,
                    MagazineSize = 34,
                    ReloadTime = 1.6f,
                    SpreadDegrees = 1.6f,
                    MovingSpreadDegrees = 2.6f,
                    RecoilPitch = 0.5f,
                    RecoilYaw = 0.35f,
                    ProjectileSpeed = 70f,
                    ProjectileRadius = 0.12f,
                    Tint = Palette.Fire,
                    OnHitStatuses = { StatusLibrary.Burn(3f, 1, 4f) }
                },

                new WeaponDefinition
                {
                    Id = "frost_lance",
                    DisplayName = "Frost Lance",
                    Flavor = "A shard of the tower moat, fired at unkind speed.",
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Ice,
                    Damage = 34f,
                    RoundsPerMinute = 110f,
                    MagazineSize = 5,
                    ReloadTime = 1.5f,
                    SpreadDegrees = 0f,
                    MovingSpreadDegrees = 0.4f,
                    RecoilPitch = 2.6f,
                    RecoilYaw = 0.2f,
                    ProjectileSpeed = 90f,
                    ProjectileRadius = 0.18f,
                    Knockback = 3f,
                    Tint = Palette.Ice,
                    OnHitStatuses = { StatusLibrary.Chill(4f, 2) }
                },

                new WeaponDefinition
                {
                    Id = "voltaic_rail",
                    DisplayName = "Voltaic Rail",
                    Flavor = "Punches through a line of cultists and keeps going.",
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Lightning,
                    Damage = 55f,
                    RoundsPerMinute = 70f,
                    MagazineSize = 4,
                    ReloadTime = 1.9f,
                    SpreadDegrees = 0f,
                    MovingSpreadDegrees = 0.2f,
                    RecoilPitch = 4.5f,
                    RecoilYaw = 0.4f,
                    Range = 200f,
                    MaxPierce = 4,
                    Tint = Palette.Lightning,
                    OnHitStatuses = { StatusLibrary.Shock(4f) }
                },

                new WeaponDefinition
                {
                    Id = "hexshot",
                    DisplayName = "Hexshot",
                    Flavor = "Eight barrels of powdered grave dirt.",
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Poison,
                    Damage = 8f,
                    PelletsPerShot = 9,
                    RoundsPerMinute = 95f,
                    MagazineSize = 6,
                    ReloadTime = 1.8f,
                    SpreadDegrees = 6.5f,
                    MovingSpreadDegrees = 8f,
                    RecoilPitch = 4f,
                    RecoilYaw = 0.6f,
                    Range = 42f,
                    Knockback = 2f,
                    Tint = Palette.Poison,
                    OnHitStatuses = { StatusLibrary.Blight(6f, 1, 2.5f) }
                },

                new WeaponDefinition
                {
                    Id = "sunder_cannon",
                    DisplayName = "Sunder Cannon",
                    Flavor = "Lobs a rune that disagrees with architecture.",
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Arcane,
                    Damage = 26f,
                    RoundsPerMinute = 80f,
                    MagazineSize = 4,
                    ReloadTime = 2.0f,
                    SpreadDegrees = 0.5f,
                    MovingSpreadDegrees = 1.2f,
                    RecoilPitch = 5f,
                    RecoilYaw = 0.5f,
                    ProjectileSpeed = 34f,
                    ProjectileRadius = 0.25f,
                    ProjectileGravity = 12f,
                    SplashRadius = 4.2f,
                    SplashDamage = 34f,
                    Knockback = 6f,
                    Tint = Palette.Arcane
                }
            };
        }
    }
}
