using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Adds a status to the payload that later damage in this chain will carry. Put these
    /// before the damage effect: applying a status alongside its damage is what lets the
    /// shatter and mark rules resolve in the right order.
    /// </summary>
    [System.Serializable]
    public class StatusPayloadEffect : AbilityEffect
    {
        public StatusId Status = StatusId.Burn;
        public float Duration = 4f;
        public int Stacks = 1;
        public float StacksPerLevel;        // fractional, floored
        public float Magnitude = 4f;
        public float DurationPerLevel;

        public override bool Execute(AbilityContext ctx)
        {
            int levels = ctx.Level - 1;
            int stacks = Stacks + Mathf.FloorToInt(levels * StacksPerLevel);
            float duration = Duration + levels * DurationPerLevel;

            ctx.AddPayload(new StatusApplication(Status, duration, Mathf.Max(1, stacks), Magnitude));
            return true;
        }

        public override string Describe() => "applies " + Status;
    }

    /// <summary>Damages everything a selector picked out.</summary>
    [System.Serializable]
    public class DealDamageEffect : AbilityEffect
    {
        public float Amount = 20f;
        public bool CanCrit;
        public float Knockback;

        /// <summary>Scale damage down with distance from the point, for blasts.</summary>
        public bool FalloffFromPoint;
        public float FalloffRadius = 6f;
        public float MinFraction = 0.4f;

        /// <summary>Strength behind the blow, compared against Smashable hardness.</summary>
        public bool UseSmashPower;

        /// <summary>
        /// Never strike the same target twice in one cast. A melee swing evaluated over
        /// several frames of a lunge needs this, or it connects once per frame.
        /// </summary>
        public bool OnlyOncePerCast;

        public override bool Execute(AbilityContext ctx)
        {
            float smash = UseSmashPower && ctx.Sheet != null ? ctx.Sheet.Get(Attr.SmashPower) : 0f;

            for (int i = 0; i < ctx.Targets.Count; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target == null || !target.IsAlive) continue;
                if (OnlyOncePerCast && !ctx.AlreadyHit.Add(target)) continue;

                Vector3 center = AbilityContext.CenterOf(target);
                float falloff = 1f;
                Vector3 away = ctx.Forward;

                if (FalloffFromPoint)
                {
                    Vector3 delta = center - ctx.Point;
                    float distance = delta.magnitude;
                    falloff = Mathf.Lerp(1f, MinFraction,
                        Mathf.Clamp01(distance / Mathf.Max(0.01f, FalloffRadius)));
                    if (distance > 0.01f) away = delta / distance;
                }

                DamageInfo info = ctx.BuildDamage(Amount * ctx.Power * falloff, center, -away, CanCrit);
                info.SmashPower = smash;
                if (Knockback > 0f) info.Knockback = away * (Knockback * falloff);

                target.TakeDamage(info);
            }
            return true;
        }

        public override string Describe() => string.Format("{0:0} damage", Amount);
    }

    /// <summary>Puts a status on the caster. Wards, hastes, anything self-targeted.</summary>
    [System.Serializable]
    public class SelfStatusEffect : AbilityEffect
    {
        public StatusId Status = StatusId.Fortify;
        public float Duration = 5f;
        public float DurationPerLevel = 1f;
        public int Stacks = 2;
        public float Magnitude = 0.18f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Status == null) return true;

            float duration = Duration + (ctx.Level - 1) * DurationPerLevel;
            ctx.Status.Apply(new StatusApplication(Status, duration, Stacks, Magnitude),
                ctx.Caster, ctx.Team);
            return true;
        }

        public override string Describe() => "grants " + Status;
    }

    /// <summary>Mends the caster, as a fraction of maximum health and/or a flat amount.</summary>
    [System.Serializable]
    public class HealSelfEffect : AbilityEffect
    {
        public float FractionOfMax = 0.12f;
        public float Flat;
        public bool ScaleWithPower = true;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Health == null) return true;

            float amount = ctx.Health.Max * FractionOfMax + Flat;
            if (ScaleWithPower) amount *= ctx.Power;

            ctx.Health.Heal(amount);
            return true;
        }

        public override string Describe() => string.Format("heals {0:0}%", FractionOfMax * 100f);
    }

    /// <summary>Moves the caster to the selected point. Pair with a sweep selector.</summary>
    [System.Serializable]
    public class TeleportEffect : AbilityEffect
    {
        public bool PreserveVelocity = true;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Motor == null) return false;
            ctx.Motor.Teleport(ctx.Point, PreserveVelocity);
            return true;
        }

        public override string Describe() => "teleport";
    }

    [System.Serializable]
    public class GrantInvulnerabilityEffect : AbilityEffect
    {
        public float Seconds = 0.18f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Health != null)
                ctx.Health.InvulnerabilityTimer = Mathf.Max(ctx.Health.InvulnerabilityTimer, Seconds);
            return true;
        }

        public override string Describe() => string.Format("{0:0.00}s invulnerable", Seconds);
    }

    /// <summary>
    /// The player dash: a burst along the movement input with brief invulnerability, spending
    /// one of the Agility-scaled charges the motor tracks. Aborts with no charges left, which
    /// refunds the activation.
    /// </summary>
    [System.Serializable]
    public class DashEffect : AbilityEffect
    {
        public override bool Execute(AbilityContext ctx)
        {
            return ctx.Motor != null && ctx.Motor.TryDash();
        }

        public override string Describe() => "dash";
    }

    /// <summary>Pushes the caster along the aim direction. Used by enemy lunges.</summary>
    [System.Serializable]
    public class ImpulseSelfEffect : AbilityEffect
    {
        public float Speed = 16f;
        public bool Flatten = true;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 direction = ctx.Forward;
            if (Flatten) direction.y = 0f;
            direction.Normalize();

            if (ctx.Motor != null)
            {
                ctx.Motor.AddImpulse(direction * Speed);
                return true;
            }

            var enemy = ctx.Caster.GetComponent<EnemyController>();
            if (enemy != null) enemy.AddImpulse(direction * Speed);
            return true;
        }

        public override string Describe() => "lunge";
    }

    /// <summary>Fires a projectile along the aim line, optionally carrying its own effect chain.</summary>
    [System.Serializable]
    public class SpawnProjectileEffect : AbilityEffect
    {
        public float Damage = 30f;
        public float Speed = 45f;
        public float Radius = 0.25f;
        public float Lifetime = 5f;
        public float Gravity;
        public float Knockback;
        public int Pierce;
        public bool Homing;
        public int Count = 1;
        public float ArcSpreadDegrees;
        public float SpreadDegrees;

        public float SplashRadius;
        public float SplashDamage;
        public bool ScaleSplashWithLevel = true;

        /// <summary>
        /// Run at the impact point when the projectile lands. This is the OnHit hook.
        /// Must start non-null: definitions fill it with a collection initializer, which calls
        /// Add on whatever is here and throws at construction time if that is null.
        /// </summary>
        [SerializeReference] public List<AbilityEffect> OnHit = new List<AbilityEffect>();

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 origin = ctx.Origin + ctx.Forward * 0.8f;

            for (int i = 0; i < Mathf.Max(1, Count); i++)
            {
                Vector3 direction = ctx.Forward;

                if (ArcSpreadDegrees > 0f && Count > 1)
                {
                    float t = i / (float)(Count - 1) - 0.5f;
                    direction = Quaternion.AngleAxis(t * ArcSpreadDegrees, Vector3.up) * direction;
                }
                if (SpreadDegrees > 0f)
                {
                    Vector2 jitter = Random.insideUnitCircle * SpreadDegrees;
                    direction = Quaternion.Euler(jitter.y, jitter.x, 0f) * direction;
                }

                Projectile p = Projectile.Create(origin, direction, ctx.Tint, Radius);
                p.OwnerTeam = ctx.Team;
                p.Owner = ctx.Caster;
                p.OwnerSheet = ctx.Sheet;
                p.IsSpell = true;
                p.CanCrit = false;
                p.Damage = Damage * ctx.Power;
                p.DamageType = ctx.DamageType;
                p.Speed = Speed;
                p.Gravity = Gravity;
                p.Lifetime = Lifetime;
                p.Knockback = Knockback;
                p.Pierce = Pierce;
                p.HomingEnabled = Homing;
                p.SplashRadius = SplashRadius * (ScaleSplashWithLevel ? ctx.LevelScale : 1f);
                p.SplashDamage = SplashDamage * ctx.Power;
                p.Statuses = new List<StatusApplication>(ctx.Payload);

                if (OnHit != null && OnHit.Count > 0) p.AttachOnHit(OnHit, ctx);

                p.Launch();
            }
            return true;
        }

        public override string Describe() => Count > 1
            ? string.Format("{0} projectiles for {1:0}", Count, Damage)
            : string.Format("a projectile for {0:0}", Damage);
    }

    /// <summary>Draws the warning shape that makes an attack dodgeable.</summary>
    [System.Serializable]
    public class TelegraphCircleEffect : AbilityEffect
    {
        public float Radius = 4f;
        public float Duration = 0.9f;
        public bool ScaleWithLevel = true;
        public bool DropToGround = true;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 point = ctx.Point;
            if (DropToGround &&
                Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 12f,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                point = hit.point;

            ctx.Point = point;
            Telegraph.Circle(point, Radius * (ScaleWithLevel ? ctx.LevelScale : 1f), Duration, ctx.Tint);
            return true;
        }

        public override string Describe() => string.Format("telegraphs {0:0.0}s", Duration);
    }

    /// <summary>
    /// Detonates at the selected point after a delay, driven by its own object so it still
    /// lands if the caster dies first.
    /// </summary>
    [System.Serializable]
    public class DelayedBlastEffect : AbilityEffect
    {
        public float Delay = 0.9f;
        public float Radius = 9f;
        public float Damage = 95f;
        public float Knockback = 10f;
        public float MinFraction = 0.5f;
        public bool ScaleRadiusWithLevel = true;

        public override bool Execute(AbilityContext ctx)
        {
            DelayedBlast.Spawn(ctx.Point, Radius * (ScaleRadiusWithLevel ? ctx.LevelScale : 1f),
                Delay, Damage * ctx.Power, ctx.DamageType, ctx.Team, ctx.Caster,
                new List<StatusApplication>(ctx.Payload), ctx.Tint, Knockback, MinFraction);
            return true;
        }

        public override string Describe() => string.Format("blast after {0:0.0}s", Delay);
    }

    /// <summary>A blast on a timer, independent of whoever created it.</summary>
    public class DelayedBlast : MonoBehaviour
    {
        private float _timer;
        private float _radius;
        private float _damage;
        private float _knockback;
        private float _minFraction;
        private DamageType _damageType;
        private Team _team;
        private GameObject _source;
        private List<StatusApplication> _statuses;
        private Color _tint;

        public static DelayedBlast Spawn(Vector3 point, float radius, float delay, float damage,
            DamageType damageType, Team team, GameObject source, List<StatusApplication> statuses,
            Color tint, float knockback, float minFraction)
        {
            var go = new GameObject("DelayedBlast");
            go.transform.position = point;

            var d = go.AddComponent<DelayedBlast>();
            d._timer = delay;
            d._radius = radius;
            d._damage = damage;
            d._knockback = knockback;
            d._minFraction = minFraction;
            d._damageType = damageType;
            d._team = team;
            d._source = source;
            d._statuses = statuses;
            d._tint = tint;
            return d;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            DamageInfo template = DamageInfo.Create(_damage, _damageType, _team, _source);
            template.CanCrit = false;
            template = template.WithStatuses(_statuses);

            Combat.Explode(transform.position, _radius, template, Layers.HitMaskFor(_team),
                _minFraction, _knockback);

            var color = new Color(_tint.r, _tint.g, _tint.b, 0.55f);
            GameObject pop = Build.Sphere(null, "BlastPop", transform.position + Vector3.up * 0.9f,
                _radius * 0.5f, MaterialLibrary.Transparent(color), collider: false);
            FadeAndDie.Attach(pop, 0.4f, color, Vector3.one * (_radius * 2f));

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Leaves a patch of ground that keeps hurting anything standing in it. Denies space
    /// rather than dealing burst damage, which is what makes it a control tool.
    /// </summary>
    [System.Serializable]
    public class LingeringZoneEffect : AbilityEffect
    {
        public float Radius = 4.5f;
        public float Duration = 6f;
        public float DamagePerTick = 6f;
        public float TickInterval = 0.5f;
        public bool ScaleWithLevel = true;

        public override bool Execute(AbilityContext ctx)
        {
            float scale = ScaleWithLevel ? ctx.LevelScale : 1f;

            LingeringZone.Spawn(ctx.Point, Radius * scale, Duration * scale,
                DamagePerTick * ctx.Power, TickInterval, ctx.DamageType, ctx.Team, ctx.Caster,
                new List<StatusApplication>(ctx.Payload), ctx.Tint);
            return true;
        }

        public override string Describe()
            => string.Format("a {0:0}m patch for {1:0}s", Radius, Duration);
    }

    /// <summary>
    /// The patch itself. Like <see cref="DelayedBlast"/> it runs off its own object, so it
    /// outlives the caster and keeps working if they die.
    /// </summary>
    public class LingeringZone : MonoBehaviour
    {
        private float _life;
        private float _duration;
        private float _radius;
        private float _damage;
        private float _tickInterval;
        private float _tickTimer;
        private DamageType _damageType;
        private Team _team;
        private GameObject _source;
        private List<StatusApplication> _statuses;
        private Material _material;
        private Color _color;

        public static LingeringZone Spawn(Vector3 point, float radius, float duration, float damagePerTick,
            float tickInterval, DamageType damageType, Team team, GameObject source,
            List<StatusApplication> statuses, Color tint)
        {
            var go = new GameObject("LingeringZone");
            go.transform.position = point;

            var color = new Color(tint.r, tint.g, tint.b, 0.30f);
            GameObject disc = Build.GroundDisc(go.transform, "Patch", Vector3.up * 0.05f, radius,
                MaterialLibrary.Transparent(color));

            var zone = go.AddComponent<LingeringZone>();
            zone._duration = duration;
            zone._radius = radius;
            zone._damage = damagePerTick;
            zone._tickInterval = Mathf.Max(0.05f, tickInterval);
            zone._damageType = damageType;
            zone._team = team;
            zone._source = source;
            zone._statuses = statuses;
            zone._color = color;

            var renderer = disc.GetComponent<MeshRenderer>();
            if (renderer != null) zone._material = renderer.material;

            return zone;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _life += dt;

            // Fade out over the last second so its expiry is readable.
            if (_material != null)
            {
                float remaining = _duration - _life;
                Color c = _color;
                c.a = _color.a * Mathf.Clamp01(remaining);
                MaterialLibrary.SetMaterialColor(_material, c);
            }

            _tickTimer -= dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = _tickInterval;
                Tick();
            }

            if (_life >= _duration) Destroy(gameObject);
        }

        private void Tick()
        {
            Collider[] found = Physics.OverlapSphere(transform.position, _radius,
                Layers.TargetMaskFor(_team), QueryTriggerInteraction.Ignore);

            var struck = new HashSet<IDamageable>();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target == null || !target.IsAlive || !struck.Add(target)) continue;

                DamageInfo info = DamageInfo.Create(_damage, _damageType, _team, _source);
                info.CanCrit = false;
                info = info.At(target.Transform.position + Vector3.up, Vector3.up)
                           .WithStatuses(_statuses);
                target.TakeDamage(info);
            }
        }
    }

    /// <summary>
    /// Chain Lightning. A loop with per-jump retargeting and decay is control flow, which
    /// does not belong in a data chain, so it stays one bespoke effect.
    /// </summary>
    [System.Serializable]
    public class ChainEffect : AbilityEffect
    {
        public float Damage = 26f;
        public float FirstRange = 45f;
        public float JumpRange = 10f;
        public int BaseJumps = 3;
        public float JumpsPerLevel = 1f;
        public float DecayPerJump = 0.8f;

        public override bool Execute(AbilityContext ctx)
        {
            if (!Physics.Raycast(ctx.Origin, ctx.Forward, out RaycastHit hit, FirstRange, ctx.HitMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            IDamageable current = Combat.FindDamageable(hit.collider);
            if (current == null || !current.IsAlive || current.Team == ctx.Team) return false;

            int jumps = BaseJumps + Mathf.FloorToInt(ctx.Level * JumpsPerLevel);
            float damage = Damage * ctx.Power;

            var visited = new HashSet<IDamageable>();
            Vector3 from = ctx.Origin;

            for (int i = 0; i < jumps && current != null; i++)
            {
                visited.Add(current);

                Vector3 center = AbilityContext.CenterOf(current);
                current.TakeDamage(ctx.BuildDamage(damage, center, Vector3.up));

                Combat.SpawnTracer(from, center, ctx.Tint, 0.08f, 0.16f);
                from = center;
                ctx.Point = center;

                damage *= DecayPerJump;
                current = FindNext(from, ctx, visited);
            }
            return true;
        }

        private static IDamageable FindNext(Vector3 from, AbilityContext ctx, HashSet<IDamageable> visited)
        {
            Collider[] found = Physics.OverlapSphere(from, 10f, ctx.TargetMask, QueryTriggerInteraction.Ignore);

            IDamageable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < found.Length; i++)
            {
                IDamageable d = Combat.FindDamageable(found[i]);
                if (d == null || !d.IsAlive || visited.Contains(d)) continue;

                float distance = Vector3.SqrMagnitude(d.Transform.position - from);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = d;
                }
            }
            return best;
        }

        public override string Describe() => "arcs between targets";
    }
}
