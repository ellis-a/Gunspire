using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The boon pool and the rules for what gets offered. Luck drives the rarity roll, which
    /// is most of what makes the Luck stat worth taking.
    ///
    /// The built-ins live in code so a fresh clone runs with nothing authored. Any
    /// <see cref="BoonAsset"/> found under a Resources folder is merged in: a matching id
    /// replaces the built-in, a new id joins the pool at whatever rarity it declares.
    /// </summary>
    public static partial class BoonLibrary
    {
        private static List<Boon> _all;

        public static IReadOnlyList<Boon> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        /// <summary>Drops the cached pool so authored assets are picked up again.</summary>
        public static void Reload() => _all = null;

        /// <summary>
        /// How likely each family is to supply a card, before normalising over the families that
        /// have anything to offer. Pacts are Legendary rewards for specific actions, never a roll.
        /// </summary>
        public static float FamilyWeight(BoonFamily family)
        {
            switch (family)
            {
                case BoonFamily.Core:    return 30f;
                case BoonFamily.Arsenal: return 20f;
                case BoonFamily.Slots:   return 10f;
                case BoonFamily.School:  return 15f;
                case BoonFamily.Spell:   return 25f;
                default:                 return 0f;
            }
        }

        /// <summary>
        /// Rolls a set of distinct, currently valid boons. Each card draws a family first, weighted
        /// by <see cref="FamilyWeight"/> over the families that still have a candidate, then a
        /// rarity, then a boon of that rarity from the family. <paramref name="rarityBonus"/> is an
        /// extra multiplier on the non-Common tiers, used to make elite rooms pay out better.
        /// </summary>
        public static List<Boon> Offer(RunState run, int count, float rarityBonus = 1f)
        {
            if (_all == null) Build();
            return Offer(run, _all, count, rarityBonus);
        }

        /// <summary>The same roll from a given pool. Public so tooling can test it against a known roster.</summary>
        public static List<Boon> Offer(RunState run, IReadOnlyList<Boon> pool, int count, float rarityBonus = 1f)
        {
            var candidates = new List<Boon>();
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].IsOffered(run)) candidates.Add(pool[i]);

            var chosen = new List<Boon>();
            var used = new HashSet<string>();
            var available = new List<Boon>();
            var families = (BoonFamily[])System.Enum.GetValues(typeof(BoonFamily));
            var weights = new float[families.Length];

            while (chosen.Count < count)
            {
                float total = 0f;
                for (int f = 0; f < families.Length; f++)
                {
                    weights[f] = 0f;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (candidates[i].Family != families[f] || used.Contains(candidates[i].Id)) continue;
                        weights[f] = FamilyWeight(families[f]);
                        break;
                    }
                    total += weights[f];
                }
                if (total <= 0f) break;

                float roll = run.Rng.Value * total;
                // Falls to the last family with weight if rounding carries the roll past the end.
                int family = -1;
                for (int f = 0; f < families.Length; f++)
                {
                    if (weights[f] <= 0f) continue;
                    family = f;
                    if (roll < weights[f]) break;
                    roll -= weights[f];
                }

                available.Clear();
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].Family == families[family] && !used.Contains(candidates[i].Id))
                        available.Add(candidates[i]);
                if (available.Count == 0) break;

                Rarity rolled = Rarities.Roll(run.Rng, run.Luck, rarityBonus);
                Boon pick = Rarities.PickOfRarity(run.Rng, available, b => b.Rarity, rolled);
                if (pick == null) break;

                used.Add(pick.Id);
                chosen.Add(pick);
            }

            return chosen;
        }

        public static Boon Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        // ---------------------------------------------------------------- pool

        private static void Build()
        {
            // Assigned before merging: anything reading the pool while assets are loading gets
            // the built-ins rather than recursing into a half-built list.
            _all = BuiltIn();

            BoonAsset[] authored = Resources.LoadAll<BoonAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                Boon boon = authored[i] != null ? authored[i].Boon : null;
                if (boon == null) continue;

                if (string.IsNullOrEmpty(boon.Id))
                {
                    Debug.LogWarning("Boon asset \"" + authored[i].name + "\" has no Id and was ignored.");
                    continue;
                }

                int existing = _all.FindIndex(b => b.Id == boon.Id);
                if (existing >= 0) _all[existing] = boon;
                else _all.Add(boon);
            }
        }

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<Boon> BuiltIn()
        {
            // The roster in Docs/BoonDesign.md, one file per family.
            _building = new List<Boon>();
            AddCore();
            AddArsenal();
            AddSlots();
            AddSchools();

            List<Boon> result = _building;
            _building = null;
            return result;
        }

        private static List<Boon> _building;

        /// <summary>
        /// Adds a boon to the roster being built and returns it, so a caller can add requirements
        /// and tags without a longer signature.
        /// </summary>
        private static Boon Add(BoonFamily family, string group, string id, string name, string description,
            Rarity rarity, int maxLevel, params BoonEffect[] effects)
        {
            var boon = new Boon
            {
                Id = id,
                Name = name,
                Description = description,
                Rarity = rarity,
                Family = family,
                Group = group,
                MaxLevel = maxLevel,
                Effects = new List<BoonEffect>(effects)
            };
            _building.Add(boon);
            return boon;
        }

        // ---------------- shorthands
        //
        // The roster is read far more often than it is written, and these cover most of it.
        // Anything more elaborate spells out its effect object in full.

        private static ModifyAttributeEffect Percent(Attr attr, float amount) =>
            new ModifyAttributeEffect { Attribute = attr, Mode = ModifierMode.Percent, Amount = amount };

        private static ModifyAttributeEffect Flat(Attr attr, float amount) =>
            new ModifyAttributeEffect { Attribute = attr, Mode = ModifierMode.Flat, Amount = amount };

        private static ModifyStatEffect Stat(StatType stat, int points) =>
            new ModifyStatEffect { Stat = stat, Points = points };

        private static BoonBehaviourEffect Behave(BoonBehaviour behaviour) =>
            new BoonBehaviourEffect { Behaviour = behaviour };

        private static ModifyGunClassEffect ClassPercent(WeaponClass weaponClass, Attr attr, float amount) =>
            new ModifyGunClassEffect { Class = weaponClass, Attribute = attr, Mode = ModifierMode.Percent, Amount = amount };

        private static ModifyGunClassEffect ClassFlat(WeaponClass weaponClass, Attr attr, float amount) =>
            new ModifyGunClassEffect { Class = weaponClass, Attribute = attr, Mode = ModifierMode.Flat, Amount = amount };

        private static RunSettingEffect Setting(RunSetting setting, float amount) =>
            new RunSettingEffect { Setting = setting, Amount = amount };

        private static MasterySettingEffect Mastery(MasterySetting setting, float amount, int fromLevel = 1) =>
            new MasterySettingEffect { Setting = setting, Amount = amount, FromLevel = fromLevel };

        private static Boon Needs(this Boon boon, params BoonRequirement[] requirements)
        {
            boon.Requirements.AddRange(requirements);
            return boon;
        }

        private static Boon Tagged(this Boon boon, params string[] tags)
        {
            boon.Tags.AddRange(tags);
            return boon;
        }

        private static WeaponClassCarriedRequirement Carries(WeaponClass weaponClass) =>
            new WeaponClassCarriedRequirement { Class = weaponClass };

        private static SchoolEquippedRequirement Holds(SpellSchool school) =>
            new SchoolEquippedRequirement { School = school };

        private static SlotFilledRequirement Filled(SpellSlot slot) => new SlotFilledRequirement { Slot = slot };

        private static NotWithBoonRequirement NotWith(string boonId) => new NotWithBoonRequirement { BoonId = boonId };
    }
}
