#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// A maze only exists while playing, so there is nothing to open and look at. These print
    /// one as text and hammer the generator for the failures that would be invisible in the
    /// editor and fatal in a run - chiefly an exit you cannot walk to.
    /// </summary>
    public static class MazeTools
    {
        private const int VerifySamples = 400;

        private static readonly Vector2Int None = new Vector2Int(-1, -1);

        private static readonly Vector2Int[] Steps4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        [MenuItem("Gunspire/Log Maze Sample")]
        public static void LogSample()
        {
            var rng = new Rng(12345);
            var maze = new MazeLayout(MazeSettings.For(RoomKind.Combat), rng);
            Debug.Log(Describe(maze));
        }

        /// <summary>
        /// Every invariant here is one the player would discover the hard way: a stranded exit
        /// is a soft-lock, and an embrasure in the wrong cell walls off half the maze. Sampling
        /// many seeds matters because each is a different graph.
        /// </summary>
        [MenuItem("Gunspire/Verify Maze Generator")]
        public static void Verify()
        {
            var problems = new List<string>();
            var archetypes = new Dictionary<ChamberArchetype, int>();
            var styles = new Dictionary<DoorStyle, int>();

            int totalChambers = 0, totalRock = 0, totalGroups = 0;
            int seedsWithEmbrasure = 0, groundBlocked = 0;
            int shortestPath = int.MaxValue, longestPath = 0;

            for (int seed = 0; seed < VerifySamples; seed++)
            {
                var maze = new MazeLayout(MazeSettings.For(RoomKind.Combat), new Rng(seed));

                int chambers = 0;
                foreach (Vector2Int cell in maze.Chambers()) chambers++;

                totalChambers += chambers;
                totalRock += maze.Width * maze.Height - chambers;
                totalGroups += maze.Groups.Count;

                bool hasEmbrasure = false;
                for (int i = 0; i < maze.Groups.Count; i++)
                {
                    ChamberGroup group = maze.Groups[i];
                    Count(archetypes, group.Archetype, group.Cells.Count);
                    if (group.Archetype == ChamberArchetype.Embrasure) hasEmbrasure = true;
                }
                if (hasEmbrasure) seedsWithEmbrasure++;

                for (int x = 0; x < maze.Width; x++)
                {
                    for (int y = 0; y < maze.Height; y++)
                    {
                        if (maze.EastEdge(x, y) == EdgeState.Door) Count(styles, maze.EastStyle(x, y), 1);
                        if (maze.NorthEdge(x, y) == EdgeState.Door) Count(styles, maze.NorthStyle(x, y), 1);
                    }
                }

                if (chambers < 4) problems.Add("seed " + seed + ": only " + chambers + " chambers");
                if (!maze.IsOpen(maze.Start)) problems.Add("seed " + seed + ": start is not a chamber");
                if (!maze.IsOpen(maze.Exit)) problems.Add("seed " + seed + ": exit is not a chamber");

                // Arriving or leaving inside an embrasure would put the player in one lane of a
                // room whose whole point is that you cannot get to the other one.
                if (maze.IsSplit(maze.Start.x, maze.Start.y))
                    problems.Add("seed " + seed + ": start is inside an embrasure");
                if (maze.IsSplit(maze.Exit.x, maze.Exit.y))
                    problems.Add("seed " + seed + ": exit is inside an embrasure");

                // The player materialises on one chamber centre and the portal on the other,
                // so neither may be an archetype that builds something solid there.
                CheckClearCentre(problems, maze, seed, maze.Start, "start");
                CheckClearCentre(problems, maze, seed, maze.Exit, "exit");

                int exitDistance = maze.Distance(maze.Exit.x, maze.Exit.y);
                if (exitDistance < 0) problems.Add("seed " + seed + ": exit unreachable from start");
                else if (exitDistance == 0 && chambers > 1)
                    problems.Add("seed " + seed + ": exit is the start chamber");

                // Each embrasure lane needs a doorway onto an ordinary chamber. A doorway onto
                // another embrasure is shared with one of its lanes, and the two then form a
                // pocket that nothing else connects to - while the maze stays connected, so
                // the reachability check below sees nothing wrong.
                for (int x = 0; x < maze.Width; x++)
                {
                    for (int y = 0; y < maze.Height; y++)
                    {
                        if (!maze.IsSplit(x, y)) continue;

                        var cell = new Vector2Int(x, y);
                        foreach (Vector2Int step in Steps4)
                        {
                            Vector2Int neighbour = cell + step;
                            if (!maze.Connected(cell, neighbour)) continue;
                            if (!maze.IsSplit(neighbour.x, neighbour.y)) continue;

                            problems.Add("seed " + seed + ": embrasures at " + cell + " and "
                                         + neighbour + " share a doorway");
                        }
                    }
                }

                // Every chamber reachable on foot, embrasures counted as enterable dead ends.
                int reachable = Reachable(maze, maze.Start, allowLedges: true, None, None);
                if (reachable != chambers)
                    problems.Add("seed " + seed + ": " + (chambers - reachable) + " chambers cut off");

                // A ledge is only ever safe because it is a loop edge. Prove it rather than
                // trusting that AddLoops is the only thing that ever sets one.
                foreach (KeyValuePair<Vector2Int, Vector2Int> ledge in Ledges(maze))
                {
                    if (Reachable(maze, maze.Start, true, ledge.Key, ledge.Value) != chambers)
                        problems.Add("seed " + seed + ": ledge at " + ledge.Key + " is the only way through");
                }

                // Not a failure, but step two needs to know: ground enemies cannot use ledges.
                groundBlocked += chambers - Reachable(maze, maze.Start, allowLedges: false, None, None);

                if (exitDistance >= 0)
                {
                    shortestPath = Mathf.Min(shortestPath, exitDistance);
                    longestPath = Mathf.Max(longestPath, exitDistance);
                }
            }

            float samples = VerifySamples;
            var report = new StringBuilder();
            MazeSettings settings = MazeSettings.For(RoomKind.Combat);

            report.AppendLine("Maze generator: " + VerifySamples + " seeds of "
                              + settings.Width + "x" + settings.Height + " at " + settings.CellSize + "m");
            report.AppendLine("  chambers " + (totalChambers / samples).ToString("0.0")
                              + " in " + (totalGroups / samples).ToString("0.0") + " groups"
                              + ", rock " + (totalRock / samples).ToString("0.0"));
            report.AppendLine("  walk to exit " + shortestPath + " to " + longestPath + " chambers");
            report.AppendLine("  embrasure appears in " + (100f * seedsWithEmbrasure / samples).ToString("0")
                              + "% of mazes");
            report.AppendLine("  chambers a ground enemy cannot reach: "
                              + (groundBlocked / samples).ToString("0.00") + " per maze");

            report.Append("  archetypes  ");
            foreach (KeyValuePair<ChamberArchetype, int> pair in archetypes)
                report.Append(pair.Key + " " + (pair.Value / samples).ToString("0.0") + "   ");
            report.AppendLine();

            report.Append("  doorways    ");
            foreach (KeyValuePair<DoorStyle, int> pair in styles)
                report.Append(pair.Key + " " + (pair.Value / samples).ToString("0.0") + "   ");
            report.AppendLine();

            if (problems.Count == 0)
            {
                report.Append("  no problems.");
                Debug.Log(report.ToString());
                return;
            }

            report.AppendLine("  " + problems.Count + " PROBLEMS:");
            for (int i = 0; i < problems.Count && i < 25; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        /// <summary>
        /// Builds real geometry and probes it, which is the only way to find out whether the
        /// navigation grid agrees with the maze graph. Everything else here reasons about the
        /// graph alone, and the graph is not what enemies walk on.
        ///
        /// The embrasure test is the one worth having. Its wall is solid below waist height and
        /// open above, so a probe sampled at chest height would read the firing slot as a way
        /// through and quietly connect the two lanes - and nothing except walking a melee enemy
        /// into that wall would ever show it.
        /// </summary>
        [MenuItem("Gunspire/Verify Maze Navigation")]
        public static void VerifyNavigation()
        {
            const int seeds = 25;

            var problems = new List<string>();
            int chambersChecked = 0, embrasuresChecked = 0, ledgesChecked = 0;
            double buildMillis = 0;

            for (int seed = 0; seed < seeds; seed++)
            {
                var maze = new MazeLayout(MazeSettings.For(RoomKind.Combat), new Rng(seed));
                var node = new RoomNode { Kind = RoomKind.Combat, Floor = 1, Seed = seed };

                var root = new GameObject("NavProbe");
                NavField field = null;

                try
                {
                    MazeBuilder.BuildShell(root.transform, maze, node, new Rng(seed));

                    field = root.AddComponent<NavField>();

                    // The probe pass runs at every room load, so its cost is worth knowing.
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    field.Build(maze.FootprintWidth, maze.FootprintDepth);
                    buildMillis += clock.Elapsed.TotalMilliseconds;
                    field.Rebuild(field.WorldToCell(maze.CellCentre(maze.Start)));

                    foreach (Vector2Int cell in maze.Chambers())
                    {
                        if (maze.IsSplit(cell.x, cell.y))
                        {
                            embrasuresChecked++;
                            CheckEmbrasure(problems, maze, field, seed, cell);
                            continue;
                        }

                        chambersChecked++;

                        Vector3 centre = maze.CellCentre(cell);
                        if (!field.TryNearestWalkable(field.WorldToCell(centre), out Vector2Int stand))
                        {
                            problems.Add("seed " + seed + ": chamber " + cell + " has no standable ground");
                            continue;
                        }

                        if (field.StepsAt(field.CellCentre(stand.x, stand.y)) < 0)
                            problems.Add("seed " + seed + ": chamber " + cell + " is unreachable on the grid");
                    }

                    foreach (KeyValuePair<Vector2Int, Vector2Int> ledge in Ledges(maze))
                    {
                        ledgesChecked++;
                        CheckLedge(problems, maze, field, seed, ledge.Key, ledge.Value);
                    }
                }
                finally
                {
                    if (field != null) Object.DestroyImmediate(field);
                    Object.DestroyImmediate(root);
                }
            }

            string summary = "Maze navigation: " + seeds + " seeds, " + chambersChecked
                             + " chambers, " + embrasuresChecked + " embrasures and "
                             + ledgesChecked + " ledges probed"
                             + "\n  grid build averages "
                             + (buildMillis / seeds).ToString("0.0") + "ms per room";

            if (problems.Count == 0)
            {
                Debug.Log(summary + "\n  no problems.");
                return;
            }

            var report = new StringBuilder(summary);
            report.AppendLine("\n  " + problems.Count + " PROBLEMS:");
            for (int i = 0; i < problems.Count && i < 25; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        /// <summary>
        /// Both lanes have to be reachable, and there must be no way across between them. A
        /// point three metres either side of the dividing wall is well clear of the wall itself,
        /// so anything connecting them is the slot being read as walkable.
        /// </summary>
        private static void CheckEmbrasure(List<string> problems, MazeLayout maze, NavField field,
            int seed, Vector2Int cell)
        {
            ChamberGroup group = maze.GroupAt(cell);
            if (group == null) return;

            Vector3 centre = maze.CellCentre(cell);
            Vector3 across = group.EmbrasureAlongX ? Vector3.forward : Vector3.right;

            Vector3 near = centre - across * 3f;
            Vector3 far = centre + across * 3f;

            if (field.StepsAt(near) < 0 || field.StepsAt(far) < 0)
            {
                problems.Add("seed " + seed + ": embrasure " + cell + " has a lane with no way in");
                return;
            }

            if (field.IsClearLine(near, far))
                problems.Add("seed " + seed + ": embrasure " + cell
                             + " lets you walk through the firing slot");
        }

        /// <summary>
        /// A ledge must be impassable on the grid. This is the check that pins the probe height
        /// down: a ledge is a 1.3m sill under a 3.7m opening, so a probe sampled any higher
        /// sails straight over the sill and the grid happily routes walking enemies through a
        /// gap they can only stare at.
        /// </summary>
        private static void CheckLedge(List<string> problems, MazeLayout maze, NavField field,
            int seed, Vector2Int a, Vector2Int b)
        {
            Vector3 from = maze.CellCentre(a);
            Vector3 to = maze.CellCentre(b);
            Vector3 boundary = (from + to) * 0.5f;
            Vector3 tangent = Vector3.Cross(Vector3.up, (to - from).normalized);

            // Sampled along the opening itself rather than out in the chambers either side.
            // Anything further in can be blocked by a column that happens to stand there, which
            // says nothing about the ledge; the sill is the only thing being asserted here.
            for (float offset = -2f; offset <= 2f; offset += 0.5f)
            {
                Vector2Int cell = field.WorldToCell(boundary + tangent * offset);
                if (!field.IsWalkable(cell.x, cell.y)) continue;

                problems.Add("seed " + seed + ": ledge " + a + "-" + b + " is walkable on the grid");
                return;
            }
        }

        private static void CheckClearCentre(List<string> problems, MazeLayout maze, int seed,
            Vector2Int cell, string what)
        {
            ChamberGroup group = maze.GroupAt(cell);
            if (group == null)
            {
                problems.Add("seed " + seed + ": " + what + " has no chamber group");
                return;
            }

            if (group.Archetype == ChamberArchetype.Keep
                || group.Archetype == ChamberArchetype.PillarForest)
                problems.Add("seed " + seed + ": " + what + " chamber is " + group.Archetype
                             + ", which fills the middle");
        }

        private static void Count<T>(Dictionary<T, int> tally, T key, int amount)
        {
            tally.TryGetValue(key, out int current);
            tally[key] = current + amount;
        }

        private static IEnumerable<KeyValuePair<Vector2Int, Vector2Int>> Ledges(MazeLayout maze)
        {
            for (int x = 0; x < maze.Width; x++)
            {
                for (int y = 0; y < maze.Height; y++)
                {
                    if (maze.EastEdge(x, y) == EdgeState.Door && maze.EastStyle(x, y) == DoorStyle.Ledge)
                        yield return new KeyValuePair<Vector2Int, Vector2Int>(
                            new Vector2Int(x, y), new Vector2Int(x + 1, y));

                    if (maze.NorthEdge(x, y) == EdgeState.Door && maze.NorthStyle(x, y) == DoorStyle.Ledge)
                        yield return new KeyValuePair<Vector2Int, Vector2Int>(
                            new Vector2Int(x, y), new Vector2Int(x, y + 1));
                }
            }
        }

        /// <summary>
        /// Chambers reachable on foot from a starting cell. Embrasures are entered but never
        /// crossed. Optionally ignores ledges (which no ground enemy can climb) and one named
        /// edge, which is how a ledge gets proved redundant.
        /// </summary>
        private static int Reachable(MazeLayout maze, Vector2Int from, bool allowLedges,
            Vector2Int skipA, Vector2Int skipB)
        {
            var directions = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            int found = 0;

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                found++;

                if (maze.IsSplit(cell.x, cell.y)) continue;

                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int next = cell + directions[i];
                    if (!maze.IsOpen(next) || seen.Contains(next)) continue;
                    if (!maze.Connected(cell, next)) continue;
                    if (!allowLedges && maze.StyleBetween(cell, next) == DoorStyle.Ledge) continue;

                    bool skipped = (cell == skipA && next == skipB) || (cell == skipB && next == skipA);
                    if (skipped) continue;

                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return found;
        }

        /// <summary>
        /// Draws the graph as text. Two characters per cell horizontally so a north-south
        /// doorway has somewhere to print.
        /// </summary>
        private static string Describe(MazeLayout maze)
        {
            var archetypeOf = new Dictionary<Vector2Int, ChamberGroup>();
            for (int i = 0; i < maze.Groups.Count; i++)
                for (int c = 0; c < maze.Groups[i].Cells.Count; c++)
                    archetypeOf[maze.Groups[i].Cells[c]] = maze.Groups[i];

            var sb = new StringBuilder();
            sb.AppendLine("Maze " + maze.Width + "x" + maze.Height + " at " + maze.CellSize
                          + "m  (" + maze.FootprintWidth + "m x " + maze.FootprintDepth + "m)");
            sb.AppendLine("cells  S start, X exit, ## rock, letter is the archetype");
            sb.AppendLine("       . bare  C colonnade  F pillars  K keep  O rotunda  R rubble  A alcoves  E embrasure");
            sb.AppendLine("doors  blank arch, n narrow, L ledge, solid wall drawn as | or --");

            for (int y = maze.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < maze.Width; x++)
                {
                    sb.Append(CellGlyph(maze, archetypeOf, x, y));
                    if (x < maze.Width - 1)
                        sb.Append(EdgeGlyph(maze.EastEdge(x, y), maze.EastStyle(x, y), "|", "n", "L"));
                }
                sb.AppendLine();

                if (y == 0) continue;

                for (int x = 0; x < maze.Width; x++)
                {
                    // The north edge of the row below is the south edge of this one.
                    sb.Append(EdgeGlyph(maze.NorthEdge(x, y - 1), maze.NorthStyle(x, y - 1), "--", "nn", "LL"));
                    if (x < maze.Width - 1) sb.Append(' ');
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string CellGlyph(MazeLayout maze, Dictionary<Vector2Int, ChamberGroup> groups,
            int x, int y)
        {
            if (!maze.IsOpen(x, y)) return "##";

            var cell = new Vector2Int(x, y);
            char marker = cell == maze.Start ? 'S' : cell == maze.Exit ? 'X' : ' ';

            if (!groups.TryGetValue(cell, out ChamberGroup group)) return marker + "?";

            return marker.ToString() + Letter(group.Archetype);
        }

        private static char Letter(ChamberArchetype archetype)
        {
            switch (archetype)
            {
                case ChamberArchetype.Colonnade: return 'C';
                case ChamberArchetype.PillarForest: return 'F';
                case ChamberArchetype.Keep: return 'K';
                case ChamberArchetype.Rotunda: return 'O';
                case ChamberArchetype.Rubble: return 'R';
                case ChamberArchetype.Alcoves: return 'A';
                case ChamberArchetype.Embrasure: return 'E';
                default: return '.';
            }
        }

        private static string EdgeGlyph(EdgeState state, DoorStyle style,
            string solid, string narrow, string ledge)
        {
            if (state == EdgeState.Solid) return solid;
            if (state == EdgeState.Open) return solid.Length == 1 ? " " : "  ";

            switch (style)
            {
                case DoorStyle.Narrow: return narrow;
                case DoorStyle.Ledge: return ledge;
                default: return solid.Length == 1 ? " " : "  ";
            }
        }
    }
}
#endif
