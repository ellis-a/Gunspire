using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// One attack an enemy can perform. Every attack in the game is dodgeable: it is either
    /// a travelling projectile, a shape that is telegraphed before it fires, or a melee swing
    /// with a visible wind-up that can be kited.
    /// </summary>
    public abstract class EnemyAttack : MonoBehaviour
    {
        [Header("Usage")]
        public float MinRange = 0f;
        public float MaxRange = 20f;
        public float Cooldown = 3f;
        public float InitialDelay = 0.6f;
        public bool RequiresLineOfSight = true;
        public int Priority = 0;

        [Header("Damage")]
        public float Damage = 10f;
        public DamageType DamageType = DamageType.Arcane;
        public Color Tint = Palette.Arcane;

        protected EnemyController Owner;
        protected Transform Target => Owner != null ? Owner.Target : null;

        private float _timer;

        public bool IsExecuting { get; protected set; }
        public float CooldownRemaining => _timer;

        public virtual void Initialise(EnemyController owner)
        {
            Owner = owner;
            _timer = InitialDelay;
        }

        protected virtual void Update()
        {
            if (_timer <= 0f) return;
            float rate = Owner != null && Owner.Sheet != null ? Owner.Sheet.Get(Attr.AttackSpeed) : 1f;
            _timer -= Time.deltaTime * rate;
        }

        public bool CanUse(float distance, bool hasLineOfSight)
        {
            if (IsExecuting || _timer > 0f || Owner == null || Owner.Target == null) return false;
            if (distance < MinRange || distance > MaxRange) return false;
            if (RequiresLineOfSight && !hasLineOfSight) return false;
            return true;
        }

        public void Begin()
        {
            IsExecuting = true;
            _timer = Cooldown;
            StartCoroutine(RunInternal());
        }

        private IEnumerator RunInternal()
        {
            yield return Execute();
            IsExecuting = false;
        }

        protected abstract IEnumerator Execute();

        /// <summary>False once the enemy is dead or frozen, so wind-ups can bail out mid-swing.</summary>
        protected bool StillActing()
        {
            if (Owner == null) return false;
            if (Owner.Health != null && !Owner.Health.IsAlive) return false;
            if (Owner.Status != null && Owner.Status.IsControlImpaired) return false;
            return true;
        }

        protected DamageInfo BuildDamage(float amount, Vector3 point, Vector3 normal,
            List<StatusApplication> statuses = null)
        {
            float scaled = amount * (Owner != null && Owner.Sheet != null
                ? Owner.Sheet.Get(Attr.DamageDealt)
                : 1f);

            DamageInfo info = DamageInfo.Create(scaled, DamageType, Team.Enemy, Owner.gameObject);
            info.CanCrit = false;
            return info.At(point, normal).WithStatuses(statuses);
        }
    }

    // ================================================================================ projectiles

    /// <summary>Fires a short burst of slow, visible orbs. The bread and butter dodgeable attack.</summary>
    public class ProjectileVolleyAttack : EnemyAttack
    {
        [Header("Volley")]
        public int ProjectileCount = 3;
        public float ShotInterval = 0.16f;
        public float Windup = 0.55f;
        public float Recover = 0.45f;
        public float ProjectileSpeed = 20f;
        public float ProjectileRadius = 0.24f;
        public float SpreadDegrees = 1.5f;
        public float ArcSpreadDegrees = 0f;     // fan the burst horizontally
        public List<StatusApplication> Statuses;

        protected override IEnumerator Execute()
        {
            GameObject flash = Telegraph.Flash(Owner.transform, Vector3.up * 1.35f, 0.35f, Windup, Tint);
            yield return new WaitForSeconds(Windup);
            if (flash != null) Destroy(flash);

            for (int i = 0; i < ProjectileCount; i++)
            {
                if (!StillActing() || Target == null) yield break;

                Vector3 direction = Owner.AimDirection;
                if (ArcSpreadDegrees > 0f && ProjectileCount > 1)
                {
                    float t = ProjectileCount == 1 ? 0f : i / (float)(ProjectileCount - 1) - 0.5f;
                    direction = Quaternion.AngleAxis(t * ArcSpreadDegrees, Vector3.up) * direction;
                }
                if (SpreadDegrees > 0f)
                {
                    Vector2 jitter = Random.insideUnitCircle * SpreadDegrees;
                    direction = Quaternion.Euler(jitter.y, jitter.x, 0f) * direction;
                }

                Projectile p = Projectile.Create(Owner.MuzzlePosition, direction, Tint, ProjectileRadius);
                p.OwnerTeam = Team.Enemy;
                p.Owner = Owner.gameObject;
                p.OwnerSheet = Owner.Sheet;
                p.CanCrit = false;
                p.Damage = Damage;
                p.DamageType = DamageType;
                p.Speed = ProjectileSpeed;
                p.Lifetime = 6f;
                p.Statuses = Statuses;
                p.Launch();

                yield return new WaitForSeconds(ShotInterval);
            }

            yield return new WaitForSeconds(Recover);
        }
    }

    // ================================================================================ melee

    /// <summary>Winds up, lunges, and swings a cone. Slow enough that a moving player can kite it.</summary>
    public class MeleeLungeAttack : EnemyAttack
    {
        [Header("Lunge")]
        public float Windup = 0.55f;
        public float LungeSpeed = 16f;
        public float LungeDuration = 0.2f;
        public float Recover = 0.6f;
        public float SwingRange = 3.4f;
        public float SwingHalfAngle = 60f;
        public float Knockback = 6f;
        public List<StatusApplication> Statuses;

        protected override IEnumerator Execute()
        {
            Vector3 lockedDirection = Owner.transform.forward;

            GameObject flash = Telegraph.Flash(Owner.transform, Vector3.up * 1.1f, 0.55f, Windup, Tint);
            float elapsed = 0f;
            while (elapsed < Windup)
            {
                elapsed += Time.deltaTime;
                if (!StillActing())
                {
                    if (flash != null) Destroy(flash);
                    yield break;
                }
                // Track the target during wind-up, but only slowly, so strafing beats it.
                if (Target != null)
                {
                    Vector3 to = Target.position - Owner.transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                        lockedDirection = Vector3.RotateTowards(lockedDirection, to.normalized,
                            2.2f * Time.deltaTime, 0f);
                }
                yield return null;
            }
            if (flash != null) Destroy(flash);
            if (!StillActing()) yield break;

            Owner.AddImpulse(lockedDirection.normalized * LungeSpeed);

            float swingTime = 0f;
            bool landed = false;
            while (swingTime < LungeDuration)
            {
                swingTime += Time.deltaTime;
                if (!landed && TrySwing(lockedDirection)) landed = true;
                yield return null;
            }

            if (!landed) TrySwing(lockedDirection);

            yield return new WaitForSeconds(Recover);
        }

        private bool TrySwing(Vector3 direction)
        {
            Vector3 origin = Owner.transform.position + Vector3.up * 1.0f;
            List<IDamageable> targets = Combat.ConeTargets(origin, direction, SwingRange,
                SwingHalfAngle, Layers.PlayerMask);

            if (targets.Count == 0) return false;

            var color = new Color(Tint.r, Tint.g, Tint.b, 0.3f);
            GameObject swing = MeshFactory.SpawnCone(origin, direction, SwingRange, SwingHalfAngle,
                MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(swing, 0.15f, color);

            for (int i = 0; i < targets.Count; i++)
            {
                DamageInfo info = BuildDamage(Damage, targets[i].Transform.position + Vector3.up,
                    -direction, Statuses);
                info.Knockback = direction.normalized * Knockback;
                targets[i].TakeDamage(info);
            }
            return true;
        }
    }

    // ================================================================================ beam

    /// <summary>
    /// Locks a firing line, warns along it, then fires a continuous beam that sweeps slowly.
    /// Beating it means stepping out of the line, not out-running the damage.
    /// </summary>
    public class BeamAttack : EnemyAttack
    {
        [Header("Beam")]
        public float Windup = 1.1f;
        public float BeamDuration = 1.4f;
        public float Recover = 0.9f;
        public float Length = 40f;
        public float Width = 0.55f;
        public float DamagePerTick = 6f;
        public float TickInterval = 0.12f;
        public float SweepDegreesPerSecond = 26f;
        public List<StatusApplication> Statuses;

        protected override IEnumerator Execute()
        {
            Vector3 origin = Owner.MuzzlePosition;
            Vector3 direction = Owner.AimDirection;
            direction.y = Mathf.Clamp(direction.y, -0.25f, 0.25f);
            direction.Normalize();

            // Sweep towards the side the player is moving, so standing still is never safe.
            float sweepSign = Random.value < 0.5f ? -1f : 1f;

            GameObject warning = Telegraph.Line(origin, direction, Length, Width * 0.6f, Windup, Tint);
            float elapsed = 0f;
            while (elapsed < Windup)
            {
                elapsed += Time.deltaTime;
                if (!StillActing())
                {
                    if (warning != null) Destroy(warning);
                    yield break;
                }
                yield return null;
            }
            if (warning != null) Destroy(warning);
            if (!StillActing()) yield break;

            GameObject beam = Build.Cube(null, "Beam", Vector3.zero, Vector3.one,
                MaterialLibrary.Emissive(Tint, 3f), collider: false);

            float beamTime = 0f;
            float tickTimer = 0f;

            while (beamTime < BeamDuration && StillActing())
            {
                float dt = Time.deltaTime;
                beamTime += dt;

                direction = Quaternion.AngleAxis(SweepDegreesPerSecond * sweepSign * dt, Vector3.up) * direction;
                origin = Owner.MuzzlePosition;

                float reach = Length;
                if (Physics.Raycast(origin, direction, out RaycastHit wall, Length,
                        Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                    reach = wall.distance;

                beam.transform.position = origin + direction * (reach * 0.5f);
                beam.transform.rotation = Quaternion.LookRotation(direction);
                beam.transform.localScale = new Vector3(Width, Width, reach);

                tickTimer -= dt;
                if (tickTimer <= 0f)
                {
                    tickTimer = TickInterval;
                    DamageAlongBeam(origin, direction, reach);
                }
                yield return null;
            }

            if (beam != null)
            {
                FadeAndDie.Attach(beam, 0.15f, Tint);
            }

            yield return new WaitForSeconds(Recover);
        }

        private void DamageAlongBeam(Vector3 origin, Vector3 direction, float reach)
        {
            if (!Physics.SphereCast(origin, Width * 0.5f, direction, out RaycastHit hit, reach,
                    Layers.PlayerMask, QueryTriggerInteraction.Ignore))
                return;

            IDamageable target = Combat.FindDamageable(hit.collider);
            if (target == null || !target.IsAlive) return;

            target.TakeDamage(BuildDamage(DamagePerTick, hit.point, -direction, Statuses));
        }
    }

    // ================================================================================ ground area

    /// <summary>
    /// Drops telegraphed circles at the player's feet. They land where the player was, so the
    /// answer is always to keep moving.
    /// </summary>
    public class GroundSlamAttack : EnemyAttack
    {
        [Header("Slam")]
        public int Impacts = 1;
        public float ImpactInterval = 0.45f;
        public float Windup = 0.9f;
        public float Recover = 0.7f;
        public float Radius = 3.6f;
        public float Knockback = 7f;
        public float LeadDistance = 0f;      // aim ahead of the player by this much
        public List<StatusApplication> Statuses;

        protected override IEnumerator Execute()
        {
            for (int i = 0; i < Impacts; i++)
            {
                if (!StillActing() || Target == null) yield break;

                Vector3 point = Target.position;
                if (LeadDistance > 0f)
                {
                    var motor = Target.GetComponent<PlayerMotor>();
                    if (motor != null)
                    {
                        Vector3 lead = motor.Velocity;
                        lead.y = 0f;
                        point += Vector3.ClampMagnitude(lead * 0.35f, LeadDistance);
                    }
                }
                point = DropToGround(point);

                Telegraph.Circle(point, Radius, Windup, Tint);
                StartCoroutine(DetonateAfter(point, Windup));

                yield return new WaitForSeconds(ImpactInterval);
            }

            yield return new WaitForSeconds(Windup + Recover);
        }

        private IEnumerator DetonateAfter(Vector3 point, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Owner == null) yield break;

            DamageInfo template = BuildDamage(Damage, point, Vector3.up, Statuses);
            Combat.Explode(point, Radius, template, Layers.PlayerMask, 0.45f, Knockback);

            var color = new Color(Tint.r, Tint.g, Tint.b, 0.6f);
            GameObject burst = Build.GroundDisc(null, "SlamBurst", point + Vector3.up * 0.05f, Radius,
                MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(burst, 0.3f, color);
        }

        private static Vector3 DropToGround(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 12f,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return point;
        }
    }

    // ================================================================================ cone breath

    /// <summary>A telegraphed cone. Short range, wide, and punishing if you stay in front.</summary>
    public class ConeBreathAttack : EnemyAttack
    {
        [Header("Breath")]
        public float Windup = 0.85f;
        public float Recover = 0.8f;
        public float Range = 11f;
        public float HalfAngle = 32f;
        public List<StatusApplication> Statuses;

        protected override IEnumerator Execute()
        {
            Vector3 origin = Owner.EyePosition;
            Vector3 direction = Owner.AimDirection;
            direction.y = 0f;
            direction.Normalize();

            Telegraph.Cone(origin, direction, Range, HalfAngle, Windup, Tint);

            float elapsed = 0f;
            while (elapsed < Windup)
            {
                elapsed += Time.deltaTime;
                if (!StillActing()) yield break;
                yield return null;
            }

            origin = Owner.EyePosition;
            List<IDamageable> targets = Combat.ConeTargets(origin, direction, Range, HalfAngle, Layers.PlayerMask);
            for (int i = 0; i < targets.Count; i++)
                targets[i].TakeDamage(BuildDamage(Damage, targets[i].Transform.position + Vector3.up,
                    -direction, Statuses));

            var color = new Color(Tint.r, Tint.g, Tint.b, 0.4f);
            GameObject blast = MeshFactory.SpawnCone(origin, direction, Range, HalfAngle,
                MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(blast, 0.3f, color);

            yield return new WaitForSeconds(Recover);
        }
    }
}
