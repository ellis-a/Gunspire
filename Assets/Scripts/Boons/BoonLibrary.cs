using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The boon pool and the rules for what gets offered. Luck nudges the rarity roll, which
    /// is most of what makes the Luck stat worth taking.
    /// </summary>
    public static class BoonLibrary
    {
        private static List<Boon> _all;

        public static IReadOnlyList<Boon> All
        {
            get
            {
                if (_all == null) BuildPool();
                return _all;
            }
        }

        /// <summary>Rolls a set of distinct, currently valid boons to put in front of the player.</summary>
        public static List<Boon> Offer(RunState run, int count, bool favourRare = false)
        {
            if (_all == null) BuildPool();

            var candidates = new List<Boon>();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].IsOffered(run)) candidates.Add(_all[i]);

            var chosen = new List<Boon>();
            var used = new HashSet<string>();

            int guard = 0;
            while (chosen.Count < count && candidates.Count > 0 && guard++ < 400)
            {
                BoonRarity wanted = RollRarity(run, favourRare);

                var bucket = new List<Boon>();
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].Rarity == wanted && !used.Contains(candidates[i].Id))
                        bucket.Add(candidates[i]);

                if (bucket.Count == 0)
                {
                    // Nothing left at that rarity; take anything still unused.
                    for (int i = 0; i < candidates.Count; i++)
                        if (!used.Contains(candidates[i].Id)) bucket.Add(candidates[i]);
                    if (bucket.Count == 0) break;
                }

                Boon pick = run.Rng.Pick(bucket);
                used.Add(pick.Id);
                chosen.Add(pick);
            }

            return chosen;
        }

        private static BoonRarity RollRarity(RunState run, bool favourRare)
        {
            int luck = run.Sheet != null ? run.Sheet.GetStat(StatType.Luck) : 5;
            float rareChance = 0.10f + luck * 0.012f + (favourRare ? 0.22f : 0f);
            float uncommonChance = 0.34f + luck * 0.010f;

            float roll = run.Rng.Value;
            if (roll < rareChance) return BoonRarity.Rare;
            if (roll < rareChance + uncommonChance) return BoonRarity.Uncommon;
            return BoonRarity.Common;
        }

        // ---------------------------------------------------------------- pool

        private static void Add(string id, string name, string description, BoonRarity rarity,
            System.Action<RunState> effect, System.Func<RunState, bool> requirement = null)
        {
            _all.Add(new Boon
            {
                Id = id,
                Name = name,
                Description = description,
                Rarity = rarity,
                Effect = effect,
                Requirement = requirement
            });
        }

        private static void BuildPool()
        {
            _all = new List<Boon>();

            // ---------------- common: straightforward numbers

            Add("quickened_step", "Quickened Step", "+12% movement speed.", BoonRarity.Common,
                run => run.Sheet.AddPercent(Attr.MoveSpeed, 0.12f, "boon"));

            Add("ironhide", "Ironhide", "+25 maximum health.", BoonRarity.Common,
                run => run.Sheet.AddFlat(Attr.MaxHealth, 25f, "boon"));

            Add("sharpshooter", "Sharpshooter", "+14% gun damage.", BoonRarity.Common,
                run => run.Sheet.AddPercent(Attr.GunDamage, 0.14f, "boon"));

            Add("arcane_focus", "Arcane Focus", "+14% spell power.", BoonRarity.Common,
                run => run.Sheet.AddPercent(Attr.SpellPower, 0.14f, "boon"));

            Add("deep_well", "Deep Well", "+30 maximum mana and +25% mana regeneration.", BoonRarity.Common,
                run =>
                {
                    run.Sheet.AddFlat(Attr.MaxMana, 30f, "boon");
                    run.Sheet.AddPercent(Attr.ManaRegen, 0.25f, "boon");
                });

            Add("steady_hands", "Steady Hands", "+30% reload speed.", BoonRarity.Common,
                run => run.Sheet.AddPercent(Attr.ReloadSpeed, 0.30f, "boon"));

            Add("cruel_edge", "Cruel Edge", "+8% critical strike chance.", BoonRarity.Common,
                run => run.Sheet.AddFlat(Attr.CritChance, 0.08f, "boon"));

            Add("light_feet", "Light Feet", "+20% jump height and +20% air control.", BoonRarity.Common,
                run =>
                {
                    run.Sheet.AddPercent(Attr.JumpHeight, 0.20f, "boon");
                    run.Sheet.AddPercent(Attr.AirControl, 0.20f, "boon");
                });

            // ---------------- uncommon: elemental payloads and kill hooks

            Add("frostbite_rounds", "Frostbite Rounds", "Your bullets chill what they hit.", BoonRarity.Uncommon,
                run => run.BulletStatuses.Add(StatusLibrary.Chill(3f, 1)));

            Add("incendiary_rounds", "Incendiary Rounds", "Your bullets set targets burning.", BoonRarity.Uncommon,
                run => run.BulletStatuses.Add(StatusLibrary.Burn(3.5f, 1, 4f)));

            Add("venomed_rounds", "Venomed Rounds", "Your bullets blight targets, poisoning them and absorbing their healing.",
                BoonRarity.Uncommon,
                run => run.BulletStatuses.Add(StatusLibrary.Blight(6f, 1, 3f)));

            Add("conductive_rounds", "Conductive Rounds", "Your bullets shock targets, making them take more damage.",
                BoonRarity.Uncommon,
                run => run.BulletStatuses.Add(StatusLibrary.Shock(4f, 1)));

            Add("runic_overflow", "Runic Overflow", "Your damaging spells also set targets burning.", BoonRarity.Uncommon,
                run => run.SpellStatuses.Add(StatusLibrary.Burn(4f, 2, 5f)));

            Add("siphon", "Siphon", "Kills restore 14 mana.", BoonRarity.Uncommon,
                run => run.ManaOnKill += 14f);

            Add("bloodfeast", "Bloodfeast", "Kills restore 9 health.", BoonRarity.Uncommon,
                run => run.HealOnKill += 9f);

            Add("momentum", "Momentum", "Kills grant a burst of speed for three seconds.", BoonRarity.Uncommon,
                run => run.HasteOnKill = true);

            Add("quickened_casting", "Quickened Casting", "Kills shave 0.8s off your spell cooldowns.",
                BoonRarity.Uncommon,
                run => run.CooldownReductionOnKill += 0.8f);

            Add("second_wind", "Second Wind", "+2 Vitality, and heal to full right now.", BoonRarity.Uncommon,
                run =>
                {
                    run.Sheet.AddStat(StatType.Vitality, 2);
                    run.Player.Health.Heal(run.Player.Health.Max);
                });

            Add("fleet", "Fleet", "+1 dash charge.", BoonRarity.Uncommon,
                run => run.Sheet.AddFlat(Attr.DashCharges, 1f, "boon"));

            Add("cold_blood", "Cold Blood", "+25% spell cooldown rate.", BoonRarity.Uncommon,
                run => run.Sheet.AddPercent(Attr.CooldownRate, 0.25f, "boon"));

            // ---------------- rare: build-defining

            Add("vampiric_sigil", "Vampiric Sigil", "Heal for 7% of all damage you deal.", BoonRarity.Rare,
                run => run.LifestealFraction += 0.07f);

            Add("blink_detonation", "Blink Detonation", "Arriving from a Blink detonates an arcane blast.",
                BoonRarity.Rare,
                run => run.BlinkDetonates = true,
                run => run.Player != null && run.Player.Book.Knows(SpellLibrary.Get("blink")));

            Add("glass_cannon", "Glass Cannon", "+45% damage dealt, but -30% maximum health.", BoonRarity.Rare,
                run =>
                {
                    run.Sheet.AddPercent(Attr.DamageDealt, 0.45f, "boon");
                    run.Sheet.AddPercent(Attr.MaxHealth, -0.30f, "boon");
                });

            Add("overcharge", "Overcharge", "+30% attack speed, but -12% maximum health.", BoonRarity.Rare,
                run =>
                {
                    run.Sheet.AddPercent(Attr.AttackSpeed, 0.30f, "boon");
                    run.Sheet.AddPercent(Attr.MaxHealth, -0.12f, "boon");
                });

            Add("aegis", "Aegis", "Take 18% less damage from everything.", BoonRarity.Rare,
                run => run.Sheet.AddPercent(Attr.DamageTaken, -0.18f, "boon"));

            Add("executioner", "Executioner", "+60% critical damage.", BoonRarity.Rare,
                run => run.Sheet.AddPercent(Attr.CritDamage, 0.60f, "boon"));

            Add("titan_grip", "Titan Grip", "+3 Strength. Heavier blows, and heavier things you can break.",
                BoonRarity.Rare,
                run => run.Sheet.AddStat(StatType.Strength, 3));

            Add("scholar", "Scholar", "+3 Intellect.", BoonRarity.Rare,
                run => run.Sheet.AddStat(StatType.Intellect, 3));

            Add("windrunner", "Windrunner", "+3 Agility.", BoonRarity.Rare,
                run => run.Sheet.AddStat(StatType.Agility, 3));

            Add("fortune", "Fortune", "+3 Luck. Better crits and better boons.", BoonRarity.Rare,
                run => run.Sheet.AddStat(StatType.Luck, 3));
        }
    }
}
