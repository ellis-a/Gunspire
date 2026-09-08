using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Fills one chamber according to its archetype.
    ///
    /// Everything here is non-blocking by construction rather than by checking afterwards:
    /// columns sit on grids with gaps wider than anything that walks, the central block leaves a
    /// corridor all the way round, and the ring wall opens towards every doorway. That matters
    /// because furniture that seals a chamber is a soft-locked run, and it would only show up on
    /// the one seed nobody tested.
    /// </summary>
    public static class ChamberBuilder
    {
        private const float FullHeight = MazeBuilder.WallHeight;

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public static void Furnish(Transform parent, MazeLayout maze, ChamberGroup group,
            RoomNode node, Rng rng, Material prop, Material trim)
        {
            switch (group.Archetype)
            {
                case ChamberArchetype.Colonnade: Colonnade(parent, maze, group, prop); break;
                case ChamberArchetype.PillarForest: PillarForest(parent, maze, group, rng, prop); break;
                case ChamberArchetype.Keep: Keep(parent, group, prop); break;
                case ChamberArchetype.Rotunda: Rotunda(parent, maze, group, prop); break;
                case ChamberArchetype.Rubble: Rubble(parent, maze, group, node, rng); break;
                case ChamberArchetype.Alcoves: Alcoves(parent, maze, group, rng, prop); break;
                case ChamberArchetype.Embrasure: Embrasure(parent, maze, group, prop, trim); break;
            }
        }

        // ---------------------------------------------------------------- columns

        /// <summary>Four columns on a fixed grid. Regular spacing is what makes it read as built.</summary>
        private static void Colonnade(Transform parent, MazeLayout maze, ChamberGroup group, Material prop)
        {
            float offset = maze.CellSize * 0.25f;

            for (int c = 0; c < group.Cells.Count; c++)
            {
                Vector3 centre = maze.CellCentre(group.Cells[c]);

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 spot = centre + new Vector3(sx * offset, 0f, sz * offset);
                        Build.Cube(parent, "Column", spot + Vector3.up * (FullHeight * 0.5f),
                            new Vector3(1.6f, FullHeight, 1.6f), prop, collider: true, layer: Layers.Level);
                    }
                }
            }
        }

        /// <summary>
        /// Nine thin columns on a jittered grid. The jitter is bounded so the tightest possible
        /// gap is still over two metres - wider than anything in the roster.
        /// </summary>
        private static void PillarForest(Transform parent, MazeLayout maze, ChamberGroup group,
            Rng rng, Material prop)
        {
            const float spacing = 4.4f;
            const float jitter = 0.7f;
            const float thickness = 0.85f;

            for (int c = 0; c < group.Cells.Count; c++)
            {
                Vector3 centre = maze.CellCentre(group.Cells[c]);

                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        Vector3 spot = centre + new Vector3(
                            i * spacing + rng.Range(-jitter, jitter), 0f,
                            j * spacing + rng.Range(-jitter, jitter));

                        Build.Cube(parent, "Pillar", spot + Vector3.up * (FullHeight * 0.5f),
                            new Vector3(thickness, FullHeight, thickness), prop,
                            collider: true, layer: Layers.Level);
                    }
                }
            }
        }

        /// <summary>
        /// A block in the middle, sized to half the chamber's shorter side so the corridor
        /// around it is always a quarter of that side - wider than the widest doorway approach.
        /// This is the one archetype that puts a loop inside a single chamber.
        /// </summary>
        private static void Keep(Transform parent, ChamberGroup group, Material prop)
        {
            float side = Mathf.Min(group.Size.x, group.Size.y) * 0.5f;

            Build.Cube(parent, "Keep", group.Centre + Vector3.up * (FullHeight * 0.5f),
                new Vector3(side, FullHeight, side), prop, collider: true, layer: Layers.Level);
        }

        // ---------------------------------------------------------------- ring

        /// <summary>
        /// A circular chamber, made by filling everything between the circle and the square cell
        /// with solid wall, leaving gaps facing the doorways.
        ///
        /// Filling out to the cell wall rather than standing a ring in the middle of it is the
        /// difference between a round room and a round thing in a square room: with an inset
        /// ring you can simply walk around the outside of it, which is not a rotunda.
        /// </summary>
        private static void Rotunda(Transform parent, MazeLayout maze, ChamberGroup group, Material prop)
        {
            Vector2Int cell = group.Cells[0];
            Vector3 centre = maze.CellCentre(cell);

            float half = maze.CellSize * 0.5f;
            float radius = half - 2.5f;
            const int segments = 32;

            // Wide enough to walk through comfortably, tight enough that a chamber with all
            // four doorways still has more wall than gap - at 34 degrees it degenerated into
            // four corner blocks and stopped reading as round at all.
            const float gapHalfAngle = 30f;

            // Overshoot into the cell wall, so the flat outer face of each segment cannot leave
            // a notch where it fails to reach the corner.
            const float overshoot = 1f;

            // Bearings, clockwise from north, of every way out of this chamber.
            var doorways = new System.Collections.Generic.List<float>();
            for (int i = 0; i < Directions.Length; i++)
            {
                if (EdgeInDirection(maze, cell, Directions[i]) == EdgeState.Solid) continue;
                doorways.Add(Mathf.Atan2(Directions[i].x, Directions[i].y) * Mathf.Rad2Deg);
            }

            float step = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * step;

                bool facesDoor = false;
                for (int d = 0; d < doorways.Count; d++)
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, doorways[d])) < gapHalfAngle) facesDoor = true;
                if (facesDoor) continue;

                float radians = angle * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));

                // How far the square cell wall is along this bearing. Ranges from half at the
                // cardinals out to half * root two at the corners, which is the whole point.
                float toWall = half / Mathf.Max(Mathf.Abs(Mathf.Sin(radians)), Mathf.Abs(Mathf.Cos(radians)));
                float depth = toWall + overshoot - radius;

                // Sized off the outer radius so neighbouring segments overlap all the way out
                // rather than fanning apart and leaving slots to shoot through.
                float chord = 2f * (toWall + overshoot) * Mathf.Sin(Mathf.PI / segments) * 1.2f;

                GameObject segment = Build.Cube(parent, "Rotunda",
                    centre + direction * (radius + depth * 0.5f) + Vector3.up * (FullHeight * 0.5f),
                    new Vector3(chord, FullHeight, depth), prop, collider: true, layer: Layers.Level);

                // Rotating about Y puts the cube's local X along the tangent and local Z along
                // the radius, which is what the chord and depth above assume.
                segment.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        // ---------------------------------------------------------------- cover

        /// <summary>Destructible cover, so a chamber can be reshaped by fighting in it.</summary>
        private static void Rubble(Transform parent, MazeLayout maze, ChamberGroup group,
            RoomNode node, Rng rng)
        {
            float offset = maze.CellSize * 0.25f;

            for (int c = 0; c < group.Cells.Count; c++)
            {
                Vector3 centre = maze.CellCentre(group.Cells[c]);

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 spot = centre + new Vector3(
                            sx * offset + rng.Range(-1.5f, 1.5f), 0.55f,
                            sz * offset + rng.Range(-1.5f, 1.5f));

                        bool reinforced = rng.Chance(0.3f);
                        Color colour = reinforced ? new Color(0.35f, 0.36f, 0.42f) : Palette.Crate;

                        GameObject crate = Build.Cube(parent, reinforced ? "ReinforcedCrate" : "Crate",
                            spot, new Vector3(1.1f, 1.1f, 1.1f), MaterialLibrary.Lit(colour, 0.1f),
                            collider: true, layer: Layers.Prop);

                        var smashable = crate.AddComponent<Smashable>();
                        smashable.Hardness = reinforced ? 6f + node.Floor : 0f;
                        smashable.MaxHealth = reinforced ? 60f : 25f;
                        smashable.BodyColor = colour;
                    }
                }
            }
        }

        /// <summary>
        /// Pairs of stubs against the solid walls, forming pockets. Only ever attached to a wall
        /// that is already there, so these cannot cut a chamber in half.
        /// </summary>
        private static void Alcoves(Transform parent, MazeLayout maze, ChamberGroup group,
            Rng rng, Material prop)
        {
            const float depth = 3.2f;
            const float thickness = 1f;
            const float mouth = 4.2f;
            const float height = 3.5f;

            for (int c = 0; c < group.Cells.Count; c++)
            {
                Vector2Int cell = group.Cells[c];
                Vector3 centre = maze.CellCentre(cell);

                for (int i = 0; i < Directions.Length; i++)
                {
                    if (EdgeInDirection(maze, cell, Directions[i]) != EdgeState.Solid) continue;
                    if (!rng.Chance(0.6f)) continue;

                    var facing = new Vector3(Directions[i].x, 0f, Directions[i].y);
                    Vector3 sideways = Vector3.Cross(Vector3.up, facing);

                    // Slid along the wall so the pockets are not all dead centre.
                    Vector3 along = sideways * rng.Range(-2.5f, 2.5f);
                    Vector3 root = centre + facing * (maze.CellSize * 0.5f - depth * 0.5f) + along;

                    Vector3 size = Mathf.Abs(facing.x) > 0.5f
                        ? new Vector3(depth, height, thickness)
                        : new Vector3(thickness, height, depth);

                    Build.Cube(parent, "Alcove", root + sideways * (mouth * 0.5f) + Vector3.up * (height * 0.5f),
                        size, prop, collider: true, layer: Layers.Level);
                    Build.Cube(parent, "Alcove", root - sideways * (mouth * 0.5f) + Vector3.up * (height * 0.5f),
                        size, prop, collider: true, layer: Layers.Level);
                }
            }
        }

        // ---------------------------------------------------------------- embrasure

        /// <summary>
        /// A wall across the chamber with a firing slot in it. The slot spans 0.85m to 1.85m,
        /// which covers every ground enemy's eye line and the player's camera at 1.62m, so both
        /// sides can shoot through it at any range. Neither can walk through it at all.
        ///
        /// The wall stops well below the Gazer's hover, so the flier is the one thing that can
        /// cross - which is most of what makes the room interesting.
        /// </summary>
        private static void Embrasure(Transform parent, MazeLayout maze, ChamberGroup group,
            Material prop, Material trim)
        {
            const float slotBottom = 0.85f;
            const float slotTop = 1.85f;
            const float height = 3.4f;
            const float thickness = 1.2f;

            Vector3 centre = maze.CellCentre(group.Cells[0]);
            float span = maze.CellSize + MazeBuilder.WallThickness;

            Vector3 Extent(float h) => group.EmbrasureAlongX
                ? new Vector3(span, h, thickness)
                : new Vector3(thickness, h, span);

            Build.Cube(parent, "EmbrasureLower", centre + Vector3.up * (slotBottom * 0.5f),
                Extent(slotBottom), prop, collider: true, layer: Layers.Level);

            float upper = height - slotTop;
            Build.Cube(parent, "EmbrasureUpper", centre + Vector3.up * (slotTop + upper * 0.5f),
                Extent(upper), prop, collider: true, layer: Layers.Level);

            // A lit lip on the sill, so the slot reads as something to shoot through rather
            // than a gap where the wall failed to build.
            Vector3 lip = group.EmbrasureAlongX
                ? new Vector3(span, 0.12f, thickness + 0.3f)
                : new Vector3(thickness + 0.3f, 0.12f, span);

            Build.Cube(parent, "EmbrasureLip", centre + Vector3.up * (slotBottom + 0.06f),
                lip, trim, collider: false, layer: Layers.Level);
        }

        // ---------------------------------------------------------------- shared

        private static EdgeState EdgeInDirection(MazeLayout maze, Vector2Int cell, Vector2Int dir)
        {
            if (dir.x > 0) return maze.EastEdge(cell.x, cell.y);
            if (dir.x < 0) return maze.EastEdge(cell.x - 1, cell.y);
            if (dir.y > 0) return maze.NorthEdge(cell.x, cell.y);
            return maze.NorthEdge(cell.x, cell.y - 1);
        }
    }
}
