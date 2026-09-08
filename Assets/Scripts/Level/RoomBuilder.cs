using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Generates one room from a <see cref="RoomNode"/>: the shell, cover to break line of
    /// sight, platforms that reward a high jump, enemies, and whatever reward the room kind
    /// promises. Everything is primitives, so there is nothing to author in the editor.
    /// </summary>
    public static class RoomBuilder
    {
        private struct Occupied
        {
            public Vector2 Center;
            public float Radius;
        }

        /// <summary>
        /// Step one of the maze work: layouts only. A maze room deliberately spawns nothing to
        /// fight, because enemies cannot path through concave geometry yet - they beeline and
        /// slide along walls, which is enough for the convex pillars of a rectangular room and
        /// nothing at all in a maze. Set this false to get the old combat rooms back.
        /// </summary>
        public static bool MazeLayouts = true;

        private static bool UsesMaze(RoomKind kind) => MazeLayouts && kind == RoomKind.Combat;

        public static RoomRuntime Generate(RoomNode node)
        {
            var rng = new Rng(node.Seed);
            return UsesMaze(node.Kind) ? GenerateMaze(node, rng) : GenerateRectangle(node, rng);
        }

        /// <summary>
        /// A maze floor: shell only for now. The exit sits in the chamber furthest from the
        /// entrance by path length, so finding it means walking the maze rather than crossing
        /// a room, which is the thing worth judging before any content goes in.
        /// </summary>
        private static RoomRuntime GenerateMaze(RoomNode node, Rng rng)
        {
            var maze = new MazeLayout(MazeSettings.For(node.Kind), rng);

            var root = new GameObject("Maze_" + node.Kind + "_F" + node.Floor);
            var runtime = root.AddComponent<RoomRuntime>();
            runtime.Kind = node.Kind;

            MazeBuilder.BuildShell(root.transform, maze, node, rng);

            runtime.PlayerSpawn = maze.CellCentre(maze.Start) + Vector3.up * 0.2f;
            runtime.PlayerFacing = Quaternion.LookRotation(maze.ExitDirection(maze.Start), Vector3.up);

            runtime.Portal = ExitPortal.Spawn(maze.CellCentre(maze.Exit), Quaternion.identity);
            runtime.Portal.transform.SetParent(root.transform, true);

            return runtime;
        }

        private static RoomRuntime GenerateRectangle(RoomNode node, Rng rng)
        {
            Vector2 size = SizeFor(node.Kind);
            float width = size.x;
            float depth = size.y;

            var root = new GameObject("Room_" + node.Kind + "_F" + node.Floor);
            var runtime = root.AddComponent<RoomRuntime>();
            runtime.Kind = node.Kind;

            var occupied = new List<Occupied>();

            BuildShell(root.transform, width, depth, node);

            Vector3 playerSpawn = new Vector3(0f, 0.2f, -depth * 0.5f + 5f);
            Vector3 portalSpot = new Vector3(0f, 0f, depth * 0.5f - 4.5f);
            runtime.PlayerSpawn = playerSpawn;

            occupied.Add(new Occupied { Center = new Vector2(playerSpawn.x, playerSpawn.z), Radius = 6f });
            occupied.Add(new Occupied { Center = new Vector2(portalSpot.x, portalSpot.z), Radius = 4f });

            runtime.Portal = ExitPortal.Spawn(portalSpot, Quaternion.identity);
            runtime.Portal.transform.SetParent(root.transform, true);

            BuildCover(root.transform, width, depth, node, rng, occupied);
            BuildPlatforms(root.transform, width, depth, node, rng, occupied);
            BuildLights(root.transform, width, depth, node);

            PopulateRoom(runtime, root.transform, width, depth, node, rng, occupied);

            return runtime;
        }

        /// <summary>The Luck every rarity roll in a room is made against.</summary>
        private static float PlayerLuck()
        {
            PlayerRig rig = PlayerRig.Instance;
            return rig != null && rig.Sheet != null ? rig.Sheet.GetStat(StatType.Luck) : 0f;
        }

        private static Vector2 SizeFor(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Elite: return new Vector2(36f, 40f);
                case RoomKind.Treasure: return new Vector2(24f, 26f);
                case RoomKind.Shrine: return new Vector2(24f, 26f);
                case RoomKind.Forge: return new Vector2(32f, 34f);
                case RoomKind.Boss: return new Vector2(54f, 56f);
                default: return new Vector2(40f, 44f);
            }
        }

        // ---------------------------------------------------------------- shell

        private static void BuildShell(Transform parent, float width, float depth, RoomNode node)
        {
            const float wallHeight = 11f;
            const float thickness = 1.5f;

            Material floorMaterial = MaterialLibrary.Lit(Palette.Floor, 0.05f);
            Material wallMaterial = MaterialLibrary.Lit(Palette.Wall, 0.05f);
            Material trimMaterial = MaterialLibrary.Emissive(node.Accent * 0.6f, 1.2f);

            Build.Cube(parent, "Floor", new Vector3(0f, -0.5f, 0f),
                new Vector3(width, 1f, depth), floorMaterial, collider: true, layer: Layers.Level);

            Build.Cube(parent, "Wall_North", new Vector3(0f, wallHeight * 0.5f, depth * 0.5f + thickness * 0.5f),
                new Vector3(width + thickness * 2f, wallHeight, thickness), wallMaterial, true, Layers.Level);
            Build.Cube(parent, "Wall_South", new Vector3(0f, wallHeight * 0.5f, -depth * 0.5f - thickness * 0.5f),
                new Vector3(width + thickness * 2f, wallHeight, thickness), wallMaterial, true, Layers.Level);
            Build.Cube(parent, "Wall_East", new Vector3(width * 0.5f + thickness * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(thickness, wallHeight, depth), wallMaterial, true, Layers.Level);
            Build.Cube(parent, "Wall_West", new Vector3(-width * 0.5f - thickness * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(thickness, wallHeight, depth), wallMaterial, true, Layers.Level);

            // A glowing seam around the floor, so the arena edge reads at a glance.
            Build.Cube(parent, "Trim_North", new Vector3(0f, 0.06f, depth * 0.5f - 0.4f),
                new Vector3(width, 0.12f, 0.25f), trimMaterial, collider: false, layer: Layers.Level);
            Build.Cube(parent, "Trim_South", new Vector3(0f, 0.06f, -depth * 0.5f + 0.4f),
                new Vector3(width, 0.12f, 0.25f), trimMaterial, collider: false, layer: Layers.Level);
        }

        private static void BuildLights(Transform parent, float width, float depth, RoomNode node)
        {
            int count = node.Kind == RoomKind.Boss ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                var go = Build.Empty(parent, "RoomLight",
                    new Vector3(Mathf.Lerp(-width * 0.3f, width * 0.3f, t), 7f,
                                Mathf.Lerp(-depth * 0.3f, depth * 0.3f, t)));

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = Mathf.Max(width, depth) * 0.8f;
                light.intensity = 1.6f;
                light.color = Color.Lerp(Color.white, node.Accent, 0.35f);
            }
        }

        // ---------------------------------------------------------------- furniture

        private static void BuildCover(Transform parent, float width, float depth, RoomNode node,
            Rng rng, List<Occupied> occupied)
        {
            if (node.Kind == RoomKind.Treasure || node.Kind == RoomKind.Shrine) return;

            int pillars = node.Kind == RoomKind.Boss ? 8 : rng.Range(5, 9);
            Material material = MaterialLibrary.Lit(Palette.Prop, 0.1f);

            for (int i = 0; i < pillars; i++)
            {
                if (!TryFindSpot(width, depth, 2.6f, rng, occupied, out Vector3 spot, clearance: 2f)) continue;

                float w = rng.Range(1.6f, 3.2f);
                float h = rng.Range(3.5f, 6.5f);
                Build.Cube(parent, "Pillar", spot + Vector3.up * (h * 0.5f),
                    new Vector3(w, h, w), material, collider: true, layer: Layers.Level);
            }
        }

        private static void BuildPlatforms(Transform parent, float width, float depth, RoomNode node,
            Rng rng, List<Occupied> occupied)
        {
            int platforms = node.Kind == RoomKind.Boss ? 4 : rng.Range(2, 5);
            Material material = MaterialLibrary.Lit(Palette.Trim * 0.7f, 0.15f);

            for (int i = 0; i < platforms; i++)
            {
                if (!TryFindSpot(width, depth, 3.2f, rng, occupied, out Vector3 spot, clearance: 2.5f)) continue;

                // A starting wizard jumps 1.55m. Spreading platform heights either side of that
                // is what makes Agility (and the jump-height boons) open up new routes.
                float height = rng.Range(1.2f, 2.6f);
                Build.Cube(parent, "Platform", spot + Vector3.up * (height - 0.2f),
                    new Vector3(rng.Range(4f, 7f), 0.4f, rng.Range(4f, 7f)),
                    material, collider: true, layer: Layers.Level);
            }
        }

        // ---------------------------------------------------------------- contents

        private static void PopulateRoom(RoomRuntime runtime, Transform parent, float width, float depth,
            RoomNode node, Rng rng, List<Occupied> occupied)
        {
            int floor = node.Floor;

            switch (node.Kind)
            {
                case RoomKind.Combat:
                    SpawnCrates(parent, width, depth, rng, occupied, rng.Range(3, 6), floor);
                    SpawnEnemies(runtime, width, depth, rng, occupied,
                        Mathf.Min(11, 4 + floor), floor, false);
                    break;

                case RoomKind.Elite:
                    SpawnCrates(parent, width, depth, rng, occupied, rng.Range(2, 4), floor);
                    SpawnEnemies(runtime, width, depth, rng, occupied,
                        Mathf.Min(5, 1 + floor / 2), floor, true);
                    SpawnEnemies(runtime, width, depth, rng, occupied, 2, floor, false);
                    break;

                case RoomKind.Treasure:
                    SpawnCrates(parent, width, depth, rng, occupied, 5, floor, forceReinforced: true);
                    if (TryFindSpot(width, depth, 2f, rng, occupied, out Vector3 vaultSpot, clearance: 2f))
                        WeaponPickup.Spawn(vaultSpot, WeaponLibrary.RollDrop(rng, PlayerLuck(), 1.5f))
                            .transform.SetParent(parent, true);
                    OrbPickup.SpawnHealth(new Vector3(-2.5f, 1f, 0f), 35f);
                    OrbPickup.SpawnMana(new Vector3(2.5f, 1f, 0f), 60f);
                    break;

                case RoomKind.Shrine:
                    SpawnShrineContents(parent, width, depth, rng, occupied);
                    break;

                case RoomKind.Forge:
                    SpawnForgeContents(parent, width, depth, rng, occupied);
                    SpawnEnemies(runtime, width, depth, rng, occupied,
                        Mathf.Min(7, 2 + floor / 2), floor, false);
                    break;

                case RoomKind.Boss:
                    SpawnBoss(runtime, floor);
                    SpawnEnemies(runtime, width, depth, rng, occupied, 2, floor, false);
                    SpawnCrates(parent, width, depth, rng, occupied, 4, floor);
                    break;
            }
        }

        private static void SpawnEnemies(RoomRuntime runtime, float width, float depth, Rng rng,
            List<Occupied> occupied, int count, int floor, bool elite)
        {
            // Enemies stand well clear of the entrance but are allowed to group up with each
            // other, so a room opens as a readable formation rather than scattered singles.
            var entrance = new Vector2(runtime.PlayerSpawn.x, runtime.PlayerSpawn.z);

            for (int i = 0; i < count; i++)
            {
                if (!TryFindSpot(width, depth, 1.6f, rng, occupied, out Vector3 spot,
                        clearance: 1.2f, preferFar: true, avoidPoint: entrance, avoidRadius: 14f))
                    spot = new Vector3(rng.Range(-width * 0.35f, width * 0.35f), 0f, depth * 0.25f);

                // Rolled from the roster rather than a fixed enum, so an authored enemy that
                // opts into the standard roster starts appearing with no code change.
                List<EnemyDefinition> pool = EnemyLibrary.StandardRoster();
                if (pool.Count == 0) continue;

                EnemyController enemy = EnemyFactory.Spawn(rng.Pick(pool), spot, floor, elite);
                if (enemy == null) continue;

                enemy.transform.SetParent(runtime.transform, true);
                runtime.Register(enemy);
            }
        }

        private static void SpawnBoss(RoomRuntime runtime, int floor)
        {
            EnemyController boss = EnemyFactory.Spawn(EnemyLibrary.BossId, new Vector3(0f, 0f, 14f), floor);
            if (boss == null) return;

            boss.transform.SetParent(runtime.transform, true);
            runtime.Register(boss);
        }

        private static void SpawnCrates(Transform parent, float width, float depth, Rng rng,
            List<Occupied> occupied, int count, int floor, bool forceReinforced = false)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryFindSpot(width, depth, 1.2f, rng, occupied, out Vector3 spot, clearance: 1.5f)) continue;

                bool reinforced = forceReinforced || rng.Chance(0.35f);
                float hardness = reinforced ? 6f + floor : 0f;
                Color color = reinforced ? new Color(0.35f, 0.36f, 0.42f) : Palette.Crate;

                GameObject crate = Build.Cube(parent, reinforced ? "ReinforcedCrate" : "Crate",
                    spot + Vector3.up * 0.55f, new Vector3(1.1f, 1.1f, 1.1f),
                    MaterialLibrary.Lit(color, 0.1f), collider: true, layer: Layers.Prop);

                var smashable = crate.AddComponent<Smashable>();
                smashable.Hardness = hardness;
                smashable.MaxHealth = reinforced ? 60f : 25f;
                smashable.BodyColor = color;

                if (reinforced)
                {
                    Build.Cube(crate.transform, "Band", Vector3.zero, new Vector3(1.05f, 0.22f, 1.05f),
                        MaterialLibrary.Emissive(new Color(1f, 0.7f, 0.35f), 1.5f), collider: false);
                }
            }
        }

        private static void SpawnShrineContents(Transform parent, float width, float depth, Rng rng,
            List<Occupied> occupied)
        {
            PlayerRig rig = PlayerRig.Instance;
            SpellBook book = rig != null ? rig.Book : null;
            MovementController movement = rig != null ? rig.Movement : null;

            // A shrine offers either a spell or a new way to move, so the Shift slot is
            // something a run can actually change rather than a fixed opening.
            bool offerMovement = rng.Chance(0.35f);

            if (TryFindSpot(width, depth, 2f, rng, occupied, out Vector3 runeSpot, clearance: 2.5f))
            {
                // Shrines roll their own rarity, so a lucky wizard can find a legendary here.
                if (offerMovement)
                {
                    MovementAbility ability = MovementAbilityLibrary.RollOffer(rng, movement, PlayerLuck(), 1.5f);
                    if (ability != null)
                        MovementPedestal.Spawn(runeSpot, ability).transform.SetParent(parent, true);
                }
                else
                {
                    Spell offer = SpellLibrary.RollOffer(rng, book, PlayerLuck(), 1.5f);
                    if (offer != null)
                        SpellPedestal.Spawn(runeSpot, offer).transform.SetParent(parent, true);
                }
            }

            if (TryFindSpot(width, depth, 2f, rng, occupied, out Vector3 stoneSpot, clearance: 2.5f))
            {
                StatType stat = EnumCache.Stats[rng.Range(0, EnumCache.Stats.Length)];
                StatShrine.Spawn(stoneSpot, stat).transform.SetParent(parent, true);
            }

            OrbPickup.SpawnHealth(new Vector3(0f, 1f, 2f), 25f);
        }

        private static void SpawnForgeContents(Transform parent, float width, float depth, Rng rng,
            List<Occupied> occupied)
        {
            float luck = PlayerLuck();
            WeaponDefinition first = WeaponLibrary.RollDrop(rng, luck);
            WeaponDefinition second = WeaponLibrary.RollDrop(rng, luck);
            int guard = 0;
            while (second.Id == first.Id && guard++ < 12) second = WeaponLibrary.RollDrop(rng, luck);

            var left = new Vector3(-4.5f, 0f, 2f);
            var right = new Vector3(4.5f, 0f, 2f);
            occupied.Add(new Occupied { Center = new Vector2(left.x, left.z), Radius = 3f });
            occupied.Add(new Occupied { Center = new Vector2(right.x, right.z), Radius = 3f });

            WeaponPickup.Spawn(left, first).transform.SetParent(parent, true);
            WeaponPickup.Spawn(right, second).transform.SetParent(parent, true);
        }

        // ---------------------------------------------------------------- placement

        /// <summary>
        /// Rejection-samples a free spot on the floor.
        ///
        /// <paramref name="clearance"/> is the extra gap on top of the two radii, which keeps
        /// things from touching. Staying away from the entrance is a separate concern
        /// (<paramref name="avoidPoint"/>) so enemies can spawn well clear of the player while
        /// still standing near each other.
        /// </summary>
        private static bool TryFindSpot(float width, float depth, float radius, Rng rng,
            List<Occupied> occupied, out Vector3 spot, float clearance = 1.5f,
            bool preferFar = false, Vector2 avoidPoint = default, float avoidRadius = 0f,
            int attempts = 48)
        {
            float halfWidth = width * 0.5f - 3f;
            float halfDepth = depth * 0.5f - 3f;

            for (int i = 0; i < attempts; i++)
            {
                float x = rng.Range(-halfWidth, halfWidth);
                float z = preferFar
                    ? rng.Range(-halfDepth * 0.1f, halfDepth)
                    : rng.Range(-halfDepth, halfDepth);

                var candidate = new Vector2(x, z);

                if (avoidRadius > 0f && Vector2.Distance(candidate, avoidPoint) < avoidRadius)
                    continue;

                bool clear = true;
                for (int j = 0; j < occupied.Count; j++)
                {
                    if (Vector2.Distance(candidate, occupied[j].Center) < occupied[j].Radius + radius + clearance)
                    {
                        clear = false;
                        break;
                    }
                }

                if (!clear) continue;

                occupied.Add(new Occupied { Center = candidate, Radius = radius });
                spot = new Vector3(x, 0f, z);
                return true;
            }

            spot = Vector3.zero;
            return false;
        }
    }
}
