#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Exercises the boon framework with boons built here rather than the roster: level caps, the
    /// gates, tag limits, behaviours and their floor events, the outgoing, incoming and lethal damage
    /// rules, target-aware crits, the context hits carry, the family-first offer roll, and every gun
    /// having a class.
    /// </summary>
    public static class BoonFrameworkTools
    {
        [MenuItem("Gunspire/Verify Boon Framework")]
        public static void VerifyBoonFramework()
        {
            var problems = new List<string>();
            var rigs = new List<PlayerRig>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            Health playerBefore = CombatRules.PlayerHealth;

            try
            {
                CombatRules.Clear();

                CheckLevels(problems);
                // Before any rig exists: this raises floor events, which a rig's masteries also hear.
                CheckBehaviours(problems);
                CheckGates(problems, rigs);
                CheckTags(problems);
                CheckOutgoing(problems);
                CheckIncomingAndLethal(problems);
                CheckCrits(problems);
                CheckHitContext(problems);
                CheckOffers(problems);
                CheckWeaponClasses(problems);
            }
            catch (System.Exception e)
            {
                problems.Add("the check itself threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                CombatRules.Clear();
                CombatRules.PlayerHealth = playerBefore;

                foreach (PlayerRig rig in rigs)
                    if (rig != null) rig.DetachPlayerSystems();

                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }

            if (problems.Count == 0)
            {
                Debug.Log("Boon framework: levels, gates, tags, behaviours, damage rules, crits, hit context, "
                          + "offers and weapon classes all behave as specified.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Boon framework: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 40; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- levels

        private static void CheckLevels(List<string> problems)
        {
            var run = new RunState(1, 3);
            Boon boon = TestBoon("capped", BoonFamily.Core, maxLevel: 2);

            if (!boon.IsOffered(run)) problems.Add("an untaken boon is not offered");
            run.AddBoon(boon);
            if (!boon.IsOffered(run)) problems.Add("a boon below its cap is not offered");
            run.AddBoon(boon);
            if (boon.IsOffered(run)) problems.Add("a boon at its cap is still offered");
            run.AddBoon(boon);
            if (run.BoonLevel(boon.Id) != 2) problems.Add("a boon went past its cap to level " + run.BoonLevel(boon.Id));

            run.ConsumeBoon(boon.Id);
            if (!run.HasBoon(boon.Id) || boon.IsOffered(run))
                problems.Add("a consumed boon stopped counting as taken");

            Boon copy = boon.Clone();
            copy.Tags.Add("copy-only");
            copy.Requirements.Add(new HasBoonRequirement());
            if (boon.Tags.Contains("copy-only") || boon.Requirements.Count > 0)
                problems.Add("a cloned boon shares its tag or requirement list with the original");
        }

        // ---------------------------------------------------------------- gates

        private static void CheckGates(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(4000f, 0f, 0f), rigs);
            var run = new RunState(2, 3) { Player = rig };

            var death = new SchoolEquippedRequirement { School = SpellSchool.Death };
            var raiseDead = new SpellEquippedRequirement { SpellId = "raise_dead" };
            var summon = new CanSummonRequirement();

            if (death.IsMet(run)) problems.Add("the Death gate passes with no Death spell equipped");
            if (raiseDead.IsMet(run)) problems.Add("the Raise Dead gate passes without it equipped");
            bool bestialBaseline = rig.Book.SchoolCount(SpellSchool.Bestial) > 0;
            if (!bestialBaseline && summon.IsMet(run)) problems.Add("the summon gate passes with nothing to summon");

            Spell spell = SpellLibrary.Get("raise_dead");
            if (spell == null) problems.Add("no raise_dead spell to test the gates with");
            else
            {
                rig.Book.Bind(spell, 0);
                if (!death.IsMet(run)) problems.Add("the Death gate fails with Raise Dead equipped");
                if (!raiseDead.IsMet(run)) problems.Add("the Raise Dead gate fails with it equipped");
                if (!summon.IsMet(run)) problems.Add("the summon gate fails with Raise Dead equipped");
            }

            var tagged = new RunState(3, 3) { Player = rig };
            rig.Book.ResetBook();
            if (!bestialBaseline && summon.IsMet(tagged)) problems.Add("the summon gate passes after the book was reset");
            Boon beetles = TestBoon("beetles", BoonFamily.Core);
            beetles.Tags.Add(BoonTags.Summon);
            tagged.AddBoon(beetles);
            if (!summon.IsMet(tagged)) problems.Add("a boon tagged as a summon does not open the summon gate");

            var movement = new SlotFilledRequirement { Slot = SpellSlot.Movement };
            rig.Movement.Equip(SpellLibrary.DefaultMovement);
            if (!movement.IsMet(run)) problems.Add("the movement gate fails with a movement spell equipped");

            WeaponDefinition handgun = WeaponLibrary.Get("arcanum");
            WeaponDefinition smg = WeaponLibrary.Get("emberspit");
            if (handgun == null || smg == null || rig.Holster == null)
            {
                problems.Add("no arcanum, emberspit or holster to test the gun gates with");
                return;
            }

            var handgunGate = new WeaponClassCarriedRequirement { Class = WeaponClass.Handgun };
            var smgGate = new WeaponClassCarriedRequirement { Class = WeaponClass.SMG };
            var projectileGate = new CarriesProjectileGunRequirement();

            rig.Holster.Clear();
            rig.Holster.SetSlot(0, handgun);
            if (!handgunGate.IsMet(run)) problems.Add("the handgun gate fails while holding the Arcanum");
            if (smgGate.IsMet(run)) problems.Add("the SMG gate passes with no SMG carried");
            if (projectileGate.IsMet(run)) problems.Add("the projectile gun gate passes with only a hitscan gun");

            rig.Holster.SetSlot(1, smg);
            if (!smgGate.IsMet(run)) problems.Add("the SMG gate fails with an SMG in the other hand");
            if (!projectileGate.IsMet(run)) problems.Add("the projectile gun gate fails with Emberspit carried");
        }

        // ---------------------------------------------------------------- tags

        private static void CheckTags(List<string> problems)
        {
            var run = new RunState(4, 3);

            Boon familiarity = TestBoon("familiarity", BoonFamily.Core, maxLevel: 1);
            familiarity.Requirements.Add(new HasTagRequirement { Tag = BoonTags.Familiar });

            Boon wisp = Familiar("wisp", maxLevel: 2);
            Boon imp = Familiar("imp", maxLevel: 1);

            if (familiarity.IsOffered(run)) problems.Add("Familiarity is offered with no familiar held");

            run.AddBoon(wisp);
            if (imp.IsOffered(run)) problems.Add("a second familiar is offered past the limit of one");
            if (!wisp.IsOffered(run)) problems.Add("the held familiar can no longer level up");
            if (!familiarity.IsOffered(run)) problems.Add("Familiarity is not offered once a familiar is held");

            run.AddBoon(familiarity);
            if (!imp.IsOffered(run)) problems.Add("a second familiar is not offered once Familiarity raises the limit");

            Boon cocky = TestBoon("cocky", BoonFamily.Core, maxLevel: 1);
            Boon tough = TestBoon("tough", BoonFamily.Core);
            tough.Tags.Add(BoonTags.Health);
            cocky.Requirements.Add(new NotWithTagRequirement { Tag = BoonTags.Health });
            tough.Requirements.Add(new NotWithBoonRequirement { BoonId = cocky.Id });

            if (!cocky.IsOffered(run) || !tough.IsOffered(run)) problems.Add("exclusions refuse boons before either is held");
            run.AddBoon(cocky);
            if (tough.IsOffered(run)) problems.Add("a health boon is offered alongside Cocky");

            var other = new RunState(5, 3);
            other.AddBoon(tough);
            if (cocky.IsOffered(other)) problems.Add("Cocky is offered alongside a health boon");
        }

        private static Boon Familiar(string id, int maxLevel)
        {
            Boon boon = TestBoon(id, BoonFamily.Core, maxLevel);
            boon.Tags.Add(BoonTags.Familiar);
            boon.Requirements.Add(new TagLimitRequirement
            {
                Tag = BoonTags.Familiar, Limit = 1, RaisedByBoon = "test_familiarity", OwnBoonId = boon.Id
            });
            return boon;
        }

        // ---------------------------------------------------------------- behaviours

        [System.Serializable]
        private class ProbeBehaviour : BoonBehaviour
        {
            public int Binds, Unbinds, LevelChanges, Entered, Leaving, Cleared, Completed;
            public float Ticked;
            public int LastPrevious = -1;

            protected override void OnBind() => Binds++;
            protected override void OnUnbind() => Unbinds++;

            protected override void OnLevelChanged(int previous)
            {
                LevelChanges++;
                LastPrevious = previous;
            }

            public override void OnFloorEntered(RoomRuntime room) => Entered++;
            public override void OnFloorLeaving(int floor) => Leaving++;
            public override void OnFloorCleared(RoomRuntime room) => Cleared++;
            public override void OnFloorCompleted(RoomRuntime room) => Completed++;
            public override void Tick(float dt) => Ticked += dt;
        }

        [System.Serializable]
        private class DoublingBehaviour : BoonBehaviour, IOutgoingDamageRule
        {
            public float OutgoingMultiplier(in DamageInfo hit, Health target) => 1f + Level;
        }

        private static void CheckBehaviours(List<string> problems)
        {
            var template = new ProbeBehaviour();
            Boon boon = TestBoon("probe", BoonFamily.Core, maxLevel: 3);
            boon.Effects.Add(new BoonBehaviourEffect { Behaviour = template });

            var runA = new RunState(6, 3);
            var runB = new RunState(7, 3);
            var host = new GameObject("BoonProbeRig").AddComponent<PlayerRig>();
            runA.Bind(host);

            runA.AddBoon(boon);
            runA.AddBoon(boon);
            runB.AddBoon(boon);

            ProbeBehaviour live = runA.FindBehaviour<ProbeBehaviour>();
            if (live == null)
            {
                problems.Add("taking a behaviour boon started no behaviour");
                runA.Unbind();
                return;
            }

            if (live == template) problems.Add("the run's behaviour is the template itself, not a copy");
            if (runA.Behaviours.Count != 1) problems.Add("two picks of one boon made " + runA.Behaviours.Count + " behaviours");
            if (live.Binds != 1) problems.Add("a behaviour was bound " + live.Binds + " times over two picks");
            if (live.Level != 2 || live.LastPrevious != 1 || live.LevelChanges != 2)
                problems.Add("a behaviour at its second pick reads level " + live.Level + " from " + live.LastPrevious);
            if (template.Binds != 0 || template.Level != 0) problems.Add("picking a boon changed its template");

            ProbeBehaviour other = runB.FindBehaviour<ProbeBehaviour>();
            if (other == null || other == live || other.Level != 1)
                problems.Add("a second run does not get its own behaviour at its own level");

            LevelEvents.RaiseFloorEntered(null);
            LevelEvents.RaiseFloorLeaving(1);
            LevelEvents.RaiseFloorCleared(null);
            LevelEvents.RaiseFloorCompleted(null);
            runA.Tick(0.5f);

            if (live.Entered != 1 || live.Leaving != 1 || live.Cleared != 1 || live.Completed != 1)
                problems.Add("a behaviour heard floor events " + live.Entered + "/" + live.Leaving + "/"
                             + live.Cleared + "/" + live.Completed + " times, not once each");
            if (!Mathf.Approximately(live.Ticked, 0.5f)) problems.Add("a behaviour ticked " + live.Ticked + "s, not 0.5s");

            // An unbound run hears nothing.
            if (other != null && other.Entered != 0) problems.Add("a run that was never bound heard floor events");

            // Rules register with their behaviour and leave with the run.
            Boon doubling = TestBoon("doubling", BoonFamily.Core);
            doubling.Effects.Add(new BoonBehaviourEffect { Behaviour = new DoublingBehaviour() });
            runA.AddBoon(doubling);

            Health enemy = Subject(Team.Enemy, 1000f);
            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Player, null));
            if (!Mathf.Approximately(enemy.Current, 980f))
                problems.Add("a level 1 doubling behaviour left an enemy at " + enemy.Current + ", not 980");

            runA.Unbind();
            if (live.Unbinds != 1) problems.Add("ending the run unbound a behaviour " + live.Unbinds + " times");
            if (runA.Behaviours.Count != 0) problems.Add("ending the run left behaviours behind");

            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Player, null));
            if (!Mathf.Approximately(enemy.Current, 970f))
                problems.Add("a behaviour's damage rule outlived its run: the enemy is at " + enemy.Current);

            LevelEvents.RaiseFloorEntered(null);
            if (live.Entered != 1) problems.Add("an unbound behaviour still hears floor events");
        }

        // ---------------------------------------------------------------- damage rules

        private sealed class ScaleRule : IOutgoingDamageRule
        {
            public float Multiplier = 2f;
            public int Calls;

            public float OutgoingMultiplier(in DamageInfo hit, Health target)
            {
                Calls++;
                return Multiplier;
            }
        }

        private sealed class HalveRule : IIncomingDamageRule
        {
            public float ModifyIncoming(in DamageInfo hit, Health player, float amount) => amount * 0.5f;
        }

        private sealed class SaveRule : ILethalHitRule
        {
            public int Priority;
            public bool Saves;
            public List<int> Order;

            public int LethalPriority => Priority;

            public bool TrySurvive(in DamageInfo hit, Health player, float amount)
            {
                Order.Add(Priority);
                if (Saves) player.SetCurrent(player.Max * 0.5f);
                return Saves;
            }
        }

        private static void CheckOutgoing(List<string> problems)
        {
            var rule = new ScaleRule();
            CombatRules.Register(rule);

            Health enemy = Subject(Team.Enemy, 1000f);
            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Player, null));
            if (!Mathf.Approximately(enemy.Current, 980f))
                problems.Add("an outgoing rule of x2 left an enemy at " + enemy.Current + ", not 980");

            Health neutral = Subject(Team.Neutral, 1000f);
            neutral.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Player, null));
            if (!Mathf.Approximately(neutral.Current, 980f))
                problems.Add("an outgoing rule does not reach a confused (neutral) enemy");

            int calls = rule.Calls;
            Health minion = Subject(Team.Player, 1000f);
            minion.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Enemy, null));
            if (!Mathf.Approximately(minion.Current, 990f) || rule.Calls != calls)
                problems.Add("an outgoing rule changed an enemy's hit on the player's side");

            Health victim = Subject(Team.Enemy, 1000f);
            victim.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Enemy, null));
            if (rule.Calls != calls) problems.Add("an outgoing rule was asked about a hit between enemies");

            // A rule that returns something negative is held at zero rather than healing.
            rule.Multiplier = -3f;
            float beforeHit = enemy.Current;
            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Player, null));
            if (enemy.Current > beforeHit) problems.Add("a negative outgoing multiplier healed the enemy");

            CombatRules.Unregister(rule);
            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Player, null));
            if (!Mathf.Approximately(enemy.Current, beforeHit - 10f))
                problems.Add("an unregistered outgoing rule still applied");
        }

        private static void CheckIncomingAndLethal(List<string> problems)
        {
            Health player = Subject(Team.Player, 100f, "BoonProbePlayer");
            Health ally = Subject(Team.Player, 100f, "BoonProbeAlly");
            CombatRules.PlayerHealth = player;

            var halve = new HalveRule();
            CombatRules.Register(halve);

            player.TakeDamage(DamageInfo.Create(20f, DamageType.True, Team.Enemy, null));
            if (!Mathf.Approximately(player.Current, 90f))
                problems.Add("an incoming rule of half left the player at " + player.Current + ", not 90");

            ally.TakeDamage(DamageInfo.Create(20f, DamageType.True, Team.Enemy, null));
            if (!Mathf.Approximately(ally.Current, 80f))
                problems.Add("an incoming rule reached a minion, which is on the player's team but is not the player");

            player.AddShield(10f, 10f);
            player.TakeDamage(DamageInfo.Create(40f, DamageType.True, Team.Enemy, null));
            if (!Mathf.Approximately(player.Current, 80f))
                problems.Add("the incoming rule should run before the shield: 40 halved to 20, 10 shielded, "
                             + "leaving 80, but the player is at " + player.Current);

            CombatRules.Unregister(halve);

            var order = new List<int>();
            var late = new SaveRule { Priority = 10, Saves = true, Order = order };
            var early = new SaveRule { Priority = 0, Saves = false, Order = order };
            CombatRules.Register(late);
            CombatRules.Register(early);

            player.TakeDamage(DamageInfo.Create(500f, DamageType.True, Team.Enemy, null));
            if (!player.IsAlive) problems.Add("a lethal rule that saves did not keep the player alive");
            else if (!Mathf.Approximately(player.Current, 50f))
                problems.Add("the saved player is at " + player.Current + ", not the 50 the rule set");
            if (order.Count != 2 || order[0] != 0 || order[1] != 10)
                problems.Add("lethal rules ran in the order " + string.Join(",", order) + ", not 0,10");

            order.Clear();
            player.TakeDamage(DamageInfo.Create(10f, DamageType.True, Team.Enemy, null));
            if (order.Count != 0) problems.Add("lethal rules were asked about a hit that was not lethal");

            ally.TakeDamage(DamageInfo.Create(500f, DamageType.True, Team.Enemy, null));
            if (ally.IsAlive) problems.Add("a lethal rule saved a minion rather than only the player");

            CombatRules.Unregister(late);
            CombatRules.Unregister(early);
            player.TakeDamage(DamageInfo.Create(500f, DamageType.True, Team.Enemy, null));
            if (player.IsAlive) problems.Add("the player survived a lethal hit with no lethal rules");

            CombatRules.PlayerHealth = null;
        }

        // ---------------------------------------------------------------- crits

        private sealed class ForceAgainst : ICritRule
        {
            public IDamageable Target;
            public CharacterSheet Seen;

            public void AdjustCrit(CharacterSheet attacker, IDamageable target, ref float chance, ref bool forced)
            {
                Seen = attacker;
                if (target != null && target == Target) forced = true;
            }
        }

        private sealed class DoubleChance : ICritRule
        {
            public void AdjustCrit(CharacterSheet attacker, IDamageable target, ref float chance, ref bool forced)
                => chance *= 2f;
        }

        private static void CheckCrits(List<string> problems)
        {
            var go = new GameObject("BoonProbeAttacker");
            CharacterSheet sheet = go.AddComponent<CharacterSheet>();
            sheet.SetBaseOverride(Attr.CritChance, 0f);

            Health marked = Subject(Team.Enemy, 100f);
            Health unmarked = Subject(Team.Enemy, 100f);

            var force = new ForceAgainst { Target = marked };
            CombatRules.Register(force);

            if (!Combat.RollCrit(sheet, marked, out float multiplier)) problems.Add("a forced crit did not crit");
            else if (!Mathf.Approximately(multiplier, sheet.Get(Attr.CritDamage)))
                problems.Add("a forced crit used " + multiplier + ", not the sheet's crit damage");
            if (force.Seen != sheet) problems.Add("a crit rule was not told who is attacking");

            for (int i = 0; i < 50; i++)
            {
                if (!Combat.RollCrit(sheet, unmarked, out _) && !Combat.RollCrit(sheet, null, out _)) continue;
                problems.Add("a crit landed at zero chance against a target the rule does not force");
                break;
            }

            CombatRules.Unregister(force);
            if (Combat.RollCrit(sheet, marked, out _)) problems.Add("an unregistered crit rule still forced a crit");

            // Doubling a certain chance cannot miss; doubling zero cannot hit.
            var doubling = new DoubleChance();
            CombatRules.Register(doubling);
            sheet.SetBaseOverride(Attr.CritChance, 0.5f);
            int crits = 0;
            for (int i = 0; i < 50; i++)
                if (Combat.RollCrit(sheet, unmarked, out _)) crits++;
            if (crits != 50) problems.Add("a doubled 50% crit chance landed " + crits + " of 50");
            CombatRules.Unregister(doubling);
        }

        // ---------------------------------------------------------------- hit context

        private static void CheckHitContext(List<string> problems)
        {
            WeaponDefinition gun = WeaponLibrary.Get("arcanum");
            if (gun == null)
            {
                problems.Add("no arcanum to build a shot with");
                return;
            }

            var holder = new GameObject("BoonProbeGun");
            holder.transform.position = new Vector3(3f, 1f, 7f);
            var weapon = holder.AddComponent<Weapon>();
            weapon.Owner = holder;
            weapon.Equip(gun);

            DamageInfo shot = weapon.BuildShotDamage(ShotSpec.Primary(gun, 0f), Vector3.zero, Vector3.up, Vector3.forward);
            if (shot.Weapon != weapon) problems.Add("a gun's hit does not carry its weapon");
            if (!shot.HasSourcePosition || shot.SourcePosition != holder.transform.position)
                problems.Add("a gun's hit does not carry where it was fired from");
            if (shot.Spell != null) problems.Add("a gun's hit carries a spell");

            WeaponHit hit = new WeaponHit { Damage = shot, Target = Subject(Team.Enemy, 100f) };
            var bonusLog = new List<DamageInfo>();
            ((Health)hit.Target).Damaged += (info, amount) => bonusLog.Add(info);
            hit.DealBonus(5f, DamageType.Energy);
            if (bonusLog.Count != 1 || bonusLog[0].Weapon != weapon)
                problems.Add("a bonus hit from a gun round does not carry the weapon");

            var caster = new GameObject("BoonProbeCaster");
            caster.transform.position = new Vector3(-4f, 0f, 2f);
            var ctx = new AbilityContext { Caster = caster, Team = Team.Player, Sheet = caster.AddComponent<CharacterSheet>() };
            Spell spell = SpellLibrary.Get("raise_dead") ?? SpellLibrary.DefaultMelee;
            ctx.BeginCast(spell, 1);
            ctx.Origin = caster.transform.position;

            DamageInfo cast = ctx.BuildDamage(1f, Vector3.zero, Vector3.up);
            if (cast.Spell != spell) problems.Add("a spell's hit does not carry its spell");
            if (!cast.HasSourcePosition || cast.SourcePosition != ctx.Origin)
                problems.Add("a spell's hit does not carry where it was cast from");
            if (cast.Weapon != null) problems.Add("a spell's hit carries a weapon");

            new SpawnProjectileEffect { Damage = 1f }.Execute(ctx);
            Projectile bolt = null;
            foreach (Projectile p in Object.FindObjectsByType<Projectile>())
                if (p.Owner == caster) bolt = p;

            if (bolt == null) problems.Add("a spell projectile could not be found to check");
            else
            {
                DamageInfo landed = bolt.BuildHitDamage(Vector3.zero, Vector3.up);
                if (landed.Spell != spell) problems.Add("a spell projectile's hit does not carry its spell");
                if (!landed.HasSourcePosition || landed.SourcePosition != bolt.LaunchPoint)
                    problems.Add("a spell projectile's hit does not carry where it was launched");
            }
        }

        // ---------------------------------------------------------------- offers

        private static void CheckOffers(List<string> problems)
        {
            var run = new RunState(8, 3);
            var pool = new List<Boon>();
            for (int i = 0; i < 6; i++) pool.Add(TestBoon("core" + i, BoonFamily.Core));
            for (int i = 0; i < 6; i++) pool.Add(TestBoon("arsenal" + i, BoonFamily.Arsenal));

            Boon pact = TestBoon("pact", BoonFamily.Pact);
            Boon gated = TestBoon("gated", BoonFamily.Core);
            gated.Requirements.Add(new HasBoonRequirement { BoonId = "never" });
            pool.Add(pact);
            pool.Add(gated);

            for (int roll = 0; roll < 200; roll++)
            {
                List<Boon> offer = BoonLibrary.Offer(run, pool, 5);
                var ids = new HashSet<string>();
                foreach (Boon b in offer)
                {
                    if (!ids.Add(b.Id)) problems.Add("an offer repeated " + b.Id);
                    if (b == pact) problems.Add("a Pact was offered by the ordinary roll");
                    if (b == gated) problems.Add("a boon whose gate fails was offered");
                }

                if (offer.Count != 5)
                {
                    problems.Add("an offer of 5 from 12 valid boons held " + offer.Count);
                    break;
                }
                if (problems.Count > 0) break;
            }

            List<Boon> all = BoonLibrary.Offer(run, pool, 50);
            if (all.Count != 12) problems.Add("asking for more than the pool holds gave " + all.Count + ", not the 12 valid boons");

            if (BoonLibrary.Offer(run, new List<Boon> { pact, gated }, 3).Count != 0)
                problems.Add("a pool of only Pacts and gated boons still produced an offer");

            // Family weights: Core 30 against Spell 25, one card each time.
            var weighted = new List<Boon> { TestBoon("weighted_core", BoonFamily.Core), TestBoon("weighted_spell", BoonFamily.Spell) };
            int core = 0;
            const int rolls = 4000;
            for (int i = 0; i < rolls; i++)
                if (BoonLibrary.Offer(run, weighted, 1)[0].Family == BoonFamily.Core) core++;

            float share = core / (float)rolls;
            float expected = BoonLibrary.FamilyWeight(BoonFamily.Core)
                             / (BoonLibrary.FamilyWeight(BoonFamily.Core) + BoonLibrary.FamilyWeight(BoonFamily.Spell));
            if (Mathf.Abs(share - expected) > 0.04f)
                problems.Add("Core took " + share.ToString("0.000") + " of Core-or-Spell offers, expected about " + expected.ToString("0.000"));

            // A family with nothing left is skipped rather than ending the offer early.
            var lopsided = new List<Boon> { TestBoon("only_core", BoonFamily.Core) };
            for (int i = 0; i < 4; i++) lopsided.Add(TestBoon("slot" + i, BoonFamily.Slots));
            for (int i = 0; i < 50; i++)
            {
                if (BoonLibrary.Offer(run, lopsided, 5).Count == 5) continue;
                problems.Add("an offer ended early once one family ran out");
                break;
            }
        }

        // ---------------------------------------------------------------- weapon classes

        private static void CheckWeaponClasses(List<string> problems)
        {
            var seen = new HashSet<WeaponClass>();
            foreach (WeaponDefinition gun in WeaponLibrary.All)
            {
                if (gun.Class == WeaponClass.Unassigned) problems.Add(gun.Id + " has no weapon class");
                else seen.Add(gun.Class);
            }

            foreach (WeaponClass cls in (WeaponClass[])System.Enum.GetValues(typeof(WeaponClass)))
                if (cls != WeaponClass.Unassigned && !seen.Contains(cls))
                    problems.Add("no gun belongs to the " + cls + " class");
        }

        // ---------------------------------------------------------------- helpers

        private static Boon TestBoon(string id, BoonFamily family, int maxLevel = 3) => new Boon
        {
            Id = "test_" + id,
            Name = id,
            Family = family,
            MaxLevel = maxLevel
        };

        private static Health Subject(Team team, float health, string name = "BoonProbeSubject")
        {
            var go = new GameObject(name);
            go.AddComponent<CharacterSheet>().SetBaseOverride(Attr.MaxHealth, health);
            go.AddComponent<StatusController>();

            var hp = go.AddComponent<Health>();
            hp.Team = team;
            hp.DestroyOnDeath = false;
            hp.ConfigureMaxHealth(health, refill: true);
            return hp;
        }
    }
}
#endif
