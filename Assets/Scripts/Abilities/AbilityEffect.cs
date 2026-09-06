using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// One step of an ability. Effects run in order against a shared
    /// <see cref="AbilityContext"/>, communicating through it rather than through return values.
    ///
    /// Returning false aborts the rest of the chain and refunds the cast, which is how
    /// "there is a wall in the way, do not spend my mana" works.
    ///
    /// Effects are shared between every caster using the ability, so they must be stateless:
    /// parameters live on the effect, everything per-cast lives on the context.
    /// </summary>
    [System.Serializable]
    public abstract class AbilityEffect
    {
        public abstract bool Execute(AbilityContext ctx);

        /// <summary>Shown in tooling and level-up text. Override where the default is unhelpful.</summary>
        public virtual string Describe() => GetType().Name.Replace("Effect", "");
    }

    /// <summary>
    /// An effect that takes time: a wind-up, a repeat, a sustained beam. Enemy attacks are
    /// built from these; spells are instantaneous and never touch this path.
    ///
    /// Signal failure by setting <see cref="AbilityContext.Aborted"/> rather than returning,
    /// since a coroutine cannot return a value.
    /// </summary>
    public interface ITimedEffect
    {
        System.Collections.IEnumerator Run(AbilityContext ctx);
    }

    /// <summary>Runs a chain of effects, instantly for spells or over time for enemy attacks.</summary>
    public static class AbilityRunner
    {
        /// <summary>
        /// Executes every effect in order. Returns false if any of them aborted, which the
        /// caller should treat as "the ability did not happen".
        /// </summary>
        public static bool Run(List<AbilityEffect> effects, AbilityContext ctx)
        {
            if (effects == null || effects.Count == 0) return false;

            for (int i = 0; i < effects.Count; i++)
            {
                AbilityEffect effect = effects[i];
                if (effect == null) continue;
                if (!effect.Execute(ctx)) return false;
            }
            return true;
        }

        /// <summary>
        /// Runs a chain that contains timed steps. Instant effects still run instantly; a
        /// timed one is yielded through. Stops at the first abort.
        /// </summary>
        public static System.Collections.IEnumerator RunTimed(List<AbilityEffect> effects, AbilityContext ctx)
        {
            if (effects == null) yield break;

            for (int i = 0; i < effects.Count; i++)
            {
                if (ctx.Aborted) yield break;

                AbilityEffect effect = effects[i];
                if (effect == null) continue;

                if (effect is ITimedEffect timed)
                {
                    yield return timed.Run(ctx);
                }
                else if (!effect.Execute(ctx))
                {
                    ctx.Aborted = true;
                    yield break;
                }
            }
        }

        public static string Describe(List<AbilityEffect> effects)
        {
            if (effects == null || effects.Count == 0) return "does nothing";

            string text = "";
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) continue;
                if (text.Length > 0) text += " > ";
                text += effects[i].Describe();
            }
            return text;
        }
    }
}
