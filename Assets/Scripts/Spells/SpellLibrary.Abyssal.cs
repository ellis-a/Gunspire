using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        private static readonly Color AbyssalTint = new Color(0.2f, 0.55f, 0.6f);

        /// <summary>
        /// Abyssal: demons and the deep, paying in your own life. Every cast costs health as well as a little
        /// mana, which the Blood Debt turns into debt. Mastery: the Blood Debt.
        /// </summary>
        private static List<Spell> AbyssalSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "gush", School = SpellSchool.Abyssal, DisplayName = "Gush", ShortName = "GUSH",
                Description = "A blast of water that knocks enemies up and away.",
                Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 8f, HealthCost = 6f, Cooldown = 5f, TintOverride = AbyssalTint,
                OnCast =
                {
                    new SelectConeEffect { Range = 9f, HalfAngle = 40f },
                    new DealDamageEffect { Amount = 18f, Knockback = 12f, KnockbackUp = 6f },
                    new VfxConeEffect { Range = 9f, HalfAngle = 40f }
                }
            },

            new Spell
            {
                Id = "ink_spray", School = SpellSchool.Abyssal, DisplayName = "Ink Spray", ShortName = "INK",
                Description = "Blinds everything in a cone. Blinded enemies shoot where they last saw you.",
                Type = SpellType.Control, DamageType = DamageType.Necrotic, Rarity = Rarity.Common,
                ManaCost = 8f, HealthCost = 5f, Cooldown = 10f, TintOverride = new Color(0.15f, 0.15f, 0.2f),
                OnCast =
                {
                    new SelectConeEffect { Range = 10f, HalfAngle = 40f },
                    new StatusPayloadEffect { Status = StatusId.Blind, Duration = 4f, DurationPerLevel = 0.5f, Stacks = 1, Magnitude = 1f },
                    new ApplyPayloadEffect(),
                    new VfxConeEffect { Range = 10f, HalfAngle = 40f }
                }
            },

            new Spell
            {
                Id = "depth_grasp", School = SpellSchool.Abyssal, DisplayName = "Depth Grasp", ShortName = "GRSP",
                Description = "Tentacles burst from the ground where you aim, slowing everything there.",
                Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 8f, HealthCost = 5f, Cooldown = 9f, TintOverride = AbyssalTint,
                OnCast =
                {
                    new AimPointEffect { Range = 30f },
                    new SelectSphereEffect { Radius = 4f, AroundPoint = true },
                    new StatusPayloadEffect { Status = StatusId.Snare, Duration = 3f, Stacks = 1, Magnitude = 0.5f },
                    new DealDamageEffect { Amount = 10f },
                    new VfxGroundRingEffect { Radius = 4f }
                }
            },

            new Spell
            {
                Id = "soul_bargain", School = SpellSchool.Abyssal, DisplayName = "Soul Bargain", ShortName = "BRGN",
                Description = "Deal more damage, and take more, for a short while.",
                Type = SpellType.Ward, DamageType = DamageType.Necrotic, Rarity = Rarity.Uncommon,
                ManaCost = 10f, HealthCost = 10f, Cooldown = 18f, TintOverride = new Color(0.7f, 0.15f, 0.25f),
                OnCast =
                {
                    new SelfStatusEffect { Status = StatusId.Empowered, Duration = 8f, DurationPerLevel = 0f, Stacks = 1, Magnitude = 0.35f },
                    new SelfStatusEffect { Status = StatusId.Exposed, Duration = 8f, DurationPerLevel = 0f, Stacks = 1, Magnitude = 0.25f }
                }
            },

            new Spell
            {
                Id = "drown", School = SpellSchool.Abyssal, DisplayName = "Drown", ShortName = "DRWN",
                Description = "Silences and damages the enemy nearest your aim.",
                Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 12f, HealthCost = 8f, Cooldown = 10f, TintOverride = AbyssalTint,
                OnCast =
                {
                    new SelectNearestAimedEffect { Range = 30f, MaxAngle = 12f },
                    new StatusPayloadEffect { Status = StatusId.Silence, Duration = 3f, DurationPerLevel = 0.5f, Stacks = 1, Magnitude = 1f },
                    new DealDamageEffect { Amount = 28f },
                    new VfxLineToTargetsEffect()
                }
            },

            new Spell
            {
                Id = "eye_of_epheraxx", School = SpellSchool.Abyssal, DisplayName = "Eye of E'pheraxx", ShortName = "EYE",
                Description = "An immobile eye that stares a necrotic beam at the nearest enemy.",
                Type = SpellType.Attack, DamageType = DamageType.Necrotic, Rarity = Rarity.Uncommon,
                ManaCost = 15f, HealthCost = 12f, Cooldown = 20f, TintOverride = new Color(0.55f, 0.9f, 0.5f),
                OnCast = { new SummonMinionEffect { MinionId = "eye", Distance = 3f } }
            },

            new Spell
            {
                Id = "leviathan", School = SpellSchool.Abyssal, DisplayName = "Leviathan", ShortName = "LEVI",
                Description = "Frost spreads fast across an area, then a leviathan breaches it, striking everything still inside.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Rare,
                ManaCost = 20f, HealthCost = 15f, Cooldown = 18f, TintOverride = AbyssalTint,
                OnCast =
                {
                    new AimPointEffect { Range = 35f },
                    new StatusPayloadEffect { Status = StatusId.Frost, Duration = 3f, Stacks = 12, Magnitude = FrostStatus.SlowPerStack },
                    new LingeringZoneEffect { Radius = 6f, Duration = 2.5f, DamagePerTick = 0f, TickInterval = 0.25f, ScaleWithLevel = false },
                    new TelegraphCircleEffect { Radius = 6f, Duration = 2.5f, ScaleWithLevel = false },
                    new DelayedBlastEffect { Delay = 2.5f, Radius = 6f, Damage = 70f, Knockback = 8f, ScaleRadiusWithLevel = false }
                }
            },

            new Spell
            {
                Id = "fathomless_gate", School = SpellSchool.Abyssal, DisplayName = "Fathomless Gate", ShortName = "GATE",
                Description = "A whirlpool drags enemies toward its centre while tentacles lash them.",
                Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Rare,
                ManaCost = 20f, HealthCost = 15f, Cooldown = 22f, TintOverride = AbyssalTint,
                OnCast =
                {
                    new AimPointEffect { Range = 35f },
                    new WhirlpoolEffect { Radius = 6f, Seconds = 6f, PullSpeed = 5f, DamagePerTick = 6f }
                }
            },

            new Spell
            {
                Id = "unspeakable_one", School = SpellSchool.Abyssal, DisplayName = "Unspeakable One", ShortName = "KRKN",
                Description = "Summon the kraken. Its tentacles burst up beside enemies and terrify them, and its maw eats anything that comes close.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Mythic,
                ManaCost = 30f, HealthCost = 25f, Cooldown = 60f, TintOverride = AbyssalTint,
                OnCast =
                {
                    new AimPointEffect { Range = 25f },
                    new KrakenEffect { Seconds = 12f, Radius = 14f, TentacleDamage = 24f }
                }
            },

            new Spell
            {
                Id = "bloodwake", School = SpellSchool.Abyssal, DisplayName = "Bloodwake", ShortName = "WAKE",
                Description = "Run faster, paying in health rather than mana, leaving a wake of blood that makes enemies bleed.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Common,
                MaxLevel = 1, TintOverride = new Color(0.75f, 0.1f, 0.15f),
                Sustain = new SustainProfile
                {
                    HealthPerSecond = 5f, MoveSpeedBonus = 0.4f,
                    Trail = new TrailProfile
                    {
                        Radius = 0.9f, SegmentLifetime = 2f, DamagePerTick = 0f, DamageType = DamageType.Kinetic,
                        Tint = new Color(0.6f, 0.05f, 0.1f), Statuses = { StatusLibrary.Bleed(999f, 1, 2f) }
                    }
                }
            },

            new Spell
            {
                Id = "rapture_of_the_deep", School = SpellSchool.Abyssal, DisplayName = "Rapture of the Deep", ShortName = "DEEP",
                Description = "Gravity becomes buoyancy: swim through the air, rising and sinking at will.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Rare,
                MaxLevel = 1, TintOverride = AbyssalTint,
                Sustain = new SustainProfile { ManaPerSecond = 8f, Buoyant = true }
            },

            new Spell
            {
                Id = "tentacle", School = SpellSchool.Abyssal, DisplayName = "Tentacle", ShortName = "TNTC",
                Description = "A tentacle strikes the enemy in front of you, knocking it away.",
                Slot = SpellSlot.Melee, Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 4f, HealthCost = 3f, Cooldown = 1.4f, MaxLevel = 3, TintOverride = AbyssalTint,
                OnCast =
                {
                    new SelectConeEffect { Range = 4f, HalfAngle = 35f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = 20f, Knockback = 16f, CanCrit = true }
                }
            }
        };
    }
}
