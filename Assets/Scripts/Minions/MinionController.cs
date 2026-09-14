using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A walking minion: zombies, the Stitched Monstrosity, Bestial's beasts. Enemy movement on the
    /// player's team - a character controller and the flow field - because following the player
    /// through a maze needs a body and a route, which floating familiars deliberately do without.
    ///
    /// It follows the player's body, fights the nearest enemy it can see or is close to, and runs the
    /// same <see cref="AbilityAttack"/> chains everything else does. On the minion layer it blocks
    /// enemies and other minions but never the player, and it registers with
    /// <see cref="TargetRegistry"/>, so enemies fight it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MinionController : MonoBehaviour, IAbilityOwner, IKnockable
    {
        private static readonly List<MinionController> LiveList = new List<MinionController>();

        /// <summary>Every minion out, knocked-down ones included.</summary>
        public static IReadOnlyList<MinionController> Live
        {
            get
            {
                LiveList.RemoveAll(m => m == null);
                return LiveList;
            }
        }

        private const float Acceleration = 24f;
        private const float Gravity = -22f;
        private const float TurnSpeed = 9f;
        private const float SeparationRadius = 1.2f;
        private const float RetargetInterval = 0.5f;

        /// <summary>Within this it fights something it cannot see, as when enemies press against a doorway it fills.</summary>
        private const float CloseEnough = 4f;

        public MinionDefinition Definition { get; private set; }

        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        public Team Team => Team.Player;
        public DamageOrigin AttackOrigin => DamageOrigin.Minion;

        public Transform Muzzle { get; set; }
        public Transform Target { get; private set; }

        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());
        public CharacterSheet Sheet => _sheet != null ? _sheet : (_sheet = GetComponent<CharacterSheet>());
        public StatusController Status => _status != null ? _status : (_status = GetComponent<StatusController>());

        /// <summary>Knocked down and waiting to get back up. Out of the fight, and out of enemies' choices.</summary>
        public bool IsDown { get; private set; }

        public Vector3 Velocity => _velocity + _knockback;

        public Vector3 Knockback
        {
            get => _knockbackActive ? _knockback : Vector3.zero;
            set => _knockback = value;
        }

        public GameObject Instigator { get; private set; }
        public Team InstigatorTeam { get; private set; }

        private Health _health;
        private CharacterSheet _sheet;
        private StatusController _status;
        private CharacterController _controller;
        private readonly List<AbilityAttack> _attacks = new List<AbilityAttack>();
        private TargetRegistry.Entry _entry;

        private Vector3 _velocity;
        private Vector3 _knockback;
        private bool _knockbackActive;
        private float _retargetTimer;
        private float _downTimer;

        public bool IsAttacking
        {
            get
            {
                for (int i = 0; i < _attacks.Count; i++)
                    if (_attacks[i].IsExecuting) return true;
                return false;
            }
        }

        /// <summary>Binds the definition, the attacks and the registration. The summoner calls this once the body is built.</summary>
        public void Initialise(MinionDefinition def)
        {
            Definition = def;
            _controller = GetComponent<CharacterController>();
            if (Muzzle == null) Muzzle = transform;

            GetComponents(_attacks);
            for (int i = 0; i < _attacks.Count; i++) _attacks[i].Initialise(this);

            if (Health != null)
            {
                Health.Died += OnDied;
                Health.Damaged += OnDamaged;
            }

            _entry = TargetRegistry.RegisterMinion(transform, Health);
            if (!LiveList.Contains(this)) LiveList.Add(this);
        }

        private void OnDestroy()
        {
            LiveList.Remove(this);
            if (_entry != null) TargetRegistry.Unregister(_entry);

            if (Health != null)
            {
                Health.Died -= OnDied;
                Health.Damaged -= OnDamaged;
            }
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (info.Knockback.sqrMagnitude > 0.01f) AddKnockback(info.Knockback, info.Source, info.SourceTeam);
        }

        private void Update()
        {
            if (WorldClock.IsStopped) return;
            Step(WorldClock.DeltaTime);
        }

        /// <summary>One frame of behaviour. Update calls it on world time; public so tooling can step a horde.</summary>
        public void Step(float dt)
        {
            if (Definition == null) return;

            if (IsDown)
            {
                _downTimer -= dt;
                if (_downTimer <= 0f) StandUp();
                return;
            }

            if (Health != null && !Health.IsAlive) return;

            bool impaired = Status != null && Status.IsControlImpaired;

            _retargetTimer -= dt;
            if (_retargetTimer <= 0f || !StillWorthFighting(Target)) AcquireTarget();

            Vector3 desired = impaired ? Vector3.zero : DesiredMove();

            if (!impaired)
            {
                Face(desired, dt);
                TryAttack();
            }

            ApplyMotion(desired, dt);
        }

        // ---------------------------------------------------------------- targeting

        public void RefreshTarget() => AcquireTarget();

        private static bool StillWorthFighting(Transform target)
        {
            if (target == null) return true;

            Health health = target.GetComponent<Health>();
            if (health == null || !health.IsAlive) return false;

            EnemyController enemy = target.GetComponent<EnemyController>();
            return enemy == null || !enemy.IsHidden;
        }

        /// <summary>
        /// The nearest enemy in range that it can see, or that is close enough to fight regardless.
        /// There is no route to anything but the player's body, so a minion fights what is near and
        /// otherwise follows, as the design settled.
        /// </summary>
        private void AcquireTarget()
        {
            _retargetTimer = RetargetInterval;
            if (Definition == null) return;

            Collider[] found = Physics.OverlapSphere(transform.position, Definition.EngageRange,
                Layers.EnemyMask, QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestDistance = float.MaxValue;
            float heldDistance = -1f;

            for (int i = 0; i < found.Length; i++)
            {
                Health candidate = found[i].GetComponentInParent<Health>();
                if (candidate == null || !candidate.IsAlive || candidate.Team == Team.Player) continue;

                Vector3 at = candidate.transform.position;
                float distance = Vector3.Distance(Flat(transform.position), Flat(at));
                if (distance > CloseEnough && !HasLineOfSightTo(at)) continue;

                if (candidate.transform == Target) heldDistance = distance;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate.transform;
                }
            }

            // The same stickiness enemies use, so a minion between two enemies does not flip.
            if (heldDistance >= 0f && bestDistance > heldDistance - TargetRegistry.StickyMargin) return;
            Target = best;
        }

        public bool HasLineOfSightTo(Vector3 position)
        {
            float eye = Definition != null ? Definition.BodyHeight * 0.8f : 1.3f;
            Vector3 from = transform.position + Vector3.up * eye;
            Vector3 delta = position + Vector3.up - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.25f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- fighting and moving

        private Vector3 DesiredMove()
        {
            Vector3 here = transform.position;
            Vector3 desired = Vector3.zero;

            if (Target != null)
            {
                Vector3 to = Flat(Target.position - here);
                if (to.magnitude > Definition.PreferredRange) desired = to.normalized;
            }
            else
            {
                Transform body = TargetRegistry.PlayerBody.Transform;
                if (body != null)
                {
                    Vector3 to = Flat(body.position - here);
                    if (to.magnitude > Definition.FollowDistance)
                    {
                        // The flow field leads to the player's body, which is exactly what a follower wants.
                        NavField field = NavField.Current;
                        Vector3 route = field != null && field.IsBuilt && !field.IsClearLine(here, body.position)
                            ? field.FlowDirection(here)
                            : Vector3.zero;

                        desired = route.sqrMagnitude > 0.001f ? route : to.normalized;
                    }
                }
            }

            desired += Separation() + Hazards.PushAt(here, Team.Player);
            desired = AvoidWalls(desired);

            if (desired.sqrMagnitude > 1f) desired.Normalize();
            if (IsAttacking) desired *= 0.25f;
            return desired;
        }

        private void Face(Vector3 desired, float dt)
        {
            Vector3 look = Target != null ? Flat(Target.position - transform.position) : Flat(desired);
            if (look.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(look.normalized, Vector3.up), TurnSpeed * dt);
        }

        private void TryAttack()
        {
            if (Target == null || IsAttacking) return;

            float distance = Vector3.Distance(Flat(transform.position), Flat(Target.position));
            bool los = HasLineOfSightTo(Target.position);

            AbilityAttack best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < _attacks.Count; i++)
            {
                AbilityAttack attack = _attacks[i];
                if (Status != null && !Status.CanAttack(attack.Reach)) continue;
                if (!attack.CanUse(distance, los) || attack.Priority <= bestPriority) continue;

                bestPriority = attack.Priority;
                best = attack;
            }

            if (best != null) best.Begin();
        }

        private void ApplyMotion(Vector3 desired, float dt)
        {
            if (_controller == null || !_controller.enabled) return;

            float speed = Sheet != null ? Sheet.Get(Attr.MoveSpeed) : Definition.MoveSpeed;

            Vector3 horizontal = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z),
                desired * speed, Acceleration * dt);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
            _velocity.y = _controller.isGrounded && _velocity.y < 0f ? -2f : _velocity.y + Gravity * dt;

            _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, 18f * dt);
            if (_knockback.sqrMagnitude < KnockbackImpacts.Threshold * KnockbackImpacts.Threshold) _knockbackActive = false;

            _controller.Move((_velocity + _knockback) * dt);
        }

        public void AddKnockback(Vector3 velocity, GameObject instigator, Team instigatorTeam)
        {
            if (velocity.sqrMagnitude < 0.0001f) return;

            _knockback += velocity;
            _knockbackActive = true;
            Instigator = instigator;
            InstigatorTeam = instigatorTeam;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit) => KnockbackImpacts.OnControllerHit(this, hit);

        private Vector3 Separation()
        {
            Collider[] neighbours = Physics.OverlapSphere(transform.position, SeparationRadius,
                Layers.MinionMask, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbours[i].transform == transform) continue;

                Vector3 away = Flat(transform.position - neighbours[i].transform.position);
                float distance = away.magnitude;
                away = distance < 0.01f ? Flat(Random.insideUnitSphere) : away / distance;
                push += away * (1f - Mathf.Clamp01(distance / SeparationRadius));
            }
            return push * 0.8f;
        }

        private Vector3 AvoidWalls(Vector3 desired)
        {
            if (desired.sqrMagnitude < 0.001f) return desired;

            Vector3 direction = desired.normalized;
            if (Physics.Raycast(transform.position + Vector3.up * 0.9f, direction, out RaycastHit hit, 1.6f,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                return Vector3.ProjectOnPlane(direction, hit.normal).normalized * desired.magnitude;

            return desired;
        }

        // ---------------------------------------------------------------- down and up

        private void OnDied(DamageInfo info)
        {
            foreach (AbilityAttack attack in GetComponents<AbilityAttack>()) attack.Cancel();

            // Without a revive timer the death is final, and Health removes the body.
            if (Definition == null || Definition.ReviveSeconds <= 0f) return;

            IsDown = true;
            _downTimer = Definition.ReviveSeconds;

            if (_entry != null)
            {
                TargetRegistry.Unregister(_entry);
                _entry = null;
            }

            SetBodyActive(false);
        }

        private void StandUp()
        {
            IsDown = false;
            SetBodyActive(true);

            if (Health != null) Health.Revive(Health.Max * Mathf.Clamp01(Definition.ReviveHealthFraction));
            _entry = TargetRegistry.RegisterMinion(transform, Health);
        }

        private void SetBodyActive(bool active)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = active;
            foreach (Collider body in GetComponentsInChildren<Collider>(true)) body.enabled = active;
            foreach (AbilityAttack attack in GetComponents<AbilityAttack>()) attack.enabled = active;

            Target = null;
            _velocity = Vector3.zero;
            _knockback = Vector3.zero;
        }

        public static void DespawnAll()
        {
            for (int i = LiveList.Count - 1; i >= 0; i--)
                if (LiveList[i] != null) Destroy(LiveList[i].gameObject);

            LiveList.Clear();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
