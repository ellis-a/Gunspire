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
    public class EnemyAttack : MonoBehaviour
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
        public List<AbilityEffect> Sequence = new List<AbilityEffect>();

        private EnemyController _owner;
        private AbilityContext _context;
        private float _timer;

        public bool IsExecuting { get; private set; }
        public float CooldownRemaining => _timer;

        public void Initialise(EnemyController owner)
        {
            _owner = owner;
            _timer = InitialDelay;

            _context = new AbilityContext
            {
                Caster = owner.gameObject,
                Team = Team.Enemy,
                Sheet = owner.Sheet,
                Health = owner.Health,
                Status = owner.Status,
                Aim = owner.Muzzle != null ? owner.Muzzle : owner.transform
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
            _context.Begin(DamageType, Category, Tint, level: 1, levelScale: 1f, isSpell: false);

            yield return AbilityRunner.RunTimed(Sequence, _context);

            _context.EndCast();
            IsExecuting = false;
        }

        // ---------------------------------------------------------------- construction

        /// <summary>Adds a configured attack to an enemy. Used by <see cref="EnemyFactory"/>.</summary>
        public static EnemyAttack Add(EnemyController enemy, string name, DamageType damageType, Color tint,
            float minRange, float maxRange, float cooldown, int priority, params AbilityEffect[] sequence)
        {
            var attack = enemy.gameObject.AddComponent<EnemyAttack>();
            attack.Name = name;
            attack.DamageType = damageType;
            attack.Tint = tint;
            attack.MinRange = minRange;
            attack.MaxRange = maxRange;
            attack.Cooldown = cooldown;
            attack.Priority = priority;

            attack.Sequence = new List<AbilityEffect>(sequence);
            return attack;
        }
    }
}
