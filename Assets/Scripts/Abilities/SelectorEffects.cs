using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Effects that decide *what* an ability affects. They write <see cref="AbilityContext.Targets"/>
    /// and <see cref="AbilityContext.Point"/>; the effects after them act on that.
    ///
    /// Selecting nothing is not a failure - a cone that catches no one still fires and still
    /// plays. Only a selector that cannot run at all returns false.
    /// </summary>
    public abstract class SelectorEffect : AbilityEffect { }

    /// <summary>Everything in a cone in front of the caster.</summary>
    [System.Serializable]
    public class SelectConeEffect : SelectorEffect
    {
        public float Range = 12f;
        public float HalfAngle = 34f;
        public bool RequireLineOfSight = true;
        public bool ScaleRangeWithLevel;

        public override bool Execute(AbilityContext ctx)
        {
            float range = Range * (ScaleRangeWithLevel ? ctx.LevelScale : 1f);

            var found = Combat.ConeTargets(ctx.Origin, ctx.Forward, range, HalfAngle, ctx.TargetMask);

            ctx.Targets.Clear();
            for (int i = 0; i < found.Count; i++)
            {
                if (RequireLineOfSight &&
                    !ctx.HasLineOfSight(ctx.Origin, AbilityContext.CenterOf(found[i]))) continue;
                ctx.Targets.Add(found[i]);
            }

            ctx.Point = ctx.Origin + ctx.Forward * (range * 0.5f);
            return true;
        }

        public override string Describe() => string.Format("cone {0:0}m / {1:0}deg", Range, HalfAngle * 2f);
    }

    /// <summary>Everything within a radius, centred on the caster or on the current point.</summary>
    [System.Serializable]
    public class SelectSphereEffect : SelectorEffect
    {
        public float Radius = 6f;
        public bool AroundPoint;           // false: around the caster
        public bool ScaleRadiusWithLevel;
        public bool RequireLineOfSight;

        public override bool Execute(AbilityContext ctx)
        {
            float radius = Radius * (ScaleRadiusWithLevel ? ctx.LevelScale : 1f);
            Vector3 center = AroundPoint ? ctx.Point : ctx.Caster.transform.position + Vector3.up * 0.9f;

            Collider[] found = Physics.OverlapSphere(center, radius, ctx.TargetMask,
                QueryTriggerInteraction.Ignore);

            ctx.Targets.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target == null || !target.IsAlive || ctx.Targets.Contains(target)) continue;
                if (RequireLineOfSight &&
                    !ctx.HasLineOfSight(center, AbilityContext.CenterOf(target))) continue;
                ctx.Targets.Add(target);
            }

            ctx.Point = center;
            return true;
        }

        public override string Describe() => string.Format("everything within {0:0}m", Radius);
    }

    /// <summary>
    /// Traces the aim line and parks <see cref="AbilityContext.Point"/> where it lands, so a
    /// later effect can detonate there. Falls back to maximum range against open sky.
    /// </summary>
    [System.Serializable]
    public class AimPointEffect : SelectorEffect
    {
        public float Range = 60f;

        public override bool Execute(AbilityContext ctx)
        {
            ctx.Point = ctx.Origin + ctx.Forward * Range;

            if (Physics.Raycast(ctx.Origin, ctx.Forward, out RaycastHit hit, Range, ctx.HitMask,
                    QueryTriggerInteraction.Ignore))
                ctx.Point = hit.point;

            return true;
        }

        public override string Describe() => string.Format("at the point you are aiming ({0:0}m)", Range);
    }

    /// <summary>
    /// Sweeps the caster's capsule forward and stops at the first wall, writing the furthest
    /// safe position to <see cref="AbilityContext.Point"/>.
    ///
    /// Aborts if there is not enough room, which refunds the cast rather than teleporting the
    /// player half a metre into a corner.
    /// </summary>
    [System.Serializable]
    public class SweepForwardEffect : SelectorEffect
    {
        public float BaseDistance = 9f;
        public float PerIntellect = 0.18f;
        public float MaxDistance = 22f;
        public float MinDistance = 0.4f;
        public float PitchDownLimit = -0.25f;
        public float PitchUpLimit = 0.45f;
        public bool ScaleWithLevel = true;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Controller == null) return false;

            Vector3 direction = ctx.Forward;
            direction.y = Mathf.Clamp(direction.y, PitchDownLimit, PitchUpLimit);
            if (direction.sqrMagnitude < 0.001f) direction = ctx.Caster.transform.forward;
            direction.Normalize();

            int intellect = ctx.Sheet != null ? ctx.Sheet.GetStat(StatType.Intellect) : 5;
            float distance = BaseDistance + intellect * PerIntellect;
            if (ScaleWithLevel) distance *= ctx.LevelScale;
            distance = Mathf.Min(MaxDistance, distance);

            distance = ClampToGeometry(ctx.Controller, ctx.Caster.transform.position, direction, distance);
            if (distance < MinDistance) return false;

            ctx.Forward = direction;
            ctx.Point = ctx.Caster.transform.position + direction * distance;
            return true;
        }

        private static float ClampToGeometry(CharacterController cc, Vector3 origin, Vector3 direction,
            float distance)
        {
            float radius = cc.radius * 0.92f;
            Vector3 center = origin + cc.center;
            float half = Mathf.Max(0.01f, cc.height * 0.5f - cc.radius);

            if (Physics.CapsuleCast(center - Vector3.up * half, center + Vector3.up * half, radius,
                    direction, out RaycastHit hit, distance, Layers.BlockingMask,
                    QueryTriggerInteraction.Ignore))
                return Mathf.Max(0f, hit.distance - 0.12f);

            return distance;
        }

        public override string Describe() => string.Format("sweep up to {0:0}m forward", MaxDistance);
    }

    /// <summary>Parks the point on the caster, for self-centred blasts.</summary>
    [System.Serializable]
    public class PointAtCasterEffect : SelectorEffect
    {
        public float HeightOffset = 0.9f;

        public override bool Execute(AbilityContext ctx)
        {
            ctx.Point = ctx.Caster.transform.position + Vector3.up * HeightOffset;
            return true;
        }

        public override string Describe() => "centred on you";
    }
}
