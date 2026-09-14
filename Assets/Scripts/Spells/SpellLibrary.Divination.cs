using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        private static readonly Color DivineTint = new Color(1f, 0.92f, 0.6f);

        /// <summary>Divination: heavenly sight, sharpening the wizard rather than the spell. Mastery: Divine Knowledge.</summary>
        private static List<Spell> DivinationSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "smite", School = SpellSchool.Divination, DisplayName = "Smite", ShortName = "SMIT",
                Description = "Your next bullet also calls lightning down on its target, dealing energy damage and shocking it.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Common,
                ManaCost = 14f, Cooldown = 6f, TintOverride = Palette.Lightning,
                OnCast =
                {
                    new InfuseBulletsEffect
                    {
                        InfusionId = "smite", Rounds = 1, BoltDamage = 34f, BoltType = DamageType.Energy,
                        BoltStatuses = { StatusLibrary.Shock(4f) }
                    }
                }
            },

            new Spell
            {
                Id = "prismatic_chains", School = SpellSchool.Divination, DisplayName = "Prismatic Chains", ShortName = "CHN",
                Description = "Tethers an enemy to the one nearest it. Kinetic or energy damage to either hurts the other as psychic.",
                Type = SpellType.Control, DamageType = DamageType.Psychic, Rarity = Rarity.Common,
                ManaCost = 18f, Cooldown = 12f, TintOverride = new Color(0.8f, 0.7f, 1f),
                OnCast =
                {
                    new SelectNearestAimedEffect { Range = 30f, MaxAngle = 15f },
                    new TetherEffect { PartnerRange = 10f, Leash = 5f, Seconds = 8f }
                }
            },

            new Spell
            {
                Id = "underworld_vial", School = SpellSchool.Divination, DisplayName = "Underworld Vial", ShortName = "VIAL",
                Description = "A vial thrown like a grenade, splashing necrotic damage and poison.",
                Type = SpellType.Attack, DamageType = DamageType.Necrotic, Rarity = Rarity.Common,
                ManaCost = 16f, Cooldown = 7f, TintOverride = Palette.Poison,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Poison, Duration = 7f, Stacks = 3, Magnitude = 1.4f },
                    new SpawnProjectileEffect { Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f, SplashRadius = 3.5f, SplashDamage = 26f }
                }
            },

            new Spell
            {
                Id = "divine_assistance", School = SpellSchool.Divination, DisplayName = "Divine Assistance", ShortName = "ASST",
                Description = "Your other weapon appears beside you and fires whenever you fire, with infinite ammo. You cannot swap weapons while it lasts.",
                Type = SpellType.Ward, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 26f, Cooldown = 24f, TintOverride = DivineTint,
                OnCast = { new MirrorGunEffect { Seconds = 8f } }
            },

            new Spell
            {
                Id = "foretell", School = SpellSchool.Divination, DisplayName = "Foretell", ShortName = "FTEL",
                Description = "The next attack that would hit you in the next few seconds misses. Dodging one halves the cooldown.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 14f, Cooldown = 14f, DodgeCooldownRefund = 7f, NoiseMultiplier = 0.3f, TintOverride = DivineTint,
                OnCast = { new SelfStatusEffect { Status = StatusId.Foretold, Duration = 3f, DurationPerLevel = 0.5f, Stacks = 1, Magnitude = 1f } }
            },

            new Spell
            {
                Id = "divine_star", School = SpellSchool.Divination, DisplayName = "Divine Star", ShortName = "STAR",
                Description = "A slow star that hunts the healthiest enemy fighting you and explodes on it.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 24f, Cooldown = 12f, TintOverride = DivineTint,
                OnCast = { new DivineStarEffect() }
            },

            new Spell
            {
                Id = "reckoning", School = SpellSchool.Divination, DisplayName = "Reckoning", ShortName = "RECK",
                Description = "Fears every enemy nearby, including ones that had not noticed you.",
                Type = SpellType.Control, DamageType = DamageType.Psychic, Rarity = Rarity.Rare,
                ManaCost = 32f, Cooldown = 20f, TintOverride = DivineTint,
                OnCast =
                {
                    new SelectSphereEffect { Radius = 9f },
                    new StatusPayloadEffect { Status = StatusId.Fear, Duration = 3f, Stacks = 1, Magnitude = 1f },
                    new ApplyPayloadEffect(),
                    new VfxGroundRingEffect { Radius = 9f }
                }
            },

            new Spell
            {
                Id = "judgement", School = SpellSchool.Divination, DisplayName = "Judgement", ShortName = "JUDG",
                Description = "A grenade carrying a set amount of burn, split between every enemy it catches. Catch just one.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Rare,
                ManaCost = 30f, Cooldown = 14f, TintOverride = Palette.Fire,
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f,
                        OnHit =
                        {
                            new SelectSphereEffect { Radius = 4f, AroundPoint = true },
                            new SplitStatusEffect { Status = StatusId.Burn, Duration = 12f, TotalMagnitude = 80f },
                            new VfxSphereEffect { Diameter = 2f, Alpha = 0.4f, Lifetime = 0.3f, GrowPerSecond = 8f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "consecrate", School = SpellSchool.Divination, DisplayName = "Consecrate", ShortName = "HOLY",
                Description = "Holy ground where you stand: faster, harder-hitting and harder to hurt while inside.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Mythic,
                ManaCost = 50f, Cooldown = 40f, TintOverride = DivineTint,
                OnCast =
                {
                    new PointAtCasterEffect { HeightOffset = 0f },
                    new LingeringZoneEffect
                    {
                        Radius = 6f, Duration = 12f, DamagePerTick = 0f, TickInterval = 0.5f, ScaleWithLevel = false,
                        AllyStatuses =
                        {
                            new StatusApplication(StatusId.Haste, 1f, 1, 0.3f),
                            new StatusApplication(StatusId.Empowered, 1f, 1, 0.3f),
                            new StatusApplication(StatusId.Fortify, 1f, 1, 0.3f)
                        }
                    }
                }
            },

            new Spell
            {
                Id = "ascend", School = SpellSchool.Divination, DisplayName = "Ascend", ShortName = "ASCN",
                Description = "A column of light lifts you straight up, briefly invulnerable, and you hang there before drifting down.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Common,
                ManaCost = 10f, Cooldown = 6f, MaxLevel = 1, TintOverride = DivineTint,
                OnCast =
                {
                    // Invulnerable for the lift, not the hang.
                    new GrantInvulnerabilityEffect { Seconds = 0.4f },
                    new ImpulseSequenceEffect
                    {
                        Steps =
                        {
                            new ImpulseStep { Delay = 0f, UpImpulse = 14f },
                            new ImpulseStep { Delay = 0.45f, Duration = 0.6f, StopVertical = true, GravityScale = 0f },
                            new ImpulseStep { Delay = 1.05f, Duration = 1.2f, GravityScale = 0.4f, EndOnLanding = true }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "path_of_light", School = SpellSchool.Divination, DisplayName = "Path of Light", ShortName = "PATH",
                Description = "Lights the route to the floor's exit for 10 seconds, and doubles your speed while you move along it.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Rare,
                ManaCost = 20f, Cooldown = 20f, MaxLevel = 1, NeverEchoes = true, TintOverride = DivineTint,
                OnCast =
                {
                    new RouteMarkersEffect { Seconds = 10f },
                    new ConditionalBuffEffect
                    {
                        BuffId = "path_of_light", Condition = BuffCondition.FollowingExitRoute,
                        Attribute = Attr.MoveSpeed, Percent = 1f, Duration = 10f
                    }
                }
            },

            new Spell
            {
                Id = "punish", School = SpellSchool.Divination, DisplayName = "Punish", ShortName = "PUNI",
                Description = "Sets the enemy in front of you burning, and deals no other damage.",
                Slot = SpellSlot.Melee, Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 5f, Cooldown = 1.2f, MaxLevel = 3, TintOverride = Palette.Fire,
                OnCast =
                {
                    new SelectConeEffect { Range = 3.2f, HalfAngle = 50f, RequireLineOfSight = false },
                    new StatusPayloadEffect { Status = StatusId.Burn, Duration = 12f, Stacks = 1, Magnitude = 22f },
                    new ApplyPayloadEffect()
                }
            }
        };
    }
}
