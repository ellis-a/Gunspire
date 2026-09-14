#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Exercises the world systems against real components: the world clock and held damage, walking
    /// minions and their persistence, knockback impacts, zones and trails, the shot-stopping volumes,
    /// and the level services.
    ///
    /// Also measures a horde of walking minions, since an uncapped zombie count is the risk the plan
    /// names. That figure is reported, not judged, unless it is far out of bounds.
    /// </summary>
    public static class WorldTools
    {
        private const float Tolerance = 0.01f;

        [MenuItem("Gunspire/Verify World Systems")]
        public static void VerifyWorldSystems()
        {
            var problems = new List<string>();
            var notes = new List<string>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

            // The collision matrix lives in project settings in the editor, and batch mode saves it on
            // quit, so the matrix the game applies at runtime is put back afterwards rather than being
            // written into a tracked settings file as a side effect of running a check.
            bool[,] matrixBefore = ReadCollisionMatrix();

            try
            {
                Layers.ConfigureCollisionMatrix();
                CheckWorldClock(problems);
                CheckMinions(problems, notes);
                CheckKnockbackImpacts(problems);
                CheckZonesAndTrails(problems);
                CheckVolumes(problems);
                CheckLevelServices(problems);
            }
            finally
            {
                WriteCollisionMatrix(matrixBefore);
                WorldClock.Reset();
                TargetRegistry.Clear();
                Hazards.Clear();
                DeathRecords.Clear();

                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }

            string noteText = notes.Count == 0 ? "" : "\n  " + string.Join("\n  ", notes);

            if (problems.Count == 0)
            {
                Debug.Log("World systems: world clock, minions, knockback impacts, zones, trails, volumes and "
                          + "level services all behave as specified.\n  no problems." + noteText);
                return;
            }

            var report = new StringBuilder("World systems: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 30; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report + noteText);
        }

        // ---------------------------------------------------------------- 3.1 the world clock

        private static void CheckWorldClock(List<string> problems)
        {
            WorldClock.Reset();
            var slowA = new object();
            var slowB = new object();
            var stop = new object();

            WorldClock.Set(slowA, 0.5f);
            WorldClock.Set(slowB, 0.5f);
            if (Mathf.Abs(WorldClock.Rate - 0.25f) > Tolerance) problems.Add("two half-speed slows gave a rate of " + WorldClock.Rate + ", not 0.25");

            WorldClock.Release(slowB);
            if (Mathf.Abs(WorldClock.Rate - 0.5f) > Tolerance) problems.Add("releasing one slow left the rate at " + WorldClock.Rate + ", not 0.5");
            WorldClock.Release(slowA);

            TargetRegistry.Clear();
            Health player = Subject("ClockPlayer", Team.Player, new Vector3(0f, 0f, -900f));
            TargetRegistry.SetPlayerBody(player.transform, player);
            Health enemy = Subject("ClockEnemy", Team.Enemy, new Vector3(5f, 0f, -900f));

            WorldClock.Set(stop, 0f);
            if (!WorldClock.IsStopped) problems.Add("a stop request did not stop the world");
            if (WorldClock.DeltaFor(enemy.gameObject) != 0f) problems.Add("an enemy still has time to spend while the world is stopped");
            if (WorldClock.DeltaFor(player.gameObject) != Time.deltaTime) problems.Add("the player's clock stopped with the world's");

            float then = WorldClock.Now;
            WorldClock.Tick(1f);
            if (WorldClock.Now != then) problems.Add("world time advanced while the world was stopped");

            enemy.TakeDamage(DamageInfo.Create(30f, DamageType.Kinetic, Team.Player, null));
            if (!Approx(enemy.Current, 1000f)) problems.Add("an enemy took damage while the world was stopped; it should wait");
            if (WorldClock.HeldCount != 1) problems.Add(WorldClock.HeldCount + " hits held while stopped, expected 1");

            player.TakeDamage(DamageInfo.Create(10f, DamageType.Kinetic, Team.Enemy, null));
            if (!Approx(player.Current, 990f)) problems.Add("a hit on the player was held while the world was stopped; the player is never held");

            WorldClock.Release(stop);
            if (!Approx(enemy.Current, 970f)) problems.Add("the held hit did not land when the world resumed");
            if (WorldClock.HeldCount != 0) problems.Add("hits were still held after the world resumed");

            WorldClock.StopFor(stop, 2f);
            WorldClock.Tick(1f);
            if (!WorldClock.IsStopped) problems.Add("a two-second stop ended after one second");
            WorldClock.Tick(1.5f);
            if (WorldClock.IsStopped) problems.Add("a two-second stop was still running after two and a half");

            WorldClock.Reset();
        }

        // ---------------------------------------------------------------- 3.2 minions

        private static void CheckMinions(List<string> problems, List<string> notes)
        {
            if ((Layers.HitMaskFor(Team.Enemy) & Layers.MinionMask) == 0) problems.Add("enemy attacks cannot hit minions");
            if ((Layers.TargetMaskFor(Team.Enemy) & Layers.MinionMask) == 0) problems.Add("enemy area attacks cannot find minions");
            if ((Layers.HitMaskFor(Team.Player) & Layers.MinionMask) != 0) problems.Add("the player's shots can hit their own minions");
            if (!Physics.GetIgnoreLayerCollision(Layers.Minion, Layers.Player)) problems.Add("minions can block the player");
            if (Physics.GetIgnoreLayerCollision(Layers.Minion, Layers.Enemy)) problems.Add("minions do not block enemies");
            if (Physics.GetIgnoreLayerCollision(Layers.Minion, Layers.Minion)) problems.Add("minions pass through each other and would stack");

            TargetRegistry.Clear();
            Health owner = Subject("MinionOwner", Team.Player, new Vector3(0f, 0f, -500f));
            TargetRegistry.SetPlayerBody(owner.transform, owner);

            MinionController zombie = MinionSummoner.Spawn("zombie", new Vector3(0f, 0f, -490f));
            if (zombie == null)
            {
                problems.Add("could not summon the reference zombie");
                return;
            }

            foreach (Transform part in zombie.GetComponentsInChildren<Transform>(true))
            {
                if (part.gameObject.layer == Layers.Minion) continue;
                problems.Add("part \"" + part.name + "\" of a minion is not on the minion layer");
                break;
            }

            if (zombie.Health == null || zombie.Health.Team != Team.Player) problems.Add("a minion is not on the player's side");
            if (zombie.AttackOrigin != DamageOrigin.Minion) problems.Add("a minion's hits report as " + zombie.AttackOrigin);
            if (zombie.GetComponents<AbilityAttack>().Length != zombie.Definition.Attacks.Count) problems.Add("a minion did not build its attacks");
            if (!InRegistry(zombie.transform)) problems.Add("a summoned minion is not something enemies can target");

            EnemyController enemy = EnemyFactory.Spawn("cultist", new Vector3(0f, 0f, -487f), 1);
            enemy.Health.DestroyOnDeath = false;
            enemy.Health.ConfigureMaxHealth(100f);
            Physics.SyncTransforms();

            zombie.RefreshTarget();
            if (zombie.Target != enemy.transform) problems.Add("a minion 3m from an enemy did not take it as its target");

            MinionDefinition companion = MinionLibrary.Get("zombie");
            companion.ReviveSeconds = 2f;
            MinionController beast = MinionSummoner.Spawn(companion, new Vector3(10f, 0f, -490f));

            beast.Health.TakeDamage(DamageInfo.Create(100000f, DamageType.True, Team.Enemy, null));
            if (!beast.IsDown) problems.Add("a minion with a revive timer died instead of going down");
            if (InRegistry(beast.transform)) problems.Add("a downed minion can still be targeted");

            beast.Step(1f);
            if (!beast.IsDown) problems.Add("a downed minion got up a second early");
            beast.Step(1.2f);
            if (beast.IsDown || !beast.Health.IsAlive) problems.Add("a downed minion did not get back up after its timer");
            else if (!InRegistry(beast.transform)) problems.Add("a minion that got back up is not targetable again");

            MinionDefinition plague = MinionLibrary.Get("zombie");
            plague.Id = "plague_test";
            plague.Persistent = false;
            MinionController temporary = MinionSummoner.Spawn(plague, new Vector3(20f, 0f, -490f));

            var roster = new MinionRoster();
            roster.Remember(new List<MinionController> { zombie, beast, temporary });
            if (roster.CountOf("zombie") != 2) problems.Add("the roster kept " + roster.CountOf("zombie") + " zombies of 2");
            if (roster.CountOf("plague_test") != 0) problems.Add("the roster kept a minion that is not persistent");

            MeasureHorde(problems, notes);
        }

        private static void MeasureHorde(List<string> problems, List<string> notes)
        {
            var horde = new List<MinionController>();
            for (int i = 0; i < 100; i++)
                horde.Add(MinionSummoner.Spawn("zombie", new Vector3(-600f + (i % 10) * 2f, 0f, -600f + (i / 10) * 2f)));
            Physics.SyncTransforms();

            const int frames = 30;
            Stopwatch watch = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
                for (int i = 0; i < horde.Count; i++)
                    if (horde[i] != null) horde[i].Step(1f / 60f);
            watch.Stop();

            double perFrame = watch.Elapsed.TotalMilliseconds / frames;
            notes.Add("a horde of " + horde.Count + " walking minions costs " + perFrame.ToString("0.00")
                      + " ms per frame, measured in edit mode");

            if (perFrame > 50.0) problems.Add("a horde of 100 minions takes " + perFrame.ToString("0") + " ms per frame");
        }

        // ---------------------------------------------------------------- 3.3 knockback impacts

        private static void CheckKnockbackImpacts(List<string> problems)
        {
            if (KnockbackImpacts.DamageFor(KnockbackImpacts.WallClosingSpeed(new Vector3(-12f, 0f, 0f), Vector3.right)) <= 0f)
                problems.Add("a 12 m/s head-on wall impact did no damage");
            if (KnockbackImpacts.WallClosingSpeed(new Vector3(0f, 0f, 12f), Vector3.right) > 0f)
                problems.Add("sliding along a wall counts as closing on it");
            if (KnockbackImpacts.DamageFor(4f) > 0f)
                problems.Add("a 4 m/s bump, below the threshold, did damage");

            var together = new Vector3(10f, 0f, 0f);
            if (KnockbackImpacts.BodyClosingSpeed(together, together, Vector3.zero, Vector3.right) > 0f)
                problems.Add("two bodies shoved forward together close on each other, so a crowd would grind itself down");

            float speed = 30f;
            int impacts = 0;
            while (KnockbackImpacts.DamageFor(speed) > 0f && impacts < 50)
            {
                impacts++;
                speed *= KnockbackImpacts.Transfer;
            }
            if (impacts >= 50) problems.Add("a cascade of impacts never ends");

            var instigator = new GameObject("Knocker");

            EnemyController wallStruck = Spawn("cultist", new Vector3(0f, 0f, -700f));
            DamageInfo last = default;
            wallStruck.Health.Damaged += (info, amount) => last = info;

            wallStruck.AddKnockback(new Vector3(-14f, 0f, 0f), instigator, Team.Player);
            float before = wallStruck.Health.Current;
            float damage = KnockbackImpacts.ApplyWallImpact(wallStruck, Vector3.right);

            if (damage <= 0f || wallStruck.Health.Current >= before) problems.Add("an enemy knocked into a wall at 14 m/s took no damage");
            else if (last.Origin != DamageOrigin.Collision) problems.Add("impact damage was reported as " + last.Origin + ", not Collision");
            if (wallStruck.Knockback.x < -0.01f) problems.Add("the push into the wall was not spent, so the same wall would be struck again");
            if (KnockbackImpacts.ApplyWallImpact(wallStruck, Vector3.right) > 0f) problems.Add("the same wall hurt twice from one knockback");

            EnemyController mover = Spawn("cultist", new Vector3(10f, 0f, -700f));
            EnemyController struck = Spawn("cultist", new Vector3(11f, 0f, -700f));
            mover.AddKnockback(new Vector3(14f, 0f, 0f), instigator, Team.Player);

            float moverBefore = mover.Health.Current, struckBefore = struck.Health.Current;
            KnockbackImpacts.ApplyBodyImpact(mover, struck);

            if (mover.Health.Current >= moverBefore || struck.Health.Current >= struckBefore)
                problems.Add("two enemies colliding at 14 m/s did not both take damage");
            if (struck.Knockback.x <= 0f || struck.Knockback.x >= 14f)
                problems.Add("the struck enemy took on " + struck.Knockback.x + " m/s, expected some but less than the 14 that hit it");

            EnemyController intoMinion = Spawn("cultist", new Vector3(20f, 0f, -700f));
            MinionController minion = MinionSummoner.Spawn("zombie", new Vector3(21f, 0f, -700f));
            intoMinion.AddKnockback(new Vector3(14f, 0f, 0f), instigator, Team.Player);

            float minionBefore = minion.Health.Current;
            KnockbackImpacts.ApplyBodyImpact(intoMinion, minion);
            if (minion.Health.Current >= minionBefore) problems.Add("a minion struck by an enemy the player knocked back took no damage (friendly fire refused it?)");
            if (minion.Knockback.sqrMagnitude <= 0f && minion.Velocity.x <= 0f) problems.Add("a minion struck by a knocked enemy was not pushed");

            var playerBody = new GameObject("KnockedPlayer");
            playerBody.transform.position = new Vector3(30f, 0f, -700f);
            var playerHealth = playerBody.AddComponent<Health>();
            playerHealth.Team = Team.Player;
            playerHealth.DestroyOnDeath = false;
            playerHealth.ConfigureMaxHealth(200f);
            var motor = playerBody.AddComponent<PlayerMotor>();

            var enemySource = new GameObject("Brute");
            motor.AddKnockback(new Vector3(-14f, 0f, 0f), enemySource, Team.Enemy);
            KnockbackImpacts.ApplyWallImpact(motor, Vector3.right);
            if (playerHealth.Current >= 200f) problems.Add("the player knocked into a wall took no damage, but takes it by the same rules");
        }

        // ---------------------------------------------------------------- 3.4 zones, trails and volumes

        private static void CheckZonesAndTrails(List<string> problems)
        {
            var caster = new GameObject("ZoneCaster");
            caster.transform.position = new Vector3(0f, 0f, 100f);

            Health ally = Subject("ZoneAlly", Team.Player, new Vector3(1f, 0f, 100f), Layers.Player, collider: true);
            Health foe = Subject("ZoneFoe", Team.Enemy, new Vector3(-1f, 0f, 100f), Layers.Enemy, collider: true);
            Physics.SyncTransforms();

            LingeringZone zone = LingeringZone.Spawn(new Vector3(0f, 0f, 100f), 3f, 10f, 0f, 0.5f, DamageType.Energy,
                    Team.Player, caster, new List<StatusApplication>(), Color.white)
                .BuffAllies(new List<StatusApplication> { StatusLibrary.Haste(3f) });

            zone.Step(0.6f);
            if (!ally.GetComponent<StatusController>().Has(StatusId.Haste)) problems.Add("an ally inside a friendly zone was not buffed");
            if (foe.GetComponent<StatusController>().Has(StatusId.Haste)) problems.Add("a friendly zone buffed an enemy");

            LingeringZone follower = LingeringZone.Spawn(caster.transform.position, 3f, 10f, 1f, 0.5f, DamageType.Energy,
                Team.Player, caster, new List<StatusApplication>(), Color.white).Follow(caster.transform);
            caster.transform.position = new Vector3(10f, 0f, 100f);
            follower.Step(0.01f);
            if (Vector3.Distance(follower.transform.position, caster.transform.position) > Tolerance)
                problems.Add("a zone following its caster was left behind");

            Hazards.Clear();
            var carrier = new GameObject("TrailCarrier");
            carrier.transform.position = new Vector3(0f, 0f, 200f);

            TrailEmitter trail = TrailEmitter.Attach(carrier.transform, Team.Player, carrier);
            trail.Spacing = 1.5f;
            trail.Radius = 1f;
            trail.SegmentLifetime = 2f;
            trail.DamagePerTick = 5f;

            trail.Step(0.05f);
            if (trail.SegmentCount != 1) problems.Add("a new trail laid " + trail.SegmentCount + " segments on its first frame, expected 1");

            for (int i = 0; i < 10; i++) trail.Step(0.05f);
            if (trail.SegmentCount != 1) problems.Add("standing still laid " + trail.SegmentCount + " segments, so a trail would pile up under someone who stops");

            carrier.transform.position = new Vector3(0f, 0f, 201f);
            trail.Step(0.05f);
            if (trail.SegmentCount != 1) problems.Add("a trail laid a segment after 1m with 1.5m spacing");

            carrier.transform.position = new Vector3(0f, 0f, 201.6f);
            trail.Step(0.05f);
            if (trail.SegmentCount != 2) problems.Add("a trail did not lay a segment after 1.6m with 1.5m spacing");
            if (Hazards.Count != 2) problems.Add("a trail's segments are not hazards the other side would shy out of");

            Health burning = Subject("TrailFoe", Team.Enemy, new Vector3(0.3f, 0f, 201.6f), Layers.Enemy, collider: true);
            Physics.SyncTransforms();
            trail.Step(0.6f);
            if (burning.Current >= 1000f) problems.Add("an enemy standing on a trail took no damage");

            trail.Step(2.5f);
            if (trail.SegmentCount != 0) problems.Add("trail segments outlived their lifetime");
        }

        private static void CheckVolumes(List<string> problems)
        {
            int wall = 1 << Layers.NetherWall, smoke = 1 << Layers.Smoke;

            if ((Layers.HitMaskFor(Team.Player) & wall) == 0 || (Layers.HitMaskFor(Team.Enemy) & wall) == 0)
                problems.Add("Nether Wall does not stop shots from both sides");
            if ((Layers.HitMaskFor(Team.Player) & smoke) == 0) problems.Add("smoke does not catch the player's shots");
            if ((Layers.HitMaskFor(Team.Enemy) & smoke) != 0) problems.Add("smoke catches enemy shots, which should pass through it");
            if ((Layers.BlockingMask & (wall | smoke)) != 0) problems.Add("a volume blocks movement or pathfinding");
            if ((Layers.SightBlockMask & (wall | smoke)) != 0) problems.Add("a volume blocks sight");

            foreach (int body in new[] { Layers.Player, Layers.Enemy, Layers.Minion })
                if (!Physics.GetIgnoreLayerCollision(Layers.NetherWall, body) || !Physics.GetIgnoreLayerCollision(Layers.Smoke, body))
                    problems.Add("a volume is solid to layer " + body);

            WeaponDefinition gun = FindHitscanGun();
            if (gun == null)
            {
                problems.Add("no hitscan gun without burst, mana cost or splash to test volumes with");
                return;
            }

            ShotRedirectVolume cloud = WorldVolumes.NetherSmoke(new Vector3(0f, 1f, 300f), new Vector3(6f, 4f, 6f), 10f, Color.gray);
            Health inside = Subject("SmokeFoe", Team.Enemy, new Vector3(2f, 0f, 300f), Layers.Enemy, collider: true);
            Physics.SyncTransforms();

            if (!cloud.TryPickTarget(Team.Player, out IDamageable picked) || !ReferenceEquals(picked, inside))
                problems.Add("smoke with one enemy inside did not pick it");
            if (cloud.TryPickTarget(Team.Enemy, out _))
                problems.Add("smoke handed on an enemy's shot");

            Fire(gun, new Vector3(0f, 1f, 290f));
            if (inside.Current >= 1000f) problems.Add("a hitscan shot into smoke did not land on the enemy inside it, 2m off the line of fire");

            ShotRedirectVolume empty = WorldVolumes.NetherSmoke(new Vector3(0f, 1f, 320f), new Vector3(6f, 4f, 6f), 10f, Color.gray);
            Health behindSmoke = Subject("BehindSmoke", Team.Enemy, new Vector3(0f, 0f, 330f), Layers.Enemy, collider: true, colliderSize: 8f);
            Physics.SyncTransforms();
            Fire(gun, new Vector3(0f, 1f, 312f));
            if (behindSmoke.Current >= 1000f)
                problems.Add("a shot through empty smoke did not carry on to the enemy behind it (" + gun.Id + "; smoke "
                             + (empty.TryPickTarget(Team.Player, out IDamageable wrongly) ? "picked " + wrongly.Transform.name : "found nobody")
                             + "; along the line: " + DescribeLine(new Vector3(0f, 1f, 312f), Vector3.forward) + ")");

            WorldVolumes.NetherWall(new Vector3(0f, 1f, 345f), Quaternion.identity, new Vector3(6f, 6f, 0.5f), 10f, Color.magenta);
            Health behindWall = Subject("BehindWall", Team.Enemy, new Vector3(0f, 0f, 350f), Layers.Enemy, collider: true, colliderSize: 8f);
            Physics.SyncTransforms();
            Fire(gun, new Vector3(0f, 1f, 340f));
            if (behindWall.Current < 1000f) problems.Add("a shot passed through a Nether Wall");
        }

        // ---------------------------------------------------------------- 3.5 level services

        private static void CheckLevelServices(List<string> problems)
        {
            WorldClock.Reset();
            DeathRecords.EnsureSubscribed();
            DeathRecords.Clear();

            Health victim = Subject("Fallen", Team.Enemy, new Vector3(5f, 0f, 400f));
            victim.TakeDamage(DamageInfo.Create(100000f, DamageType.True, Team.Player, null));

            var records = new List<DeathRecords.Record>();
            DeathRecords.Collect(records);
            if (records.Count != 1) problems.Add(records.Count + " deaths recorded for one death");
            else if (Vector3.Distance(records[0].Position, new Vector3(5f, 0f, 400f)) > Tolerance) problems.Add("a death was recorded in the wrong place");

            WorldClock.Tick(DeathRecords.Lifetime + 0.5f);
            if (DeathRecords.Count != 0) problems.Add("a death record outlived its lifetime");

            Subject("FallenAgain", Team.Enemy, new Vector3(6f, 0f, 400f))
                .TakeDamage(DamageInfo.Create(100000f, DamageType.True, Team.Player, null));

            int left = -1;
            RoomRuntime entered = null;
            bool enteredRaised = false;
            System.Action<int> onLeaving = floor => left = floor;
            System.Action<RoomRuntime> onEntered = room => { entered = room; enteredRaised = true; };

            LevelEvents.FloorLeaving += onLeaving;
            LevelEvents.FloorEntered += onEntered;
            LevelEvents.RaiseFloorLeaving(3);
            LevelEvents.RaiseFloorEntered(null);
            LevelEvents.FloorLeaving -= onLeaving;
            LevelEvents.FloorEntered -= onEntered;

            if (left != 3) problems.Add("the floor-leaving hook did not report the floor being left");
            if (!enteredRaised || entered != null) problems.Add("the floor-entered hook did not fire");
            if (DeathRecords.Count != 0) problems.Add("death records survived leaving the floor");

            int seedsChecked = 0;
            for (int seed = 1; seed <= 25; seed++)
            {
                var maze = new MazeLayout(MazeSettings.For(RoomKind.Combat), new Rng(seed));
                var route = new ExitRouteMap(maze);
                seedsChecked++;

                if (route.StepsAt(maze.Exit) != 0)
                {
                    problems.Add("seed " + seed + ": the exit is not zero steps from itself");
                    continue;
                }

                foreach (Vector2Int cell in maze.Chambers())
                {
                    int steps = route.StepsAt(cell);

                    if (steps < 0)
                    {
                        if (!maze.IsSplit(cell.x, cell.y))
                            problems.Add("seed " + seed + ": chamber " + cell + " has no walking route to the exit");
                        continue;
                    }

                    if (steps == 0) continue;

                    if (!route.TryNextCell(cell, out Vector2Int next))
                    {
                        problems.Add("seed " + seed + ": chamber " + cell + " is " + steps + " steps out but leads nowhere");
                        continue;
                    }

                    if (route.StepsAt(next) != steps - 1)
                        problems.Add("seed " + seed + ": the route from " + cell + " does not get closer");
                    if (maze.StyleBetween(cell, next) == DoorStyle.Ledge)
                        problems.Add("seed " + seed + ": the route from " + cell + " climbs a ledge");
                    if (!maze.Connected(cell, next))
                        problems.Add("seed " + seed + ": the route from " + cell + " goes through a wall");
                    if (next != maze.Exit && maze.IsSplit(next.x, next.y))
                        problems.Add("seed " + seed + ": the route from " + cell + " passes through an embrasure");

                    Vector3 toward = (maze.CellCentre(next) - maze.CellCentre(cell)).normalized;
                    Vector3 direction = route.DirectionAt(maze.CellCentre(cell));
                    if (Vector3.Dot(direction, toward) < 0.99f)
                        problems.Add("seed " + seed + ": from " + cell + " the route points " + direction + ", not at the doorway");

                    if (!route.IsFollowing(maze.CellCentre(cell), toward * 7f)
                        || route.IsFollowing(maze.CellCentre(cell), -toward * 7f))
                        problems.Add("seed " + seed + ": following the route from " + cell + " is not told apart from leaving it");
                }

                if (problems.Count > 20) break;
            }

            if (seedsChecked == 0) problems.Add("no mazes were built to check exit routes on");
        }

        // ---------------------------------------------------------------- helpers

        private static Health Subject(string name, Team team, Vector3 position, int layer = -1, bool collider = false,
            float colliderSize = 0f)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (layer >= 0) go.layer = layer;

            if (collider)
            {
                if (colliderSize > 0f)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(colliderSize, colliderSize, 1f);
                    box.center = new Vector3(0f, 1f, 0f);
                }
                else
                {
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.radius = 0.4f;
                    capsule.height = 1.8f;
                    capsule.center = new Vector3(0f, 0.9f, 0f);
                }
            }

            go.AddComponent<CharacterSheet>().SetBaseOverride(Attr.MaxHealth, 1000f);
            go.AddComponent<StatusController>();

            var health = go.AddComponent<Health>();
            health.Team = team;
            health.DestroyOnDeath = false;
            health.ConfigureMaxHealth(1000f);
            return health;
        }

        /// <summary>Edit mode never calls Awake, so health is configured the way the factory's would be.</summary>
        private static EnemyController Spawn(string id, Vector3 position)
        {
            EnemyController enemy = EnemyFactory.Spawn(id, position, 1);
            enemy.Health.DestroyOnDeath = false;
            enemy.Health.ConfigureMaxHealth(1000f);
            return enemy;
        }

        private static bool InRegistry(Transform transform)
        {
            IReadOnlyList<TargetRegistry.Entry> minions = TargetRegistry.Minions;
            for (int i = 0; i < minions.Count; i++)
                if (minions[i].Transform == transform) return true;
            return false;
        }

        private static void Fire(WeaponDefinition gun, Vector3 from)
        {
            var shooter = new GameObject("VolumeShooter");
            shooter.transform.position = from;

            var weapon = shooter.AddComponent<Weapon>();
            weapon.OwnerTeam = Team.Player;
            weapon.Owner = shooter;
            weapon.AimOrigin = shooter.transform;
            weapon.Equip(gun);
            weapon.TryFire();
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

        /// <summary>Every collider a player's shot would meet along a line, nearest first, for a failure message.</summary>
        private static string DescribeLine(Vector3 from, Vector3 direction)
        {
            RaycastHit[] hits = Physics.RaycastAll(from, direction, 60f, Layers.HitMaskFor(Team.Player),
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            if (hits.Length == 0) return "nothing";

            var parts = new string[hits.Length];
            for (int i = 0; i < hits.Length; i++)
                parts[i] = hits[i].collider.name + " on layer " + hits[i].collider.gameObject.layer
                           + " at " + hits[i].distance.ToString("0.0") + "m";
            return string.Join(", ", parts);
        }

        private static WeaponDefinition FindHitscanGun()
        {
            foreach (WeaponDefinition gun in WeaponLibrary.All)
                if (gun.Delivery == DeliveryKind.Hitscan && gun.Mode != FireMode.Burst && gun.ManaPerShot <= 0f
                    && gun.SplashRadius <= 0f && gun.MagazineSize > 1)
                    return gun;
            return null;
        }

        private static bool Approx(float a, float b) => Mathf.Abs(a - b) <= Tolerance;
    }
}
#endif
