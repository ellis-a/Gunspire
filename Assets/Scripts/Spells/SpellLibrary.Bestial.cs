using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    public static partial class SpellLibrary
    {
        private static readonly Color BestialTint = new Color(0.75f, 0.55f, 0.3f);

        /// <summary>Bestial: furred things fighting beside you. Mastery: the companion ladder.</summary>
        private static List<Spell> BestialSpells() => new List<Spell>
        {
            new Spell
            {
                Id = "murder", School = SpellSchool.Bestial, DisplayName = "Murder", ShortName = "MRDR",
                Description = "A murder of crows attacks enemies in a zone. You and your companion move faster inside it.",
                Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 18f, Cooldown = 12f, TintOverride = new Color(0.3f, 0.3f, 0.35f),
                OnCast =
                {
                    new AimPointEffect { Range = 30f },
                    new LingeringZoneEffect
                    {
                        Radius = 5f, Duration = 8f, DamagePerTick = 5f, TickInterval = 0.5f, ScaleWithLevel = false,
                        AllyStatuses = { new StatusApplication(StatusId.Haste, 1f, 1, 0.25f) }
                    }
                }
            },

            new Spell
            {
                Id = "howl", School = SpellSchool.Bestial, DisplayName = "Howl", ShortName = "HOWL",
                Description = "You shoot and reload faster, and your companion attacks faster.",
                Type = SpellType.Ward, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 16f, Cooldown = 14f, TintOverride = BestialTint,
                OnCast =
                {
                    new SelfStatusEffect { Status = StatusId.Quickened, Duration = 8f, DurationPerLevel = 1f, Stacks = 1, Magnitude = 0.3f },
                    new CompanionStatusEffect { Status = StatusId.Quickened, Duration = 8f, Magnitude = 0.3f }
                }
            },

            new Spell
            {
                Id = "blood_scent", School = SpellSchool.Bestial, DisplayName = "Blood Scent", ShortName = "SCNT",
                Description = "Sense every enemy through walls for a while.",
                Type = SpellType.Ward, DamageType = DamageType.Kinetic, Rarity = Rarity.Common,
                ManaCost = 12f, Cooldown = 16f, NoiseMultiplier = 0.2f, TintOverride = new Color(0.85f, 0.25f, 0.25f),
                OnCast = { new SelfStatusEffect { Status = StatusId.Scenting, Duration = 10f, DurationPerLevel = 2f, Stacks = 1, Magnitude = 1f } }
            },

            new Spell
            {
                Id = "fang_and_claw", School = SpellSchool.Bestial, DisplayName = "Fang and Claw", ShortName = "FANG",
                Description = "A bleeding arc in front of you, and again from your companion's position.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 20f, Cooldown = 5f, TintOverride = BestialTint,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Bleed, Duration = 999f, Stacks = 1, Magnitude = 4f },
                    new SelectConeEffect { Range = 3.4f, HalfAngle = 60f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = 20f },
                    new VfxConeEffect { Range = 3.4f, HalfAngle = 60f },
                    new FromCompanionEffect
                    {
                        Body =
                        {
                            new SelectConeEffect { Range = 3.4f, HalfAngle = 60f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 20f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "embiggen", School = SpellSchool.Bestial, DisplayName = "Embiggen", ShortName = "BIGN",
                Description = "You and your companion grow larger and hit harder for a moment, each shrugging off a debuff.",
                Type = SpellType.Ward, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 22f, Cooldown = 18f, TintOverride = BestialTint,
                OnCast =
                {
                    new RemoveRandomDebuffEffect { FromCompanion = true },
                    new SelfStatusEffect { Status = StatusId.Enlarged, Duration = 6f, DurationPerLevel = 0f, Stacks = 1, Magnitude = 0.3f },
                    new CompanionStatusEffect { Status = StatusId.Enlarged, Duration = 6f, Magnitude = 0.3f }
                }
            },

            new Spell
            {
                Id = "lunge", School = SpellSchool.Bestial, DisplayName = "Lunge", ShortName = "LNGE",
                Description = "Leap forward and strike whatever you land beside.",
                Type = SpellType.Attack, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 18f, Cooldown = 6f, TintOverride = BestialTint,
                OnCast =
                {
                    new ImpulseSelfEffect { Speed = 18f },
                    new DelayedEffect
                    {
                        Seconds = 0.2f,
                        Body =
                        {
                            new OriginFromCasterEffect(),
                            new SelectConeEffect { Range = 3f, HalfAngle = 60f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 30f }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "vipers_sting", School = SpellSchool.Bestial, DisplayName = "Viper's Sting", ShortName = "VIPR",
                Description = "For a while your bullets and your companion's attacks poison what they hit.",
                Type = SpellType.Ward, DamageType = DamageType.Necrotic, Rarity = Rarity.Rare,
                ManaCost = 30f, Cooldown = 20f, TintOverride = Palette.Poison,
                OnCast =
                {
                    new InfuseBulletsEffect
                    {
                        InfusionId = "vipers_sting", Seconds = 10f, IncludeCompanion = true,
                        Statuses = { StatusLibrary.Poison(6f, 2, 1.4f) }
                    }
                }
            },

            new Spell
            {
                Id = "hunters_mark", School = SpellSchool.Bestial, DisplayName = "Hunter's Mark", ShortName = "MARK",
                Description = "A dart that death-marks the enemy it hits. The next hit finishes it; elites take a heavy blow instead.",
                Type = SpellType.Control, DamageType = DamageType.Energy, Rarity = Rarity.Rare,
                ManaCost = 20f, Cooldown = 30f, NoiseMultiplier = 0.4f, TintOverride = new Color(0.95f, 0.1f, 0.35f),
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Deathmark, Duration = 3f, Stacks = 1, Magnitude = 1f },
                    new SpawnProjectileEffect { Damage = 0f, Speed = 70f, Radius = 0.12f, Lifetime = 2.5f }
                }
            },

            Shapeshift("shapeshift_cobra", "Shapeshift: Death Cobra", "COBR", AnimalKind.DeathCobra,
                "Become a death cobra: spit venom, or leap forward and bite. Cast again to return."),
            Shapeshift("shapeshift_stag", "Shapeshift: Alpha Stag", "STAG", AnimalKind.AlphaStag,
                "Become an alpha stag: hold to charge unstoppably, or bash with your hooves. Cast again to return."),
            Shapeshift("shapeshift_lizard", "Shapeshift: Tyrant Lizard", "TYRA", AnimalKind.TyrantLizard,
                "Become a tyrant lizard: bite, or swipe. Cast again to return."),

            new Spell
            {
                Id = "bound", School = SpellSchool.Bestial, DisplayName = "Bound", ShortName = "BOND",
                Description = "Two quick hops the way you are moving, the second steerable in the air. Standing still, both go straight up.",
                Slot = SpellSlot.Movement, Type = SpellType.Mobility, Rarity = Rarity.Common,
                ManaCost = 8f, Cooldown = 3.5f, MaxLevel = 1, TintOverride = BestialTint,
                OnCast =
                {
                    new ImpulseSequenceEffect
                    {
                        Steps =
                        {
                            new ImpulseStep { Delay = 0f, Duration = 0.05f, ForwardImpulse = 9f, UpImpulse = 7f, ForwardFromInput = true, SuppressFriction = true },
                            new ImpulseStep { Delay = 0.35f, ForwardImpulse = 9f, UpImpulse = 7f, ForwardFromInput = true }
                        }
                    }
                }
            },

            new Spell
            {
                Id = "maul", School = SpellSchool.Bestial, DisplayName = "Maul", ShortName = "MAUL",
                Description = "A heavy blow that stuns. Elites are stunned for less.",
                Slot = SpellSlot.Melee, Type = SpellType.Control, DamageType = DamageType.Kinetic, Rarity = Rarity.Uncommon,
                ManaCost = 6f, Cooldown = 2.5f, MaxLevel = 3, TintOverride = BestialTint,
                OnCast =
                {
                    new StatusPayloadEffect { Status = StatusId.Snare, Duration = 1.2f, Stacks = 1, Magnitude = SnareStatus.StunMagnitude },
                    new SelectConeEffect { Range = 3.2f, HalfAngle = 50f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = 18f, CanCrit = true }
                }
            }
        };

        /// <summary>
        /// Shapeshift is three spells sharing a variant group, the cheapest version of choosing a form when you
        /// take it: the offer shows them as separate cards, and only one can be equipped.
        /// </summary>
        private static Spell Shapeshift(string id, string name, string shortName, AnimalKind form, string description) => new Spell
        {
            Id = id, School = SpellSchool.Bestial, DisplayName = name, ShortName = shortName, Description = description,
            Type = SpellType.Mobility, DamageType = DamageType.Kinetic, Rarity = Rarity.Mythic,
            ManaCost = 40f, Cooldown = 20f, MaxLevel = 1, VariantGroup = "shapeshift", TintOverride = BestialTint,
            OnCast = { new ShapeshiftEffect { Form = form } }
        };
    }
}
