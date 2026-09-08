using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>How two neighbouring chambers meet.</summary>
    public enum EdgeState
    {
        /// <summary>A full wall. No way through.</summary>
        Solid,

        /// <summary>A wall with a gap in the middle.</summary>
        Door,

        /// <summary>No wall at all, so the two chambers read as one larger hall.</summary>
        Open
    }

    /// <summary>What a doorway looks like, and so who fits through it.</summary>
    public enum DoorStyle
    {
        /// <summary>A wide opening. Anything walks through.</summary>
        Arch,

        /// <summary>A narrow gap. Still walkable, but a chokepoint.</summary>
        Narrow,

        /// <summary>
        /// A tall opening with a raised sill: the player jumps it, fliers cross it, and ground
        /// enemies cannot. Only ever placed on a loop edge, so it is a shortcut and a filter
        /// rather than a gate - nothing is ever locked behind one.
        /// </summary>
        Ledge
    }

    /// <summary>What fills a chamber. Every one of these is non-blocking by construction.</summary>
    public enum ChamberArchetype
    {
        /// <summary>Nothing. Density only reads as variety if something is sparse.</summary>
        Bare,

        /// <summary>A regular grid of columns, so it reads as architecture rather than debris.</summary>
        Colonnade,

        /// <summary>Thin columns on a jittered grid. Close quarters.</summary>
        PillarForest,

        /// <summary>A solid block in the middle with a corridor all the way around it.</summary>
        Keep,

        /// <summary>A circular chamber: the corners of the cell filled in, gaps at the doorways.</summary>
        Rotunda,

        /// <summary>Smashable crates. Cover that does not last.</summary>
        Rubble,

        /// <summary>Stub walls forming pockets against the chamber walls.</summary>
        Alcoves,

        /// <summary>
        /// A wall across the middle with a firing slot in it: two lanes that can shoot each
        /// other and cannot reach each other. Getting across means going round through the maze.
        /// </summary>
        Embrasure
    }

    /// <summary>
    /// One or more cells that read as a single space, because the walls between them were
    /// dropped entirely. Archetypes are assigned per group rather than per cell so a merged
    /// hall does not end up with two different rooms inside it.
    /// </summary>
    public class ChamberGroup
    {
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();

        public ChamberArchetype Archetype = ChamberArchetype.Bare;

        /// <summary>For <see cref="ChamberArchetype.Embrasure"/>: does the dividing wall run east-west?</summary>
        public bool EmbrasureAlongX;

        /// <summary>World centre and extents of the bounding box.</summary>
        public Vector3 Centre;
        public Vector2 Size;

        /// <summary>False for an L-shaped merge, where the bounding box covers cells that are not here.</summary>
        public bool IsRectangular;
    }

    /// <summary>Tuning for one maze. Kept apart from the layout so a room kind can size its own.</summary>
    public struct MazeSettings
    {
        public int Width;
        public int Height;

        /// <summary>Metres per cell, walls included.</summary>
        public float CellSize;

        /// <summary>Width of the gap in an <see cref="DoorStyle.Arch"/> doorway.</summary>
        public float DoorWidth;

        /// <summary>Chance a cell is solid rock instead of a chamber. Ignored if it would split the maze.</summary>
        public float RockChance;

        /// <summary>Chance an edge the spanning tree left walled is opened anyway, creating a loop.</summary>
        public float LoopChance;

        /// <summary>Chance a doorway is widened into a full opening, merging two chambers.</summary>
        public float MergeChance;

        /// <summary>Chance a doorway is a chokepoint instead of an arch.</summary>
        public float NarrowChance;

        /// <summary>Chance a loop doorway is raised into a jump-only shortcut.</summary>
        public float LedgeChance;

        /// <summary>
        /// Chambers big enough to fight in and doorways wide enough to run through. A tighter
        /// maze reads better on paper and ruins the movement, which is the whole draw here.
        /// </summary>
        public static MazeSettings For(RoomKind kind)
        {
            var settings = new MazeSettings
            {
                Width = 4,
                Height = 4,
                CellSize = 16f,
                DoorWidth = 7f,
                RockChance = 0.12f,
                LoopChance = 0.25f,
                MergeChance = 0.15f,
                NarrowChance = 0.18f,
                LedgeChance = 0.35f
            };

            if (kind == RoomKind.Boss)
            {
                // A boss wants one big space, not a warren: fewer, larger cells, mostly merged.
                settings.Width = 3;
                settings.Height = 3;
                settings.CellSize = 22f;
                settings.RockChance = 0f;
                settings.MergeChance = 0.6f;
            }

            return settings;
        }
    }

    /// <summary>
    /// A grid of chambers joined by doorways: the shape of a maze room, with no geometry in it.
    ///
    /// This is deliberately plain data. The builder turns it into cubes, and the navigation
    /// work that follows reads the same graph, so both agree on what connects to what by
    /// construction rather than by probing the world twice and hoping.
    /// </summary>
    public class MazeLayout
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public float DoorWidth { get; }

        /// <summary>Where the player arrives, and the chamber furthest from it by path length.</summary>
        public Vector2Int Start { get; private set; }
        public Vector2Int Exit { get; private set; }

        public IReadOnlyList<ChamberGroup> Groups => _groups;

        private readonly bool[,] _open;
        private readonly EdgeState[,] _east;    // between (x, y) and (x + 1, y)
        private readonly EdgeState[,] _north;   // between (x, y) and (x, y + 1)
        private readonly DoorStyle[,] _eastStyle;
        private readonly DoorStyle[,] _northStyle;
        private readonly int[,] _distance;      // chambers walked from Start, -1 if unreachable
        private readonly bool[,] _split;        // an embrasure wall runs through this cell

        private readonly List<ChamberGroup> _groups = new List<ChamberGroup>();

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public MazeLayout(MazeSettings settings, Rng rng)
        {
            Width = Mathf.Max(2, settings.Width);
            Height = Mathf.Max(2, settings.Height);
            CellSize = settings.CellSize;
            DoorWidth = Mathf.Min(settings.DoorWidth, CellSize - 3f);

            _open = new bool[Width, Height];
            _east = new EdgeState[Width, Height];
            _north = new EdgeState[Width, Height];
            _eastStyle = new DoorStyle[Width, Height];
            _northStyle = new DoorStyle[Width, Height];
            _distance = new int[Width, Height];
            _split = new bool[Width, Height];

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _open[x, y] = true;

            CarveRock(settings.RockChance, rng);
            CarveSpanningTree(rng);
            AddLoops(settings.LoopChance, rng);
            MergeChambers(settings.MergeChance, rng);

            BuildGroups();
            AssignArchetypes(rng);

            // Ledges go last, and only where the maze can still be walked without them. That
            // cannot be decided before the embrasures exist: a loop edge stops being redundant
            // the moment the alternative route runs through a chamber nothing can cross.
            AssignLedges(settings.LedgeChance, rng);
            StyleDoorways(settings.NarrowChance, rng);

            Start = PickStart(rng);
            Exit = MeasureFrom(Start);
            ClearArrivalPoints();
        }

        /// <summary>
        /// The player spawns on the entrance chamber's centre and the portal sits on the exit
        /// chamber's centre, so those two chambers cannot be the archetypes that put something
        /// solid there. Only the keep and the pillar forest do; everything else already leaves
        /// the middle clear, so this costs almost no variety.
        /// </summary>
        private void ClearArrivalPoints()
        {
            Blocked(GroupAt(Start));
            Blocked(GroupAt(Exit));

            void Blocked(ChamberGroup group)
            {
                if (group == null) return;
                if (group.Archetype == ChamberArchetype.Keep
                    || group.Archetype == ChamberArchetype.PillarForest)
                    group.Archetype = ChamberArchetype.Colonnade;
            }
        }

        /// <summary>Which chamber group a cell belongs to, or null for rock.</summary>
        public ChamberGroup GroupAt(Vector2Int cell)
        {
            for (int i = 0; i < _groups.Count; i++)
                for (int c = 0; c < _groups[i].Cells.Count; c++)
                    if (_groups[i].Cells[c] == cell) return _groups[i];
            return null;
        }

        // ---------------------------------------------------------------- queries

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>True if this is a chamber. Anything outside the grid is not.</summary>
        public bool IsOpen(int x, int y) => InBounds(x, y) && _open[x, y];

        public bool IsOpen(Vector2Int cell) => IsOpen(cell.x, cell.y);

        /// <summary>The edge east of this cell. Out of range reads as Solid.</summary>
        public EdgeState EastEdge(int x, int y)
            => InBounds(x, y) && x < Width - 1 ? _east[x, y] : EdgeState.Solid;

        /// <summary>The edge north of this cell. Out of range reads as Solid.</summary>
        public EdgeState NorthEdge(int x, int y)
            => InBounds(x, y) && y < Height - 1 ? _north[x, y] : EdgeState.Solid;

        public DoorStyle EastStyle(int x, int y)
            => InBounds(x, y) ? _eastStyle[x, y] : DoorStyle.Arch;

        public DoorStyle NorthStyle(int x, int y)
            => InBounds(x, y) ? _northStyle[x, y] : DoorStyle.Arch;

        /// <summary>True if an embrasure wall divides this cell into two lanes.</summary>
        public bool IsSplit(int x, int y) => InBounds(x, y) && _split[x, y];

        /// <summary>Chambers walked from <see cref="Start"/>, or -1 for rock and unreachable cells.</summary>
        public int Distance(int x, int y) => InBounds(x, y) ? _distance[x, y] : -1;

        /// <summary>Can something walk straight from one cell to an adjacent one?</summary>
        public bool Connected(Vector2Int a, Vector2Int b)
            => EdgeBetween(a, b) != EdgeState.Solid && IsOpen(a) && IsOpen(b);

        public EdgeState EdgeBetween(Vector2Int a, Vector2Int b)
        {
            if (b.x == a.x + 1 && b.y == a.y) return EastEdge(a.x, a.y);
            if (a.x == b.x + 1 && b.y == a.y) return EastEdge(b.x, b.y);
            if (b.y == a.y + 1 && b.x == a.x) return NorthEdge(a.x, a.y);
            if (a.y == b.y + 1 && b.x == a.x) return NorthEdge(b.x, b.y);
            return EdgeState.Solid;
        }

        public DoorStyle StyleBetween(Vector2Int a, Vector2Int b)
        {
            if (b.x == a.x + 1 && b.y == a.y) return EastStyle(a.x, a.y);
            if (a.x == b.x + 1 && b.y == a.y) return EastStyle(b.x, b.y);
            if (b.y == a.y + 1 && b.x == a.x) return NorthStyle(a.x, a.y);
            if (a.y == b.y + 1 && b.x == a.x) return NorthStyle(b.x, b.y);
            return DoorStyle.Arch;
        }

        /// <summary>
        /// A way out of this chamber, so the player can be pointed into the maze instead of at
        /// whichever wall happens to face world north.
        /// </summary>
        public Vector3 ExitDirection(Vector2Int cell)
        {
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int next = cell + Directions[i];
                if (Connected(cell, next)) return (CellCentre(next) - CellCentre(cell)).normalized;
            }
            return Vector3.forward;
        }

        /// <summary>The maze is centred on the origin, matching the rectangular rooms.</summary>
        public Vector3 CellCentre(int x, int y) => new Vector3(
            (x - (Width - 1) * 0.5f) * CellSize,
            0f,
            (y - (Height - 1) * 0.5f) * CellSize);

        public Vector3 CellCentre(Vector2Int cell) => CellCentre(cell.x, cell.y);

        public float FootprintWidth => Width * CellSize;
        public float FootprintDepth => Height * CellSize;

        /// <summary>Every chamber, for callers that want to place something in each.</summary>
        public IEnumerable<Vector2Int> Chambers()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (_open[x, y]) yield return new Vector2Int(x, y);
        }

        // ---------------------------------------------------------------- generation

        private void SetEdge(Vector2Int a, Vector2Int b, EdgeState state)
        {
            if (b.x == a.x + 1 && b.y == a.y) _east[a.x, a.y] = state;
            else if (a.x == b.x + 1 && b.y == a.y) _east[b.x, b.y] = state;
            else if (b.y == a.y + 1 && b.x == a.x) _north[a.x, a.y] = state;
            else if (a.y == b.y + 1 && b.x == a.x) _north[b.x, b.y] = state;
        }

        /// <summary>
        /// Punches out whole cells so the outline is ragged rather than a perfect rectangle.
        /// Each removal is rolled back if it would strand part of the grid, which is cheaper
        /// and far more predictable than generating and rejecting whole layouts.
        /// </summary>
        private void CarveRock(float chance, Rng rng)
        {
            if (chance <= 0f) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!rng.Chance(chance)) continue;

                    _open[x, y] = false;
                    if (!AllChambersReachable()) _open[x, y] = true;
                }
            }
        }

        /// <summary>Flood fill over adjacency, before any edges exist.</summary>
        private bool AllChambersReachable()
        {
            int total = 0;
            var first = new Vector2Int(-1, -1);

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (_open[x, y])
                    {
                        total++;
                        if (first.x < 0) first = new Vector2Int(x, y);
                    }

            if (total == 0) return false;

            var seen = new bool[Width, Height];
            var queue = new Queue<Vector2Int>();
            seen[first.x, first.y] = true;
            queue.Enqueue(first);
            int found = 0;

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                found++;

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (!IsOpen(next) || seen[next.x, next.y]) continue;
                    seen[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }

            return found == total;
        }

        /// <summary>
        /// Randomised depth-first search. On its own this gives a perfect maze - exactly one
        /// route between any two chambers - which is why <see cref="AddLoops"/> runs after it.
        /// </summary>
        private void CarveSpanningTree(Rng rng)
        {
            var visited = new bool[Width, Height];
            var stack = new Stack<Vector2Int>();
            var candidates = new List<Vector2Int>(4);

            Vector2Int start = new Vector2Int(-1, -1);
            foreach (Vector2Int cell in Chambers()) { start = cell; break; }
            if (start.x < 0) return;

            visited[start.x, start.y] = true;
            stack.Push(start);

            while (stack.Count > 0)
            {
                Vector2Int cell = stack.Peek();

                candidates.Clear();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (IsOpen(next) && !visited[next.x, next.y]) candidates.Add(next);
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                Vector2Int chosen = rng.Pick(candidates);
                SetEdge(cell, chosen, EdgeState.Door);
                visited[chosen.x, chosen.y] = true;
                stack.Push(chosen);
            }
        }

        /// <summary>
        /// Reconnects some of the walls the spanning tree left standing. Loops are what let a
        /// fight keep moving: without them every retreat is a backtrack down the way you came.
        /// </summary>
        private void AddLoops(float chance, Rng rng)
        {
            if (chance <= 0f) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (x < Width - 1 && _east[x, y] == EdgeState.Solid
                        && _open[x, y] && _open[x + 1, y] && rng.Chance(chance))
                        _east[x, y] = EdgeState.Door;

                    if (y < Height - 1 && _north[x, y] == EdgeState.Solid
                        && _open[x, y] && _open[x, y + 1] && rng.Chance(chance))
                        _north[x, y] = EdgeState.Door;
                }
            }
        }

        /// <summary>
        /// Raises some doorways into jump-only shortcuts, keeping only the ones the maze can do
        /// without. Each candidate is applied and then rolled back if it leaves anything
        /// unwalkable, so the invariant holds cumulatively rather than one ledge at a time.
        ///
        /// Doorways onto an embrasure are left alone. Each of its lanes has exactly one way in,
        /// and raising that would seal the lane off from anything that cannot jump.
        /// </summary>
        private void AssignLedges(float chance, Rng rng)
        {
            if (chance <= 0f) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (x < Width - 1 && _east[x, y] == EdgeState.Door
                        && !_split[x, y] && !_split[x + 1, y] && rng.Chance(chance))
                    {
                        _eastStyle[x, y] = DoorStyle.Ledge;
                        if (!GroundConnected()) _eastStyle[x, y] = DoorStyle.Arch;
                    }

                    if (y < Height - 1 && _north[x, y] == EdgeState.Door
                        && !_split[x, y] && !_split[x, y + 1] && rng.Chance(chance))
                    {
                        _northStyle[x, y] = DoorStyle.Ledge;
                        if (!GroundConnected()) _northStyle[x, y] = DoorStyle.Arch;
                    }
                }
            }
        }

        /// <summary>
        /// Can something that cannot jump still reach every chamber? Ledges are impassable here
        /// and embrasures are dead ends, which together are exactly the constraints a walking
        /// enemy is under. If this ever returns false the maze has a chamber nothing can get to.
        /// </summary>
        private bool GroundConnected()
        {
            int total = 0;
            var first = new Vector2Int(-1, -1);

            foreach (Vector2Int cell in Chambers())
            {
                total++;
                // An embrasure cannot be crossed, so starting inside one would prove nothing.
                if (first.x < 0 && !_split[cell.x, cell.y]) first = cell;
            }

            if (total == 0 || first.x < 0) return true;

            var seen = new HashSet<Vector2Int> { first };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(first);
            int found = 0;

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                found++;

                if (_split[cell.x, cell.y]) continue;

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (!IsOpen(next) || seen.Contains(next)) continue;
                    if (!Connected(cell, next)) continue;
                    if (StyleBetween(cell, next) == DoorStyle.Ledge) continue;

                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return found == total;
        }

        /// <summary>Drops the wall entirely on some doorways, so not every chamber is the same size.</summary>
        private void MergeChambers(float chance, Rng rng)
        {
            if (chance <= 0f) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_east[x, y] == EdgeState.Door && rng.Chance(chance))
                        _east[x, y] = EdgeState.Open;

                    if (_north[x, y] == EdgeState.Door && rng.Chance(chance))
                        _north[x, y] = EdgeState.Open;
                }
            }
        }

        /// <summary>Narrows some of the remaining arches into chokepoints.</summary>
        private void StyleDoorways(float narrowChance, Rng rng)
        {
            if (narrowChance <= 0f) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_east[x, y] == EdgeState.Door && _eastStyle[x, y] == DoorStyle.Arch
                        && rng.Chance(narrowChance)) _eastStyle[x, y] = DoorStyle.Narrow;

                    if (_north[x, y] == EdgeState.Door && _northStyle[x, y] == DoorStyle.Arch
                        && rng.Chance(narrowChance)) _northStyle[x, y] = DoorStyle.Narrow;
                }
            }
        }

        // ---------------------------------------------------------------- chambers

        /// <summary>Connected components over the walls that were removed entirely.</summary>
        private void BuildGroups()
        {
            var claimed = new bool[Width, Height];

            foreach (Vector2Int seed in Chambers())
            {
                if (claimed[seed.x, seed.y]) continue;

                var group = new ChamberGroup();
                var queue = new Queue<Vector2Int>();
                claimed[seed.x, seed.y] = true;
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    group.Cells.Add(cell);

                    for (int i = 0; i < Directions.Length; i++)
                    {
                        Vector2Int next = cell + Directions[i];
                        if (!IsOpen(next) || claimed[next.x, next.y]) continue;
                        if (EdgeBetween(cell, next) != EdgeState.Open) continue;

                        claimed[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }

                Measure(group);
                _groups.Add(group);
            }
        }

        private void Measure(ChamberGroup group)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int i = 0; i < group.Cells.Count; i++)
            {
                Vector2Int cell = group.Cells[i];
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            group.Centre = (CellCentre(minX, minY) + CellCentre(maxX, maxY)) * 0.5f;
            group.Size = new Vector2((maxX - minX + 1) * CellSize, (maxY - minY + 1) * CellSize);
            group.IsRectangular = group.Cells.Count == (maxX - minX + 1) * (maxY - minY + 1);
        }

        /// <summary>
        /// Rolls what fills each chamber. Embrasures are checked first because they are rare
        /// and strict; everything else is a weighted roll with Bare common enough that the maze
        /// still has room to breathe.
        /// </summary>
        private void AssignArchetypes(Rng rng)
        {
            var split = new List<Vector2Int>();

            for (int i = 0; i < _groups.Count; i++)
            {
                ChamberGroup group = _groups[i];

                if (group.Cells.Count == 1
                    && TryEmbrasure(group.Cells[0], split, out bool alongX))
                {
                    group.Archetype = ChamberArchetype.Embrasure;
                    group.EmbrasureAlongX = alongX;
                    split.Add(group.Cells[0]);
                    _split[group.Cells[0].x, group.Cells[0].y] = true;
                    continue;
                }

                group.Archetype = RollArchetype(group, rng);
            }
        }

        private static ChamberArchetype RollArchetype(ChamberGroup group, Rng rng)
        {
            float roll = rng.Value;
            bool single = group.Cells.Count == 1;

            if (roll < 0.18f) return ChamberArchetype.Bare;
            if (roll < 0.34f) return ChamberArchetype.Colonnade;
            if (roll < 0.48f) return ChamberArchetype.PillarForest;
            if (roll < 0.60f) return ChamberArchetype.Rubble;
            if (roll < 0.74f) return ChamberArchetype.Alcoves;

            // A ring only reads as a ring in one cell, and a central block needs a bounding box
            // that is actually all floor, so an L-shaped merge falls back to something safe.
            if (roll < 0.87f && single) return ChamberArchetype.Rotunda;
            if (group.IsRectangular) return ChamberArchetype.Keep;
            return ChamberArchetype.Colonnade;
        }

        /// <summary>
        /// An embrasure needs its dividing wall to miss every doorway, and a doorway on each
        /// side so neither lane is a sealed pocket. That means doors on exactly one opposite
        /// pair of edges: the wall then runs between them, and each lane keeps its own way out.
        ///
        /// The cell also has to be one the maze can do without, because the wall stops anything
        /// crossing from lane to lane - so if the rest of the maze needs to route through here,
        /// putting a wall in would strand something.
        /// </summary>
        private bool TryEmbrasure(Vector2Int cell, List<Vector2Int> alreadySplit, out bool alongX)
        {
            // Never next to another embrasure. Two of them side by side share the doorway
            // between them, so that doorway is the only way out of a lane on each side - and
            // those two lanes then lead nowhere but into each other. The maze as a whole stays
            // connected, which is why checking only the maze misses it entirely.
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int neighbour = cell + Directions[i];
                for (int s = 0; s < alreadySplit.Count; s++)
                {
                    if (alreadySplit[s] != neighbour) continue;
                    alongX = true;
                    return false;
                }
            }

            bool east = EastEdge(cell.x, cell.y) != EdgeState.Solid;
            bool west = EastEdge(cell.x - 1, cell.y) != EdgeState.Solid;
            bool north = NorthEdge(cell.x, cell.y) != EdgeState.Solid;
            bool south = NorthEdge(cell.x, cell.y - 1) != EdgeState.Solid;

            // Doors north and south: an east-west wall sits between them and blocks neither.
            if (north && south && !east && !west)
            {
                alongX = true;
                return RestConnectedWithout(cell, alreadySplit);
            }

            if (east && west && !north && !south)
            {
                alongX = false;
                return RestConnectedWithout(cell, alreadySplit);
            }

            alongX = true;
            return false;
        }

        /// <summary>
        /// Is every other chamber still reachable from every other, with these cells taken out?
        /// Checked against the embrasures already placed as well as the candidate, because two
        /// that are each individually harmless can cut the maze in half between them.
        /// </summary>
        private bool RestConnectedWithout(Vector2Int candidate, List<Vector2Int> alreadySplit)
        {
            bool Excluded(Vector2Int cell)
            {
                if (cell == candidate) return true;
                for (int i = 0; i < alreadySplit.Count; i++)
                    if (alreadySplit[i] == cell) return true;
                return false;
            }

            int total = 0;
            var first = new Vector2Int(-1, -1);

            foreach (Vector2Int cell in Chambers())
            {
                if (Excluded(cell)) continue;
                total++;
                if (first.x < 0) first = cell;
            }

            if (total == 0) return false;

            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            seen.Add(first);
            queue.Enqueue(first);
            int found = 0;

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                found++;

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (!IsOpen(next) || Excluded(next) || seen.Contains(next)) continue;
                    if (!Connected(cell, next)) continue;

                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return found == total;
        }

        // ---------------------------------------------------------------- routing

        /// <summary>An edge chamber, so a run starts by walking in rather than in the middle of it.</summary>
        private Vector2Int PickStart(Rng rng)
        {
            var border = new List<Vector2Int>();
            var any = new List<Vector2Int>();

            foreach (Vector2Int cell in Chambers())
            {
                // Arriving inside an embrasure would drop the player in one lane of a room
                // built to be looked into from the other.
                if (_split[cell.x, cell.y]) continue;

                any.Add(cell);
                if (cell.x == 0 || cell.y == 0 || cell.x == Width - 1 || cell.y == Height - 1)
                    border.Add(cell);
            }

            if (border.Count > 0) return rng.Pick(border);
            return any.Count > 0 ? rng.Pick(any) : Vector2Int.zero;
        }

        /// <summary>
        /// Breadth-first over the carved edges, filling in <see cref="_distance"/>. Returns the
        /// furthest chamber, which is where the exit goes so that reaching it means walking the
        /// maze rather than crossing a room.
        ///
        /// An embrasure can be entered but not crossed, so it is a leaf here: reachable, never
        /// routed through, and never chosen as the exit. That keeps every distance honest even
        /// though a cell-per-node graph cannot express its two lanes separately.
        /// </summary>
        private Vector2Int MeasureFrom(Vector2Int origin)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _distance[x, y] = -1;

            if (!IsOpen(origin)) return origin;

            var queue = new Queue<Vector2Int>();
            _distance[origin.x, origin.y] = 0;
            queue.Enqueue(origin);

            Vector2Int furthest = origin;

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();

                if (!_split[cell.x, cell.y]
                    && _distance[cell.x, cell.y] > _distance[furthest.x, furthest.y])
                    furthest = cell;

                if (_split[cell.x, cell.y]) continue;

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (!IsOpen(next) || _distance[next.x, next.y] >= 0) continue;
                    if (!Connected(cell, next)) continue;

                    _distance[next.x, next.y] = _distance[cell.x, cell.y] + 1;
                    queue.Enqueue(next);
                }
            }

            return furthest;
        }
    }
}
