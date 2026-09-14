using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// One step of a timed impulse sequence, played by <see cref="PlayerMotor.PlaySequence"/>. Forward
    /// is the cast direction with its pitch removed; up is the motor's current up axis.
    /// </summary>
    [System.Serializable]
    public class ImpulseStep
    {
        /// <summary>Seconds after the sequence starts that this step begins.</summary>
        public float Delay;

        /// <summary>How long the forces, the gravity scale and the friction suppression last. Zero for an impulse alone.</summary>
        public float Duration;

        /// <summary>Added once, the moment the step begins.</summary>
        public float ForwardImpulse;
        public float UpImpulse;

        /// <summary>Added every second the step runs, in metres per second per second.</summary>
        public float ForwardForce;
        public float UpForce;

        /// <summary>Gravity is multiplied by this while the step runs. A slow fall is under one.</summary>
        public float GravityScale = 1f;

        /// <summary>The step stops early once the player lands, so a jump taken during it is not affected.</summary>
        public bool EndOnLanding;

        public bool SuppressFriction;
    }

    /// <summary>Plays a timed run of pushes on the caster. Ride the Gale, Bound.</summary>
    [System.Serializable]
    public class ImpulseSequenceEffect : AbilityEffect
    {
        public List<ImpulseStep> Steps = new List<ImpulseStep>();

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Motor == null) return false;
            ctx.Motor.PlaySequence(Steps, ctx.Forward);
            return true;
        }

        public override string Describe() => Steps.Count + "-step push";
    }

    /// <summary>Turns the caster's velocity and knockback around. Repulse.</summary>
    [System.Serializable]
    public class InvertVelocityEffect : AbilityEffect
    {
        public bool IncludeVertical;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Motor == null) return false;
            ctx.Motor.InvertVelocity(IncludeVertical);
            return true;
        }

        public override string Describe() => "reverse momentum";
    }

    /// <summary>
    /// The furthest walkable point along the aim within range, through walls, writing it to the point.
    /// Aborts, refunding the cast, when nowhere along the line can be stood on. A validated teleport for
    /// Blink, which pairs this with <see cref="TeleportEffect"/>.
    /// </summary>
    [System.Serializable]
    public class SelectWalkableLandingEffect : SelectorEffect
    {
        public float Range = 12f;
        public float MinDistance = 1.5f;
        public bool ScaleWithLevel = true;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return false;

            float range = Range * (ScaleWithLevel ? ctx.LevelScale : 1f);
            if (!TeleportTargeting.FurthestWalkable(NavField.Current, ctx.Caster.transform.position, ctx.Forward,
                    range, MinDistance, out Vector3 landing))
                return false;

            ctx.Point = landing;
            return true;
        }

        public override string Describe() => string.Format("to the furthest floor within {0:0}m, through walls", Range);
    }

    /// <summary>A timed buff that only applies while its condition holds. Path of Light's speed along the route.</summary>
    [System.Serializable]
    public class ConditionalBuffEffect : AbilityEffect
    {
        public string BuffId = "buff";
        public BuffCondition Condition = BuffCondition.Always;
        public Attr Attribute = Attr.MoveSpeed;

        /// <summary>A fraction: one doubles the attribute.</summary>
        public float Percent = 1f;

        public float Duration = 6f;
        public float DurationPerLevel;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerBuffs buffs = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerBuffs>() : null;
            if (buffs == null) return false;

            buffs.Add(BuffId, Condition, Attribute, Percent, Duration + (ctx.Level - 1) * DurationPerLevel);
            return true;
        }

        public override string Describe() => string.Format("+{0:0}% {1} for {2:0}s while {3}", Percent * 100f, Attribute, Duration, Condition);
    }

    /// <summary>Where a teleport through walls may land.</summary>
    public static class TeleportTargeting
    {
        /// <summary>
        /// Walks back from the full range toward the start, half a nav cell at a time, and takes the
        /// first cell that is walkable, can be walked to from where the player's body is, and has a floor
        /// under it. The walk-to rule is what keeps a landing off the far side of the map's outer wall and
        /// out of sealed rock. Needs the field built from the player's body, as it is in play.
        /// </summary>
        public static bool FurthestWalkable(NavField field, Vector3 from, Vector3 direction, float range,
            float minDistance, out Vector3 landing)
        {
            landing = from;
            if (field == null || !field.IsBuilt) return false;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f) return false;
            flat.Normalize();

            float step = NavField.CellSize * 0.5f;
            for (float distance = range; distance >= minDistance; distance -= step)
            {
                Vector3 probe = from + flat * distance;
                Vector2Int cell = field.WorldToCell(probe);
                if (!field.IsWalkable(cell.x, cell.y) || field.StepsAt(probe) < 0) continue;

                Vector3 centre = field.CellCentre(cell.x, cell.y);
                if (!FloorUnder(new Vector3(centre.x, from.y, centre.z), out float floorY)) continue;

                landing = new Vector3(centre.x, floorY, centre.z);
                return true;
            }

            return false;
        }

        private static bool FloorUnder(Vector3 at, out float y)
        {
            y = at.y;
            if (!Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 6f,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                return false;

            y = hit.point.y + 0.05f;
            return true;
        }
    }
}
