using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Enemy brain and body. Keeps its preferred distance from the player, strafes so it is
    /// not a static target, and hands off to whichever <see cref="AbilityAttack"/> is in range
    /// and off cooldown. No navmesh: rooms are open arenas and steering is enough.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : MonoBehaviour, IAbilityOwner
    {
        // IAbilityOwner. MonoBehaviour already has gameObject and transform in lower case;
        // these just expose them under the interface's names.
        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        public Team Team => Team.Enemy;

        [Header("Identity")]
        public string DisplayName = "Cultist";

        [Header("Spacing")]
        public float PreferredRange = 12f;
        public float MinComfortRange = 7f;
        public float LeashRange = 60f;

        [Header("Movement")]
        public float Acceleration = 24f;
        public float TurnSpeed = 9f;
        public float Gravity = -22f;
        public float StrafeInterval = 1.8f;
        public float SeparationRadius = 1.6f;
        public float MoveSpeedWhileAttacking = 0.25f;

        [Header("Flight")]
        /// <summary>Ignores gravity and holds an altitude above whatever is beneath it.</summary>
        public bool Flying;
        public float HoverHeight = 3.2f;
        public float HoverBob = 0.3f;
        public float HoverBobSpeed = 1.5f;
        public float ClimbAcceleration = 16f;

        /// <summary>
        /// Where this enemy sees and shoots from. Ground archetypes are tall and look out of
        /// their heads; a flier is small and looks out of its middle.
        /// </summary>
        public float EyeHeight = 1.35f;

        public Health Health { get; private set; }
        public CharacterSheet Sheet { get; private set; }
        public StatusController Status { get; private set; }
        public Transform Target { get; private set; }

        /// <summary>Set by the factory once the body is built. A property so it satisfies
        /// <see cref="IAbilityOwner"/>; enemies have no prefab, so nothing serializes it.</summary>
        public Transform Muzzle { get; set; }

        private CharacterController _controller;
        private readonly List<AbilityAttack> _attacks = new List<AbilityAttack>();
        private Vector3 _velocity;
        private Vector3 _externalVelocity;
        private float _strafeTimer;
        private int _strafeSign = 1;
        private float _retargetTimer;
        private float _bobPhase;

        public bool IsAttacking
        {
            get
            {
                for (int i = 0; i < _attacks.Count; i++)
                    if (_attacks[i].IsExecuting) return true;
                return false;
            }
        }

        public float DistanceToTarget => Target == null
            ? float.MaxValue
            : Vector3.Distance(Flat(transform.position), Flat(Target.position));

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Health = GetComponent<Health>();
            Sheet = GetComponent<CharacterSheet>();
            Status = GetComponent<StatusController>();

            if (Muzzle == null) Muzzle = transform;
            _strafeSign = Random.value < 0.5f ? -1 : 1;
            _strafeTimer = Random.Range(0f, StrafeInterval);

            // Offset per enemy, otherwise a group of fliers bobs in perfect unison.
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Start()
        {
            GetComponents(_attacks);
            for (int i = 0; i < _attacks.Count; i++) _attacks[i].Initialise(this);

            if (Health != null) Health.Damaged += OnDamaged;
            AcquireTarget();
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (info.Knockback.sqrMagnitude > 0.01f)
                _externalVelocity += info.Knockback;
        }

        private void Update()
        {
            if (Health != null && !Health.IsAlive) return;

            _retargetTimer -= Time.deltaTime;
            if (Target == null || _retargetTimer <= 0f) AcquireTarget();

            bool impaired = Status != null && Status.IsControlImpaired;

            if (!impaired)
            {
                FaceTarget();
                TryAttack();
            }

            Move(impaired);
        }

        private void AcquireTarget()
        {
            _retargetTimer = 1f;
            PlayerRig player = PlayerRig.Instance;
            Target = player != null && player.Health != null && player.Health.IsAlive
                ? player.transform
                : null;
        }

        // ---------------------------------------------------------------- attacking

        private void TryAttack()
        {
            if (Target == null || IsAttacking) return;

            float distance = DistanceToTarget;
            bool los = HasLineOfSight();

            // Highest priority ready attack wins; ties fall back to declaration order.
            AbilityAttack best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < _attacks.Count; i++)
            {
                AbilityAttack attack = _attacks[i];
                if (!attack.CanUse(distance, los)) continue;
                if (attack.Priority > bestPriority)
                {
                    bestPriority = attack.Priority;
                    best = attack;
                }
            }

            if (best != null) best.Begin();
        }

        public bool HasLineOfSight()
        {
            if (Target == null) return false;

            Vector3 from = EyePosition;
            Vector3 to = Target.position + Vector3.up * 1.0f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.25f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }

        public Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;

        public Vector3 AimDirection
        {
            get
            {
                if (Target == null) return transform.forward;
                Vector3 aimAt = Target.position + Vector3.up * 0.95f;
                return (aimAt - MuzzlePosition).normalized;
            }
        }

        public Vector3 MuzzlePosition => Muzzle != null ? Muzzle.position : EyePosition;

        // ---------------------------------------------------------------- movement

        private void FaceTarget()
        {
            if (Target == null) return;

            // A flier looks at the player properly rather than only turning on the spot. It
            // hovers above them, so a flattened stare would point at the floor beyond them -
            // and for something whose whole read is where it is looking, that matters.
            Vector3 to = Flying
                ? (Target.position + Vector3.up * 0.95f) - EyePosition
                : Flat(Target.position) - Flat(transform.position);

            if (to.sqrMagnitude < 0.01f) return;

            Quaternion wanted = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, TurnSpeed * Time.deltaTime);
        }

        private void Move(bool impaired)
        {
            float dt = Time.deltaTime;
            Vector3 desired = Vector3.zero;

            if (!impaired && Target != null)
            {
                float distance = DistanceToTarget;
                Vector3 toTarget = Flat(Target.position) - Flat(transform.position);
                Vector3 forward = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);

                _strafeTimer -= dt;
                if (_strafeTimer <= 0f)
                {
                    _strafeTimer = StrafeInterval * Random.Range(0.7f, 1.4f);
                    _strafeSign = -_strafeSign;
                }

                if (distance > PreferredRange) desired += forward;
                else if (distance < MinComfortRange) desired -= forward;
                else desired += right * _strafeSign * 0.9f;

                desired += right * (_strafeSign * 0.35f);
                desired += Separation();
                desired = AvoidWalls(desired);

                if (desired.sqrMagnitude > 1f) desired.Normalize();
                if (IsAttacking) desired *= MoveSpeedWhileAttacking;
            }

            float speed = Sheet != null ? Sheet.Get(Attr.MoveSpeed) : 4.5f;
            Vector3 wanted = desired * speed;

            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, wanted, Acceleration * dt);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;

            if (Flying) Hover(dt);
            else if (_controller.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            else _velocity.y += Gravity * dt;

            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, 18f * dt);

            _controller.Move((_velocity + _externalVelocity) * dt);
        }

        /// <summary>
        /// Holds station above whatever is below, rather than falling. Seeks the altitude
        /// through velocity instead of snapping to it, so knockback can still shove a flier
        /// around and it visibly recovers - which is most of what sells the thing as floating.
        /// </summary>
        private void Hover(float dt)
        {
            _bobPhase += dt * HoverBobSpeed;

            // Enemies are not in BlockingMask, so this cannot hit the flier itself.
            float groundY = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
                out RaycastHit below, HoverHeight + 30f, Layers.BlockingMask, QueryTriggerInteraction.Ignore)
                ? below.point.y
                : transform.position.y - HoverHeight;   // nothing underneath: keep the current altitude

            float wantedY = groundY + HoverHeight + Mathf.Sin(_bobPhase) * HoverBob;
            float error = wantedY - transform.position.y;

            float wantedRise = Mathf.Clamp(error * 4f, -7f, 7f);
            _velocity.y = Mathf.MoveTowards(_velocity.y, wantedRise, ClimbAcceleration * dt);
        }

        /// <summary>Used by lunges and knockback.</summary>
        public void AddImpulse(Vector3 impulse) => _externalVelocity += impulse;

        /// <summary>Keeps a pack from collapsing into one point.</summary>
        private Vector3 Separation()
        {
            Collider[] neighbours = Physics.OverlapSphere(transform.position, SeparationRadius,
                Layers.EnemyMask, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbours[i].transform == transform) continue;
                Vector3 away = Flat(transform.position) - Flat(neighbours[i].transform.position);
                float distance = away.magnitude;
                if (distance < 0.01f) away = Random.insideUnitSphere;
                else away /= distance;
                push += away * (1f - Mathf.Clamp01(distance / SeparationRadius));
            }
            return push * 0.8f;
        }

        /// <summary>Cheap whisker avoidance so enemies slide along walls instead of grinding into them.</summary>
        private Vector3 AvoidWalls(Vector3 desired)
        {
            if (desired.sqrMagnitude < 0.001f) return desired;

            Vector3 origin = transform.position + Vector3.up * 0.9f;
            Vector3 direction = desired.normalized;
            const float probe = 1.9f;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, probe,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal).normalized;
                return slide * desired.magnitude;
            }
            return desired;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
