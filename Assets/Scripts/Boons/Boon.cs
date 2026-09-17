using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// One upgrade offered between rooms. Boons write into <see cref="RunState"/>, onto the
    /// character sheet, or start a <see cref="BoonBehaviour"/> that runs for the rest of the run.
    ///
    /// A boon can be taken more than once. Each pick raises its level and runs
    /// <see cref="Effects"/> again with the new level, so an effect applies its own per-level
    /// increment rather than recomputing a total.
    ///
    /// A taken boon is permanent. Losing the spell or gun it acts on leaves it working as
    /// written with nothing to act on; its <see cref="Requirements"/> only decide what is offered.
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

        /// <summary>Which part of a build this hangs off. An offer rolls a family before a rarity.</summary>
        public BoonFamily Family = BoonFamily.Core;

        /// <summary>A heading within the family (Stat, Damage, Familiar...). For tooling and the UI only.</summary>
        public string Group = string.Empty;

        /// <summary>
        /// Shown on the draft cards and in the character sheet. Leave empty and the UI draws a
        /// tinted placeholder. Assign it on the boon asset - a boon built in code cannot
        /// reference one.
        /// </summary>
        public Texture2D Icon;

        /// <summary>
        /// How many times it can be taken. One means it is a one-off. A taken boon is never
        /// removed from the run, so a one-off that is used up (Charge Up) is still never offered again.
        /// </summary>
        public int MaxLevel = 3;

        /// <summary>
        /// Labels other boons can count or exclude: "familiar" for the one-familiar limit, "health"
        /// for what Cocky rules out.
        /// </summary>
        public List<string> Tags = new List<string>();

        /// <summary>What taking it does. Runs in order on every pick.</summary>
        [SerializeReference] public List<BoonEffect> Effects = new List<BoonEffect>();

        /// <summary>Gates on being offered. All of them must pass.</summary>
        [SerializeReference] public List<BoonRequirement> Requirements = new List<BoonRequirement>();

        public void Apply(RunState run, int newLevel) => BoonRunner.Apply(Effects, run, newLevel);

        public bool HasTag(string tag) => !string.IsNullOrEmpty(tag) && Tags != null && Tags.Contains(tag);

        /// <summary>Can this still be offered: not yet capped, and every requirement is met.</summary>
        public bool IsOffered(RunState run)
        {
            if (run == null || run.BoonLevel(Id) >= MaxLevel) return false;
            return RequirementsMet(run);
        }

        public bool RequirementsMet(RunState run)
        {
            if (Requirements == null) return true;

            for (int i = 0; i < Requirements.Count; i++)
                if (Requirements[i] != null && !Requirements[i].IsMet(run)) return false;
            return true;
        }

        public Color RarityColor => Rarities.Tint(Rarity);

        /// <summary>What the effect chain actually does, for tooling and for spotting typos.</summary>
        public string EffectSummary() => BoonRunner.Describe(Effects);

        /// <summary>The gates, for tooling. Empty when there are none.</summary>
        public string RequirementSummary()
        {
            if (Requirements == null) return string.Empty;

            string text = string.Empty;
            for (int i = 0; i < Requirements.Count; i++)
            {
                if (Requirements[i] == null) continue;
                if (text.Length > 0) text += ", ";
                text += Requirements[i].Describe();
            }
            return text;
        }

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

            // The lists are shared by MemberwiseClone. Effects and requirements hold only
            // parameters and are never mutated, so sharing the instances is fine - but the lists
            // themselves must not be, or authoring one boon would edit another's.
            copy.Effects = new List<BoonEffect>(Effects);
            copy.Requirements = new List<BoonRequirement>(Requirements);
            copy.Tags = new List<string>(Tags);
            return copy;
        }
    }
}
