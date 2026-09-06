using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The gun roster. The starting weapon is named by <see cref="StartingLoadout.WeaponId"/>
    /// and is excluded from world drops.
    /// </summary>
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

            // Fall back to the first gun on the roster. Never route this through the starting
            // weapon: if that id is the one missing, the two would call each other forever.
            Debug.LogWarning("WeaponLibrary has no weapon with id \"" + id + "\". Falling back to "
                             + _all[0].DisplayName + ".");
            return _all[0].Clone();
        }

        /// <summary>
        /// A gun for a plinth or a vault. Rolls a rarity from the player's Luck, then picks a
        /// gun at that tier. The weapon the player already starts holding is excluded, so
        /// changing the starting gun automatically keeps it out of world drops.
        /// </summary>
        public static WeaponDefinition RollDrop(Rng rng, float luck, float rarityBonus = 1f)
        {
            if (_all == null) BuildRoster();

            var pool = new List<WeaponDefinition>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Id != StartingLoadout.WeaponId) pool.Add(_all[i]);
            if (pool.Count == 0) pool.AddRange(_all);

            Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
            WeaponDefinition pick = Rarities.PickOfRarity(rng, pool, w => w.Rarity, rolled);
            return pick.Clone();
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
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Normal,
                    Damage = 14f,
                    RoundsPerMinute = 320f,
                    MagazineSize = 12,
                    ReloadTime = 1.1f,
                    SpreadDegrees = 0.4f,
                    MovingSpreadDegrees = 1.1f,
                    RecoilPitch = 1.3f,
                    RecoilYaw = 0.3f,
                    Range = 140f,
                    Tint = DamageTypes.Tint(DamageType.Normal)
                },

                new WeaponDefinition
                {
                    Id = "ember_repeater",
                    DisplayName = "Ember Repeater",
                    Flavor = "Spits burning slag. Hold the trigger and let it cook.",
                    Rarity = Rarity.Common,
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
                    Tint = DamageTypes.Tint(DamageType.Fire),
                    OnHitStatuses = { StatusLibrary.Burn(3f, 1, 4f) }
                },

                new WeaponDefinition
                {
                    Id = "frost_lance",
                    DisplayName = "Frost Lance",
                    Flavor = "A shard of the tower moat, fired at unkind speed.",
                    Rarity = Rarity.Uncommon,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Frost,
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
                    Tint = DamageTypes.Tint(DamageType.Frost),
                    OnHitStatuses = { StatusLibrary.Chill(4f, 2) }
                },

                new WeaponDefinition
                {
                    Id = "hexshot",
                    DisplayName = "Hexshot",
                    Flavor = "Eight barrels of powdered grave dirt and crushed root.",
                    Rarity = Rarity.Uncommon,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Nature,
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
                    Tint = DamageTypes.Tint(DamageType.Nature),
                    OnHitStatuses = { StatusLibrary.Blight(6f, 1, 2.5f) }
                },

                new WeaponDefinition
                {
                    Id = "sunder_cannon",
                    DisplayName = "Sunder Cannon",
                    Flavor = "Lobs a rune that disagrees with architecture.",
                    Rarity = Rarity.Rare,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Normal,
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
                    Tint = new Color(0.9f, 0.8f, 0.55f)
                },

                new WeaponDefinition
                {
                    Id = "voltaic_rail",
                    DisplayName = "Voltaic Rail",
                    Flavor = "Punches through a line of cultists and keeps going.",
                    Rarity = Rarity.Rare,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Astral,
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
                    Tint = DamageTypes.Tint(DamageType.Astral),
                    OnHitStatuses = { StatusLibrary.Shock(4f) }
                },

                new WeaponDefinition
                {
                    Id = "trigram",
                    DisplayName = "Trigram",
                    Flavor = "Three marks, drawn in one motion. The third is the one that sticks.",
                    Rarity = Rarity.Rare,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Burst,
                    BurstCount = 3,
                    BurstInterval = 0.055f,
                    DamageType = DamageType.Shadow,
                    Damage = 16f,
                    // RoundsPerMinute paces the burst, not the round: 120 is one burst every half second.
                    RoundsPerMinute = 120f,
                    MagazineSize = 18,          // six clean bursts
                    ReloadTime = 1.6f,
                    SpreadDegrees = 0.5f,
                    MovingSpreadDegrees = 1.4f,
                    RecoilPitch = 1.8f,
                    RecoilYaw = 0.35f,
                    Range = 120f,
                    Tint = DamageTypes.Tint(DamageType.Shadow),
                    OnHitStatuses = { StatusLibrary.Weaken(4f) }
                },

                new WeaponDefinition
                {
                    Id = "nightfall",
                    DisplayName = "Nightfall",
                    Flavor = "Fires a sliver of the dark between stars. The wounds do not close.",
                    Rarity = Rarity.Mythic,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Auto,
                    DamageType = DamageType.Shadow,
                    Damage = 17f,
                    RoundsPerMinute = 300f,
                    MagazineSize = 20,
                    ReloadTime = 1.7f,
                    SpreadDegrees = 0.8f,
                    MovingSpreadDegrees = 1.5f,
                    RecoilPitch = 1.1f,
                    RecoilYaw = 0.3f,
                    ProjectileSpeed = 68f,
                    ProjectileRadius = 0.16f,
                    MaxPierce = 1,
                    Tint = DamageTypes.Tint(DamageType.Shadow),
                    OnHitStatuses = { StatusLibrary.Weaken(4f), StatusLibrary.Blight(5f, 1, 3f) }
                },

                new WeaponDefinition
                {
                    Id = "requiem",
                    DisplayName = "Requiem",
                    Flavor = "The last thing eight wizards ever heard, in sequence.",
                    Rarity = Rarity.Legendary,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Astral,
                    Damage = 95f,
                    RoundsPerMinute = 45f,
                    MagazineSize = 3,
                    ReloadTime = 2.2f,
                    SpreadDegrees = 0f,
                    MovingSpreadDegrees = 0.1f,
                    RecoilPitch = 7f,
                    RecoilYaw = 0.5f,
                    Range = 250f,
                    MaxPierce = 8,
                    Knockback = 4f,
                    Tint = new Color(1f, 0.85f, 0.45f),
                    OnHitStatuses = { StatusLibrary.Shock(5f, 2), StatusLibrary.Mark(6f) }
                }
            };
        }
    }
}
