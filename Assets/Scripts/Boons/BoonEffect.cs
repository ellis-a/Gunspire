using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// One thing a boon does when it is taken. Same idea as <see cref="AbilityEffect"/>, and
    /// for the same reason: a boon used to be a C# lambda, which cannot be serialized, so a
    /// boon could not be an asset. Describing the effect as data instead means the whole
    /// roster can be authored in the Inspector.
    ///
    /// Effects are shared, so they hold parameters only - everything per-run arrives as an
    /// argument. A boon can be taken repeatedly, and each pick runs the chain again with the
    /// new level, so an effect applies its own increment rather than recomputing a total.
    /// </summary>
    [System.Serializable]
    public abstract class BoonEffect
    {
        /// <summary>
        /// Skip this effect until the boon reaches this level. Lets one boon switch something
        /// on with the first pick and deepen it with later ones.
        /// </summary>
        public int MinLevel = 1;

        public abstract void Apply(RunState run, int level);

        /// <summary>Shown in tooling. Descriptions on the boon itself are still hand-written.</summary>
        public virtual string Describe() => GetType().Name.Replace("Effect", "");
    }

    /// <summary>
    /// A gate on whether a boon may be offered. Serializable for the same reason the effects
    /// are: <c>Func&lt;RunState, bool&gt;</c> cannot live in an asset.
    /// </summary>
    [System.Serializable]
    public abstract class BoonRequirement
    {
        public abstract bool IsMet(RunState run);
        public virtual string Describe() => GetType().Name.Replace("Requirement", "");
    }

    public static class BoonRunner
    {
        public static void Apply(List<BoonEffect> effects, RunState run, int level)
        {
            if (effects == null || run == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                BoonEffect effect = effects[i];
                if (effect == null) continue;
                if (level < effect.MinLevel) continue;

                effect.Apply(run, level);
            }
        }

        public static string Describe(List<BoonEffect> effects)
        {
            if (effects == null || effects.Count == 0) return "does nothing";

            string text = "";
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) continue;

                string part = effects[i].Describe();
                if (string.IsNullOrEmpty(part)) continue;

                if (effects[i].MinLevel > 1) part += " (level " + effects[i].MinLevel + "+)";
                if (text.Length > 0) text += ", ";
                text += part;
            }
            return text.Length > 0 ? text : "does nothing";
        }
    }
}
