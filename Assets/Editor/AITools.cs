#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Exercises the enemy AI foundations against real components: who an enemy chooses to fight,
    /// what it perceives, what its statuses let it do, fleeing, hazards and pulls, hiding an enemy
    /// without deleting it, and copying one.
    ///
    /// These are rules that fail quietly. An enemy that keeps aiming at you while blind, a banished
    /// enemy that lets its room clear, or a copy that is not counted all look like the game working
    /// until the moment they matter.
    /// </summary>
    public static class AITools
    {
        [MenuItem("Gunspire/Verify Enemy AI")]
        public static void VerifyEnemyAI()
        {
            var problems = new List<string>();
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

            try
            {
                CheckTargetChoice(problems);
                CheckPerception(problems);
                CheckControlHooks(problems);
                CheckAttackReach(problems);
                CheckFearSilenceAndConfusion(problems);
                CheckFleeing(problems);
                CheckHazardsAndPulls(problems);
                CheckHiding(problems);
                CheckCopying(problems);
            }
            finally
            {
                TargetRegistry.Clear();
                Hazards.Clear();

                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }

            if (problems.Count == 0)
            {
                Debug.Log("Enemy AI: targeting, perception, control hooks, fleeing, hazards, hiding and "
                          + "copying all behave as specified.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Enemy AI: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 30; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- 2.1 targeting

        private static void CheckTargetChoice(List<string> problems)
        {
            TargetRegistry.Entry body = Entry("Body", new Vector3(0f, 0f, 10f), isPlayerBody: true);
            TargetRegistry.Entry minion = Entry("Minion", new Vector3(0f, 0f, 4f));
            var candidates = new List<TargetRegistry.Entry> { body, minion };

            ExpectChoice(problems, "with nothing held",
                TargetRegistry.Choose(Vector3.zero, null, false, candidates, null), minion);
            ExpectChoice(problems, "when another target is 6m closer than the one held",
                TargetRegistry.Choose(Vector3.zero, body.Transform, false, candidates, null), minion);

            minion.Transform.position = new Vector3(0f, 0f, 9f);
            ExpectChoice(problems, "when another target is only 1m closer than the one held",
                TargetRegistry.Choose(Vector3.zero, body.Transform, false, candidates, null), body);

            minion.Transform.position = new Vector3(0f, 0f, 4f);
            ExpectChoice(problems, "as an elite that can reach the player's body",
                TargetRegistry.Choose(Vector3.zero, null, true, candidates, e => true), body);
            ExpectChoice(problems, "as an elite that cannot reach the player's body",
                TargetRegistry.Choose(Vector3.zero, null, true, candidates, e => !e.IsPlayerBody), minion);

            minion.Health.TakeDamage(DamageInfo.Create(1000000f, DamageType.True, Team.Enemy, null));
            ExpectChoice(problems, "with the nearer target dead",
                TargetRegistry.Choose(Vector3.zero, null, false, candidates, null), body);

            TargetRegistry.Clear();
            TargetRegistry.SetPlayerBody(body.Transform, body.Health);
            if (TargetRegistry.PlayerBody.Transform != body.Transform)
                problems.Add("the registry did not take a new player body");

            TargetRegistry.Entry registered = Entry("Registered", new Vector3(2f, 0f, 0f));
            registered = TargetRegistry.RegisterMinion(registered.Transform, registered.Health);

            var collected = new List<TargetRegistry.Entry>();
            TargetRegistry.Collect(collected);
            if (collected.Count != 2 || collected[0] != TargetRegistry.PlayerBody)
                problems.Add("the registry collected " + collected.Count + " targets, expected the player's body then one minion");

            registered.HiddenFromHearing = true;
            if (!TargetRegistry.IsInaudibleAt(registered.Transform.position))
                problems.Add("a noise from something hidden from hearing could still be heard");

            TargetRegistry.Unregister(registered);
            TargetRegistry.Collect(collected);
            if (collected.Count != 1) problems.Add("an unregistered minion is still a target");
        }

        // ---------------------------------------------------------------- 2.2 and 2.3 perception

        private static void CheckPerception(List<string> problems)
        {
            TargetRegistry.Clear();
            TargetRegistry.Entry body = Entry("PerceivedBody", new Vector3(0f, 0f, 8f), isPlayerBody: true);
            TargetRegistry.Entry registered = TargetRegistry.SetPlayerBody(body.Transform, body.Health);

            EnemyController enemy = Spawn("cultist", Vector3.zero);
            if (enemy == null)
            {
                problems.Add("could not spawn a cultist to test perception with");
                return;
            }
            Physics.SyncTransforms();

            if (!enemy.CanSee(registered)) problems.Add("an enemy could not see the player's body 8m straight ahead");

            enemy.Alert();
            if (enemy.Target != body.Transform) problems.Add("an alerted enemy did not take the player's body as its target");

            enemy.Status.Apply(StatusLibrary.Blind(10f), null, Team.Player);
            enemy.RefreshTarget();
            ExpectFrozenAt(problems, "while blind", enemy, body.Transform, new Vector3(0f, 0f, 8f));
            if (enemy.CanSee(registered)) problems.Add("a blind enemy could still see");

            body.Transform.position = new Vector3(5f, 0f, 8f);
            enemy.RefreshTarget();
            ExpectFrozenAt(problems, "while blind, after its target moved", enemy, body.Transform, new Vector3(0f, 0f, 8f));

            enemy.Status.Remove(StatusId.Blind);
            enemy.RefreshTarget();
            if (enemy.Target != body.Transform) problems.Add("an enemy whose sight returned did not go back to the live target");

            registered.HiddenFromSight = true;
            enemy.RefreshTarget();
            ExpectFrozenAt(problems, "with its target hidden from sight", enemy, body.Transform, new Vector3(5f, 0f, 8f));
            if (enemy.CanSee(registered)) problems.Add("an enemy could see a target hidden from sight");
            registered.HiddenFromSight = false;
        }

        // ---------------------------------------------------------------- 2.4 control

        private static void CheckControlHooks(List<string> problems)
        {
            ExpectControl(problems, "a snare", StatusLibrary.Snare(3f, 0.4f), canMove: true, ranged: true, melee: true);
            ExpectControl(problems, "a stun", StatusLibrary.Stun(2f), canMove: false, ranged: false, melee: false);
            ExpectControl(problems, "fear", StatusLibrary.Fear(3f), canMove: true, ranged: false, melee: false);
            ExpectControl(problems, "silence", StatusLibrary.Silence(3f), canMove: true, ranged: false, melee: true);
            ExpectControl(problems, "disarm", StatusLibrary.Disarm(3f), canMove: true, ranged: true, melee: false);
            ExpectControl(problems, "sleep", StatusLibrary.Sleep(3f), canMove: false, ranged: false, melee: false);

            StatusController slowed = Subject(elite: false);
            CharacterSheet sheet = slowed.GetComponent<CharacterSheet>();
            sheet.SetBaseOverride(Attr.MoveSpeed, 10f);
            slowed.Apply(StatusLibrary.Snare(3f, 0.4f), null, Team.Player);
            float speed = sheet.Get(Attr.MoveSpeed);
            if (Mathf.Abs(speed - 6f) > 0.05f)
                problems.Add("a 40% snare left speed at " + speed.ToString("0.##") + " of 10, expected 6");

            StatusController elite = Subject(elite: true);
            ExpectEliteDuration(problems, elite, StatusLibrary.Fear(4f), 2f);
            ExpectEliteDuration(problems, elite, StatusLibrary.Stun(2f), 1f);

            StatusController confused = Subject(elite: false);
            Health health = confused.GetComponent<Health>();

            health.TakeDamage(DamageInfo.Create(5f, DamageType.Energy, Team.Player, null)
                .WithStatus(StatusLibrary.Confusion(5f)));
            if (!confused.IsConfused) problems.Add("the damaging hit that applied confusion also cured it");

            confused.Apply(StatusLibrary.Burn(amount: 10f), null, Team.Player);
            ActiveStatus burn = confused.Find(StatusId.Burn);
            if (burn != null) burn.Def.OnTick(confused, burn);
            if (!confused.IsConfused) problems.Add("a burn tick cured confusion");

            health.TakeDamage(DamageInfo.Create(5f, DamageType.Kinetic, Team.Player, null));
            if (confused.IsConfused) problems.Add("a direct hit did not cure confusion");
        }

        /// <summary>Silence and disarm are only meaningful if the roster has both kinds of attack.</summary>
        private static void CheckAttackReach(List<string> problems)
        {
            int melee = 0, total = 0;
            foreach (EnemyDefinition def in EnemyLibrary.All)
            {
                foreach (AttackDefinition attack in def.Attacks)
                {
                    if (attack == null) continue;
                    total++;
                    if (attack.Reach == AttackReach.Melee) melee++;
                }
            }

            if (melee == 0) problems.Add("no enemy attack is tagged melee, so disarm does nothing to any enemy");
            if (melee == total) problems.Add("every enemy attack is tagged melee, so silence does nothing to any enemy");

            EnemyController hound = Spawn("hound", new Vector3(20f, 0f, 0f));
            AbilityAttack lunge = hound != null ? hound.GetComponent<AbilityAttack>() : null;
            if (lunge == null) problems.Add("could not spawn a hound to check its lunge");
            else if (lunge.Reach != AttackReach.Melee) problems.Add("the hound's lunge is built as " + lunge.Reach + ", not melee");
        }

        private static void CheckFearSilenceAndConfusion(List<string> problems)
        {
            TargetRegistry.Clear();
            TargetRegistry.Entry body = Entry("FearBody", new Vector3(0f, 0f, -30f), isPlayerBody: true);
            TargetRegistry.SetPlayerBody(body.Transform, body.Health);

            EnemyController enemy = Spawn("cultist", new Vector3(-20f, 0f, 0f));
            AbilityAttack attack = enemy != null ? enemy.GetComponent<AbilityAttack>() : null;
            if (attack == null)
            {
                problems.Add("could not spawn a cultist with an attack to test fear with");
                return;
            }

            // A long wind-up and nothing else, so the attack is certainly still executing when checked.
            attack.Sequence = new List<AbilityEffect> { new WaitEffect { Seconds = 30f } };
            attack.Initialise(enemy);
            attack.Begin();
            if (!attack.IsExecuting)
            {
                problems.Add("an attack that was begun is not executing, so cancelling cannot be tested");
                return;
            }

            if (enemy.IsAlerted) problems.Add("the test enemy was alerted before anything happened to it");

            enemy.Status.Apply(StatusLibrary.Fear(3f), null, Team.Player);
            enemy.SyncStatusEffects();
            if (!enemy.IsAlerted) problems.Add("fear did not alert an enemy that had not noticed anything");
            if (attack.IsExecuting) problems.Add("an attack winding up when fear landed was not cancelled");

            enemy.Status.Remove(StatusId.Fear);
            enemy.SyncStatusEffects();

            attack.Begin();
            enemy.Status.Apply(StatusLibrary.Disarm(3f), null, Team.Player);
            enemy.SyncStatusEffects();
            if (!attack.IsExecuting) problems.Add("disarm cancelled a ranged attack");

            enemy.Status.Apply(StatusLibrary.Silence(3f), null, Team.Player);
            enemy.SyncStatusEffects();
            if (attack.IsExecuting) problems.Add("silence did not cancel a ranged attack winding up");
            attack.Cancel();

            enemy.Status.Apply(StatusLibrary.Confusion(5f), null, Team.Player);
            enemy.SyncStatusEffects();
            if (enemy.Team != Team.Neutral) problems.Add("a confused enemy attacks as " + enemy.Team + ", not neutral");

            enemy.Status.Remove(StatusId.Confusion);
            enemy.SyncStatusEffects();
            if (enemy.Team != Team.Enemy) problems.Add("an enemy that recovered from confusion attacks as " + enemy.Team);
        }

        // ---------------------------------------------------------------- 2.5 movement

        private static void CheckFleeing(List<string> problems)
        {
            var host = new GameObject("FleeField");
            var field = host.AddComponent<NavField>();
            field.Build(30f, 30f);
            field.Rebuild(field.WorldToCell(Vector3.zero));

            var from = new Vector3(4f, 0f, 0f);
            ExpectDirection(problems, "fleeing the player up the field", field.FleeDirection(from), Vector3.right);
            ExpectDirection(problems, "stepping away from a point behind", field.AwayFrom(from, Vector3.zero), Vector3.right);
            ExpectDirection(problems, "stepping away from a point ahead", field.AwayFrom(from, new Vector3(8f, 0f, 0f)), Vector3.left);

            TargetRegistry.Clear();
            TargetRegistry.Entry body = Entry("FleeBody", Vector3.zero, isPlayerBody: true);
            TargetRegistry.SetPlayerBody(body.Transform, body.Health);

            EnemyController enemy = Spawn("cultist", new Vector3(4f, 0f, 0f));
            enemy.Status.Apply(StatusLibrary.Fear(3f), body.Transform.gameObject, Team.Player);
            ExpectDirection(problems, "a feared enemy running from the player's body", enemy.FleeDirection(), Vector3.right);

            EnemyController other = Spawn("cultist", new Vector3(4f, 0f, 10f));
            var totem = new GameObject("Totem");
            totem.transform.position = new Vector3(8f, 0f, 10f);
            other.Status.Apply(StatusLibrary.Fear(3f), totem, Team.Player);
            Object.DestroyImmediate(totem);
            ExpectDirection(problems, "a feared enemy whose source has despawned", other.FleeDirection(), Vector3.left);
        }

        private static void CheckHazardsAndPulls(List<string> problems)
        {
            Hazards.Clear();
            Hazards.Hazard fire = Hazards.Register(Vector3.zero, 2f, Team.Player);

            Vector3 inside = Hazards.PushAt(new Vector3(1f, 0f, 0f), Team.Enemy);
            if (inside.x <= 0f) problems.Add("an enemy inside the player's hazard is not pushed out of it");
            if (Hazards.PushAt(new Vector3(1f, 0f, 0f), Team.Player).sqrMagnitude > 0f)
                problems.Add("a hazard pushes the side that made it");
            if (Hazards.PushAt(new Vector3(10f, 0f, 0f), Team.Enemy).sqrMagnitude > 0f)
                problems.Add("a hazard pushes from well beyond its edge");
            if (Hazards.PushAt(new Vector3(3f, 0f, 0f), Team.Enemy).magnitude >= inside.magnitude)
                problems.Add("a hazard pushes as hard near its edge as near its centre, so the push is not soft");

            Hazards.Unregister(fire);
            if (Hazards.PushAt(new Vector3(1f, 0f, 0f), Team.Enemy).sqrMagnitude > 0f)
                problems.Add("a removed hazard still pushes");

            int count = Hazards.Count;
            LingeringZone.Spawn(new Vector3(0f, 0f, 40f), 3f, 5f, 1f, 0.5f, DamageType.Energy, Team.Player,
                null, null, Color.white);
            if (Hazards.Count != count + 1) problems.Add("a lingering zone did not register itself as a hazard");

            EnemyController pulled = Spawn("cultist", new Vector3(0f, 0f, 60f));
            Combat.PullToward(pulled.transform, new Vector3(0f, 0f, 50f), 8f);
            if (Vector3.Dot(pulled.ExternalVelocity, Vector3.back) < 7.9f)
                problems.Add("a pull toward a point left the enemy moving at " + pulled.ExternalVelocity
                             + ", expected 8 toward the point");
        }

        // ---------------------------------------------------------------- 2.6 hiding

        private static void CheckHiding(List<string> problems)
        {
            EnemyController enemy = Spawn("cultist", new Vector3(0f, 0f, -60f));
            var room = new GameObject("HideRoom").AddComponent<RoomRuntime>();
            room.Register(enemy);

            Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length < 2)
            {
                problems.Add("the test enemy has too few renderers to check hiding with");
                return;
            }

            // Off before hiding, so it has to stay off after revealing.
            renderers[0].enabled = false;

            var controller = enemy.GetComponent<CharacterController>();
            AbilityAttack attack = enemy.GetComponent<AbilityAttack>();
            enemy.Status.Apply(StatusLibrary.Weaken(10f), null, Team.Player);
            ActiveStatus weaken = enemy.Status.Find(StatusId.Weaken);

            enemy.Hide();

            if (!enemy.IsHidden) problems.Add("Hide did not mark the enemy hidden");
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                problems.Add("a hidden enemy still draws " + renderers[i].name);
                break;
            }
            if (controller.enabled) problems.Add("a hidden enemy's body still collides");
            if (attack != null && attack.enabled) problems.Add("a hidden enemy's attack timers still run");
            if (!room.Contains(enemy) || room.EnemiesRemaining != 1)
                problems.Add("hiding an enemy took it out of its room, which would let the room clear");

            float remaining = weaken.Remaining;
            enemy.Status.Tick(1f);
            if (Mathf.Abs(weaken.Remaining - remaining) > 0.001f) problems.Add("a hidden enemy's statuses kept running down");

            enemy.Reveal();

            if (enemy.IsHidden) problems.Add("Reveal did not bring the enemy back");
            if (renderers[0].enabled) problems.Add("revealing switched on a renderer that was off before the enemy was hidden");
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i].enabled) continue;
                problems.Add("revealing left " + renderers[i].name + " switched off");
                break;
            }
            if (!controller.enabled) problems.Add("a revealed enemy's body does not collide");
            if (attack != null && !attack.enabled) problems.Add("a revealed enemy's attacks stayed paused");

            enemy.Status.Tick(1f);
            if (weaken.Remaining > remaining - 0.5f) problems.Add("a revealed enemy's statuses did not start running again");
        }

        // ---------------------------------------------------------------- 2.7 copying

        private static void CheckCopying(List<string> problems)
        {
            TargetRegistry.Clear();
            TargetRegistry.Entry body = Entry("CopyBody", new Vector3(0f, 0f, 90f), isPlayerBody: true);
            TargetRegistry.SetPlayerBody(body.Transform, body.Health);

            EnemyController original = EnemyFactory.Spawn("cultist", new Vector3(0f, 0f, 80f), 2, elite: true);
            if (original == null)
            {
                problems.Add("could not spawn an elite cultist to copy");
                return;
            }

            original.Health.DestroyOnDeath = false;
            original.Health.ConfigureMaxHealth(400f);
            original.Health.SetCurrent(250f);
            original.Status.Apply(StatusLibrary.Fear(8f), null, Team.Player);
            original.Status.Apply(StatusLibrary.Deathmark(8f), null, Team.Player);
            original.Alert();

            var room = new GameObject("CopyRoom").AddComponent<RoomRuntime>();
            room.Register(original);

            var at = new Vector3(3f, 0f, 80f);
            EnemyController copy = EnemyFactory.Copy(original, at, id => id == StatusId.Deathmark);
            if (copy == null)
            {
                problems.Add("copying a live enemy returned nothing");
                return;
            }

            if (copy.Definition != original.Definition || copy.Floor != 2)
                problems.Add("a copy is not the same archetype on the same floor");
            if (copy.Health == null || !copy.Health.IsElite) problems.Add("a copy of an elite is not elite");
            else if (Mathf.Abs(copy.Health.Max - 400f) > 0.01f || Mathf.Abs(copy.Health.Current - 250f) > 0.01f)
                problems.Add("a copy has " + copy.Health.Current + " of " + copy.Health.Max + " health, expected 250 of 400");

            if (!copy.Status.Has(StatusId.Fear)) problems.Add("a copy did not inherit the original's statuses");
            if (copy.Status.Has(StatusId.Deathmark)) problems.Add("a copy inherited a status it was told to leave out");

            ActiveStatus fearOriginal = original.Status.Find(StatusId.Fear);
            ActiveStatus fearCopy = copy.Status.Find(StatusId.Fear);
            if (fearOriginal != null && fearCopy != null && Mathf.Abs(fearOriginal.Remaining - fearCopy.Remaining) > 0.01f)
                problems.Add("a copy's fear has " + fearCopy.Remaining + "s left, not the original's "
                             + fearOriginal.Remaining + "s - its elite scaling was applied twice");

            if (!copy.IsAlerted) problems.Add("a copy of an alerted enemy has not noticed anything");
            if (!room.Contains(copy) || room.EnemiesRemaining != 2)
                problems.Add("a copy was not registered with the original's room, so the room could clear while it lives");
            if (Vector3.Distance(copy.transform.position, at) > 0.01f) problems.Add("a copy did not start where it was asked to");
        }

        // ---------------------------------------------------------------- helpers

        private static TargetRegistry.Entry Entry(string name, Vector3 position, bool isPlayerBody = false)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var health = go.AddComponent<Health>();
            health.Team = Team.Player;
            health.DestroyOnDeath = false;
            health.ConfigureMaxHealth(100f);

            return new TargetRegistry.Entry { Transform = go.transform, Health = health, IsPlayerBody = isPlayerBody };
        }

        private static StatusController Subject(bool elite)
        {
            var go = new GameObject("ControlSubject");
            go.AddComponent<CharacterSheet>();
            var status = go.AddComponent<StatusController>();

            var health = go.AddComponent<Health>();
            health.Team = Team.Enemy;
            health.IsElite = elite;
            health.DestroyOnDeath = false;
            health.ConfigureMaxHealth(1000f);

            return status;
        }

        /// <summary>Edit mode never calls Awake, so health is configured the way enemies are built.</summary>
        private static EnemyController Spawn(string id, Vector3 position)
        {
            EnemyController enemy = EnemyFactory.Spawn(id, position, 1);
            if (enemy == null) return null;

            enemy.Health.DestroyOnDeath = false;
            enemy.Health.ConfigureMaxHealth(100f);
            return enemy;
        }

        private static void ExpectChoice(List<string> problems, string when, TargetRegistry.Entry actual,
            TargetRegistry.Entry expected)
        {
            if (actual == expected) return;
            problems.Add("choosing a target " + when + " picked " + NameOf(actual) + ", expected " + NameOf(expected));
        }

        private static string NameOf(TargetRegistry.Entry entry)
            => entry == null ? "nothing" : entry.Transform != null ? entry.Transform.name : "a destroyed object";

        private static void ExpectFrozenAt(List<string> problems, string when, EnemyController enemy, Transform live,
            Vector3 point)
        {
            if (enemy.Target == null || enemy.Target == live)
                problems.Add("an enemy " + when + " still tracked the live target");
            else if (Vector3.Distance(enemy.Target.position, point) > 0.01f)
                problems.Add("an enemy " + when + " aimed at " + enemy.Target.position + ", expected where it last perceived them, " + point);
        }

        private static void ExpectControl(List<string> problems, string what, StatusApplication app, bool canMove,
            bool ranged, bool melee)
        {
            StatusController status = Subject(elite: false);
            status.Apply(app, null, Team.Player);

            if (status.CanMove != canMove)
                problems.Add(what + ": moving allowed is " + status.CanMove + ", expected " + canMove);
            if (status.CanAttack(AttackReach.Ranged) != ranged)
                problems.Add(what + ": ranged attacks allowed is " + !ranged + ", expected " + ranged);
            if (status.CanAttack(AttackReach.Melee) != melee)
                problems.Add(what + ": melee attacks allowed is " + !melee + ", expected " + melee);
        }

        private static void ExpectEliteDuration(List<string> problems, StatusController status, StatusApplication app,
            float expected)
        {
            status.Apply(app, null, Team.Player);
            ActiveStatus s = status.Find(app.Id);

            if (s == null) problems.Add(app.Id + " did not apply to an elite");
            else if (Mathf.Abs(s.Duration - expected) > 0.01f)
                problems.Add("an elite's " + app.Id + " lasts " + s.Duration.ToString("0.##") + "s of "
                             + app.Duration + ", expected " + expected);
        }

        private static void ExpectDirection(List<string> problems, string what, Vector3 actual, Vector3 expected)
        {
            if (actual.sqrMagnitude < 0.001f) problems.Add(what + " gave no direction at all");
            else if (Vector3.Dot(actual.normalized, expected) < 0.5f)
                problems.Add(what + " went " + actual + ", expected roughly " + expected);
        }
    }
}
#endif
