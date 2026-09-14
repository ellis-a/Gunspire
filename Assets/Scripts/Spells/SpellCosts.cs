using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Checking and paying what a spell costs: mana, souls, health and psi charge. Every slot goes
    /// through here, so the one rule holds everywhere: a cost that cannot be paid refuses the cast,
    /// it is never clamped to what is there.
    ///
    /// Souls and psi live on their masteries, so a caster without the mastery has none of either and
    /// cannot pay a cost in them. Health is spent through <see cref="Health.Drain"/> and recorded by
    /// the Blood Debt when there is one, and can never take the caster below one hit point.
    /// </summary>
    public static class SpellCosts
    {
        /// <summary>A health cost may leave the caster this low and no lower.</summary>
        public const float HealthFloor = 1f;

        /// <summary>Ready, or the first cost that cannot be paid.</summary>
        public static CastOutcome Check(Spell spell, AbilityContext ctx)
        {
            if (spell == null) return CastOutcome.NoSpell;
            if (ctx == null) return CastOutcome.Ready;

            if (spell.ManaCost > 0f && (ctx.Mana == null || !ctx.Mana.Has(spell.ManaCost)))
                return CastOutcome.NotEnoughMana;

            if (!spell.SpendsAllSouls && spell.SoulCost > 0)
            {
                SoulsMastery souls = Souls(ctx);
                if (souls == null || souls.Souls < spell.SoulCost) return CastOutcome.NotEnoughSouls;
            }

            if (spell.PsiCost > 0)
            {
                PsiBladesMastery psi = Psi(ctx);
                if (psi == null || psi.Charge + 0.0001f < spell.PsiCost) return CastOutcome.NotEnoughPsi;
            }

            if (spell.HealthCost > 0f && !CanPayHealth(ctx.Health, spell.HealthCost))
                return CastOutcome.NotEnoughHealth;

            return CastOutcome.Ready;
        }

        /// <summary>How many souls a cast will spend, set on the context before the chain runs.</summary>
        public static int SoulsFor(Spell spell, AbilityContext ctx)
        {
            if (spell == null) return 0;
            if (!spell.SpendsAllSouls) return Mathf.Max(0, spell.SoulCost);

            SoulsMastery souls = Souls(ctx);
            return souls != null ? souls.Souls : 0;
        }

        /// <summary>Takes every cost. Call only once <see cref="Check"/> has passed and the chain has committed.</summary>
        public static void Pay(Spell spell, AbilityContext ctx)
        {
            if (spell == null || ctx == null) return;

            if (spell.ManaCost > 0f && ctx.Mana != null) ctx.Mana.TrySpend(spell.ManaCost);

            SoulsMastery souls = Souls(ctx);
            if (souls != null)
            {
                // What the cast was told it spent rather than whatever is held now, since its own kills may
                // have bound more while it ran.
                if (spell.SpendsAllSouls) souls.TrySpend(Mathf.Min(ctx.SoulsSpent, souls.Souls));
                else if (spell.SoulCost > 0) souls.TrySpend(spell.SoulCost);
            }

            if (spell.PsiCost > 0)
            {
                PsiBladesMastery psi = Psi(ctx);
                if (psi != null) psi.TrySpend(spell.PsiCost);
            }

            if (spell.HealthCost > 0f) PayHealth(ctx.Health, spell.HealthCost);
        }

        public static bool CanPayHealth(Health health, float amount)
            => amount <= 0f || (health != null && health.IsAlive && health.Current - amount >= HealthFloor);

        /// <summary>
        /// Spends health as a price, recording it as debt when the Blood Debt is held. Refuses, taking
        /// nothing, when it would leave less than one hit point.
        /// </summary>
        public static bool PayHealth(Health health, float amount)
        {
            if (amount <= 0f) return true;
            if (!CanPayHealth(health, amount)) return false;

            float paid = health.Drain(amount);

            BloodDebtMastery debt = health.GetComponent<BloodDebtMastery>();
            if (debt != null) debt.Record(paid);
            return true;
        }

        public static SoulsMastery Souls(AbilityContext ctx)
            => ctx != null && ctx.Caster != null ? ctx.Caster.GetComponent<SoulsMastery>() : null;

        public static PsiBladesMastery Psi(AbilityContext ctx)
            => ctx != null && ctx.Caster != null ? ctx.Caster.GetComponent<PsiBladesMastery>() : null;

        /// <summary>Words for a refusal, shared by every slot so they all explain themselves the same way.</summary>
        public static string Refusal(CastOutcome outcome, Spell spell)
        {
            string name = spell != null ? spell.DisplayName : "that";
            switch (outcome)
            {
                case CastOutcome.NotEnoughMana: return "Not enough mana";
                case CastOutcome.NotEnoughSouls: return "Not enough souls for " + name;
                case CastOutcome.NotEnoughHealth: return "Too little health to pay for " + name;
                case CastOutcome.NotEnoughPsi: return "Not enough psi charge for " + name;
                case CastOutcome.Silenced: return "Silenced - no spells";
                case CastOutcome.Disarmed: return "Disarmed";
                case CastOutcome.NoRoom: return "No room to cast that";
                default: return null;
            }
        }
    }
}
