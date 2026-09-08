using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The pause that makes an attack readable. Aborts if the caster is killed or frozen
    /// part-way through, which is what lets a well-timed Cone of Cold cancel a wind-up.
    /// </summary>
    [System.Serializable]
    public class WaitEffect : AbilityEffect, ITimedEffect
    {
        public float Seconds = 0.5f;
        public bool AbortIfInterrupted = true;

        public override bool Execute(AbilityContext ctx) => true;   // instant chains skip the pause

        public IEnumerator Run(AbilityContext ctx)
        {
            float elapsed = 0f;
            while (elapsed < Seconds)
            {
                if (AbortIfInterrupted && !ctx.CasterCanAct)
                {
                    ctx.Aborted = true;
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        public override string Describe() => null;
    }

    /// <summary>Runs a sub-chain several times on an interval: a volley, a series of slams.</summary>
    [System.Serializable]
    public class RepeatEffect : AbilityEffect, ITimedEffect
    {
        public int Times = 3;
        public float Interval = 0.2f;
        public bool AbortIfInterrupted = true;
        [SerializeReference] public List<AbilityEffect> Body = new List<AbilityEffect>();

        public override bool Execute(AbilityContext ctx)
        {
            for (int i = 0; i < Times; i++)
                if (!AbilityRunner.Run(Body, ctx)) return false;
            return true;
        }

        public IEnumerator Run(AbilityContext ctx)
        {
            for (int i = 0; i < Times; i++)
            {
                if (AbortIfInterrupted && !ctx.CasterCanAct)
                {
                    ctx.Aborted = true;
                    yield break;
                }

                yield return AbilityRunner.RunTimed(Body, ctx);
                if (ctx.Aborted) yield break;

                if (Interval > 0f) yield return new WaitForSeconds(Interval);
            }
        }

        public override string Describe() => Times + "x " + AbilityRunner.Describe(Body);
    }

    /// <summary>
    /// A sustained beam that sweeps sideways while it fires. Dodging it means leaving the
    /// line, not out-running the damage, so the sweep is what stops standing still working.
    ///
    /// Sustained state over time is not something a data chain expresses, so this is one of
    /// the deliberate bespoke effects.
    /// </summary>
    [System.Serializable]
    public class BeamEffect : AbilityEffect, ITimedEffect
    {
        public float Duration = 1.4f;
        public float Length = 40f;
        public float Width = 0.55f;
        public float DamagePerTick = 6f;
        public float TickInterval = 0.12f;
        public float SweepDegreesPerSecond = 26f;

        /// <summary>
        /// How far off horizontal the beam may point. Ground casters are held near level so a
        /// sweep stays readable; a flier firing down at the player needs the full range, and
        /// at 0.25 would shoot over their head from anywhere close.
        /// </summary>
        public float MaxPitch = 0.25f;

        public override bool Execute(AbilityContext ctx) => true;

        public IEnumerator Run(AbilityContext ctx)
        {
            Vector3 direction = ctx.Forward;
            direction.y = Mathf.Clamp(direction.y, -MaxPitch, MaxPitch);
            direction.Normalize();

            float sweepSign = Random.value < 0.5f ? -1f : 1f;

            GameObject beam = Build.Cube(null, "Beam", Vector3.zero, Vector3.one,
                MaterialLibrary.Emissive(ctx.Tint, 3f), collider: false);

            // The beam owns its own lifetime from the moment it exists. This coroutine runs on
            // the caster, and killing the caster mid-beam stops the coroutine dead - the code
            // after the loop never runs - so cleanup cannot live down there.
            BeamVisual visual = BeamVisual.Attach(beam, ctx.Tint, Duration);

            Sfx.PlayAt(SoundLibrary.Beam(ctx.DamageType),
                ctx.Aim != null ? ctx.Aim.position : ctx.Origin, 0.85f);

            float elapsed = 0f;
            float tickTimer = 0f;

            // The beam can now retire itself, so never assume it is still there. In practice
            // its cap sits well past Duration, but a long enough stall lets wall-clock time
            // outrun accumulated deltaTime, and touching a destroyed transform would throw.
            while (elapsed < Duration && ctx.CasterCanAct && beam != null)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                direction = Quaternion.AngleAxis(SweepDegreesPerSecond * sweepSign * dt, Vector3.up) * direction;
                Vector3 origin = ctx.Aim != null ? ctx.Aim.position : ctx.Origin;

                float reach = Length;
                if (Physics.Raycast(origin, direction, out RaycastHit wall, Length,
                        Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                    reach = wall.distance;

                beam.transform.position = origin + direction * (reach * 0.5f);
                beam.transform.rotation = Quaternion.LookRotation(direction);
                beam.transform.localScale = new Vector3(Width, Width, reach);
                visual.Keep();

                tickTimer -= dt;
                if (tickTimer <= 0f)
                {
                    tickTimer = TickInterval;
                    Burn(ctx, origin, direction, reach);
                }
                yield return null;
            }

            if (visual != null) visual.Finish();
        }

        private void Burn(AbilityContext ctx, Vector3 origin, Vector3 direction, float reach)
        {
            if (!Physics.SphereCast(origin, Width * 0.5f, direction, out RaycastHit hit, reach,
                    ctx.TargetMask, QueryTriggerInteraction.Ignore))
                return;

            IDamageable target = Combat.FindDamageable(hit.collider);
            if (target == null || !target.IsAlive) return;

            target.TakeDamage(ctx.BuildDamage(DamagePerTick * ctx.Power, hit.point, -direction));
        }

        public override string Describe() => "a sweeping beam";
    }

    /// <summary>
    /// Keeps a beam alive only while something is actively driving it.
    ///
    /// A beam is the one piece of ability geometry whose position is updated every frame by
    /// the coroutine that made it, rather than being fired and forgotten like a telegraph
    /// or a blast. That made it the one piece whose cleanup lived in the coroutine, and
    /// Unity stops a coroutine the moment its GameObject is destroyed - without unwinding
    /// it, so a finally block would not have run either. Kill a caster mid-beam and the
    /// beam was orphaned at the scene root with nothing left holding a reference to it.
    ///
    /// So the beam watches for its driver going quiet instead of being told what happened.
    /// It does not need to know whether the caster died, the ability aborted, or the room
    /// was torn down underneath it.
    /// </summary>
    public class BeamVisual : MonoBehaviour
    {
        /// <summary>
        /// How long to wait after the last update before assuming nobody is driving this.
        /// Comfortably longer than a frame even during a bad hitch, and short enough that
        /// an orphan is gone before it registers as a stuck laser.
        /// </summary>
        public float GraceSeconds = 0.35f;

        public float FadeSeconds = 0.15f;
        public Color Tint = Color.white;

        /// <summary>
        /// Hard ceiling, in case something keeps calling Keep forever. Derived from the
        /// beam's own duration rather than a constant, so a long beam is never truncated.
        /// </summary>
        public float MaxLifetime = 5f;

        private float _lastKept;
        private float _born;
        private bool _finishing;

        public static BeamVisual Attach(GameObject beam, Color tint, float duration)
        {
            var visual = beam.AddComponent<BeamVisual>();
            visual.Tint = tint;
            visual.MaxLifetime = duration + visual.GraceSeconds + 1f;
            return visual;
        }

        private void Awake()
        {
            _born = Time.time;
            _lastKept = _born;
        }

        /// <summary>Called every frame by whatever is driving the beam.</summary>
        public void Keep() => _lastKept = Time.time;

        /// <summary>Normal completion: fade out rather than vanish.</summary>
        public void Finish()
        {
            if (_finishing) return;
            _finishing = true;

            FadeAndDie.Attach(gameObject, FadeSeconds, Tint);
            enabled = false;
        }

        private void Update()
        {
            if (_finishing) return;

            if (Time.time - _lastKept > GraceSeconds || Time.time - _born > MaxLifetime)
                Finish();
        }
    }

    // ================================================================================ aiming

    /// <summary>
    /// Points the chain at whatever the caster is attacking. Enemy attacks call this after
    /// their wind-up so the shot leaves in the right direction, and before a telegraph so the
    /// warning shows where the attack will actually go.
    /// </summary>
    [System.Serializable]
    public class AimAtTargetEffect : AbilityEffect
    {
        public float AimHeight = 0.95f;
        public bool Flatten;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.TargetTransform == null) return false;   // target died: drop the attack

            ctx.Origin = ctx.Aim != null ? ctx.Aim.position : ctx.Caster.transform.position + Vector3.up;

            Vector3 to = ctx.TargetTransform.position + Vector3.up * AimHeight - ctx.Origin;
            if (Flatten) to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return false;

            ctx.Forward = to.normalized;
            ctx.Point = ctx.TargetTransform.position;
            return true;
        }

        public override string Describe() => null;
    }

    /// <summary>
    /// Moves the origin to wherever the caster is now, leaving the direction alone.
    ///
    /// A lunging melee swing needs this: the direction was locked at wind-up so strafing
    /// beats it, but the swing still has to land from wherever the attacker ended up.
    /// </summary>
    [System.Serializable]
    public class OriginFromCasterEffect : AbilityEffect
    {
        public float Height = 1.0f;

        public override bool Execute(AbilityContext ctx)
        {
            ctx.Origin = ctx.Caster.transform.position + Vector3.up * Height;
            return true;
        }

        public override string Describe() => null;
    }

    /// <summary>
    /// Parks the point on the ground under the target, optionally led ahead of where they are
    /// moving. Areas landing where you *were* is what forces constant movement.
    /// </summary>
    [System.Serializable]
    public class TargetGroundPointEffect : AbilityEffect
    {
        public float LeadDistance;
        public float LeadSeconds = 0.35f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.TargetTransform == null) return false;

            Vector3 point = ctx.TargetTransform.position;

            if (LeadDistance > 0f)
            {
                var motor = ctx.TargetTransform.GetComponent<PlayerMotor>();
                if (motor != null)
                {
                    Vector3 lead = motor.Velocity;
                    lead.y = 0f;
                    point += Vector3.ClampMagnitude(lead * LeadSeconds, LeadDistance);
                }
            }

            if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 12f,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                point = hit.point;

            ctx.Point = point;
            return true;
        }

        public override string Describe() => null;
    }

    // ================================================================================ telegraphs

    /// <summary>A warning line along the firing direction. Pair it with a wait.</summary>
    [System.Serializable]
    public class TelegraphLineEffect : AbilityEffect
    {
        public float Length = 40f;
        public float Width = 0.33f;
        public float Duration = 1.1f;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 origin = ctx.Aim != null ? ctx.Aim.position : ctx.Origin;
            Telegraph.Line(origin, ctx.Forward, Length, Width, Duration, ctx.Tint);
            return true;
        }

        public override string Describe() => null;
    }

    [System.Serializable]
    public class TelegraphConeEffect : AbilityEffect
    {
        public float Range = 11f;
        public float HalfAngle = 32f;
        public float Duration = 0.85f;

        public override bool Execute(AbilityContext ctx)
        {
            Telegraph.Cone(ctx.Origin, ctx.Forward, Range, HalfAngle, Duration, ctx.Tint);
            return true;
        }

        public override string Describe() => null;
    }

    /// <summary>A glow on the caster, so a melee wind-up reads at close range.</summary>
    [System.Serializable]
    public class TelegraphFlashEffect : AbilityEffect
    {
        public float Radius = 0.4f;
        public float Duration = 0.55f;
        public float Height = 1.2f;

        public override bool Execute(AbilityContext ctx)
        {
            Telegraph.Flash(ctx.Caster.transform, Vector3.up * Height, Radius, Duration, ctx.Tint);
            return true;
        }

        public override string Describe() => null;
    }
}
