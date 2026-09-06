using System;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// One upgrade offered between rooms. Boons write into <see cref="RunState"/> or straight
    /// onto the character sheet; nothing else needs to know they exist.
    ///
    /// A boon can be taken more than once. Each pick raises its level and calls
    /// <see cref="Effect"/> again with the new level, so effects apply their own per-level
    /// increment rather than recomputing a total.
    /// </summary>
    public class Boon
    {
        public string Id;
        public string Name;
        public string Description;
        public Rarity Rarity = Rarity.Common;

        /// <summary>How many times it can be taken. One means it is a one-off.</summary>
        public int MaxLevel = 3;

        /// <summary>
        /// Applied on every pick. The int is the new level, 1-based; most effects ignore it
        /// and simply add their increment, but a flag-style boon can use it to scale.
        /// </summary>
        public Action<RunState, int> Effect;

        /// <summary>Optional gate, e.g. a Blink upgrade only shows if Blink is known.</summary>
        public Func<RunState, bool> Requirement;

        public void Apply(RunState run, int newLevel) => Effect?.Invoke(run, newLevel);

        /// <summary>Can this still be offered: not yet capped, and its requirement is met.</summary>
        public bool IsOffered(RunState run)
        {
            if (run.BoonLevel(Id) >= MaxLevel) return false;
            return Requirement == null || Requirement(run);
        }

        public Color RarityColor => Rarities.Tint(Rarity);

        /// <summary>Card subtitle: shows whether this is a new pick or a level-up.</summary>
        public string LevelLabel(RunState run)
        {
            int level = run.BoonLevel(Id);
            if (level <= 0) return MaxLevel > 1 ? "NEW  -  max level " + MaxLevel : "NEW";
            return string.Format("LEVEL {0} to {1}", level, Mathf.Min(level + 1, MaxLevel));
        }
    }
}
