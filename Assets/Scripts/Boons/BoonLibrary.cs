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
    public static class BoonLibrary
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
        /// Rolls a set of distinct, currently valid boons. <paramref name="rarityBonus"/> is an
        /// extra multiplier on the non-Common tiers, used to make elite rooms pay out better.
        /// </summary>
        public static List<Boon> Offer(RunState run, int count, float rarityBonus = 1f)
        {
            if (_all == null) Build();

            var candidates = new List<Boon>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].IsOffered(run)) candidates.Add(_all[i]);

            var chosen = new List<Boon>();
            var used = new HashSet<string>();
            var available = new List<Boon>();

            int guard = 0;
            while (chosen.Count < count && guard++ < 200)
            {
                available.Clear();
                for (int i = 0; i < candidates.Count; i++)
                    if (!used.Contains(candidates[i].Id)) available.Add(candidates[i]);
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
            // Empty while the redesigned roster in Docs/BoonDesign.md is built. Offers with an
            // empty pool fall straight through to the room choice.
            _building = new List<Boon>();

            List<Boon> result = _building;
            _building = null;
            return result;
        }

        private static List<Boon> _building;

        private static void Add(string id, string name, string description, Rarity rarity, int maxLevel,
            params BoonEffect[] effects)
        {
            AddGated(id, name, description, rarity, maxLevel, null, effects);
        }

        private static void AddGated(string id, string name, string description, Rarity rarity, int maxLevel,
            BoonRequirement requirement, params BoonEffect[] effects)
        {
            _building.Add(new Boon
            {
                Id = id,
                Name = name,
                Description = description,
                Rarity = rarity,
                MaxLevel = maxLevel,
                Requirement = requirement,
                Effects = new List<BoonEffect>(effects)
            });
        }

        // ---------------- shorthands
        //
        // The roster is read far more often than it is written, and these cover most of it.
        // Anything more elaborate spells out its effect object in full.

        private static ModifyAttributeEffect Percent(Attr attr, float amount) =>
            new ModifyAttributeEffect { Attribute = attr, Mode = ModifierMode.Percent, Amount = amount };

        private static ModifyAttributeEffect Flat(Attr attr, float amount) =>
            new ModifyAttributeEffect { Attribute = attr, Mode = ModifierMode.Flat, Amount = amount };

        private static AddOnHitStatusEffect Bullets(StatusApplication status) =>
            new AddOnHitStatusEffect { Status = status, OnSpells = false };

        private static AddOnHitStatusEffect Spells(StatusApplication status) =>
            new AddOnHitStatusEffect { Status = status, OnSpells = true };
    }
}
