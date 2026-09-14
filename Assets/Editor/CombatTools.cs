#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Exercises the combat core against real components: how every hit is labelled, executes,
    /// kill credit and lifesteal, setting health directly, sides, and a gun's events and
    /// infusions.
    ///
    /// Most of the spell designs lean on these, and none of them fails loudly. A burn tick
    /// mistaken for a bullet, or an execute lifesteal can see, does not break anything: it
    /// quietly makes a mastery or a boon wrong, which nobody notices in play.
    /// </summary>
    public static class CombatTools
    {
        private const float Tolerance = 0.01f;

        [MenuItem("Gunspire/Verify Combat Core")]
        public static void VerifyCombatCore()
        {
            var problems = new List<string>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

            try
            {
                CheckOrigins(problems);
                CheckExecutes(problems);
                CheckKillCredit(problems);
                CheckSetCurrent(problems);
                CheckSides(problems);
                CheckBonusHit(problems);
                CheckGunEventsAndInfusions(problems);
            }
            finally
            {
                // Tracers, impacts and projectiles never get the Update that removes them in edit
                // mode, so everything the checks created is cleared out here in one go.
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }

            if (problems.Count == 0)
            {
                Debug.Log("Combat core: hit origins, executes, kill credit, setting health, sides, "
                          + "gun events and infusions all behave as specified.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Combat core: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 30; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- 1.1 origins

        private static void CheckOrigins(List<string> problems)
        {
            Health burning = Subject(Team.Enemy, 1000f);
            var burnLog = new Recorder(burning);
            var status = burning.GetComponent<StatusController>();
            status.Apply(StatusLibrary.Burn(amount: 20f), null, Team.Player);
            ActiveStatus burn = status.Find(StatusId.Burn);
            if (burn != null) burn.Def.OnTick(status, burn);
            ExpectOrigin(problems, "a burn tick", burnLog, DamageOrigin.StatusTick);

            var caster = new GameObject("Caster");
            var ctx = new AbilityContext
            {
                Caster = caster, Team = Team.Player, Sheet = caster.AddComponent<CharacterSheet>()
            };

            Spell cast = FirstInSlot(SpellSlot.Cast);
            if (cast == null) problems.Add("no cast spell in the roster to test with");
            else
            {
                ctx.BeginCast(cast, 1);
                ExpectBuilt(problems, "a cast spell's hit", ctx, DamageOrigin.Spell);

                // A spell's projectile has to carry the origin across to where it lands.
                new SpawnProjectileEffect { Damage = 1f }.Execute(ctx);
                Projectile spellRound = null;
                foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    if (p.Owner == caster) spellRound = p;

                if (spellRound == null) problems.Add("a spell's projectile effect spawned no projectile");
                else if (spellRound.BuildHitDamage(Vector3.zero, Vector3.up).Origin != DamageOrigin.Spell)
                    problems.Add("a spell's projectile hit was reported as "
                                 + spellRound.BuildHitDamage(Vector3.zero, Vector3.up).Origin);
            }

            Spell melee = SpellLibrary.DefaultMelee;
            if (melee != null)
            {
                ctx.BeginCast(melee, 1);
                ExpectBuilt(problems, "a melee spell's hit", ctx, DamageOrigin.Melee);
            }

            ctx.Begin(DamageType.Kinetic, SpellType.Attack, Color.white, 1, 1f, isSpell: false);
            ExpectBuilt(problems, "an ability that is not a spell", ctx, DamageOrigin.Attack);

            var familiar = new GameObject("Familiar").AddComponent<FamiliarController>();
            if (familiar.AttackOrigin != DamageOrigin.Minion)
                problems.Add("a familiar's attacks report as " + familiar.AttackOrigin + ", not Minion");

            var enemy = new GameObject("Enemy").AddComponent<EnemyController>();
            if (enemy.AttackOrigin != DamageOrigin.Attack)
                problems.Add("an enemy's attacks report as " + enemy.AttackOrigin + ", not Attack");
        }

        // ---------------------------------------------------------------- 1.2 executes

        private static void CheckExecutes(List<string> problems)
        {
            Health marked = Subject(Team.Enemy, 500f);
            var markedLog = new Recorder(marked);
            marked.GetComponent<StatusController>().Apply(StatusLibrary.Deathmark(), null, Team.Player);
            marked.TakeDamage(DamageInfo.Create(1f, DamageType.Energy, Team.Player, null));
            ExpectExecuteReported(problems, "a death mark", marked, markedLog, expectDead: true);

            Health frozen = Subject(Team.Enemy, 500f);
            var frozenLog = new Recorder(frozen);
            frozen.GetComponent<StatusController>().Apply(StatusLibrary.Frost(stacks: FrostStatus.FullStacks), null, Team.Player);
            frozen.TakeDamage(DamageInfo.Create(1f, DamageType.Kinetic, Team.Player, null));
            ExpectExecuteReported(problems, "a frost shatter", frozen, frozenLog, expectDead: true);

            Health elite = Subject(Team.Enemy, 1000f, elite: true);
            var eliteLog = new Recorder(elite);
            elite.GetComponent<StatusController>().Apply(StatusLibrary.Deathmark(), null, Team.Player);
            elite.TakeDamage(DamageInfo.Create(1f, DamageType.Energy, Team.Player, null));
            ExpectExecuteReported(problems, "a death mark on an elite", elite, eliteLog, expectDead: false);
            ExpectHealth(problems, "an elite after a death mark", elite, 1000f * (1f - DeathmarkStatus.EliteFraction));

            DamageInfo cause = DamageInfo.Create(0f, DamageType.Necrotic, Team.Player, null);

            Health chunked = Subject(Team.Enemy, 1000f, elite: true);
            chunked.Execute(cause, 0.25f);
            ExpectHealth(problems, "an elite executed for a quarter", chunked, 750f);

            Health finished = Subject(Team.Enemy, 1000f, elite: true);
            finished.Execute(cause, Health.FinishesElites);
            if (finished.IsAlive) problems.Add("an execute set to finish elites left one alive");

            Health player = Subject(Team.Player, 200f);
            if (player.Execute(cause, Health.FinishesElites) || !player.IsAlive)
                problems.Add("the player was executed");
        }

        // ---------------------------------------------------------------- 1.3 kill credit

        private static void CheckKillCredit(List<string> problems)
        {
            if (!RunState.CountsAsKill(Subject(Team.Enemy, 10f)))
                problems.Add("an enemy death does not count as the player's kill");
            if (RunState.CountsAsKill(Subject(Team.Player, 10f)))
                problems.Add("a death on the player's own side counts as a kill");

            var player = new GameObject("Player");
            var somebodyElse = new GameObject("Minion");

            foreach (DamageOrigin origin in System.Enum.GetValues(typeof(DamageOrigin)))
            {
                bool expected = origin == DamageOrigin.Gun || origin == DamageOrigin.Spell
                                || origin == DamageOrigin.Melee || origin == DamageOrigin.StatusTick;

                DamageInfo own = DamageInfo.Create(10f, DamageType.Kinetic, Team.Player, player);
                own.Origin = origin;

                if (RunState.GrantsLifesteal(own, player) != expected)
                    problems.Add("lifesteal is " + (expected ? "refused" : "granted") + " for the player's own "
                                 + origin + " hit");
            }

            DamageInfo execute = DamageInfo.Create(10f, DamageType.Execute, Team.Player, player);
            execute.Origin = DamageOrigin.Gun;
            if (RunState.GrantsLifesteal(execute, player)) problems.Add("lifesteal is granted for an execute");

            DamageInfo borrowed = DamageInfo.Create(10f, DamageType.Kinetic, Team.Player, somebodyElse);
            borrowed.Origin = DamageOrigin.Gun;
            if (RunState.GrantsLifesteal(borrowed, player))
                problems.Add("lifesteal is granted for a hit something else on the player's side dealt");
        }

        // ---------------------------------------------------------------- 1.4 setting health

        private static void CheckSetCurrent(List<string> problems)
        {
            Health health = Subject(Team.Player, 200f);
            var log = new Recorder(health);
            var status = health.GetComponent<StatusController>();
            status.Apply(StatusLibrary.Bleed(), null, Team.Enemy);

            health.SetCurrent(50f);
            ExpectHealth(problems, "setting health down to 50", health, 50f);

            health.SetCurrent(180f);
            ExpectHealth(problems, "setting health up to 180", health, 180f);

            if (!status.Has(StatusId.Bleed)) problems.Add("setting health upward cured a bleed, as a heal does");
            if (log.Hits.Count > 0 || log.Heals > 0)
                problems.Add("setting health raised " + log.Hits.Count + " damage and " + log.Heals + " heal events");

            health.SetCurrent(-40f);
            if (!health.IsAlive || health.Current <= 0f) problems.Add("setting health to -40 killed, and it never may");

            health.SetCurrent(9999f);
            ExpectHealth(problems, "setting health above the maximum", health, health.Max);
        }

        // ---------------------------------------------------------------- 1.6 sides

        private static void CheckSides(List<string> problems)
        {
            Health enemy = Subject(Team.Enemy, 100f);
            Health player = Subject(Team.Player, 100f);
            var confused = new GameObject("Confused");

            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.Energy, Team.Neutral, confused));
            ExpectHealth(problems, "an enemy hit by a neutral attack", enemy, 90f);

            player.TakeDamage(DamageInfo.Create(10f, DamageType.Energy, Team.Neutral, confused));
            ExpectHealth(problems, "the player hit by a neutral attack", player, 90f);

            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.Energy, Team.Enemy, confused));
            ExpectHealth(problems, "an enemy hit by another enemy's attack", enemy, 90f);

            enemy.TakeDamage(DamageInfo.Create(10f, DamageType.Energy, Team.Neutral, enemy.gameObject));
            ExpectHealth(problems, "an enemy hit by its own neutral attack", enemy, 90f);

            foreach (int layer in new[] { Layers.Player, Layers.Enemy, Layers.Familiar, Layers.Level })
                if ((Layers.HitMaskFor(Team.Neutral) & (1 << layer)) == 0)
                    problems.Add("a neutral attack cannot hit layer " + layer);

            foreach (int layer in new[] { Layers.Player, Layers.Enemy, Layers.Familiar })
                if ((Layers.TargetMaskFor(Team.Neutral) & (1 << layer)) == 0)
                    problems.Add("a neutral attack cannot target layer " + layer);

            if ((Layers.HitMaskFor(Team.Player) & (1 << Layers.Player)) != 0)
                problems.Add("the player's own shots can hit the player's layer");

            Health bumped = Subject(Team.Enemy, 100f);
            var bumpLog = new Recorder(bumped);
            bumped.TakeDamage(DamageInfo.OnBehalfOf(15f, DamageType.Kinetic, Team.Player, confused, DamageOrigin.Collision));
            ExpectHealth(problems, "an enemy hurt on the player's behalf", bumped, 85f);
            ExpectOrigin(problems, "damage on the player's behalf", bumpLog, DamageOrigin.Collision);

            var body = new GameObject("Turncoat") { layer = Layers.Enemy };
            var bodyHealth = body.AddComponent<Health>();
            bodyHealth.Team = Team.Enemy;
            var controller = body.AddComponent<EnemyController>();

            controller.SetAttackTeam(Team.Neutral);
            if (controller.Team != Team.Neutral) problems.Add("SetAttackTeam(Neutral) left its attacks on " + controller.Team);
            if (bodyHealth.Team != Team.Enemy) problems.Add("changing only an enemy's attacks moved its body to " + bodyHealth.Team);

            controller.SetSide(Team.Player);
            if (controller.Team != Team.Player || bodyHealth.Team != Team.Player || body.layer != Layers.Familiar)
                problems.Add("SetSide(Player) left attacks on " + controller.Team + ", body on " + bodyHealth.Team
                             + ", layer " + body.layer);

            controller.SetSide(Team.Enemy);
            if (controller.Team != Team.Enemy || bodyHealth.Team != Team.Enemy || body.layer != Layers.Enemy)
                problems.Add("SetSide(Enemy) did not bring the enemy all the way back");

            Projectile round = Projectile.Create(Vector3.zero, Vector3.forward, Color.white, 0.1f);
            round.OwnerTeam = Team.Enemy;
            round.Launch();
            round.SwitchSide(Team.Player, confused, null, Vector3.back);

            if (round.OwnerTeam != Team.Player) problems.Add("a reflected projectile stayed on " + round.OwnerTeam);
            if (round.HitMask != Layers.HitMaskFor(Team.Player)) problems.Add("a reflected projectile kept the old side's hit mask");
            if (round.gameObject.layer != Layers.PlayerProjectile) problems.Add("a reflected projectile kept the old side's layer");
            if (Vector3.Dot(round.transform.forward, Vector3.back) < 0.99f) problems.Add("a reflected projectile did not turn around");
        }

        // ---------------------------------------------------------------- 1.7 bonus hits

        /// <summary>
        /// Ethereal throws a kinetic hit away before anything else is considered, so a necrotic bonus
        /// folded into that hit would vanish with it. Dealt as its own instance, it lands doubled.
        /// </summary>
        private static void CheckBonusHit(List<string> problems)
        {
            Health ghost = Subject(Team.Enemy, 1000f);
            ghost.EtherealByNature = true;

            DamageInfo kinetic = DamageInfo.Create(40f, DamageType.Kinetic, Team.Player, null);
            ghost.TakeDamage(kinetic);

            new WeaponHit { Target = ghost, Damage = kinetic, Point = ghost.transform.position, Normal = Vector3.up }
                .DealBonus(10f, DamageType.Necrotic);

            ExpectHealth(problems, "an ethereal enemy after a kinetic hit with a 10 necrotic bonus", ghost, 980f);
        }

        // ---------------------------------------------------------------- 1.7 and 1.8 guns

        /// <summary>
        /// Fires real guns at a real collider: one trigger pull is one Fired, every round that lands
        /// raises Hit with a gun origin, infusions ride along and expire by round and by time, and
        /// a charge survives swapping guns and leaves with a projectile.
        /// </summary>
        private static void CheckGunEventsAndInfusions(List<string> problems)
        {
            WeaponDefinition hitscanGun = FindGun(DeliveryKind.Hitscan);
            WeaponDefinition projectileGun = FindGun(DeliveryKind.Projectile);
            if (hitscanGun == null || projectileGun == null)
            {
                problems.Add("need a hitscan and a projectile gun with no burst, mana cost or splash to test with");
                return;
            }

            var shooter = new GameObject("Shooter");
            shooter.transform.position = new Vector3(0f, 1f, 0f);
            var weapon = shooter.AddComponent<Weapon>();
            weapon.OwnerTeam = Team.Player;
            weapon.Owner = shooter;
            weapon.AimOrigin = shooter.transform;

            Health target = Subject(Team.Enemy, 100000f, name: "Target");
            target.transform.position = new Vector3(0f, 1f, 8f);
            target.gameObject.layer = Layers.Enemy;
            target.gameObject.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 1f);
            Physics.SyncTransforms();

            int fired = 0;
            var hits = new List<WeaponHit>();
            weapon.Fired += shot => fired++;
            weapon.Hit += hit => hits.Add(hit);

            weapon.Equip(hitscanGun);

            int infusionHits = 0;
            BulletInfusion oneRound = BulletInfusion.ForRounds("one_round", 1, StatusLibrary.Poison());
            oneRound.OnHit = hit => infusionHits++;
            weapon.Infuse(oneRound);
            weapon.Infuse(BulletInfusion.ForSeconds("timed", 5f, StatusLibrary.Weaken()));

            weapon.TryFire();

            if (fired != 1) problems.Add("one pull of " + hitscanGun.Id + " raised Fired " + fired + " times");

            if (hits.Count == 0)
            {
                problems.Add(hitscanGun.Id + " fired into a wall-sized target and raised no Hit");
            }
            else
            {
                if (!ReferenceEquals(hits[0].Target, target)) problems.Add("Hit named something other than the target");
                if (hits[0].Damage.Origin != DamageOrigin.Gun)
                    problems.Add("a hitscan round's hit was reported as " + hits[0].Damage.Origin);
                if (infusionHits != hits.Count)
                    problems.Add("an infusion's on-hit ran " + infusionHits + " times for " + hits.Count + " hits");
            }

            var targetStatus = target.GetComponent<StatusController>();
            if (!targetStatus.Has(StatusId.Poison)) problems.Add("a one-round infusion's poison did not land");
            if (!targetStatus.Has(StatusId.Weaken)) problems.Add("a timed infusion's weaken did not land");
            if (HasInfusion(weapon, "one_round")) problems.Add("a one-round infusion was not spent by its round");
            if (!HasInfusion(weapon, "timed")) problems.Add("a timed infusion was spent by a round");

            weapon.TickInfusions(6f);
            if (HasInfusion(weapon, "timed")) problems.Add("a five-second infusion outlived six seconds");

            weapon.Infuse(BulletInfusion.ForSeconds("carried", 10f, StatusLibrary.Shock()));
            weapon.Equip(projectileGun);
            if (!HasInfusion(weapon, "carried")) problems.Add("an infusion did not survive swapping guns");

            weapon.TryFire();

            Projectile round = null;
            foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if (p.SourceWeapon == weapon) round = p;

            if (round == null)
            {
                problems.Add(projectileGun.Id + " fired no projectile that knows which gun it came from");
                return;
            }

            if (round.Origin != DamageOrigin.Gun) problems.Add("a gun's projectile reports as " + round.Origin);
            if (round.Infusions == null || !round.Infusions.Exists(i => i.Id == "carried"))
                problems.Add("a projectile left without the gun's infusions, so its hit could not run them");
            if (round.Statuses == null || !round.Statuses.Exists(s => s.Id == StatusId.Shock))
                problems.Add("a projectile left without its infusion's statuses");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A living thing on the given team, with health and statuses, that is never destroyed on death.</summary>
        private static Health Subject(Team team, float health, bool elite = false, string name = "Subject")
        {
            var go = new GameObject(name);
            go.AddComponent<CharacterSheet>().SetBaseOverride(Attr.MaxHealth, health);
            go.AddComponent<StatusController>();

            var hp = go.AddComponent<Health>();
            hp.Team = team;
            hp.IsElite = elite;
            hp.DestroyOnDeath = false;

            // Edit mode never calls Awake, so the maximum is set the way enemies are built.
            hp.ConfigureMaxHealth(health, refill: true);
            return hp;
        }

        private sealed class Recorder
        {
            public readonly List<DamageInfo> Hits = new List<DamageInfo>();
            public DamageInfo? Death;
            public int Heals;

            public Recorder(Health health)
            {
                health.Damaged += (info, amount) => Hits.Add(info);
                health.Died += info => Death = info;
                health.Healed += amount => Heals++;
            }
        }

        private static void ExpectOrigin(List<string> problems, string what, Recorder log, DamageOrigin expected)
        {
            if (log.Hits.Count == 0) problems.Add(what + " dealt no damage to check");
            else if (log.Hits[log.Hits.Count - 1].Origin != expected)
                problems.Add(what + " was reported as " + log.Hits[log.Hits.Count - 1].Origin + ", not " + expected);
        }

        private static void ExpectBuilt(List<string> problems, string what, AbilityContext ctx, DamageOrigin expected)
        {
            DamageOrigin actual = ctx.BuildDamage(1f, Vector3.zero, Vector3.up).Origin;
            if (actual != expected) problems.Add(what + " was reported as " + actual + ", not " + expected);
        }

        private static void ExpectExecuteReported(List<string> problems, string what, Health health, Recorder log,
            bool expectDead)
        {
            if (health.IsAlive == expectDead)
                problems.Add(what + (expectDead ? " left the target alive" : " killed an elite outright"));

            if (log.Hits.Count == 0 || log.Hits[log.Hits.Count - 1].Type != DamageType.Execute)
                problems.Add(what + " was reported as "
                             + (log.Hits.Count == 0 ? "no damage" : log.Hits[log.Hits.Count - 1].Type.ToString())
                             + ", not as an execute");

            if (expectDead && (log.Death == null || log.Death.Value.Type != DamageType.Execute))
                problems.Add(what + "'s death was reported as "
                             + (log.Death == null ? "nothing" : log.Death.Value.Type.ToString()) + ", not as an execute");
        }

        private static void ExpectHealth(List<string> problems, string what, Health health, float expected)
        {
            if (Mathf.Abs(health.Current - expected) > Tolerance)
                problems.Add(what + ": health " + health.Current.ToString("0.##") + ", expected " + expected.ToString("0.##"));
        }

        private static Spell FirstInSlot(SpellSlot slot)
        {
            List<Spell> spells = SpellLibrary.ForSlot(slot);
            return spells.Count > 0 ? spells[0] : null;
        }

        private static WeaponDefinition FindGun(DeliveryKind delivery)
        {
            foreach (WeaponDefinition gun in WeaponLibrary.All)
                if (gun.Delivery == delivery && gun.Mode != FireMode.Burst && gun.ManaPerShot <= 0f
                    && gun.SplashRadius <= 0f && gun.MagazineSize > 1)
                    return gun;
            return null;
        }

        private static bool HasInfusion(Weapon weapon, string id)
        {
            for (int i = 0; i < weapon.Infusions.Count; i++)
                if (weapon.Infusions[i].Id == id) return true;
            return false;
        }
    }
}
#endif
