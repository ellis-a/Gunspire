using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Shared damage plumbing: crit rolls, outgoing multipliers, applying a hit to whatever
    /// a raycast found, and radial damage. Weapons, spells and enemies all funnel through here
    /// so a boon only has to be written once.
    /// </summary>
    public static class Combat
    {
        private static readonly Collider[] OverlapBuffer = new Collider[64];

        /// <summary>
        /// Global outgoing multiplier for a source. Spells scale on Intellect and guns on
        /// Strength, then both pick up whatever the sheet grants for that damage school, and
        /// spells additionally for their category.
        /// </summary>
        public static float OutgoingMultiplier(CharacterSheet sheet, bool isSpell, DamageType damageType,
            SpellType spellType = SpellType.Attack)
        {
            if (sheet == null) return 1f;

            float m = sheet.Get(Attr.DamageDealt);
            m *= isSpell ? sheet.Get(Attr.SpellPower) : sheet.Get(Attr.GunDamage);
            m *= sheet.DamageTypeMultiplier(damageType);
            if (isSpell) m *= sheet.SpellTypeMultiplier(spellType);
            return m;
        }

        public static bool RollCrit(CharacterSheet sheet, out float multiplier)
        {
            multiplier = 1f;
            if (sheet == null) return false;

            if (Random.value < sheet.Get(Attr.CritChance))
            {
                multiplier = sheet.Get(Attr.CritDamage);
                return true;
            }
            return false;
        }

        /// <summary>Finds the damageable on a collider, walking up to the rigidbody or parents.</summary>
        public static IDamageable FindDamageable(Collider collider)
        {
            if (collider == null) return null;

            if (collider.attachedRigidbody != null)
            {
                var fromBody = collider.attachedRigidbody.GetComponent<IDamageable>();
                if (fromBody != null) return fromBody;
            }
            return collider.GetComponentInParent<IDamageable>();
        }

        /// <summary>Applies a hit if the collider belongs to something damageable. Returns true if it landed.</summary>
        public static bool ApplyHit(Collider collider, in DamageInfo info)
        {
            IDamageable target = FindDamageable(collider);
            if (target == null || !target.IsAlive) return false;
            if (target.Team == info.SourceTeam && target.Team != Team.Neutral) return false;

            target.TakeDamage(info);
            return true;
        }

        /// <summary>
        /// Radial damage with linear falloff to <paramref name="minFraction"/> at the rim.
        /// Used by grenades, ground slams and Blink detonations.
        /// </summary>
        public static int Explode(Vector3 center, float radius, DamageInfo template,
            int layerMask, float minFraction = 0.35f, float knockback = 0f)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, layerMask,
                QueryTriggerInteraction.Ignore);

            var hitOnce = new HashSet<IDamageable>();
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                IDamageable target = FindDamageable(OverlapBuffer[i]);
                if (target == null || !target.IsAlive) continue;
                if (target.Team == template.SourceTeam && target.Team != Team.Neutral) continue;
                if (!hitOnce.Add(target)) continue;

                Vector3 toTarget = target.Transform.position + Vector3.up * 0.9f - center;
                float distance = toTarget.magnitude;
                float falloff = Mathf.Lerp(1f, minFraction, Mathf.Clamp01(distance / Mathf.Max(0.01f, radius)));

                DamageInfo info = template;
                info.Amount = template.Amount * falloff;
                info.HitPoint = target.Transform.position;
                info.HitNormal = distance > 0.01f ? toTarget / distance : Vector3.up;
                if (knockback > 0f) info.Knockback = info.HitNormal * knockback * falloff;

                target.TakeDamage(info);
                hits++;
            }
            return hits;
        }

        /// <summary>Everything alive on the given team inside a cone. Used by Cone of Cold and melee sweeps.</summary>
        public static List<IDamageable> ConeTargets(Vector3 origin, Vector3 forward, float range,
            float halfAngleDegrees, int layerMask)
        {
            var results = new List<IDamageable>();
            int count = Physics.OverlapSphereNonAlloc(origin, range, OverlapBuffer, layerMask,
                QueryTriggerInteraction.Ignore);

            float cosLimit = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);
            forward.Normalize();

            for (int i = 0; i < count; i++)
            {
                IDamageable target = FindDamageable(OverlapBuffer[i]);
                if (target == null || !target.IsAlive || results.Contains(target)) continue;

                Vector3 toTarget = target.Transform.position + Vector3.up * 0.9f - origin;
                float distance = toTarget.magnitude;
                if (distance > range) continue;
                if (distance > 0.05f && Vector3.Dot(toTarget / distance, forward) < cosLimit) continue;

                results.Add(target);
            }
            return results;
        }

        /// <summary>A short-lived glow where something got hit.</summary>
        /// <summary>
        /// The visual and audible pop where something landed. Every gun, spell and projectile
        /// funnels through here, so this is the one place impact feedback has to be wired.
        /// </summary>
        public static void SpawnImpact(Vector3 point, Vector3 normal, Color color, float size = 0.35f,
            DamageType type = DamageType.Normal)
        {
            GameObject go = Build.Sphere(null, "Impact", point + normal * 0.05f, size,
                MaterialLibrary.Transparent(color), collider: false);
            FadeAndDie.Attach(go, 0.18f, color, Vector3.one * (size * 3f));

            // Scaled by size so a glancing tick is quieter than a solid hit, and capped per
            // frame because one explosion can call this for everything in the blast at once.
            Sfx.PlayAt(SoundLibrary.Impact(type), point, Mathf.Clamp(size * 2.2f, 0.35f, 1f));
        }

        /// <summary>Straight line tracer between two points, for hitscan shots and beams.</summary>
        public static GameObject SpawnTracer(Vector3 from, Vector3 to, Color color, float width = 0.05f, float life = 0.06f)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return null;

            GameObject go = Build.Cube(null, "Tracer", from + delta * 0.5f,
                new Vector3(width, width, length), MaterialLibrary.Transparent(color), collider: false);
            go.transform.rotation = Quaternion.LookRotation(delta);
            FadeAndDie.Attach(go, life, color);
            return go;
        }
    }
}
