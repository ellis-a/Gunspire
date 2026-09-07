using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// One attack an enemy can perform. This component owns only the *usage rules* - range
    /// band, cooldown, priority, line of sight. What the attack actually does is a chain of
    /// <see cref="AbilityEffect"/>s, the same ones player spells are built from.
    ///
    /// Every attack stays dodgeable by construction: a travelling projectile, a shape
    /// telegraphed before it fires, or a swing with a visible wind-up that can be kited. Those
    /// properties come from the effects the chain is built out of, not from this class.
    /// </summary>
    public class AbilityAttack : MonoBehaviour
    {
        [Header("Usage")]
        public string Name = "Attack";
        public float MinRange = 0f;
        public float MaxRange = 20f;
        public float Cooldown = 3f;
        public float InitialDelay = 0.6f;
        public bool RequiresLineOfSight = true;
        public int Priority = 0;

        [Header("Ability")]
        public DamageType DamageType = DamageType.Astral;
        public SpellType Category = SpellType.Attack;
        public Color Tint = Palette.Arcane;

        /// <summary>What happens, in order. Wind-ups and repeats are effects like any other.</summary>
        [SerializeReference] public List<AbilityEffect> Sequence = new List<AbilityEffect>();

        /// <summary>
        /// Scales every damage number in the sequence. Floor difficulty and the elite bonus
        /// arrive here rather than being multiplied into the authored values, so what a
        /// definition says an attack does is what it does on floor one.
        /// </summary>
        public float DamageMultiplier = 1f;

        private IAbilityOwner _owner;
        private AbilityContext _context;
        private float _timer;

        public bool IsExecuting { get; private set; }
        public float CooldownRemaining => _timer;

        /// <summary>
        /// Binds this attack to whatever is using it. Taking an interface rather than an
        /// EnemyController is what lets a familiar run the same chains on the player's team -
        /// the attack does not care which side of the fight it is on.
        /// </summary>
        public void Initialise(IAbilityOwner owner)
        {
            _owner = owner;
            _timer = InitialDelay;

            _context = new AbilityContext
            {
                Caster = owner.GameObject,
                Team = owner.Team,
                Sheet = owner.Sheet,
                Health = owner.Health,
                Status = owner.Status,
                Aim = owner.Muzzle != null ? owner.Muzzle : owner.Transform
            };
        }

        private void Update()
        {
            if (_timer <= 0f) return;

            // Chill slows an enemy's attack rate as well as its feet.
            float rate = _owner != null && _owner.Sheet != null ? _owner.Sheet.Get(Attr.AttackSpeed) : 1f;
            _timer -= Time.deltaTime * rate;
        }

        public bool CanUse(float distance, bool hasLineOfSight)
        {
            if (IsExecuting || _timer > 0f || _owner == null || _owner.Target == null) return false;
            if (distance < MinRange || distance > MaxRange) return false;
            if (RequiresLineOfSight && !hasLineOfSight) return false;
            return true;
        }

        public void Begin()
        {
            IsExecuting = true;
            _timer = Cooldown;
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            _context.TargetTransform = _owner.Target;

            // levelScale feeds ctx.Power, which every damage-dealing effect already multiplies
            // by - so one number here scales the whole chain however it is built.
            _context.Begin(DamageType, Category, Tint, level: 1,
                levelScale: DamageMultiplier, isSpell: false);

            yield return AbilityRunner.RunTimed(Sequence, _context);

            _context.EndCast();
            IsExecuting = false;
        }

        // ---------------------------------------------------------------- construction

        /// <summary>Builds the component from authored data. Used by the enemy and familiar factories.</summary>
        public static AbilityAttack Add(GameObject host, AttackDefinition def)
        {
            var attack = host.AddComponent<AbilityAttack>();
            attack.Name = def.Name;
            attack.DamageType = def.DamageType;
            attack.Category = def.Category;
            attack.Tint = def.Tint;
            attack.MinRange = def.MinRange;
            attack.MaxRange = def.MaxRange;
            attack.Cooldown = def.Cooldown;
            attack.InitialDelay = def.InitialDelay;
            attack.RequiresLineOfSight = def.RequiresLineOfSight;
            attack.Priority = def.Priority;

            attack.Sequence = new List<AbilityEffect>(def.Sequence);
            return attack;
        }
    }
}
