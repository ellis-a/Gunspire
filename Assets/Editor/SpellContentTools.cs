#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Checks the designed spells and the effect library they are built from. Each school's roster matches the
    /// design, every spell can be cast on a real player rig without throwing, and each new effect, status and
    /// controller does what its spell needs.
    ///
    /// Nothing here judges numbers: they are all placeholders until play decides them.
    /// </summary>
    public static class SpellContentTools
    {
        private const float Tolerance = 0.05f;

        private static readonly Dictionary<SpellSchool, string[]> Designed = new Dictionary<SpellSchool, string[]>
        {
            {
                SpellSchool.Elemental, new[]
                {
                    "flaming_skull", "ice_lance", "storm_blast", "frost_burn", "hailstorm", "star_comet",
                    "elemental_chaos", "elemental_order", "elemental_form", "ride_the_gale", "burning_feet", "rimeblade"
                }
            },
            {
                SpellSchool.Bestial, new[]
                {
                    "murder", "howl", "blood_scent", "fang_and_claw", "embiggen", "lunge", "vipers_sting", "hunters_mark",
                    "shapeshift_cobra", "shapeshift_stag", "shapeshift_lizard", "bound", "spider_legs", "maul"
                }
            },
            {
                SpellSchool.Abyssal, new[]
                {
                    "gush", "ink_spray", "depth_grasp", "soul_bargain", "drown", "eye_of_epheraxx", "leviathan",
                    "fathomless_gate", "unspeakable_one", "bloodwake", "rapture_of_the_deep", "tentacle"
                }
            },
            {
                SpellSchool.Divination, new[]
                {
                    "smite", "prismatic_chains", "underworld_vial", "divine_assistance", "foretell", "divine_star",
                    "reckoning", "judgement", "consecrate", "ascend", "path_of_light", "punish"
                }
            },
            {
                SpellSchool.Death, new[]
                {
                    "raise_dead", "wither", "bone_shards", "banshee_wail", "corpse_explosion", "desecrate",
                    "stitched_monstrosity", "soul_storm", "apocalypse", "gravewalk", "lich_guise", "gravebite"
                }
            },
            {
                SpellSchool.Psionic, new[]
                {
                    "mind_spike", "phantasmal_mimic", "telekinesis", "ego_fracture", "brain_fog", "intrusive_thoughts",
                    "superego_death", "superid", "assume_identity", "repulse", "force_of_will", "slice"
                }
            },
            {
                SpellSchool.Aetherics, new[]
                {
                    "banish", "reality_shards", "nether_wall", "invisibility", "collapse_space", "flicker", "nether_smoke",
                    "echo", "stop_time", "blink", "rewind", "space_hammer"
                }
            }
        };

        [MenuItem("Gunspire/Verify Spell Content")]
        public static void VerifySpellContent()
        {
            var problems = new List<string>();
            var notes = new List<string>();
            var rigs = new List<PlayerRig>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            bool[,] matrixBefore = ReadCollisionMatrix();

            try
            {
                Layers.ConfigureCollisionMatrix();
                WorldClock.Reset();
                TargetRegistry.Clear();
                Hazards.Clear();
                DeathRecords.EnsureSubscribed();
                DeathRecords.Clear();

                CheckRosters(problems);
                Guard(problems, "the smoke test", () => CheckEverySpellCasts(problems, notes, rigs));
                Guard(problems, "the general effects", () => CheckGeneralEffects(problems, rigs));
                Guard(problems, "the enemy effects", () => CheckEnemyEffects(problems, rigs));
                Guard(problems, "the statuses", () => CheckStatuses(problems, rigs));
                Guard(problems, "the player states", () => CheckPlayerStates(problems, rigs));
                Guard(problems, "the summons and world", () => CheckWorld(problems, rigs));
            }
            finally
            {
                foreach (PlayerRig rig in rigs)
                    if (rig != null) rig.DetachPlayerSystems();

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
                Debug.Log("Spell content: every school's roster, every spell cast, and the effect library behave as specified."
                          + "\n  no problems." + noteText);
                return;
            }

            var report = new StringBuilder("Spell content: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 50; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report + noteText);
        }

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

        // ---------------------------------------------------------------- rosters

        private static void CheckRosters(List<string> problems)
        {
            foreach (KeyValuePair<SpellSchool, string[]> school in Designed)
            {
                var spells = new List<Spell>();
                foreach (string id in school.Value)
                {
                    Spell spell = SpellLibrary.Get(id);
                    if (spell == null) problems.Add(school.Key + ": designed spell \"" + id + "\" is missing");
                    else if (spell.School != school.Key) problems.Add(id + " belongs to " + spell.School + ", not " + school.Key);
                    else spells.Add(spell);
                }

                ExpectCount(problems, school.Key, spells, SpellSlot.Cast, Rarity.Common, 3);
                ExpectCount(problems, school.Key, spells, SpellSlot.Cast, Rarity.Uncommon, 3);
                ExpectCount(problems, school.Key, spells, SpellSlot.Cast, Rarity.Rare, 2);
                ExpectCount(problems, school.Key, spells, SpellSlot.Cast, Rarity.Mythic, 1);
                ExpectCount(problems, school.Key, spells, SpellSlot.Movement, Rarity.Common, 1);
                ExpectCount(problems, school.Key, spells, SpellSlot.Movement, Rarity.Rare, 1);
                ExpectCount(problems, school.Key, spells, SpellSlot.Melee, Rarity.Uncommon, 1);

                foreach (Spell spell in spells)
                {
                    if (school.Key == SpellSchool.Abyssal && spell.Slot != SpellSlot.Movement && spell.HealthCost <= 0f)
                        problems.Add(spell.Id + " is Abyssal but costs no health");
                }
            }

            float aethericsMana = AverageCastMana(SpellSchool.Aetherics);
            foreach (SpellSchool school in new[] { SpellSchool.Elemental, SpellSchool.Bestial, SpellSchool.Divination, SpellSchool.Psionic, SpellSchool.Death })
                if (AverageCastMana(school) >= aethericsMana)
                    problems.Add("Aetherics cast spells average " + aethericsMana.ToString("0") + " mana, no more than " + school + "'s "
                                 + AverageCastMana(school).ToString("0") + "; the school is meant to cost more");

            int soulSpenders = 0;
            foreach (string id in Designed[SpellSchool.Death])
            {
                Spell spell = SpellLibrary.Get(id);
                if (spell != null && (spell.SoulCost > 0 || spell.SpendsAllSouls)) soulSpenders++;
            }
            if (soulSpenders < 5) problems.Add("only " + soulSpenders + " Death spells spend souls");

            Spell chaos = SpellLibrary.Get("elemental_chaos");
            if (chaos != null && !(chaos.OnCast.Count == 1 && chaos.OnCast[0] is CastRandomSpellEffect random && random.AllowRepeats))
                problems.Add("Elemental Chaos does not cast random spells with repeats allowed, as decided");

            Spell blink = SpellLibrary.Get("blink");
            if (blink != null && (blink.OnCast.Count == 0 || !(blink.OnCast[0] is SelectWalkableLandingEffect)))
                problems.Add("Blink still stops at walls: its chain does not start with a walkable landing");

            Spell spider = SpellLibrary.Get("spider_legs");
            if (spider != null && spider.DisplayName != "Spider Gravity") problems.Add("spider_legs is still called " + spider.DisplayName);

            Spell form = SpellLibrary.Get("elemental_form");
            if (form != null && (!form.IsStance || form.Stance.Modes.Count != 3 || form.Stance.Modes[0].Name != "Fire"))
                problems.Add("Elemental Form is not a three-mode stance starting in fire");

            foreach (string id in new[] { "shapeshift_cobra", "shapeshift_stag", "shapeshift_lizard" })
            {
                Spell shift = SpellLibrary.Get(id);
                if (shift != null && shift.VariantGroup != "shapeshift") problems.Add(id + " is not in the shapeshift variant group");
            }
        }

        private static void ExpectCount(List<string> problems, SpellSchool school, List<Spell> spells, SpellSlot slot, Rarity rarity, int expected)
        {
            var groups = new HashSet<string>();
            int count = 0;
            foreach (Spell spell in spells)
            {
                if (spell.Slot != slot || spell.Rarity != rarity) continue;
                if (!string.IsNullOrEmpty(spell.VariantGroup) && !groups.Add(spell.VariantGroup)) continue;
                count++;
            }

            if (count != expected)
                problems.Add(school + " has " + count + " " + rarity + " " + slot + " spells, not " + expected);
        }

        private static float AverageCastMana(SpellSchool school)
        {
            float total = 0f;
            int count = 0;
            foreach (string id in Designed[school])
            {
                Spell spell = SpellLibrary.Get(id);
                if (spell == null || spell.Slot != SpellSlot.Cast || spell.IsStance || spell.IsSustained) continue;
                total += spell.ManaCost;
                count++;
            }
            return count == 0 ? 0f : total / count;
        }

        // ---------------------------------------------------------------- every spell casts

        private static void CheckEverySpellCasts(List<string> problems, List<string> notes, List<PlayerRig> rigs)
        {
            Floor(new Vector3(12000f, -0.5f, 0f), 80f);
            PlayerRig rig = MakeRig(new Vector3(12000f, 0.05f, 0f), rigs);
            rig.Sheet.SetBaseOverride(Attr.MaxMana, 1000f);
            SetMana(rig, 1000f);
            rig.Holster.SetSlot(1, SecondGun(rig.Holster.GetSlot(0)));

            var room = new GameObject("SmokeRoom").AddComponent<RoomRuntime>();
            for (int i = 0; i < 4; i++)
            {
                EnemyController enemy = SpawnEnemy("cultist", new Vector3(11997f + i * 2f, 0.05f, 8f + i));
                room.Register(enemy);
                enemy.Alert();
            }
            Physics.SyncTransforms();

            var refused = new List<string>();
            int cast = 0;

            foreach (Spell spell in SpellLibrary.All)
            {
                try
                {
                    SetMana(rig, 1000f);
                    rig.Health.Heal(rig.Health.Max);
                    rig.Book.ResetCooldowns();

                    bool happened = CastOnce(rig, spell);
                    if (happened) cast++;
                    else refused.Add(spell.Id);
                }
                catch (System.Exception e)
                {
                    problems.Add("casting " + spell.Id + " threw " + e.GetType().Name + ": " + e.Message);
                }
                finally
                {
                    if (rig.Possession != null) rig.Possession.Cancel();
                    WorldClock.Reset();
                    rig.Status.ClearAll();
                    MirrorGun mirror = rig.GetComponent<MirrorGun>();
                    if (mirror != null) mirror.End();
                    if (rig.Motor.IsKinematic) rig.Motor.EndKinematic();
                    rig.Book.ResetBook();
                }
            }

            notes.Add(cast + " of " + SpellLibrary.All.Count + " spells went off on the smoke-test rig; refused there, as allowed: "
                      + (refused.Count == 0 ? "none" : string.Join(", ", refused)));
        }

        private static bool CastOnce(PlayerRig rig, Spell spell)
        {
            switch (spell.Slot)
            {
                case SpellSlot.Movement:
                    rig.Movement.Equip(spell);
                    bool moved = rig.Movement.TryActivate();
                    rig.Movement.Deactivate();
                    return moved;

                case SpellSlot.Melee:
                    rig.CombatInput.EquipMelee(spell);
                    return rig.CombatInput.TryCastMelee() == CastOutcome.Cast;

                default:
                    rig.Book.Bind(spell, 0);
                    if (spell.IsStance) return rig.Book.TryCastSlot(0) == CastOutcome.StanceChanged;
                    if (spell.IsCharged)
                    {
                        rig.Book.PressSlot(0);
                        rig.Book.Tick(spell.Charge.SecondsToFull);
                        return rig.Book.ReleaseSlot(0) == CastOutcome.Cast;
                    }

                    CastOutcome outcome = rig.Book.TryCastSlot(0);
                    if (spell.IsSustained && outcome == CastOutcome.Cast) rig.Book.TryCastSlot(0);
                    return outcome == CastOutcome.Cast;
            }
        }

        // ---------------------------------------------------------------- the general effects

        private static void CheckGeneralEffects(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(13000f, 0f, 0f), rigs);
            AbilityContext ctx = rig.SpellContext;

            // The instant ray takes the first thing along the aim, and stops at walls.
            Health ahead = Subject("RayTarget", Team.Enemy, new Vector3(13000f, 0.7f, 10f), Layers.Enemy, true);
            Physics.SyncTransforms();
            Begin(ctx);
            new InstantRayEffect { Range = 30f, DrawLine = false }.Execute(ctx);
            if (ctx.Targets.Count != 1 || ctx.Targets[0].Transform != ahead.transform) problems.Add("the instant ray did not select the enemy straight ahead");

            Build.Cube(null, "RayWall", new Vector3(13000f, 1.5f, 5f), new Vector3(4f, 4f, 0.5f), MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Physics.SyncTransforms();
            Begin(ctx);
            new InstantRayEffect { Range = 30f, DrawLine = false }.Execute(ctx);
            if (ctx.Targets.Count != 0) problems.Add("the instant ray passed through a wall");

            Begin(ctx);
            new SetDamageTypeEffect { Type = DamageType.Kinetic }.Execute(ctx);
            if (ctx.DamageType != DamageType.Kinetic) problems.Add("setting the damage type mid-chain did nothing");

            Begin(ctx);
            Vector3 origin = ctx.Origin;
            new OriginOffsetEffect { Back = 3f }.Execute(ctx);
            if (Mathf.Abs(Vector3.Dot(ctx.Origin - origin, rig.transform.forward) + 3f) > Tolerance)
                problems.Add("offsetting the origin back three metres moved it " + Vector3.Dot(ctx.Origin - origin, rig.transform.forward));

            // A narrow box: straight ahead in, beside out.
            PlayerRig boxRig = MakeRig(new Vector3(13100f, 0f, 0f), rigs);
            Health inLine = Subject("BoxAhead", Team.Enemy, new Vector3(13100f, 0f, 2.5f), Layers.Enemy, true);
            Health beside = Subject("BoxBeside", Team.Enemy, new Vector3(13101.8f, 0f, 2.5f), Layers.Enemy, true);
            Physics.SyncTransforms();
            Begin(boxRig.SpellContext);
            new SelectBoxEffect { Range = 4.5f, Width = 0.9f, Height = 3f }.Execute(boxRig.SpellContext);
            if (!Selected(boxRig.SpellContext, inLine)) problems.Add("the narrow strike missed the enemy straight ahead");
            if (Selected(boxRig.SpellContext, beside)) problems.Add("the narrow strike caught an enemy almost two metres to the side");

            // A pull moves an enemy toward the point.
            EnemyController pulled = SpawnEnemy("cultist", new Vector3(13200f, 0f, 5f));
            Physics.SyncTransforms();
            Begin(ctx);
            ctx.Targets.Add(pulled.Health);
            ctx.Point = new Vector3(13200f, 0f, 0f);
            new PullTowardPointEffect { Speed = 12f }.Execute(ctx);
            if (Vector3.Dot(pulled.ExternalVelocity, Vector3.back) <= 0f) problems.Add("pulling toward a point did not move the enemy toward it");

            // Casting random spells: two, from the right pool, never itself.
            Spell chaos = SpellLibrary.Get("elemental_chaos");
            if (chaos != null)
            {
                SetMana(rig, rig.Mana.Max);
                rig.Book.Bind(chaos, 0);
                rig.Book.TryCastSlot(0);
                if (CastRandomSpellEffect.LastCast.Count != 2) problems.Add("Elemental Chaos cast " + CastRandomSpellEffect.LastCast.Count + " spells, not 2");
                foreach (string id in CastRandomSpellEffect.LastCast)
                {
                    Spell child = SpellLibrary.Get(id);
                    if (id == chaos.Id || child == null || child.School != SpellSchool.Elemental || child.Rarity != Rarity.Uncommon)
                        problems.Add("Elemental Chaos cast " + id + ", which is not another uncommon Elemental spell");
                }
                rig.Book.Bind(null, 0);
            }

            // A status split between everything caught.
            Health splitA = Subject("SplitA", Team.Enemy, new Vector3(13300f, 0f, 0f));
            Health splitB = Subject("SplitB", Team.Enemy, new Vector3(13302f, 0f, 0f));
            Begin(ctx);
            ctx.Targets.Add(splitA);
            ctx.Targets.Add(splitB);
            new SplitStatusEffect { Status = StatusId.Burn, TotalMagnitude = 80f }.Execute(ctx);
            float expectedShare = 80f * ctx.StatusPower / 2f;
            ActiveStatus burnA = splitA.GetComponent<StatusController>().Find(StatusId.Burn);
            if (burnA == null || Mathf.Abs(burnA.Magnitude - expectedShare) > Tolerance)
                problems.Add("a burn of 80 split between two enemies gave " + (burnA != null ? burnA.Magnitude.ToString("0.0") : "none") + ", not " + expectedShare.ToString("0.0"));

            // Removing a random debuff never takes a buff.
            rig.Status.Apply(StatusLibrary.Weaken(10f), null, Team.Enemy);
            rig.Status.Apply(StatusLibrary.Haste(10f), rig.gameObject, Team.Player);
            Begin(ctx);
            new RemoveRandomDebuffEffect().Execute(ctx);
            if (rig.Status.Has(StatusId.Weaken) || !rig.Status.Has(StatusId.Haste)) problems.Add("removing a random debuff did not take the debuff and leave the buff");
            rig.Status.ClearAll();

            // Stopping time.
            Begin(ctx);
            new StopTimeEffect { Seconds = 6f }.Execute(ctx);
            if (!WorldClock.IsStopped) problems.Add("Stop Time did not stop the world");
            WorldClock.Tick(6.2f);
            if (WorldClock.IsStopped) problems.Add("Stop Time was still running after its six seconds");
            WorldClock.Reset();

            // A projectile carrying a trail lays it.
            Begin(ctx);
            new SpawnProjectileEffect { Damage = 0f, Speed = 10f, Lifetime = 5f, Trail = new TrailProfile { Radius = 1f } }.Execute(ctx);
            TrailEmitter trail = Object.FindAnyObjectByType<TrailEmitter>();
            if (trail == null || trail.Carrier == null) problems.Add("a projectile with a trail laid no trail behind it");
            else
            {
                trail.Step(0.1f);
                if (trail.SegmentCount == 0) problems.Add("a projectile's trail laid no segment");
            }

            // The companion's copy of a strike.
            PlayerRig beastRig = MakeRig(new Vector3(13400f, 0f, 0f), rigs);
            beastRig.Book.Bind(TestSpell("beast_test", SpellSchool.Bestial), 0);
            MinionController companion = beastRig.Masteries.Get<BeastMastery>().Companion;
            if (companion == null) problems.Add("setup: a Bestial spell summoned no companion for the companion strike");
            else
            {
                companion.transform.position = new Vector3(13410f, 0f, 0f);
                Health nearCompanion = Subject("NearCompanion", Team.Enemy, new Vector3(13410f, 0f, 2f), Layers.Enemy, true);
                Physics.SyncTransforms();

                Begin(beastRig.SpellContext);
                new FromCompanionEffect
                {
                    Body =
                    {
                        new SelectConeEffect { Range = 4f, HalfAngle = 90f, RequireLineOfSight = false },
                        new DealDamageEffect { Amount = 10f }
                    }
                }.Execute(beastRig.SpellContext);

                if (nearCompanion.Current >= 1000f) problems.Add("a strike cast from the companion did not hit the enemy beside it");
            }
        }

        // ---------------------------------------------------------------- enemies

        private static void CheckEnemyEffects(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(14000f, 0f, 0f), rigs);
            AbilityContext ctx = rig.SpellContext;
            var room = new GameObject("EnemyRoom").AddComponent<RoomRuntime>();

            // Banish: shared out, elites resisting half, still in the room, back after their time.
            EnemyController one = SpawnEnemy("cultist", new Vector3(14000f, 0f, 10f));
            EnemyController two = SpawnEnemy("cultist", new Vector3(14003f, 0f, 10f));
            two.Health.IsElite = true;
            room.Register(one);
            room.Register(two);

            Begin(ctx);
            ctx.Targets.Add(one.Health);
            ctx.Targets.Add(two.Health);
            new BanishEffect { TotalSeconds = 12f, EliteResistance = 0.5f }.Execute(ctx);

            BanishTimer oneTimer = one.GetComponent<BanishTimer>();
            BanishTimer twoTimer = two.GetComponent<BanishTimer>();
            if (!one.IsHidden || !two.IsHidden) problems.Add("banished enemies were not taken out of reality");
            if (oneTimer == null || Mathf.Abs(oneTimer.Remaining - 6f) > Tolerance) problems.Add("two enemies sharing 12 seconds of banishment did not get 6 each");
            if (twoTimer == null || Mathf.Abs(twoTimer.Remaining - 3f) > Tolerance) problems.Add("a banished elite did not resist half its share");
            if (!room.Contains(one) || !room.Contains(two)) problems.Add("banished enemies left their room's count, which would clear it");

            if (oneTimer != null) oneTimer.Step(6.1f);
            if (one.IsHidden) problems.Add("a banished enemy did not come back when its time ran out");

            // Splitting: halves, both marked, the copy in the room, never twice.
            EnemyController original = SpawnEnemy("cultist", new Vector3(14010f, 0f, 10f));
            room.Register(original);
            original.Status.Apply(StatusLibrary.Deathmark(5f), rig.gameObject, Team.Player);

            Begin(ctx);
            ctx.Targets.Add(original.Health);
            SplitEnemyEffect.LastCopy = null;
            new SplitEnemyEffect().Execute(ctx);
            EnemyController copy = SplitEnemyEffect.LastCopy;

            if (copy == null) problems.Add("splitting an enemy made no copy");
            else
            {
                if (Mathf.Abs(original.Health.Max - 500f) > 1f || Mathf.Abs(copy.Health.Max - 500f) > 1f)
                    problems.Add("a split left halves of " + original.Health.Max + " and " + copy.Health.Max + " maximum health, not 500 each");
                if (!room.Contains(copy)) problems.Add("a split copy was not registered with its room, which could clear with it alive");
                if (original.GetComponent<SplitMarker>() == null || copy.GetComponent<SplitMarker>() == null) problems.Add("the halves of a split were not both marked");
                if (copy.Status.Has(StatusId.Deathmark)) problems.Add("a split copied the death mark, which would turn one mark into two executes");
                if (!copy.Status.IsSilenced || !copy.Status.IsDisarmed) problems.Add("a split copy was not silenced and disarmed");

                SplitEnemyEffect.LastCopy = null;
                Begin(ctx);
                ctx.Targets.Add(copy.Health);
                new SplitEnemyEffect().Execute(ctx);
                if (SplitEnemyEffect.LastCopy != null) problems.Add("a split enemy was split again");
            }

            // Tethers: kinetic passes on as psychic, psychic never passes back.
            Health tetherA = Subject("TetherA", Team.Enemy, new Vector3(14020f, 0f, 10f), Layers.Enemy, true);
            Health tetherB = Subject("TetherB", Team.Enemy, new Vector3(14022f, 0f, 10f), Layers.Enemy, true);
            Physics.SyncTransforms();
            Begin(ctx);
            ctx.Targets.Add(tetherA);
            if (!new TetherEffect().Execute(ctx)) problems.Add("Prismatic Chains found no second enemy two metres away");
            else
            {
                tetherA.TakeDamage(DamageInfo.Create(20f, DamageType.Kinetic, Team.Player, rig.gameObject));
                if (Mathf.Abs(tetherB.Current - 980f) > Tolerance) problems.Add("kinetic damage to a chained enemy did not pass to its partner");

                float aBefore = tetherA.Current;
                tetherB.TakeDamage(DamageInfo.Create(20f, DamageType.Psychic, Team.Player, rig.gameObject));
                if (!Approx(tetherA.Current, aBefore)) problems.Add("psychic damage passed along a chain, which lets two enemies bounce damage forever");
            }

            Health loner = Subject("Loner", Team.Enemy, new Vector3(14500f, 0f, 10f), Layers.Enemy, true);
            Physics.SyncTransforms();
            Begin(ctx);
            ctx.Targets.Add(loner);
            if (new TetherEffect().Execute(ctx)) problems.Add("Prismatic Chains was cast with nobody to chain to; it should refuse");

            // Corpses.
            DeathRecords.Clear();
            Begin(ctx);
            if (new ChainCorpsesEffect().Execute(ctx)) problems.Add("Corpse Explosion went off with no corpses; it should refuse");

            Subject("Corpse", Team.Enemy, new Vector3(14030f, 0f, 8f)).Kill();
            Health nearCorpse = Subject("NearCorpse", Team.Enemy, new Vector3(14031f, 0f, 8f), Layers.Enemy, true);
            Physics.SyncTransforms();
            Begin(ctx);
            ctx.Forward = (new Vector3(14030f, 0.5f, 8f) - ctx.Origin).normalized;
            if (!new ChainCorpsesEffect { FirstRange = 60f }.Execute(ctx) || nearCorpse.Current >= 1000f)
                problems.Add("Corpse Explosion did not explode a fresh corpse beside a living enemy");

            // Reflects.
            Projectile incoming = Projectile.Create(rig.transform.position + new Vector3(0f, 0.9f, 2f), Vector3.back, Color.red, 0.1f);
            incoming.OwnerTeam = Team.Enemy;
            incoming.Lifetime = 100f;
            incoming.Launch();

            Begin(ctx);
            new ReflectProjectilesEffect { SpendsPsi = true }.Execute(ctx);
            if (incoming.OwnerTeam != Team.Enemy) problems.Add("a reflect that costs psi went off with no psi charge");

            rig.Book.Bind(TestSpell("psi_test", SpellSchool.Psionic), 0);
            PsiBladesMastery psi = rig.Masteries.Get<PsiBladesMastery>();
            psi.AddBonus(1f);
            Begin(ctx);
            new ReflectProjectilesEffect { SpendsPsi = true }.Execute(ctx);
            if (incoming.OwnerTeam != Team.Player || Vector3.Dot(incoming.transform.forward, Vector3.forward) <= 0f)
                problems.Add("Force of Will did not send an enemy projectile back the way it came");
            if (psi.Charge > Tolerance) problems.Add("reflecting a projectile did not spend the psi charge");

            Begin(ctx);
            float charge = psi.Charge;
            psi.AddBonus(1f);
            new ReflectProjectilesEffect { SpendsPsi = true }.Execute(ctx);
            if (psi.Charge < charge + 1f - Tolerance) problems.Add("a reflect with nothing to reflect still spent a psi charge");

            // Taking over the last enemy in a room is refused.
            var lastRoom = new GameObject("LastRoom").AddComponent<RoomRuntime>();
            EnemyController last = SpawnEnemy("cultist", new Vector3(14040f, 0f, 10f));
            lastRoom.Register(last);
            Begin(ctx);
            ctx.Targets.Add(last.Health);
            if (new PossessEffect().Execute(ctx)) problems.Add("Assume Identity took the last enemy in the room");
            rig.Possession.Cancel();
        }

        // ---------------------------------------------------------------- statuses

        private static void CheckStatuses(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(15000f, 0f, 0f), rigs);

            // Plague: a plagued enemy that dies rises as your zombie and passes it on.
            Health plagued = Subject("Plagued", Team.Enemy, new Vector3(15000f, 0f, 10f), Layers.Enemy, true);
            Health neighbour = Subject("PlagueNeighbour", Team.Enemy, new Vector3(15002f, 0f, 10f), Layers.Enemy, true);
            Physics.SyncTransforms();

            int zombies = MinionController.CountOf(PlagueStatus.ZombieId);
            plagued.GetComponent<StatusController>().Apply(StatusLibrary.Plague(), rig.gameObject, Team.Player);
            plagued.Kill();
            if (MinionController.CountOf(PlagueStatus.ZombieId) != zombies + 1) problems.Add("a plagued enemy that died did not rise as a plague zombie");
            if (!neighbour.GetComponent<StatusController>().Has(StatusId.Plague)) problems.Add("a plagued death did not pass the plague to the enemy beside it");

            Health cured = Subject("PlagueCured", Team.Enemy, new Vector3(15020f, 0f, 10f));
            StatusController curedStatus = cured.GetComponent<StatusController>();
            curedStatus.Apply(StatusLibrary.Plague(1f), rig.gameObject, Team.Player);
            zombies = MinionController.CountOf(PlagueStatus.ZombieId);
            curedStatus.Tick(2f);
            if (MinionController.CountOf(PlagueStatus.ZombieId) != zombies) problems.Add("a plague that simply wore off raised a zombie");

            // Torment.
            Health tormented = Subject("Tormented", Team.Enemy, new Vector3(15030f, 0f, 10f));
            var types = new List<DamageType>();
            tormented.Damaged += (info, amount) => types.Add(info.Type);
            StatusController tormentStatus = tormented.GetComponent<StatusController>();
            tormentStatus.Apply(StatusLibrary.Torment(6f, 1, 8f), rig.gameObject, Team.Player);
            tormentStatus.Tick(0.6f);
            if (!types.Contains(DamageType.Psychic)) problems.Add("Torment dealt no psychic damage over time");

            // Withered finishes below its threshold, elites included, and never the player.
            foreach (bool elite in new[] { false, true })
            {
                Health withering = Subject(elite ? "WitheredElite" : "Withered", Team.Enemy, new Vector3(15040f + (elite ? 3f : 0f), 0f, 10f));
                withering.IsElite = elite;
                withering.SetCurrent(80f);
                withering.GetComponent<StatusController>().Apply(StatusLibrary.Withered(8f, 0.1f), rig.gameObject, Team.Player);
                if (withering.IsAlive) problems.Add("a " + (elite ? "withered elite" : "withered enemy") + " at 8% health did not die");
            }

            rig.Health.SetCurrent(5f);
            rig.Status.Apply(StatusLibrary.Withered(8f, 0.5f), null, Team.Enemy);
            rig.Status.Tick(0.2f);
            if (!rig.Health.IsAlive) problems.Add("Withered killed the player");
            rig.Status.ClearAll();
            rig.Health.Heal(rig.Health.Max);

            // Foretold dodges the next direct hit, and refunds the spell that asks.
            Spell foretell = SpellLibrary.Get("foretell");
            rig.Book.Bind(foretell, 0);
            rig.Book.TryCastSlot(0);
            float cooldown = rig.Book.GetCooldown(0);
            rig.Book.Tick(0f);

            DamageInfo tick = DamageInfo.Create(5f, DamageType.Energy, Team.Enemy, null);
            tick.Origin = DamageOrigin.StatusTick;
            rig.Health.TakeDamage(tick);
            if (!rig.Status.Has(StatusId.Foretold)) problems.Add("a status tick used up Foretell");

            float health = rig.Health.Current;
            rig.Health.TakeDamage(DamageInfo.Create(20f, DamageType.Kinetic, Team.Enemy, null));
            if (!Approx(rig.Health.Current, health)) problems.Add("Foretell did not dodge the next direct hit");
            if (rig.Status.Has(StatusId.Foretold)) problems.Add("Foretell was not used up by the hit it dodged");
            if (foretell != null && rig.Book.GetCooldown(0) > cooldown - foretell.DodgeCooldownRefund + Tolerance)
                problems.Add("dodging with Foretell did not shorten its cooldown");

            rig.Health.TakeDamage(DamageInfo.Create(20f, DamageType.Kinetic, Team.Enemy, null));
            if (rig.Health.Current >= health) problems.Add("a second hit after Foretell was also dodged");
            rig.Book.ResetBook();

            // Phased: unseen, untargetable and immune, until it ends.
            rig.Status.Apply(StatusLibrary.Phased(1f), rig.gameObject, Team.Player);
            if (!rig.Concealment.Untargetable || !rig.Health.IsInvulnerable) problems.Add("Flicker's phase did not make the player untargetable and immune");
            rig.Status.Remove(StatusId.Phased);
            if (rig.Concealment.Untargetable) problems.Add("the player stayed untargetable after Flicker's phase ended");

            // Unleashed: the gun fires itself, never runs dry and cannot be swapped.
            rig.Status.Apply(StatusLibrary.Unleashed(8f), rig.gameObject, Team.Player);
            if (!rig.AutoFire.Active || !rig.Weapon.InfiniteAmmo || !rig.Holster.SwapLocked) problems.Add("Superid did not start auto-fire, infinite ammo and the swap lock");
            rig.Status.Remove(StatusId.Unleashed);
            if (rig.AutoFire.Active || rig.Weapon.InfiniteAmmo || rig.Holster.SwapLocked) problems.Add("Superid's effects outlasted it");

            float dealt = rig.Sheet.Get(Attr.DamageDealt);
            rig.Status.Apply(StatusLibrary.Empowered(8f, 0.25f), rig.gameObject, Team.Player);
            if (rig.Sheet.Get(Attr.DamageDealt) <= dealt) problems.Add("Empowered did not raise damage dealt");
            rig.Status.ClearAll();
        }

        // ---------------------------------------------------------------- player states

        private static void CheckPlayerStates(List<string> problems, List<PlayerRig> rigs)
        {
            // Elemental Form: fire scorches what is near, ice steadies, storm quickens, and none of it outlasts the stance.
            PlayerRig rig = MakeRig(new Vector3(16000f, 0f, 0f), rigs);
            Spell form = SpellLibrary.Get("elemental_form");
            Health scorched = Subject("Scorched", Team.Enemy, new Vector3(16001.5f, 0f, 0f), Layers.Enemy, true);
            Physics.SyncTransforms();

            float spread = rig.Sheet.Get(Attr.Spread);
            float speed = rig.Sheet.Get(Attr.MoveSpeed);

            rig.Book.Bind(form, 0);
            rig.Book.Tick(0.6f);
            if (scorched.Current >= 1000f) problems.Add("Elemental Form's fire did not scorch an enemy beside the player");

            rig.Book.TryCastSlot(0);
            if (rig.Sheet.Get(Attr.Spread) >= spread) problems.Add("Elemental Form's ice did not steady the player's aim");

            rig.Book.TryCastSlot(0);
            if (rig.Sheet.Get(Attr.Spread) < spread - Tolerance) problems.Add("leaving ice form kept its steadier aim");
            if (rig.Sheet.Get(Attr.MoveSpeed) <= speed) problems.Add("Elemental Form's storm did not quicken the player");

            rig.Book.Bind(null, 0);
            if (!Approx(rig.Sheet.Get(Attr.MoveSpeed), speed)) problems.Add("unbinding Elemental Form kept storm form's speed");

            // Swimming: no falling, and rising on command.
            PlayerRig swimmer = MakeRig(new Vector3(16100f, 400f, 0f), rigs);
            swimmer.Motor.InputEnabled = false;
            swimmer.Motor.Buoyant = true;
            Run(swimmer.Motor, 0.5f);
            if (swimmer.Motor.Velocity.y < -0.5f) problems.Add("a buoyant player still fell (" + swimmer.Motor.Velocity.y.ToString("0.0") + " m/s)");
            swimmer.Motor.RiseOverride = 1f;
            Run(swimmer.Motor, 0.5f);
            if (swimmer.Motor.Velocity.y < 1f) problems.Add("a buoyant player did not rise when asked");
            swimmer.Motor.RiseOverride = 0f;
            swimmer.Motor.Buoyant = false;

            // Repulse: dashing back the way you came.
            PlayerRig repulsed = MakeRig(new Vector3(16200f, 400f, 0f), rigs);
            repulsed.Motor.AddImpulse(Vector3.forward * 8f);
            Begin(repulsed.SpellContext);
            if (!new RepulseEffect().Execute(repulsed.SpellContext)) problems.Add("Repulse refused with a dash charge available");
            else
            {
                Run(repulsed.Motor, 0.05f);
                if (Vector3.Dot(repulsed.Motor.Velocity, Vector3.forward) >= 0f) problems.Add("Repulse did not send the player back the way they came");
            }

            // Burning Feet's trail follows the toggle.
            PlayerRig runner = MakeRig(new Vector3(16300f, 0f, 0f), rigs);
            Spell feet = SpellLibrary.Get("burning_feet");
            if (feet != null)
            {
                int trailsBefore = Object.FindObjectsByType<TrailEmitter>(FindObjectsSortMode.None).Length;
                runner.Movement.Equip(feet);
                runner.Movement.TryActivate();
                TrailEmitter[] trails = Object.FindObjectsByType<TrailEmitter>(FindObjectsSortMode.None);
                if (trails.Length != trailsBefore + 1) problems.Add("Burning Feet laid no trail when switched on");
                runner.Movement.Deactivate();
                foreach (TrailEmitter trail in trails)
                    if (trail.Carrier == runner.transform && trail.IsEmitting) problems.Add("Burning Feet's trail kept going after it was switched off");
            }

            // Divine Assistance: the other gun fires beside you whenever you fire, with no swapping.
            PlayerRig gunner = MakeRig(new Vector3(16400f, 0f, 0f), rigs);
            WeaponDefinition first = gunner.Holster.GetSlot(0);
            WeaponDefinition second = SecondGun(first);
            Begin(gunner.SpellContext);
            if (new MirrorGunEffect().Execute(gunner.SpellContext)) problems.Add("Divine Assistance was cast with an empty second hand");

            gunner.Holster.SetSlot(1, second);
            gunner.Holster.SetActive(0);
            Begin(gunner.SpellContext);
            if (!new MirrorGunEffect().Execute(gunner.SpellContext)) problems.Add("Divine Assistance refused with a gun in the other hand");
            else
            {
                MirrorGun mirror = gunner.GetComponent<MirrorGun>();
                if (mirror.Phantom.Weapon.Definition != second) problems.Add("Divine Assistance summoned the gun in hand, not the other one");
                if (!gunner.Holster.SwapLocked) problems.Add("weapons could still be swapped during Divine Assistance");

                gunner.Weapon.TryFire();
                if (mirror.Shots != 1) problems.Add("the mirrored gun did not fire when the player fired");

                mirror.End();
                if (gunner.Holster.SwapLocked) problems.Add("the swap lock outlasted Divine Assistance");
            }

            // Shapeshift: into the lizard, bite, and back out on a spell key.
            PlayerRig shifter = MakeRig(new Vector3(16500f, 0f, 0f), rigs);
            Floor(new Vector3(16500f, -0.5f, 0f), 30f);
            Health prey = Subject("Prey", Team.Enemy, new Vector3(16500f, 0f, 2.2f), Layers.Enemy, true);
            Physics.SyncTransforms();

            Begin(shifter.SpellContext);
            if (!new ShapeshiftEffect { Form = AnimalKind.TyrantLizard }.Execute(shifter.SpellContext))
                problems.Add("Shapeshift did not take the player into the lizard");
            else
            {
                AnimalForm lizard = shifter.Possession.Current as AnimalForm;
                if (lizard == null) problems.Add("Shapeshift possessed something that is not an animal form");
                else
                {
                    lizard.Act(0);
                    if (prey.Current >= 1000f) problems.Add("the lizard's bite did not hurt the enemy in front of it");

                    float playerHealth = shifter.Health.Current;
                    lizard.Health.TakeDamage(DamageInfo.Create(10f, DamageType.Kinetic, Team.Enemy, null));
                    if (Mathf.Abs(shifter.Health.Current - (playerHealth - 10f)) > Tolerance) problems.Add("damage to the animal form was not the player's");

                    shifter.Possession.Tick(0.02f, new PossessionInput { ActionsPressed = 1 << 2 });
                    if (shifter.Possession.IsBusy || shifter.ControlsSuppressed) problems.Add("a spell key did not end Shapeshift and hand back the controls");
                    if (lizard != null) problems.Add("the animal form was left behind after Shapeshift ended");
                }
            }

            // Rewind: back to where and how you were.
            PlayerRig rewinder = MakeRig(new Vector3(16600f, 0f, 0f), rigs);
            Vector3 start = rewinder.transform.position;
            float startHealth = rewinder.Health.Current;
            for (int i = 0; i < 30; i++)
            {
                rewinder.Rewind.Record(0.1f);
                LandingCheck.Place(rewinder.transform, rewinder.transform.position + Vector3.forward * 0.3f);
                if (i == 15) rewinder.Health.Drain(40f);
            }

            Begin(rewinder.SpellContext);
            if (!new RewindEffect().Execute(rewinder.SpellContext)) problems.Add("Rewind refused with three seconds of history");
            else
            {
                RewindPlayback playback = rewinder.GetComponent<RewindPlayback>();
                RewindRecorder.Snapshot oldest = rewinder.Rewind[0];
                playback.Step(1f);

                if ((rewinder.transform.position - oldest.Position).sqrMagnitude > 0.05f) problems.Add("Rewind did not land where the oldest snapshot was");
                if (!Approx(rewinder.Health.Current, oldest.Health)) problems.Add("Rewind did not restore health to exactly what it was");
                if (oldest.Health < startHealth - Tolerance) problems.Add("setup: the oldest rewind snapshot was taken after the drain");
                if (rewinder.Motor.IsKinematic) problems.Add("the controller stayed off after Rewind landed");
            }

            // Gravewalk: through enemies, weakening them, then solid again.
            PlayerRig shade = MakeRig(new Vector3(16700f, 0f, 0f), rigs);
            Health passed = Subject("Passed", Team.Enemy, new Vector3(16700f, 0f, 0.5f), Layers.Enemy, true);
            Physics.SyncTransforms();
            Begin(shade.SpellContext);
            new GravewalkEffect().Execute(shade.SpellContext);
            if (!shade.Motor.PassesThroughEnemies) problems.Add("Gravewalk did not let the player pass through enemies");
            ShadeLunge lunge = shade.GetComponent<ShadeLunge>();
            lunge.Step(0.01f);
            if (!passed.GetComponent<StatusController>().Has(StatusId.Weaken)) problems.Add("Gravewalk did not weaken an enemy it passed through");
            lunge.Step(1f);
            if (shade.Motor.PassesThroughEnemies) problems.Add("the player still passed through enemies after Gravewalk ended");
        }

        // ---------------------------------------------------------------- summons and the world

        private static void CheckWorld(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(17000f, 0f, 0f), rigs);
            AbilityContext ctx = rig.SpellContext;

            // One monstrosity at a time.
            Begin(ctx);
            if (!new SummonMinionEffect { MinionId = "monstrosity", OneAtATime = true }.Execute(ctx)) problems.Add("the first Stitched Monstrosity was refused");
            if (new SummonMinionEffect { MinionId = "monstrosity", OneAtATime = true }.Execute(ctx)) problems.Add("a second Stitched Monstrosity was summoned while one was out");

            // Smite: the next round calls a bolt, and only that one.
            PlayerRig smiter = MakeRig(new Vector3(17100f, 0f, 0f), rigs);
            WeaponDefinition hitscan = FindHitscanGun();
            smiter.Holster.SetSlot(0, hitscan);
            Health smitten = Subject("Smitten", Team.Enemy, new Vector3(17100f, 0.7f, 8f), Layers.Enemy, true);
            Physics.SyncTransforms();

            var hits = new List<DamageInfo>();
            smitten.Damaged += (info, amount) => hits.Add(info);
            Begin(smiter.SpellContext);
            new InfuseBulletsEffect { InfusionId = "smite", Rounds = 1, BoltDamage = 30f, BoltStatuses = { StatusLibrary.Shock(4f) } }.Execute(smiter.SpellContext);
            smiter.Weapon.TryFire();

            if (!hits.Exists(h => h.Origin == DamageOrigin.Spell && h.Type == DamageType.Energy)) problems.Add("Smite's round called down no bolt");
            if (!smitten.GetComponent<StatusController>().Has(StatusId.Shock)) problems.Add("Smite's bolt did not shock its target");

            hits.Clear();
            smiter.Weapon.Equip(hitscan);
            smiter.Weapon.TryFire();
            if (hits.Exists(h => h.Origin == DamageOrigin.Spell)) problems.Add("Smite called a bolt on a second round");

            // Divine Star goes after the healthiest enemy in combat.
            DivineStarEffect.LastTarget = null;
            Begin(ctx);
            if (new DivineStarEffect().Execute(ctx)) problems.Add("Divine Star was cast with nothing in combat");

            EnemyController weak = SpawnEnemy("cultist", new Vector3(17000f, 0f, 12f));
            EnemyController strong = SpawnEnemy("cultist", new Vector3(17004f, 0f, 12f));
            weak.Health.SetCurrent(200f);
            weak.Alert();
            strong.Alert();
            Begin(ctx);
            new DivineStarEffect().Execute(ctx);
            if (DivineStarEffect.LastTarget != strong.transform) problems.Add("Divine Star did not hunt the healthiest enemy in combat");

            // Reality Shards: all the mana spent, spheres grown, fired at enemies in sight.
            SetMana(rig, 100f);
            Begin(ctx);
            new RealityShardsEffect { Spheres = 2, SpheresPerLevel = 0 }.Execute(ctx);
            RealityShards shards = rig.GetComponent<RealityShards>();
            if (rig.Mana.Current > Tolerance) problems.Add("Reality Shards did not spend all the mana");
            shards.Step(0.01f);
            if (shards.Count != 1 || shards.Pending != 1) problems.Add("Reality Shards did not start growing its spheres one at a time");
            for (int i = 0; i < 40; i++) shards.Step(0.1f);
            if (shards.Fired == 0) problems.Add("Reality Shards' spheres never fired at an enemy in sight");

            // The Unspeakable One: tentacles fear, the maw eats.
            Vector3 lair = new Vector3(17200f, 0f, 0f);
            Health lurker = Subject("Lurker", Team.Enemy, lair + new Vector3(4f, 0f, 0f), Layers.Enemy, true);
            Health morsel = Subject("Morsel", Team.Enemy, lair + new Vector3(0.5f, 0f, 0f), Layers.Enemy, true);
            Physics.SyncTransforms();

            KrakenSummon kraken = KrakenSummon.Spawn(lair, 10f, 14f, 20f, Team.Player, rig.gameObject, Color.cyan);
            for (int i = 0; i < 6; i++) kraken.Tentacle();
            if (kraken.Tentacles == 0) problems.Add("the Unspeakable One raised no tentacles with enemies nearby");
            if (!lurker.GetComponent<StatusController>().IsFeared && !morsel.GetComponent<StatusController>().IsFeared)
                problems.Add("the Unspeakable One's tentacles feared nobody");

            kraken.Maw();
            if (morsel.IsAlive) problems.Add("the Unspeakable One's maw did not eat the enemy standing in it");

            // Soul Storm lasts while you kill.
            SoulStorm storm = SoulStorm.Spawn(rig.transform, 5f, 2.5f, 9f, 10f, DamageType.Necrotic, Team.Player, rig.gameObject, Color.green);
            float remaining = storm.Remaining;
            Subject("StormKill", Team.Enemy, new Vector3(17300f, 0f, 0f)).Kill();
            if (storm.Remaining < remaining + 2.4f) problems.Add("a kill during Soul Storm did not extend it");

            // Desecrate: a shooter standing in it adds necrotic damage.
            Health defiled = Subject("Defiled", Team.Enemy, new Vector3(17400f, 0f, 20f));
            var necrotic = new List<float>();
            defiled.Damaged += (info, amount) => { if (info.Type == DamageType.Necrotic) necrotic.Add(amount); };
            DesecratedGround.Spawn(rig.transform.position, 5f, 10f, 6f, rig.Weapon, Color.green);
            rig.Weapon.ReportHit(defiled, DamageInfo.Create(1f, DamageType.Kinetic, Team.Player, rig.gameObject),
                defiled.transform.position, Vector3.up, Vector3.forward, null);
            if (necrotic.Count == 0) problems.Add("a bullet fired from desecrated ground dealt no necrotic bonus");

            // A whirlpool drags enemies in, and its centre is a hazard.
            int hazards = Hazards.Count;
            EnemyController swirled = SpawnEnemy("cultist", new Vector3(17500f, 0f, 4f));
            Physics.SyncTransforms();
            Whirlpool pool = Whirlpool.Spawn(new Vector3(17500f, 0f, 0f), 6f, 6f, 5f, 0f, DamageType.Kinetic, Team.Player, rig.gameObject, Color.blue);
            pool.Step(0.3f);
            if (Vector3.Dot(swirled.ExternalVelocity, Vector3.back) <= 0f) problems.Add("Fathomless Gate did not drag an enemy toward its centre");
            if (Hazards.Count != hazards + 1) problems.Add("Fathomless Gate's centre is not a hazard enemies avoid");

            // Nether Smoke: a redirect volume, and not a hazard.
            hazards = Hazards.Count;
            Begin(ctx);
            ctx.Point = new Vector3(17600f, 0f, 0f);
            new SmokeCloudEffect().Execute(ctx);
            if (Object.FindAnyObjectByType<ShotRedirectVolume>() == null) problems.Add("Nether Smoke made no volume to hand shots on");
            if (Hazards.Count != hazards) problems.Add("Nether Smoke counts as a hazard, which would empty it of targets");

            // Lich Guise swaps with the zombie nearest the aim.
            PlayerRig lich = MakeRig(new Vector3(17700f, 0f, 0f), rigs);
            MinionController zombie = MinionSummoner.Spawn("zombie", new Vector3(17700f, 0f, 20f));
            Vector3 lichStart = lich.transform.position;
            Begin(lich.SpellContext);
            if (!new SwapWithMinionEffect().Execute(lich.SpellContext)) problems.Add("Lich Guise found no zombie straight ahead");
            else if ((lich.transform.position - new Vector3(17700f, 0f, 20f)).sqrMagnitude > 0.5f
                     || (zombie.transform.position - lichStart).sqrMagnitude > 0.5f)
                problems.Add("Lich Guise did not swap the player and the zombie");

            // The mimic fires a copy of the gun in hand.
            var oldMimics = new HashSet<MimicGunner>(Object.FindObjectsByType<MimicGunner>(FindObjectsSortMode.None));
            Begin(ctx);
            if (!new SummonMimicEffect().Execute(ctx)) problems.Add("Phantasmal Mimic was refused");
            else
            {
                MimicGunner gunner = null;
                foreach (MimicGunner found in Object.FindObjectsByType<MimicGunner>(FindObjectsSortMode.None))
                    if (!oldMimics.Contains(found)) gunner = found;
                if (gunner == null || gunner.Gun.Weapon.Definition != rig.Weapon.Definition || !gunner.Gun.Weapon.IsPhantom)
                    problems.Add("the mimic is not armed with a phantom of the gun in hand");
            }

            // Echo.
            Begin(ctx);
            new EchoEffect().Execute(ctx);
            if (!rig.Actions.IsEchoing) problems.Add("Echo did not start echoing");

            Begin(ctx);
            new NetherWallEffect().Execute(ctx);
            bool wall = false;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "NetherWall" && root.layer == Layers.NetherWall) wall = true;
            if (!wall) problems.Add("Nether Wall made no wall on its layer");
        }

        // ---------------------------------------------------------------- helpers

        private static void Begin(AbilityContext ctx)
        {
            ctx.Begin(DamageType.Energy, SpellType.Attack, Color.white, 1, 1f, isSpell: true);
        }

        private static bool Selected(AbilityContext ctx, Health health)
        {
            for (int i = 0; i < ctx.Targets.Count; i++)
                if (ctx.Targets[i] != null && ctx.Targets[i].Transform == health.transform) return true;
            return false;
        }

        private static PlayerRig MakeRig(Vector3 position, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerRig.Spawn(position);
            rigs.Add(rig);

            Wake(rig.Health);
            Wake(rig.Mana);
            Wake(rig.Motor);
            Wake(rig.Look);
            Wake(rig);

            rig.Book.ResetBook();
            rig.Movement.Equip(SpellLibrary.DefaultMovement);
            rig.CombatInput.EquipMelee(SpellLibrary.DefaultMelee);

            rig.Health.DestroyOnDeath = false;
            rig.Sheet.SetBaseOverride(Attr.MaxHealth, 100f);
            rig.Health.ConfigureMaxHealth(100f);
            rig.Sheet.SetBaseOverride(Attr.MaxMana, 100f);
            SetMana(rig, 100f);
            rig.Motor.RefillDashes();
            return rig;
        }

        private static void Wake(Component component)
        {
            if (component == null) return;
            MethodInfo awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (awake != null) awake.Invoke(component, null);
        }

        private static void SetMana(PlayerRig rig, float value)
        {
            float delta = rig.Mana.Current - value;
            if (delta > 0f) rig.Mana.TrySpend(delta);
            else if (delta < 0f) rig.Mana.Add(-delta);
        }

        private static Spell TestSpell(string id, SpellSchool school) => new Spell
        {
            Id = id, DisplayName = id, School = school, ManaCost = 1f, Cooldown = 1f, OnCast = { new PointAtCasterEffect() }
        };

        private static void Floor(Vector3 centre, float size)
        {
            Build.Cube(null, "ContentFloor", centre, new Vector3(size, 1f, size), MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Physics.SyncTransforms();
        }

        private static void Run(PlayerMotor motor, float seconds)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / 0.05f));
            for (int i = 0; i < steps; i++) motor.Step(0.05f);
        }

        private static Health Subject(string name, Team team, Vector3 position, int layer = -1, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (layer >= 0) go.layer = layer;

            if (collider)
            {
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.radius = 0.4f;
                capsule.height = 1.8f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
            }

            go.AddComponent<CharacterSheet>().SetBaseOverride(Attr.MaxHealth, 1000f);
            go.AddComponent<StatusController>();

            var health = go.AddComponent<Health>();
            health.Team = team;
            health.DestroyOnDeath = false;
            health.ConfigureMaxHealth(1000f);
            return health;
        }

        private static EnemyController SpawnEnemy(string id, Vector3 position)
        {
            EnemyController enemy = EnemyFactory.Spawn(id, position, 1);
            enemy.transform.position = position;
            enemy.Health.DestroyOnDeath = false;
            enemy.Health.ConfigureMaxHealth(1000f);
            return enemy;
        }

        private static WeaponDefinition FindHitscanGun()
        {
            foreach (WeaponDefinition gun in WeaponLibrary.All)
                if (gun.Delivery == DeliveryKind.Hitscan && gun.Mode != FireMode.Burst && gun.ManaPerShot <= 0f
                    && gun.SplashRadius <= 0f && gun.MagazineSize > 1 && gun.Damage > 0f)
                    return gun;
            return null;
        }

        private static WeaponDefinition SecondGun(WeaponDefinition not)
        {
            foreach (WeaponDefinition gun in WeaponLibrary.All)
                if (not == null || gun.Id != not.Id) return gun;
            return null;
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

        private static bool Approx(float a, float b) => Mathf.Abs(a - b) <= Tolerance;
    }
}
#endif
