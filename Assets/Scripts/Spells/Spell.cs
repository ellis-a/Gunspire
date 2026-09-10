using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A castable ability. This is data: identity, cost, school, and the chain of
    /// <see cref="AbilityEffect"/>s that runs when it is cast. The behaviour lives in the
    /// effects, which are shared with every other ability in the game.
    ///
    /// One instance is shared by every caster, so nothing per-cast is stored here - the level
    /// lives on the caster's <see cref="SpellBook"/> and the working state on the context.
    /// </summary>
    [System.Serializable]
    public class Spell
    {
        public string Id = "spell";
        public string DisplayName = "Spell";
        public string ShortName = "SPEL";
        public string Description = string.Empty;

        public float ManaCost = 20f;
        public float Cooldown = 6f;

        /// <summary>
        /// How far casting this carries to something listening, as a multiple of the listener's
        /// hearing range. Same scale as a gun's. A quiet utility spell belongs well under one.
        /// </summary>
        public float NoiseMultiplier = 1f;

        public Rarity Rarity = Rarity.Common;
        public SpellType Type = SpellType.Attack;
        public DamageType DamageType = DamageType.Astral;

        /// <summary>Which slot this can be bound to. See <see cref="SpellSlot"/>.</summary>
        public SpellSlot Slot = SpellSlot.Cast;

        /// <summary>
        /// Filled in only on a movement spell that stays on until switched off rather than
        /// firing once. Always present as an object - Unity insists - but inert until its
        /// drain is set, so <see cref="IsSustained"/> is the question to ask, never a null check.
        /// </summary>
        public SustainProfile Sustain = new SustainProfile();

        public bool IsSustained => Sustain != null && Sustain.Exists;

        /// <summary>
        /// Whether casting this spends one of the Agility-scaled dash charges, which the HUD
        /// draws as pips instead of a cooldown. Read off the effect chain rather than kept as a
        /// second flag beside it, so an authored spell that drops in a dash gets the pips too.
        /// </summary>
        public bool UsesDashCharges
        {
            get
            {
                for (int i = 0; i < OnCast.Count; i++)
                    if (OnCast[i] is DashEffect) return true;
                return false;
            }
        }

        /// <summary>How many times it can be taken. Each pick past the first raises the level.</summary>
        public int MaxLevel = 5;

        /// <summary>Damage, range and area growth per level past the first.</summary>
        public float GrowthPerLevel = 0.22f;

        /// <summary>Optional line describing what levelling buys, when the generic text is vague.</summary>
        public string LevelUpNote;

        /// <summary>Leave clear to take the colour of the damage school.</summary>
        public Color TintOverride = Color.clear;

        /// <summary>
        /// Shown on the HUD slots, the choice screens and the rune pedestal. Leave empty and
        /// the UI draws a tinted placeholder. Assign it on the spell asset - a spell built in
        /// code cannot reference one.
        /// </summary>
        public Texture2D Icon;

        /// <summary>What actually happens, in order. Aborting any step refunds the cast.</summary>
        [SerializeReference] public List<AbilityEffect> OnCast = new List<AbilityEffect>();

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

        /// <summary>One line of cost for the HUD, the pedestal prompt and the choice screens.</summary>
        public string CostLine()
        {
            if (IsSustained) return Sustain.CostLine();

            string mana = ManaCost > 0f ? Mathf.RoundToInt(ManaCost) + " mana" : "";
            string cooldown = Cooldown > 0f ? Cooldown.ToString("0.#") + "s cooldown" : "";

            if (mana.Length > 0 && cooldown.Length > 0) return mana + ", " + cooldown;
            if (mana.Length > 0) return mana;
            return cooldown.Length > 0 ? cooldown : "free";
        }

        /// <summary>
        /// Runs the effect chain. Returns false if any effect aborted, in which case the
        /// caller refunds the mana and the cooldown.
        /// </summary>
        public bool Cast(AbilityContext ctx, int level)
        {
            ctx.BeginCast(this, level);

            bool cast = AbilityRunner.Run(OnCast, ctx);
            if (cast)
            {
                AbilityEvents.RaiseCast(Id, ctx, ctx.Point);

                // Only the player gives themselves away by casting. Treated as one at a time,
                // rather than reading zero as silent, so a spell asset written before this
                // existed is heard rather than mysteriously not.
                if (ctx.Team == Team.Player && ctx.Caster != null)
                    Noise.Emit(ctx.Caster.transform.position,
                        NoiseMultiplier > 0f ? NoiseMultiplier : 1f);
            }

            ctx.EndCast();
            return cast;
        }
    }
}
