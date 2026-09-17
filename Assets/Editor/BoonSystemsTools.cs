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
    /// Exercises the systems the boons stand on: drawing guns, the per-class and per-school channels, the new
    /// attributes where they are read, sourced stat points, shillings and kill rewards, the boon statuses, the
    /// mastery hooks and the ally boosts.
    /// </summary>
    public static class BoonSystemsTools
    {
        private const float Tolerance = 0.01f;

        [MenuItem("Gunspire/Verify Boon Systems")]
        public static void VerifyBoonSystems()
        {
            var problems = new List<string>();
            var rigs = new List<PlayerRig>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            Health playerBefore = CombatRules.PlayerHealth;
            AllyBoosts alliesBefore = AllyBoosts.Active;

            try
            {
                CombatRules.Clear();
                WorldClock.Reset();
                TargetRegistry.Clear();

                CheckSheetChannels(problems);
                CheckDrawing(problems, rigs);
                CheckGunStats(problems, rigs);
                CheckSchools(problems, rigs);
                CheckKnockbackAndJumps(problems, rigs);
                CheckOrbs(problems, rigs);
                CheckDebuffPotency(problems);
                CheckShillings(problems, rigs);
                CheckStatuses(problems);
                CheckMasteryHooks(problems, rigs);
                CheckAllyBoosts(problems);
            }
            catch (System.Exception e)
            {
                problems.Add("the check itself threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                CombatRules.Clear();
                CombatRules.PlayerHealth = playerBefore;
                AllyBoosts.Active = alliesBefore;

                foreach (PlayerRig rig in rigs)
                    if (rig != null) rig.DetachPlayerSystems();

                WorldClock.Reset();
                TargetRegistry.Clear();
                Hazards.Clear();
                DeathRecords.Clear();

                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }

            if (problems.Count == 0)
            {
                Debug.Log("Boon systems: draw time, class and school channels, gun stats, knockback, air jumps, orbs, "
                          + "debuff potency, shillings, boon statuses, mastery hooks and ally boosts all behave as specified.\n"
                          + "  no problems.");
                return;
            }

            var report = new StringBuilder("Boon systems: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 60; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- the sheet

        private static void CheckSheetChannels(List<string> problems)
        {
            var go = new GameObject("BoonSheet");
            CharacterSheet sheet = go.AddComponent<CharacterSheet>();
            var source = new object();

            float plain = sheet.Get(Attr.ReloadSpeed);
            sheet.AddPercent(Attr.ReloadSpeed, 0.5f, source);
            sheet.AddClassModifier(WeaponClass.Handgun, StatModifier.Percent(Attr.ReloadSpeed, 0.25f, source));

            float global = sheet.Get(Attr.ReloadSpeed);
            float handgun = sheet.GetFor(WeaponClass.Handgun, Attr.ReloadSpeed);
            float sniper = sheet.GetFor(WeaponClass.Sniper, Attr.ReloadSpeed);

            if (!Approx(global, plain * 1.5f)) problems.Add("a +50% reload modifier read " + global + ", not " + plain * 1.5f);
            if (!Approx(handgun, plain * 1.75f))
                problems.Add("a class bonus did not add to the global one: handgun reload is " + handgun + ", not " + plain * 1.75f);
            if (!Approx(sniper, global)) problems.Add("a handgun bonus reached a sniper: " + sniper);

            sheet.AddClassModifier(WeaponClass.Sniper, StatModifier.Flat(Attr.Pierce, 2f, source));
            if (!Approx(sheet.GetFor(WeaponClass.Sniper, Attr.Pierce), 2f) || !Approx(sheet.Get(Attr.Pierce), 0f))
                problems.Add("a flat class pierce bonus read wrongly");

            sheet.RemoveModifiersFrom(source);
            if (!Approx(sheet.GetFor(WeaponClass.Handgun, Attr.ReloadSpeed), plain))
                problems.Add("removing a source left its class modifier behind");

            // Neutral values for the new attributes.
            if (!Approx(sheet.Get(Attr.MagazineSize), 1f) || !Approx(sheet.Get(Attr.JumpCount), 0f)
                || !Approx(sheet.Get(Attr.OrbDropChance), 0f) || !Approx(sheet.Get(Attr.DebuffPotency), 1f)
                || !Approx(sheet.Get(Attr.ShillingGain), 1f) || !Approx(sheet.Get(Attr.DrawSpeed), 1f))
                problems.Add("a new attribute does not rest at its neutral value");

            // Sourced stat points.
            int luck = sheet.GetStat(StatType.Luck);
            sheet.SetStatBonus(source, StatType.Luck, 3);
            if (sheet.GetStat(StatType.Luck) != luck + 3) problems.Add("a sourced stat bonus of 3 was not added");
            sheet.SetStatBonus(source, StatType.Luck, 1);
            if (sheet.GetStat(StatType.Luck) != luck + 1) problems.Add("replacing a sourced stat bonus did not replace it");
            sheet.RemoveStatBonuses(source);
            if (sheet.GetStat(StatType.Luck) != luck) problems.Add("removing a sourced stat bonus left it");

            sheet.SetStatBonus(source, StatType.Power, 2);
            sheet.AddSchoolDamage(SpellSchool.Death, 0.5f, source);
            sheet.ResetToBase();
            if (sheet.StatBonusFrom(source, StatType.Power) != 0 || !Approx(sheet.SchoolDamageMultiplier(SpellSchool.Death), 1f))
                problems.Add("a reset sheet kept a sourced stat or a school modifier");

            sheet.AddSchoolCost(SpellSchool.Abyssal, -2f, source);
            if (!Approx(sheet.SchoolCostMultiplier(SpellSchool.Abyssal), 0f)) problems.Add("a school cost went below free");
        }

        // ---------------------------------------------------------------- drawing

        private static void CheckDrawing(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5000f, 0f, 0f), rigs);
            Holster holster = rig.Holster;
            Weapon weapon = rig.Weapon;

            WeaponDefinition handgun = WeaponLibrary.Get("arcanum");
            WeaponDefinition sniper = WeaponLibrary.Get("frost_lance");
            WeaponDefinition smg = WeaponLibrary.Get("emberspit");

            if (!Approx(handgun.DrawSeconds, WeaponLibrary.DrawTimeFor(WeaponClass.Handgun)))
                problems.Add("the Arcanum draws in " + handgun.DrawSeconds + "s, not the handgun default");
            if (!Approx(new WeaponDefinition { Class = WeaponClass.Heavy }.DrawSeconds, WeaponLibrary.DrawTimeFor(WeaponClass.Heavy)))
                problems.Add("an unset draw time does not fall back to its class default");
            foreach (WeaponDefinition gun in WeaponLibrary.All)
                if (gun.DrawTime < 0f) problems.Add(gun.Id + " has no draw time of its own");

            holster.Clear();
            holster.SetSlot(0, handgun);
            if (weapon.IsDrawing) problems.Add("putting a gun straight into the hand started a draw");
            holster.SetSlot(1, sniper);

            holster.Swap();
            if (!weapon.IsDrawing) problems.Add("swapping guns did not start a draw");
            else if (!Approx(weapon.DrawTime, sniper.DrawSeconds)) problems.Add("the sniper draws in " + weapon.DrawTime + "s, not " + sniper.DrawSeconds);

            int ammo = weapon.AmmoInMagazine;
            weapon.TryFire();
            weapon.HandleInput(true, true, false);
            if (weapon.AmmoInMagazine != ammo) problems.Add("a gun fired while being drawn");
            if (weapon.IsFocusing) problems.Add("a gun focused while being drawn");

            weapon.SetAmmo(1);
            weapon.StartReload();
            if (weapon.IsReloading) problems.Add("a gun started reloading while being drawn");

            weapon.TickDraw(sniper.DrawSeconds + 0.1f);
            if (weapon.IsDrawing) problems.Add("a draw did not end once its time ran out");

            var faster = new object();
            rig.Sheet.AddClassModifier(WeaponClass.Sniper, StatModifier.Percent(Attr.DrawSpeed, 1f, faster));
            if (!Approx(weapon.DrawTime, sniper.DrawSeconds * 0.5f))
                problems.Add("doubling sniper draw speed gave a draw of " + weapon.DrawTime + "s");
            rig.Sheet.RemoveModifiersFrom(faster);

            holster.SetActive(0);
            if (weapon.IsDrawing) problems.Add("restoring the active gun directly, as Rewind does, started a draw");

            holster.Take(smg, -1, out _);
            if (!weapon.IsDrawing) problems.Add("taking a gun from a plinth did not draw it");
            weapon.FinishDraw();
        }

        // ---------------------------------------------------------------- gun stats

        private static void CheckGunStats(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5100f, 0f, 0f), rigs);
            var source = new object();

            // Magazines, in hand and holstered.
            WeaponDefinition handgun = WeaponLibrary.Get("arcanum");
            WeaponDefinition smg = WeaponLibrary.Get("emberspit");
            rig.Holster.Clear();
            rig.Holster.SetSlot(0, handgun);
            rig.Holster.SetSlot(1, smg);

            rig.Sheet.AddPercent(Attr.MagazineSize, 0.5f, source);
            rig.Sheet.AddClassModifier(WeaponClass.Handgun, StatModifier.Percent(Attr.MagazineSize, 0.25f, source));
            int handgunMag = Mathf.FloorToInt(handgun.MagazineSize * 1.75f + 0.0001f);
            int smgMag = Mathf.FloorToInt(smg.MagazineSize * 1.5f + 0.0001f);

            if (rig.Weapon.MagazineSize != handgunMag) problems.Add("the handgun's magazine is " + rig.Weapon.MagazineSize + ", not " + handgunMag);
            rig.Holster.RefillAll();
            if (rig.Holster.AmmoIn(0) != handgunMag || rig.Weapon.AmmoInMagazine != handgunMag)
                problems.Add("refilling left the drawn handgun at " + rig.Weapon.AmmoInMagazine + ", not " + handgunMag);
            if (rig.Holster.AmmoIn(1) != smgMag || rig.Holster.MagazineIn(1) != smgMag)
                problems.Add("the holstered SMG refilled to " + rig.Holster.AmmoIn(1) + ", not " + smgMag);
            rig.Sheet.RemoveModifiersFrom(source);

            // Spin-up, projectile speed, splash and pierce, on guns held apart from the rig.
            Weapon minigun = LooseGun("minigun", rig.Sheet);
            float spin = minigun.SpinUpTime;
            rig.Sheet.AddClassModifier(WeaponClass.Heavy, StatModifier.Percent(Attr.SpinUpRate, 1f, source));
            if (!Approx(minigun.SpinUpTime, spin * 0.5f)) problems.Add("doubling spin-up rate gave " + minigun.SpinUpTime + "s, not " + spin * 0.5f);

            Weapon launcher = LooseGun("knell", rig.Sheet);
            WeaponDefinition knell = launcher.Definition;
            rig.Sheet.AddClassModifier(WeaponClass.Launcher, StatModifier.Percent(Attr.ProjectileSpeed, 1f, source));
            rig.Sheet.AddClassModifier(WeaponClass.Launcher, StatModifier.Percent(Attr.SplashRadius, 0.5f, source));
            rig.Sheet.AddFlat(Attr.Pierce, 2f, source);

            launcher.FireNow();
            Projectile shot = null;
            foreach (Projectile p in Projectile.Live)
                if (p.SourceWeapon == launcher) shot = p;

            if (shot == null) problems.Add("the launcher fired nothing to check");
            else
            {
                if (!Approx(shot.Speed, knell.ProjectileSpeed * 2f)) problems.Add("a launcher round flew at " + shot.Speed + ", not " + knell.ProjectileSpeed * 2f);
                if (!Approx(shot.SplashRadius, knell.SplashRadius * 1.5f)) problems.Add("a launcher round's splash was " + shot.SplashRadius);
                if (shot.Pierce != knell.MaxPierce + 2) problems.Add("a round pierced " + shot.Pierce + ", not " + (knell.MaxPierce + 2));
            }

            if (launcher.RoundsSinceReload != 1 || launcher.RoundsSinceDraw != 1)
                problems.Add("a fired round was not counted since the reload and the draw");
            launcher.RefillMagazine();
            if (launcher.RoundsSinceReload != 0) problems.Add("a refill did not reset the rounds since the reload");

            rig.Sheet.RemoveModifiersFrom(source);
        }

        private static Weapon LooseGun(string id, CharacterSheet sheet)
        {
            var go = new GameObject("BoonGun_" + id);
            go.transform.position = sheet.transform.position + Vector3.up * 1.5f;
            var weapon = go.AddComponent<Weapon>();
            weapon.Owner = go;
            weapon.OwnerSheet = sheet;
            weapon.Equip(WeaponLibrary.Get(id));
            return weapon;
        }

        // ---------------------------------------------------------------- schools

        private static void CheckSchools(List<string> problems, List<PlayerRig> rigs)
        {
            var caster = new GameObject("BoonCaster");
            CharacterSheet sheet = caster.AddComponent<CharacterSheet>();
            var ctx = new AbilityContext { Caster = caster, Team = Team.Player, Sheet = sheet };

            Spell death = PlayerTools.TestSpell("school_death", SpellSchool.Death);
            Spell fire = PlayerTools.TestSpell("school_fire", SpellSchool.Elemental);
            death.ManaCost = 20f;
            death.HealthCost = 10f;

            ctx.BeginCast(death, 1);
            float deathPower = ctx.Power;
            ctx.BeginCast(fire, 1);
            float firePower = ctx.Power;

            sheet.AddSchoolDamage(SpellSchool.Death, 0.5f);
            ctx.BeginCast(death, 1);
            if (!Approx(ctx.Power, deathPower * 1.5f)) problems.Add("+50% Death damage gave a power of " + ctx.Power + ", not " + deathPower * 1.5f);
            ctx.BeginCast(fire, 1);
            if (!Approx(ctx.Power, firePower)) problems.Add("a Death damage bonus reached an Elemental spell");

            sheet.AddSchoolCost(SpellSchool.Death, -0.5f);
            if (!Approx(SpellCosts.ManaCostOf(death, ctx), 10f) || !Approx(SpellCosts.HealthCostOf(death, ctx), 5f))
                problems.Add("half-price Death spells cost " + SpellCosts.ManaCostOf(death, ctx) + " mana and "
                             + SpellCosts.HealthCostOf(death, ctx) + " health, not 10 and 5");
            if (!Approx(SpellCosts.ManaCostOf(fire, ctx), fire.ManaCost)) problems.Add("a Death cost cut reached an Elemental spell");

            // Cooldowns in the book.
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5200f, 0f, 0f), rigs);
            Spell slow = PlayerTools.TestSpell("school_cd", SpellSchool.Death);
            slow.Cooldown = 10f;
            rig.Book.Bind(slow, 0);
            PlayerTools.SetMana(rig, 100f);

            CastOutcome cast = rig.Book.TryCastSlot(0);
            if (cast != CastOutcome.Cast)
            {
                problems.Add("a test spell for school cooldowns did not cast: " + cast);
                return;
            }

            float start = rig.Book.GetCooldown(0);
            rig.Book.Tick(1f);
            float plainStep = start - rig.Book.GetCooldown(0);

            rig.Sheet.AddSchoolCooldown(SpellSchool.Death, 1f);
            float mid = rig.Book.GetCooldown(0);
            rig.Book.Tick(1f);
            float fastStep = mid - rig.Book.GetCooldown(0);

            if (plainStep <= 0f || !Approx(fastStep, plainStep * 2f))
                problems.Add("doubling the Death cooldown rate turned a " + plainStep + "s step into " + fastStep + "s");
        }

        // ---------------------------------------------------------------- knockback and jumps

        private static void CheckKnockbackAndJumps(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5300f, 0f, 0f), rigs);
            CombatRules.PlayerHealth = rig.Health;
            var source = new object();
            rig.Sheet.AddPercent(Attr.Knockback, 1f, source);

            Health enemy = PlayerTools.Subject("BoonKnock", Team.Enemy, new Vector3(5300f, 0f, 5f));
            var seen = new List<Vector3>();
            enemy.Damaged += (info, amount) => seen.Add(info.Knockback);

            DamageInfo own = DamageInfo.Create(1f, DamageType.True, Team.Player, rig.gameObject);
            own.Knockback = Vector3.forward * 2f;
            enemy.TakeDamage(own);

            DamageInfo other = DamageInfo.Create(1f, DamageType.True, Team.Player, null);
            other.Knockback = Vector3.forward * 2f;
            enemy.TakeDamage(other);

            if (seen.Count != 2) problems.Add("two knockback hits were seen " + seen.Count + " times");
            else
            {
                if (!Approx(seen[0].magnitude, 4f)) problems.Add("doubled knockback on the player's own hit came through as " + seen[0].magnitude);
                if (!Approx(seen[1].magnitude, 2f)) problems.Add("the player's knockback bonus reached a hit that was not theirs");
            }

            rig.Sheet.RemoveModifiersFrom(source);
            CombatRules.PlayerHealth = null;

            // Air jumps.
            PlayerMotor motor = rig.Motor;
            if (motor.TryAirJump()) problems.Add("an air jump happened with no jump count");

            rig.Sheet.AddFlat(Attr.JumpCount, 1f, source);
            if (motor.IsGrounded) problems.Add("the test motor reports being on the ground, so air jumps cannot be checked");
            else
            {
                if (!motor.TryAirJump()) problems.Add("one air jump was refused with a jump count of one");
                if (motor.TryAirJump()) problems.Add("a second air jump happened with a jump count of one");
            }
            rig.Sheet.RemoveModifiersFrom(source);
        }

        // ---------------------------------------------------------------- orbs

        private static void CheckOrbs(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5400f, 0f, 0f), rigs);
            var source = new object();

            PlayerTools.SetMana(rig, 50f);
            rig.Sheet.AddPercent(Attr.OrbPotency, 1f, source);
            OrbPickup orb = OrbPickup.SpawnMana(rig.transform.position, 10f);
            if (!orb.TryCollect(rig) || !Approx(rig.Mana.Current, 70f))
                problems.Add("a 10 mana orb at double potency left mana at " + rig.Mana.Current + ", not 70");
            rig.Sheet.RemoveModifiersFrom(source);

            if (PlayerRig.Instance == rig)
            {
                PlayerTools.SetMana(rig, 50f);
                OrbPickup far = OrbPickup.SpawnMana(rig.transform.position + new Vector3(3f, 1f, 0f), 10f);
                MethodInfo update = typeof(OrbPickup).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

                update.Invoke(far, null);
                if (far == null || !Approx(rig.Mana.Current, 50f)) problems.Add("an orb 3m away was picked up at the normal radius");

                rig.Sheet.AddPercent(Attr.PickupRadius, 2f, source);
                if (far != null) update.Invoke(far, null);
                if (!Approx(rig.Mana.Current, 60f)) problems.Add("an orb 3m away was not picked up at triple radius");
                rig.Sheet.RemoveModifiersFrom(source);
            }
            else
            {
                problems.Add("the test rig is not the player instance, so the pickup radius cannot be checked");
            }
        }

        // ---------------------------------------------------------------- debuff potency

        private static void CheckDebuffPotency(List<string> problems)
        {
            Health target = PlayerTools.Subject("BoonResilient", Team.Player, new Vector3(5500f, 0f, 0f));
            StatusController status = target.GetComponent<StatusController>();
            CharacterSheet sheet = target.GetComponent<CharacterSheet>();
            var source = new object();
            sheet.AddPercent(Attr.DebuffPotency, -0.5f, source);

            status.Apply(StatusLibrary.Burn(12f, 1, 20f), null, Team.Enemy);
            status.Apply(StatusLibrary.Frost(4f, 20), null, Team.Enemy);
            status.Apply(StatusLibrary.Weaken(4f), null, Team.Enemy);
            status.Apply(StatusLibrary.Poison(7f, 3, 1.4f), null, Team.Enemy);
            status.Apply(StatusLibrary.Haste(4f), null, Team.Enemy);
            status.Apply(StatusLibrary.Bleed(999f, 1, 4f), null, Team.Enemy);

            ExpectStatus(problems, status, StatusId.Burn, s => Approx(s.Magnitude, 10f), "half-potency burn should hold 10");
            ExpectStatus(problems, status, StatusId.Frost, s => s.Stacks == 10, "half-potency frost should land 10 stacks");
            ExpectStatus(problems, status, StatusId.Weaken, s => Approx(s.Remaining, 2f), "half-potency weaken should last 2s");
            ExpectStatus(problems, status, StatusId.Poison, s => Approx(s.Remaining, 7f) && s.Stacks == 3 && Approx(s.Magnitude, 1.4f),
                "poison should be untouched");
            ExpectStatus(problems, status, StatusId.Haste, s => Approx(s.Remaining, 4f), "a buff should be untouched");
            ExpectStatus(problems, status, StatusId.Bleed, s => Approx(s.Magnitude, 2f) && Approx(s.Remaining, 999f),
                "half-potency bleed should deal half and never shorten");

            status.ClearAll();
            sheet.AddPercent(Attr.DebuffPotency, -0.5f, source);
            status.Apply(StatusLibrary.Frost(4f, 20), null, Team.Enemy);
            if (status.Has(StatusId.Frost)) problems.Add("frost landed on something with no debuff potency left");
        }

        private static void ExpectStatus(List<string> problems, StatusController status, StatusId id,
            System.Func<ActiveStatus, bool> check, string expectation)
        {
            ActiveStatus s = status.Find(id);
            if (s == null) problems.Add(id + " did not land: " + expectation);
            else if (!check(s)) problems.Add(id + " landed wrongly (stacks " + s.Stacks + ", magnitude " + s.Magnitude
                                             + ", remaining " + s.Remaining + "): " + expectation);
        }

        // ---------------------------------------------------------------- shillings

        private static void CheckShillings(List<string> problems, List<PlayerRig> rigs)
        {
            var wallet = new Wallet();
            int got = wallet.Earn(0.4f, ShillingSource.Kill) + wallet.Earn(0.4f, ShillingSource.Kill);
            got += wallet.Earn(0.4f, ShillingSource.Kill);
            if (got != 1 || wallet.Balance != 1) problems.Add("three 0.4 earnings made " + wallet.Balance + " shillings, not 1");
            if (wallet.TrySpend(2) || wallet.Balance != 1) problems.Add("spending more than the balance succeeded");
            if (!wallet.TrySpend(1) || wallet.Balance != 0) problems.Add("spending the whole balance failed");

            var rng = new Rng(11);
            for (int i = 0; i < 200; i++)
            {
                int common = KillRewards.Roll(rng, null, false, false);
                int elite = KillRewards.Roll(rng, null, true, false);
                int boss = KillRewards.Roll(rng, null, false, true);
                if (common < KillRewards.CommonMin || common > KillRewards.CommonMax
                    || elite % KillRewards.EliteMultiplier != 0 || elite < KillRewards.CommonMin * KillRewards.EliteMultiplier
                    || elite > KillRewards.CommonMax * KillRewards.EliteMultiplier
                    || boss < KillRewards.BossMin || boss > KillRewards.BossMax)
                {
                    problems.Add("a drop rolled outside its range: common " + common + ", elite " + elite + ", boss " + boss);
                    break;
                }
            }

            var fixedDef = new EnemyDefinition { ShillingsMin = 5, ShillingsMax = 5 };
            if (KillRewards.Roll(rng, fixedDef, false, false) != 5 || KillRewards.Roll(rng, fixedDef, true, false) != 20)
                problems.Add("an enemy's own shilling range was not used, or not multiplied for an elite");

            // A bound run: kills pay coins, coins bank, gain applies to kills only.
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5600f, 0f, 0f), rigs);
            var run = new RunState(12, 3);
            run.Bind(rig);
            try
            {
                var source = new object();
                rig.Sheet.AddPercent(Attr.ShillingGain, 1f, source);
                if (run.EarnShillings(3f, ShillingSource.Kill) != 6) problems.Add("doubled shilling gain did not double a kill's shillings");
                if (run.EarnShillings(3f, ShillingSource.Gilded) != 3) problems.Add("shilling gain reached Gilded shillings");
                rig.Sheet.RemoveModifiersFrom(source);
                int balance = run.Wallet.Balance;

                EnemyController paying = SpawnEnemy(new Vector3(5600f, 0f, 10f), 1);
                EnemyController copy = EnemyFactory.Copy(paying, new Vector3(5602f, 0f, 10f));
                if (copy != null) copy.Health.DestroyOnDeath = false;
                if (copy == null || !copy.PaysNoReward) problems.Add("a split copy is not marked as paying no reward");

                int coinsBefore = ShillingPickup.Live.Count;
                float dropped = CoinTotal();
                paying.Health.Kill();
                int coinsAfterKill = ShillingPickup.Live.Count;
                float fromKill = CoinTotal() - dropped;
                if (coinsAfterKill <= coinsBefore) problems.Add("killing an enemy dropped no shillings");
                if (fromKill < KillRewards.CommonMin - Tolerance || fromKill > KillRewards.CommonMax + Tolerance)
                    problems.Add("an ordinary kill dropped " + fromKill + " shillings");

                if (copy != null)
                {
                    copy.Health.Kill();
                    if (ShillingPickup.Live.Count != coinsAfterKill) problems.Add("a split copy dropped shillings");
                }

                LevelEvents.RaiseFloorCompleted(null);
                if (ShillingPickup.Live.Count != 0) problems.Add("coins were left on the floor after the exit was taken");
                if (run.Wallet.Balance != balance + Mathf.RoundToInt(fromKill))
                    problems.Add("banking the floor gave " + (run.Wallet.Balance - balance) + " shillings, not " + fromKill);

                // Orb drops.
                rig.Sheet.AddFlat(Attr.OrbDropChance, 1f, source);
                int orbs = Object.FindObjectsByType<OrbPickup>().Length;
                KillRewards.Drop(run, SpawnEnemy(new Vector3(5604f, 0f, 10f), 1), new Vector3(5604f, 1f, 10f));
                if (Object.FindObjectsByType<OrbPickup>().Length != orbs + 1) problems.Add("a certain orb drop dropped no orb");
                rig.Sheet.RemoveModifiersFrom(source);
                ShillingPickup.BankAll(run);
            }
            finally
            {
                run.Unbind();
            }

            if (new RunState(13, 3).Wallet.Balance != 0) problems.Add("a new run does not start with an empty wallet");
        }

        /// <summary>A real enemy that stays in the scene when killed, since edit mode cannot destroy it with a delay.</summary>
        private static EnemyController SpawnEnemy(Vector3 position, int floor, bool elite = false)
        {
            EnemyController enemy = EnemyFactory.Spawn("cultist", position, floor, elite);
            if (enemy != null && enemy.Health != null) enemy.Health.DestroyOnDeath = false;
            return enemy;
        }

        private static float CoinTotal()
        {
            float total = 0f;
            foreach (ShillingPickup coin in ShillingPickup.Live) total += coin.Amount;
            return total;
        }

        // ---------------------------------------------------------------- statuses

        private static void CheckStatuses(List<string> problems)
        {
            // Charm.
            EnemyController elite = SpawnEnemy(new Vector3(5700f, 0f, 0f), 1, elite: true);
            elite.Status.Apply(StatusLibrary.Charmed(), null, Team.Player);
            if (elite.Status.Has(StatusId.Charmed)) problems.Add("an elite was charmed");

            EnemyController charmed = SpawnEnemy(new Vector3(5703f, 0f, 0f), 1);
            var room = new GameObject("BoonRoom").AddComponent<RoomRuntime>();
            room.Register(charmed);

            charmed.Status.Apply(StatusLibrary.Charmed(), null, Team.Player);
            charmed.SyncStatusEffects();
            if (!charmed.IsCharmed) problems.Add("an ordinary enemy was not charmed");
            if (charmed.Health.Team != Team.Player) problems.Add("a charmed enemy stayed on the enemy side");
            if (room.EnemiesRemaining != 0 || !room.IsCleared) problems.Add("a room whose only enemy was charmed did not clear");

            bool listed = false;
            foreach (TargetRegistry.Entry entry in TargetRegistry.Minions)
                if (entry.Transform == charmed.transform) listed = true;
            if (!listed) problems.Add("a charmed enemy is not among the things enemies fight");

            charmed.Status.Tick(60f);
            if (!charmed.IsCharmed) problems.Add("a charm wore off with time; it should last the floor");

            Health mine = PlayerTools.Subject("BoonMine", Team.Player, new Vector3(5706f, 0f, 0f));
            mine.GetComponent<StatusController>().Apply(StatusLibrary.Charmed(), null, Team.Player);
            if (mine.GetComponent<StatusController>().Has(StatusId.Charmed)) problems.Add("something on the player's side was charmed");

            // Hex.
            Health hexed = PlayerTools.Subject("BoonHexed", Team.Enemy, new Vector3(5710f, 0f, 0f));
            StatusController hex = hexed.GetComponent<StatusController>();
            hex.Apply(StatusLibrary.Hex(4f, 0.5f), null, Team.Player);
            hexed.TakeDamage(DamageInfo.Create(100f, DamageType.True, Team.Player, null));
            hex.Apply(StatusLibrary.Hex(4f, 0.2f), null, Team.Player);
            ExpectStatus(problems, hex, StatusId.Hex, s => Approx(s.Stored, 50f) && Approx(s.Magnitude, 0.7f),
                "a 50% hex should store 50 of 100, and a 20% top-up should make it 70%");
            float beforePayout = hexed.Current;
            hex.Tick(5f);
            if (hex.Has(StatusId.Hex)) problems.Add("a hex did not end");
            if (!Approx(beforePayout - hexed.Current, 50f)) problems.Add("an ending hex dealt " + (beforePayout - hexed.Current) + ", not 50");

            Health doomed = PlayerTools.Subject("BoonHexDoomed", Team.Enemy, new Vector3(5712f, 0f, 0f));
            doomed.GetComponent<StatusController>().Apply(StatusLibrary.Hex(4f, 0.5f), null, Team.Player);
            doomed.TakeDamage(DamageInfo.Create(50f, DamageType.True, Team.Player, null));
            doomed.Kill();

            // Volatile.
            Physics.SyncTransforms();
            Health bomb = PlayerTools.Subject("BoonBomb", Team.Enemy, new Vector3(5720f, 0f, 0f), Layers.Enemy, collider: true);
            Health near = PlayerTools.Subject("BoonNear", Team.Enemy, new Vector3(5721.5f, 0f, 0f), Layers.Enemy, collider: true);
            Health ally = PlayerTools.Subject("BoonAlly", Team.Player, new Vector3(5720f, 0f, 1.5f), Layers.Minion, collider: true);
            Physics.SyncTransforms();

            StatusController fuse = bomb.GetComponent<StatusController>();
            int explosions = VolatileStatus.Explosions;
            fuse.Apply(StatusLibrary.Volatile(60f), null, Team.Player);
            if (VolatileStatus.Explosions != explosions) problems.Add("volatile exploded below its threshold");
            fuse.Apply(StatusLibrary.Volatile(50f), null, Team.Player);
            if (VolatileStatus.Explosions != explosions + 1) problems.Add("volatile did not explode at its threshold");
            if (fuse.Has(StatusId.Volatile)) problems.Add("volatile did not reset after exploding");
            if (near.Current >= near.Max) problems.Add("a volatile explosion did not hurt the enemy beside it");
            if (ally.Current < ally.Max) problems.Add("a volatile explosion hurt the player's side");
            if (near.GetComponent<StatusController>().Has(StatusId.Volatile)) problems.Add("a volatile explosion spread volatile");

            // Gilded.
            EnemyController rich = SpawnEnemy(new Vector3(5730f, 0f, 0f), 1);
            rich.Status.Apply(StatusLibrary.Gilded(4f), null, Team.Player);
            rich.Status.Apply(StatusLibrary.Gilded(8f), null, Team.Player);
            ExpectStatus(problems, rich.Status, StatusId.Gilded, s => Approx(s.Stored, GildedStatus.MaxShillings),
                "gilded should cap at " + GildedStatus.MaxShillings);
            rich.Status.Tick(60f);
            if (!rich.Status.Has(StatusId.Gilded)) problems.Add("gilded wore off with time");

            float coins = CoinTotal();
            rich.Health.Kill();
            if (!Approx(CoinTotal() - coins, GildedStatus.MaxShillings))
                problems.Add("a gilded enemy dropped " + (CoinTotal() - coins) + " shillings, not " + GildedStatus.MaxShillings);

            EnemyController poorCopy = SpawnEnemy(new Vector3(5732f, 0f, 0f), 1);
            poorCopy.PaysNoReward = true;
            poorCopy.Status.Apply(StatusLibrary.Gilded(5f), null, Team.Player);
            coins = CoinTotal();
            poorCopy.Health.Kill();
            if (!Approx(CoinTotal(), coins)) problems.Add("a gilded enemy that pays no reward dropped shillings");
            foreach (ShillingPickup coin in new List<ShillingPickup>(ShillingPickup.Live)) coin.Collect(null);

            // Dread.
            Health scared = PlayerTools.Subject("BoonScared", Team.Enemy, new Vector3(5740f, 0f, 0f));
            StatusController dread = scared.GetComponent<StatusController>();
            dread.Apply(StatusLibrary.Dread(60f), null, Team.Player);
            dread.Tick(1f);
            ExpectStatus(problems, dread, StatusId.Dread, s => Approx(s.Stored, 60f - DreadStatus.DrainPerSecond),
                "dread should drain " + DreadStatus.DrainPerSecond + " a second");
            if (dread.IsFeared) problems.Add("dread feared below its threshold");
            dread.Apply(StatusLibrary.Dread(60f), null, Team.Player);
            if (!dread.IsFeared) problems.Add("dread did not fear at its threshold");
            if (dread.Has(StatusId.Dread)) problems.Add("dread did not reset once it feared");

            Health braveElite = PlayerTools.Subject("BoonBrave", Team.Enemy, new Vector3(5742f, 0f, 0f));
            braveElite.IsElite = true;
            StatusController eliteDread = braveElite.GetComponent<StatusController>();
            eliteDread.Apply(StatusLibrary.Dread(80f), null, Team.Player);
            ExpectStatus(problems, eliteDread, StatusId.Dread, s => Approx(s.Stored, 80f * DreadStatus.EliteRate),
                "an elite should fill dread at " + DreadStatus.EliteRate);

            dread.Remove(StatusId.Fear);
            dread.Apply(StatusLibrary.Dread(10f), null, Team.Player);
            dread.Tick(1f);
            if (dread.Has(StatusId.Dread)) problems.Add("dread that drained to nothing did not end");
        }

        // ---------------------------------------------------------------- mastery hooks

        private static void CheckMasteryHooks(List<string> problems, List<PlayerRig> rigs)
        {
            PlayerRig rig = PlayerTools.MakeRig(new Vector3(5800f, 0f, 0f), rigs);
            MasteryHost host = rig.Masteries;

            // Souls.
            SoulsMastery souls = host.Get<SoulsMastery>();
            rig.Book.Bind(PlayerTools.TestSpell("hook_death", SpellSchool.Death), 0);
            souls.CapBonus = 2;
            int wasted = 0;
            int spent = 0;
            souls.WastedAtCap += victim => wasted++;
            souls.Spent += count => spent += count;
            for (int i = 0; i < 5; i++) PlayerTools.Subject("BoonSoul", Team.Enemy, new Vector3(5800f, 0f, 30f)).Kill();
            if (souls.Cap != SoulsMastery.CapFor(1) + 2 || souls.Souls != souls.Cap)
                problems.Add("a soul cap bonus of 2 gave a cap of " + souls.Cap + " holding " + souls.Souls);
            if (wasted != 5 - souls.Cap) problems.Add(wasted + " kills were reported wasted at the cap, not " + (5 - souls.Cap));
            souls.TrySpend(2);
            if (spent != 2) problems.Add("spending two souls reported " + spent);

            // Blood Debt.
            BloodDebtMastery debt = host.Get<BloodDebtMastery>();
            rig.Book.Bind(PlayerTools.TestSpell("hook_abyss", SpellSchool.Abyssal), 1);
            float repaid = -1f;
            bool cleared = false;
            debt.Repaid += (amount, done) => { repaid = amount; cleared = done; };
            debt.RepayBonus = 10f;
            debt.Record(30f);
            debt.RepayOnKill();
            if (!Approx(repaid, 30f) || !cleared) problems.Add("a kill with a repay bonus of 10 repaid " + repaid + " and cleared " + cleared);

            // Psi Blades.
            PsiBladesMastery psi = host.Get<PsiBladesMastery>();
            rig.Book.Bind(PlayerTools.TestSpell("hook_psi", SpellSchool.Psionic), 2);
            psi.ChargeMultiplier = 2f;
            float psiSpent = 0f;
            psi.Spent += amount => psiSpent += amount;
            psi.AddFromHit(100f);
            if (!Approx(psi.Charge, PsiBladesMastery.ChargePerHit * 2f)) problems.Add("doubled psi gain gave " + psi.Charge);
            psi.TrySpend(0.5f);
            if (!Approx(psiSpent, 0.5f)) problems.Add("spending psi reported " + psiSpent);

            // Divine Knowledge.
            DivineKnowledgeMastery knowledge = host.Get<DivineKnowledgeMastery>();
            knowledge.RangeMultiplier = 2f;
            if (!Approx(knowledge.CurrentRange, DivineKnowledgeMastery.Range * 2f)) problems.Add("doubled Divine Knowledge range read " + knowledge.CurrentRange);

            // Arcane Warp, with its own rig so the other masteries do not share the slots.
            PlayerRig warpRig = PlayerTools.MakeRig(new Vector3(5850f, 0f, 0f), rigs);
            ArcaneWarpMastery warp = warpRig.Masteries.Get<ArcaneWarpMastery>();
            warpRig.Book.Bind(PlayerTools.TestSpell("hook_aeth", SpellSchool.Aetherics), 0);
            int ended = 0;
            warp.BonusEnded += () => ended++;

            PlayerTools.SetMana(warpRig, 50f);
            float single = warp.Bonus;
            warp.RateMultiplier = 2f;
            warp.Refresh();
            if (single <= 0f || !Approx(warp.Bonus, single * 2f)) problems.Add("doubling Arcane Warp's rate turned " + single + " into " + warp.Bonus);

            warp.LingerSeconds = 5f;
            warp.Refresh();
            PlayerTools.SetMana(warpRig, 100f);
            if (!Approx(warp.Bonus, single * 2f)) problems.Add("Arcane Warp's bonus did not linger after the pool refilled: " + warp.Bonus);
            if (ended != 0) problems.Add("Arcane Warp's bonus was reported ended while it lingered");
            warp.TickLinger(6f);
            if (!Approx(warp.Bonus, 0f) || ended != 1) problems.Add("a finished linger left a bonus of " + warp.Bonus + " and reported " + ended + " endings");

            // Conflux, with three Elemental spells for detonations.
            PlayerRig fireRig = PlayerTools.MakeRig(new Vector3(5900f, 0f, 0f), rigs);
            ConfluxMastery conflux = fireRig.Masteries.Get<ConfluxMastery>();
            for (int i = 0; i < 3; i++) fireRig.Book.Bind(PlayerTools.TestSpell("hook_fire_" + i, SpellSchool.Elemental), i);
            int bursts = 0;
            int detonations = 0;
            conflux.Reacted += (target, element, detonated) => { if (detonated) detonations++; else bursts++; };

            Health burning = PlayerTools.Subject("BoonReact", Team.Enemy, new Vector3(5900f, 0f, 20f));
            StatusController elements = burning.GetComponent<StatusController>();
            elements.Apply(StatusLibrary.Burn(), fireRig.gameObject, Team.Player);
            elements.Apply(StatusLibrary.Frost(), fireRig.gameObject, Team.Player);
            if (bursts != 1) problems.Add("two elements on one enemy reported " + bursts + " reactions");

            conflux.KeepsElements = true;
            WorldClock.Tick(ConfluxMastery.ReactionCooldown + 0.5f);
            elements.Apply(StatusLibrary.Shock(), fireRig.gameObject, Team.Player);
            if (detonations != 1) problems.Add("a third element at rank three reported " + detonations + " detonations");
            if (!elements.Has(StatusId.Burn) || !elements.Has(StatusId.Frost))
                problems.Add("a detonation that keeps its elements removed them");

            // Beast Mastery, and the reset.
            PlayerRig beastRig = PlayerTools.MakeRig(new Vector3(5950f, 0f, 0f), rigs);
            BeastMastery beast = beastRig.Masteries.Get<BeastMastery>();
            int companionDeaths = 0;
            beast.CompanionDied += () => companionDeaths++;
            beastRig.Book.Bind(PlayerTools.TestSpell("hook_beast", SpellSchool.Bestial), 0);
            if (beast.Companion == null) problems.Add("a Bestial spell summoned no companion to check");
            else
            {
                GameObject body = beast.Companion.gameObject;
                if (!beast.IsCompanionSource(body)) problems.Add("the companion is not recognised as the companion");
                beast.Companion.Health.DestroyOnDeath = false;
                beast.Companion.Health.Kill();
                if (companionDeaths != 1) problems.Add("the companion's death was reported " + companionDeaths + " times");
            }

            host.ResetForRun();
            warpRig.Masteries.ResetForRun();
            fireRig.Masteries.ResetForRun();
            if (souls.CapBonus != 0 || debt.RepayBonus != 0f || !Approx(psi.ChargeMultiplier, 1f) || !Approx(knowledge.RangeMultiplier, 1f)
                || !Approx(warp.RateMultiplier, 1f) || warp.LingerSeconds != 0f || conflux.KeepsElements)
                problems.Add("a new run kept a boon's mastery setting");
        }

        // ---------------------------------------------------------------- ally boosts

        private static void CheckAllyBoosts(List<string> problems)
        {
            MinionDefinition jackalope = MinionLibrary.Get("jackalope");
            MinionDefinition zombie = MinionLibrary.Get(PlagueStatus.ZombieId);
            if (jackalope == null || zombie == null)
            {
                problems.Add("no jackalope or plague zombie to check ally boosts with");
                return;
            }

            AllyBoosts.Active = new AllyBoosts { Health = 1f, CompanionHealth = 1f, Damage = 0.5f };

            MinionController companion = MinionSummoner.Spawn(jackalope, new Vector3(6000f, 0f, 0f));
            MinionController minion = MinionSummoner.Spawn(zombie, new Vector3(6003f, 0f, 0f));

            if (!Approx(companion.Health.Max, jackalope.Health * 3f))
                problems.Add("a companion with +100% ally and +100% companion health has " + companion.Health.Max + ", not " + jackalope.Health * 3f);
            if (!Approx(minion.Health.Max, zombie.Health * 2f))
                problems.Add("a minion with +100% ally health has " + minion.Health.Max + ", not " + zombie.Health * 2f);
            if (!Approx(minion.Health.Current, minion.Health.Max)) problems.Add("a boosted minion did not start at full health");
            if (!Approx(minion.Sheet.Get(Attr.DamageDealt), 1.5f)) problems.Add("a minion with +50% ally damage deals " + minion.Sheet.Get(Attr.DamageDealt));

            var familiar = new FamiliarDefinition { Health = 40f, DamageMultiplier = 1f };
            AllyBoosts.Active.ApplyTo(familiar);
            if (!Approx(familiar.Health, 80f) || !Approx(familiar.DamageMultiplier, 1.5f))
                problems.Add("a boosted familiar has " + familiar.Health + " health and x" + familiar.DamageMultiplier + " damage");

            AllyBoosts.Active = null;
            MinionController plain = MinionSummoner.Spawn(zombie, new Vector3(6006f, 0f, 0f));
            if (!Approx(plain.Health.Max, zombie.Health)) problems.Add("a minion summoned outside a run was boosted");
        }

        private static bool Approx(float a, float b) => Mathf.Abs(a - b) <= Tolerance;
    }
}
#endif
