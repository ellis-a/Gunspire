using System;
using UnityEngine;

namespace WizardGun
{
    public enum BoonRarity { Common, Uncommon, Rare }

    /// <summary>
    /// One upgrade offered between rooms. Boons write into <see cref="RunState"/> or straight
    /// onto the character sheet; nothing else needs to know they exist.
    /// </summary>
    public class Boon
    {
        public string Id;
        public string Name;
        public string Description;
        public BoonRarity Rarity = BoonRarity.Common;
        public Color Tint = Palette.Arcane;

        /// <summary>Applied when the player picks it.</summary>
        public Action<RunState> Effect;

        /// <summary>Optional gate, e.g. a Blink upgrade only shows if Blink is known.</summary>
        public Func<RunState, bool> Requirement;

        /// <summary>Boons that should only ever be taken once.</summary>
        public bool Unique = true;

        public void Apply(RunState run) => Effect?.Invoke(run);

        public bool IsOffered(RunState run)
        {
            if (Unique && run.HasBoon(Id)) return false;
            return Requirement == null || Requirement(run);
        }

        public Color RarityColor
        {
            get
            {
                switch (Rarity)
                {
                    case BoonRarity.Rare: return new Color(1f, 0.72f, 0.35f);
                    case BoonRarity.Uncommon: return new Color(0.55f, 0.8f, 1f);
                    default: return new Color(0.82f, 0.84f, 0.9f);
                }
            }
        }
    }
}
