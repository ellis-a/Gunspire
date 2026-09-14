using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Picks what an aim-assisted gun fires at: the living hostile nearest the reticle by angle, among
    /// those in line of sight. By angle rather than distance, so the nearest enemy behind you is never it
    /// and looking toward a threat switches to it.
    /// </summary>
    public static class AutoAim
    {
        private static readonly Collider[] Buffer = new Collider[64];

        public static IDamageable PickTarget(Vector3 origin, Vector3 forward, float range, float maxAngle, Team team)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, range, Buffer, Layers.TargetMaskFor(team),
                QueryTriggerInteraction.Ignore);

            IDamageable best = null;
            float bestAngle = maxAngle;

            for (int i = 0; i < count; i++)
            {
                IDamageable target = Combat.FindDamageable(Buffer[i]);
                if (target == null || !target.IsAlive || (target.Team == team && team != Team.Neutral)) continue;

                Vector3 centre = AbilityContext.CenterOf(target);
                float angle = Vector3.Angle(forward, centre - origin);
                if (angle > bestAngle || !ClearLine(origin, centre)) continue;

                best = target;
                bestAngle = angle;
            }

            return best;
        }

        public static bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.3f, Layers.SightBlockMask,
                QueryTriggerInteraction.Ignore);
        }
    }

    /// <summary>
    /// Makes a gun fire itself at whatever <see cref="AutoAim"/> picks, never missing: hitscan rounds go
    /// straight at the target and projectiles steer onto it. It holds fire with nothing in sight, since
    /// firing at nothing would only make noise. Fire rate is the gun's fastest, so a semi-automatic gun
    /// fires as fast as it can. Superid.
    /// </summary>
    public class AutoFireDriver : MonoBehaviour
    {
        public Weapon Weapon;
        public Transform Aim;
        public StatusController Status;

        /// <summary>How far off the reticle a target may be. The whole view by default.</summary>
        public float MaxAngle = 180f;

        public bool Active { get; private set; }
        public IDamageable Target { get; private set; }

        public void Begin()
        {
            Active = true;
            if (Weapon != null) Weapon.AutoFire = true;
        }

        public void End()
        {
            Active = false;
            Target = null;

            if (Weapon == null) return;
            Weapon.AutoFire = false;
            Weapon.ForcedTarget = null;
        }

        private void Update()
        {
            if (Active) Step();
        }

        /// <summary>One frame: pick a target and fire at it if there is one. Public so tooling can drive it.</summary>
        public void Step()
        {
            if (Weapon == null || Aim == null) return;

            float range = Weapon.Definition != null ? Weapon.Definition.Range : 60f;
            Target = AutoAim.PickTarget(Aim.position, Aim.forward, range, MaxAngle, Weapon.OwnerTeam);
            Weapon.ForcedTarget = Target != null ? Target.Transform : null;

            if (Target == null || (Status != null && Status.IsDisarmed)) return;
            Weapon.TryFire();
        }
    }
}
