using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A travelling shot. Moves by spherecast rather than physics so fast rounds cannot
    /// tunnel through walls, and so player and enemy projectiles behave identically.
    /// Every projectile in the game (guns, spells, enemy attacks) is one of these.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public Team OwnerTeam = Team.Player;
        public GameObject Owner;
        public CharacterSheet OwnerSheet;

        public float Damage = 10f;
        public DamageType DamageType = DamageType.Kinetic;
        public bool IsSpell;
        public bool CanCrit = true;

        public float Speed = 45f;
        public float Gravity;
        public float Radius = 0.15f;
        public float Lifetime = 5f;
        public float Knockback;

        public float SplashRadius;
        public float SplashDamage;

        public int Pierce;               // how many extra targets it can pass through
        public bool HomingEnabled;
        public float HomingStrength = 3.5f;
        public float HomingRange = 30f;

        public Color Tint = Color.white;
        public List<StatusApplication> Statuses;

        /// <summary>How its hits are reported. The gun or the ability that fired it sets this.</summary>
        public DamageOrigin Origin;

        /// <summary>
        /// The gun that fired it, so its hits reach that gun's on-hit hook, and the infusions the
        /// gun carried at the moment it fired. Both null for anything but a gun's round.
        /// </summary>
        public Weapon SourceWeapon;
        public List<BulletInfusion> Infusions;

        private Vector3 _velocity;
        private float _age;
        private int _hitMask;
        private Transform _homingTarget;
        private readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();

        private List<AbilityEffect> _onHit;
        private AbilityContext _onHitContext;

        /// <summary>
        /// Gives the projectile an effect chain to run where it lands - the OnHit hook.
        ///
        /// The caster's context is reused for every cast, so this snapshots the parts that
        /// matter instead of holding a reference that the next cast would overwrite.
        /// </summary>
        public void AttachOnHit(List<AbilityEffect> effects, AbilityContext source)
        {
            if (effects == null || effects.Count == 0 || source == null) return;

            _onHit = effects;
            _onHitContext = new AbilityContext
            {
                Caster = source.Caster,
                Team = source.Team,
                Sheet = source.Sheet,
                Mana = source.Mana,
                Health = source.Health,
                Status = source.Status,
                Motor = source.Motor,
                Controller = source.Controller,
                Aim = source.Aim,
                Level = source.Level,
                Power = source.Power,
                StatusPower = source.StatusPower,
                DamageOrigin = source.DamageOrigin,
                LevelScale = source.LevelScale,
                DamageType = source.DamageType,
                Category = source.Category,
                Tint = source.Tint
            };
            _onHitContext.Payload.AddRange(source.Payload);
        }

        private void RunOnHit(Vector3 point)
        {
            if (_onHit == null || _onHitContext == null) return;

            _onHitContext.Origin = point;
            _onHitContext.Point = point;
            _onHitContext.Forward = transform.forward;
            _onHitContext.Targets.Clear();

            AbilityRunner.Run(_onHit, _onHitContext);
        }

        /// <summary>Creates the visual body. Configure the public fields, then call <see cref="Launch"/>.</summary>
        public static Projectile Create(Vector3 position, Vector3 direction, Color color, float radius)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            var go = new GameObject("Projectile");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction);

            GameObject body = Build.Sphere(go.transform, "Body", Vector3.zero, radius * 2f,
                MaterialLibrary.Emissive(color, 2.5f), collider: false);
            body.transform.localScale = new Vector3(radius * 2f, radius * 2f, radius * 3.2f);

            var p = go.AddComponent<Projectile>();
            p.Radius = radius;
            p.Tint = color;
            return p;
        }

        public void Launch()
        {
            _velocity = transform.forward * Speed;
            _hitMask = Layers.HitMaskFor(OwnerTeam);
            Layers.SetRecursively(gameObject,
                OwnerTeam == Team.Player ? Layers.PlayerProjectile : Layers.EnemyProjectile);
        }

        /// <summary>Layers this projectile can currently hit.</summary>
        public int HitMask => _hitMask;

        /// <summary>
        /// Hands the projectile to another side and sends it off in a new direction at the same
        /// speed, for reflects. It forgets what it already pierced, since those were the old side's
        /// hits, and drops the old side's homing target and the gun it came from.
        /// </summary>
        public void SwitchSide(Team team, GameObject owner, CharacterSheet ownerSheet, Vector3 direction)
        {
            OwnerTeam = team;
            Owner = owner;
            OwnerSheet = ownerSheet;
            SourceWeapon = null;
            Infusions = null;

            _alreadyHit.Clear();
            _homingTarget = null;

            if (_onHitContext != null)
            {
                _onHitContext.Team = team;
                _onHitContext.Caster = owner;
                _onHitContext.Sheet = ownerSheet;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                float speed = _velocity.sqrMagnitude > 0.0001f ? _velocity.magnitude : Speed;
                _velocity = direction * speed;
                transform.forward = direction;
            }

            _hitMask = Layers.HitMaskFor(team);
            Layers.SetRecursively(gameObject,
                team == Team.Player ? Layers.PlayerProjectile : Layers.EnemyProjectile);
        }

        private void Update()
        {
            // Shots are part of the world: the player's own hang in the air when it stops.
            float dt = WorldClock.DeltaTime;
            _age += dt;
            if (_age >= Lifetime)
            {
                Expire();
                return;
            }

            if (Gravity != 0f) _velocity += Vector3.down * (Gravity * dt);
            if (HomingEnabled) ApplyHoming(dt);

            Vector3 start = transform.position;
            Vector3 step = _velocity * dt;
            float distance = step.magnitude;
            if (distance <= 0.0001f) return;

            Vector3 direction = step / distance;

            if (Physics.SphereCast(start, Radius, direction, out RaycastHit hit, distance, _hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (!HandleHit(hit)) MoveTo(start + step, direction);
                return;
            }

            MoveTo(start + step, direction);
        }

        private void MoveTo(Vector3 position, Vector3 direction)
        {
            transform.position = position;
            if (direction.sqrMagnitude > 0.0001f) transform.forward = direction;
        }

        /// <summary>Returns true when the projectile is done and should stop moving this frame.</summary>
        private bool HandleHit(RaycastHit hit)
        {
            Vector3 point = hit.point;
            Vector3 normal = hit.normal;
            IDamageable target;

            // Smoke that hands shots on: the round lands on someone inside instead, and with nobody
            // inside it flies straight through. Looked up through parents, so a collider on the
            // smoke's own visuals counts as the smoke rather than as a wall.
            var redirect = hit.collider.GetComponentInParent<ShotRedirectVolume>();
            if (redirect != null)
            {
                if (!redirect.TryPickTarget(OwnerTeam, out target)) return false;
                point = AbilityContext.CenterOf(target);
                normal = -transform.forward;
            }
            else
            {
                target = Combat.FindDamageable(hit.collider);
            }

            bool hitTarget = target != null && target.IsAlive &&
                             (target.Team != OwnerTeam || target.Team == Team.Neutral);

            if (hitTarget && _alreadyHit.Contains(target))
                return false;   // already pierced this one, keep flying

            transform.position = point - transform.forward * (Radius * 0.5f);

            if (SplashRadius > 0f)
            {
                Detonate(point);
                return true;
            }

            if (hitTarget)
            {
                _alreadyHit.Add(target);
                DamageInfo info = BuildHitDamage(point, normal);
                target.TakeDamage(info);
                if (SourceWeapon != null)
                    SourceWeapon.ReportHit(target, info, point, normal, transform.forward, Infusions);
                Combat.SpawnImpact(point, normal, Tint, 0.3f, DamageType);

                if (Pierce > 0)
                {
                    Pierce--;
                    return false;
                }

                RunOnHit(point);
                Destroy(gameObject);
                return true;
            }

            // Hit the world.
            Combat.SpawnImpact(point, normal, Tint, 0.25f, DamageType);
            RunOnHit(point);
            Destroy(gameObject);
            return true;
        }

        /// <summary>The hit this projectile deals on a direct strike. Public so tooling can inspect it.</summary>
        public DamageInfo BuildHitDamage(Vector3 point, Vector3 normal)
        {
            float amount = Damage;
            bool crit = false;

            if (CanCrit && Combat.RollCrit(OwnerSheet, out float critMultiplier))
            {
                amount *= critMultiplier;
                crit = true;
            }

            DamageInfo info = DamageInfo.Create(amount, DamageType, OwnerTeam, Owner);
            info.IsCrit = crit;
            info.CanCrit = CanCrit;
            info.Origin = Origin;
            info.Knockback = transform.forward * Knockback;
            info = info.At(point, normal).WithStatuses(Statuses);
            return info;
        }

        private void Detonate(Vector3 point)
        {
            DamageInfo template = DamageInfo.Create(SplashDamage > 0f ? SplashDamage : Damage,
                DamageType, OwnerTeam, Owner);
            template.CanCrit = false;
            template.Origin = Origin;
            template = template.WithStatuses(Statuses);

            // Everything a gun's blast catches is a hit from that gun, so its on-hit hook fires
            // for each of them, not only for whatever the round struck directly.
            Weapon weapon = SourceWeapon;
            List<BulletInfusion> infusions = Infusions;
            Vector3 direction = transform.forward;
            System.Action<IDamageable, DamageInfo> onHit = weapon == null
                ? (System.Action<IDamageable, DamageInfo>)null
                : (target, info) =>
                {
                    if (weapon != null)
                        weapon.ReportHit(target, info, info.HitPoint, info.HitNormal, direction, infusions);
                };

            Combat.Explode(point, SplashRadius, template, Layers.HitMaskFor(OwnerTeam), 0.4f, Knockback, onHit);

            GameObject blast = Build.Sphere(null, "Blast", point, SplashRadius * 0.5f,
                MaterialLibrary.Transparent(new Color(Tint.r, Tint.g, Tint.b, 0.5f)), collider: false);
            FadeAndDie.Attach(blast, 0.25f, new Color(Tint.r, Tint.g, Tint.b, 0.5f),
                Vector3.one * (SplashRadius * 3f));

            RunOnHit(point);
            Destroy(gameObject);
        }

        private void Expire()
        {
            if (SplashRadius > 0f) Detonate(transform.position);
            else Destroy(gameObject);
        }

        private void ApplyHoming(float dt)
        {
            if (_homingTarget == null || !_homingTarget.gameObject.activeInHierarchy)
                _homingTarget = FindHomingTarget();

            if (_homingTarget == null) return;

            Vector3 desired = (_homingTarget.position + Vector3.up * 0.9f - transform.position).normalized;
            Vector3 newDirection = Vector3.RotateTowards(_velocity.normalized, desired,
                HomingStrength * dt, 0f);
            _velocity = newDirection * _velocity.magnitude;
        }

        private Transform FindHomingTarget()
        {
            int mask = OwnerTeam == Team.Player ? Layers.EnemyMask : Layers.PlayerMask;
            Collider[] found = Physics.OverlapSphere(transform.position, HomingRange, mask,
                QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestScore = -1f;
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable d = Combat.FindDamageable(found[i]);
                if (d == null || !d.IsAlive) continue;

                Vector3 to = d.Transform.position - transform.position;
                float score = Vector3.Dot(to.normalized, _velocity.normalized);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = d.Transform;
                }
            }
            return best;
        }
    }
}
