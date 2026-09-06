using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The loadouts offered on the opening screen.
    ///
    /// The three built-ins live in code so a fresh clone has something to pick without any
    /// authored assets. Any <see cref="StartingLoadoutAsset"/> found under a Resources folder
    /// is merged in: a matching id replaces the built-in, a new id is added. That gives
    /// Inspector editing without the game depending on assets existing.
    ///
    /// All three are built on 25 stat points and a gun tuned to roughly the same damage, so
    /// none of them opens ahead of the others.
    /// </summary>
    public static class LoadoutLibrary
    {
        private static List<LoadoutDefinition> _all;

        public static IReadOnlyList<LoadoutDefinition> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static LoadoutDefinition Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return All.Count > 0 ? All[0] : null;
        }

        /// <summary>Drops the cached roster so authored assets are picked up again.</summary>
        public static void Reload() => _all = null;

        private static void Build()
        {
            _all = BuiltIn();

            StartingLoadoutAsset[] authored = Resources.LoadAll<StartingLoadoutAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                LoadoutDefinition def = authored[i] != null ? authored[i].Loadout : null;
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                int existing = _all.FindIndex(l => l.Id == def.Id);
                if (existing >= 0) _all[existing] = def;
                else _all.Add(def);
            }
        }

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<LoadoutDefinition> BuiltIn()
        {
            return new List<LoadoutDefinition>
            {
                new LoadoutDefinition
                {
                    Id = "pyromancer",
                    DisplayName = "Pyromancer",
                    Description = "Set the room alight and keep moving. Burning stacks reward " +
                                  "spraying widely, and Lava Splash turns a doorway into a wall of fire.",
                    Strength = 4, Intellect = 8, Agility = 6, Vitality = 4, Luck = 3,
                    WeaponId = "emberspit",
                    MovementAbilityId = "dash",
                    SpellId = "lava_splash"
                },

                new LoadoutDefinition
                {
                    Id = "ice_wizard",
                    DisplayName = "Ice Wizard",
                    Description = "Freeze them where they stand and shoot them at your leisure. " +
                                  "Frozen targets shatter, and a shotgun answers whatever is left.",
                    Strength = 4, Intellect = 7, Agility = 4, Vitality = 7, Luck = 3,
                    WeaponId = "hailmaker",
                    MovementAbilityId = "dash",
                    SpellId = "cone_of_cold"
                },

                new LoadoutDefinition
                {
                    Id = "warlock",
                    DisplayName = "Warlock",
                    Description = "Deny the ground and let rot do the work. Heavy on splash, " +
                                  "light on precision, and everything you touch stops healing itself.",
                    Strength = 6, Intellect = 6, Agility = 4, Vitality = 5, Luck = 4,
                    WeaponId = "knell",
                    MovementAbilityId = "dash",
                    SpellId = "blightbloom"
                }
            };
        }
    }
}
