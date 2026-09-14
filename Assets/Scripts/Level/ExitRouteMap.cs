using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// How far every chamber is from a maze floor's exit on foot, and which way the route leads from
    /// anywhere. Path of Light lights the route and doubles your speed while you follow it.
    ///
    /// Built once per floor as a breadth-first search outward from the exit, over the same ground rules
    /// the maze's own connectivity check uses: a ledge cannot be climbed on foot, so it is never part of
    /// the route, and an embrasure cannot be crossed, so the route can end in one but never passes
    /// through. Anything else would lead the player straight at a wall.
    /// </summary>
    public class ExitRouteMap
    {
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public MazeLayout Maze { get; }

        private readonly int[,] _steps;

        public ExitRouteMap(MazeLayout maze)
        {
            Maze = maze;
            _steps = new int[maze.Width, maze.Height];

            for (int x = 0; x < maze.Width; x++)
                for (int y = 0; y < maze.Height; y++)
                    _steps[x, y] = -1;

            Vector2Int exit = maze.Exit;
            if (!maze.IsOpen(exit)) return;

            var queue = new Queue<Vector2Int>();
            _steps[exit.x, exit.y] = 0;
            queue.Enqueue(exit);

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (cell != exit && maze.IsSplit(cell.x, cell.y)) continue;

                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (!maze.IsOpen(next) || _steps[next.x, next.y] >= 0 || !Passable(cell, next)) continue;

                    _steps[next.x, next.y] = _steps[cell.x, cell.y] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        /// <summary>Walkable on foot between two neighbouring chambers: connected, and not a jump-only ledge.</summary>
        public bool Passable(Vector2Int a, Vector2Int b) => Maze.Connected(a, b) && Maze.StyleBetween(a, b) != DoorStyle.Ledge;

        /// <summary>Chambers to walk to the exit, or -1 for rock and anywhere the exit cannot be walked to from.</summary>
        public int StepsAt(Vector2Int cell) => Maze.InBounds(cell.x, cell.y) ? _steps[cell.x, cell.y] : -1;

        public int StepsAt(Vector3 world) => StepsAt(WorldToCell(world));

        public Vector2Int WorldToCell(Vector3 world) => new Vector2Int(
            Mathf.RoundToInt(world.x / Maze.CellSize + (Maze.Width - 1) * 0.5f),
            Mathf.RoundToInt(world.z / Maze.CellSize + (Maze.Height - 1) * 0.5f));

        /// <summary>The next chamber along the route, never one inside an embrasure unless it is the exit.</summary>
        public bool TryNextCell(Vector2Int cell, out Vector2Int next)
        {
            next = cell;
            int steps = StepsAt(cell);
            if (steps <= 0) return false;

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int candidate = cell + Directions[i];
                if (StepsAt(candidate) != steps - 1 || !Passable(cell, candidate)) continue;
                if (candidate != Maze.Exit && Maze.IsSplit(candidate.x, candidate.y)) continue;

                next = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Which way the route leads from a point: toward the doorway into the next chamber, or straight
        /// at the exit's centre once inside its chamber. Zero where there is no route.
        /// </summary>
        public Vector3 DirectionAt(Vector3 world)
        {
            Vector2Int cell = WorldToCell(world);
            int steps = StepsAt(cell);
            if (steps < 0) return Vector3.zero;

            Vector3 aim;
            if (steps == 0 || !TryNextCell(cell, out Vector2Int next))
            {
                aim = Maze.CellCentre(Maze.Exit);
            }
            else
            {
                // Doorways are centred on the wall two chambers share, so the midpoint of their centres
                // is the gap. Standing in it already, aim on into the next chamber.
                aim = (Maze.CellCentre(cell) + Maze.CellCentre(next)) * 0.5f;
                if (Flat(aim - world).sqrMagnitude < 1f) aim = Maze.CellCentre(next);
            }

            Vector3 delta = Flat(aim - world);
            return delta.sqrMagnitude > 0.01f ? delta.normalized : Vector3.zero;
        }

        /// <summary>Whether moving like this follows the route: fast enough to count, and heading its way.</summary>
        public bool IsFollowing(Vector3 position, Vector3 velocity, float minSpeed = 0.5f)
        {
            Vector3 flat = Flat(velocity);
            if (flat.magnitude < minSpeed) return false;

            Vector3 route = DirectionAt(position);
            return route.sqrMagnitude > 0f && Vector3.Dot(flat.normalized, route) > 0.5f;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
