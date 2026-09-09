using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Everything that can occupy the Shift slot. Dash is the default every wizard opens with;
    /// the rest are found on shrine pedestals.
    /// </summary>
    public static class MovementAbilityLibrary
    {
        public const string DefaultId = "dash";

        private static List<MovementAbility> _all;

        public static IReadOnlyList<MovementAbility> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static MovementAbility Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        public static MovementAbility Default => Get(DefaultId);

        /// <summary>Abilities the player does not already have, for a pedestal to offer.</summary>
        public static List<MovementAbility> Offerable(MovementController controller)
        {
            var list = new List<MovementAbility>();
            for (int i = 0; i < All.Count; i++)
                if (controller == null || controller.Current == null || controller.Current.Id != All[i].Id)
                    list.Add(All[i]);
            return list;
        }

        public static MovementAbility RollOffer(Rng rng, MovementController controller, float luck,
            float rarityBonus = 1f)
        {
            List<MovementAbility> pool = Offerable(controller);
            if (pool.Count == 0) return null;

            Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
            return Rarities.PickOfRarity(rng, pool, a => a.Rarity, rolled);
        }

        private static void Build()
        {
            _all = new List<MovementAbility>
            {
                new MovementAbility
                {
                    Id = "dash",
                    DisplayName = "Dash",
                    ShortName = "DASH",
                    Description = "A short burst in the direction you are moving, with a sliver of " +
                                  "invulnerability. Charges recover on their own, and Agility grants more.",
                    Mode = MovementMode.Instant,
                    Rarity = Rarity.Common,
                    UsesDashCharges = true,
                    Tint = new Color(0.6f, 0.95f, 1f),
                    OnActivate = { new DashEffect() }
                },

                new MovementAbility
                {
                    Id = "blink",
                    DisplayName = "Blink",
                    ShortName = "BLNK",
                    Description = "Teleport forward, passing through anything in the way. Stops at the " +
                                  "first wall rather than dropping you inside it.",
                    Mode = MovementMode.Instant,
                    Rarity = Rarity.Uncommon,
                    ManaCost = 16f,
                    Cooldown = 4f,
                    Tint = Palette.Arcane,
                    OnActivate =
                    {
                        // Aborts against a wall, which refunds the mana and the cooldown.
                        new SweepForwardEffect { BaseDistance = 9f, PerIntellect = 0.18f, MaxDistance = 22f },
                        new VfxGhostTrailEffect(),
                        new TeleportEffect(),
                        new GrantInvulnerabilityEffect { Seconds = 0.18f }
                    }
                },

                new MovementAbility
                {
                    Id = "sprint",
                    DisplayName = "Sprint",
                    ShortName = "SPRT",
                    Description = "Hold a hard run for as long as your mana lasts. No invulnerability, " +
                                  "no tricks, just distance.",
                    Mode = MovementMode.Sustained,
                    Rarity = Rarity.Common,
                    ManaPerSecond = 9f,
                    MoveSpeedBonus = 0.55f,
                    Tint = new Color(1f, 0.9f, 0.45f)
                },

                new MovementAbility
                {
                    Id = "shield",
                    DisplayName = "Aegis Stance",
                    ShortName = "SHLD",
                    Description = "Plant your feet and ignore everything for a moment. Breaks the " +
                                  "instant you move, so it answers a telegraph rather than a chase.",
                    Mode = MovementMode.Sustained,
                    Rarity = Rarity.Uncommon,
                    ManaPerSecond = 14f,
                    MaxDuration = 2.5f,
                    Invulnerable = true,
                    BreakOnMovement = true,
                    Tint = new Color(0.8f, 0.85f, 1f)
                },

                new MovementAbility
                {
                    Id = "spider_legs",
                    DisplayName = "Spider Legs",
                    ShortName = "SPDR",
                    Description = "Fires a line at whatever you are looking at and hauls you to " +
                                  "it. Stick, and that surface becomes your new floor - jump and " +
                                  "gravity pulls you straight back down onto it, not away.",
                    Mode = MovementMode.Sustained,
                    Rarity = Rarity.Rare,
                    ManaPerSecond = 11f,
                    WallZip = true,
                    Tint = new Color(0.55f, 0.9f, 0.5f)
                }
            };
        }
    }
}
