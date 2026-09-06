using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// One playable opening: a stat spread, a gun and a pair of spells. Plain data, so the
    /// built-in roster can live in code and an authored asset can carry the same shape.
    /// </summary>
    [System.Serializable]
    public class LoadoutDefinition
    {
        public string Id = "loadout";
        public string DisplayName = "Wizard";

        /// <summary>One line on what this build wants you to do.</summary>
        public string Description = string.Empty;

        [Header("Core stats")]
        public int Strength = 5;
        public int Intellect = 5;
        public int Agility = 5;
        public int Vitality = 5;
        public int Luck = 5;

        [Header("Equipment")]
        [Tooltip("Id from WeaponLibrary. This gun is also excluded from world drops.")]
        public string WeaponId = "arcanum";

        [Tooltip("Id from MovementAbilityLibrary. Sits on Shift. Everyone opens with Dash.")]
        public string MovementAbilityId = MovementAbilityLibrary.DefaultId;

        [Tooltip("The one spell a run opens with, bound to E. Q starts empty and is filled at a shrine.")]
        public string SpellId = "cone_of_cold";

        [Tooltip("Which slot the starting spell goes in. 0 is Q, 1 is E.")]
        public int SpellSlot = 1;

        /// <summary>Card colour on the selection screen. Leave clear to take the gun's school.</summary>
        public Color TintOverride = Color.clear;

        public int StatFor(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return Strength;
                case StatType.Intellect: return Intellect;
                case StatType.Agility: return Agility;
                case StatType.Vitality: return Vitality;
                default: return Luck;
            }
        }

        public int TotalStatPoints => Strength + Intellect + Agility + Vitality + Luck;

        public Color Tint
        {
            get
            {
                if (TintOverride.a > 0f) return TintOverride;
                WeaponDefinition gun = WeaponLibrary.Peek(WeaponId);
                return gun != null ? DamageTypes.Tint(gun.DamageType) : Palette.Arcane;
            }
        }

        /// <summary>Human-readable stat line for the selection card.</summary>
        public string StatLine()
        {
            return string.Format("STR {0}   INT {1}   AGI {2}   VIT {3}   LCK {4}",
                Strength, Intellect, Agility, Vitality, Luck);
        }

        public LoadoutDefinition Clone() => (LoadoutDefinition)MemberwiseClone();
    }
}
