using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>How the body is assembled from primitives. Add a case to EnemyFactory to add one.</summary>
    public enum BodyShape { Humanoid, Eyeball }

    /// <summary>
    /// One attack, as data. Mirrors the usage rules on <see cref="AbilityAttack"/>; the component
    /// is built from this at spawn.
    ///
    /// Damage inside the sequence is a base value at floor 1. Floor scaling is applied through
    /// the ability context rather than baked into these numbers, so what is authored here is
    /// what the attack is worth, not what it happens to be worth on floor three.
    /// </summary>
    [System.Serializable]
    public class AttackDefinition
    {
        public string Name = "Attack";

        [Header("Usage")]
        public float MinRange = 0f;
        public float MaxRange = 20f;
        public float Cooldown = 3f;
        public float InitialDelay = 0.6f;
        public bool RequiresLineOfSight = true;
        public int Priority;

        [Header("Ability")]
        public DamageType DamageType = DamageType.Astral;
        public SpellType Category = SpellType.Attack;
        public Color Tint = new Color(0.60f, 0.45f, 1.00f);

        /// <summary>What happens, in order. Wind-ups and repeats are effects like any other.</summary>
        [SerializeReference] public List<AbilityEffect> Sequence = new List<AbilityEffect>();

        public AttackDefinition Clone()
        {
            var copy = (AttackDefinition)MemberwiseClone();
            copy.Sequence = new List<AbilityEffect>(Sequence);
            return copy;
        }

        public string Summary() =>
            string.Format("{0,-18} {1,-7} {2,3}-{3,-3}m  cd {4,4}  pri {5}  {6}",
                Name, DamageTypes.Name(DamageType), MinRange.ToString("0"), MaxRange.ToString("0"),
                Cooldown.ToString("0.#"), Priority, AbilityRunner.Describe(Sequence));
    }

    /// <summary>
    /// One enemy archetype, as data. Everything <see cref="EnemyFactory"/> needs to assemble
    /// the GameObject: a chassis, a shape, an affinity and a list of attacks.
    /// </summary>
    [System.Serializable]
    public class EnemyDefinition
    {
        public string Id = "enemy";
        public string DisplayName = "Enemy";

        /// <summary>
        /// Whether ordinary combat rooms may roll this. Off for the boss, and the switch an
        /// authored enemy flips to start appearing without touching code.
        /// </summary>
        public bool InStandardRoster = true;

        [Header("Chassis")]
        public float Health = 55f;
        public float MoveSpeed = 4.2f;
        public float Radius = 0.42f;
        public float BodyHeight = 1.8f;
        public float BodyWidth = 0.8f;

        [Header("Behaviour")]
        public float PreferredRange = 12f;
        public float MinComfortRange = 7f;
        public float StrafeInterval = 1.8f;
        public float TurnSpeed = 9f;

        [Header("Flight")]
        public bool Flying;
        public float HoverHeight = 3.2f;

        /// <summary>Where it sees and shoots from. Zero takes a sensible guess from the body.</summary>
        public float EyeHeight;

        [Header("Look")]
        public BodyShape Shape = BodyShape.Humanoid;
        public Color BodyColor = new Color(0.55f, 0.25f, 0.55f);
        public Color EyeColor = new Color(1f, 0.5f, 1f);

        /// <summary>Wears the crown an elite would. Bosses are always crowned.</summary>
        public bool Crowned;

        [Header("Affinity")]
        [Tooltip("Damage school this shrugs off. Set Resistance to 0 to have no affinity.")]
        public DamageType Resists = DamageType.Astral;
        public float Resistance = 0.40f;

        [Tooltip("Damage school this is soft to.")]
        public DamageType WeakTo = DamageType.Nature;
        public float Vulnerability = 0.30f;

        [Tooltip("Resists every elemental school a little instead of the pairing above. For bosses.")]
        public bool ResistsEverything;
        public float BroadResistance = 0.18f;

        [Header("Elite")]
        public float EliteHealthMultiplier = 2.6f;
        public float EliteSpeedMultiplier = 1.08f;
        public float EliteDamageMultiplier = 1.25f;
        public float EliteCooldownMultiplier = 0.8f;

        [Header("Attacks")]
        public List<AttackDefinition> Attacks = new List<AttackDefinition>();

        /// <summary>Eye height, falling back to something reasonable for the body.</summary>
        public float ResolvedEyeHeight => EyeHeight > 0f ? EyeHeight : BodyHeight * 0.75f;

        public EnemyDefinition Clone()
        {
            var copy = (EnemyDefinition)MemberwiseClone();

            // Attacks are mutated per spawn - floor scaling and elite cooldowns - so both the
            // list and the entries have to be fresh, or spawning one enemy would retune the
            // roster for every enemy after it.
            copy.Attacks = new List<AttackDefinition>();
            for (int i = 0; i < Attacks.Count; i++)
                if (Attacks[i] != null) copy.Attacks.Add(Attacks[i].Clone());

            return copy;
        }

        public string StatLine() =>
            string.Format("{0,4:0} hp  {1,4:0.0} speed  {2}  range {3:0}-{4:0}m  {5} attack(s)",
                Health, MoveSpeed, Flying ? "flying " + HoverHeight.ToString("0.#") + "m" : "grounded",
                MinComfortRange, PreferredRange, Attacks.Count);
    }
}
