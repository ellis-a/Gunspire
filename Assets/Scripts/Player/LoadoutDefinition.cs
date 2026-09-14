using UnityEngine;

namespace Gunspire
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

        // Stored by field name. These were renamed by the asset migration rather than with
        // FormerlySerializedAs on purpose: an unmigrated asset falls back to the baseline of 10,
        // instead of carrying across an old value that would now sit seven points too low.
        [Header("Core stats")]
        public int Dexterity = CharacterSheet.Baseline;
        public int Power = CharacterSheet.Baseline;
        public int Athletics = CharacterSheet.Baseline;
        public int Endurance = CharacterSheet.Baseline;
        public int Luck = CharacterSheet.Baseline;

        [Header("Equipment")]
        [Tooltip("Id from WeaponLibrary. This gun is also excluded from world drops.")]
        public string WeaponId = "arcanum";

        [Tooltip("Movement-slot spell id. Sits on Shift. Everyone opens with Dash.")]
        public string MovementAbilityId = SpellLibrary.DefaultMovementId;

        [Tooltip("Melee-slot spell id. Everyone opens with Bash.")]
        public string MeleeSpellId = SpellLibrary.DefaultMeleeId;

        [Tooltip("A cast spell the run opens with, in the slot below. Leave empty to open with none.")]
        public string SpellId = string.Empty;

        [Tooltip("Which slot the starting spell goes in. 0 is Q, 1 is E, 2 is F.")]
        public int SpellSlot = 1;

        /// <summary>Card colour on the selection screen. Leave clear to take the gun's school.</summary>
        public Color TintOverride = Color.clear;

        public int StatFor(StatType stat)
        {
            switch (stat)
            {
                case StatType.Dexterity: return Dexterity;
                case StatType.Power: return Power;
                case StatType.Athletics: return Athletics;
                case StatType.Endurance: return Endurance;
                default: return Luck;
            }
        }

        public int TotalStatPoints => Dexterity + Power + Athletics + Endurance + Luck;

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
            return string.Format("DEX {0}   POW {1}   ATH {2}   END {3}   LCK {4}",
                Dexterity, Power, Athletics, Endurance, Luck);
        }

        public LoadoutDefinition Clone() => (LoadoutDefinition)MemberwiseClone();
    }
}
