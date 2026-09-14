using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Lays hurting ground behind something moving: Flaming Skull, Burning Feet. Two rules shape it.
    ///
    /// Segments drop by distance travelled, not by time, so someone standing still does not pile patches
    /// on one spot and multiply the damage for free. And one object owns the whole trail: a single
    /// overlap each tick covers every segment however long the trail grows, rather than each patch
    /// querying on its own. Each segment is also a hazard, so the other side shies out of it.
    ///
    /// It is its own object rather than a child of what carries it, so the trail outlives the carrier
    /// and burns out on its own.
    /// </summary>
    public class TrailEmitter : MonoBehaviour
    {
        public Transform Carrier;
        public float Spacing = 1.5f;
        public float Radius = 1f;
        public float SegmentLifetime = 3f;
        public float DamagePerTick = 4f;
        public float TickInterval = 0.5f;
        public DamageType DamageType = DamageType.Energy;
        public Team Team = Team.Player;
        public GameObject Source;
        public DamageOrigin Origin = DamageOrigin.Spell;
        public List<StatusApplication> Statuses;
        public Color Tint = new Color(1f, 0.5f, 0.15f);

        private struct Segment
        {
            public Vector3 Position;
            public float Age;
            public GameObject Visual;
            public Hazards.Hazard Hazard;
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private readonly HashSet<IDamageable> _struck = new HashSet<IDamageable>();
        private Vector3 _lastDrop;
        private bool _hasDropped;
        private bool _emitting = true;
        private float _tickTimer;

        public int SegmentCount => _segments.Count;
        public bool IsEmitting => _emitting;

        public static TrailEmitter Attach(Transform carrier, Team team, GameObject source)
        {
            var trail = new GameObject("Trail").AddComponent<TrailEmitter>();
            trail.Carrier = carrier;
            trail.Team = team;
            trail.Source = source;
            return trail;
        }

        /// <summary>Stops laying segments. What is already down burns out, and then the trail removes itself.</summary>
        public void Stop() => _emitting = false;

        private void Update() => Step(WorldClock.DeltaTime);

        private void OnDestroy()
        {
            for (int i = 0; i < _segments.Count; i++) Hazards.Unregister(_segments[i].Hazard);
        }

        /// <summary>One frame of the trail. Update calls it on world time; public so tooling can step it.</summary>
        public void Step(float dt)
        {
            AgeSegments(dt);

            // The carrier is gone: stop laying, but let what is already down burn out.
            if (_emitting && Carrier == null) _emitting = false;
            if (_emitting) Emit();

            _tickTimer -= dt;
            if (_tickTimer <= 0f && _segments.Count > 0)
            {
                _tickTimer = Mathf.Max(0.05f, TickInterval);
                Burn();
            }

            if (!_emitting && _segments.Count == 0) Remove(gameObject);
        }

        private void AgeSegments(float dt)
        {
            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                Segment segment = _segments[i];
                segment.Age += dt;

                if (segment.Age < SegmentLifetime)
                {
                    _segments[i] = segment;
                    continue;
                }

                Hazards.Unregister(segment.Hazard);
                if (segment.Visual != null) Remove(segment.Visual);
                _segments.RemoveAt(i);
            }
        }

        private void Emit()
        {
            Vector3 at = GroundBelow(Carrier.position);
            if (_hasDropped && Flat(at - _lastDrop).magnitude < Spacing) return;

            _hasDropped = true;
            _lastDrop = at;

            var color = new Color(Tint.r, Tint.g, Tint.b, 0.35f);
            GameObject visual = Build.GroundDisc(null, "TrailSegment", at + Vector3.up * 0.05f, Radius,
                MaterialLibrary.Transparent(color));
            visual.transform.SetParent(transform, true);

            _segments.Add(new Segment
            {
                Position = at,
                Visual = visual,
                Hazard = Hazards.Register(at, Radius, Team)
            });
        }

        /// <summary>One overlap around the whole trail, then a distance check against the segments.</summary>
        private void Burn()
        {
            if (DamagePerTick <= 0f && (Statuses == null || Statuses.Count == 0)) return;

            var bounds = new Bounds(_segments[0].Position, Vector3.zero);
            for (int i = 1; i < _segments.Count; i++) bounds.Encapsulate(_segments[i].Position);

            Collider[] found = Physics.OverlapBox(bounds.center + Vector3.up,
                bounds.extents + new Vector3(Radius + 0.5f, 2f, Radius + 0.5f), Quaternion.identity,
                Layers.TargetMaskFor(Team), QueryTriggerInteraction.Ignore);

            _struck.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target == null || !target.IsAlive || !_struck.Add(target)) continue;
                if (!OnTrail(target.Transform.position)) continue;

                DamageInfo info = DamageInfo.Create(DamagePerTick, DamageType, Team, Source);
                info.CanCrit = false;
                info.Origin = Origin;
                target.TakeDamage(info.At(target.Transform.position + Vector3.up, Vector3.up).WithStatuses(Statuses));
            }
        }

        private bool OnTrail(Vector3 position)
        {
            // A little past the radius, since a body standing on the edge has most of itself on the patch.
            float reach = (Radius + 0.4f) * (Radius + 0.4f);
            for (int i = 0; i < _segments.Count; i++)
                if (Flat(position - _segments[i].Position).sqrMagnitude <= reach) return true;
            return false;
        }

        private static Vector3 GroundBelow(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : position;
        }

        private static void Remove(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
