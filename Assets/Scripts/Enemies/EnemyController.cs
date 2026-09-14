using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Enemy brain and body. Keeps its preferred distance from what it is fighting, strafes so it is
    /// not a static target, and hands off to whichever <see cref="AbilityAttack"/> is in range and off
    /// cooldown. No navmesh: rooms are open arenas and steering is enough.
    ///
    /// What it fights comes from <see cref="TargetRegistry"/>, and what it may do from its statuses:
    /// whether it can move, which attacks it may start, and whether it is running away instead.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : MonoBehaviour, IAbilityOwner
    {
        // IAbilityOwner. MonoBehaviour already has gameObject and transform in lower case;
        // these just expose them under the interface's names.
        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        public Team Team => _attackTeam;
        public DamageOrigin AttackOrigin => DamageOrigin.Attack;

        [Header("Identity")]
        public string DisplayName = "Cultist";

        /// <summary>What it was built from and on which floor, so it can be copied. Set by the factory.</summary>
        public EnemyDefinition Definition { get; set; }
        public int Floor { get; set; } = 1;

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

        [Header("Perception")]
        public IdleActivity Idle = IdleActivity.Stand;
        public float SightRange = 22f;
        public float SightHalfAngle = 60f;
        public float HearingRange = 16f;

        [Header("Idling")]
        [SerializeField] private float wanderRadius = 7f;
        [SerializeField] private float patrolRadius = 11f;
        [SerializeField] private float idlePauseSeconds = 2.2f;
        [SerializeField] private float idleSpeedFraction = 0.45f;
        [SerializeField] private float arriveDistance = 1.2f;

        /// <summary>
        /// Has something switched this on? Nothing switches it back off - once a room knows you
        /// are in it, it stays that way, which keeps a fight from resetting because you found a
        /// corner to stand in.
        /// </summary>
        public bool IsAlerted { get; private set; }

        // Resolved lazily as well as in Awake, so tooling can drive an enemy that never woke.
        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());
        public CharacterSheet Sheet => _sheet != null ? _sheet : (_sheet = GetComponent<CharacterSheet>());
        public StatusController Status => _status != null ? _status : (_status = GetComponent<StatusController>());

        /// <summary>
        /// Where it believes its target is. The target itself, except while it cannot perceive
        /// them, when it is a fixed point where it last did.
        /// </summary>
        public Transform Target { get; private set; }

        /// <summary>Set by the factory once the body is built. A property so it satisfies
        /// <see cref="IAbilityOwner"/>; enemies have no prefab, so nothing serializes it.</summary>
        public Transform Muzzle { get; set; }

        private Health _health;
        private CharacterSheet _sheet;
        private StatusController _status;

        private Team _attackTeam = Team.Enemy;
        private CharacterController _controller;
        private readonly List<AbilityAttack> _attacks = new List<AbilityAttack>();
        private readonly List<AbilityAttack> _attackScratch = new List<AbilityAttack>();
        private Vector3 _velocity;
        private Vector3 _externalVelocity;
        private float _strafeTimer;
        private int _strafeSign = 1;
        private float _retargetTimer;
        private float _bobPhase;

        private Vector3 _home;
        private Vector3 _idleDestination;
        private float _idlePause;
        private readonly List<Vector3> _patrol = new List<Vector3>();
        private int _patrolIndex;

        // Who it is really fighting, and the stand-in point it fights instead when it cannot perceive them.
        private TargetRegistry.Entry _liveEntry;
        private Transform _lastSeen;
        private bool _lastSeenFixed;
        private readonly List<TargetRegistry.Entry> _candidates = new List<TargetRegistry.Entry>();

        /// <summary>Minions are looked for once a second rather than every frame; a room can hold many.</summary>
        private const float MinionSightInterval = 1f;
        private float _minionSightTimer;

        private bool _wasFeared;
        private bool _wasConfused;
        private Team _teamBeforeConfusion = Team.Enemy;

        // What Hide switched off, so Reveal turns back on exactly that and nothing more.
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private readonly List<Collider> _hiddenColliders = new List<Collider>();
        private readonly List<AbilityAttack> _hiddenAttacks = new List<AbilityAttack>();

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

        /// <summary>Knockback and pulls still being worked off. Read by tooling.</summary>
        public Vector3 ExternalVelocity => _externalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _sheet = GetComponent<CharacterSheet>();
            _status = GetComponent<StatusController>();

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

            _home = transform.position;
            _idleDestination = _home;
            BuildPatrolRoute();

            Noise.Heard += OnNoise;

            // A hunter never idles. Everything else waits to be given a reason.
            if (Idle == IdleActivity.Hunt) Alert();
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Damaged -= OnDamaged;
            Noise.Heard -= OnNoise;
            if (_lastSeen != null) Destroy(_lastSeen.gameObject);
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            if (info.Knockback.sqrMagnitude > 0.01f)
                _externalVelocity += info.Knockback;

            // Being hit is the one signal that never needs checking against a range.
            Alert();
        }

        private void Update()
        {
            if (IsHidden || (Health != null && !Health.IsAlive)) return;

            float dt = Time.deltaTime;
            SyncStatusEffects();

            bool impaired = Status != null && Status.IsControlImpaired;

            if (!IsAlerted)
            {
                if (!impaired && NoticesSomething()) Alert();
                else
                {
                    UpdateIdle(dt, impaired);
                    return;
                }
            }

            _retargetTimer -= dt;
            if (_liveEntry == null || !_liveEntry.IsAlive || _retargetTimer <= 0f) AcquireTarget();
            else UpdatePerceivedTarget();

            if (!impaired && Status != null && Status.IsFeared)
            {
                Flee(dt);
                return;
            }

            if (!impaired)
            {
                FaceTarget();
                TryAttack();
            }

            Move(impaired);
        }

        // ---------------------------------------------------------------- allegiance

        /// <summary>
        /// Which side its attacks are on, apart from its body. Confusion sets this to neutral, so
        /// its attacks can hit anyone while its body stays an enemy the others will not shoot.
        /// </summary>
        public void SetAttackTeam(Team team) => _attackTeam = team;

        /// <summary>
        /// Moves the whole enemy to a side: its attacks, what can hurt it, and its physics layer.
        /// Assume Identity moves a controlled enemy to the player's side and back. This changes
        /// only what can hurt what; who it and the other enemies choose to fight is the registry's.
        /// </summary>
        public void SetSide(Team team)
        {
            _attackTeam = team;
            if (Health != null) Health.Team = team;
            Layers.SetRecursively(gameObject, Layers.BodyLayerFor(team));
        }

        // ---------------------------------------------------------------- statuses

        /// <summary>
        /// Reacts to statuses landing and lifting. Fear alerts it and cancels whatever attack was
        /// winding up, rather than letting a slam that started a frame earlier still land. Silence
        /// and disarm cancel the kind of attack they forbid. Confusion puts its attacks on the
        /// neutral team until it lifts. Called every frame; public so tooling can drive it.
        /// </summary>
        public void SyncStatusEffects()
        {
            StatusController status = Status;
            if (status == null) return;

            bool feared = status.IsFeared;
            if (feared && !_wasFeared)
            {
                Alert();
                CancelAttacks(null);
            }
            _wasFeared = feared;

            if (status.IsSilenced || status.IsDisarmed) CancelAttacks(status);

            bool confused = status.IsConfused;
            if (confused != _wasConfused)
            {
                if (confused)
                {
                    _teamBeforeConfusion = _attackTeam;
                    _attackTeam = Team.Neutral;
                }
                else
                {
                    _attackTeam = _teamBeforeConfusion;
                }

                _wasConfused = confused;

                // Friend and foe just changed, so look again now rather than at the next retarget.
                _retargetTimer = 0f;
            }
        }

        /// <summary>Stops executing attacks: every one, or only those the given statuses forbid.</summary>
        private void CancelAttacks(StatusController forbiddenBy)
        {
            GetComponents(_attackScratch);
            for (int i = 0; i < _attackScratch.Count; i++)
            {
                AbilityAttack attack = _attackScratch[i];
                if (forbiddenBy == null || !forbiddenBy.CanAttack(attack.Reach)) attack.Cancel();
            }
        }

        // ---------------------------------------------------------------- perception

        /// <summary>Switches from idling to fighting. Deliberately one-way.</summary>
        public void Alert()
        {
            if (IsAlerted) return;
            IsAlerted = true;
            AcquireTarget();
        }

        /// <summary>
        /// The player's body is watched every frame. Minions are only looked for at an interval,
        /// since a room can hold many and a sight check for each, every frame, for every enemy adds up.
        /// </summary>
        private bool NoticesSomething()
        {
            if (Status != null && Status.IsBlind) return false;
            if (CanSee(TargetRegistry.PlayerBody)) return true;

            _minionSightTimer -= Time.deltaTime;
            if (_minionSightTimer > 0f) return false;
            _minionSightTimer = MinionSightInterval;

            IReadOnlyList<TargetRegistry.Entry> minions = TargetRegistry.Minions;
            for (int i = 0; i < minions.Count; i++)
                if (CanSee(minions[i])) return true;

            return false;
        }

        /// <summary>
        /// A cone in front, out to the sight range, with a wall check. Fliers included - looking down
        /// from above is still looking. Nothing hidden from sight is ever seen, and nothing is seen blind.
        /// </summary>
        public bool CanSee(TargetRegistry.Entry entry)
        {
            if (entry == null || !entry.IsAlive || entry.HiddenFromSight) return false;
            if (Status != null && Status.IsBlind) return false;

            Vector3 to = entry.Transform.position - transform.position;
            if (to.sqrMagnitude > SightRange * SightRange) return false;

            // Measured flat, so standing directly above or below something does not slip out of
            // its cone on a technicality.
            Vector3 flatTo = Flat(to);
            if (flatTo.sqrMagnitude < 0.01f) return true;
            if (Vector3.Angle(Flat(transform.forward), flatTo) > SightHalfAngle) return false;

            return HasLineOfSightTo(entry.Transform.position);
        }

        /// <summary>
        /// Loudness scales this listener's own hearing range rather than being a distance, so
        /// one gun is heard further by a sharp-eared enemy than a dull one without the gun
        /// having to know anything about who is listening.
        /// </summary>
        private void OnNoise(Vector3 position, float loudness)
        {
            if (IsAlerted || this == null) return;
            if (IsHidden || (Health != null && !Health.IsAlive)) return;

            // Asleep, nothing is noticed. Enemies cannot be un-alerted, so sleep suspends
            // perception rather than resetting it, and waking restores it as it was.
            if (Status != null && Status.IsAsleep) return;

            if (TargetRegistry.IsInaudibleAt(position)) return;

            // Shock deadens hearing, which is what makes it worth putting on something that has
            // not noticed you yet rather than only on something already shooting at you.
            float hearing = HearingRange * ShockStatus.HearingScale(Status);

            if (TravelDistanceTo(position) <= hearing * loudness) Alert();
        }

        /// <summary>
        /// How far a sound actually has to travel to get here - around walls, not through them.
        ///
        /// The navigation flow field is a breadth-first sweep outward from the player's body through
        /// walkable space, so the step count already sitting in this enemy's own cell is exactly
        /// that distance. It only answers for that body's position, though, so a noise made
        /// anywhere else falls back to a straight line.
        /// </summary>
        private float TravelDistanceTo(Vector3 point)
        {
            NavField field = NavField.Current;
            Transform body = TargetRegistry.PlayerBody.Transform;

            if (field != null && field.IsBuilt && body != null
                && (point - body.position).sqrMagnitude < 4f)
            {
                int steps = field.StepsAt(transform.position);
                if (steps >= 0) return steps * NavField.CellSize;
            }

            return Vector3.Distance(transform.position, point);
        }

        // ---------------------------------------------------------------- targeting

        /// <summary>Picks and perceives a target now rather than at the next retarget. For tooling and redirects.</summary>
        public void RefreshTarget() => AcquireTarget();

        private void AcquireTarget()
        {
            _retargetTimer = 1f;

            CollectCandidates(_candidates);
            bool elite = Health != null && Health.IsElite;

            _liveEntry = TargetRegistry.Choose(transform.position,
                _liveEntry != null ? _liveEntry.Transform : null, elite, _candidates, CanReach);

            UpdatePerceivedTarget();
        }

        /// <summary>
        /// The registry's hostiles, plus, while confused, every other enemy within sight range - it
        /// cannot tell friend from foe, so its own kind are fair game.
        /// </summary>
        private void CollectCandidates(List<TargetRegistry.Entry> into)
        {
            TargetRegistry.Collect(into);
            if (Status == null || !Status.IsConfused) return;

            Collider[] found = Physics.OverlapSphere(transform.position, SightRange, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found.Length; i++)
            {
                Health other = found[i].GetComponentInParent<Health>();
                if (other == null || other == Health || !other.IsAlive || Contains(into, other.transform)) continue;
                into.Add(new TargetRegistry.Entry { Transform = other.transform, Health = other });
            }
        }

        private static bool Contains(List<TargetRegistry.Entry> entries, Transform transform)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Transform == transform) return true;
            return false;
        }

        /// <summary>Whether it could walk to a target, for an elite deciding between the player's body and a minion.</summary>
        private bool CanReach(TargetRegistry.Entry entry)
        {
            NavField field = NavField.Current;
            if (Flying || field == null || !field.IsBuilt || entry == null || entry.Transform == null) return true;
            if (field.IsClearLine(transform.position, entry.Transform.position)) return true;

            // The field is built outward from the player's body, so it only knows routes to that.
            return !entry.IsPlayerBody || field.StepsAt(transform.position) >= 0;
        }

        /// <summary>
        /// Perception holds while it can see its target: not blind, and the target not hidden from
        /// sight. While it holds, the target is the target. When it breaks, the enemy keeps fighting a
        /// fixed point where it last perceived them - facing it, walking at it and shooting at it -
        /// while they are free to be anywhere else. Aim, movement, attack range and line of sight all
        /// read <see cref="Target"/>, so swapping in that point is the whole effect.
        /// </summary>
        private void UpdatePerceivedTarget()
        {
            Transform live = _liveEntry != null && _liveEntry.IsAlive ? _liveEntry.Transform : null;
            bool perceiving = live != null && !_liveEntry.HiddenFromSight && (Status == null || !Status.IsBlind);

            if (live == null || perceiving)
            {
                Target = live;
                _lastSeenFixed = false;
                return;
            }

            if (_lastSeen == null) _lastSeen = new GameObject(name + " LastSeen").transform;

            if (!_lastSeenFixed)
            {
                _lastSeen.position = live.position;
                _lastSeenFixed = true;
            }

            Target = _lastSeen;
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
                if (Status != null && !Status.CanAttack(attack.Reach)) continue;
                if (!attack.CanUse(distance, los)) continue;
                if (attack.Priority > bestPriority)
                {
                    bestPriority = attack.Priority;
                    best = attack;
                }
            }

            if (best != null) best.Begin();
        }

        public bool HasLineOfSight() => Target != null && HasLineOfSightTo(Target.position);

        public bool HasLineOfSightTo(Vector3 position)
        {
            Vector3 from = EyePosition;
            Vector3 to = position + Vector3.up * 1.0f;
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
                Vector3 direction = (aimAt - MuzzlePosition).normalized;

                // Poison spoils an enemy's aim the same way it spoils the player's: by moving
                // where the shot actually goes, rather than by drawing something wobbly. The
                // bob phase is already per-enemy, so a poisoned group does not sway in unison.
                float sway = PoisonStatus.SwayFor(Status);
                if (sway <= 0f) return direction;

                float t = Time.time + _bobPhase;
                return Quaternion.Euler(
                    Mathf.Sin(t * 1.1f) * sway * 0.7f,
                    Mathf.Sin(t * 1.7f) * sway,
                    0f) * direction;
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

                if (TryNavigate(out Vector3 route))
                {
                    // No way to walk straight at the target, so the only job is getting there.
                    // Holding range or strafing along a route would just scrape the walls.
                    desired += route;
                }
                else
                {
                    if (distance > PreferredRange) desired += forward;
                    else if (distance < MinComfortRange) desired -= forward;
                    else desired += right * _strafeSign * 0.9f;

                    desired += right * (_strafeSign * 0.35f);
                }

                desired += Separation();
                desired += HazardPush();
                desired = AvoidWalls(desired);

                if (desired.sqrMagnitude > 1f) desired.Normalize();
                if (IsAttacking) desired *= MoveSpeedWhileAttacking;
            }

            ApplyMotion(desired, dt, 1f);
        }

        private void Flee(float dt)
        {
            Vector3 desired = FleeDirection() + Separation() + HazardPush();
            desired = AvoidWalls(desired);
            if (desired.sqrMagnitude > 1f) desired.Normalize();

            FaceMovement(desired, dt);
            ApplyMotion(desired, dt, 1f);
        }

        /// <summary>
        /// Away from whatever scared it. Fear from the player's body climbs the flow field, which
        /// only measures distance to that body, so it runs through the maze rather than into the
        /// nearest wall. Fear from anything else is fled one step at a time from where that thing
        /// stood when the fear landed, since it may be gone by now.
        /// </summary>
        public Vector3 FleeDirection()
        {
            ActiveStatus fear = Status != null ? Status.Find(StatusId.Fear) : null;
            Transform body = TargetRegistry.PlayerBody.Transform;

            bool fromBody = fear == null || !fear.HasSourcePosition
                            || (fear.Source != null && body != null && fear.Source.transform == body);

            Vector3 threat = fromBody
                ? (body != null ? body.position : transform.position + transform.forward)
                : fear.SourcePosition;

            NavField field = NavField.Current;
            if (!Flying && field != null && field.IsBuilt)
            {
                Vector3 route = fromBody
                    ? field.FleeDirection(transform.position)
                    : field.AwayFrom(transform.position, threat);

                if (route.sqrMagnitude > 0.001f) return route;
            }

            Vector3 away = Flat(transform.position - threat);
            return away.sqrMagnitude > 0.001f ? away.normalized : Flat(-transform.forward).normalized;
        }

        private Vector3 HazardPush() => Hazards.PushAt(transform.position, Health != null ? Health.Team : Team.Enemy);

        /// <summary>
        /// Turns a wish direction into actual movement. Shared by fighting and idling so a
        /// wandering enemy falls, hovers, separates and slides along walls exactly as a
        /// chasing one does - only slower, and towards somewhere else.
        /// </summary>
        private void ApplyMotion(Vector3 desired, float dt, float speedFraction)
        {
            float speed = (Sheet != null ? Sheet.Get(Attr.MoveSpeed) : 4.5f) * speedFraction;
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

        // ---------------------------------------------------------------- idling

        /// <summary>
        /// What it does while it has not noticed anything. Standing still is not the same as
        /// doing nothing: gravity, hover and knockback still have to be integrated, or a
        /// standing enemy floats where it spawned and shrugs off being shot.
        /// </summary>
        private void UpdateIdle(float dt, bool impaired)
        {
            Vector3 desired = Vector3.zero;

            if (!impaired && Idle != IdleActivity.Stand)
            {
                if (_idlePause > 0f)
                {
                    _idlePause -= dt;
                }
                else
                {
                    Vector3 toSpot = Flat(_idleDestination) - Flat(transform.position);

                    if (toSpot.magnitude <= arriveDistance) ChooseIdleDestination();
                    else
                    {
                        desired = AvoidWalls(toSpot.normalized) + Separation();
                        if (desired.sqrMagnitude > 1f) desired.Normalize();
                    }
                }

                FaceMovement(desired, dt);
            }

            // Even something standing guard steps out of a fire rather than burning in place.
            if (!impaired) desired += HazardPush();

            ApplyMotion(desired, dt, idleSpeedFraction);
        }

        private void ChooseIdleDestination()
        {
            _idlePause = idlePauseSeconds * Random.Range(0.6f, 1.5f);

            if (Idle == IdleActivity.Patrol && _patrol.Count > 0)
            {
                _patrolIndex = (_patrolIndex + 1) % _patrol.Count;
                _idleDestination = _patrol[_patrolIndex];
                return;
            }

            _idleDestination = PickSpotNear(_home, wanderRadius);
        }

        /// <summary>
        /// A round of points fixed at spawn, so a patrol is a route rather than a wander with
        /// extra steps - you can learn it and time your way past it.
        /// </summary>
        private void BuildPatrolRoute()
        {
            _patrol.Clear();
            if (Idle != IdleActivity.Patrol) return;

            for (int i = 0; i < 3; i++) _patrol.Add(PickSpotNear(_home, patrolRadius));
            _idleDestination = _patrol[0];
        }

        /// <summary>
        /// Somewhere reachable near a point. Asks the navigation grid where the floor actually
        /// is when there is one, so a wanderer does not spend its life walking into a wall it
        /// picked a destination inside of.
        /// </summary>
        private Vector3 PickSpotNear(Vector3 origin, float radius)
        {
            NavField field = NavField.Current;

            if (field != null && field.IsBuilt)
            {
                var rng = new Rng(Random.Range(int.MinValue, int.MaxValue));
                if (field.TryFindSpot(rng, origin, radius, out Vector3 spot)) return spot;
            }

            Vector2 offset = Random.insideUnitCircle * radius;
            return origin + new Vector3(offset.x, 0f, offset.y);
        }

        /// <summary>Looks where it is going, rather than staring at a player it has not seen.</summary>
        private void FaceMovement(Vector3 desired, float dt)
        {
            if (desired.sqrMagnitude < 0.01f) return;

            Quaternion wanted = Quaternion.LookRotation(Flat(desired).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, TurnSpeed * dt);
        }

        // ---------------------------------------------------------------- hiding

        /// <summary>True while banished or otherwise taken out of the world without being destroyed.</summary>
        public bool IsHidden { get; private set; }

        /// <summary>
        /// Takes the enemy out of the world without deleting it: no body, no collisions, no
        /// behaviour, and out of every check that finds things through physics. Its statuses and
        /// attack timers pause where they stand. It stays registered with its room, which is what
        /// keeps a room holding a banished enemy from clearing and opening its exit.
        /// </summary>
        public void Hide()
        {
            if (IsHidden) return;
            IsHidden = true;

            _hiddenAttacks.Clear();
            foreach (AbilityAttack attack in GetComponents<AbilityAttack>())
            {
                attack.Cancel();
                if (!attack.enabled) continue;
                attack.enabled = false;
                _hiddenAttacks.Add(attack);
            }

            _hiddenRenderers.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                renderer.enabled = false;
                _hiddenRenderers.Add(renderer);
            }

            _hiddenColliders.Clear();
            foreach (Collider body in GetComponentsInChildren<Collider>(true))
            {
                if (!body.enabled) continue;
                body.enabled = false;
                _hiddenColliders.Add(body);
            }

            if (Status != null) Status.Paused = true;

            _velocity = Vector3.zero;
            _externalVelocity = Vector3.zero;
        }

        /// <summary>
        /// Brings a hidden enemy back exactly as it was, at the given point or where it left. A spot
        /// that has since become a wall moves it to the nearest open cell. Landing on top of another
        /// body waits for the shared landing check in Phase 4.
        /// </summary>
        public void Reveal(Vector3? at = null)
        {
            if (!IsHidden) return;

            Vector3 position = at ?? transform.position;

            NavField field = NavField.Current;
            if (!Flying && field != null && field.IsBuilt)
            {
                Vector2Int cell = field.WorldToCell(position);
                if (!field.IsWalkable(cell.x, cell.y) && field.TryNearestWalkable(cell, out Vector2Int open))
                {
                    Vector3 centre = field.CellCentre(open.x, open.y);
                    position = new Vector3(centre.x, position.y, centre.z);
                }
            }

            // The character controller is still switched off, so nothing undoes this move.
            transform.position = position;

            for (int i = 0; i < _hiddenColliders.Count; i++)
                if (_hiddenColliders[i] != null) _hiddenColliders[i].enabled = true;
            for (int i = 0; i < _hiddenRenderers.Count; i++)
                if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
            for (int i = 0; i < _hiddenAttacks.Count; i++)
                if (_hiddenAttacks[i] != null) _hiddenAttacks[i].enabled = true;

            _hiddenColliders.Clear();
            _hiddenRenderers.Clear();
            _hiddenAttacks.Clear();

            if (Status != null) Status.Paused = false;
            IsHidden = false;
        }

        // ---------------------------------------------------------------- flight and steering

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

        /// <summary>
        /// Asks the flow field which way to go, and returns false when walking straight at the
        /// target is fine - which is the common case, and keeps open-room behaviour exactly as
        /// it was before there was any pathfinding at all.
        ///
        /// Fliers never path. They cross walls, ledges and embrasures that stop everything on
        /// the ground, and that difference is most of what makes them worth having.
        /// </summary>
        private bool TryNavigate(out Vector3 direction)
        {
            direction = Vector3.zero;
            if (Flying || Target == null) return false;

            NavField field = NavField.Current;
            if (field == null || !field.IsBuilt) return false;
            if (field.IsClearLine(transform.position, Target.position)) return false;

            direction = field.FlowDirection(transform.position);
            return direction.sqrMagnitude > 0.001f;
        }

        /// <summary>Used by lunges, knockback and pulls.</summary>
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
