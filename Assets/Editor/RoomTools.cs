#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Checks generated rooms from the player's side:
    /// - every room kind has a nav grid;
    /// - Blink finds somewhere to land from anywhere with room around it, in every direction;
    /// - enemies start clear of the walls and not facing one;
    /// - the training room's dummies come back when killed and heal when left alone.
    ///
    /// Blink's results are also tallied by where the player stood, so a chamber that breaks it shows up by name.
    /// </summary>
    public static class RoomTools
    {
        private const int MazeSeeds = 12;
        private const int OtherSeeds = 3;

        /// <summary>Cells of open floor needed all round a sample, so the nearest landing Blink tries is certainly open.</summary>
        private const int ClearCells = 3;

        private const int MaxSamplesPerRoom = 120;

        [MenuItem("Gunspire/Verify Rooms")]
        public static void VerifyRooms()
        {
            var problems = new List<string>();
            var notes = new List<string>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            bool[,] matrixBefore = ReadCollisionMatrix();

            try
            {
                Layers.ConfigureCollisionMatrix();
                TargetRegistry.Clear();

                var nodes = new List<RoomNode>();
                for (int seed = 1; seed <= MazeSeeds; seed++)
                    nodes.Add(new RoomNode { Kind = RoomKind.Combat, Floor = 3, Seed = seed });
                foreach (RoomKind kind in new[] { RoomKind.Elite, RoomKind.Treasure, RoomKind.Shrine, RoomKind.Forge, RoomKind.Boss })
                    for (int seed = 1; seed <= OtherSeeds; seed++)
                        nodes.Add(new RoomNode { Kind = kind, Floor = 3, Seed = seed });

                var tally = new BlinkTally();
                foreach (RoomNode node in nodes)
                {
                    RoomNode current = node;
                    Guard(problems, node.Kind + " seed " + node.Seed, () => CheckRoom(current, problems, tally, before));
                }

                tally.Report(problems, notes);
                Guard(problems, "the training room", () => CheckTraining(problems, before));
            }
            finally
            {
                WriteCollisionMatrix(matrixBefore);
                TargetRegistry.Clear();
                DestroyNew(before);
            }

            string noteText = notes.Count == 0 ? "" : "\n  " + string.Join("\n  ", notes);

            if (problems.Count == 0)
            {
                Debug.Log("Rooms: every kind has a nav grid, Blink lands from everywhere with room, enemies spawn clear and facing out, "
                          + "and training dummies recover.\n  no problems." + noteText);
                return;
            }

            var report = new StringBuilder("Rooms: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 50; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report + noteText);
        }

        // ---------------------------------------------------------------- rooms

        private static void CheckRoom(RoomNode node, List<string> problems, BlinkTally tally, HashSet<GameObject> before)
        {
            RoomRuntime room = RoomBuilder.Generate(node);
            PlayerRig rig = null;
            string name = node.Kind + " (seed " + node.Seed + ")";

            try
            {
                NavField field = room.GetComponent<NavField>();
                if (field == null || !field.IsBuilt)
                {
                    problems.Add(name + " has no nav grid, so Blink cannot land, minions cannot follow and enemies cannot route in it");
                    return;
                }

                CheckEnemies(room, name, problems);

                rig = PlayerRig.Spawn(room.PlayerSpawn);
                WakeRig(rig);
                Wake(field);
                CheckBlink(room, field, rig, node, tally, problems);
            }
            finally
            {
                if (rig != null) rig.DetachPlayerSystems();
                DestroyNew(before);
            }
        }

        private static void CheckEnemies(RoomRuntime room, string name, List<string> problems)
        {
            int geometryMask = Layers.BlockingMask | (1 << Layers.Prop);

            foreach (EnemyController enemy in room.GetComponentsInChildren<EnemyController>())
            {
                var controller = enemy.GetComponent<CharacterController>();
                float radius = controller.radius;
                if (enemy.Definition != null) radius = Mathf.Max(radius, enemy.Definition.BodyWidth * 0.5f);
                float height = controller.height;

                Vector3 feet = enemy.transform.position;
                Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
                Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.05f, height - radius);
                if (Physics.CheckCapsule(bottom, top, radius, geometryMask, QueryTriggerInteraction.Ignore))
                    problems.Add(name + ": " + enemy.DisplayName + " spawned partly inside the level at " + feet);

                // The nearest wall, found finely and independently of the placement's own sixteen directions.
                Vector3 eye = feet + Vector3.up * Mathf.Min(1f, height * 0.5f);
                float nearest = float.MaxValue;
                Vector3 toWall = Vector3.zero;
                string wallName = "";
                for (int i = 0; i < 72; i++)
                {
                    float angle = i * 5f * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    if (!Physics.Raycast(eye, direction, out RaycastHit hit, 4f, Layers.BlockingMask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.distance >= nearest) continue;

                    nearest = hit.distance;
                    toWall = direction;
                    wallName = hit.collider.name;
                }

                // Turned away from the nearest wall. Placement allows up to 60 degrees off straight away, which is -0.5
                // here; the tolerance covers two walls within a few centimetres of each other.
                if (nearest < 4f && Vector3.Dot(enemy.transform.forward, toWall) > -0.3f)
                {
                    Vector3 wouldFace = SpawnPlacement.FacingAwayFromWalls(eye, Quaternion.identity) * Vector3.forward;
                    problems.Add(name + ": " + enemy.DisplayName + " spawned facing toward a wall " + nearest.ToString("0.0") + "m away"
                                 + " (" + wallName + " toward " + toWall.ToString("0.00") + ", facing " + enemy.transform.forward.ToString("0.00")
                                 + ", placement now picks " + wouldFace.ToString("0.00") + ", eye " + eye.ToString("0.0") + ")");
                }
            }
        }

        // ---------------------------------------------------------------- blink

        private static void CheckBlink(RoomRuntime room, NavField field, PlayerRig rig, RoomNode node, BlinkTally tally,
            List<string> problems)
        {
            Spell blink = SpellLibrary.Get("blink");
            var landing = blink != null && blink.OnCast.Count > 0 ? blink.OnCast[0] as SelectWalkableLandingEffect : null;
            if (landing == null)
            {
                problems.Add("Blink's chain does not start with a walkable landing");
                return;
            }

            int width = 0;
            while (field.InBounds(width, 0)) width++;
            int depth = 0;
            while (field.InBounds(0, depth)) depth++;

            var samples = new List<Vector2Int>();
            for (int y = 0; y < depth; y++)
                for (int x = 0; x < width; x++)
                    if (OpenAround(field, x, y)) samples.Add(new Vector2Int(x, y));

            int stride = Mathf.Max(1, samples.Count / MaxSamplesPerRoom);
            AbilityContext ctx = rig.SpellContext;

            for (int s = 0; s < samples.Count; s += stride)
            {
                Vector2Int cell = samples[s];
                Vector3 centre = field.CellCentre(cell.x, cell.y);
                if (!Physics.Raycast(centre + Vector3.up * 1.5f, Vector3.down, 3f, Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                    continue;

                LandingCheck.Place(rig.transform, centre + Vector3.up * 0.05f);
                field.Rebuild(cell);
                string place = PlaceName(room, node, centre);

                for (int d = 0; d < 8; d++)
                {
                    float angle = d * 45f * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                    ctx.Begin(DamageType.Energy, SpellType.Mobility, Color.white, 1, 1f, isSpell: true);
                    ctx.Forward = direction;

                    bool landed = landing.Execute(ctx) && field.StepsAt(ctx.Point) >= 0;
                    tally.Add(place, landed, node.Kind + " seed " + node.Seed + " at " + centre.ToString("0.0") + " facing " + direction.ToString("0.0"));
                }
            }
        }

        private static bool OpenAround(NavField field, int x, int y)
        {
            for (int dy = -ClearCells; dy <= ClearCells; dy++)
                for (int dx = -ClearCells; dx <= ClearCells; dx++)
                    if (!field.IsWalkable(x + dx, y + dy)) return false;
            return true;
        }

        /// <summary>Where a sample stood: the room kind, or for a maze the chamber's archetype and whether it has a ledge doorway.</summary>
        private static string PlaceName(RoomRuntime room, RoomNode node, Vector3 at)
        {
            MazeLayout maze = room.Maze;
            if (maze == null) return node.Kind + " room";

            var cell = new Vector2Int(
                Mathf.RoundToInt(at.x / maze.CellSize + (maze.Width - 1) * 0.5f),
                Mathf.RoundToInt(at.z / maze.CellSize + (maze.Height - 1) * 0.5f));

            string archetype = "unknown chamber";
            foreach (ChamberGroup group in maze.Groups)
                if (group.Cells.Contains(cell)) archetype = group.Archetype.ToString();

            bool ledge = LedgeDoor(maze.EastEdge(cell.x, cell.y), maze.EastStyle(cell.x, cell.y))
                         || LedgeDoor(maze.EastEdge(cell.x - 1, cell.y), maze.EastStyle(cell.x - 1, cell.y))
                         || LedgeDoor(maze.NorthEdge(cell.x, cell.y), maze.NorthStyle(cell.x, cell.y))
                         || LedgeDoor(maze.NorthEdge(cell.x, cell.y - 1), maze.NorthStyle(cell.x, cell.y - 1));

            return "maze " + archetype + (ledge ? " with a ledge doorway" : "");
        }

        private static bool LedgeDoor(EdgeState state, DoorStyle style) => state != EdgeState.Solid && style == DoorStyle.Ledge;

        private sealed class BlinkTally
        {
            private readonly SortedDictionary<string, int[]> _byPlace = new SortedDictionary<string, int[]>();
            private readonly List<string> _failures = new List<string>();

            public void Add(string place, bool landed, string where)
            {
                if (!_byPlace.TryGetValue(place, out int[] counts)) _byPlace[place] = counts = new int[2];
                counts[0]++;
                if (landed) return;

                counts[1]++;
                if (_failures.Count < 8) _failures.Add(where);
            }

            public void Report(List<string> problems, List<string> notes)
            {
                var line = new StringBuilder("Blink from spots with room all round, failures by where the player stood:");
                foreach (KeyValuePair<string, int[]> pair in _byPlace)
                {
                    line.Append("\n    " + pair.Key + ": " + pair.Value[1] + " of " + pair.Value[0]);
                    if (pair.Value[1] > 0)
                        problems.Add("Blink found nowhere to land " + pair.Value[1] + " of " + pair.Value[0] + " times standing in " + pair.Key);
                }

                for (int i = 0; i < _failures.Count; i++) line.Append("\n    failed: " + _failures[i]);
                notes.Add(line.ToString());
            }
        }

        // ---------------------------------------------------------------- training

        private static void CheckTraining(List<string> problems, HashSet<GameObject> before)
        {
            RoomRuntime room = RoomBuilder.Generate(TrainingRoom.Node());
            TrainingRoom training = room != null ? room.GetComponent<TrainingRoom>() : null;

            try
            {
                if (training == null)
                {
                    problems.Add("the training room was built without its training component");
                    return;
                }

                if (room.GetComponent<NavField>() == null) problems.Add("the training room has no nav grid");

                int count = training.DummyCount;
                if (count < 8) problems.Add("the training room opened with " + count + " dummies");

                var dummies = new List<EnemyController>(room.GetComponentsInChildren<EnemyController>());
                if (dummies.Count == 0) return;

                foreach (EnemyController dummy in dummies)
                {
                    if (dummy.GetComponents<AbilityAttack>().Length > 0) problems.Add("a training dummy can attack");
                    dummy.Health.DestroyOnDeath = false;
                }

                training.Step(0.01f);

                EnemyController killed = dummies[0];
                killed.Health.Kill();
                training.Step(0.1f);
                if (training.DummyCount != count - 1) problems.Add("a killed dummy still counted as standing");

                training.Step(TrainingRoom.RespawnSeconds + 0.1f);
                if (training.DummyCount != count) problems.Add("a killed dummy did not come back after " + TrainingRoom.RespawnSeconds + "s");

                // Health reads its maximum from the sheet in Awake, which edit mode never runs.
                EnemyController hurt = dummies[1];
                hurt.Health.ConfigureMaxHealth(TrainingRoom.DummyDefinition.Health);
                hurt.Health.TakeDamage(DamageInfo.Create(50f, DamageType.Kinetic, Team.Player, null));
                if (training.RecentDamage < 49f) problems.Add("the damage readout missed a 50 damage hit on a dummy (read " + training.RecentDamage + ")");

                training.Step(1f);
                if (hurt.Health.Current >= hurt.Health.Max) problems.Add("a dummy healed while it was still being fought");

                training.Step(TrainingRoom.ResetAfterSeconds);
                if (hurt.Health.Current < hurt.Health.Max) problems.Add("a dummy left alone did not heal");

                training.Step(TrainingRoom.DamageWindow + 0.1f);
                if (training.RecentDamage > 0f) problems.Add("the damage readout kept a hit past its window");
            }
            finally
            {
                if (training != null) training.Unbind();
                DestroyNew(before);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void Guard(List<string> problems, string what, System.Action check)
        {
            try
            {
                check();
            }
            catch (System.Exception e)
            {
                problems.Add(what + " threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        private static void WakeRig(PlayerRig rig)
        {
            Wake(rig.Health);
            Wake(rig.Mana);
            Wake(rig.Motor);
            Wake(rig.Look);
            Wake(rig);
        }

        private static void Wake(Component component)
        {
            if (component == null) return;
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (awake != null) awake.Invoke(component, null);
        }

        private static void DestroyNew(HashSet<GameObject> before)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!before.Contains(root)) Object.DestroyImmediate(root);
        }

        private static bool[,] ReadCollisionMatrix()
        {
            var ignored = new bool[32, 32];
            for (int a = 0; a < 32; a++)
                for (int b = a; b < 32; b++)
                    ignored[a, b] = Physics.GetIgnoreLayerCollision(a, b);
            return ignored;
        }

        private static void WriteCollisionMatrix(bool[,] ignored)
        {
            for (int a = 0; a < 32; a++)
                for (int b = a; b < 32; b++)
                    if (Physics.GetIgnoreLayerCollision(a, b) != ignored[a, b])
                        Physics.IgnoreLayerCollision(a, b, ignored[a, b]);
        }
    }
}
#endif
