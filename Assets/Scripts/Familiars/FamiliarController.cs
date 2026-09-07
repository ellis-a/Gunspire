using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// A summoned ally. Floats near the player, picks the nearest enemy it can see, and runs
    /// the same <see cref="AbilityAttack"/> chains enemies use - on the player's team.
    ///
    /// Movement is written directly to the transform rather than through a CharacterController.
    /// A familiar has no business colliding with the level: it drifts, it does not walk, and
    /// giving it a physical body only creates ways for it to shove the player or get stuck on a
    /// crate. It still carries a collider so blasts and beams can kill it.
    /// </summary>
    public class FamiliarController : MonoBehaviour, IAbilityOwner
    {
        private static readonly List<FamiliarController> Live = new List<FamiliarController>();
        private static readonly Collider[] SearchBuffer = new Collider[48];

        public static IReadOnlyList<FamiliarController> All => Live;

        public FamiliarDefinition Definition { get; private set; }

        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        public Team Team => Team.Player;

        public Transform Muzzle { get; set; }
        public Transform Target { get; private set; }
        public CharacterSheet Sheet { get; private set; }
        public Health Health { get; private set; }
        public StatusController Status { get; private set; }

        private PlayerRig _owner;
        private readonly List<AbilityAttack> _attacks = new List<AbilityAttack>();
        private float _retargetTimer;
        private float _bobPhase;
        private bool _auraApplied;

        // ---- empowerment, driven by spells like Fel Empowerment ----
        private float _empowerTimer;
        private float _empowerDamage = 1f;
        private float _empowerHealthCost;

        public bool IsEmpowered => _empowerTimer > 0f;
        public float EmpowermentRemaining => _empowerTimer;

        public bool IsAttacking
        {
            get
            {
                for (int i = 0; i < _attacks.Count; i++)
                    if (_attacks[i].IsExecuting) return true;
                return false;
            }
        }

        public void Initialise(FamiliarDefinition def, PlayerRig owner)
        {
            Definition = def;
            _owner = owner;

            Sheet = GetComponent<CharacterSheet>();
            Health = GetComponent<Health>();
            Status = GetComponent<StatusController>();

            _bobPhase = Random.Range(0f, Mathf.PI * 2f);

            GetComponents(_attacks);
            for (int i = 0; i < _attacks.Count; i++)
            {
                _attacks[i].Initialise(this);
                _attacks[i].DamageMultiplier = def.DamageMultiplier;
            }

            if (Health != null)
            {
                Health.Died += OnDied;
                Health.Damaged += OnDamaged;
            }

            ApplyAura();
        }

        private void OnEnable() => Live.Add(this);

        private void OnDisable()
        {
            Live.Remove(this);

            // Covers being destroyed with the room as well as dying, so the aura can never
            // outlive the familiar that granted it.
            RemoveAura();
        }

        private void OnDestroy()
        {
            if (Health == null) return;
            Health.Died -= OnDied;
            Health.Damaged -= OnDamaged;
        }

        // ---------------------------------------------------------------- aura

        private void ApplyAura()
        {
            if (_auraApplied || Definition == null || _owner == null || _owner.Sheet == null) return;

            for (int i = 0; i < Definition.Auras.Count; i++)
            {
                FamiliarAura aura = Definition.Auras[i];
                if (aura == null) continue;

                if (aura.Mode == ModifierMode.Percent)
                    _owner.Sheet.AddPercent(aura.Attribute, aura.Amount, this, Definition.DisplayName);
                else
                    _owner.Sheet.AddFlat(aura.Attribute, aura.Amount, this, Definition.DisplayName);
            }

            _auraApplied = true;
        }

        private void RemoveAura()
        {
            if (!_auraApplied || _owner == null || _owner.Sheet == null) return;

            // Keyed on this component, so it lifts exactly this familiar's contribution even
            // with several of them granting the same attribute.
            _owner.Sheet.RemoveModifiersFrom(this);
            _auraApplied = false;
        }

        private void OnDied(DamageInfo info)
        {
            RemoveAura();

            Vector3 at = transform.position;
            Color tint = Definition != null ? Definition.BodyColor : Palette.Arcane;

            GameObject pop = Build.Sphere(null, "FamiliarFade", at, Definition != null ? Definition.BodyDiameter * 2f : 1f,
                MaterialLibrary.Transparent(new Color(tint.r, tint.g, tint.b, 0.5f)), collider: false);
            FadeAndDie.Attach(pop, 0.35f, new Color(tint.r, tint.g, tint.b, 0.5f), Vector3.one * 3f);

            if (Definition != null && GameDirector.Instance != null)
                GameDirector.Instance.Notify(Definition.DisplayName + " has fallen", 1.4f);

            Destroy(gameObject);
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            // Nothing to steer, but a familiar being chipped away should be visible.
            Sfx.PlayAt(SoundLibrary.Impact(info.Type), transform.position, 0.4f);
        }

        // ---------------------------------------------------------------- empowerment

        /// <summary>
        /// Applied by buff spells. Re-applying refreshes rather than stacks, so holding two
        /// copies of the same spell is not a damage multiplier.
        /// </summary>
        public void Empower(float damageMultiplier, float duration, float healthCostPerAttack)
        {
            _empowerTimer = Mathf.Max(_empowerTimer, duration);
            _empowerDamage = Mathf.Max(_empowerDamage, damageMultiplier);
            _empowerHealthCost = Mathf.Max(_empowerHealthCost, healthCostPerAttack);

            RefreshAttackDamage();
        }

        private void RefreshAttackDamage()
        {
            float scale = Definition != null ? Definition.DamageMultiplier : 1f;
            if (IsEmpowered) scale *= _empowerDamage;

            for (int i = 0; i < _attacks.Count; i++) _attacks[i].DamageMultiplier = scale;
        }

        /// <summary>
        /// The price of an empowered swing. Called by the familiar itself when an attack
        /// starts, so the cost tracks attacks made rather than seconds elapsed - which is what
        /// makes empowering a fast familiar in an empty room harmless and doing it mid-fight
        /// a real decision.
        /// </summary>
        private void PayForAttack()
        {
            if (!IsEmpowered || _empowerHealthCost <= 0f) return;
            if (_owner == null || _owner.Health == null || !_owner.Health.IsAlive) return;

            // Straight to Current rather than through TakeDamage: this is a price paid, not an
            // attack on the player, and it must not trigger resistances, statuses or lifesteal.
            _owner.Health.Drain(_empowerHealthCost);
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (_owner == null || Health == null || !Health.IsAlive) return;

            if (_empowerTimer > 0f)
            {
                _empowerTimer -= Time.deltaTime;
                if (_empowerTimer <= 0f)
                {
                    _empowerDamage = 1f;
                    _empowerHealthCost = 0f;
                    RefreshAttackDamage();
                }
            }

            _retargetTimer -= Time.deltaTime;
            if (Target == null || _retargetTimer <= 0f) AcquireTarget();

            Drift();
            FaceTarget();
            TryAttack();
        }

        private void AcquireTarget()
        {
            _retargetTimer = 0.4f;
            Target = null;

            if (_owner == null) return;

            // Searched around the player rather than around the familiar, so it never chases
            // something into the next room and leaves you alone.
            int count = Physics.OverlapSphereNonAlloc(_owner.transform.position, Definition.EngageRange,
                SearchBuffer, Layers.EnemyMask, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                IDamageable candidate = Combat.FindDamageable(SearchBuffer[i]);
                if (candidate == null || !candidate.IsAlive || candidate.Team != Team.Enemy) continue;

                float distance = Vector3.SqrMagnitude(candidate.Transform.position - transform.position);
                if (distance >= best) continue;

                best = distance;
                Target = candidate.Transform;
            }
        }

        /// <summary>Floats toward a slot beside and above the player, easing rather than snapping.</summary>
        private void Drift()
        {
            _bobPhase += Time.deltaTime * 2f;

            Transform player = _owner.transform;
            Vector3 anchor = player.position
                             + Vector3.up * Definition.HoverHeight
                             - player.forward * Definition.FollowDistance * 0.5f
                             + player.right * Definition.FollowDistance;

            anchor += Vector3.up * Mathf.Sin(_bobPhase) * 0.12f;

            // Speed rises with distance so it can catch up after a Blink without drifting at a
            // constant crawl the rest of the time.
            float gap = Vector3.Distance(transform.position, anchor);
            float speed = Definition.MoveSpeed * Mathf.Clamp(gap * 0.5f, 0.4f, 4f);

            transform.position = Vector3.MoveTowards(transform.position, anchor, speed * Time.deltaTime);
        }

        private void FaceTarget()
        {
            Vector3 lookAt = Target != null
                ? Target.position + Vector3.up * 0.9f
                : transform.position + _owner.transform.forward;

            Vector3 to = lookAt - transform.position;
            if (to.sqrMagnitude < 0.001f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to.normalized, Vector3.up), 8f * Time.deltaTime);
        }

        private void TryAttack()
        {
            if (Target == null || IsAttacking) return;

            float distance = Vector3.Distance(transform.position, Target.position);
            bool los = HasLineOfSight();

            AbilityAttack best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < _attacks.Count; i++)
            {
                AbilityAttack attack = _attacks[i];
                if (!attack.CanUse(distance, los)) continue;
                if (attack.Priority <= bestPriority) continue;

                bestPriority = attack.Priority;
                best = attack;
            }

            if (best == null) return;

            best.Begin();
            PayForAttack();
        }

        public bool HasLineOfSight()
        {
            if (Target == null) return false;

            Vector3 from = transform.position;
            Vector3 to = Target.position + Vector3.up * 0.9f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.25f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- static helpers

        /// <summary>Empowers every live familiar. Used by buff spells.</summary>
        public static int EmpowerAll(float damageMultiplier, float duration, float healthCostPerAttack)
        {
            for (int i = 0; i < Live.Count; i++)
                Live[i].Empower(damageMultiplier, duration, healthCostPerAttack);

            return Live.Count;
        }

        public static void DespawnAll()
        {
            // Backwards, because each Destroy pulls its entry out of the list on disable.
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] != null) Destroy(Live[i].gameObject);

            Live.Clear();
        }
    }
}
