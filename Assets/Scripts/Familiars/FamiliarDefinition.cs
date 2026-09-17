using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A stat change a familiar grants its owner while it is alive.
    ///
    /// Deliberately not a <see cref="BoonEffect"/>, even though the shape is similar. Boon
    /// effects are permanent by design - taking one and losing it later is not a thing that
    /// happens - whereas a familiar's aura has to come off cleanly when it dies. These are
    /// applied with the familiar as the modifier source so removing them is one call.
    /// </summary>
    [System.Serializable]
    public class FamiliarAura
    {
        public Attr Attribute = Attr.DamageDealt;
        public ModifierMode Mode = ModifierMode.Percent;
        public float Amount = 0.10f;

        public string Describe()
        {
            string value = Mode == ModifierMode.Percent
                ? (Amount * 100f).ToString("+0.#;-0.#") + "%"
                : Amount.ToString("+0.##;-0.##");
            return value + " " + Attribute;
        }
    }

    /// <summary>Something a familiar does beyond its attacks and auras. Stored as an integer; append only.</summary>
    public enum FamiliarPerk
    {
        None,

        /// <summary>Targets the enemy nearest the player's crosshair rather than the nearest enemy. The Seer.</summary>
        CrosshairTarget,

        /// <summary>Destroys an enemy projectile near the player every interval. The Aegis Mote.</summary>
        BlockProjectiles,

        /// <summary>Draws orbs within range to the player. The Magpie.</summary>
        FetchOrbs,

        /// <summary>Restores mana each second while an enemy is within range of the player. The Mana Sprite.</summary>
        ManaNearEnemies,

        /// <summary>Raises the player's Luck while it lives. The Coin Imp.</summary>
        LuckAura,

        /// <summary>Enemies attack it as they would a minion. The Homunculus.</summary>
        Decoy,

        /// <summary>Puts rounds back in the player's holstered guns every interval. The Gremlin.</summary>
        ReloadHolstered
    }

    /// <summary>
    /// One familiar archetype, as data.
    ///
    /// A familiar is an ally that runs the same <see cref="AbilityEffect"/> chains enemies do,
    /// on the player's team. What it actually is - a gun turret, a healer, a walking buff - is
    /// decided entirely by what is in its attacks and auras rather than by a type flag.
    /// </summary>
    [System.Serializable]
    public class FamiliarDefinition
    {
        public string Id = "familiar";
        public string DisplayName = "Familiar";

        [TextArea(2, 4)]
        public string Description = string.Empty;

        [Header("Chassis")]
        public float Health = 60f;
        public float MoveSpeed = 7f;
        public float Radius = 0.35f;

        [Header("Positioning")]
        /// <summary>How far behind the player it tries to sit when there is nothing to fight.</summary>
        public float FollowDistance = 2.8f;

        /// <summary>Height above the player's feet. Familiars float; it keeps them out from underfoot.</summary>
        public float HoverHeight = 1.9f;

        /// <summary>
        /// How far from the player it will look for something to attack. Kept modest on
        /// purpose: a familiar that wanders off across the arena stops reading as yours.
        /// </summary>
        public float EngageRange = 18f;

        [Header("Look")]
        public float BodyDiameter = 0.6f;
        public Color BodyColor = new Color(0.60f, 0.45f, 1.00f);
        public Color EyeColor = new Color(1f, 0.9f, 0.6f);

        [Header("Abilities")]
        public List<AttackDefinition> Attacks = new List<AttackDefinition>();

        /// <summary>Granted to the owner while this is alive, and taken back when it dies.</summary>
        public List<FamiliarAura> Auras = new List<FamiliarAura>();

        [Header("Perk")]
        public FamiliarPerk Perk = FamiliarPerk.None;

        /// <summary>Seconds between a perk's actions, for perks that act on a timer.</summary>
        public float PerkInterval = 4f;

        /// <summary>How much a perk gives: mana a second, Luck, or the share of a magazine put back.</summary>
        public float PerkAmount = 1f;

        /// <summary>How far from the player a perk reaches.</summary>
        public float PerkRange = 12f;

        /// <summary>
        /// Multiplies every damage number in its attacks. Raised by the boon level, so taking
        /// the same familiar boon again makes the familiar you already have hit harder.
        /// </summary>
        public float DamageMultiplier = 1f;

        public FamiliarDefinition Clone()
        {
            var copy = (FamiliarDefinition)MemberwiseClone();

            copy.Attacks = new List<AttackDefinition>();
            for (int i = 0; i < Attacks.Count; i++)
                if (Attacks[i] != null) copy.Attacks.Add(Attacks[i].Clone());

            copy.Auras = new List<FamiliarAura>(Auras);
            return copy;
        }

        public string StatLine()
        {
            string auras = string.Empty;
            for (int i = 0; i < Auras.Count; i++)
                auras += (auras.Length > 0 ? ", " : string.Empty) + Auras[i].Describe();

            return string.Format("{0,3:0} hp  {1,4:0.0} speed  engage {2:0}m  {3} ability(s){4}",
                Health, MoveSpeed, EngageRange, Attacks.Count,
                auras.Length > 0 ? "  aura: " + auras : string.Empty);
        }
    }
}
