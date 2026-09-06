using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Every spell in the game, as data. A spell is its identity plus a chain of
    /// <see cref="AbilityEffect"/>s; the effects themselves are shared with enemy attacks.
    ///
    /// Adding a spell is one entry here. It needs new C# only when it wants behaviour no
    /// existing effect provides.
    /// </summary>
    public static class SpellLibrary
    {
        private static List<Spell> _all;

        public static IReadOnlyList<Spell> All
        {
            get
            {
                if (_all == null) BuildRoster();
                return _all;
            }
        }

        public static Spell Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>
        /// Spells a shrine can still offer: never learned, or learned but below their level
        /// cap. Taking one you already know levels it instead of rebinding it.
        /// </summary>
        public static List<Spell> Offerable(SpellBook book)
        {
            var list = new List<Spell>();
            for (int i = 0; i < All.Count; i++)
                if (book == null || book.CanTake(All[i])) list.Add(All[i]);
            return list;
        }

        /// <summary>Rolls a rarity from Luck, then picks an offerable spell at that tier.</summary>
        public static Spell RollOffer(Rng rng, SpellBook book, float luck, float rarityBonus = 1f)
        {
            List<Spell> pool = Offerable(book);
            if (pool.Count == 0) return null;

            Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
            return Rarities.PickOfRarity(rng, pool, s => s.Rarity, rolled);
        }

        // ---------------------------------------------------------------- roster

        private static void BuildRoster()
        {
            _all = new List<Spell>
            {
                LavaSplash(),
                ConeOfCold(),
                Firebolt(),
                KineticSlam(),
                ArcaneWard(),
                Blightbloom(),
                ChainLightning(),
                GlacialPrison(),
                Eventide()
            };
        }


        /// <summary>A lobbed grenade that bursts and leaves the ground burning.</summary>
        private static Spell LavaSplash() => new Spell
        {
            Id = "lava_splash",
            DisplayName = "Lava Splash",
            ShortName = "LAVA",
            Description = "Lob a gobbet of molten rock. It bursts on landing and leaves the floor " +
                          "burning, so it denies a doorway as readily as it kills.",
            ManaCost = 24f,
            Cooldown = 6f,
            Rarity = Rarity.Common,
            Type = SpellType.Attack,
            DamageType = DamageType.Fire,
            LevelUpNote = "and a wider, longer-lasting pool",
            OnCast =
            {
                new StatusPayloadEffect
                {
                    Status = StatusId.Burn, Duration = 4f,
                    Stacks = 2, StacksPerLevel = 1f, Magnitude = 5f
                },
                new SpawnProjectileEffect
                {
                    // Thrown, not fired: gravity is what makes it a grenade.
                    Damage = 0f,
                    Speed = 26f,
                    Gravity = 11f,
                    Radius = 0.3f,
                    Lifetime = 5f,
                    OnHit =
                    {
                        new SelectSphereEffect { Radius = 3.8f, AroundPoint = true, ScaleRadiusWithLevel = true },
                        new DealDamageEffect
                        {
                            Amount = 34f, FalloffFromPoint = true, FalloffRadius = 3.8f,
                            MinFraction = 0.45f, Knockback = 4f
                        },
                        new LingeringZoneEffect
                        {
                            Radius = 3.4f, Duration = 4.5f, DamagePerTick = 5f, TickInterval = 0.5f
                        },
                        new VfxSphereEffect { Diameter = 1.8f, Alpha = 0.55f, Lifetime = 0.3f, GrowPerSecond = 10f }
                    }
                }
            }
        };

        private static Spell ConeOfCold() => new Spell
        {
            Id = "cone_of_cold",
            DisplayName = "Cone of Cold",
            ShortName = "COLD",
            Description = "A freezing cone that damages and heavily chills everything in front of you. " +
                          "Chilled enemies that saturate freeze solid.",
            ManaCost = 26f,
            Cooldown = 6.5f,
            Rarity = Rarity.Common,
            Type = SpellType.Control,
            DamageType = DamageType.Frost,
            LevelUpNote = "and more chill per hit",
            OnCast =
            {
                new SelectConeEffect { Range = 13f, HalfAngle = 34f, RequireLineOfSight = true },
                new StatusPayloadEffect
                {
                    Status = StatusId.Chill, Duration = 4.5f,
                    Stacks = 2, StacksPerLevel = 0.5f, Magnitude = 0.11f
                },
                new DealDamageEffect { Amount = 17f },
                new VfxConeEffect { Range = 13f, HalfAngle = 34f },
                new VfxShardsEffect { Range = 13f, SpreadDegrees = 34f }
            }
        };

        private static Spell Firebolt() => new Spell
        {
            Id = "firebolt",
            DisplayName = "Firebolt",
            ShortName = "FIRE",
            Description = "Hurl a bolt of fire that detonates on impact and sets the survivors alight.",
            ManaCost = 22f,
            Cooldown = 3.5f,
            Rarity = Rarity.Uncommon,
            Type = SpellType.Attack,
            DamageType = DamageType.Fire,
            OnCast =
            {
                new StatusPayloadEffect
                {
                    Status = StatusId.Burn, Duration = 5f,
                    Stacks = 2, StacksPerLevel = 1f, Magnitude = 5f
                },
                new SpawnProjectileEffect
                {
                    // All of the damage comes from the blast, so the bolt itself only carries
                    // the payload. This is the OnHit hook doing the work.
                    Damage = 0f,
                    Speed = 42f,
                    Radius = 0.28f,
                    Lifetime = 5f,
                    OnHit =
                    {
                        new SelectSphereEffect { Radius = 3.2f, AroundPoint = true, ScaleRadiusWithLevel = true },
                        new DealDamageEffect
                        {
                            Amount = 30f, FalloffFromPoint = true, FalloffRadius = 3.2f,
                            MinFraction = 0.5f, Knockback = 2f
                        },
                        new VfxSphereEffect { Diameter = 1.6f, Alpha = 0.5f, Lifetime = 0.25f, GrowPerSecond = 8f }
                    }
                }
            }
        };

        private static Spell KineticSlam() => new Spell
        {
            Id = "kinetic_slam",
            DisplayName = "Kinetic Slam",
            ShortName = "SLAM",
            Description = "A shockwave around you that hurls enemies back and shatters weak barriers.",
            ManaCost = 24f,
            Cooldown = 8f,
            Rarity = Rarity.Uncommon,
            Type = SpellType.Control,
            DamageType = DamageType.Normal,
            TintOverride = new Color(0.9f, 0.75f, 0.45f),
            OnCast =
            {
                new SelectSphereEffect { Radius = 6.5f, ScaleRadiusWithLevel = true },
                new StatusPayloadEffect
                {
                    Status = StatusId.Weaken, Duration = 4f, Stacks = 1, Magnitude = 0.15f
                },
                new DealDamageEffect
                {
                    Amount = 22f, FalloffFromPoint = true, FalloffRadius = 6.5f,
                    MinFraction = 0.5f, Knockback = 9f, UseSmashPower = true
                },
                new VfxGroundRingEffect { Radius = 6.5f }
            }
        };

        private static Spell ArcaneWard() => new Spell
        {
            Id = "arcane_ward",
            DisplayName = "Arcane Ward",
            ShortName = "WARD",
            Description = "Sheathe yourself in force: take much less damage for a few seconds and mend a wound.",
            ManaCost = 28f,
            Cooldown = 14f,
            Rarity = Rarity.Uncommon,
            Type = SpellType.Ward,
            DamageType = DamageType.Astral,
            TintOverride = new Color(0.75f, 0.8f, 1f),
            LevelUpNote = "and a longer ward",
            OnCast =
            {
                new SelfStatusEffect
                {
                    Status = StatusId.Fortify, Duration = 5f, DurationPerLevel = 1f,
                    Stacks = 2, Magnitude = 0.18f
                },
                new HealSelfEffect { FractionOfMax = 0.12f },
                new VfxSphereEffect { Diameter = 2.4f, Alpha = 0.22f, Lifetime = 0.6f, AttachToCaster = true }
            }
        };

        private static Spell Blightbloom() => new Spell
        {
            Id = "blightbloom",
            DisplayName = "Blightbloom",
            ShortName = "BLOM",
            Description = "Lob a seed that bursts into a patch of rot. Anything standing in it takes " +
                          "nature damage and is blighted, so its healing is swallowed too.",
            ManaCost = 32f,
            Cooldown = 11f,
            Rarity = Rarity.Rare,
            Type = SpellType.Control,
            DamageType = DamageType.Nature,
            MaxLevel = 4,
            LevelUpNote = "and a wider, longer-lived patch",
            OnCast =
            {
                new StatusPayloadEffect
                {
                    Status = StatusId.Blight, Duration = 6f,
                    Stacks = 2, StacksPerLevel = 0.5f, Magnitude = 3f
                },
                new SpawnProjectileEffect
                {
                    // The seed does nothing on its own; the patch it leaves is the whole spell.
                    Damage = 0f,
                    Speed = 30f,
                    Radius = 0.3f,
                    Gravity = 10f,          // lobbed, so it can be thrown over cover
                    Lifetime = 5f,
                    OnHit =
                    {
                        new LingeringZoneEffect
                        {
                            Radius = 4.5f, Duration = 6f,
                            DamagePerTick = 6f, TickInterval = 0.5f
                        },
                        new VfxSphereEffect
                        {
                            Diameter = 1.2f, Alpha = 0.45f, Lifetime = 0.3f, GrowPerSecond = 10f
                        }
                    }
                }
            }
        };

        private static Spell ChainLightning() => new Spell
        {
            Id = "chain_lightning",
            DisplayName = "Chain Lightning",
            ShortName = "ARC",
            Description = "An arc that leaps between nearby enemies, shocking each one.",
            ManaCost = 30f,
            Cooldown = 7f,
            Rarity = Rarity.Rare,
            Type = SpellType.Attack,
            DamageType = DamageType.Astral,
            LevelUpNote = "and one more target",
            OnCast =
            {
                new StatusPayloadEffect
                {
                    Status = StatusId.Shock, Duration = 4f, Stacks = 1, Magnitude = 0.12f
                },
                new ChainEffect { Damage = 26f, BaseJumps = 3, JumpsPerLevel = 1f }
            }
        };

        private static Spell GlacialPrison() => new Spell
        {
            Id = "glacial_prison",
            DisplayName = "Glacial Prison",
            ShortName = "PRSN",
            Description = "Every enemy around you is frozen solid and takes heavy frost damage. " +
                          "Frozen targets shatter for bonus damage when struck.",
            ManaCost = 42f,
            Cooldown = 18f,
            Rarity = Rarity.Mythic,
            Type = SpellType.Control,
            DamageType = DamageType.Frost,
            MaxLevel = 4,
            LevelUpNote = "and a longer freeze",
            OnCast =
            {
                new SelectSphereEffect { Radius = 14f, ScaleRadiusWithLevel = true },
                new StatusPayloadEffect
                {
                    Status = StatusId.Freeze, Duration = 2.0f, DurationPerLevel = 0.35f,
                    Stacks = 1, Magnitude = 1f
                },
                new DealDamageEffect { Amount = 40f },
                new VfxOnTargetsEffect { Lifetime = 2f, LifetimePerLevel = 0.35f },
                new VfxGroundRingEffect { Radius = 14f, Alpha = 0.3f, Lifetime = 0.5f }
            }
        };

        private static Spell Eventide() => new Spell
        {
            Id = "eventide",
            DisplayName = "Eventide",
            ShortName = "EVEN",
            Description = "Mark a point in the world. A moment later the light there collapses, " +
                          "tearing apart everything inside and withering the survivors.",
            ManaCost = 55f,
            Cooldown = 22f,
            Rarity = Rarity.Legendary,
            Type = SpellType.Attack,
            DamageType = DamageType.Shadow,
            MaxLevel = 3,
            GrowthPerLevel = 0.35f,
            OnCast =
            {
                new AimPointEffect { Range = 60f },
                new TelegraphCircleEffect { Radius = 9f, Duration = 0.9f },
                new StatusPayloadEffect
                {
                    Status = StatusId.Weaken, Duration = 6f, Stacks = 2, Magnitude = 0.15f
                },
                new StatusPayloadEffect
                {
                    Status = StatusId.Blight, Duration = 6f, Stacks = 3, Magnitude = 4f
                },
                new DelayedBlastEffect { Delay = 0.9f, Radius = 9f, Damage = 95f, Knockback = 10f }
            }
        };
    }
}
