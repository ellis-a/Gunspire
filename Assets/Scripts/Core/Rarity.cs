using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Shared quality tier for guns, spells and boons. Ordered worst to best, so
    /// <c>(int)rarity</c> can be compared and stepped down.
    /// </summary>
    public enum Rarity { Common, Uncommon, Rare, Mythic, Legendary }

    /// <summary>
    /// Spawn odds and presentation for each rarity.
    ///
    /// Each tier has a flat chance to be rolled. Luck multiplies every tier above Common,
    /// and Common absorbs whatever probability is left, so investing in Luck genuinely
    /// pushes rolls up the ladder rather than just re-rolling the same pool.
    /// </summary>
    public static class Rarities
    {
        public static readonly Rarity[] All = (Rarity[])Enum.GetValues(typeof(Rarity));

        /// <summary>How much each point of Luck multiplies the non-Common tiers.</summary>
        public const float LuckScaling = 0.08f;

        /// <summary>Flat chance per roll at zero Luck. Common takes the remainder.</summary>
        public static float BaseChance(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Legendary: return 0.0001f;   // 1 in 10,000
                case Rarity.Mythic:    return 0.0040f;   // 1 in 250
                case Rarity.Rare:      return 0.0500f;   // 1 in 20
                case Rarity.Uncommon:  return 0.2200f;
                default:               return 1f;        // Common: whatever is left
            }
        }

        public static Color Tint(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Legendary: return new Color(1.00f, 0.55f, 0.15f);
                case Rarity.Mythic:   return new Color(0.95f, 0.35f, 0.85f);
                case Rarity.Rare:     return new Color(0.40f, 0.65f, 1.00f);
                case Rarity.Uncommon: return new Color(0.45f, 0.90f, 0.55f);
                default:              return new Color(0.82f, 0.84f, 0.90f);
            }
        }

        public static string Name(Rarity rarity) => rarity.ToString().ToUpperInvariant();

        /// <summary>
        /// Rolls a tier. <paramref name="luck"/> is the Luck stat; <paramref name="bonusMultiplier"/>
        /// is an extra push from the context (an elite room paying out better, say).
        /// </summary>
        public static Rarity Roll(Rng rng, float luck, float bonusMultiplier = 1f)
        {
            float factor = (1f + Mathf.Max(0f, luck) * LuckScaling) * Mathf.Max(0.01f, bonusMultiplier);

            float roll = rng.Value;
            float cursor = 0f;

            // Walk from the rarest tier down; the first band the roll lands in wins.
            for (int i = All.Length - 1; i >= 1; i--)
            {
                cursor += BaseChance(All[i]) * factor;
                if (roll < Mathf.Min(cursor, 1f)) return All[i];
            }
            return Rarity.Common;
        }

        /// <summary>
        /// Picks an item of the rolled tier. If nothing exists at that tier the search steps
        /// down, then up, so a thin pool degrades gracefully instead of returning nothing.
        /// </summary>
        public static T PickOfRarity<T>(Rng rng, IList<T> items, Func<T, Rarity> rarityOf, Rarity target)
            where T : class
        {
            if (items == null || items.Count == 0) return null;

            var bucket = new List<T>();

            for (int step = (int)target; step >= 0; step--)
            {
                bucket.Clear();
                for (int i = 0; i < items.Count; i++)
                    if ((int)rarityOf(items[i]) == step) bucket.Add(items[i]);
                if (bucket.Count > 0) return rng.Pick(bucket);
            }

            for (int step = (int)target + 1; step < All.Length; step++)
            {
                bucket.Clear();
                for (int i = 0; i < items.Count; i++)
                    if ((int)rarityOf(items[i]) == step) bucket.Add(items[i]);
                if (bucket.Count > 0) return rng.Pick(bucket);
            }

            return rng.Pick(items);
        }

        /// <summary>Odds at a given Luck, for the character sheet.</summary>
        public static float ChanceAtLuck(Rarity rarity, float luck, float bonusMultiplier = 1f)
        {
            if (rarity == Rarity.Common) return 0f;
            float factor = (1f + Mathf.Max(0f, luck) * LuckScaling) * Mathf.Max(0.01f, bonusMultiplier);
            return Mathf.Clamp01(BaseChance(rarity) * factor);
        }
    }
}
