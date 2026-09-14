using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Volumes that stop shots rather than bodies. Each is a solid collider on a layer that is in the
    /// shooters' hit masks but out of the collision matrix against every body, out of
    /// <see cref="Layers.BlockingMask"/> and out of <see cref="Layers.SightBlockMask"/>. So bodies walk
    /// through, pathfinding ignores them, sight carries on through, and only shots are stopped.
    /// </summary>
    public static class WorldVolumes
    {
        /// <summary>Stops shots from both sides. Size and duration are still open decisions, so both are parameters.</summary>
        public static GameObject NetherWall(Vector3 centre, Quaternion rotation, Vector3 size, float lifetime, Color tint)
        {
            GameObject wall = Box("NetherWall", centre, rotation, size, Layers.NetherWall, tint, 0.3f);
            wall.AddComponent<WorldTimedLife>().Seconds = lifetime;
            return wall;
        }

        /// <summary>
        /// Stops the player's shots only, and hands each one to a random enemy inside. Whether it blocks
        /// enemy sight or counts as a hazard is still open, so it does neither.
        /// </summary>
        public static ShotRedirectVolume NetherSmoke(Vector3 centre, Vector3 size, float lifetime, Color tint)
        {
            GameObject smoke = Box("NetherSmoke", centre, Quaternion.identity, size, Layers.Smoke, tint, 0.55f);
            smoke.AddComponent<WorldTimedLife>().Seconds = lifetime;
            return smoke.AddComponent<ShotRedirectVolume>();
        }

        private static GameObject Box(string name, Vector3 centre, Quaternion rotation, Vector3 size, int layer,
            Color tint, float alpha)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(centre, rotation);
            root.AddComponent<BoxCollider>().size = size;

            GameObject look = Build.Cube(null, "Look", centre, size,
                MaterialLibrary.Transparent(new Color(tint.r, tint.g, tint.b, alpha)), collider: false);
            look.transform.rotation = rotation;
            look.transform.SetParent(root.transform, true);

            Layers.SetRecursively(root, layer);
            return root;
        }
    }

    /// <summary>
    /// A volume that takes a shot and lands it on someone inside instead: a random target for the
    /// shooter's side, found by overlap. With nobody inside, the shot carries on through.
    /// </summary>
    public class ShotRedirectVolume : MonoBehaviour
    {
        /// <summary>Whose shots it hands on. Everyone else's pass straight through, since only their hit mask holds the layer.</summary>
        public Team HandsOnFor = Team.Player;

        private BoxCollider _box;
        private readonly List<IDamageable> _candidates = new List<IDamageable>();

        public bool TryPickTarget(Team shooterTeam, out IDamageable target)
        {
            target = null;
            if (shooterTeam != HandsOnFor) return false;

            if (_box == null) _box = GetComponent<BoxCollider>();
            if (_box == null) return false;

            Bounds bounds = _box.bounds;
            Collider[] found = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                Layers.TargetMaskFor(shooterTeam), QueryTriggerInteraction.Ignore);

            _candidates.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable candidate = Combat.FindDamageable(found[i]);
                if (candidate == null || !candidate.IsAlive || candidate.Team == shooterTeam) continue;
                if (!_candidates.Contains(candidate)) _candidates.Add(candidate);
            }

            if (_candidates.Count == 0) return false;

            target = _candidates[Random.Range(0, _candidates.Count)];
            return true;
        }
    }

    /// <summary>Removes its object after a length of world time, so a stopped world holds it in place too.</summary>
    public class WorldTimedLife : MonoBehaviour
    {
        public float Seconds = 5f;

        private void Update()
        {
            Seconds -= WorldClock.DeltaTime;
            if (Seconds <= 0f) Destroy(gameObject);
        }
    }
}
