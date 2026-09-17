using System.Collections.Generic;

namespace Gunspire
{
    // The Slots and School families.
    public static partial class BoonLibrary
    {
        private static void AddSlots()
        {
            const BoonFamily F = BoonFamily.Slots;

            Add(F, "Cast slots", "quiss", "Quiss", "The spell on Q is one level higher.",
                Rarity.Uncommon, 1, Behave(new SlotLevelBehaviour { Slot = 0 }));
            Add(F, "Cast slots", "esarl", "Esarl", "The spell on E is one level higher.",
                Rarity.Uncommon, 1, Behave(new SlotLevelBehaviour { Slot = 1 }));
            Add(F, "Cast slots", "fex", "Fex", "The spell on F is one level higher.",
                Rarity.Uncommon, 1, Behave(new SlotLevelBehaviour { Slot = 2 }));
            Add(F, "Cast slots", "spell_magazine", "Spell Magazine",
                "Firing the last round in a magazine casts the spell on Q for free.",
                Rarity.Mythic, 1, Behave(new SpellMagazineBehaviour { Slot = 0 }));
            Add(F, "Cast slots", "twincast", "Twincast", "Each spell cast has a 25% chance to cast twice.",
                Rarity.Mythic, 1, Behave(new TwincastBehaviour()));

            Add(F, "Movement", "afterimage", "Afterimage",
                    "Your movement spell leaves a decoy behind that enemies attack, and it explodes when destroyed.",
                    Rarity.Mythic, 1, Behave(new AfterimageBehaviour()))
                .Needs(Filled(SpellSlot.Movement));
            Add(F, "Movement", "tailwind", "Tailwind", "Kills reset your movement spell's cooldown.",
                    Rarity.Rare, 1, Behave(new TailwindBehaviour()))
                .Needs(Filled(SpellSlot.Movement));

            Add(F, "Melee", "gorelust", "Gorelust", "Melee damage heals you for a share of the damage dealt.",
                    Rarity.Uncommon, 3, Behave(new GorelustBehaviour()))
                .Needs(Filled(SpellSlot.Melee));
        }

        private static void AddSchools()
        {
            AddElemental();
            AddBestial();
            AddAbyssal();
            AddDivination();
            AddDeath();
            AddPsionic();
            AddAetherics();
        }

        public const int SchoolCommonLevels = 5;
        public const int SchoolUncommonLevels = 3;
        public const int SchoolRareLevels = 2;

        private static Boon School(SpellSchool school, string id, string name, string description, Rarity rarity,
            params BoonEffect[] effects)
        {
            int levels = rarity == Rarity.Common ? SchoolCommonLevels
                : rarity == Rarity.Uncommon ? SchoolUncommonLevels
                : SchoolRareLevels;
            return Add(BoonFamily.School, school.ToString(), id, name, description, rarity, levels, effects)
                .Needs(Holds(school));
        }

        /// <summary>Every school's three shared Commons: damage, cost and cooldown for its own spells.</summary>
        private static void SchoolTemplates(SpellSchool school, string damageId, string damageName,
            string costId, string costName, string cooldownId, string cooldownName, string costNote = "")
        {
            School(school, damageId, damageName, school + " spells deal more damage.", Rarity.Common,
                new ModifySchoolEffect { School = school, Channel = SchoolChannel.Damage, Amount = 0.06f });
            School(school, costId, costName, school + " spells cost less" + costNote + ".", Rarity.Common,
                new ModifySchoolEffect { School = school, Channel = SchoolChannel.Cost, Amount = -0.05f });
            School(school, cooldownId, cooldownName, school + " spells recover faster.", Rarity.Common,
                new ModifySchoolEffect { School = school, Channel = SchoolChannel.Cooldown, Amount = 0.05f });
        }

        private static ModifySpellEffectEffect StatusAmount(SpellSchool school, StatusId status, float amount) =>
            new ModifySpellEffectEffect
            {
                Channel = SpellEffectChannel.StatusAmount, School = school,
                Statuses = new List<StatusId> { status }, Amount = amount
            };

        private static void AddElemental()
        {
            const SpellSchool S = SpellSchool.Elemental;
            SchoolTemplates(S, "kindling", "Kindling", "conduit", "Conduit", "squall", "Squall", " mana");
            School(S, "stoked", "Stoked", "Elemental spells apply more burn.", Rarity.Common,
                StatusAmount(S, StatusId.Burn, 0.1f));
            School(S, "deep_freeze", "Deep Freeze", "Elemental spells apply more frost stacks.", Rarity.Common,
                StatusAmount(S, StatusId.Frost, 0.1f));
            School(S, "overcharge", "Overcharge", "Elemental spells apply more shock stacks.", Rarity.Common,
                StatusAmount(S, StatusId.Shock, 0.1f));
            School(S, "tri_attuned", "Tri-Attuned",
                "Elemental spells deal more damage for each different element already on the target.",
                Rarity.Uncommon, Behave(new TriAttunedBehaviour()));
            School(S, "excess_force", "Excess Force",
                "When an enemy dies with burn, frost or shock still on it, all of it jumps to a nearby enemy. Each level extends the jump's range.",
                Rarity.Uncommon, Behave(new ExcessForceBehaviour()));
            School(S, "fusion", "Fusion",
                "A Conflux reaction no longer removes the elements that caused it, so the same enemy can react again. The second level shortens the reaction cooldown.",
                Rarity.Rare, Mastery(MasterySetting.ConfluxKeepsElements, 1f),
                Mastery(MasterySetting.ConfluxCooldown, 0.3f, fromLevel: 2));
            School(S, "imbued_rounds", "Imbued Rounds",
                "Your gun hits apply the element of the last Elemental spell you cast, so guns can set off reactions.",
                Rarity.Rare, Behave(new ImbuedRoundsBehaviour()));
        }

        private static void AddBestial()
        {
            const SpellSchool S = SpellSchool.Bestial;
            SchoolTemplates(S, "savagery", "Savagery", "lean_hunt", "Lean Hunt", "wild_pace", "Wild Pace", " mana");
            School(S, "well_fed", "Well Fed", "Your companion has more health.", Rarity.Common,
                new ModifyAlliesEffect { CompanionHealth = 0.1f });
            School(S, "sharp_teeth", "Sharp Teeth", "Your companion deals more damage.", Rarity.Common,
                new ModifyAlliesEffect { CompanionDamage = 0.08f });
            School(S, "fleet_paws", "Fleet Paws", "Your companion moves faster.", Rarity.Common,
                new ModifyAlliesEffect { CompanionMoveSpeed = 0.06f });
            School(S, "pack_tactics", "Pack Tactics", "You deal more damage to the enemy your companion is attacking.",
                Rarity.Uncommon, Behave(new PackTacticsBehaviour()));
            School(S, "vengeful_rage", "Vengeful Rage",
                "When your companion dies, you deal 100% more damage per level for 10 seconds.",
                Rarity.Uncommon, Behave(new VengefulRageBehaviour()));
            School(S, "shared_instinct", "Shared Instinct", "Your companion's attacks carry your gun enchantments.",
                Rarity.Rare, Behave(new SharedInstinctBehaviour()));
            School(S, "blooded", "Blooded", "Kills by your companion shorten your Bestial cooldowns.",
                Rarity.Rare, Behave(new BloodedBehaviour()));
        }

        private static void AddAbyssal()
        {
            const SpellSchool S = SpellSchool.Abyssal;
            SchoolTemplates(S, "crushing_depths", "Crushing Depths", "shallow_wounds", "Shallow Wounds",
                "riptide", "Riptide", " health");
            HealthBoon(School(S, "thick_blood", "Thick Blood", "More maximum health.", Rarity.Common,
                Flat(Attr.MaxHealth, 10f)));
            School(S, "low_interest", "Low Interest", "The Blood Debt's interest changes.", Rarity.Common,
                Mastery(MasterySetting.DebtInterest, 0.1f));
            School(S, "deep_pockets", "Deep Pockets", "Kills repay more debt.", Rarity.Common,
                Mastery(MasterySetting.DebtRepay, 4f));
            School(S, "riding_the_current", "Riding the Current", "Move faster the deeper you are in debt.",
                Rarity.Uncommon, Behave(new RidingTheCurrentBehaviour()));
            School(S, "hemorrhage", "Hemorrhage", "Abyssal spells apply bleed.", Rarity.Uncommon,
                Behave(new SchoolHitStatusBehaviour { School = S, Status = StatusId.Bleed, Base = 3f, PerLevel = 1.5f }));
            School(S, "foreclosure", "Foreclosure",
                "Paying your debt off to zero makes the killing blow explode, dealing damage equal to what you repaid.",
                Rarity.Rare, Behave(new ForeclosureBehaviour()));
            School(S, "tidal_surge", "Tidal Surge", "Repaying debt sends out a wave around you that knocks enemies back.",
                Rarity.Rare, Behave(new TidalSurgeBehaviour()));
        }

        private static void AddDivination()
        {
            const SpellSchool S = SpellSchool.Divination;
            SchoolTemplates(S, "zealotry", "Zealotry", "grace", "Grace", "devotion", "Devotion", " mana");
            School(S, "farsight", "Farsight", "Divine Knowledge reaches further.", Rarity.Common,
                Mastery(MasterySetting.KnowledgeRange, 0.1f));
            School(S, "lingering_light", "Lingering Light",
                "Divination zones and buffs last longer, such as Consecrate, Foretell and Path of Light.", Rarity.Common,
                new ModifySpellEffectEffect { Channel = SpellEffectChannel.StatusDuration, School = S, Amount = 0.1f },
                new ModifySpellEffectEffect { Channel = SpellEffectChannel.ZoneDuration, School = S, Amount = 0.1f });
            School(S, "clear_sight", "Clear Sight", "Enemies' attack timers show earlier.", Rarity.Common,
                Mastery(MasterySetting.KnowledgeWarning, 0.2f));
            School(S, "guided_shots", "Guided Shots",
                "Headshots on an enemy you hit with a Divination spell in the last few seconds deal more damage.",
                Rarity.Uncommon, Behave(new GuidedShotsBehaviour()));
            School(S, "blinding_light", "Blinding Light", "Divination spells blind what they hit.", Rarity.Uncommon,
                Behave(new SchoolHitStatusBehaviour
                {
                    School = S, Status = StatusId.Blind, Cooldown = 6f, Base = 1.5f, PerLevel = 0.5f
                }));
            School(S, "perfect_timing", "Perfect Timing",
                "Using your movement spell just before an enemy attack lands refunds it and blinds the attacker.",
                Rarity.Rare, Behave(new PerfectTimingBehaviour()));
            School(S, "prophecy", "Prophecy", "Each enemy's first attack on you each floor misses.",
                Rarity.Rare, Behave(new ProphecyBehaviour()));
        }

        private static void AddDeath()
        {
            const SpellSchool S = SpellSchool.Death;
            SchoolTemplates(S, "grave_rites", "Grave Rites", "paupers_grave", "Pauper's Grave",
                "restless_dead", "Restless Dead", " mana. Soul costs are unchanged");
            School(S, "soul_jar", "Soul Jar", "+1 soul cap per level.", Rarity.Common,
                Mastery(MasterySetting.SoulCap, 1f));
            School(S, "bone_density", "Bone Density",
                "Your undead have more health: zombies, the monstrosity and plague zombies.", Rarity.Common,
                new ModifyAlliesEffect { UndeadHealth = 0.1f });
            School(S, "grave_hunger", "Grave Hunger", "Kills have a chance to leave an extra soul.", Rarity.Common,
                Behave(new GraveHungerBehaviour()));
            School(S, "enfeeble", "Enfeeble", "Death spells weaken what they hit.", Rarity.Uncommon,
                Behave(new SchoolHitStatusBehaviour { School = S, Status = StatusId.Weaken, Base = 0.1f, PerLevel = 0.05f }));
            School(S, "soul_slave", "Soul Slave",
                    "Spending a soul summons a short-lived ghost that flies at an enemy and explodes, dealing necrotic damage around it.",
                    Rarity.Uncommon, Behave(new SoulSlaveBehaviour()))
                .Tagged(BoonTags.Summon);
            School(S, "soul_shield", "Soul Shield",
                "Once per floor, a killing blow consumes a soul instead, if you have one. The blow is cancelled entirely.",
                Rarity.Rare, Behave(new SoulShieldBehaviour()));
            School(S, "overflowing_souls", "Overflowing Souls",
                "A soul gained while you are at the soul cap explodes where the enemy died.",
                Rarity.Rare, Behave(new OverflowingSoulsBehaviour()));
        }

        private static void AddPsionic()
        {
            const SpellSchool S = SpellSchool.Psionic;
            SchoolTemplates(S, "psychic_pressure", "Psychic Pressure", "lucid_mind", "Lucid Mind",
                "racing_thoughts", "Racing Thoughts", " mana");
            School(S, "keen_mind", "Keen Mind", "Gun hits build more psi.", Rarity.Common,
                Mastery(MasterySetting.PsiCharge, 0.1f));
            School(S, "sharpened_will", "Sharpened Will", "Empowered melee deals more damage.", Rarity.Common,
                Mastery(MasterySetting.PsiMeleeDamage, 3f));
            School(S, "lingering_doubt", "Lingering Doubt", "Your confusion, fear and torment last longer.", Rarity.Common,
                new ModifySpellEffectEffect
                {
                    Channel = SpellEffectChannel.StatusDuration, AnySchool = true, Amount = 0.1f,
                    Statuses = new List<StatusId> { StatusId.Confusion, StatusId.Fear, StatusId.Torment }
                });
            School(S, "overflow", "Overflow", "Spending psi makes your guns deal more damage for a few seconds.",
                Rarity.Uncommon, Behave(new OverflowBehaviour()));
            School(S, "fractured_mind", "Fractured Mind", "Confused enemies take more damage from you.",
                Rarity.Uncommon, Behave(new FracturedMindBehaviour()));
            School(S, "psychic_wave", "Psychic Wave",
                "Empowered melee also sends a blade wave forward that damages everything in its path.",
                Rarity.Rare, Behave(new PsychicWaveBehaviour()));
            School(S, "thrown_blade", "Thrown Blade",
                "Melee with full charges throws a psi blade instead, spending every charge on one ranged hit.",
                Rarity.Rare, Behave(new ThrownBladeBehaviour()));
        }

        private static void AddAetherics()
        {
            const SpellSchool S = SpellSchool.Aetherics;
            SchoolTemplates(S, "void_edge", "Void Edge", "folded_mana", "Folded Mana",
                "borrowed_seconds", "Borrowed Seconds", " mana");
            School(S, "wide_void", "Wide Void", "Arcane Warp grants more power from missing mana.", Rarity.Common,
                Mastery(MasterySetting.WarpRate, 0.1f));
            School(S, "aether_tap", "Aether Tap", "Gun hits restore more mana.", Rarity.Common,
                Mastery(MasterySetting.WarpManaPerHit, 0.2f));
            School(S, "deep_reservoir", "Deep Reservoir", "More maximum mana.", Rarity.Common,
                Flat(Attr.MaxMana, 10f));
            School(S, "empty_vessel", "Empty Vessel", "Move faster the emptier your mana.",
                Rarity.Uncommon, Behave(new EmptyVesselBehaviour()));
            School(S, "displacement", "Displacement", "Aetherics spells expose what they hit, so it takes more damage.",
                Rarity.Uncommon,
                Behave(new SchoolHitStatusBehaviour { School = S, Status = StatusId.Exposed, Base = 0.1f, PerLevel = 0.05f }));
            School(S, "void_pocket", "Void Pocket", "Arcane Warp's bonus lingers for five seconds per level after your mana refills.",
                Rarity.Rare, Mastery(MasterySetting.WarpLinger, 5f));
            School(S, "void_rounds", "Void Rounds", "Below half mana, your gun rounds pierce.",
                Rarity.Rare, Behave(new VoidRoundsBehaviour()));
        }
    }
}
