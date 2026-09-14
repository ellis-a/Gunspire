using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        private static readonly Color PsionicTint = new Color(0.85f, 0.45f, 0.95f);

        /// <summary>Psionic: the mind as a weapon, and as something to be turned. Mastery: Psi Blades.</summary>
        private static List<Spell> PsionicSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "mind_spike", School = SpellSchool.Psionic, DisplayName = "Mind Spike", ShortName = "SPKE",
                Description = "An instant psychic ray. Spends a psi charge for bonus damage when you have one, but does not need one.",
                Type = SpellType.Attack, DamageType = DamageType.Psychic, Rarity = Rarity.Common,
                ManaCost = 12f, Cooldown = 2.5f, TintOverride = PsionicTint,
                OnCast =
                {
                    new InstantRayEffect { Range = 45f },
                    new DealDamageEffect { Amount = 20f },
                    new PsiBonusEffect { BonusDamage = 18f }
                }
            },

            new Spell
            {
                Id = "phantasmal_mimic", School = SpellSchool.Psionic, DisplayName = "Phantasmal Mimic", ShortName = "MIMC",
                Description = "An immobile illusion of you that shoots with the gun you hold. Its hits charge your psi.",
                Type = SpellType.Attack, DamageType = DamageType.Psychic, Rarity = Rarity.Common,
                ManaCost = 20f, Cooldown = 18f, TintOverride = PsionicTint,
                OnCast = { new SummonMimicEffect() }
            },

            new Spell
            {
                Id = "telekinesis", School = SpellSchool.Psionic, DisplayName = "Telekinesis", ShortName = "TELK",
                Description = "Hurl an enemy away. Anything it slams into hurts it, and is hurt in turn.",
                Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 16f, Cooldown = 6f, TintOverride = PsionicTint,
                OnCast =
                {
                    new InstantRayEffect { Range = 30f, Radius = 0.3f },
                    new DealDamageEffect { Amount = 8f, Knockback = 22f }
                }
            },

            new Spell
            {
                Id = "ego_fracture", School = SpellSchool.Psionic, DisplayName = "Ego Fracture", ShortName = "EGO",
                Description = "A grenade that stuns everything it catches. Elites are stunned for less.",
                Type = SpellType.Control, DamageType = DamageType.Psychic, Rarity = Rarity.Uncommon,
                ManaCost = 24f, Cooldown = 12f, TintOverride = PsionicTint,
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f,
                        OnHit =
                        {
                            new SelectSphereEffect { Radius = 4f, AroundPoint = true },
                            new StatusPayloadEffect { Status = StatusId.Snare, Duration = 1.5f, Stacks = 1, Magnitude = SnareStatus.StunMagnitude },
                            new ApplyPayloadEffect(),
                            new VfxSphereEffect { Diameter = 2f, Alpha = 0.35f, Lifetime = 0.3f, GrowPerSecond = 8f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "brain_fog", School = SpellSchool.Psionic, DisplayName = "Brain Fog", ShortName = "FOG",
                Description = "A bolt that confuses the enemy it hits: it cannot tell friend from foe until something strikes it.",
                Type = SpellType.Control, DamageType = DamageType.Psychic, Rarity = Rarity.Uncommon,
                ManaCost = 20f, Cooldown = 10f, NoiseMultiplier = 0.4f, TintOverride = PsionicTint,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Confusion, Duration = 5f, DurationPerLevel = 1f, Stacks = 1, Magnitude = 1f },
                    new SpawnProjectileEffect { Damage = 0f, Speed = 40f, Radius = 0.2f, Lifetime = 3f }
                }
            },

            new Spell
            {
                Id = "intrusive_thoughts", School = SpellSchool.Psionic, DisplayName = "Intrusive Thoughts", ShortName = "THGT",
                Description = "A bolt that torments its target with psychic damage over time.",
                Type = SpellType.Attack, DamageType = DamageType.Psychic, Rarity = Rarity.Uncommon,
                ManaCost = 20f, Cooldown = 7f, TintOverride = PsionicTint,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Torment, Duration = 6f, Stacks = 1, Magnitude = 8f },
                    new SpawnProjectileEffect { Damage = 6f, Speed = 45f, Radius = 0.18f, Lifetime = 3f }
                }
            },

            new Spell
            {
                Id = "superego_death", School = SpellSchool.Psionic, DisplayName = "Superego Death", ShortName = "SUPE",
                Description = "The first enemy it hits becomes two copies of itself at half health each, silenced and disarmed for a moment. " +
                              "Nothing splits twice. Shooting a split enemy charges extra psi.",
                Type = SpellType.Control, DamageType = DamageType.Psychic, Rarity = Rarity.Rare,
                ManaCost = 36f, Cooldown = 25f, TintOverride = PsionicTint,
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 45f, Radius = 0.22f, Lifetime = 3f,
                        OnHit =
                        {
                            new SelectSphereEffect { Radius = 1.2f, AroundPoint = true },
                            new SplitEnemyEffect()
                        }
                    }
                }
            },

            new Spell
            {
                Id = "superid", School = SpellSchool.Psionic, DisplayName = "Superid", ShortName = "ID",
                Description = "Become id incarnate: your gun fires on its own at the enemy nearest your aim and never misses or reloads, " +
                              "while you move faster and take less damage. You can still melee.",
                Type = SpellType.Ward, DamageType = DamageType.Psychic, Rarity = Rarity.Rare,
                ManaCost = 36f, Cooldown = 30f, NeverEchoes = true, TintOverride = PsionicTint,
                OnCast = { new SelfStatusEffect { Status = StatusId.Unleashed, Duration = 8f, DurationPerLevel = 1f, Stacks = 1, Magnitude = 1f } }
            },

            new Spell
            {
                Id = "assume_identity", School = SpellSchool.Psionic, DisplayName = "Assume Identity", ShortName = "ASUM",
                Description = "Take control of the enemy nearest your aim for 10 seconds, or until it takes damage. Your body is " +
                              "hidden and immune meanwhile. Not while it is the last enemy in the room.",
                Type = SpellType.Control, DamageType = DamageType.Psychic, Rarity = Rarity.Mythic,
                ManaCost = 50f, Cooldown = 45f, NeverEchoes = true, TintOverride = PsionicTint,
                OnCast =
                {
                    new SelectNearestAimedEffect { Range = 40f, MaxAngle = 10f },
                    new PossessEffect { Seconds = 10f }
                }
            },

            new Spell
            {
                Id = "repulse", School = SpellSchool.Psionic, DisplayName = "Repulse", ShortName = "RPLS",
                Description = "Dash the way you came, turning every force on you around. Spends a dash charge.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Common,
                ManaCost = 8f, Cooldown = 0.5f, MaxLevel = 1, TintOverride = PsionicTint,
                OnCast = { new RepulseEffect() }
            },

            new Spell
            {
                Id = "force_of_will", School = SpellSchool.Psionic, DisplayName = "Force of Will", ShortName = "WILL",
                Description = "A dash. With a psi charge, it spends one to throw every nearby enemy projectile back the way it came.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Rare,
                ManaCost = 8f, Cooldown = 0f, MaxLevel = 1, TintOverride = PsionicTint,
                OnCast =
                {
                    new DashEffect(),
                    new ReflectProjectilesEffect { Radius = 5f }
                }
            },

            new Spell
            {
                Id = "slice", School = SpellSchool.Psionic, DisplayName = "Slice", ShortName = "SLCE",
                Description = "A quick vertical strike with long reach.",
                Slot = SpellSlot.Melee, Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 2f, Cooldown = 0.35f, MaxLevel = 3, TintOverride = PsionicTint,
                OnCast =
                {
                    new SelectBoxEffect { Range = 4.5f, Width = 0.9f, Height = 3f },
                    new DealDamageEffect { Amount = 9f, CanCrit = true }
                }
            }
        };
    }
}
