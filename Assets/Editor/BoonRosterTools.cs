#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Checks the boon roster: its shape against Docs/BoonDesign.md (ids, families, gates, levels and the rarity
    /// counts), that every boon can be taken to its cap and survive a round of combat events, and what a
    /// representative boon from each group actually does.
    /// </summary>
    public static class BoonRosterTools
    {
        public const int Commons = 101;
        public const int Uncommons = 60;
        public const int Rares = 53;
        public const int Mythics = 15;

        private const float Tolerance = 0.01f;

        private static int _seed = 900;
        private static int _slot;

        [MenuItem("Gunspire/Verify Boons")]
        public static void VerifyBoons()
        {
            var problems = new List<string>();
            Health playerBefore = CombatRules.PlayerHealth;
            AllyBoosts alliesBefore = AllyBoosts.Active;
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            _slot = 0;

            try
            {
                CombatRules.Clear();
                WorldClock.Reset();
                TargetRegistry.Clear();

                List<Boon> roster = BoonLibrary.BuiltIn();
                CheckStructure(roster, problems);
                CheckSmoke(roster, problems);
                CheckOffers(problems);
                CheckData(problems);
                CheckCoreBehaviours(problems);
                CheckArsenalBehaviours(problems);
                CheckSlotBehaviours(problems);
                CheckSchoolBehaviours(problems);
            }
            catch (Exception e)
            {
                problems.Add("the check itself threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                CombatRules.Clear();
                CombatRules.PlayerHealth = playerBefore;
                AllyBoosts.Active = alliesBefore;
                OrbPickup.FullHealthCollector = null;
                KnockbackImpacts.PlayerImpactScale = 1f;
                WorldClock.Reset();
                TargetRegistry.Clear();
                Hazards.Clear();
                DeathRecords.Clear();

                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }

            if (problems.Count == 0)
            {
                Debug.Log("Boons: the roster matches the design (" + Commons + "/" + Uncommons + "/" + Rares + "/" + Mythics
                          + "), every boon reaches its cap through a round of combat, and the behaviours do what they say.\n"
                          + "  no problems.");
                return;
            }

            var report = new StringBuilder("Boons: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 80; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- structure

        private static void CheckStructure(List<Boon> roster, List<string> problems)
        {
            var ids = new HashSet<string>();
            var counts = new Dictionary<Rarity, int>();

            foreach (Boon boon in roster)
            {
                string at = "\"" + boon.Id + "\"";
                if (!ids.Add(boon.Id)) problems.Add("two boons share the id " + at);
                if (string.IsNullOrEmpty(boon.Name) || string.IsNullOrEmpty(boon.Description) || string.IsNullOrEmpty(boon.Group))
                    problems.Add(at + " is missing a name, description or group");

                counts.TryGetValue(boon.Rarity, out int count);
                counts[boon.Rarity] = count + 1;

                if (boon.Family == BoonFamily.Spell || boon.Family == BoonFamily.Pact)
                    problems.Add(at + " is in the " + boon.Family + " family, which has no boons yet");
                if (boon.Rarity == Rarity.Legendary) problems.Add(at + " is Legendary, and there are none yet");

                bool exempt = boon.Id == BoonIds.Familiarity;
                if ((boon.Effects == null || boon.Effects.Count == 0) && !exempt) problems.Add(at + " has no effects");
                if (boon.Effects != null)
                    foreach (BoonEffect effect in boon.Effects)
                    {
                        if (effect == null) problems.Add(at + " has a null effect");
                        else if (effect is BoonBehaviourEffect be && be.Behaviour == null) problems.Add(at + " has an empty behaviour");
                        else if (effect is GrantFamiliarEffect gf && FamiliarLibrary.Get(gf.FamiliarId) == null)
                            problems.Add(at + " grants the unknown familiar \"" + gf.FamiliarId + "\"");
                    }

                try
                {
                    boon.EffectSummary();
                    boon.RequirementSummary();
                }
                catch (Exception e)
                {
                    problems.Add(at + " threw describing itself: " + e.Message);
                }

                CheckLevels(boon, problems);
                CheckGates(boon, problems);
            }

            Expect(counts, Rarity.Common, Commons, problems);
            Expect(counts, Rarity.Uncommon, Uncommons, problems);
            Expect(counts, Rarity.Rare, Rares, problems);
            Expect(counts, Rarity.Mythic, Mythics, problems);
        }

        private static void Expect(Dictionary<Rarity, int> counts, Rarity rarity, int expected, List<string> problems)
        {
            counts.TryGetValue(rarity, out int count);
            if (count != expected) problems.Add("the roster has " + count + " " + rarity + " boons, not " + expected);
        }

        private static void CheckLevels(Boon boon, List<string> problems)
        {
            string at = "\"" + boon.Id + "\"";
            if (boon.Group == BoonLibrary.StatsGroup)
            {
                if (boon.MaxLevel != BoonLibrary.StatBoonLevels) problems.Add("stat boon " + at + " caps at " + boon.MaxLevel + ", not 5");
                return;
            }

            if (boon.Family == BoonFamily.School)
            {
                int expected = boon.Rarity == Rarity.Common ? 5 : boon.Rarity == Rarity.Uncommon ? 3 : 2;
                if (boon.MaxLevel != expected) problems.Add("school boon " + at + " caps at " + boon.MaxLevel + ", not " + expected);
                return;
            }

            int ceiling = boon.Rarity == Rarity.Common ? 5 : boon.Rarity == Rarity.Uncommon ? 3 : boon.Rarity == Rarity.Rare ? 2 : 1;
            if (boon.MaxLevel < 1 || boon.MaxLevel > ceiling)
                problems.Add(at + " caps at " + boon.MaxLevel + ", above the " + boon.Rarity + " cap of " + ceiling);
        }

        private static void CheckGates(Boon boon, List<string> problems)
        {
            string at = "\"" + boon.Id + "\"";

            if (boon.Family == BoonFamily.School)
            {
                var gate = Find<SchoolEquippedRequirement>(boon);
                if (gate == null || gate.School.ToString() != boon.Group)
                    problems.Add("school boon " + at + " is not gated on its school, " + boon.Group);
            }
            else if (Find<SchoolEquippedRequirement>(boon) != null)
                problems.Add(at + " is gated on a school but is not a School boon");

            if (boon.Group == "Classes" && Find<WeaponClassCarriedRequirement>(boon) == null)
                problems.Add("class boon " + at + " is not gated on a gun class");

            if (boon.HasTag(BoonTags.Familiar))
            {
                var limit = Find<TagLimitRequirement>(boon);
                if (limit == null || limit.OwnBoonId != boon.Id || limit.RaisedByBoon != BoonIds.Familiarity)
                    problems.Add("familiar boon " + at + " is not limited to one familiar, raised by Familiarity");
            }

            if (boon.HasTag(BoonTags.Health))
            {
                bool cocky = false, glass = false;
                foreach (BoonRequirement r in boon.Requirements)
                    if (r is NotWithBoonRequirement not)
                    {
                        cocky |= not.BoonId == BoonIds.Cocky;
                        glass |= not.BoonId == BoonIds.GlassSoul;
                    }
                if (!cocky || !glass) problems.Add("health boon " + at + " is still offered alongside Cocky or Glass Soul");
            }

            bool raisesHealth = false;
            foreach (BoonEffect effect in boon.Effects)
                raisesHealth |= effect is ModifyStatEffect s && s.Stat == StatType.Endurance
                                || effect is ModifyAttributeEffect a && a.Attribute == Attr.MaxHealth && a.Amount > 0f;
            if (raisesHealth && !boon.HasTag(BoonTags.Health)) problems.Add(at + " raises maximum health but is not a health boon");
        }

        private static T Find<T>(Boon boon) where T : BoonRequirement
        {
            foreach (BoonRequirement r in boon.Requirements)
                if (r is T found) return found;
            return null;
        }

        // ---------------------------------------------------------------- every boon, to its cap

        private static void CheckSmoke(List<Boon> roster, List<string> problems)
        {
            var failures = new List<string>();
            WeaponDefinition gun = null;
            foreach (WeaponDefinition def in WeaponLibrary.All)
                if (def.Class == WeaponClass.Handgun) { gun = def; break; }

            foreach (Boon boon in roster)
            {
                Test("taking " + boon.Id, failures, (run, rig) =>
                {
                    if (gun != null) rig.Weapon.Equip(gun);
                    rig.Book.Bind(PlayerTools.TestSpell("smoke_death", SpellSchool.Death), 0);

                    for (int i = 0; i < boon.MaxLevel; i++) run.AddBoon(boon);
                    if (run.BoonLevel(boon.Id) != boon.MaxLevel)
                        failures.Add(boon.Id + " reached level " + run.BoonLevel(boon.Id) + " of " + boon.MaxLevel);

                    EnemyController enemy = Enemy(rig, new Vector3(0f, 0f, 6f));
                    LevelEvents.RaiseFloorEntered(null);
                    run.Tick(0.1f);

                    DamageInfo shot = PlayerHit(rig, 5f, DamageOrigin.Gun);
                    shot.Weapon = rig.Weapon;
                    enemy.Health.TakeDamage(shot);
                    rig.Weapon.ReportHit(enemy.Health, shot, enemy.transform.position, Vector3.up, Vector3.forward, null, 1);
                    rig.Weapon.ReportMiss(2);
                    rig.Weapon.FireNow();
                    enemy.Health.TakeDamage(PlayerHit(rig, 5f, DamageOrigin.Melee));

                    Hurt(rig, enemy, 5f);
                    run.Tick(0.1f);
                    run.EarnShillings(120f, ShillingSource.Other);
                    Kill(enemy.Health, rig);

                    LevelEvents.RaiseFloorCleared(null);
                    LevelEvents.RaiseFloorCompleted(null);
                    LevelEvents.RaiseFloorLeaving(1);
                    run.Tick(0.1f);
                });
            }

            if (CombatRules.OutgoingMultiplier(DamageInfo.Create(1f, DamageType.Kinetic, Team.Player, null), null) != 1f)
                failures.Add("a rule was still registered after every run ended");

            problems.AddRange(failures);
        }

        // ---------------------------------------------------------------- offers

        private static void CheckOffers(List<string> problems)
        {
            Test("familiar limits", problems, (run, rig) =>
            {
                Boon wisp = BoonLibrary.Get("wisp");
                Boon imp = BoonLibrary.Get("imp");
                Boon familiarity = BoonLibrary.Get(BoonIds.Familiarity);

                if (familiarity.IsOffered(run)) problems.Add("Familiarity is offered with no familiar held");
                if (!imp.IsOffered(run)) problems.Add("a familiar is not offered with none held");

                run.AddBoon(wisp);
                if (imp.IsOffered(run)) problems.Add("a second familiar is offered without Familiarity");
                if (!familiarity.IsOffered(run)) problems.Add("Familiarity is not offered with a familiar held");

                run.AddBoon(familiarity);
                if (!imp.IsOffered(run)) problems.Add("a second familiar is not offered with Familiarity");
                run.AddBoon(imp);
                if (BoonLibrary.Get("crow").IsOffered(run)) problems.Add("a third familiar is offered with one Familiarity");
            });

            Test("health boons and Cocky", problems, (run, rig) =>
            {
                if (!BoonLibrary.Get("tough").IsOffered(run)) problems.Add("Tough is not offered in a fresh run");
                run.AddBoon(BoonLibrary.Get(BoonIds.Cocky));
                if (BoonLibrary.Get("tough").IsOffered(run) || BoonLibrary.Get("thick_blood").IsOffered(run))
                    problems.Add("a health boon is offered while holding Cocky");
                if (BoonLibrary.Get(BoonIds.GlassSoul).IsOffered(run)) problems.Add("Glass Soul is offered while holding Cocky");
                if (!BoonLibrary.Get("lucky").IsOffered(run)) problems.Add("Lucky is refused while holding Cocky");
            });

            Test("gates", problems, (run, rig) =>
            {
                if (BoonLibrary.Get("kindling").IsOffered(run)) problems.Add("an Elemental boon is offered with no Elemental spell");
                rig.Book.Bind(PlayerTools.TestSpell("gate_fire", SpellSchool.Elemental), 0);
                if (!BoonLibrary.Get("kindling").IsOffered(run)) problems.Add("an Elemental boon is refused with an Elemental spell");

                if (BoonLibrary.Get("master_summoner").IsOffered(run) && rig.Book.SchoolCount(SpellSchool.Bestial) == 0)
                    problems.Add("Master Summoner is offered with nothing to summon");
                run.AddBoon(BoonLibrary.Get("beetle_swarm"));
                if (!BoonLibrary.Get("blood_pact").IsOffered(run)) problems.Add("Beetle Swarm does not open Blood Pact's gate");
            });
        }

        // ---------------------------------------------------------------- data boons

        private static void CheckData(List<string> problems)
        {
            Test("data boons", problems, (run, rig) =>
            {
                CharacterSheet sheet = rig.Sheet;

                int endurance = sheet.GetStat(StatType.Endurance);
                Take(run, "tough", 2);
                if (sheet.GetStat(StatType.Endurance) != endurance + 4) problems.Add("two levels of Tough did not add 4 Endurance");

                int luck = sheet.GetStat(StatType.Luck);
                Take(run, "ascendant", 1);
                if (sheet.GetStat(StatType.Luck) != luck + 5) problems.Add("Ascendant did not add 5 Luck");

                Take(run, "kindling", 2);
                if (!Approx(sheet.SchoolDamageMultiplier(SpellSchool.Elemental), 1.12f))
                    problems.Add("two levels of Kindling gave an Elemental damage multiplier of " + sheet.SchoolDamageMultiplier(SpellSchool.Elemental));
                Take(run, "conduit", 1);
                if (!(sheet.SchoolCostMultiplier(SpellSchool.Elemental) < 1f)) problems.Add("Conduit did not make Elemental spells cheaper");

                float reload = sheet.GetFor(WeaponClass.Handgun, Attr.ReloadSpeed);
                float sniperReload = sheet.GetFor(WeaponClass.Sniper, Attr.ReloadSpeed);
                Take(run, "sidearm", 1);
                if (!(sheet.GetFor(WeaponClass.Handgun, Attr.ReloadSpeed) > reload)
                    || !Approx(sheet.GetFor(WeaponClass.Sniper, Attr.ReloadSpeed), sniperReload))
                    problems.Add("Sidearm did not speed up handgun reloads alone");

                Take(run, "overpenetration", 2);
                if (!Approx(sheet.GetFor(WeaponClass.Sniper, Attr.Pierce), 2f)) problems.Add("two levels of Overpenetration did not add 2 sniper pierce");

                Take(run, "rewarded", 2);
                if (run.ExtraBoonPicks != 2 || run.ExtraOfferChoices != 2) problems.Add("two levels of Rewarded did not add two picks and two cards");
                Take(run, "fickle_fate", 1);
                if (run.OfferRerolls != 1) problems.Add("Fickle Fate did not add a reroll");

                Take(run, "acrophobia", 1);
                if (!Approx(sheet.Get(Attr.JumpCount), 1f)) problems.Add("Acrophobia did not add an air jump");

                Take(run, "well_fed", 1);
                Take(run, "bone_density", 1);
                if (!(run.Allies.CompanionHealth > 0f) || !(run.Allies.UndeadHealth > 0f)) problems.Add("Well Fed or Bone Density added nothing");

                Take(run, "lingering_doubt", 1);
                if (!(sheet.SpellEffectMultiplier(SpellEffectChannel.StatusDuration, SpellSchool.Death, StatusId.Fear) > 1f)
                    || !Approx(sheet.SpellEffectMultiplier(SpellEffectChannel.StatusDuration, SpellSchool.Death, StatusId.Burn), 1f))
                    problems.Add("Lingering Doubt did not lengthen fear alone");
            });

            Test("mastery settings", problems, (run, rig) =>
            {
                rig.Book.Bind(PlayerTools.TestSpell("set_fire", SpellSchool.Elemental), 0);
                ConfluxMastery conflux = rig.Masteries.Get<ConfluxMastery>();
                Boon fusion = BoonLibrary.Get("fusion");

                run.AddBoon(fusion);
                if (!conflux.KeepsElements || !Approx(conflux.ReactionCooldownScale, 1f))
                    problems.Add("Fusion's first level did not keep elements, or already cut the cooldown");
                run.AddBoon(fusion);
                if (!Approx(conflux.ReactionCooldownScale, 0.7f)) problems.Add("Fusion's second level set a cooldown scale of " + conflux.ReactionCooldownScale);

                SoulsMastery souls = rig.Masteries.Get<SoulsMastery>();
                Take(run, "soul_jar", 2);
                if (souls.CapBonus != 2) problems.Add("two levels of Soul Jar gave a cap bonus of " + souls.CapBonus);

                ArcaneWarpMastery warp = rig.Masteries.Get<ArcaneWarpMastery>();
                Take(run, "void_pocket", 2);
                if (!Approx(warp.LingerSeconds, 10f)) problems.Add("two levels of Void Pocket linger " + warp.LingerSeconds + " s, not 10");
            });
        }

        // ---------------------------------------------------------------- Core

        private static void CheckCoreBehaviours(List<string> problems)
        {
            Test("Executioner", problems, (run, rig) =>
            {
                Take(run, "executioner", 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                var rule = run.FindBehaviour<ExecutionerBehaviour>();
                DamageInfo hit = PlayerHit(rig, 1f, DamageOrigin.Gun);
                if (!Approx(rule.OutgoingMultiplier(hit, enemy.Health), 1f)) problems.Add("Executioner raised damage on a healthy enemy");
                enemy.Health.SetCurrent(enemy.Health.Max * 0.2f);
                if (!(rule.OutgoingMultiplier(hit, enemy.Health) > 1f)) problems.Add("Executioner did not raise damage below 30%");
            });

            Test("Charge Up", problems, (run, rig) =>
            {
                Take(run, BoonIds.ChargeUp, 1);
                var rule = run.FindBehaviour<ChargeUpBehaviour>();
                if (rule.Active) problems.Add("Charge Up was active on the floor it was taken");
                LevelEvents.RaiseFloorEntered(null);
                if (!rule.Active) problems.Add("Charge Up was not active on the next floor");
                LevelEvents.RaiseFloorCompleted(null);
                if (rule.Active || !run.FindBoon(BoonIds.ChargeUp).Consumed) problems.Add("Charge Up was not used up after its floor");
                LevelEvents.RaiseFloorEntered(null);
                if (rule.Active) problems.Add("Charge Up came back on a later floor");
                if (BoonLibrary.Get(BoonIds.ChargeUp).IsOffered(run)) problems.Add("Charge Up is offered again");
            });

            Test("Phylactery and Soul Shield", problems, (run, rig) =>
            {
                rig.Book.Bind(PlayerTools.TestSpell("shield_death", SpellSchool.Death), 0);
                SoulsMastery souls = rig.Masteries.Get<SoulsMastery>();
                Take(run, BoonIds.Phylactery, 1);
                Take(run, "soul_shield", 1);
                souls.AddSouls(1);

                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                rig.Health.SetCurrent(40f);
                Hurt(rig, enemy, 500f);
                if (!Approx(rig.Health.Current, 40f) || souls.Souls != 0)
                    problems.Add("Soul Shield did not cancel a killing blow for a soul: health " + rig.Health.Current + ", souls " + souls.Souls);
                if (run.FindBoon(BoonIds.Phylactery).Consumed) problems.Add("Phylactery went before Soul Shield");

                Hurt(rig, enemy, 500f);
                if (!rig.Health.IsAlive || !Approx(rig.Health.Current, rig.Health.Max * 0.5f))
                    problems.Add("Phylactery did not leave the player at half health: " + rig.Health.Current);
                if (!run.FindBoon(BoonIds.Phylactery).Consumed) problems.Add("Phylactery was not used up");

                Hurt(rig, enemy, 500f);
                if (rig.Health.IsAlive) problems.Add("the player survived a third killing blow");
            });

            Test("Cocky", problems, (run, rig) =>
            {
                Take(run, BoonIds.Cocky, 1);
                rig.Health.ConfigureMaxHealth(rig.Sheet.Get(Attr.MaxHealth));
                if (!Approx(rig.Health.Max, 1f)) problems.Add("Cocky left maximum health at " + rig.Health.Max);
                if (!Approx(rig.Sheet.Get(Attr.DamageDealt), 3f)) problems.Add("Cocky set damage dealt to " + rig.Sheet.Get(Attr.DamageDealt));
            });

            Test("Glass Soul", problems, (run, rig) =>
            {
                Take(run, BoonIds.GlassSoul, 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                if (!Approx(rig.Health.Shield, 100f)) problems.Add("Glass Soul started with a shield of " + rig.Health.Shield);
                Hurt(rig, enemy, 30f);
                run.Tick(1f);
                if (!Approx(rig.Health.Shield, 70f)) problems.Add("Glass Soul refilled within 3 s of a hit");
                run.Tick(2.5f);
                if (!Approx(rig.Health.Shield, 100f)) problems.Add("Glass Soul did not refill 3 s after a hit: " + rig.Health.Shield);
            });

            Test("Juggernaut", problems, (run, rig) =>
            {
                float speed = rig.Sheet.Get(Attr.MoveSpeed);
                Take(run, "juggernaut", 1);
                rig.Motor.AddKnockback(Vector3.forward * 10f, null, Team.Enemy);
                if (rig.Motor.Knockback != Vector3.zero) problems.Add("Juggernaut did not stop knockback");
                rig.Status.Apply(StatusLibrary.Frost(), null, Team.Enemy);
                if (rig.Status.Has(StatusId.Frost)) problems.Add("Juggernaut did not stop frost");
                if (!(rig.Sheet.Get(Attr.MoveSpeed) < speed)) problems.Add("Juggernaut did not slow the player");
                run.Unbind();
                if (rig.Motor.KnockbackImmunity != 0 || rig.Status.IsImmune(StatusId.Frost))
                    problems.Add("Juggernaut's immunities outlived the run");
            });

            Test("Every Reaction and Prophecy", problems, (run, rig) =>
            {
                Take(run, "every_reaction", 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                float before = enemy.Health.Current;
                Hurt(rig, enemy, 10f);
                if (!(enemy.Health.Current < before)) problems.Add("Every Reaction did not hurt the attacker");

                DamageInfo tick = DamageInfo.Create(10f, DamageType.Energy, Team.Enemy, enemy.gameObject);
                tick.Origin = DamageOrigin.StatusTick;
                before = enemy.Health.Current;
                rig.Health.TakeDamage(tick);
                if (!Approx(enemy.Health.Current, before)) problems.Add("Every Reaction reflected a status tick");

                Take(run, "prophecy", 1);
                EnemyController fresh = Enemy(rig, Vector3.forward * 7f);
                float health = rig.Health.Current;
                Hurt(rig, fresh, 10f);
                if (!Approx(rig.Health.Current, health)) problems.Add("Prophecy did not make an enemy's first attack miss");
                Hurt(rig, fresh, 10f);
                if (Approx(rig.Health.Current, health)) problems.Add("Prophecy made an enemy's second attack miss too");
                LevelEvents.RaiseFloorEntered(null);
                health = rig.Health.Current;
                Hurt(rig, fresh, 10f);
                if (!Approx(rig.Health.Current, health)) problems.Add("Prophecy did not reset on a new floor");
            });

            Test("Monarch", problems, (run, rig) =>
            {
                float regen = rig.Sheet.Get(Attr.ManaRegen);
                Take(run, "monarch", 1);
                var rule = run.FindBehaviour<MonarchBehaviour>();
                if (!rule.IsActive || !(rig.Sheet.Get(Attr.ManaRegen) > regen)) problems.Add("Monarch is not active at the start");

                EnemyController a = Enemy(rig, Vector3.forward * 5f);
                EnemyController b = Enemy(rig, Vector3.forward * 7f);
                Hurt(rig, a, 5f);
                Hurt(rig, b, 5f);
                if (rule.IsActive || !Approx(rig.Sheet.Get(Attr.ManaRegen), regen)) problems.Add("Monarch stayed active after being hurt");

                DamageInfo nobody = DamageInfo.Create(5f, DamageType.Energy, Team.Enemy, null);
                Kill(a.Health, rig);
                if (rule.IsActive) problems.Add("Monarch returned with one of two attackers alive");
                Kill(b.Health, rig);
                if (!rule.IsActive) problems.Add("Monarch did not return once every attacker died");
                rig.Health.TakeDamage(nobody);
                if (!rule.IsActive) problems.Add("damage with no attacker broke Monarch");
            });

            Test("Mana Shield", problems, (run, rig) =>
            {
                Take(run, "mana_shield", 1);
                rig.Health.ConfigureMaxHealth(rig.Sheet.Get(Attr.MaxHealth));
                if (!Approx(rig.Health.Max, 50f)) problems.Add("Mana Shield left maximum health at " + rig.Health.Max);

                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                float mana = rig.Mana.Current;
                Hurt(rig, enemy, 20f);
                if (!Approx(rig.Health.Current, 40f) || !Approx(rig.Mana.Current, mana - 10f))
                    problems.Add("Mana Shield split a 20 hit into " + (50f - rig.Health.Current) + " health and " + (mana - rig.Mana.Current) + " mana");
            });

            Test("recovery", problems, (run, rig) =>
            {
                Take(run, "rest_a_moment", 1);
                Take(run, "padding", 2);
                rig.Health.SetCurrent(50f);
                LevelEvents.RaiseFloorCompleted(null);
                if (!(rig.Health.Current > 50f)) problems.Add("Rest a Moment did not heal on finishing a floor");
                LevelEvents.RaiseFloorEntered(null);
                if (!Approx(rig.Health.Shield, 24f)) problems.Add("two levels of Padding gave a shield of " + rig.Health.Shield);

                Take(run, "vampire_sight", 1);
                int orbs = OrbPickup.Live.Count;
                Kill(Enemy(rig, Vector3.forward * 5f).Health, rig);
                if (OrbPickup.Live.Count < orbs + 1) problems.Add("Vampire Sight did not drop an orb on a kill");

                Take(run, "bottled_orb", 1);
                rig.Health.SetCurrent(rig.Health.Max);
                var bottle = run.FindBehaviour<BottledOrbBehaviour>();
                OrbPickup orb = OrbPickup.Live[OrbPickup.Live.Count - 1];
                if (OrbPickup.FullHealthCollector == null || !OrbPickup.FullHealthCollector(orb, rig) || bottle.Stored != 1)
                    problems.Add("Bottled Orb did not store an orb at full health");
                if (OrbPickup.FullHealthCollector(orb, rig)) problems.Add("Bottled Orb stored past its level");
                rig.Health.SetCurrent(10f);
                run.Tick(0.1f);
                if (bottle.Stored != 0 || !(rig.Health.Current > 10f)) problems.Add("Bottled Orb did not drink its orb when low");
            });

            Test("shillings", problems, (run, rig) =>
            {
                Take(run, "hoarder", 1);
                int luck = rig.Sheet.GetStat(StatType.Luck);
                run.EarnShillings(250f, ShillingSource.Other);
                if (rig.Sheet.GetStat(StatType.Luck) != luck + 2) problems.Add("Hoarder gave " + (rig.Sheet.GetStat(StatType.Luck) - luck) + " Luck at 250 shillings");

                Take(run, "pickpocket", 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                int coins = ShillingPickup.Live.Count;
                for (int i = 0; i < 5; i++) enemy.Health.TakeDamage(PlayerHit(rig, 1f, DamageOrigin.Melee));
                int dropped = ShillingPickup.Live.Count - coins;
                if (dropped < 1) problems.Add("Pickpocket dropped nothing on a melee hit");
            });

            Test("world", problems, (run, rig) =>
            {
                EnemyController plain = Enemy(rig, Vector3.forward * 9f);
                Take(run, "ghostrealm", 1);
                Take(run, "courageous", 1);
                if (!plain.Health.EtherealByNature) problems.Add("Ghostrealm did not haunt an enemy already on the floor");
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                if (!enemy.Health.EtherealByNature) problems.Add("Ghostrealm did not make a new enemy ethereal");
                if (!(enemy.Health.Max > plain.Health.Max)) problems.Add("Courageous did not strengthen a new enemy");
            });

            Test("Otherworldly Beauty", problems, (run, rig) =>
            {
                Take(run, "otherworldly_beauty", 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                RaiseSawPlayer(enemy);
                if (!enemy.Status.Has(StatusId.Charmed)) problems.Add("Otherworldly Beauty did not charm the first enemy to see the player");
                EnemyController second = Enemy(rig, Vector3.forward * 7f);
                RaiseSawPlayer(second);
                if (second.Status.Has(StatusId.Charmed)) problems.Add("Otherworldly Beauty charmed twice on one floor");
            });
        }

        private static void RaiseSawPlayer(EnemyController enemy)
        {
            var field = typeof(EnemyController).GetField("SawPlayer",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var handler = field != null ? field.GetValue(null) as Action<EnemyController> : null;
            handler?.Invoke(enemy);
        }

        // ---------------------------------------------------------------- Arsenal

        private static void CheckArsenalBehaviours(List<string> problems)
        {
            Test("enchantments", problems, (run, rig) =>
            {
                StatusId[] statuses =
                {
                    StatusId.Burn, StatusId.Bleed, StatusId.Poison, StatusId.Torment, StatusId.Shock, StatusId.Frost,
                    StatusId.Weaken, StatusId.Hex, StatusId.Volatile, StatusId.Gilded, StatusId.Dread
                };
                foreach (StatusId status in statuses)
                {
                    var enchantment = new EnchantmentBehaviour { Status = status };
                    run.LevelBehaviour(enchantment, 1);
                    EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                    foreach (EnchantmentBehaviour live in EnchantmentBehaviour.Active)
                        if (live.Status == status && live.Run == run) live.ApplyTo(enemy.Health, 20f);
                    if (!enemy.Status.Has(status)) problems.Add(status + " Enchantment applied nothing");
                }
            });

            Test("Shock and Burn Enchantment", problems, (run, rig) =>
            {
                Take(run, "shock_enchantment", 1);
                Take(run, "burn_enchantment", 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 6f);

                DamageInfo later = GunHit(rig, 2);
                rig.Weapon.ReportHit(enemy.Health, later, enemy.transform.position, Vector3.up, Vector3.forward, null);
                if (enemy.Status.Has(StatusId.Shock)) problems.Add("Shock Enchantment shocked a round that was not the first after a reload");
                if (!enemy.Status.Has(StatusId.Burn)) problems.Add("Burn Enchantment did not burn on a gun hit");

                DamageInfo first = GunHit(rig, 1);
                rig.Weapon.ReportHit(enemy.Health, first, enemy.transform.position, Vector3.up, Vector3.forward, null);
                if (!enemy.Status.Has(StatusId.Shock)) problems.Add("Shock Enchantment did not shock the first round after a reload");
            });

            Test("class rounds", problems, (run, rig) =>
            {
                WeaponDefinition handgun = Gun(WeaponClass.Handgun);
                if (handgun == null) { problems.Add("no handgun to test class boons with"); return; }
                rig.Weapon.Equip(handgun);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);

                Take(run, "fresh_mag", 1);
                var fresh = run.FindBehaviour<ClassRoundBehaviour>();
                DamageInfo first = GunHit(rig, 1);
                DamageInfo second = GunHit(rig, 2);
                if (!(fresh.OutgoingMultiplier(first, enemy.Health) > 1f) || !Approx(fresh.OutgoingMultiplier(second, enemy.Health), 1f))
                    problems.Add("Fresh Mag did not favour the first round after a reload alone");

                Take(run, "dead_mans_hand", 1);
                var hand = run.FindBehaviour<DeadMansHandBehaviour>();
                rig.Weapon.ReportHit(enemy.Health, second, enemy.transform.position, Vector3.up, Vector3.forward, null);
                rig.Weapon.ReportHit(enemy.Health, second, enemy.transform.position, Vector3.up, Vector3.forward, null);
                if (hand.Streak != 2 || !Approx(hand.OutgoingMultiplier(second, enemy.Health), 1.2f))
                    problems.Add("Dead Man's Hand after two hits has a streak of " + hand.Streak);
                rig.Weapon.ReportMiss(3);
                if (hand.Streak != 0) problems.Add("a miss did not reset Dead Man's Hand");

                WeaponDefinition rifle = Gun(WeaponClass.Rifle);
                if (rifle != null)
                {
                    rig.Weapon.Equip(rifle);
                    Take(run, "pinpoint", 1);
                    var pinpoint = run.FindBehaviour<PinpointBehaviour>();
                    int forcedAt = 0;
                    for (int i = 1; i <= 3; i++)
                    {
                        float chance = 0f;
                        bool forced = false;
                        pinpoint.AdjustCrit(rig.Sheet, enemy.Health, rig.Weapon, ref chance, ref forced);
                        if (forced) forcedAt = i;
                    }
                    if (forcedAt != 3) problems.Add("Pinpoint forced a crit on hit " + forcedAt + ", not the third");
                }
            });

            Test("handling", problems, (run, rig) =>
            {
                Take(run, "third_hand", 1);
                if (rig.Holster.SlotCount != Holster.MaxSlots) problems.Add("Third Hand did not open a third slot");

                WeaponDefinition handgun = Gun(WeaponClass.Handgun);
                rig.Weapon.Equip(handgun);
                Take(run, "gunmage", 1);
                int ammo = rig.Weapon.AmmoInMagazine;
                float mana = rig.Mana.Current;
                rig.Weapon.FireNow();
                if (rig.Weapon.AmmoInMagazine != ammo || !(rig.Mana.Current < mana)) problems.Add("Gunmage did not pay a round with mana");
                PlayerTools.SetMana(rig, 0f);
                rig.Weapon.FireNow();
                if (rig.Weapon.AmmoInMagazine != ammo - 1) problems.Add("Gunmage did not fall back to ammo with no mana");

                Take(run, "lock_and_load", 1);
                ammo = rig.Weapon.AmmoInMagazine;
                Kill(Enemy(rig, Vector3.forward * 5f).Health, rig);
                if (rig.Weapon.AmmoInMagazine != ammo + 1) problems.Add("Lock and Load did not return a round on a kill");

                run.Unbind();
                if (rig.Holster.SlotCount != Holster.BaseSlots || rig.Weapon.RoundPayer != null)
                    problems.Add("Third Hand or Gunmage outlived the run");
            });

            Test("Mirror Barrel", problems, (run, rig) =>
            {
                Take(run, "mirror_barrel", 1);
                var mirror = run.FindBehaviour<MirrorBarrelBehaviour>();
                if (mirror.Phantom == null || !Approx(mirror.Phantom.Weapon.DamageScale, 0.5f) || !mirror.Phantom.FollowsOtherHand)
                    problems.Add("Mirror Barrel did not make a half-strength copy of the other hand");
                run.Unbind();
                if (mirror.Phantom != null) problems.Add("Mirror Barrel's copy outlived the run");
            });

            Test("on-hit", problems, (run, rig) =>
            {
                Take(run, "chain_static", 1);
                EnemyController struck = Enemy(rig, Vector3.forward * 5f);
                EnemyController near = Enemy(rig, Vector3.forward * 8f);
                Physics.SyncTransforms();
                float before = near.Health.Current;
                rig.Weapon.ReportHit(struck.Health, PlayerHit(rig, 20f, DamageOrigin.Gun), struck.transform.position,
                    Vector3.up, Vector3.forward, null);
                if (!(near.Health.Current < before)) problems.Add("Chain Static did not arc to a nearby enemy");

                Take(run, "birthday_party", 1);
                DamageInfo head = PlayerHit(rig, 20f, DamageOrigin.Gun);
                head.IsHeadshot = true;
                before = near.Health.Current;
                rig.Weapon.ReportHit(near.Health, head, near.transform.position, Vector3.up, Vector3.forward, null);
                if (!(near.Health.Current < before)) problems.Add("Birthday Party's headshot did not explode");
                if (!Approx(rig.Health.Current, rig.Health.Max)) problems.Add("a blast hurt the player");
            });
        }

        // ---------------------------------------------------------------- Slots

        private static void CheckSlotBehaviours(List<string> problems)
        {
            Test("cast slots", problems, (run, rig) =>
            {
                Take(run, "esarl", 1);
                if (rig.Book.SlotLevelBonus(1) != 1 || rig.Book.SlotLevelBonus(0) != 0) problems.Add("Esarl did not raise the E slot alone");
                run.Unbind();
                if (rig.Book.SlotLevelBonus(1) != 0) problems.Add("Esarl's bonus outlived the run");
            });

            Test("Twincast", problems, (run, rig) =>
            {
                Take(run, "twincast", 1);
                var twin = run.FindBehaviour<TwincastBehaviour>();
                twin.RollOverride = () => true;
                rig.Book.Bind(PlayerTools.TestSpell("twin", SpellSchool.Elemental), 0);
                if (rig.Book.TryCastSlot(0) != CastOutcome.Cast) problems.Add("the Twincast test spell was not cast");
                else if (twin.Repeats != 1) problems.Add("Twincast repeated a cast " + twin.Repeats + " times on a won roll");
            });

            Test("Spell Magazine", problems, (run, rig) =>
            {
                Take(run, "spell_magazine", 1);
                var magazine = run.FindBehaviour<SpellMagazineBehaviour>();
                rig.Book.Bind(PlayerTools.TestSpell("mag", SpellSchool.Elemental), 0);
                rig.Weapon.Equip(Gun(WeaponClass.Handgun));
                rig.Weapon.SetAmmo(2);
                rig.Weapon.FireNow();
                if (magazine.Casts != 0) problems.Add("Spell Magazine cast before the magazine was empty");
                rig.Weapon.FireNow();
                if (magazine.Casts != 1) problems.Add("Spell Magazine did not cast on the last round");
            });

            Test("movement", problems, (run, rig) =>
            {
                Take(run, "afterimage", 1);
                Take(run, "tailwind", 1);
                var after = run.FindBehaviour<AfterimageBehaviour>();

                rig.Movement.Step(0f, true);
                if (after.LastDecoy == null) { problems.Add("Afterimage left no decoy"); return; }
                bool registered = false;
                foreach (TargetRegistry.Entry entry in TargetRegistry.Minions)
                    registered |= entry.Transform == after.LastDecoy.transform;
                if (!registered) problems.Add("the Afterimage decoy is not a target");

                if (!(rig.Movement.Cooldown > 0f) && !(rig.Motor.DashCharges < rig.Motor.MaxDashCharges))
                    problems.Add("the movement spell had no cost to test Tailwind with");
                Kill(Enemy(rig, Vector3.forward * 5f).Health, rig);
                if (rig.Movement.Cooldown > 0f) problems.Add("Tailwind did not reset the movement cooldown");

                AfterimageDecoy decoy = after.LastDecoy;
                decoy.Health.TakeDamage(DamageInfo.Create(999f, DamageType.True, Team.Enemy, null));
                if (decoy.BlastHits < 0) problems.Add("the Afterimage decoy did not explode when destroyed");
            });

            Test("Gorelust", problems, (run, rig) =>
            {
                Take(run, "gorelust", 1);
                rig.Health.SetCurrent(50f);
                Enemy(rig, Vector3.forward * 5f).Health.TakeDamage(PlayerHit(rig, 20f, DamageOrigin.Melee));
                if (!(rig.Health.Current > 50f)) problems.Add("Gorelust did not heal on a melee hit");
                float health = rig.Health.Current;
                Enemy(rig, Vector3.forward * 7f).Health.TakeDamage(PlayerHit(rig, 20f, DamageOrigin.Gun));
                if (!Approx(rig.Health.Current, health)) problems.Add("Gorelust healed on a gun hit");
            });
        }

        // ---------------------------------------------------------------- Schools

        private static void CheckSchoolBehaviours(List<string> problems)
        {
            Test("school hit statuses", problems, (run, rig) =>
            {
                Spell abyssal = PlayerTools.TestSpell("hit_abyss", SpellSchool.Abyssal);
                Spell divine = PlayerTools.TestSpell("hit_divine", SpellSchool.Divination);
                Take(run, "hemorrhage", 1);
                Take(run, "blinding_light", 1);

                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                enemy.Health.TakeDamage(SpellHit(rig, 5f, abyssal));
                if (!enemy.Status.Has(StatusId.Bleed)) problems.Add("Hemorrhage did not bleed an Abyssal spell's target");
                enemy.Health.TakeDamage(SpellHit(rig, 5f, divine));
                if (enemy.Status.Has(StatusId.Blind) == false) problems.Add("Blinding Light did not blind");
                enemy.Status.Remove(StatusId.Blind);
                enemy.Health.TakeDamage(SpellHit(rig, 5f, divine));
                if (enemy.Status.Has(StatusId.Blind)) problems.Add("Blinding Light blinded again inside its cooldown");

                EnemyController other = Enemy(rig, Vector3.forward * 7f);
                other.Health.TakeDamage(PlayerHit(rig, 5f, DamageOrigin.Gun));
                if (other.Status.Has(StatusId.Bleed)) problems.Add("Hemorrhage bled a gun hit");
            });

            Test("Elemental", problems, (run, rig) =>
            {
                // No Elemental spell is bound, so Conflux has no rank and cannot react the elements away.
                Spell fire = PlayerTools.TestSpell("elem_fire", SpellSchool.Elemental);
                Take(run, "tri_attuned", 1);
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                enemy.Status.Apply(StatusLibrary.Burn(), rig.gameObject, Team.Player);
                enemy.Status.Apply(StatusLibrary.Frost(stacks: 2), rig.gameObject, Team.Player);
                var tri = run.FindBehaviour<TriAttunedBehaviour>();
                if (!Approx(tri.OutgoingMultiplier(SpellHit(rig, 1f, fire), enemy.Health), 1.2f))
                    problems.Add("Tri-Attuned with two elements gave " + tri.OutgoingMultiplier(SpellHit(rig, 1f, fire), enemy.Health));

                Take(run, "excess_force", 1);
                EnemyController near = Enemy(rig, Vector3.forward * 8f);
                Physics.SyncTransforms();
                Kill(enemy.Health, rig);
                if (!near.Status.Has(StatusId.Burn)) problems.Add("Excess Force did not pass burn to a nearby enemy");

                EnemyController foreign = Enemy(rig, Vector3.forward * 20f);
                EnemyController beside = Enemy(rig, Vector3.forward * 22f);
                Physics.SyncTransforms();
                foreign.Status.Apply(StatusLibrary.Shock(), foreign.gameObject, Team.Enemy);
                Kill(foreign.Health, rig);
                if (beside.Status.Has(StatusId.Shock)) problems.Add("Excess Force passed on a shock the player did not apply");
            });

            Test("Bestial", problems, (run, rig) =>
            {
                rig.Book.Bind(PlayerTools.TestSpell("beast", SpellSchool.Bestial), 0);
                BeastMastery beast = rig.Masteries.Get<BeastMastery>();
                if (beast.Companion == null) beast.Resummon();
                if (beast.Companion == null) { problems.Add("no companion to test the Bestial boons with"); return; }
                beast.Companion.GetComponent<Health>().DestroyOnDeath = false;

                Take(run, "vengeful_rage", 1);
                Take(run, "blooded", 1);
                Take(run, "burn_enchantment", 1);
                Take(run, "shared_instinct", 1);

                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                DamageInfo bite = DamageInfo.Create(10f, DamageType.Kinetic, Team.Player, beast.Companion.gameObject);
                bite.Origin = DamageOrigin.Minion;
                enemy.Health.TakeDamage(bite);
                if (!enemy.Status.Has(StatusId.Burn)) problems.Add("Shared Instinct did not carry Burn Enchantment on a companion bite");

                beast.Companion.GetComponent<Health>().TakeDamage(DamageInfo.Create(99999f, DamageType.True, Team.Enemy, null));
                if (!rig.Status.Has(StatusId.Empowered)) problems.Add("Vengeful Rage did not empower the player when the companion died");
            });

            Test("Abyssal", problems, (run, rig) =>
            {
                rig.Book.Bind(PlayerTools.TestSpell("abyss", SpellSchool.Abyssal), 0);
                BloodDebtMastery debt = rig.Masteries.Get<BloodDebtMastery>();
                Take(run, "foreclosure", 1);
                Take(run, "tidal_surge", 1);
                Take(run, "riding_the_current", 1);

                debt.Record(60f);
                float speed = rig.Sheet.Get(Attr.MoveSpeed);
                run.Tick(0.1f);
                if (!(rig.Sheet.Get(Attr.MoveSpeed) > speed)) problems.Add("Riding the Current did not speed up a player in debt");

                EnemyController victim = Enemy(rig, Vector3.forward * 5f);
                debt.RepayOnKill(victim.Health);
                if (run.FindBehaviour<TidalSurgeBehaviour>().Waves != 1) problems.Add("Tidal Surge did not send a wave on a repayment");
                if (run.FindBehaviour<ForeclosureBehaviour>().Blasts != 0) problems.Add("Foreclosure exploded before the debt was cleared");
                while (debt.Debt > 0f) debt.RepayOnKill(victim.Health);
                if (run.FindBehaviour<ForeclosureBehaviour>().Blasts != 1) problems.Add("Foreclosure did not explode when the debt was cleared");
            });

            Test("Divination", problems, (run, rig) =>
            {
                Spell divine = PlayerTools.TestSpell("guide", SpellSchool.Divination);
                rig.Book.Bind(divine, 0);
                Take(run, "guided_shots", 1);
                var guided = run.FindBehaviour<GuidedShotsBehaviour>();
                EnemyController enemy = Enemy(rig, Vector3.forward * 5f);
                DamageInfo head = PlayerHit(rig, 1f, DamageOrigin.Gun);
                head.IsHeadshot = true;
                if (!Approx(guided.OutgoingMultiplier(head, enemy.Health), 1f)) problems.Add("Guided Shots boosted an unmarked enemy");
                enemy.Health.TakeDamage(SpellHit(rig, 1f, divine));
                if (!(guided.OutgoingMultiplier(head, enemy.Health) > 1f)) problems.Add("Guided Shots did not boost a headshot after a Divination hit");
                run.Tick(10f);
                if (!Approx(guided.OutgoingMultiplier(head, enemy.Health), 1f)) problems.Add("Guided Shots lasted past its window");
            });

            Test("Death", problems, (run, rig) =>
            {
                rig.Book.Bind(PlayerTools.TestSpell("death", SpellSchool.Death), 0);
                SoulsMastery souls = rig.Masteries.Get<SoulsMastery>();
                Take(run, "grave_hunger", 1);
                var hunger = run.FindBehaviour<GraveHungerBehaviour>();

                // What a kill gives without the boon's soul, then with it; the cap is two, so each starts from none.
                hunger.RollOverride = () => false;
                souls.SpendAll();
                Kill(Enemy(rig, Vector3.forward * 5f).Health, rig);
                int plain = souls.Souls;
                hunger.RollOverride = () => true;
                souls.SpendAll();
                Kill(Enemy(rig, Vector3.forward * 6f).Health, rig);
                if (souls.Souls != plain + 1) problems.Add("Grave Hunger's won roll left " + souls.Souls + " souls, not " + (plain + 1));

                Take(run, "soul_slave", 1);
                Enemy(rig, Vector3.forward * 9f);
                souls.TrySpend(1);
                if (run.FindBehaviour<SoulSlaveBehaviour>().Ghosts != 1) problems.Add("Soul Slave sent no ghost for a spent soul");
            });

            Test("Psionic", problems, (run, rig) =>
            {
                rig.Book.Bind(PlayerTools.TestSpell("psi_a", SpellSchool.Psionic), 0);
                rig.Book.Bind(PlayerTools.TestSpell("psi_b", SpellSchool.Psionic), 1);
                PsiBladesMastery psi = rig.Masteries.Get<PsiBladesMastery>();
                Take(run, "overflow", 1);
                Take(run, "thrown_blade", 1);
                var overflow = run.FindBehaviour<OverflowBehaviour>();

                psi.AddBonus(10f);
                if (overflow.Active) problems.Add("Overflow was active before any psi was spent");
                var blade = run.FindBehaviour<ThrownBladeBehaviour>();
                if (rig.CombatInput.MeleeOverride == null || !rig.CombatInput.MeleeOverride(null, Vector3.forward))
                    problems.Add("Thrown Blade did not throw with full charges");
                if (blade.Throws != 1 || psi.Charge > 0.001f) problems.Add("Thrown Blade did not spend every charge");
                if (!overflow.Active) problems.Add("spending psi did not start Overflow");
                if (rig.CombatInput.MeleeOverride(null, Vector3.forward)) problems.Add("Thrown Blade threw with no charges");
            });

            Test("Aetherics", problems, (run, rig) =>
            {
                Take(run, "void_rounds", 1);
                Take(run, "empty_vessel", 1);
                float speed = rig.Sheet.Get(Attr.MoveSpeed);
                run.Tick(0.1f);
                if (!Approx(rig.Sheet.Get(Attr.Pierce), 0f)) problems.Add("Void Rounds pierced at full mana");
                PlayerTools.SetMana(rig, 20f);
                run.Tick(0.1f);
                if (!Approx(rig.Sheet.Get(Attr.Pierce), 1f)) problems.Add("Void Rounds did not pierce below half mana");
                if (!(rig.Sheet.Get(Attr.MoveSpeed) > speed)) problems.Add("Empty Vessel did not speed up an emptier player");
                PlayerTools.SetMana(rig, 100f);
                run.Tick(0.1f);
                if (!Approx(rig.Sheet.Get(Attr.Pierce), 0f)) problems.Add("Void Rounds kept piercing after mana refilled");
            });
        }

        // ---------------------------------------------------------------- harness

        /// <summary>A fresh player and run for one check, torn down whatever happens.</summary>
        private static void Test(string name, List<string> problems, Action<RunState, PlayerRig> body)
        {
            var rigs = new List<PlayerRig>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            Vector3 at = new Vector3(8000f + 300f * (_slot % 40), 0f, 300f * (_slot / 40));
            _slot++;

            RunState run = null;
            PlayerRig rig = null;
            try
            {
                CombatRules.Clear();
                rig = PlayerTools.MakeRig(at, rigs);
                run = new RunState(_seed++, 3);
                run.Bind(rig);
                body(run, rig);
            }
            catch (Exception e)
            {
                problems.Add(name + " threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                try { run?.Unbind(); }
                catch (Exception e) { problems.Add(name + " threw ending the run: " + e.Message); }

                foreach (PlayerRig r in rigs)
                    if (r != null) r.DetachPlayerSystems();

                OrbPickup.FullHealthCollector = null;
                TargetRegistry.Clear();
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }
        }

        private static void Take(RunState run, string id, int levels)
        {
            Boon boon = BoonLibrary.Get(id);
            if (boon == null) throw new InvalidOperationException("no boon \"" + id + "\"");
            for (int i = 0; i < levels; i++) run.AddBoon(boon);
        }

        private static EnemyController Enemy(PlayerRig rig, Vector3 offset, bool elite = false)
        {
            EnemyController enemy = EnemyFactory.Spawn("cultist", rig.transform.position + offset, 1, elite);
            enemy.Health.DestroyOnDeath = false;

            // What Awake would have done in play: without it the enemy has no health and dies to its first hit.
            typeof(Health).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                                              | System.Reflection.BindingFlags.Public)?.Invoke(enemy.Health, null);
            Physics.SyncTransforms();
            return enemy;
        }

        private static WeaponDefinition Gun(WeaponClass weaponClass)
        {
            WeaponDefinition any = null;
            foreach (WeaponDefinition def in WeaponLibrary.All)
            {
                if (def.Class != weaponClass) continue;
                if (def.ManaPerShot <= 0f) return def;
                if (any == null) any = def;
            }
            return any;
        }

        private static DamageInfo PlayerHit(PlayerRig rig, float amount, DamageOrigin origin)
        {
            DamageInfo info = DamageInfo.Create(amount, DamageType.Energy, Team.Player, rig.gameObject);
            info.Origin = origin;
            info.CanCrit = false;
            return info;
        }

        private static DamageInfo GunHit(PlayerRig rig, int sinceReload)
        {
            DamageInfo info = PlayerHit(rig, 10f, DamageOrigin.Gun);
            info.Weapon = rig.Weapon;
            info.Shot.SinceReload = sinceReload;
            info.Shot.SinceDraw = sinceReload;
            return info;
        }

        private static DamageInfo SpellHit(PlayerRig rig, float amount, Spell spell)
        {
            DamageInfo info = PlayerHit(rig, amount, DamageOrigin.Spell);
            info.Spell = spell;
            return info;
        }

        private static void Hurt(PlayerRig rig, EnemyController enemy, float amount)
        {
            DamageInfo info = DamageInfo.Create(amount, DamageType.Kinetic, Team.Enemy, enemy != null ? enemy.gameObject : null);
            info.Origin = DamageOrigin.Attack;
            info.CanCrit = false;
            rig.Health.TakeDamage(info);
        }

        private static void Kill(Health health, PlayerRig rig)
        {
            DamageInfo info = DamageInfo.Create(99999f, DamageType.True, Team.Player, rig.gameObject);
            info.CanCrit = false;
            health.TakeDamage(info);
        }

        private static bool Approx(float a, float b) => Mathf.Abs(a - b) <= Tolerance;
    }
}
#endif
