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
    /// Exercises the player systems against a real player rig: spell levels and offers, the masteries,
    /// costs and cast rules, the activation modes, the motor's verbs, hiding, possession and the weapon
    /// extensions.
    ///
    /// Edit mode never calls Awake, so the rig's components that set themselves up there are woken by
    /// hand before anything is checked.
    /// </summary>
    public static class PlayerTools
    {
        private const float Tolerance = 0.01f;

        [MenuItem("Gunspire/Verify Player Systems")]
        public static void VerifyPlayerSystems()
        {
            var problems = new List<string>();
            var rigs = new List<PlayerRig>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

            // Passing through enemies is set on the collision matrix, which the editor keeps in project
            // settings, so the matrix is put back afterwards.
            bool[,] matrixBefore = ReadCollisionMatrix();

            try
            {
                Layers.ConfigureCollisionMatrix();
                WorldClock.Reset();
                TargetRegistry.Clear();

                CheckLevelsAndOffers(problems, rigs);
                CheckMasteries(problems, rigs);
                CheckCostsAndRules(problems, rigs);
                CheckActivationModes(problems, rigs);
                CheckMotor(problems, rigs);
                CheckHiding(problems, rigs);
                CheckPossession(problems, rigs);
                CheckWeaponExtensions(problems, rigs);
            }
            catch (System.Exception e)
            {
                problems.Add("the check itself threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
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

            if (problems.Count == 0)
            {
                Debug.Log("Player systems: levels and offers, masteries, costs, activation modes, the motor, "
                          + "hiding, possession and weapon extensions all behave as specified.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Player systems: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 40; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- 4.1 slots and levels

        private static void CheckLevelsAndOffers(List<string> problems, List<PlayerRig> rigs)
        {
            var none = new HashSet<string>();
            PlayerRig rig = MakeRig(new Vector3(2000f, 0f, 0f), rigs);

            Spell dash = SpellLibrary.Get(SpellLibrary.DefaultMovementId);
            if (dash == null) problems.Add("no default movement spell to test levels with");
            else
            {
                if (dash.MaxLevel < 2) problems.Add("dash has a level cap of " + dash.MaxLevel + ", so its charges can never grow");

                rig.Movement.Equip(dash);
                if (rig.Book.GetLevel(dash) != 1) problems.Add("an equipped movement spell is at level " + rig.Book.GetLevel(dash) + ", not 1");
                if (!rig.Book.IsEquipped(dash)) problems.Add("the book does not know the movement spell is equipped");

                if (dash.MaxLevel > 1 && !SpellLibrary.Offerable(rig.Book, SpellSlot.Movement, none).Contains(dash))
                    problems.Add("the equipped dash below its cap is not offered to level up");

                int charges = rig.Motor.MaxDashCharges;
                rig.Book.LevelUp(dash);
                rig.Movement.Step(0f, false);
                if (dash.MaxLevel > 1 && rig.Motor.MaxDashCharges != charges + 1)
                    problems.Add("dash at level 2 has " + rig.Motor.MaxDashCharges + " charges, not " + (charges + 1));

                while (rig.Book.CanTake(dash)) rig.Book.LevelUp(dash);
                if (SpellLibrary.Offerable(rig.Book, SpellSlot.Movement, none).Contains(dash))
                    problems.Add("the equipped dash at its level cap is still offered");
            }

            Spell bash = SpellLibrary.Get(SpellLibrary.DefaultMeleeId);
            if (bash != null)
            {
                rig.CombatInput.EquipMelee(bash);
                if (rig.Book.GetLevel(bash) != 1 || rig.CombatInput.MeleeLevel != 1)
                    problems.Add("an equipped melee spell is not tracked at level 1");
            }

            var eliminated = new HashSet<string> { "blink" };
            if (SpellLibrary.Offerable(null, SpellSlot.Movement, eliminated).Exists(s => s.Id == "blink"))
                problems.Add("an eliminated spell is still offered");

            // Variants: only one version of a group equipped.
            Spell formA = TestSpell("form_a", SpellSchool.Bestial);
            Spell formB = TestSpell("form_b", SpellSchool.Bestial);
            formA.VariantGroup = formB.VariantGroup = "test_form";

            rig.Book.Bind(formA, 0);
            if (!rig.Book.HasOtherVariant(formB)) problems.Add("an equipped variant does not keep its siblings out of offers");
            rig.Book.Bind(formB, 1);
            if (rig.Book.GetSlot(0) != null) problems.Add("binding a second version of a variant left the first equipped");
            rig.Book.Bind(null, 1);

            // One Petty spell per offer while school spells remain; Petty fills in once they run out.
            var pool = new List<Spell>();
            for (int i = 0; i < 5; i++) pool.Add(TestSpell("petty_" + i, SpellSchool.Petty));
            for (int i = 0; i < 5; i++) pool.Add(TestSpell("school_" + i, SpellSchool.Death));

            var rng = new Rng(1234);
            bool tooMany = false, tooFew = false;
            for (int trial = 0; trial < 200; trial++)
            {
                List<Spell> offer = SpellLibrary.OfferDistinct(rng, pool, CharacterSheet.Baseline, 3);
                if (offer.Count != 3) tooFew = true;
                if (offer.FindAll(s => s.School == SpellSchool.Petty).Count > 1) tooMany = true;
            }
            if (tooMany) problems.Add("an offer held more than one Petty spell while school spells were left");
            if (tooFew) problems.Add("the one-Petty rule shrank an offer below three with nine spells to choose from");

            List<Spell> onlyPetty = SpellLibrary.OfferDistinct(rng, pool.FindAll(s => s.School == SpellSchool.Petty),
                CharacterSheet.Baseline, 3);
            if (onlyPetty.Count != 3)
                problems.Add("with only Petty spells to offer, an offer held " + onlyPetty.Count + " cards, not 3");
        }

        // ---------------------------------------------------------------- 4.2 masteries

        private static void CheckMasteries(List<string> problems, List<PlayerRig> rigs)
        {
            var equipped = new List<Spell>
            {
                TestSpell("d1", SpellSchool.Death), TestSpell("d2", SpellSchool.Death), TestSpell("p1", SpellSchool.Petty),
                TestSpell("d3", SpellSchool.Death), TestSpell("d4", SpellSchool.Death), TestSpell("d5", SpellSchool.Death)
            };
            if (Masteries.Count(equipped, SpellSchool.Death) != 4)
                problems.Add("five Death spells counted as " + Masteries.Count(equipped, SpellSchool.Death) + ", not the cap of 4");
            if (Masteries.Count(equipped, SpellSchool.Petty) != 0) problems.Add("Petty spells count toward a mastery");

            CheckSouls(problems, rigs);
            CheckPsiBlades(problems, rigs);
            CheckArcaneWarp(problems, rigs);
            CheckBloodDebt(problems, rigs);
            CheckConflux(problems, rigs);
            CheckDivineKnowledge(problems, rigs);
            CheckBeasts(problems, rigs);
        }

        private static void CheckSouls(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(2100f, 0f, 0f), rigs);
            SoulsMastery souls = rig.Masteries.Get<SoulsMastery>();

            rig.Book.Bind(TestSpell("death_a", SpellSchool.Death), 0);
            rig.Book.Bind(TestSpell("death_b", SpellSchool.Death), 1);

            if (souls.Rank != 2) problems.Add("two equipped Death spells ranked Souls at " + souls.Rank + ", not 2");
            if (souls.Cap != 3) problems.Add("two Death spells gave a soul cap of " + souls.Cap + ", not 3");

            for (int i = 0; i < 5; i++) Victim(new Vector3(2100f, 0f, 30f)).Kill();
            if (souls.Souls != 3) problems.Add("five kills at a cap of 3 left " + souls.Souls + " souls");

            rig.Book.Bind(null, 1);
            if (souls.Souls != 2) problems.Add("lowering the soul cap to 2 left " + souls.Souls + " souls; the excess should be lost");
            if (souls.TrySpend(3)) problems.Add("spending three souls while holding two succeeded");
            if (!souls.TrySpend(2) || souls.Souls != 0) problems.Add("spending two souls while holding two did not empty the counter");
        }

        private static void CheckPsiBlades(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(2200f, 0f, 0f), rigs);
            PsiBladesMastery psi = rig.Masteries.Get<PsiBladesMastery>();

            rig.Book.Bind(TestSpell("psi_a", SpellSchool.Psionic), 0);
            if (psi.Max != 2) problems.Add("one Psionic spell gave a psi maximum of " + psi.Max + ", not 2");

            psi.AddFromHit(10f);
            psi.AddFromHit(10.1f);
            if (!Approx(psi.Charge, PsiBladesMastery.ChargePerHit))
                problems.Add("two bullet hits a tenth of a second apart gave " + psi.Charge + " charge; the second is inside the quarter-second limit");

            psi.AddFromHit(10.3f);
            psi.AddBonus(1f);
            if (!Approx(psi.Charge, 1.5f)) problems.Add("bonus charge was held to the rate limit (charge " + psi.Charge + ", expected 1.5)");

            for (int i = 0; i < 20; i++) psi.AddFromHit(20f + i);
            if (!Approx(psi.Charge, 2f)) problems.Add("psi charge went to " + psi.Charge + ", past its maximum of 2");

            // A swing with charge spends one and adds psychic damage to what it strikes.
            Physics.SyncTransforms();
            Health dummy = Subject("PsiDummy", Team.Enemy, rig.transform.position + new Vector3(0f, 0f, 2f),
                Layers.Enemy, collider: true);
            Physics.SyncTransforms();

            bool bonusLanded = false;
            dummy.Damaged += (info, amount) =>
            {
                if (info.Origin == DamageOrigin.Mastery && info.Type == DamageType.Psychic) bonusLanded = true;
            };

            float chargeBefore = psi.Charge;
            CastOutcome swing = rig.CombatInput.TryCastMelee();
            if (swing != CastOutcome.Cast) problems.Add("a melee swing for Psi Blades did not happen: " + swing);
            else
            {
                if (!Approx(psi.Charge, chargeBefore - 1f)) problems.Add("a melee attack with charge left " + psi.Charge + " charge; it should spend one");
                if (!bonusLanded) problems.Add("a charged melee attack dealt no bonus psychic damage");
            }
        }

        private static void CheckArcaneWarp(List<string> problems, List<PlayerRig> rigs)
        {
            if (!Approx(ArcaneWarpMastery.RateFor(1), 0.005f) || !Approx(ArcaneWarpMastery.RateFor(2), 0.0075f)
                || !Approx(ArcaneWarpMastery.RateFor(3), 0.01f) || !Approx(ArcaneWarpMastery.RateFor(4), 0.0125f))
                problems.Add("Arcane Warp's rates per missing mana point are not 0.5%, 0.75%, 1% and 1.25%");

            var gun = new WeaponDefinition { RoundsPerMinute = 300f, PelletsPerShot = 4, Mode = FireMode.Semi };
            if (!Approx(ArcaneWarpMastery.ManaPerRound(gun), 0.5f))
                problems.Add("a four-pellet gun at 300 rounds a minute restores " + ArcaneWarpMastery.ManaPerRound(gun) + " per pellet, not 0.5");

            PlayerRig rig = MakeRig(new Vector3(2300f, 0f, 0f), rigs);
            ArcaneWarpMastery warp = rig.Masteries.Get<ArcaneWarpMastery>();

            for (int i = 0; i < 3; i++) rig.Book.Bind(TestSpell("aeth_" + i, SpellSchool.Aetherics), i);
            Spell blink = SpellLibrary.Get("blink");
            if (blink != null && blink.School == SpellSchool.Aetherics) rig.Movement.Equip(blink);
            else rig.Movement.Equip(TestMovement("aeth_move", SpellSchool.Aetherics));

            if (warp.Rank != 4) problems.Add("four equipped Aetherics spells ranked Arcane Warp at " + warp.Rank);

            SetMana(rig, 100f);
            float attackFull = rig.Sheet.Get(Attr.AttackSpeed);
            float reloadFull = rig.Sheet.Get(Attr.ReloadSpeed);

            SetMana(rig, 80f);
            float expected = 1f + 20f * ArcaneWarpMastery.RateFor(4);
            if (!Approx(rig.Sheet.Get(Attr.AttackSpeed) / attackFull, expected))
                problems.Add("20 missing mana at four spells scaled attack speed by " + rig.Sheet.Get(Attr.AttackSpeed) / attackFull + ", not " + expected);
            if (!Approx(rig.Sheet.Get(Attr.ReloadSpeed) / reloadFull, expected))
                problems.Add("20 missing mana at four spells scaled reload speed by " + rig.Sheet.Get(Attr.ReloadSpeed) / reloadFull + ", not " + expected);

            SetMana(rig, 50f);
            float share = ArcaneWarpMastery.ManaPerRound(rig.Weapon.Definition);
            Health target = Victim(new Vector3(2300f, 0f, 30f));
            DamageInfo hit = DamageInfo.Create(1f, DamageType.Kinetic, Team.Player, rig.gameObject);

            rig.Weapon.ReportHit(target, hit, Vector3.zero, Vector3.up, Vector3.forward, null, round: 7);
            if (!Approx(rig.Mana.Current, 50f + share)) problems.Add("a round landing restored " + (rig.Mana.Current - 50f) + " mana, not " + share);

            rig.Weapon.ReportHit(target, hit, Vector3.zero, Vector3.up, Vector3.forward, null, round: 7);
            rig.Weapon.ReportHit(target, hit, Vector3.zero, Vector3.up, Vector3.forward, null, round: 8, echo: true);
            if (!Approx(rig.Mana.Current, 50f + share)) problems.Add("a round striking twice, or an echoed round, restored mana again");
        }

        private static void CheckBloodDebt(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(2400f, 0f, 0f), rigs);
            BloodDebtMastery debt = rig.Masteries.Get<BloodDebtMastery>();
            float basePower = rig.Sheet.Get(Attr.SpellPower);

            rig.Book.Bind(TestSpell("abyss_1", SpellSchool.Abyssal), 0);

            if (!SpellCosts.PayHealth(rig.Health, 30f)) problems.Add("a 30 health cost at full health was refused");
            if (!Approx(debt.Debt, 30f)) problems.Add("paying 30 health with the Blood Debt held left " + debt.Debt + " debt");

            if (SpellCosts.PayHealth(rig.Health, 70f)) problems.Add("a health cost that would leave less than one hit point was paid");
            if (!Approx(rig.Health.Current, 70f)) problems.Add("a refused health cost still took health (" + rig.Health.Current + " left)");

            Victim(new Vector3(2400f, 0f, 30f)).Kill();
            if (!Approx(debt.Debt, 10f) || !Approx(rig.Health.Current, 90f))
                problems.Add("a kill at one Abyssal spell left " + debt.Debt + " debt and " + rig.Health.Current + " health, not 10 and 90");

            rig.Book.Bind(TestSpell("abyss_2", SpellSchool.Abyssal), 1);
            SpellCosts.PayHealth(rig.Health, 40f);
            Victim(new Vector3(2400f, 0f, 32f)).Kill();
            float interestHealth = 50f + BloodDebtMastery.RepayPerKill * (1f + BloodDebtMastery.Interest);
            if (!Approx(rig.Health.Current, interestHealth))
                problems.Add("a kill at two Abyssal spells healed to " + rig.Health.Current + ", not " + interestHealth + " with interest");

            rig.Book.Bind(TestSpell("abyss_3", SpellSchool.Abyssal), 2);
            float expectedPower = basePower * (1f + debt.Debt * BloodDebtMastery.SpellPowerPerDebt);
            if (!Approx(rig.Sheet.Get(Attr.SpellPower), expectedPower))
                problems.Add("carrying " + debt.Debt + " debt at three spells gave spell power " + rig.Sheet.Get(Attr.SpellPower) + ", not " + expectedPower);

            rig.Movement.Equip(TestMovement("abyss_move", SpellSchool.Abyssal));
            rig.Health.Heal(rig.Health.Max);
            Victim(new Vector3(2400f, 0f, 34f)).Kill();
            if (rig.Health.Shield <= 0f) problems.Add("repayment at full health with four Abyssal spells gave no shield");

            float shield = rig.Health.Shield;
            rig.Health.TakeDamage(DamageInfo.Create(Mathf.Min(10f, shield), DamageType.True, Team.Enemy, null));
            if (!Approx(rig.Health.Current, rig.Health.Max) || !Approx(rig.Health.Shield, shield - Mathf.Min(10f, shield)))
                problems.Add("damage reached health before the shield was spent");
        }

        private static void CheckConflux(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(2500f, 0f, 0f), rigs);
            ConfluxMastery conflux = rig.Masteries.Get<ConfluxMastery>();
            GameObject player = rig.gameObject;

            rig.Book.Bind(TestSpell("elem_1", SpellSchool.Elemental), 0);

            Health enemy = Subject("ConfluxA", Team.Enemy, new Vector3(2500f, 0f, 40f), Layers.Enemy, collider: true);
            StatusController status = enemy.GetComponent<StatusController>();

            status.Apply(StatusLibrary.Burn(amount: 10f), player, Team.Player);
            float before = enemy.Current;
            status.Apply(StatusLibrary.Frost(stacks: 10), player, Team.Player);
            if (!Approx(before - enemy.Current, ConfluxMastery.BurstDamage))
                problems.Add("a second element on an enemy dealt " + (before - enemy.Current) + ", not the rank-one burst of " + ConfluxMastery.BurstDamage);

            before = enemy.Current;
            status.Apply(StatusLibrary.Shock(), player, Team.Player);
            if (!Approx(before, enemy.Current)) problems.Add("an enemy reacted twice inside its reaction cooldown");

            Health foreign = Subject("ConfluxEnemySource", Team.Enemy, new Vector3(2510f, 0f, 40f));
            StatusController foreignStatus = foreign.GetComponent<StatusController>();
            foreignStatus.Apply(StatusLibrary.Burn(amount: 10f), null, Team.Enemy);
            foreignStatus.Apply(StatusLibrary.Frost(stacks: 10), null, Team.Enemy);
            if (!Approx(foreign.Current, 1000f)) problems.Add("elements an enemy applied set off a reaction");

            // Three elements at three spells detonate across an area and are consumed.
            rig.Book.Bind(TestSpell("elem_2", SpellSchool.Elemental), 1);
            rig.Book.Bind(TestSpell("elem_3", SpellSchool.Elemental), 2);

            Health centre = Subject("ConfluxCentre", Team.Enemy, new Vector3(2530f, 0f, 40f), Layers.Enemy, collider: true);
            Health neighbour = Subject("ConfluxNeighbour", Team.Enemy, new Vector3(2531.5f, 0f, 40f), Layers.Enemy, collider: true);
            Physics.SyncTransforms();
            StatusController centreStatus = centre.GetComponent<StatusController>();

            centreStatus.Apply(StatusLibrary.Burn(amount: 10f), player, Team.Player);
            centreStatus.Apply(StatusLibrary.Frost(stacks: 10), player, Team.Player);
            WorldClock.Tick(ConfluxMastery.ReactionCooldown + 0.1f);

            int detonations = conflux.Detonations;
            centreStatus.Apply(StatusLibrary.Shock(), player, Team.Player);

            if (conflux.Detonations != detonations + 1) problems.Add("a third element at three Elemental spells did not detonate");
            if (neighbour.Current >= 1000f) problems.Add("a detonation did not reach the enemy beside it");
            if (centreStatus.Has(StatusId.Burn) || centreStatus.Has(StatusId.Frost) || centreStatus.Has(StatusId.Shock))
                problems.Add("a detonation left its elements on the enemy that set it off");

            // At four, the detonation puts its element on everything it catches.
            rig.Movement.Equip(TestMovement("elem_move", SpellSchool.Elemental));

            Health centre4 = Subject("ConfluxCentre4", Team.Enemy, new Vector3(2560f, 0f, 40f), Layers.Enemy, collider: true);
            Health neighbour4 = Subject("ConfluxNeighbour4", Team.Enemy, new Vector3(2561.5f, 0f, 40f), Layers.Enemy, collider: true);
            Physics.SyncTransforms();
            StatusController centre4Status = centre4.GetComponent<StatusController>();

            centre4Status.Apply(StatusLibrary.Burn(amount: 10f), player, Team.Player);
            centre4Status.Apply(StatusLibrary.Frost(stacks: 10), player, Team.Player);
            WorldClock.Tick(ConfluxMastery.ReactionCooldown + 0.1f);
            centre4Status.Apply(StatusLibrary.Shock(), player, Team.Player);

            if (!neighbour4.GetComponent<StatusController>().Has(StatusId.Shock))
                problems.Add("a detonation at four Elemental spells did not put its element on what it caught");
        }

        private static void CheckDivineKnowledge(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(2600f, 0f, 0f), rigs);
            DivineKnowledgeMastery knowledge = rig.Masteries.Get<DivineKnowledgeMastery>();

            rig.Book.Bind(TestSpell("div_1", SpellSchool.Divination), 0);
            if (!knowledge.ShowsHealthBars || knowledge.ShowsPerception) problems.Add("one Divination spell should show health bars and nothing more");

            rig.Book.Bind(TestSpell("div_2", SpellSchool.Divination), 1);
            if (!knowledge.ShowsPerception || knowledge.ShowsAttackTimers) problems.Add("two Divination spells should add perception and not attack timers");

            rig.Book.Bind(TestSpell("div_3", SpellSchool.Divination), 2);
            if (!knowledge.ShowsAttackTimers) problems.Add("three Divination spells do not show attack timers");

            EnemyController enemy = SpawnEnemy("cultist", new Vector3(2600f, 0f, 10f));
            foreach (AbilityAttack attack in enemy.GetComponents<AbilityAttack>()) attack.Initialise(enemy);

            var readouts = new List<DivineKnowledgeMastery.Readout>();
            var enemies = new List<EnemyController> { enemy };

            knowledge.Collect(rig.transform.position, readouts, enemies);
            if (readouts.Count != 1) problems.Add("Divine Knowledge read " + readouts.Count + " enemies, not the one in range");
            else
            {
                if (!readouts[0].ShowPerception) problems.Add("an enemy that has noticed nothing did not show its senses");

                AbilityAttack first = enemy.GetComponent<AbilityAttack>();
                if (first != null && first.CooldownRemaining > 0f && readouts[0].NextAttackIn <= 0f)
                    problems.Add("an enemy with its attacks cooling down showed no attack timer");
            }

            enemy.Alert();
            knowledge.Collect(rig.transform.position, readouts, enemies);
            if (readouts.Count == 1 && readouts[0].ShowPerception) problems.Add("an alerted enemy still showed its senses");
        }

        private static void CheckBeasts(List<string> problems, List<PlayerRig> rigs)
        {
            string[] ladder = { null, "jackalope", "fox", "wolf", "bear" };
            for (int rank = 0; rank < ladder.Length; rank++)
            {
                if (BeastMastery.TierFor(rank) != ladder[rank])
                    problems.Add("Bestial rank " + rank + " summons " + BeastMastery.TierFor(rank) + ", not " + ladder[rank]);
                if (ladder[rank] != null && MinionLibrary.Get(ladder[rank]) == null)
                    problems.Add("the Bestial ladder names " + ladder[rank] + ", which is not a minion");
            }

            PlayerRig rig = MakeRig(new Vector3(2700f, 0f, 0f), rigs);
            BeastMastery beast = rig.Masteries.Get<BeastMastery>();

            rig.Book.Bind(TestSpell("beast_1", SpellSchool.Bestial), 0);
            if (beast.Companion == null || beast.Companion.Definition.Id != "jackalope")
                problems.Add("one Bestial spell did not summon a jackalope");

            rig.Book.Bind(TestSpell("beast_2", SpellSchool.Bestial), 1);
            if (beast.Companion == null || beast.Companion.Definition.Id != "fox")
            {
                problems.Add("a second Bestial spell did not raise the companion to a fox");
                return;
            }

            MinionController fox = beast.Companion;
            fox.Health.DestroyOnDeath = false;
            fox.Health.TakeDamage(DamageInfo.Create(100000f, DamageType.True, Team.Enemy, null));

            rig.Book.Bind(TestSpell("beast_3", SpellSchool.Bestial), 2);
            if (beast.Companion != null) problems.Add("a companion that died came back on the same floor when the rank rose");

            beast.OnFloorEntered(null);
            if (beast.Companion == null || beast.Companion.Definition.Id != "wolf")
                problems.Add("a new floor did not bring the companion back, as a wolf at three spells");
        }

        // ---------------------------------------------------------------- 4.3 costs and rules

        private static void CheckCostsAndRules(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(3000f, 0f, 0f), rigs);
            SpellBook book = rig.Book;
            SoulsMastery souls = rig.Masteries.Get<SoulsMastery>();
            PsiBladesMastery psi = rig.Masteries.Get<PsiBladesMastery>();

            Spell soulSpell = TestSpell("test_soul_cost", SpellSchool.Death);
            soulSpell.SoulCost = 2;
            soulSpell.ManaCost = 5f;
            book.Bind(soulSpell, 0);

            CastOutcome refused = book.TryCastSlot(0);
            if (refused != CastOutcome.NotEnoughSouls) problems.Add("a spell costing two souls, cast with none, gave " + refused);

            souls.AddSouls(2);
            if (book.TryCastSlot(0) != CastOutcome.Cast) problems.Add("a spell costing two souls was refused with two held");
            if (souls.Souls != 0) problems.Add("casting a spell costing two souls left " + souls.Souls + " souls");
            if (!Approx(rig.Mana.Current, 95f)) problems.Add("a spell's mana cost was not taken alongside its souls");

            // An echo is free of every cost, and starts no cooldown.
            float mana = rig.Mana.Current;
            float cooldown = book.GetCooldown(0);
            if (!book.CastEcho(soulSpell, 1, Vector3.forward)) problems.Add("an echo of a spell costing souls was refused with none held");
            if (!Approx(rig.Mana.Current, mana) || souls.Souls != 0) problems.Add("an echoed cast paid a cost");
            if (!Approx(book.GetCooldown(0), cooldown)) problems.Add("an echoed cast changed a cooldown");

            Spell healthSpell = TestSpell("test_health_cost", SpellSchool.Petty);
            healthSpell.HealthCost = 30f;
            book.Bind(healthSpell, 1);
            rig.Health.Drain(80f);
            if (book.TryCastSlot(1) != CastOutcome.NotEnoughHealth) problems.Add("a 30 health cost was not refused at 20 health");
            rig.Health.Heal(rig.Health.Max);
            if (book.TryCastSlot(1) != CastOutcome.Cast || !Approx(rig.Health.Current, 70f))
                problems.Add("a 30 health cost at full health did not leave 70");

            Spell psiSpell = TestSpell("test_psi_cost", SpellSchool.Psionic);
            psiSpell.PsiCost = 1;
            book.Bind(psiSpell, 2);
            if (book.TryCastSlot(2) != CastOutcome.NotEnoughPsi) problems.Add("a psi cost was not refused with no charge");
            psi.AddBonus(1f);
            if (book.TryCastSlot(2) != CastOutcome.Cast || !Approx(psi.Charge, 0f)) problems.Add("a psi cost was not paid from the charge");

            // Silence stops spells; disarm stops the gun and the melee slot.
            book.ResetCooldowns();
            rig.Status.Apply(StatusLibrary.Silence(5f), null, Team.Enemy);
            if (book.Evaluate(1) != CastOutcome.Silenced) problems.Add("a silenced player could still cast: " + book.Evaluate(1));
            if (rig.Movement.TryActivate()) problems.Add("a silenced player could still use their movement spell");
            if (rig.CombatInput.EvaluateMelee() != CastOutcome.Ready) problems.Add("silence stopped the melee slot: " + rig.CombatInput.EvaluateMelee());
            rig.Status.Remove(StatusId.Silence);

            rig.Status.Apply(StatusLibrary.Disarm(5f), null, Team.Enemy);
            if (rig.CombatInput.CanShoot) problems.Add("a disarmed player could still shoot");
            if (rig.CombatInput.EvaluateMelee() != CastOutcome.Disarmed) problems.Add("a disarmed player could still swing: " + rig.CombatInput.EvaluateMelee());
            if (book.Evaluate(1) != CastOutcome.Ready) problems.Add("disarm stopped a spell: " + book.Evaluate(1));
            rig.Status.Remove(StatusId.Disarm);
        }

        // ---------------------------------------------------------------- 4.4 activation modes

        private static void CheckActivationModes(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(3100f, 0f, 0f), rigs);
            SpellBook book = rig.Book;

            // A toggle in a cast slot.
            Spell toggle = TestSpell("test_toggle", SpellSchool.Petty);
            toggle.OnCast.Clear();
            toggle.Cooldown = 2f;
            toggle.Sustain = new SustainProfile { ManaPerSecond = 10f, BreakOnShoot = true, HideFromSight = true };
            book.Bind(toggle, 0);

            if (book.TryCastSlot(0) != CastOutcome.Cast || !book.IsSustainActive(0)) problems.Add("a toggle in a cast slot did not switch on");
            if (!TargetRegistry.RigEntry.HiddenFromSight) problems.Add("a toggle that hides from sight did not hide the player");

            float mana = rig.Mana.Current;
            book.Tick(1f);
            if (!Approx(rig.Mana.Current, mana - 10f)) problems.Add("a 10 mana a second toggle drained " + (mana - rig.Mana.Current) + " in a second");

            if (book.TryCastSlot(0) != CastOutcome.ToggledOff || book.IsSustainActive(0)) problems.Add("pressing a running toggle did not switch it off");
            if (book.GetCooldown(0) <= 0f) problems.Add("switching a toggle off started no cooldown");
            if (TargetRegistry.RigEntry.HiddenFromSight) problems.Add("switching off a toggle that hides left the player hidden");

            book.ResetCooldowns();
            book.TryCastSlot(0);
            rig.Weapon.TryFire();
            if (book.IsSustainActive(0)) problems.Add("firing the gun did not break a toggle that breaks on shooting");

            Spell bloodwake = TestSpell("test_health_toggle", SpellSchool.Petty);
            bloodwake.OnCast.Clear();
            bloodwake.Sustain = new SustainProfile { HealthPerSecond = 20f };
            book.Bind(bloodwake, 1);
            rig.Health.Heal(rig.Health.Max);

            book.TryCastSlot(1);
            book.Tick(4f);
            book.Tick(1f);
            if (book.IsSustainActive(1)) problems.Add("a health-draining toggle kept running when the drain would take the last hit point");
            if (rig.Health.Current < SpellCosts.HealthFloor) problems.Add("a health-draining toggle took the player below one hit point");

            // Hold to charge, release to cast.
            Spell charged = TestSpell("test_charged", SpellSchool.Petty);
            charged.ManaCost = 5f;
            charged.Charge = new ChargeProfile { SecondsToFull = 2f, MinimumFraction = 0.25f };
            charged.OnCast.Clear();
            charged.OnCast.Add(new RecordChargeEffect());
            book.Bind(charged, 2);
            SetMana(rig, 100f);

            if (book.PressSlot(2) != CastOutcome.Charging) problems.Add("pressing a charged spell did not start charging");
            book.Tick(1f);
            if (!Approx(book.ChargeFraction(2), 0.5f)) problems.Add("a second of a two-second charge read " + book.ChargeFraction(2));

            CastOutcome released = book.ReleaseSlot(2);
            if (released != CastOutcome.Cast || !Approx(RecordChargeEffect.Last, 0.5f))
                problems.Add("releasing at half charge gave " + released + " at charge " + RecordChargeEffect.Last);
            if (!Approx(rig.Mana.Current, 95f)) problems.Add("a charged spell's cost was not paid on release");

            book.ResetCooldowns();
            int calls = RecordChargeEffect.Calls;
            book.PressSlot(2);
            book.Tick(0.2f);
            book.ReleaseSlot(2);
            if (RecordChargeEffect.Calls != calls || !Approx(rig.Mana.Current, 95f))
                problems.Add("a charge released below its minimum still cast or cost something");

            // A stance: always on, starting in its first mode, stepped by the key, never off, never draining.
            Spell stance = TestSpell("test_stance", SpellSchool.Elemental);
            stance.ManaCost = 0f;
            stance.OnCast.Clear();
            stance.Stance = new StanceProfile
            {
                Modes =
                {
                    new StanceMode { Name = "Fire", BulletStatuses = { StatusLibrary.Burn(amount: 5f) } },
                    new StanceMode { Name = "Ice", BulletStatuses = { StatusLibrary.Frost(stacks: 5) } },
                    new StanceMode { Name = "Storm", BulletStatuses = { StatusLibrary.Shock() } }
                }
            };

            book.Bind(stance, 0);
            StanceMode mode = book.ActiveStanceMode(0);
            if (mode == null || mode.Name != "Fire") problems.Add("a stance did not start in its first mode");
            if (!StanceCarries(rig, stance, StatusId.Burn)) problems.Add("a stance's first mode put nothing on the bullets");

            mana = rig.Mana.Current;
            if (book.TryCastSlot(0) != CastOutcome.StanceChanged || book.ActiveStanceMode(0).Name != "Ice")
                problems.Add("tapping a stance did not step to its next mode");
            if (!StanceCarries(rig, stance, StatusId.Frost) || StanceCarries(rig, stance, StatusId.Burn))
                problems.Add("changing mode did not change what the bullets carry");

            book.TryCastSlot(0);
            book.TryCastSlot(0);
            if (book.ActiveStanceMode(0).Name != "Fire") problems.Add("a stance did not wrap round to its first mode; it has no off");
            if (!Approx(rig.Mana.Current, mana) || book.GetCooldown(0) > 0f) problems.Add("changing stance cost mana or started a cooldown");

            book.Bind(null, 0);
            if (InfusionFor(rig, stance) != null) problems.Add("unbinding a stance left its charge on the bullets");

            // A conditional buff: its clock always runs, its modifier only while the condition holds.
            PlayerBuffs buffs = rig.Buffs;
            bool condition = true;
            buffs.ConditionOverride = c => condition;
            float speed = rig.Sheet.Get(Attr.MoveSpeed);

            buffs.Add("test_path", BuffCondition.FollowingExitRoute, Attr.MoveSpeed, 0.5f, 3f);
            if (!Approx(rig.Sheet.Get(Attr.MoveSpeed), speed * 1.5f)) problems.Add("a conditional buff did not apply while its condition held");

            condition = false;
            buffs.Tick(1f);
            if (!Approx(rig.Sheet.Get(Attr.MoveSpeed), speed)) problems.Add("a conditional buff stayed on when its condition broke");

            condition = true;
            buffs.Tick(1f);
            if (!buffs.IsApplied("test_path")) problems.Add("a conditional buff did not come back when its condition held again");

            buffs.Tick(1.5f);
            if (buffs.Has("test_path") || !Approx(rig.Sheet.Get(Attr.MoveSpeed), speed)) problems.Add("a conditional buff outlasted its duration");

            buffs.ConditionOverride = null;
            if (buffs.Evaluate(BuffCondition.FollowingExitRoute)) problems.Add("following the exit route counted with no route to follow");
        }

        [System.Serializable]
        private class RecordChargeEffect : AbilityEffect
        {
            public static float Last;
            public static int Calls;

            public override bool Execute(AbilityContext ctx)
            {
                Last = ctx.Charge;
                Calls++;
                return true;
            }
        }

        private static BulletInfusion InfusionFor(PlayerRig rig, Spell stance)
        {
            string id = SpellBook.StanceInfusionId(stance);
            foreach (BulletInfusion infusion in rig.Weapon.Infusions)
                if (infusion.Id == id) return infusion;
            return null;
        }

        private static bool StanceCarries(PlayerRig rig, Spell stance, StatusId status)
        {
            BulletInfusion infusion = InfusionFor(rig, stance);
            return infusion != null && infusion.Statuses.Exists(s => s.Id == status);
        }

        // ---------------------------------------------------------------- 4.5 the motor

        private static void CheckMotor(List<string> problems, List<PlayerRig> rigs)
        {
            CheckImpulseSequence(problems, rigs);
            CheckFrictionAndInversion(problems, rigs);
            CheckTeleportTargeting(problems);
            CheckLandingCheck(problems);
            CheckRewindRecorder(problems, rigs);
        }

        private static void CheckImpulseSequence(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(5000f, 500f, 0f), rigs);
            PlayerMotor motor = rig.Motor;
            motor.InputEnabled = false;

            var steps = new List<ImpulseStep>
            {
                new ImpulseStep { Delay = 0f, Duration = 0.5f, ForwardForce = 40f, SuppressFriction = true },
                new ImpulseStep { Delay = 0.6f, UpImpulse = 10f },
                new ImpulseStep { Delay = 0.6f, Duration = 1f, GravityScale = 0.25f, EndOnLanding = true }
            };
            motor.PlaySequence(steps, new Vector3(0f, 0.7f, 1f));

            Run(motor, 0.5f);
            float forward = Vector3.Dot(motor.Velocity, Vector3.forward);
            if (forward < 15f) problems.Add("half a second of a 40 m/s/s forward push reached " + forward + " m/s");

            Run(motor, 0.05f);
            float beforeImpulse = motor.Velocity.y;
            Run(motor, 0.1f);
            float afterImpulse = motor.Velocity.y;
            if (afterImpulse - beforeImpulse < 7f) problems.Add("the sequence's upward impulse changed vertical speed by " + (afterImpulse - beforeImpulse));

            Run(motor, 0.2f);
            float drop = afterImpulse - motor.Velocity.y;
            if (drop > 26f * 0.2f * 0.5f) problems.Add("the slow-fall step still fell " + drop + " m/s in 0.2 seconds");
        }

        private static void CheckFrictionAndInversion(List<string> problems, List<PlayerRig> rigs)
        {
            Build.Cube(null, "MotorFloor", new Vector3(5100f, -0.5f, 0f), new Vector3(60f, 1f, 60f),
                MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Physics.SyncTransforms();

            PlayerRig rig = MakeRig(new Vector3(5100f, 0.05f, 0f), rigs);
            PlayerMotor motor = rig.Motor;
            motor.InputEnabled = false;
            Run(motor, 0.3f);

            if (!motor.IsGrounded) problems.Add("the motor test rig never landed on its floor");

            motor.AddImpulse(Vector3.forward * 10f);
            Run(motor, 0.2f);
            if (motor.HorizontalSpeed > 3f) problems.Add("ground friction left " + motor.HorizontalSpeed + " m/s of a 10 m/s shove after 0.2 seconds");

            motor.AddImpulse(Vector3.forward * 10f - Vector3.ProjectOnPlane(motor.Velocity, Vector3.up));
            motor.SuppressFriction(1f);
            Run(motor, 0.2f);
            if (motor.HorizontalSpeed < 9f) problems.Add("suppressed friction still slowed a 10 m/s shove to " + motor.HorizontalSpeed);

            // Inversion, from a known velocity.
            PlayerRig air = MakeRig(new Vector3(5200f, 500f, 0f), rigs);
            PlayerMotor flying = air.Motor;
            flying.AddImpulse(new Vector3(3f, 4f, 5f) - flying.Velocity);

            flying.InvertVelocity(includeVertical: false);
            if (!Approx(flying.Velocity, new Vector3(-3f, 4f, -5f))) problems.Add("a horizontal inversion gave " + flying.Velocity);
            flying.InvertVelocity(includeVertical: true);
            if (!Approx(flying.Velocity, new Vector3(3f, -4f, 5f))) problems.Add("a full inversion gave " + flying.Velocity);

            // Travel with the controller off.
            Vector3 home = motor.transform.position;
            motor.BeginKinematic();
            if (rig.Controller.enabled) problems.Add("travelling kinematically left the controller on");
            motor.MoveKinematic(home + Vector3.forward * 20f);
            Run(motor, 0.2f);
            if (!Approx(motor.transform.position, home + Vector3.forward * 20f)) problems.Add("the motor moved a body it was not in charge of");
            motor.EndKinematic();
            if (!rig.Controller.enabled || motor.Velocity.sqrMagnitude > 0.0001f) problems.Add("ending kinematic travel did not restore the controller at rest");

            motor.SetPassThroughEnemies(true);
            if (!motor.PassesThroughEnemies) problems.Add("the player could not be set to pass through enemies");
            motor.SetPassThroughEnemies(false);
            if (motor.PassesThroughEnemies) problems.Add("the player still passes through enemies after it was switched off");
        }

        private static void CheckTeleportTargeting(List<string> problems)
        {
            // NavField grids are centred on the world origin, so this corner of the world is kept for it.
            Build.Cube(null, "NavFloor", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f),
                MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Build.Cube(null, "NavWall", new Vector3(0f, 1.5f, 3f), new Vector3(12f, 3f, 0.5f),
                MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);

            // A sealed pocket: walkable inside, but nothing can walk in.
            Build.Cube(null, "PocketSouth", new Vector3(-7f, 1.5f, 5f), new Vector3(4.5f, 3f, 0.5f), MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Build.Cube(null, "PocketNorth", new Vector3(-7f, 1.5f, 9.5f), new Vector3(4.5f, 3f, 0.5f), MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Build.Cube(null, "PocketWest", new Vector3(-9.25f, 1.5f, 7.25f), new Vector3(0.5f, 3f, 5f), MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Build.Cube(null, "PocketEast", new Vector3(-4.75f, 1.5f, 7.25f), new Vector3(0.5f, 3f, 5f), MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);

            var go = new GameObject("TestNavField");
            NavField field = go.AddComponent<NavField>();
            field.Build(20f, 20f);

            Vector3 from = new Vector3(0f, 0f, -5f);
            field.Rebuild(field.WorldToCell(from));

            if (!TeleportTargeting.FurthestWalkable(field, from, Vector3.forward, 12f, 1f, out Vector3 landing))
                problems.Add("a blink with open floor beyond a wall found nowhere to land");
            else if (landing.z < 3.5f)
                problems.Add("a blink landed at z " + landing.z.ToString("0.0") + ", short of the wall it should pass through");

            if (TeleportTargeting.FurthestWalkable(field, from, Vector3.forward, 40f, 1f, out Vector3 far) && far.z > 10f)
                problems.Add("a blink landed at z " + far.z.ToString("0.0") + ", past the edge of the map");

            Vector3 besidePocket = new Vector3(-7f, 0f, -5f);
            field.Rebuild(field.WorldToCell(besidePocket));
            if (TeleportTargeting.FurthestWalkable(field, besidePocket, Vector3.forward, 12f, 1f, out Vector3 pocket) && pocket.z > 5f)
                problems.Add("a blink landed inside a sealed pocket nothing can walk into, at z " + pocket.z.ToString("0.0"));
        }

        private static void CheckLandingCheck(List<string> problems)
        {
            var spot = new Vector3(6000f, 0f, 0f);
            EnemyController occupant = SpawnEnemy("cultist", spot);
            occupant.transform.position = spot;
            Physics.SyncTransforms();

            if (LandingCheck.IsClear(spot, 0.4f, 1.8f, null)) problems.Add("a spot with an enemy standing on it read as clear");

            int moved = LandingCheck.ShoveClear(spot, 0.4f, 1.8f, null);
            float radius = occupant.GetComponent<CharacterController>().radius;
            float gap = Flat(occupant.transform.position - spot).magnitude;

            if (moved != 1) problems.Add("the landing check moved " + moved + " bodies off a spot with one on it");
            if (gap < 0.4f + radius) problems.Add("a shoved body was left " + gap.ToString("0.00") + "m from the landing, still overlapping");
            if (!LandingCheck.IsClear(spot, 0.4f, 1.8f, null)) problems.Add("a landing was still occupied after the shove");

            // A banished enemy coming back onto a body moves it aside.
            var banishSpot = new Vector3(6100f, 0f, 0f);
            EnemyController banished = SpawnEnemy("cultist", banishSpot);
            banished.transform.position = banishSpot;
            banished.Hide();

            Health blocker = Subject("BanishBlocker", Team.Enemy, banishSpot, Layers.Enemy, collider: true);
            Physics.SyncTransforms();
            banished.Reveal();

            if (Flat(blocker.transform.position - banished.transform.position).magnitude < 0.5f)
                problems.Add("a banished enemy came back on top of the body standing in its spot");
        }

        private static void CheckRewindRecorder(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(6200f, 500f, 0f), rigs);
            RewindRecorder recorder = rig.Rewind;

            for (int i = 0; i < 100; i++) recorder.Record(0.05f);
            if (recorder.Count < 30 || recorder.Count > 31)
                problems.Add("five seconds of recording kept " + recorder.Count + " snapshots, not three seconds' worth at ten a second");
            if (recorder.TryOldest(out RewindRecorder.Snapshot oldest) && recorder.Now - oldest.Time > RewindRecorder.Window + Tolerance)
                problems.Add("the rewind history kept a snapshot " + (recorder.Now - oldest.Time) + " seconds old");

            rig.Health.Drain(10f);
            recorder.Record(RewindRecorder.Interval);
            if (recorder.Count > 0 && !Approx(recorder[recorder.Count - 1].Health, rig.Health.Current))
                problems.Add("a rewind snapshot did not record current health");

            rig.Motor.Teleport(rig.transform.position + Vector3.right);
            if (recorder.Count != 0) problems.Add("a teleport did not clear the rewind history");

            recorder.Record(0.5f);
            LevelEvents.RaiseFloorLeaving(1);
            if (recorder.Count != 0) problems.Add("leaving a floor did not clear the rewind history");

            recorder.Paused = true;
            recorder.Record(1f);
            if (recorder.Count != 0) problems.Add("a paused rewind recorder still recorded");
        }

        // ---------------------------------------------------------------- 4.6 hiding the player

        private static void CheckHiding(List<string> problems, List<PlayerRig> rigs)
        {
            TargetRegistry.Clear();
            PlayerRig rig = MakeRig(new Vector3(7000f, 0f, 0f), rigs);

            EnemyController watcher = SpawnEnemy("cultist", new Vector3(7000f, 0f, 8f));
            watcher.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms();

            if (!watcher.CanSee(TargetRegistry.PlayerBody))
            {
                problems.Add("setup: an enemy facing the player could not see them, so hiding cannot be checked");
                return;
            }

            PlayerConcealment concealment = rig.Concealment;
            var sight = new object();
            var flicker = new object();

            concealment.Hide(sight, fromSight: true);
            if (watcher.CanSee(TargetRegistry.PlayerBody)) problems.Add("an enemy still saw a player hidden from sight");

            concealment.Hide(flicker, fromSight: true, untargetable: true);
            var targets = new List<TargetRegistry.Entry>();
            TargetRegistry.Collect(targets);
            if (targets.Exists(e => e.IsPlayerBody)) problems.Add("an untargetable player was still offered to enemies as a target");

            concealment.Release(flicker);
            if (!concealment.HiddenFromSight) problems.Add("releasing one concealment request lifted another's");
            TargetRegistry.Collect(targets);
            if (!targets.Exists(e => e.IsPlayerBody)) problems.Add("the player stayed untargetable after the request that made them so was released");

            concealment.Release(sight);
            if (!watcher.CanSee(TargetRegistry.PlayerBody)) problems.Add("an enemy could not see the player once every concealment was released");

            PlayerConcealment.Request request = concealment.Hide(sight, fromSight: true);
            request.BreakOnShoot = true;
            rig.Weapon.TryFire();
            if (concealment.Holds(sight)) problems.Add("a concealment that breaks on shooting survived a shot");
        }

        // ---------------------------------------------------------------- 4.7 possession

        private static void CheckPossession(List<string> problems, List<PlayerRig> rigs)
        {
            TargetRegistry.Clear();
            PlayerRig rig = MakeRig(new Vector3(8000f, 0f, 0f), rigs);
            PossessionController possession = rig.Possession;
            Camera camera = rig.Camera;

            EnemyController hound = SpawnEnemy("hound", new Vector3(8000f, 0f, 10f));
            PossessionEnd? endedWith = null;
            possession.Ended += (body, reason) => endedWith = reason;

            if (!possession.Begin(hound, 5f))
            {
                problems.Add("taking over an enemy was refused");
                return;
            }

            if (hound.Health.Team != Team.Player) problems.Add("a possessed enemy is still on the enemy side");
            if (hound.gameObject.layer != Layers.BodyLayerFor(Team.Player)) problems.Add("a possessed enemy is not on the player's body layer");
            if (TargetRegistry.PlayerBody.Transform != hound.transform) problems.Add("enemies do not treat the possessed body as the player");
            if (!rig.ControlsSuppressed || rig.Motor.InputEnabled || rig.CombatInput.InputEnabled) problems.Add("the player's own controls stayed on while possessing");
            if (!rig.Health.IsInvulnerable) problems.Add("the player's real body is not immune while possessing");
            if (!rig.Concealment.HiddenFromSight || !rig.Concealment.HiddenFromHearing || !rig.Concealment.Untargetable)
                problems.Add("the player's real body is not hidden from sight, hearing and targeting while possessing");
            if (!camera.transform.IsChildOf(hound.transform)) problems.Add("the camera did not move into the possessed body");

            Vector3 start = hound.transform.position;
            var input = new PossessionInput { Yaw = 0f, Move = new Vector2(0f, 1f) };
            for (int i = 0; i < 10; i++) possession.Tick(0.05f, input);
            if (hound.transform.position.z - start.z < 0.3f) problems.Add("driving a possessed enemy forward moved it " + (hound.transform.position.z - start.z) + "m");

            if (hound.ActionCount == 0) problems.Add("setup: the hound has no attacks to fire");
            else
            {
                AbilityAttack[] attacks = hound.GetComponents<AbilityAttack>();
                foreach (AbilityAttack attack in attacks) attack.TickCooldown(30f);

                input.ActionsPressed = 1;
                possession.Tick(0.02f, input);
                input.ActionsPressed = 0;
                if (!attacks[0].IsExecuting) problems.Add("pressing the first action did not start the possessed enemy's first attack");
            }

            DamageInfo tick = DamageInfo.Create(1f, DamageType.Energy, Team.Enemy, null);
            tick.Origin = DamageOrigin.StatusTick;
            hound.Health.TakeDamage(tick);
            if (!possession.IsPossessing) problems.Add("a status tick on the possessed body ended control");

            hound.Health.TakeDamage(DamageInfo.Create(1f, DamageType.Kinetic, Team.Enemy, null));
            if (possession.IsPossessing) problems.Add("a direct hit on the possessed body did not end control");
            if (endedWith != PossessionEnd.BodyDamaged) problems.Add("control ended with " + endedWith + ", not BodyDamaged");
            if (hound.Health.Team != Team.Enemy || hound.IsPossessed) problems.Add("the enemy did not go back to its own side when control ended");
            if (TargetRegistry.PlayerBody.Transform != rig.transform) problems.Add("enemies still treat the enemy as the player after control ended");
            if (!possession.IsReturning || !rig.ControlsSuppressed) problems.Add("the controls came back before the camera flew home");
            if (!rig.Health.IsInvulnerable) problems.Add("the real body lost its immunity before the camera arrived");

            possession.Tick(PossessionController.ReturnSeconds + 0.1f, default);
            if (possession.IsReturning || rig.ControlsSuppressed) problems.Add("the controls did not come back once the camera arrived");
            if (camera.transform.parent != rig.CameraPivot) problems.Add("the camera did not return to the player's own eyes");
            if (rig.Concealment.HiddenFromSight) problems.Add("the real body stayed hidden after control ended");

            // Expiry.
            endedWith = null;
            if (!possession.Begin(hound, 0.1f)) problems.Add("an enemy could not be taken over a second time");
            else
            {
                possession.Tick(0.2f, new PossessionInput { Yaw = 0f });
                if (endedWith != PossessionEnd.Expired) problems.Add("control running out of time ended with " + endedWith);
                possession.Cancel();
                if (rig.ControlsSuppressed) problems.Add("cancelling the return left the controls off");
            }
        }

        // ---------------------------------------------------------------- 4.8 weapon extensions

        private static void CheckWeaponExtensions(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = MakeRig(new Vector3(9000f, 0f, 0f), rigs);
            Weapon gun = rig.Weapon;

            WeaponDefinition hitscan = FindHitscanGun();
            WeaponDefinition other = null;
            foreach (WeaponDefinition def in WeaponLibrary.All)
                if (hitscan != null && def.Id != hitscan.Id) { other = def; break; }

            if (hitscan == null || other == null)
            {
                problems.Add("setup: need a plain hitscan gun and a second gun to check weapon extensions");
                return;
            }

            rig.Holster.SetSlot(0, hitscan);
            rig.Holster.SetSlot(1, other);

            // Phantoms.
            PhantomWeapon phantom = PhantomWeapon.Create(rig.Holster, rig.CombatInput.Aim, rig.gameObject,
                useOwnerStats: true, shareInfusions: true);
            if (phantom.Weapon.Definition != hitscan) problems.Add("a phantom did not copy the gun in hand");

            gun.Infuse(BulletInfusion.ForRounds("test_smite", 1, StatusLibrary.Shock()));
            DamageInfo copied = phantom.Weapon.BuildShotDamage(ShotSpec.Primary(hitscan, 0f), Vector3.zero, Vector3.up, Vector3.forward);
            if (!Carries(copied, StatusId.Shock)) problems.Add("a phantom sharing infusions did not carry the real gun's");

            int realAmmo = gun.AmmoInMagazine;
            int phantomAmmo = phantom.Weapon.AmmoInMagazine;
            bool phantomShot = false;
            phantom.Weapon.Fired += shot => phantomShot = shot.IsPhantom;
            phantom.Weapon.TryFire();

            if (!phantomShot) problems.Add("a phantom's shot was not marked as a phantom's");
            if (phantom.Weapon.AmmoInMagazine != phantomAmmo || gun.AmmoInMagazine != realAmmo) problems.Add("a phantom's shot spent ammo");
            if (gun.Infusions.Count == 0) problems.Add("a phantom's shot used up the real gun's one-round infusion");

            rig.Holster.SetActive(1);
            if (phantom.Weapon.Definition != other) problems.Add("a phantom did not follow a weapon swap");
            rig.Holster.SetActive(0);
            gun.ClearInfusions();

            var lockKey = new object();
            rig.Holster.LockSwap(lockKey);
            if (rig.Holster.CanSwap) problems.Add("a locked holster still allowed a swap");
            rig.Holster.UnlockSwap(lockKey);
            if (!rig.Holster.CanSwap) problems.Add("unlocking the holster did not allow swaps again");

            gun.Equip(hitscan);
            gun.InfiniteAmmo = true;
            int ammo = gun.AmmoInMagazine;
            gun.TryFire();
            if (gun.AmmoInMagazine != ammo) problems.Add("a gun with infinite ammo spent a round");
            gun.InfiniteAmmo = false;

            // Auto-aim: nearest the reticle by angle, in sight.
            Vector3 eye = rig.CombatInput.Aim.position;
            Subject("AimBehind", Team.Enemy, new Vector3(9000f, 0f, -3f), Layers.Enemy, collider: true);
            Health ahead = Subject("AimAhead", Team.Enemy, new Vector3(9003f, 0f, 25f), Layers.Enemy, collider: true);
            Health wide = Subject("AimWide", Team.Enemy, new Vector3(9006f, 0f, 6f), Layers.Enemy, collider: true);
            Physics.SyncTransforms();

            IDamageable picked = AutoAim.PickTarget(eye, Vector3.forward, 60f, 180f, Team.Player);
            if (picked == null || picked.Transform != ahead.transform)
                problems.Add("auto-aim picked " + (picked != null ? picked.Transform.name : "nothing") + ", not the enemy nearest the reticle");

            Build.Cube(null, "AimWall", new Vector3(9002f, 1.5f, 12f), new Vector3(6f, 4f, 0.5f),
                MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Physics.SyncTransforms();

            picked = AutoAim.PickTarget(eye, Vector3.forward, 60f, 180f, Team.Player);
            if (picked == null || picked.Transform != wide.transform)
                problems.Add("auto-aim picked " + (picked != null ? picked.Transform.name : "nothing") + " with a wall hiding the nearer-aimed enemy");

            gun.Equip(hitscan);
            rig.AutoFire.Begin();
            rig.AutoFire.Step();
            if (rig.AutoFire.Target == null || rig.AutoFire.Target.Transform != wide.transform)
                problems.Add("auto-fire did not choose the only enemy in sight");
            if (wide.Current >= 1000f) problems.Add("auto-fire at an enemy well off the reticle did not land a round on it");
            rig.AutoFire.End();
            if (gun.AutoFire || gun.ForcedTarget != null) problems.Add("ending auto-fire left the gun aiming itself");

            CheckProjectiles(problems);
            CheckActionLog(problems, rig, hitscan);
        }

        private static void CheckProjectiles(List<string> problems)
        {
            Projectile tracked = Projectile.Create(new Vector3(9500f, 5f, 0f), Vector3.forward, Color.white, 0.1f);
            tracked.OwnerTeam = Team.Enemy;
            tracked.Lifetime = 100f;
            tracked.Launch();

            var found = new List<Projectile>();
            if (Projectile.FindNear(new Vector3(9500f, 5f, 0f), 2f, Team.Enemy, found) != 1 || found[0] != tracked)
                problems.Add("a live enemy projectile was not found where it flies");
            if (Projectile.FindNear(new Vector3(9500f, 5f, 0f), 2f, Team.Player, found) != 0)
                problems.Add("a search for the player's projectiles found an enemy's");

            int live = Projectile.Live.Count;
            Object.DestroyImmediate(tracked.gameObject);
            if (Projectile.Live.Count != live - 1) problems.Add("a destroyed projectile stayed in the live list");

            Projectile weave = Projectile.Create(new Vector3(9600f, 5f, 0f), Vector3.forward, Color.white, 0.1f);
            weave.OwnerTeam = Team.Player;
            weave.Speed = 10f;
            weave.Lifetime = 100f;
            weave.SwayAmplitude = 0.5f;
            weave.SwayFrequency = 1f;
            weave.Launch();

            float widest = 0f;
            for (int i = 0; i < 20; i++)
            {
                weave.Step(0.05f);
                widest = Mathf.Max(widest, Mathf.Abs(weave.transform.position.x - 9600f));
            }
            if (widest < 0.3f || widest > 0.6f) problems.Add("a projectile weaving half a metre drifted " + widest.ToString("0.00") + "m to the side");
            if (Mathf.Abs(weave.transform.position.x - 9600f) > 0.1f) problems.Add("a weaving projectile was off its line after a full cycle");
            if (weave.transform.position.z < 9f) problems.Add("weaving slowed a projectile down");

            var target = new GameObject("HomingOverride");
            target.transform.position = new Vector3(9710f, 4.1f, 0f);
            Projectile homing = Projectile.Create(new Vector3(9700f, 5f, 0f), Vector3.forward, Color.white, 0.1f);
            homing.OwnerTeam = Team.Player;
            homing.Speed = 10f;
            homing.Lifetime = 100f;
            homing.HomingEnabled = true;
            homing.HomingStrength = 10f;
            homing.HomingTarget = target.transform;
            homing.Launch();
            for (int i = 0; i < 10; i++) homing.Step(0.1f);
            if (homing.transform.position.x < 9701f) problems.Add("a projectile with a homing target did not steer toward it");

            Build.Cube(null, "PhaseWall", new Vector3(9800f, 1f, 0f), new Vector3(4f, 4f, 0.5f),
                MaterialLibrary.Lit(Color.gray), collider: true, layer: Layers.Level);
            Physics.SyncTransforms();

            Projectile phasing = Projectile.Create(new Vector3(9800f, 1f, -3f), Vector3.forward, Color.white, 0.1f);
            phasing.OwnerTeam = Team.Player;
            phasing.Speed = 20f;
            phasing.Lifetime = 100f;
            phasing.PassesThroughWalls = true;
            int moves = 0;
            phasing.Moved += (p, from, to) => moves++;
            phasing.Launch();

            if ((phasing.HitMask & (1 << Layers.Level)) != 0) problems.Add("a projectile passing through walls can still hit level geometry");
            for (int i = 0; i < 10; i++)
                if (phasing != null) phasing.Step(0.05f);

            if (phasing == null || phasing.transform.position.z < 1f) problems.Add("a projectile passing through walls was stopped by one");
            if (moves == 0) problems.Add("a moving projectile never raised its moved event");
        }

        private static void CheckActionLog(List<string> problems, PlayerRig rig, WeaponDefinition hitscan)
        {
            ActionLog log = rig.Actions;
            Weapon gun = rig.Weapon;
            log.Clear();

            bool echoed = false;
            gun.Fired += shot => echoed |= shot.IsEcho;

            gun.Equip(hitscan);
            gun.TryFire();
            if (log.Entries.Count != 1 || log.Entries[0].Kind != ActionLog.Kind.Shot) problems.Add("a shot from the player's gun was not recorded");
            else
            {
                int ammo = gun.AmmoInMagazine;
                log.Replay(log.Entries[0]);
                if (!echoed) problems.Add("an echoed shot was not fired as an echo");
                if (gun.AmmoInMagazine != ammo) problems.Add("an echoed shot spent ammo");
                if (log.Entries.Count != 1) problems.Add("an echoed shot was recorded to be echoed again");
            }

            Spell spell = TestSpell("test_echo", SpellSchool.Petty);
            spell.ManaCost = 10f;
            spell.Cooldown = 5f;
            spell.OnCast.Clear();
            spell.OnCast.Add(new RecordChargeEffect());
            rig.Book.Bind(spell, 0);
            SetMana(rig, 100f);

            rig.Book.TryCastSlot(0);
            if (log.Entries.Count != 2 || log.Entries[1].Kind != ActionLog.Kind.Spell) problems.Add("a cast was not recorded");
            else
            {
                int calls = RecordChargeEffect.Calls;
                float mana = rig.Mana.Current;
                float cooldown = rig.Book.GetCooldown(0);

                log.Replay(log.Entries[1]);
                if (RecordChargeEffect.Calls != calls + 1) problems.Add("replaying a recorded cast did not cast it");
                if (!Approx(rig.Mana.Current, mana) || !Approx(rig.Book.GetCooldown(0), cooldown)) problems.Add("an echoed cast cost mana or touched the cooldown");
                if (log.Entries.Count != 2) problems.Add("an echoed cast was recorded");
            }

            Spell rewind = TestSpell("test_never_echoes", SpellSchool.Aetherics);
            rewind.NeverEchoes = true;
            rig.Book.Bind(rewind, 1);
            rig.Book.TryCastSlot(1);
            if (log.Entries.Count != 2) problems.Add("a spell marked never to echo was recorded");

            echoed = false;
            log.BeginEchoing(5f, 1f);
            gun.Equip(hitscan);
            gun.TryFire();
            if (log.PendingCount != 1) problems.Add("a shot while echoing did not queue its echo");

            log.Tick(0.5f);
            if (echoed) problems.Add("an echo played before its delay");
            log.Tick(0.6f);
            if (!echoed || log.PendingCount != 0) problems.Add("an echo did not play once its delay had passed");
        }

        // ---------------------------------------------------------------- helpers

        private static PlayerRig MakeRig(Vector3 position, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerRig.Spawn(position);
            rigs.Add(rig);

            // What Awake would have done in play.
            Wake(rig.Health);
            Wake(rig.Mana);
            Wake(rig.Motor);
            Wake(rig.Look);
            Wake(rig);

            // A clean kit whatever loadout is selected: a loadout opening on a school's movement spell would
            // otherwise start a mastery a rank ahead of what each check binds.
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

            MethodInfo awake = component.GetType().GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
            Id = id,
            DisplayName = id,
            School = school,
            ManaCost = 1f,
            Cooldown = 1f,
            OnCast = { new PointAtCasterEffect() }
        };

        private static Spell TestMovement(string id, SpellSchool school)
        {
            Spell spell = TestSpell(id, school);
            spell.Slot = SpellSlot.Movement;
            return spell;
        }

        private static void Run(PlayerMotor motor, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / 0.05f);
            for (int i = 0; i < steps; i++) motor.Step(0.05f);
        }

        private static Health Victim(Vector3 position)
        {
            Health victim = Subject("Victim", Team.Enemy, position);
            return victim;
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

        /// <summary>Edit mode never calls Awake, so health is configured the way the factory's would be.</summary>
        private static EnemyController SpawnEnemy(string id, Vector3 position)
        {
            EnemyController enemy = EnemyFactory.Spawn(id, position, 1);
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

        private static bool Carries(in DamageInfo info, StatusId id)
        {
            if (info.Statuses == null) return false;
            for (int i = 0; i < info.Statuses.Count; i++)
                if (info.Statuses[i].Id == id) return true;
            return false;
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

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static bool Approx(float a, float b) => Mathf.Abs(a - b) <= Tolerance;

        private static bool Approx(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= Tolerance * Tolerance;
    }
}
#endif
