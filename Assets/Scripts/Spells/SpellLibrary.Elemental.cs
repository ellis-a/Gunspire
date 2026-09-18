using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        /// <summary>Elemental: fire, ice and storms, the school that simply deals damage well. Mastery: Conflux.</summary>
        private static List<Spell> ElementalSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "flaming_skull", School = SpellSchool.Elemental, DisplayName = "Flaming Skull", ShortName = "SKUL",
                Description = "A swaying skull of fire that passes through enemies, burning them and the ground behind it.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Common,
                ManaCost = 16f, Cooldown = 4f, TintOverride = Palette.Fire,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Burn, Duration = 12f, Stacks = 1, Magnitude = 10f },
                    new SpawnProjectileEffect
                    {
                        Damage = 18f, Speed = 16f, Radius = 0.35f, Lifetime = 3f, Pierce = 99,
                        SwayAmplitude = 1.2f, SwayFrequency = 1.5f,
                        FlattenAim = true,
                        Trail = new TrailProfile { Radius = 1f, SegmentLifetime = 3f, DamagePerTick = 4f }
                    }
                }
            },

            new Spell
            {
                Id = "ice_lance", School = SpellSchool.Elemental, DisplayName = "Ice Lance", ShortName = "LANC",
                Description = "An icy bolt that damages the first enemy it hits and applies frost.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 14f, Cooldown = 2.5f, TintOverride = Palette.Ice,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Frost, Duration = 5f, Stacks = 20, Magnitude = FrostStatus.SlowPerStack },
                    new SpawnProjectileEffect { Damage = 24f, Speed = 60f, Radius = 0.15f, Lifetime = 2.5f }
                }
            },

            new Spell
            {
                Id = "storm_blast", School = SpellSchool.Elemental, DisplayName = "Storm Blast", ShortName = "STRM",
                Description = "A blast of wind from behind you that knocks everything forward, including enemies beside you.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Common,
                ManaCost = 18f, Cooldown = 6f, TintOverride = Palette.Lightning,
                OnCast =
                {
                    new OriginOffsetEffect { Back = 3f },
                    new SelectConeEffect { Range = 11f, HalfAngle = 55f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = 14f, Knockback = 15f },
                    new VfxConeEffect { Range = 11f, HalfAngle = 55f }
                }
            },

            new Spell
            {
                Id = "frost_burn", School = SpellSchool.Elemental, DisplayName = "Frost Burn", ShortName = "FRBN",
                Description = "A cone of flame that burns and freezes at once.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 24f, Cooldown = 7f,
                OnCast =
                {
                    new SelectConeEffect { Range = 10f, HalfAngle = 35f },
                    new StatusPayloadEffect { Status = StatusId.Burn, Duration = 12f, Stacks = 1, Magnitude = 14f },
                    new StatusPayloadEffect { Status = StatusId.Frost, Duration = 5f, Stacks = 15, Magnitude = FrostStatus.SlowPerStack },
                    new DealDamageEffect { Amount = 12f },
                    new VfxConeEffect { Range = 10f, HalfAngle = 35f }
                }
            },

            new Spell
            {
                Id = "hailstorm", School = SpellSchool.Elemental, DisplayName = "Hailstorm", ShortName = "HAIL",
                Description = "A room-sized zone of hail and lightning for 15 seconds, applying frost and shock.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 30f, Cooldown = 20f, TintOverride = Palette.Ice,
                OnCast =
                {
                    new AimPointEffect { Range = 40f },
                    new StatusPayloadEffect { Status = StatusId.Frost, Duration = 3f, Stacks = 4, Magnitude = FrostStatus.SlowPerStack },
                    new StatusPayloadEffect { Status = StatusId.Shock, Duration = 3f, Stacks = 6, Magnitude = 0.01f },
                    new LingeringZoneEffect { Radius = 8f, Duration = 15f, DamagePerTick = 3f, TickInterval = 0.5f, ScaleWithLevel = false }
                }
            },

            new Spell
            {
                Id = "star_comet", School = SpellSchool.Elemental, DisplayName = "Star Comet", ShortName = "CMET",
                Description = "A fire and lightning comet from the sky: energy then kinetic damage, knocking enemies away and shocking them.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 28f, Cooldown = 10f, TintOverride = Palette.Fire,
                OnCast =
                {
                    new AimPointEffect { Range = 40f },
                    new TelegraphCircleEffect { Radius = 4f, Duration = 0.9f },
                    new StatusPayloadEffect { Status = StatusId.Shock, Duration = 4f, Stacks = 12, Magnitude = 0.01f },
                    new DelayedBlastEffect { Delay = 0.9f, Radius = 4f, Damage = 30f, Knockback = 0f },
                    new SetDamageTypeEffect { Type = DamageType.Kinetic },
                    new DelayedBlastEffect { Delay = 0.95f, Radius = 4f, Damage = 30f, Knockback = 14f }
                }
            },

            new Spell
            {
                Id = "elemental_chaos", School = SpellSchool.Elemental, DisplayName = "Elemental Chaos", ShortName = "CHAO",
                Description = "Casts two random uncommon Elemental spells. The same one can come up twice.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Rare,
                ManaCost = 40f, Cooldown = 14f,
                OnCast = { new CastRandomSpellEffect { School = SpellSchool.Elemental, Rarity = Rarity.Uncommon, Count = 2, AllowRepeats = true } }
            },

            new Spell
            {
                Id = "elemental_order", School = SpellSchool.Elemental, DisplayName = "Elemental Order", ShortName = "ORDR",
                Description = "Applies burn, shock and frost to every enemy nearby.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Rare,
                ManaCost = 38f, Cooldown = 16f,
                OnCast =
                {
                    new SelectSphereEffect { Radius = 9f },
                    new StatusPayloadEffect { Status = StatusId.Burn, Duration = 12f, Stacks = 1, Magnitude = 14f },
                    new StatusPayloadEffect { Status = StatusId.Shock, Duration = 5f, Stacks = 12, Magnitude = 0.01f },
                    new StatusPayloadEffect { Status = StatusId.Frost, Duration = 5f, Stacks = 20, Magnitude = FrostStatus.SlowPerStack },
                    new ApplyPayloadEffect(),
                    new VfxGroundRingEffect { Radius = 9f }
                }
            },

            new Spell
            {
                Id = "elemental_form", School = SpellSchool.Elemental, DisplayName = "Elemental Form", ShortName = "FORM",
                Description = "A stance, always on. Tap to change form: fire burns what you shoot and scorches what is near, " +
                              "ice freezes and steadies your aim, storm shocks and quickens you.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Mythic,
                ManaCost = 0f, Cooldown = 0f, MaxLevel = 1,
                Stance = new StanceProfile
                {
                    Modes =
                    {
                        new StanceMode
                        {
                            Name = "Fire", Tint = Palette.Fire,
                            BulletStatuses = { StatusLibrary.Burn(amount: 6f) },
                            AuraDamagePerSecond = 6f, AuraRadius = 4f
                        },
                        new StanceMode
                        {
                            Name = "Ice", Tint = Palette.Ice,
                            BulletStatuses = { StatusLibrary.Frost(stacks: 6) },
                            Modifiers =
                            {
                                new StanceModifier { Attr = Attr.Spread, Percent = -0.35f },
                                new StanceModifier { Attr = Attr.Recoil, Percent = -0.35f }
                            }
                        },
                        new StanceMode
                        {
                            Name = "Storm", Tint = Palette.Lightning,
                            BulletStatuses = { StatusLibrary.Shock(3f, 6) },
                            Modifiers = { new StanceModifier { Attr = Attr.MoveSpeed, Percent = 0.2f } }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "ride_the_gale", School = SpellSchool.Elemental, DisplayName = "Ride the Gale", ShortName = "GALE",
                Description = "A push forward, a kick upward, then a slow fall: an S curve, best from mid-air.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Common,
                ManaCost = 10f, Cooldown = 5f, MaxLevel = 1, TintOverride = Palette.Lightning,
                OnCast =
                {
                    new ImpulseSequenceEffect
                    {
                        Steps =
                        {
                            new ImpulseStep { Delay = 0f, Duration = 0.25f, ForwardForce = 70f, SuppressFriction = true },
                            new ImpulseStep { Delay = 0.35f, UpImpulse = 11f },
                            new ImpulseStep { Delay = 0.4f, Duration = 1f, GravityScale = 0.35f, EndOnLanding = true }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "burning_feet", School = SpellSchool.Elemental, DisplayName = "Burning Feet", ShortName = "FEET",
                Description = "Run faster, leaving a trail of fire behind you.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Rare,
                MaxLevel = 1, TintOverride = Palette.Fire,
                Sustain = new SustainProfile
                {
                    ManaPerSecond = 9f, MoveSpeedBonus = 0.35f,
                    Trail = new TrailProfile { Radius = 1f, Spacing = 1.5f, SegmentLifetime = 2f, DamagePerTick = 3f }
                }
            },

            new Spell
            {
                Id = "rimeblade", School = SpellSchool.Elemental, DisplayName = "Rimeblade", ShortName = "RIME",
                Description = "A wide horizontal arc of ice.",
                Slot = SpellSlot.Melee, Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 6f, Cooldown = 1f, MaxLevel = 3, TintOverride = Palette.Ice,
                OnCast =
                {
                    new SelectConeEffect { Range = 3.4f, HalfAngle = 80f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = 26f, CanCrit = true }
                }
            }
        };
    }
}
