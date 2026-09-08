using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Turns a <see cref="MazeLayout"/> into geometry: a floor slab per chamber, a solid block
    /// per rock cell, a wall on every edge that needs one, and whatever the chamber archetype
    /// puts inside. Same primitives as the rectangular rooms, so there is still nothing to
    /// author in the editor.
    /// </summary>
    public static class MazeBuilder
    {
        /// <summary>
        /// How tall every wall, column and rock block is, and so how high the roof sits. This
        /// is the one number that sets how big the level feels - raise it and the whole space
        /// grows, since the roof rides on top and the architecture grows with it.
        /// </summary>
        public const float WallHeight = 11f;

        public const float WallThickness = 1.5f;

        /// <summary>
        /// The roof over the entire maze, walls and rock included. Sits directly on top of the
        /// walls, so raising <see cref="WallHeight"/> raises the whole space with it - that one
        /// number is the knob for how big the level feels.
        /// </summary>
        public const float RoofHeight = WallHeight;

        private const float NarrowWidth = 3.2f;
        private const float LedgeWidth = 5f;

        /// <summary>Base jump clears 1.55m, and a ground enemy steps 0.4m. Anything between works.</summary>
        public const float LedgeSill = 1.3f;

        /// <summary>Tall enough that a Gazer can cross a ledge without having to descend.</summary>
        private const float LedgeTop = 5f;

        public static void BuildShell(Transform parent, MazeLayout maze, RoomNode node, Rng rng)
        {
            Material floorMaterial = MaterialLibrary.Lit(Palette.Floor, 0.05f);
            Material wallMaterial = MaterialLibrary.Lit(Palette.Wall, 0.05f);
            Material rockMaterial = MaterialLibrary.Lit(Palette.Wall * 1.25f, 0.05f);
            Material trimMaterial = MaterialLibrary.Emissive(node.Accent * 0.6f, 1.2f);
            Material propMaterial = MaterialLibrary.Lit(Palette.Prop, 0.1f);

            BuildCells(parent, maze, floorMaterial, rockMaterial);
            BuildWalls(parent, maze, wallMaterial, trimMaterial);
            BuildRoof(parent, maze, rockMaterial);
            BuildLights(parent, maze, node);

            for (int i = 0; i < maze.Groups.Count; i++)
                ChamberBuilder.Furnish(parent, maze, maze.Groups[i], node, rng, propMaterial, trimMaterial);
        }

        // ---------------------------------------------------------------- cells

        /// <summary>
        /// One floor cube per chamber rather than a single slab: it keeps the rock cells as
        /// genuine holes in the floor plan, and it means the built-in pipeline picks each
        /// chamber's own nearest lights instead of four for the whole maze.
        /// </summary>
        private static void BuildCells(Transform parent, MazeLayout maze, Material floor, Material rock)
        {
            for (int x = 0; x < maze.Width; x++)
            {
                for (int y = 0; y < maze.Height; y++)
                {
                    Vector3 centre = maze.CellCentre(x, y);

                    if (maze.IsOpen(x, y))
                    {
                        Build.Cube(parent, "Floor_" + x + "_" + y, centre + Vector3.down * 0.5f,
                            new Vector3(maze.CellSize, 1f, maze.CellSize), floor,
                            collider: true, layer: Layers.Level);
                    }
                    else
                    {
                        // Rock fills its whole cell, which is why the wall pass skips any edge
                        // touching one - the block is already the wall on all four sides.
                        Build.Cube(parent, "Rock_" + x + "_" + y, centre + Vector3.up * (WallHeight * 0.5f),
                            new Vector3(maze.CellSize, WallHeight, maze.CellSize), rock,
                            collider: true, layer: Layers.Level);
                    }
                }
            }
        }

        /// <summary>
        /// One slab over the whole level, rock included. A single piece rather than a panel per
        /// cell because overlapping panels share a coplanar underside, and two coplanar faces
        /// z-fight - which reads as flickering seams across the ceiling.
        /// </summary>
        private static void BuildRoof(Transform parent, MazeLayout maze, Material material)
        {
            Build.Cube(parent, "Roof", Vector3.up * (RoofHeight + 0.5f),
                new Vector3(maze.FootprintWidth + WallThickness * 2f, 1f,
                            maze.FootprintDepth + WallThickness * 2f),
                material, collider: true, layer: Layers.Level);
        }

        // ---------------------------------------------------------------- walls

        private static void BuildWalls(Transform parent, MazeLayout maze, Material wall, Material trim)
        {
            float half = maze.CellSize * 0.5f;

            // Starting at -1 folds the outer boundary into the same pass: the cell "before"
            // column zero is outside the grid, which reads as a wall like any other.
            for (int y = 0; y < maze.Height; y++)
            {
                for (int x = -1; x < maze.Width; x++)
                {
                    if (!TryEdgeState(maze, x, y, x + 1, y, out EdgeState state)) continue;

                    Vector3 line = maze.CellCentre(x, y) + new Vector3(half, 0f, 0f);
                    BuildEdge(parent, line, alongZ: true, state, maze.EastStyle(x, y), maze, wall, trim);
                }
            }

            for (int x = 0; x < maze.Width; x++)
            {
                for (int y = -1; y < maze.Height; y++)
                {
                    if (!TryEdgeState(maze, x, y, x, y + 1, out EdgeState state)) continue;

                    Vector3 line = maze.CellCentre(x, y) + new Vector3(0f, 0f, half);
                    BuildEdge(parent, line, alongZ: false, state, maze.NorthStyle(x, y), maze, wall, trim);
                }
            }
        }

        /// <summary>
        /// Works out what belongs on the boundary between two cells, and whether anything does.
        /// Nothing is needed where neither side is walkable, or where the far side is rock.
        /// </summary>
        private static bool TryEdgeState(MazeLayout maze, int ax, int ay, int bx, int by, out EdgeState state)
        {
            bool aOpen = maze.IsOpen(ax, ay);
            bool bOpen = maze.IsOpen(bx, by);

            if (!aOpen && !bOpen)
            {
                state = EdgeState.Solid;
                return false;
            }

            if (aOpen && bOpen)
            {
                state = ax == bx ? maze.NorthEdge(ax, ay) : maze.EastEdge(ax, ay);
                return state != EdgeState.Open;
            }

            // One chamber, one something else. Rock already fills its cell; outside needs a wall.
            bool otherIsRock = aOpen ? maze.InBounds(bx, by) : maze.InBounds(ax, ay);
            state = EdgeState.Solid;
            return !otherIsRock;
        }

        /// <summary>
        /// Builds the wall on one cell boundary. A solid edge overhangs by the wall thickness so
        /// corners meet with no seam to shoot through; a doorway keeps its full width by
        /// overhanging outwards only.
        /// </summary>
        private static void BuildEdge(Transform parent, Vector3 line, bool alongZ, EdgeState state,
            DoorStyle style, MazeLayout maze, Material wall, Material trim)
        {
            if (state == EdgeState.Open) return;

            float cell = maze.CellSize;
            Vector3 run = alongZ ? Vector3.forward : Vector3.right;
            Vector3 up = Vector3.up * (WallHeight * 0.5f);

            if (state == EdgeState.Solid)
            {
                Build.Cube(parent, "Wall", line + up, Extent(alongZ, cell + WallThickness, WallHeight),
                    wall, collider: true, layer: Layers.Level);
                return;
            }

            float gap = GapWidth(style, maze);

            // A doorway is the space between two stubs, so the stubs are what actually get built.
            float stub = (cell - gap) * 0.5f;
            if (stub > 0.01f)
            {
                float length = stub + WallThickness * 0.5f;
                float offset = cell * 0.5f + WallThickness * 0.25f - stub * 0.5f;

                Build.Cube(parent, "Wall", line + up - run * offset, Extent(alongZ, length, WallHeight),
                    wall, collider: true, layer: Layers.Level);
                Build.Cube(parent, "Wall", line + up + run * offset, Extent(alongZ, length, WallHeight),
                    wall, collider: true, layer: Layers.Level);
            }

            float trimHeight = 0.06f;

            if (style == DoorStyle.Ledge)
            {
                // Sill below and lintel above, leaving a tall window. The sill is the whole
                // point: too high for a ground enemy to step, low enough for a base jump.
                Build.Cube(parent, "LedgeSill", line + Vector3.up * (LedgeSill * 0.5f),
                    Extent(alongZ, gap, LedgeSill), wall, collider: true, layer: Layers.Level);

                float lintel = WallHeight - LedgeTop;
                Build.Cube(parent, "LedgeLintel", line + Vector3.up * (LedgeTop + lintel * 0.5f),
                    Extent(alongZ, gap, lintel), wall, collider: true, layer: Layers.Level);

                trimHeight = LedgeSill + 0.06f;
            }

            // A lit strip across the threshold. In a maze you need to spot a way through from
            // the far side of a chamber, and an unlit gap in a dark wall does not read.
            Build.Cube(parent, "DoorTrim", line + Vector3.up * trimHeight,
                alongZ ? new Vector3(0.3f, 0.12f, gap) : new Vector3(gap, 0.12f, 0.3f),
                trim, collider: false, layer: Layers.Level);
        }

        private static float GapWidth(DoorStyle style, MazeLayout maze)
        {
            switch (style)
            {
                case DoorStyle.Narrow: return NarrowWidth;
                case DoorStyle.Ledge: return LedgeWidth;
                default: return maze.DoorWidth;
            }
        }

        private static Vector3 Extent(bool alongZ, float length, float height)
            => alongZ
                ? new Vector3(WallThickness, height, length)
                : new Vector3(length, height, WallThickness);

        // ---------------------------------------------------------------- lighting

        private static void BuildLights(Transform parent, MazeLayout maze, RoomNode node)
        {
            Color colour = Color.Lerp(Color.white, node.Accent, 0.35f);

            foreach (Vector2Int cell in maze.Chambers())
            {
                var go = Build.Empty(parent, "MazeLight", maze.CellCentre(cell) + Vector3.up * 5.5f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = maze.CellSize * 1.6f;
                light.intensity = 1.3f;
                light.color = colour;
            }
        }
    }
}
