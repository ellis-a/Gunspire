using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        /// <summary>
        /// Aetherics: reality and time, taking things out of the normal flow and putting them back somewhere or
        /// somewhen else. Its spells cost more mana than other schools', which is what feeds Arcane Warp.
        /// Blink, its common movement spell, lives with the older spells in SpellLibrary.cs.
        /// </summary>
        private static List<Spell> AethericsSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "banish", School = SpellSchool.Aetherics, DisplayName = "Banish", ShortName = "BNSH",
                Description = "A grenade that removes what it catches from reality, sharing 12 seconds between them. Elites resist half.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Common,
                ManaCost = 30f, Cooldown = 14f, TintOverride = Palette.Arcane,
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f,
                        OnHit =
                        {
                            new SelectSphereEffect { Radius = 3.5f, AroundPoint = true },
                            new BanishEffect { TotalSeconds = 12f, EliteResistance = 0.5f },
                            new VfxSphereEffect { Diameter = 2f, Alpha = 0.4f, Lifetime = 0.3f, GrowPerSecond = 8f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "reality_shards", School = SpellSchool.Aetherics, DisplayName = "Reality Shards", ShortName = "SHRD",
                Description = "Spend all your mana to start growing spheres of warped reality that follow you and fire at enemies. " +
                              "Casting again replaces them.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Common,
                ManaCost = 30f, Cooldown = 12f, TintOverride = Palette.Arcane,
                OnCast = { new RealityShardsEffect() }
            },

            new Spell
            {
                Id = "nether_wall", School = SpellSchool.Aetherics, DisplayName = "Nether Wall", ShortName = "WALL",
                Description = "A wall of nether across your path that stops shots from both sides. Anyone can walk through it.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Common,
                ManaCost = 26f, Cooldown = 14f, TintOverride = Palette.Arcane,
                OnCast = { new NetherWallEffect() }
            },

            new Spell
            {
                Id = "invisibility", School = SpellSchool.Aetherics, DisplayName = "Invisibility", ShortName = "INVS",
                Description = "A toggle: enemies cannot see you while it drains your mana. Shooting, casting or striking breaks it; footsteps are still heard.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                Cooldown = 4f, NoiseMultiplier = 0.1f, TintOverride = Palette.Arcane,
                Sustain = new SustainProfile
                {
                    ManaPerSecond = 12f, HideFromSight = true, BreakOnShoot = true, BreakOnCast = true, BreakOnMelee = true
                }
            },

            new Spell
            {
                Id = "collapse_space", School = SpellSchool.Aetherics, DisplayName = "Collapse Space", ShortName = "CLPS",
                Description = "A grenade that deals energy damage and drags enemies into its centre.",
                Type = SpellType.Attack, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 34f, Cooldown = 12f, TintOverride = Palette.Arcane,
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f,
                        OnHit =
                        {
                            new SelectSphereEffect { Radius = 5f, AroundPoint = true },
                            new DealDamageEffect { Amount = 22f, FalloffFromPoint = true, FalloffRadius = 5f },
                            new PullTowardPointEffect { Speed = 14f },
                            new VfxSphereEffect { Diameter = 3f, Alpha = 0.4f, Lifetime = 0.3f, GrowPerSecond = -6f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "flicker", School = SpellSchool.Aetherics, DisplayName = "Flicker", ShortName = "FLKR",
                Description = "Vanish from reality for a moment: untouchable and unseen, slow, unable to shoot. Removes a random debuff.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Uncommon,
                ManaCost = 24f, Cooldown = 10f, NoiseMultiplier = 0.2f, TintOverride = Palette.Arcane,
                OnCast =
                {
                    new RemoveRandomDebuffEffect(),
                    new SelfStatusEffect { Status = StatusId.Phased, Duration = 0.6f, DurationPerLevel = 0f, Stacks = 1, Magnitude = 1f },
                    new SelfStatusEffect { Status = StatusId.Disarm, Duration = 0.6f, DurationPerLevel = 0f, Stacks = 1, Magnitude = 1f },
                    new SelfStatusEffect { Status = StatusId.Snare, Duration = 0.6f, DurationPerLevel = 0f, Stacks = 1, Magnitude = 0.5f }
                }
            },

            new Spell
            {
                Id = "nether_smoke", School = SpellSchool.Aetherics, DisplayName = "Nether Smoke", ShortName = "SMOK",
                Description = "A grenade of smoke that burns and blinds enemies inside. Any shot of yours into it strikes a random enemy within.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Rare,
                ManaCost = 44f, Cooldown = 20f, TintOverride = new Color(0.3f, 0.25f, 0.4f),
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f,
                        OnHit = { new SmokeCloudEffect() }
                    }
                }
            },

            new Spell
            {
                Id = "echo", School = SpellSchool.Aetherics, DisplayName = "Echo", ShortName = "ECHO",
                Description = "For a short time, every shot, spell and swing happens again a moment later, for no ammo or mana.",
                Type = SpellType.Ward, DamageType = DamageType.Energy, Rarity = Rarity.Rare,
                ManaCost = 40f, Cooldown = 30f, NeverEchoes = true, TintOverride = Palette.Arcane,
                OnCast = { new EchoEffect { Seconds = 6f, Delay = 0.35f } }
            },

            new Spell
            {
                Id = "stop_time", School = SpellSchool.Aetherics, DisplayName = "Stop Time", ShortName = "STOP",
                Description = "Freeze everything but you for 6 seconds. Your shots hang in the air and land together when time resumes.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Mythic,
                ManaCost = 50f, Cooldown = 30f, MaxLevel = 1, NeverEchoes = true, TintOverride = Palette.Arcane,
                OnCast = { new StopTimeEffect { Seconds = 6f } }
            },

            new Spell
            {
                Id = "rewind", School = SpellSchool.Aetherics, DisplayName = "Rewind", ShortName = "RWND",
                Description = "Snap back along your path to where you were three seconds ago, with your health and ammo as they were.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Rare,
                ManaCost = 24f, Cooldown = 8f, MaxLevel = 1, NeverEchoes = true, TintOverride = Palette.Arcane,
                OnCast = { new RewindEffect { Seconds = 0.75f } }
            },

            new Spell
            {
                Id = "space_hammer", School = SpellSchool.Aetherics, DisplayName = "Space Hammer", ShortName = "HAMR",
                Description = "One huge swing of a hammer from the void, with a very long cooldown.",
                Slot = SpellSlot.Melee, Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 12f, Cooldown = 12f, MaxLevel = 3, TintOverride = Palette.Arcane,
                OnCast =
                {
                    new SelectConeEffect { Range = 3.6f, HalfAngle = 60f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = 90f, CanCrit = true }
                }
            }
        };
    }
}
