using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        private static readonly Color DeathTint = new Color(0.55f, 0.95f, 0.75f);

        /// <summary>Death: zombies, skeletons and spirits, raised and spent. Many casts cost souls. Mastery: Souls.</summary>
        private static List<Spell> DeathSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "raise_dead", School = SpellSchool.Death, DisplayName = "Raise Dead", ShortName = "RAIS",
                Description = "Pull a long-dead body from the ground and bind a soul to it: a slow zombie that follows you between floors.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 10f, SoulCost = 1, Cooldown = 3f, TintOverride = DeathTint,
                OnCast = { new SummonMinionEffect { MinionId = "zombie", Distance = 2.5f } }
            },

            new Spell
            {
                Id = "wither", School = SpellSchool.Death, DisplayName = "Wither", ShortName = "WTHR",
                Description = "A grenade that weakens what it hits, and kills anything that falls below 10% health, elites included. " +
                              "Each level raises the threshold by a point.",
                Type = SpellType.Control, DamageType = DamageType.Necrotic, Rarity = Rarity.Common,
                ManaCost = 14f, SoulCost = 1, Cooldown = 8f, TintOverride = new Color(0.4f, 0.45f, 0.3f),
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 0f, Speed = 22f, Gravity = 14f, Radius = 0.25f, Lifetime = 4f,
                        OnHit =
                        {
                            new SelectSphereEffect { Radius = 3.5f, AroundPoint = true },
                            new StatusPayloadEffect { Status = StatusId.Weaken, Duration = 8f, Stacks = 1, Magnitude = 0.15f },
                            new StatusPayloadEffect { Status = StatusId.Withered, Duration = 8f, Stacks = 1, Magnitude = 0.1f, MagnitudePerLevel = 0.01f },
                            new ApplyPayloadEffect(),
                            new VfxSphereEffect { Diameter = 2f, Alpha = 0.35f, Lifetime = 0.3f, GrowPerSecond = 7f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "bone_shards", School = SpellSchool.Death, DisplayName = "Bone Shards", ShortName = "BONE",
                Description = "Hold to charge, release to fire a flurry of bone. More bones the longer you charge, and more " +
                              "for every soul, which it spends all of but does not need.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 16f, SpendsAllSouls = true, Cooldown = 5f, TintOverride = new Color(0.95f, 0.92f, 0.85f),
                Charge = new ChargeProfile { SecondsToFull = 1.5f },
                OnCast =
                {
                    new SpawnProjectileEffect
                    {
                        Damage = 9f, Speed = 55f, Radius = 0.1f, Lifetime = 2f, Count = 3,
                        ExtraCountAtFullCharge = 6, ExtraCountPerSoul = 2, SpreadDegrees = 8f
                    }
                }
            },

            new Spell
            {
                Id = "banshee_wail", School = SpellSchool.Death, DisplayName = "Banshee Wail", ShortName = "WAIL",
                Description = "A wailing cone that silences, freezes and deals necrotic damage.",
                Type = SpellType.Control, DamageType = DamageType.Necrotic, Rarity = Rarity.Uncommon,
                ManaCost = 18f, SoulCost = 1, Cooldown = 10f, TintOverride = DeathTint,
                OnCast =
                {
                    new SelectConeEffect { Range = 10f, HalfAngle = 40f },
                    new StatusPayloadEffect { Status = StatusId.Silence, Duration = 3f, Stacks = 1, Magnitude = 1f },
                    new StatusPayloadEffect { Status = StatusId.Frost, Duration = 4f, Stacks = 15, Magnitude = FrostStatus.SlowPerStack },
                    new DealDamageEffect { Amount = 22f },
                    new VfxConeEffect { Range = 10f, HalfAngle = 40f }
                }
            },

            new Spell
            {
                Id = "corpse_explosion", School = SpellSchool.Death, DisplayName = "Corpse Explosion", ShortName = "CRPS",
                Description = "A soul bounces between fresh corpses, exploding each one.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 20f, SoulCost = 1, Cooldown = 8f, TintOverride = DeathTint,
                OnCast = { new ChainCorpsesEffect() }
            },

            new Spell
            {
                Id = "desecrate", School = SpellSchool.Death, DisplayName = "Desecrate", ShortName = "DSCR",
                Description = "Defile the ground. Your bullets deal extra necrotic damage while you stand in it, and against enemies standing in it.",
                Type = SpellType.Ward, DamageType = DamageType.Necrotic, Rarity = Rarity.Uncommon,
                ManaCost = 24f, Cooldown = 18f, TintOverride = new Color(0.35f, 0.5f, 0.3f),
                OnCast = { new DesecrateEffect() }
            },

            new Spell
            {
                Id = "stitched_monstrosity", School = SpellSchool.Death, DisplayName = "Stitched Monstrosity", ShortName = "STCH",
                Description = "Summon a fast zombie goliath whose slam knocks enemies away. One at a time; it follows you between floors.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Rare,
                ManaCost = 30f, SoulCost = 2, Cooldown = 10f, TintOverride = DeathTint,
                OnCast = { new SummonMinionEffect { MinionId = "monstrosity", OneAtATime = true, Distance = 3f } }
            },

            new Spell
            {
                Id = "soul_storm", School = SpellSchool.Death, DisplayName = "Soul Storm", ShortName = "SSTM",
                Description = "Souls rain around you as you move. Every kill while it lasts extends it, with no limit.",
                Type = SpellType.Attack, DamageType = DamageType.Necrotic, Rarity = Rarity.Rare,
                ManaCost = 30f, SoulCost = 2, Cooldown = 30f, TintOverride = DeathTint,
                OnCast = { new SoulStormEffect() }
            },

            new Spell
            {
                Id = "apocalypse", School = SpellSchool.Death, DisplayName = "Apocalypse", ShortName = "APOC",
                Description = "A cone of poison, fear and plague. A plagued enemy that dies rises as your zombie and passes the plague on.",
                Type = SpellType.Control, DamageType = DamageType.Necrotic, Rarity = Rarity.Mythic,
                ManaCost = 55f, Cooldown = 45f, TintOverride = new Color(0.5f, 0.65f, 0.25f),
                OnCast =
                {
                    new SelectConeEffect { Range = 12f, HalfAngle = 40f },
                    new StatusPayloadEffect { Status = StatusId.Poison, Duration = 7f, Stacks = 3, Magnitude = 1.4f },
                    new StatusPayloadEffect { Status = StatusId.Fear, Duration = 3f, Stacks = 1, Magnitude = 1f },
                    new StatusPayloadEffect { Status = StatusId.Plague, Duration = 12f, Stacks = 1, Magnitude = 1f },
                    new ApplyPayloadEffect(),
                    new VfxConeEffect { Range = 12f, HalfAngle = 40f }
                }
            },

            new Spell
            {
                Id = "gravewalk", School = SpellSchool.Death, DisplayName = "Gravewalk", ShortName = "GRVW",
                Description = "A quick lunge as a shade, passing through enemies and weakening each one.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Common,
                ManaCost = 10f, Cooldown = 4f, MaxLevel = 1, TintOverride = DeathTint,
                OnCast = { new GravewalkEffect() }
            },

            new Spell
            {
                Id = "lich_guise", School = SpellSchool.Death, DisplayName = "Lich Guise", ShortName = "LICH",
                Description = "Swap places with the zombie nearest your aim, however far. It stands where you were.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Rare,
                ManaCost = 18f, Cooldown = 8f, MaxLevel = 1, TintOverride = DeathTint,
                OnCast = { new SwapWithMinionEffect() }
            },

            new Spell
            {
                Id = "gravebite", School = SpellSchool.Death, DisplayName = "Gravebite", ShortName = "BITE",
                Description = "A jaw bursts from your hand and bites the enemy in front of you, weakening it.",
                Slot = SpellSlot.Melee, Type = SpellType.Attack, DamageType = DamageType.Necrotic, Rarity = Rarity.Uncommon,
                ManaCost = 5f, Cooldown = 1.1f, MaxLevel = 3, TintOverride = DeathTint,
                OnCast =
                {
                    new SelectConeEffect { Range = 3f, HalfAngle = 45f, RequireLineOfSight = false },
                    new StatusPayloadEffect { Status = StatusId.Weaken, Duration = 5f, Stacks = 1, Magnitude = 0.15f },
                    new DealDamageEffect { Amount = 18f, CanCrit = true }
                }
            }
        };
    }
}
