using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// A castable ability. This is data: identity, cost, school, and the chain of
    /// <see cref="AbilityEffect"/>s that runs when it is cast. The behaviour lives in the
    /// effects, which are shared with every other ability in the game.
    ///
    /// One instance is shared by every caster, so nothing per-cast is stored here - the level
    /// lives on the caster's <see cref="SpellBook"/> and the working state on the context.
    /// </summary>
    public class Spell
    {
        public string Id = "spell";
        public string DisplayName = "Spell";
        public string ShortName = "SPEL";
        public string Description = string.Empty;

        public float ManaCost = 20f;
        public float Cooldown = 6f;

        public Rarity Rarity = Rarity.Common;
        public SpellType Type = SpellType.Attack;
        public DamageType DamageType = DamageType.Astral;

        /// <summary>How many times it can be taken. Each pick past the first raises the level.</summary>
        public int MaxLevel = 5;

        /// <summary>Damage, range and area growth per level past the first.</summary>
        public float GrowthPerLevel = 0.22f;

        /// <summary>Optional line describing what levelling buys, when the generic text is vague.</summary>
        public string LevelUpNote;

        /// <summary>Leave clear to take the colour of the damage school.</summary>
        public Color TintOverride = Color.clear;

        /// <summary>What actually happens, in order. Aborting any step refunds the cast.</summary>
        public List<AbilityEffect> OnCast = new List<AbilityEffect>();

        public Color Tint => TintOverride.a > 0f ? TintOverride : DamageTypes.Tint(DamageType);

        /// <summary>Multiplier applied to the spell's own numbers at a given level.</summary>
        public float LevelMultiplier(int level) => 1f + Mathf.Max(0, level - 1) * GrowthPerLevel;

        /// <summary>Cooldowns shorten slightly as a spell levels, to a floor of 60% of base.</summary>
        public float CooldownAtLevel(int level)
            => Cooldown * Mathf.Max(0.6f, 1f - Mathf.Max(0, level - 1) * 0.06f);

        /// <summary>One line describing what the next level buys, for the pedestal prompt.</summary>
        public string LevelUpSummary(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return "Already at maximum level.";

            string generic = string.Format("level {0} to {1}: effect +{2:0}%, cooldown {3:0.0}s to {4:0.0}s",
                currentLevel, currentLevel + 1, GrowthPerLevel * 100f,
                CooldownAtLevel(currentLevel), CooldownAtLevel(currentLevel + 1));

            return string.IsNullOrEmpty(LevelUpNote) ? generic : generic + ", " + LevelUpNote;
        }

        /// <summary>Human-readable chain, skipping the cosmetic steps.</summary>
        public string EffectSummary() => AbilityRunner.Describe(OnCast);

        /// <summary>
        /// Runs the effect chain. Returns false if any effect aborted, in which case the
        /// caller refunds the mana and the cooldown.
        /// </summary>
        public bool Cast(AbilityContext ctx, int level)
        {
            ctx.BeginCast(this, level);

            bool cast = AbilityRunner.Run(OnCast, ctx);
            if (cast) AbilityEvents.RaiseCast(Id, ctx, ctx.Point);

            ctx.EndCast();
            return cast;
        }
    }
}
