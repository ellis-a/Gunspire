using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A body knockback can move, and so one that can strike something while it does: enemies,
    /// walking minions and the player.
    /// </summary>
    public interface IKnockable
    {
        Transform Transform { get; }
        Health Health { get; }

        /// <summary>Everything it is moving with, its own walking included, for how fast two bodies close.</summary>
        Vector3 Velocity { get; }

        /// <summary>The knockback still playing out, zero when none is. Impacts spend it.</summary>
        Vector3 Knockback { get; set; }

        GameObject Instigator { get; }
        Team InstigatorTeam { get; }

        void AddKnockback(Vector3 velocity, GameObject instigator, Team instigatorTeam);
    }

    /// <summary>
    /// Anything knocked into something hard takes damage, from any source of knockback. A body that
    /// strikes another hurts both and hands on part of its speed, so impacts cascade through a crowd.
    ///
    /// Damage comes from closing speed, not speed: a crowd shoved forward together is fast and touching
    /// its neighbours, and would grind itself to death on absolute speed. Below a threshold nothing
    /// happens, which stops walking into things hurting and lets cascades end, since each transfer
    /// hands on less than it received. All three numbers are first guesses.
    /// </summary>
    public static class KnockbackImpacts
    {
        /// <summary>Closing speed, in metres per second, below which an impact does nothing.</summary>
        public const float Threshold = 6f;

        /// <summary>Damage per metre per second of closing speed above the threshold.</summary>
        public const float DamagePerSpeed = 2.5f;

        /// <summary>How much of the closing speed a struck body takes on. Under one, so cascades die out.</summary>
        public const float Transfer = 0.5f;

        /// <summary>Multiplies impact damage from knockback the player caused. Pinball Wizard.</summary>
        public static float PlayerImpactScale = 1f;

        public static float WallClosingSpeed(Vector3 knockback, Vector3 wallNormal)
            => Mathf.Max(0f, -Vector3.Dot(knockback, wallNormal));

        public static float BodyClosingSpeed(Vector3 velocityA, Vector3 velocityB, Vector3 positionA, Vector3 positionB)
        {
            Vector3 direction = Flat(positionB - positionA);
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Max(0f, Vector3.Dot(velocityA - velocityB, direction.normalized));
        }

        public static float DamageFor(float closingSpeed)
            => closingSpeed <= Threshold ? 0f : (closingSpeed - Threshold) * DamagePerSpeed;

        /// <summary>
        /// Called from a body's collision report. Only a body with knockback playing out can strike
        /// anything, so a creature's own lunge, dash or walk never counts.
        /// </summary>
        public static void OnControllerHit(IKnockable body, ControllerColliderHit hit)
        {
            if (body == null || hit == null || hit.collider == null) return;
            if (body.Knockback.sqrMagnitude < Threshold * Threshold) return;

            // Floors and ceilings are landings, not impacts.
            if (Mathf.Abs(hit.normal.y) > 0.6f) return;

            IKnockable other = hit.collider.GetComponentInParent<IKnockable>();
            if (other != null && !ReferenceEquals(other, body))
            {
                ApplyBodyImpact(body, other);
                return;
            }

            if (((1 << hit.collider.gameObject.layer) & Layers.BlockingMask) != 0)
                ApplyWallImpact(body, hit.normal);
        }

        /// <summary>A knocked body meets a wall. Returns the damage dealt.</summary>
        public static float ApplyWallImpact(IKnockable body, Vector3 wallNormal)
        {
            Vector3 normal = Flat(wallNormal);
            if (normal.sqrMagnitude < 0.0001f) return 0f;
            normal.Normalize();

            float closing = WallClosingSpeed(body.Knockback, normal);
            if (closing <= 0f) return 0f;

            // The push into the wall is spent whether it hurt or not, or the same wall is struck
            // again on the very next move.
            body.Knockback += normal * closing;

            float damage = DamageFor(closing);
            if (damage > 0f) Hurt(body, damage, body.Instigator, body.InstigatorTeam);
            return damage;
        }

        /// <summary>
        /// A knocked body meets another. Both take the damage, and the struck one takes on part of the
        /// speed along the line between them, carrying the same instigator. Returns the damage dealt.
        /// </summary>
        public static float ApplyBodyImpact(IKnockable mover, IKnockable struck)
        {
            Vector3 direction = Flat(struck.Transform.position - mover.Transform.position);
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            direction.Normalize();

            float closing = BodyClosingSpeed(mover.Velocity, struck.Velocity,
                mover.Transform.position, struck.Transform.position);
            if (closing <= Threshold) return 0f;

            Vector3 handedOn = direction * (closing * Transfer);
            mover.Knockback -= handedOn;
            struck.AddKnockback(handedOn, mover.Instigator, mover.InstigatorTeam);

            float damage = DamageFor(closing);
            Hurt(mover, damage, mover.Instigator, mover.InstigatorTeam);
            Hurt(struck, damage, mover.Instigator, mover.InstigatorTeam);
            return damage;
        }

        /// <summary>
        /// Issued on the instigator's behalf, so friendly fire does not refuse it. Onto the instigator's
        /// own side - its minions, or the player - only the neutral team can land a hit, so that is what
        /// carries it there.
        /// </summary>
        private static void Hurt(IKnockable body, float damage, GameObject instigator, Team instigatorTeam)
        {
            Health health = body.Health;
            if (health == null || !health.IsAlive || damage <= 0f) return;

            Team issuing = health.Team == instigatorTeam ? Team.Neutral : instigatorTeam;
            if (instigator != null && PlayerRig.Instance != null && instigator == PlayerRig.Instance.gameObject)
                damage *= Mathf.Max(0f, PlayerImpactScale);
            GameObject source = instigator == health.gameObject ? null : instigator;

            DamageInfo info = DamageInfo.OnBehalfOf(damage, DamageType.Kinetic, issuing, source, DamageOrigin.Collision);
            info.CanCrit = false;
            health.TakeDamage(info.At(body.Transform.position + Vector3.up, Vector3.up));
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
