using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Settles a freshly spawned enemy: off any geometry its body would sit inside, and turned away from the nearest
    /// wall so it does not open the fight staring at one.
    ///
    /// Spots come from the nav grid or from rejection sampling, and both only know a small probe or the props they
    /// placed themselves. A wide body at the edge of a walkable cell, or a fallback spot nobody checked, ends up partly
    /// in a wall; this tests the body itself.
    /// </summary>
    public static class SpawnPlacement
    {
        private const float Margin = 0.08f;
        private const float SearchStep = 0.5f;
        private const float SearchReach = 6f;
        private const int SearchAngles = 12;
        private const float WallProbe = 12f;
        private const int FacingDirections = 72;

        /// <summary>Walls, floors and props: anything a body can visibly stand inside.</summary>
        private static int GeometryMask => Layers.BlockingMask | (1 << Layers.Prop);

        public static void Settle(EnemyController enemy)
        {
            if (enemy == null) return;

            BodySize(enemy, out float radius, out float height);
            Transform body = enemy.transform;
            Physics.SyncTransforms();

            if (!Fits(body.position, radius, height, enemy.gameObject)
                && TryFindFit(body.position, radius, height, enemy.gameObject, out Vector3 spot))
                LandingCheck.Place(body, spot);

            body.rotation = FacingAwayFromWalls(body.position + Vector3.up * Mathf.Min(1f, height * 0.5f), body.rotation);
        }

        /// <summary>
        /// The controller's size, widened to the visible body where that is wider: it is the visible part that reads as
        /// being in the wall.
        /// </summary>
        public static void BodySize(EnemyController enemy, out float radius, out float height)
        {
            var controller = enemy.GetComponent<CharacterController>();
            radius = controller != null ? controller.radius : 0.45f;
            height = controller != null ? controller.height : 1.8f;

            if (enemy.Definition != null) radius = Mathf.Max(radius, enemy.Definition.BodyWidth * 0.5f);
            radius += Margin;
        }

        private static bool Fits(Vector3 feet, float radius, float height, GameObject self)
        {
            Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.05f, height - radius);

            if (Physics.CheckCapsule(bottom, top, radius, GeometryMask, QueryTriggerInteraction.Ignore)) return false;
            return LandingCheck.IsClear(feet, radius, height, self);
        }

        /// <summary>
        /// The nearest spot that fits, in rings outward. Each ring is turned a little from the last so the candidates do
        /// not line up along the same spokes. A spot must have floor under it and be in the open from where the enemy
        /// started, so nothing is moved through a wall into the chamber next door.
        /// </summary>
        private static bool TryFindFit(Vector3 origin, float radius, float height, GameObject self, out Vector3 spot)
        {
            Vector3 chest = origin + Vector3.up;

            for (float reach = SearchStep; reach <= SearchReach; reach += SearchStep)
            {
                for (int i = 0; i < SearchAngles; i++)
                {
                    float angle = (i + reach) * (Mathf.PI * 2f / SearchAngles);
                    Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;

                    if (!HasFloor(candidate)) continue;
                    if (Physics.Linecast(chest, candidate + Vector3.up, Layers.BlockingMask, QueryTriggerInteraction.Ignore)) continue;
                    if (!Fits(candidate, radius, height, self)) continue;

                    spot = candidate;
                    return true;
                }
            }

            spot = origin;
            return false;
        }

        private static bool HasFloor(Vector3 feet) =>
            Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, 12f, Layers.BlockingMask, QueryTriggerInteraction.Ignore);

        /// <summary>
        /// Away from the nearest wall: of the directions within 60 degrees of straight away from it, the one with the most
        /// room. In a corner that turns the enemy out along the open side rather than into the other wall, and in a
        /// corridor it looks down the corridor at an angle rather than across it.
        ///
        /// The nearest wall is found with fine rays, and on its own. Weighing every wall in range let far walls outvote
        /// a thin pillar right beside the enemy.
        /// </summary>
        public static Quaternion FacingAwayFromWalls(Vector3 eye, Quaternion fallback)
        {
            var free = new float[FacingDirections];
            int nearest = -1;
            float nearestDistance = WallProbe;

            for (int i = 0; i < FacingDirections; i++)
            {
                free[i] = Physics.Raycast(eye, Direction(i), out RaycastHit wall, WallProbe, Layers.BlockingMask,
                    QueryTriggerInteraction.Ignore)
                    ? wall.distance
                    : WallProbe;

                if (free[i] < nearestDistance)
                {
                    nearestDistance = free[i];
                    nearest = i;
                }
            }

            if (nearest < 0) return fallback;

            Vector3 away = -Direction(nearest);
            int best = -1;
            float bestScore = float.MinValue;

            for (int i = 0; i < FacingDirections; i++)
            {
                float alignment = Vector3.Dot(Direction(i), away);
                if (alignment < 0.5f) continue;

                // Room first; alignment breaks ties toward straight away.
                float score = free[i] + alignment * 2f;
                if (score <= bestScore) continue;

                bestScore = score;
                best = i;
            }

            return Quaternion.LookRotation(best >= 0 ? Direction(best) : away, Vector3.up);
        }

        private static Vector3 Direction(int index)
        {
            float angle = index * Mathf.PI * 2f / FacingDirections;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }
    }
}
