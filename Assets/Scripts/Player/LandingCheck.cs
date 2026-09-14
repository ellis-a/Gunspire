using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Arriving somewhere a body may already be standing. Tests a capsule at the spot and moves every
    /// body it overlaps out to its edge by position, before the arrival. Knockback plays out over later
    /// frames, so shoving with it would land the arrival inside the occupant, and two character
    /// controllers overlapping tend to stick.
    ///
    /// Shared by Rewind's landing, Lich Guise, Gravewalk and an enemy returning from banishment.
    /// </summary>
    public static class LandingCheck
    {
        public static readonly int BodyMask = Layers.PlayerMask | Layers.EnemyMask | Layers.MinionMask;

        private const float Margin = 0.05f;
        private static readonly Collider[] Buffer = new Collider[32];

        /// <summary>Whether a capsule standing on this spot overlaps any body but the one arriving.</summary>
        public static bool IsClear(Vector3 feet, float radius, float height, GameObject arriving)
        {
            Physics.SyncTransforms();
            int count = Overlap(feet, radius, height);

            for (int i = 0; i < count; i++)
                if (BodyOf(Buffer[i], arriving) != null) return false;
            return true;
        }

        /// <summary>Moves every body in the way clear of the spot. Returns how many moved.</summary>
        public static int ShoveClear(Vector3 feet, float radius, float height, GameObject arriving)
        {
            Physics.SyncTransforms();
            int count = Overlap(feet, radius, height);

            var bodies = new List<Transform>(count);
            for (int i = 0; i < count; i++)
            {
                Transform body = BodyOf(Buffer[i], arriving);
                if (body != null && !bodies.Contains(body)) bodies.Add(body);
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                Transform body = bodies[i];

                Vector3 away = body.position - feet;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f)
                {
                    // Dead centre: any way out will do, so take the one behind whatever is arriving.
                    away = arriving != null ? -arriving.transform.forward : Vector3.forward;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
                }

                Vector3 target = feet + away.normalized * (radius + RadiusOf(body) + Margin);
                target.y = body.position.y;
                Place(body, OntoWalkable(target));
            }

            if (bodies.Count > 0) Physics.SyncTransforms();
            return bodies.Count;
        }

        /// <summary>Puts a body at a position directly, with its controller off for the move so nothing undoes it.</summary>
        public static void Place(Transform body, Vector3 position)
        {
            var controller = body.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;

            if (controller != null) controller.enabled = false;
            body.position = position;
            if (controller != null) controller.enabled = wasEnabled;
        }

        private static int Overlap(Vector3 feet, float radius, float height)
        {
            Vector3 bottom = feet + Vector3.up * radius;
            Vector3 top = feet + Vector3.up * Mathf.Max(radius, height - radius);
            return Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Buffer, BodyMask, QueryTriggerInteraction.Ignore);
        }

        private static Transform BodyOf(Collider collider, GameObject arriving)
        {
            if (collider == null) return null;

            var controller = collider.GetComponentInParent<CharacterController>();
            Transform body = controller != null ? controller.transform : collider.transform;

            if (arriving != null && (body.gameObject == arriving || body.IsChildOf(arriving.transform))) return null;
            return body;
        }

        private static float RadiusOf(Transform body)
        {
            var controller = body.GetComponent<CharacterController>();
            return controller != null ? controller.radius : 0.4f;
        }

        /// <summary>A shove that would put a body inside a wall puts it on the nearest open cell instead.</summary>
        private static Vector3 OntoWalkable(Vector3 position)
        {
            NavField field = NavField.Current;
            if (field == null || !field.IsBuilt) return position;

            Vector2Int cell = field.WorldToCell(position);
            if (field.IsWalkable(cell.x, cell.y) || !field.TryNearestWalkable(cell, out Vector2Int open)) return position;

            Vector3 centre = field.CellCentre(open.x, open.y);
            return new Vector3(centre.x, position.y, centre.z);
        }
    }
}
