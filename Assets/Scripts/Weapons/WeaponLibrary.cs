using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The gun roster. The starting weapon is named by <see cref="StartingLoadout.WeaponId"/>
    /// and is excluded from world drops.
    ///
    /// The built-ins live in code so a fresh clone runs with nothing authored. Any
    /// <see cref="WeaponAsset"/> found under a Resources folder is merged in: a matching id
    /// replaces the built-in, a new id is added. That gives Inspector editing without the game
    /// depending on assets existing.
    /// </summary>
    public static class WeaponLibrary
    {
        private static List<WeaponDefinition> _all;

        public static IReadOnlyList<WeaponDefinition> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        /// <summary>Drops the cached roster so authored assets are picked up again.</summary>
        public static void Reload() => _all = null;

        private static void Build()
        {
            // Assign before merging: anything that reads the roster while assets are loading
            // gets the built-ins rather than recursing into a half-built list.
            _all = BuiltIn();

            WeaponAsset[] authored = Resources.LoadAll<WeaponAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                WeaponDefinition def = authored[i] != null ? authored[i].Definition : null;
                if (def == null) continue;

                if (string.IsNullOrEmpty(def.Id))
                {
                    Debug.LogWarning("Weapon asset \"" + authored[i].name + "\" has no Id and was ignored.");
                    continue;
                }

                int existing = _all.FindIndex(w => w.Id == def.Id);
                if (existing >= 0) _all[existing] = def;
                else _all.Add(def);
            }
        }

        public static WeaponDefinition Get(string id)
        {
            if (_all == null) Build();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Id == id) return _all[i].Clone();

            // Fall back to the first gun on the roster. Never route this through the starting
            // weapon: if that id is the one missing, the two would call each other forever.
            Debug.LogWarning("WeaponLibrary has no weapon with id \"" + id + "\". Falling back to "
                             + _all[0].DisplayName + ".");
            return _all[0].Clone();
        }

        /// <summary>
        /// The roster entry itself, for display only. Unlike <see cref="Get"/> this does not
        /// clone, so never hand the result to a Weapon - UI reading it every frame would
        /// otherwise allocate a copy each time.
        /// </summary>
        public static WeaponDefinition Peek(string id)
        {
            if (_all == null) Build();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Id == id) return _all[i];
            return null;
        }

        /// <summary>
        /// A gun for a plinth or a vault. Rolls a rarity from the player's Luck, then picks a
        /// gun at that tier. The weapon the player already starts holding is excluded, so
        /// changing the starting gun automatically keeps it out of world drops.
        /// </summary>
        public static WeaponDefinition RollDrop(Rng rng, float luck, float rarityBonus = 1f)
        {
            if (_all == null) Build();

            var pool = new List<WeaponDefinition>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Id != StartingLoadout.WeaponId) pool.Add(_all[i]);
            if (pool.Count == 0) pool.AddRange(_all);

            Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
            WeaponDefinition pick = Rarities.PickOfRarity(rng, pool, w => w.Rarity, rolled);
            return pick.Clone();
        }

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<WeaponDefinition> BuiltIn()
        {
            List<WeaponDefinition> guns = BuiltInBodies();
            AttachAltFires(guns);
            return guns;
        }

        /// <summary>
        /// Right click, kept in one place rather than spread through the table above.
        /// Balancing an alt fire means comparing it against the other alt fires, and every gun
        /// that has none is visible here by its absence.
        ///
        /// UnlockTier 0 is live the moment you pick the gun up. Every starting gun is tier 0,
        /// so all three classes meet the mechanic in the first room; the stronger alt fires sit
        /// behind the Gunsmith boon, which is what lets a good gun arrive before its best
        /// button does.
        /// </summary>
        private static void AttachAltFires(List<WeaponDefinition> guns)
        {
            // The revolver dump. Costs the rest of the cylinder and a long reload after.
            Set(guns, "arcanum", new AltFireProfile
            {
                Kind = AltFireKind.Salvo,
                Name = "Fan the Hammer",
                Description = "Empties the cylinder as fast as the hammer will fall.",
                Cooldown = 2.6f,
                SalvoRateMultiplier = 4.5f,
                SalvoDamageMultiplier = 0.8f,
                SalvoMaxRounds = 12
            });

            // An uzi is inaccurate by design, so its alt is the answer to that rather than
            // more of the same.
            Set(guns, "emberspit", new AltFireProfile
            {
                Kind = AltFireKind.Focus,
                Name = "Cinder Sights",
                Description = "Hold to steady the spray.",
                FocusFov = 62f,
                FocusSpreadMultiplier = 0.30f,
                FocusDamageMultiplier = 1.15f,
                FocusRateMultiplier = 0.85f
            });

            Set(guns, "hailmaker", new AltFireProfile
            {
                Kind = AltFireKind.Shot,
                Name = "Slug Round",
                Description = "One solid slug instead of the spread. Hits hard, hits far.",
                Cooldown = 1.1f,
                AmmoCost = 1,
                Delivery = DeliveryKind.Hitscan,
                Damage = 62f,
                Knockback = 7f,
                RecoilPitch = 4.5f
            });

            // Already a launcher, so its alt inverts it: no blast, all impact.
            Set(guns, "knell", new AltFireProfile
            {
                Kind = AltFireKind.Shot,
                Name = "Contact Fuse",
                Description = "A flat, fast round that spends everything on the thing it hits.",
                Cooldown = 1.6f,
                Delivery = DeliveryKind.Projectile,
                Damage = 78f,
                ProjectileSpeed = 62f,
                ProjectileRadius = 0.2f,
                Knockback = 5f,
                RecoilPitch = 3.4f
            });

            // ember_repeater deliberately has none. It is the plain one.

            Set(guns, "frost_lance", new AltFireProfile
            {
                Kind = AltFireKind.Focus,
                Name = "Steady Aim",
                Description = "Hold to brace the lance.",
                FocusFov = 45f,
                FocusSpreadMultiplier = 0.1f,
                FocusDamageMultiplier = 1.35f,
                FocusRateMultiplier = 0.75f
            });

            Set(guns, "hexshot", new AltFireProfile
            {
                Kind = AltFireKind.Salvo,
                Name = "Both Barrels",
                Description = "Dumps every shell in the tube.",
                UnlockTier = 1,
                Cooldown = 4f,
                SalvoRateMultiplier = 3.2f,
                SalvoDamageMultiplier = 0.9f,
                SalvoMaxRounds = 6
            });

            Set(guns, "sunder_cannon", new AltFireProfile
            {
                Kind = AltFireKind.Shot,
                Name = "Airburst",
                Description = "Arcs high and opens wide. Less bite, far more reach.",
                UnlockTier = 1,
                Cooldown = 2.4f,
                AmmoCost = 2,
                Delivery = DeliveryKind.Projectile,
                Damage = 14f,
                SplashRadius = 7.5f,
                SplashDamage = 52f,
                ProjectileSpeed = 26f,
                ProjectileGravity = 14f,
                ProjectileRadius = 0.32f,
                Knockback = 9f,
                RecoilPitch = 5f
            });

            Set(guns, "voltaic_rail", new AltFireProfile
            {
                Kind = AltFireKind.Focus,
                Name = "Overcharge",
                Description = "Hold to narrow the coil.",
                UnlockTier = 1,
                FocusFov = 38f,
                FocusSpreadMultiplier = 0.05f,
                FocusDamageMultiplier = 1.5f,
                FocusRateMultiplier = 0.7f
            });

            // The underbarrel launcher on a burst rifle.
            Set(guns, "trigram", new AltFireProfile
            {
                Kind = AltFireKind.Shot,
                Name = "Underbarrel Grenade",
                Description = "Lobs a grenade. Arcs, so lead your throws.",
                UnlockTier = 1,
                Cooldown = 3.2f,
                AmmoCost = 3,
                Delivery = DeliveryKind.Projectile,
                Damage = 20f,
                SplashRadius = 4.6f,
                SplashDamage = 46f,
                ProjectileSpeed = 30f,
                ProjectileGravity = 16f,
                ProjectileRadius = 0.26f,
                Knockback = 7f,
                RecoilPitch = 4f
            });

            Set(guns, "nightfall", new AltFireProfile
            {
                Kind = AltFireKind.Shot,
                Name = "Umbral Lance",
                Description = "A heavy bolt that runs the length of a corridor.",
                UnlockTier = 2,
                Cooldown = 2.8f,
                AmmoCost = 4,
                Delivery = DeliveryKind.Projectile,
                Damage = 96f,
                MaxPierce = 5,
                ProjectileSpeed = 70f,
                ProjectileRadius = 0.34f,
                Knockback = 6f,
                RecoilPitch = 4.2f
            });

            Set(guns, "requiem", new AltFireProfile
            {
                Kind = AltFireKind.Focus,
                Name = "Dirge",
                Description = "Hold to sight down the barrel.",
                UnlockTier = 2,
                FocusFov = 32f,
                FocusSpreadMultiplier = 0.02f,
                FocusDamageMultiplier = 1.6f,
                FocusRateMultiplier = 0.65f
            });
        }

        private static void Set(List<WeaponDefinition> guns, string id, AltFireProfile alt)
        {
            WeaponDefinition gun = guns.Find(w => w.Id == id);
            if (gun == null)
            {
                // A renamed gun would otherwise lose its alt fire silently.
                Debug.LogWarning("Alt fire authored for unknown weapon id \"" + id + "\".");
                return;
            }
            gun.AltFire = alt;
        }

        private static List<WeaponDefinition> BuiltInBodies()
        {
            return new List<WeaponDefinition>
            {
                new WeaponDefinition
                {
                    Id = "arcanum",
                    DisplayName = "Arcanum .38",
                    Flavor = "Enchanted sidearm. Reliable, unglamorous, always loaded.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Kinetic,
                    Damage = 14f,
                    RoundsPerMinute = 320f,
                    MagazineSize = 12,
                    ReloadTime = 1.1f,
                    SpreadDegrees = 0.4f,
                    MovingSpreadDegrees = 1.1f,
                    RecoilPitch = 1.3f,
                    RecoilYaw = 0.3f,
                    Range = 140f,
                    Tint = DamageTypes.Tint(DamageType.Kinetic)
                },

                // ---- the three signature starting guns, tuned to roughly the Arcanum's 75 dps
                // so no loadout opens ahead of the others ----

                new WeaponDefinition
                {
                    Id = "emberspit",
                    DisplayName = "Emberspit",
                    Flavor = "Thirty rounds of lit pitch. Accuracy is not the point.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Auto,
                    DamageType = DamageType.Energy,
                    Damage = 6f,
                    RoundsPerMinute = 750f,
                    MagazineSize = 30,
                    ReloadTime = 1.5f,
                    SpreadDegrees = 2.2f,
                    MovingSpreadDegrees = 3.4f,
                    RecoilPitch = 0.35f,
                    RecoilYaw = 0.4f,
                    ProjectileSpeed = 75f,
                    ProjectileRadius = 0.11f,
                    Tint = DamageTypes.Tint(DamageType.Energy),
                    OnHitStatuses = { StatusLibrary.Burn(2.5f, 1, 3f) }
                },

                new WeaponDefinition
                {
                    Id = "hailmaker",
                    DisplayName = "Hailmaker",
                    Flavor = "Loads a fistful of frozen gravel. Best answered at close range.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Kinetic,
                    Damage = 7f,
                    PelletsPerShot = 7,
                    RoundsPerMinute = 90f,
                    MagazineSize = 5,
                    ReloadTime = 1.9f,
                    SpreadDegrees = 7f,
                    MovingSpreadDegrees = 8.5f,
                    RecoilPitch = 3.8f,
                    RecoilYaw = 0.55f,
                    Range = 38f,
                    Knockback = 2f,
                    Tint = DamageTypes.Tint(DamageType.Kinetic),
                    OnHitStatuses = { StatusLibrary.Frost(3f, 1) }
                },

                // ---- the two starter guns with no alt fire. Both plain Normal damage: they
                // are the openings for classes built around a stat rather than a school ----

                new WeaponDefinition
                {
                    Id = "rocket_launcher",
                    DisplayName = "Rocket Launcher",
                    Flavor = "Points, fires, and asks nothing else of you.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Kinetic,
                    Damage = 20f,
                    RoundsPerMinute = 65f,
                    MagazineSize = 4,
                    ReloadTime = 2.2f,
                    SpreadDegrees = 0.5f,
                    MovingSpreadDegrees = 1.2f,
                    RecoilPitch = 5f,
                    RecoilYaw = 0.4f,

                    // Flat, unlike the Knell's lob: no gravity, so it goes where you point it.
                    ProjectileSpeed = 44f,
                    ProjectileRadius = 0.28f,
                    ProjectileGravity = 0f,
                    ProjectileLifetime = 5f,
                    SplashRadius = 4.4f,
                    SplashDamage = 46f,
                    Knockback = 8f,
                    Tint = DamageTypes.Tint(DamageType.Kinetic)
                },

                new WeaponDefinition
                {
                    Id = "minigun",
                    DisplayName = "Minigun",
                    Flavor = "Takes a moment to get going. Then it does not stop.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Auto,
                    DamageType = DamageType.Kinetic,
                    Damage = 5f,
                    RoundsPerMinute = 900f,
                    MagazineSize = 90,
                    ReloadTime = 3f,

                    // The trade: it hoses a room but cannot pick anything out of it.
                    SpreadDegrees = 5.5f,
                    MovingSpreadDegrees = 7.5f,
                    RecoilPitch = 0.22f,
                    RecoilYaw = 0.5f,
                    Range = 90f,

                    SpinUpSeconds = 0.7f,
                    SpinDownMultiplier = 1.6f,
                    Tint = DamageTypes.Tint(DamageType.Kinetic)
                },

                new WeaponDefinition
                {
                    Id = "knell",
                    DisplayName = "Knell",
                    Flavor = "Fires a bell that rings once, on arrival, for whoever is nearest.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Necrotic,
                    Damage = 16f,
                    RoundsPerMinute = 80f,
                    MagazineSize = 4,
                    ReloadTime = 2.0f,
                    SpreadDegrees = 0.6f,
                    MovingSpreadDegrees = 1.3f,
                    RecoilPitch = 4.5f,
                    RecoilYaw = 0.45f,
                    ProjectileSpeed = 32f,
                    ProjectileRadius = 0.24f,
                    ProjectileGravity = 9f,
                    SplashRadius = 3.6f,
                    SplashDamage = 30f,
                    Knockback = 6f,
                    Tint = DamageTypes.Tint(DamageType.Necrotic),
                    OnHitStatuses = { StatusLibrary.Weaken(4f) }
                },

                new WeaponDefinition
                {
                    Id = "ember_repeater",
                    DisplayName = "Ember Repeater",
                    Flavor = "Spits burning slag. Hold the trigger and let it cook.",
                    Rarity = Rarity.Common,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Auto,
                    DamageType = DamageType.Energy,
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
                    Tint = DamageTypes.Tint(DamageType.Energy),
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
                    DamageType = DamageType.Kinetic,
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
                    Tint = DamageTypes.Tint(DamageType.Kinetic),
                    OnHitStatuses = { StatusLibrary.Frost(4f, 2) }
                },

                new WeaponDefinition
                {
                    Id = "hexshot",
                    DisplayName = "Hexshot",
                    Flavor = "Eight barrels of powdered grave dirt and crushed root.",
                    Rarity = Rarity.Uncommon,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Necrotic,
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
                    Tint = DamageTypes.Tint(DamageType.Necrotic),
                    OnHitStatuses = { StatusLibrary.Poison(6f, 1, 2.5f) }
                },

                new WeaponDefinition
                {
                    Id = "sunder_cannon",
                    DisplayName = "Sunder Cannon",
                    Flavor = "Lobs a rune that disagrees with architecture.",
                    Rarity = Rarity.Rare,
                    Delivery = DeliveryKind.Projectile,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Kinetic,
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
                    DamageType = DamageType.Energy,
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
                    Tint = DamageTypes.Tint(DamageType.Energy),
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
                    DamageType = DamageType.Necrotic,
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
                    Tint = DamageTypes.Tint(DamageType.Necrotic),
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
                    DamageType = DamageType.Necrotic,
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
                    Tint = DamageTypes.Tint(DamageType.Necrotic),
                    OnHitStatuses = { StatusLibrary.Weaken(4f), StatusLibrary.Poison(5f, 1, 3f) }
                },

                new WeaponDefinition
                {
                    Id = "requiem",
                    DisplayName = "Requiem",
                    Flavor = "The last thing eight wizards ever heard, in sequence.",
                    Rarity = Rarity.Legendary,
                    Delivery = DeliveryKind.Hitscan,
                    Mode = FireMode.Semi,
                    DamageType = DamageType.Energy,
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
