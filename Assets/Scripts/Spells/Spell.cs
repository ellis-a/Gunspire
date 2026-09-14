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
        /// Costs beyond mana. Every one is refused when it cannot be paid, never clamped: a spell
        /// costing two souls cannot be cast with one. Health is spent through the Blood Debt, and
        /// never below one hit point.
        /// </summary>
        public int SoulCost;
        public float HealthCost;
        public int PsiCost;

        /// <summary>
        /// Spends every soul held instead of a fixed number, and can be cast with none. Bone Shards.
        /// The count spent is on <see cref="AbilityContext.SoulsSpent"/> for the effects to read.
        /// </summary>
        public bool SpendsAllSouls;

        /// <summary>
        /// How far casting this carries to something listening, as a multiple of the listener's
        /// hearing range. Same scale as a gun's. A quiet utility spell belongs well under one.
        /// </summary>
        public float NoiseMultiplier = 1f;

        public Rarity Rarity = Rarity.Common;
        public SpellType Type = SpellType.Attack;
        public DamageType DamageType = DamageType.Energy;

        /// <summary>
        /// The tradition this belongs to. Elemental is first so that spell assets written before
        /// schools existed land there, which is where most of the opening roster belongs anyway.
        /// </summary>
        public SpellSchool School = SpellSchool.Elemental;

        /// <summary>Which slot this can be bound to. See <see cref="SpellSlot"/>.</summary>
        public SpellSlot Slot = SpellSlot.Cast;

        /// <summary>
        /// Filled in only on a spell that stays on until switched off rather than firing once, in
        /// any slot. Always present as an object - Unity insists - so <see cref="IsSustained"/> is
        /// the question to ask, never a null check.
        /// </summary>
        public SustainProfile Sustain = new SustainProfile();

        public bool IsSustained => Sustain != null && Sustain.Exists;

        /// <summary>Filled in only on a spell held to charge and released to cast. Bone Shards.</summary>
        public ChargeProfile Charge = new ChargeProfile();

        public bool IsCharged => Charge != null && Charge.Exists;

        /// <summary>
        /// Filled in only on a stance: a set of modes that is always on while bound, stepped through
        /// by the key. Elemental Form.
        /// </summary>
        public StanceProfile Stance = new StanceProfile();

        public bool IsStance => Stance != null && Stance.Exists;

        /// <summary>
        /// Spells sharing a group are versions of one spell chosen at pick time, such as Shapeshift's
        /// forms. Only one of a group can be equipped; binding another replaces it.
        /// </summary>
        public string VariantGroup = string.Empty;

        /// <summary>Echo never repeats this. Rewind, whose second copy would have nothing to return to.</summary>
        public bool NeverEchoes;

        /// <summary>Seconds taken off this spell's cooldown whenever its caster dodges a hit. Foretell.</summary>
        public float DodgeCooldownRefund;

        /// <summary>
        /// Whether casting this spends one of the dash charges, which the HUD
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
            if (IsStance) return "always on, tap to change form";
            if (IsSustained) return Sustain.CostLine();

            var parts = new List<string>(5);
            if (ManaCost > 0f) parts.Add(Mathf.RoundToInt(ManaCost) + " mana");
            if (SpendsAllSouls) parts.Add("every soul");
            else if (SoulCost > 0) parts.Add(SoulCost + (SoulCost == 1 ? " soul" : " souls"));
            if (HealthCost > 0f) parts.Add(Mathf.RoundToInt(HealthCost) + " health");
            if (PsiCost > 0) parts.Add(PsiCost + " psi");
            if (Cooldown > 0f) parts.Add(Cooldown.ToString("0.#") + "s cooldown");

            return parts.Count > 0 ? string.Join(", ", parts) : "free";
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
