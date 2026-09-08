using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// One upgrade offered between rooms. Boons write into <see cref="RunState"/> or straight
    /// onto the character sheet; nothing else needs to know they exist.
    ///
    /// A boon can be taken more than once. Each pick raises its level and runs
    /// <see cref="Effects"/> again with the new level, so an effect applies its own per-level
    /// increment rather than recomputing a total.
    ///
    /// Effects are a [SerializeReference] list rather than a C# delegate, which is what lets a
    /// boon exist as an asset. See <see cref="BoonEffect"/>.
    /// </summary>
    [System.Serializable]
    public class Boon
    {
        public string Id = "boon";
        public string Name = "Boon";

        [TextArea(2, 4)]
        public string Description = string.Empty;

        public Rarity Rarity = Rarity.Common;

        /// <summary>
        /// Shown on the draft cards and in the character sheet. Leave empty and the UI draws a
        /// tinted placeholder. Assign it on the boon asset - a boon built in code cannot
        /// reference one.
        /// </summary>
        public Texture2D Icon;

        /// <summary>How many times it can be taken. One means it is a one-off.</summary>
        public int MaxLevel = 3;

        /// <summary>What taking it does. Runs in order on every pick.</summary>
        [SerializeReference] public List<BoonEffect> Effects = new List<BoonEffect>();

        /// <summary>Optional gate, e.g. a Blink upgrade only shows if Blink is carried.</summary>
        [SerializeReference] public BoonRequirement Requirement;

        public void Apply(RunState run, int newLevel) => BoonRunner.Apply(Effects, run, newLevel);

        /// <summary>Can this still be offered: not yet capped, and its requirement is met.</summary>
        public bool IsOffered(RunState run)
        {
            if (run.BoonLevel(Id) >= MaxLevel) return false;
            return Requirement == null || Requirement.IsMet(run);
        }

        public Color RarityColor => Rarities.Tint(Rarity);

        /// <summary>What the effect chain actually does, for tooling and for spotting typos.</summary>
        public string EffectSummary() => BoonRunner.Describe(Effects);

        /// <summary>Card subtitle: shows whether this is a new pick or a level-up.</summary>
        public string LevelLabel(RunState run)
        {
            int level = run.BoonLevel(Id);
            if (level <= 0) return MaxLevel > 1 ? "NEW  -  max level " + MaxLevel : "NEW";
            return string.Format("LEVEL {0} to {1}", level, Mathf.Min(level + 1, MaxLevel));
        }

        public Boon Clone()
        {
            var copy = (Boon)MemberwiseClone();

            // The effect list is shared by MemberwiseClone. Effects hold only parameters and
            // are never mutated, so sharing the instances is fine - but the list itself must
            // not be, or authoring one boon would edit another's chain.
            copy.Effects = new List<BoonEffect>(Effects);
            return copy;
        }
    }
}
