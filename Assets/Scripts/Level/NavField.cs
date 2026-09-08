using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A walkable-space grid over the current room, plus a flow field pointing at the player.
    ///
    /// One breadth-first sweep outward from the player fills in the whole field, and every
    /// enemy then just reads the downhill direction in its own cell. That is one sweep per time
    /// the player crosses a cell boundary, no matter how many enemies are chasing - as opposed
    /// to a path search each, which is the same answer computed once per enemy.
    ///
    /// Walkability is probed from the world rather than derived from <see cref="MazeLayout"/>,
    /// so it covers columns, rubble and anything else built inside a chamber without the two
    /// having to agree about what they each put where.
    /// </summary>
    public class NavField : MonoBehaviour
    {
        public static NavField Current { get; private set; }

        public const float CellSize = 0.75f;

        /// <summary>The widest thing in the roster is under half a metre across.</summary>
        private const float AgentRadius = 0.55f;

        /// <summary>
        /// Where the probe sphere sits. This has to be low: an embrasure wall is solid to 0.85m
        /// and open above it, so probing at chest height would read the firing slot as a way
        /// through and send melee enemies face-first into the wall. Spanning 0.35m to 1.45m
        /// catches every sill and stops short of the floor itself.
        /// </summary>
        private const float ProbeHeight = 0.9f;

        /// <summary>How far down the gradient to look when smoothing the path direction.</summary>
        private const int Lookahead = 6;

        private int _width;
        private int _depth;
        private Vector3 _origin;        // world position of cell (0, 0)
        private bool[] _walkable;
        private int[] _distance;        // cells from the player, -1 unreachable

        private Vector2Int _source = new Vector2Int(int.MinValue, int.MinValue);
        private readonly Queue<int> _queue = new Queue<int>();

        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool IsBuilt => _walkable != null;

        // ---------------------------------------------------------------- building

        /// <summary>
        /// Probes an area centred on the origin. Called during room generation rather than from
        /// Start, so enemy placement can use it on the same frame the room is built.
        /// </summary>
        public void Build(float width, float depth)
        {
            _width = Mathf.CeilToInt(width / CellSize);
            _depth = Mathf.CeilToInt(depth / CellSize);
            _origin = new Vector3(-width * 0.5f + CellSize * 0.5f, 0f, -depth * 0.5f + CellSize * 0.5f);

            _walkable = new bool[_width * _depth];
            _distance = new int[_width * _depth];

            // The geometry was created this frame, and Unity does not push new transforms into
            // the physics scene until it has to. Without this every probe would miss.
            Physics.SyncTransforms();

            for (int y = 0; y < _depth; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 probe = CellCentre(x, y) + Vector3.up * ProbeHeight;
                    _walkable[x + y * _width] = !Physics.CheckSphere(probe, AgentRadius,
                        Layers.BlockingMask, QueryTriggerInteraction.Ignore);
                }
            }
        }

        // ---------------------------------------------------------------- queries

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _depth;

        public bool IsWalkable(int x, int y) => InBounds(x, y) && _walkable[x + y * _width];

        public Vector3 CellCentre(int x, int y)
            => _origin + new Vector3(x * CellSize, 0f, y * CellSize);

        public Vector2Int WorldToCell(Vector3 world) => new Vector2Int(
            Mathf.RoundToInt((world.x - _origin.x) / CellSize),
            Mathf.RoundToInt((world.z - _origin.z) / CellSize));

        private int Steps(Vector2Int cell)
            => InBounds(cell.x, cell.y) ? _distance[cell.x + cell.y * _width] : -1;

        /// <summary>Cells to walk to the player, or -1 if there is no route at all.</summary>
        public int StepsAt(Vector3 world) => Steps(WorldToCell(world));

        /// <summary>
        /// Can something walk straight from here to there without clipping a wall? This is what
        /// decides whether an enemy chases directly or falls back to the flow field, and it is
        /// deliberately not a line of sight test - through an embrasure slot you can see and
        /// shoot the player while having no way at all to reach them.
        /// </summary>
        public bool IsClearLine(Vector3 from, Vector3 to)
        {
            if (!IsBuilt) return true;

            Vector3 delta = to - from;
            delta.y = 0f;

            float length = delta.magnitude;
            if (length < 0.01f) return true;

            int steps = Mathf.CeilToInt(length / (CellSize * 0.5f));
            Vector3 step = delta / steps;

            for (int i = 1; i <= steps; i++)
            {
                Vector2Int cell = WorldToCell(from + step * i);
                if (!IsWalkable(cell.x, cell.y)) return false;
            }
            return true;
        }

        /// <summary>
        /// Which way to move to get closer to the player, or zero if there is no route.
        ///
        /// Follows the gradient a few cells ahead and aims at the furthest one it can walk
        /// straight to, rather than at the very next cell. Without that, a grid this coarse
        /// produces a visible stair-step wobble as an enemy crosses open ground.
        /// </summary>
        public Vector3 FlowDirection(Vector3 from)
        {
            if (!IsBuilt) return Vector3.zero;

            Vector2Int cursor = WorldToCell(from);
            if (Steps(cursor) < 0 && !TryNearestWalkable(cursor, out cursor)) return Vector3.zero;
            if (Steps(cursor) < 0) return Vector3.zero;

            Vector3 aim = CellCentre(cursor.x, cursor.y);

            for (int i = 0; i < Lookahead; i++)
            {
                Vector2Int next = Downhill(cursor);
                if (next == cursor) break;

                cursor = next;
                Vector3 candidate = CellCentre(cursor.x, cursor.y);
                if (IsClearLine(from, candidate)) aim = candidate;
            }

            Vector3 delta = aim - from;
            delta.y = 0f;
            return delta.sqrMagnitude > 0.0004f ? delta.normalized : Vector3.zero;
        }

        /// <summary>The neighbour closest to the player, or the cell itself at the bottom.</summary>
        private Vector2Int Downhill(Vector2Int cell)
        {
            int best = Steps(cell);
            Vector2Int result = cell;

            for (int i = 0; i < Neighbours.Length; i++)
            {
                Vector2Int next = cell + Neighbours[i];
                int steps = Steps(next);
                if (steps < 0 || steps >= best) continue;

                // A diagonal is only a move if both of its sides are open, or agents clip the
                // corner of a wall and get stuck on it.
                if (Neighbours[i].x != 0 && Neighbours[i].y != 0
                    && (!IsWalkable(cell.x + Neighbours[i].x, cell.y)
                        || !IsWalkable(cell.x, cell.y + Neighbours[i].y)))
                    continue;

                best = steps;
                result = next;
            }

            return result;
        }

        /// <summary>Spirals outward for somewhere an agent could actually stand.</summary>
        public bool TryNearestWalkable(Vector2Int from, out Vector2Int result)
        {
            for (int radius = 0; radius < 8; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;

                        var candidate = new Vector2Int(from.x + dx, from.y + dy);
                        if (!IsWalkable(candidate.x, candidate.y)) continue;

                        result = candidate;
                        return true;
                    }
                }
            }

            result = from;
            return false;
        }

        /// <summary>A free spot near a point, for dropping something into the world.</summary>
        public bool TryFindSpot(Rng rng, Vector3 near, float radius, out Vector3 spot)
        {
            Vector2Int centre = WorldToCell(near);
            int reach = Mathf.Max(1, Mathf.RoundToInt(radius / CellSize));

            for (int attempt = 0; attempt < 40; attempt++)
            {
                var candidate = new Vector2Int(
                    centre.x + rng.Range(-reach, reach + 1),
                    centre.y + rng.Range(-reach, reach + 1));

                if (!IsWalkable(candidate.x, candidate.y)) continue;

                spot = CellCentre(candidate.x, candidate.y);
                return true;
            }

            spot = near;
            return false;
        }

        // ---------------------------------------------------------------- the field

        private void Update()
        {
            if (!IsBuilt) return;

            PlayerRig rig = PlayerRig.Instance;
            if (rig == null) return;

            Vector2Int cell = WorldToCell(rig.transform.position);
            if (cell == _source) return;

            Rebuild(cell);
        }

        /// <summary>
        /// Breadth-first outward from the player. Four-connected, because the eight-connected
        /// version needs weighted costs to stay honest and the lookahead smoothing in
        /// <see cref="FlowDirection"/> already removes the stair-stepping that buys.
        /// </summary>
        public void Rebuild(Vector2Int playerCell)
        {
            if (!IsBuilt) return;

            _source = playerCell;

            for (int i = 0; i < _distance.Length; i++) _distance[i] = -1;

            if (!IsWalkable(playerCell.x, playerCell.y)
                && !TryNearestWalkable(playerCell, out playerCell))
                return;

            _queue.Clear();
            int start = playerCell.x + playerCell.y * _width;
            _distance[start] = 0;
            _queue.Enqueue(start);

            while (_queue.Count > 0)
            {
                int index = _queue.Dequeue();
                int x = index % _width;
                int y = index / _width;
                int next = _distance[index] + 1;

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + Neighbours[i].x;
                    int ny = y + Neighbours[i].y;
                    if (!IsWalkable(nx, ny)) continue;

                    int neighbour = nx + ny * _width;
                    if (_distance[neighbour] >= 0) continue;

                    _distance[neighbour] = next;
                    _queue.Enqueue(neighbour);
                }
            }
        }
    }
}
