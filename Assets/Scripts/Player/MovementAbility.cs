using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Instant abilities fire once and go on cooldown. Sustained ones stay on until you switch
    /// them off, you run dry, or their condition breaks.
    /// </summary>
    public enum MovementMode { Instant, Sustained }

    /// <summary>
    /// What sits on Shift. Movement gets its own slot rather than being a third spell,
    /// because a toggle that drains mana for as long as it is held is a different shape from
    /// cast-once-and-wait.
    ///
    /// Instant abilities run an <see cref="AbilityEffect"/> chain, so they share every effect
    /// spells and enemy attacks use. Sustained ones are described by the four knobs below,
    /// each mapping to one capability on the motor or the character sheet.
    /// </summary>
    public class MovementAbility
    {
        public string Id = "movement";
        public string DisplayName = "Movement";
        public string ShortName = "MOVE";
        public string Description = string.Empty;

        public MovementMode Mode = MovementMode.Instant;
        public Rarity Rarity = Rarity.Common;
        public Color Tint = Palette.Arcane;

        // ---- instant ----
        public float Cooldown = 0f;
        public float ManaCost = 0f;

        /// <summary>Charge-based instead of cooldown-based. Dash reads its charges off Agility.</summary>
        public bool UsesDashCharges;

        /// <summary>What happens when it fires. Aborting refunds the cost, as with spells.</summary>
        [SerializeReference] public List<AbilityEffect> OnActivate = new List<AbilityEffect>();

        // ---- sustained ----
        public float ManaPerSecond = 0f;

        /// <summary>Seconds it can stay up. Zero means until the mana runs out.</summary>
        public float MaxDuration = 0f;

        /// <summary>Percent bonus applied to move speed while active.</summary>
        public float MoveSpeedBonus = 0f;

        /// <summary>Ignores damage while active.</summary>
        public bool Invulnerable;

        /// <summary>Drops the moment the caster moves. Pairs with <see cref="Invulnerable"/>.</summary>
        public bool BreakOnMovement;

        /// <summary>Lets the caster cling to and run along walls.</summary>
        public bool WallCling;

        public bool IsSustained => Mode == MovementMode.Sustained;

        /// <summary>One line for the HUD and the pedestal prompt.</summary>
        public string CostLine()
        {
            if (IsSustained)
            {
                string duration = MaxDuration > 0f ? ", up to " + MaxDuration.ToString("0.#") + "s" : "";
                return ManaPerSecond.ToString("0") + " mana/s" + duration;
            }

            if (UsesDashCharges) return "charges with Agility";
            string mana = ManaCost > 0f ? Mathf.RoundToInt(ManaCost) + " mana, " : "";
            return mana + Cooldown.ToString("0.#") + "s cooldown";
        }
    }
}
