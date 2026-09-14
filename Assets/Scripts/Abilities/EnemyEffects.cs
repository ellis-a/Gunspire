using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Effects that do something unusual to enemies themselves: banishing, splitting, tethering, reflecting
    // their shots, and taking them over.

    /// <summary>
    /// Takes what it caught out of reality, sharing a set banishment between them: one enemy for all twelve
    /// seconds, two for six each. Elites resist half their share, which is lost rather than handed on. A
    /// banished enemy is hidden, never deleted, so its room stays uncleared. Banish.
    /// </summary>
    [System.Serializable]
    public class BanishEffect : AbilityEffect
    {
        public float TotalSeconds = 12f;
        public float EliteResistance = 0.5f;

        public override bool Execute(AbilityContext ctx)
        {
            var enemies = new List<EnemyController>();
            for (int i = 0; i < ctx.Targets.Count; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target == null || !target.IsAlive || target.Transform == null) continue;

                EnemyController enemy = target.Transform.GetComponent<EnemyController>();
                if (enemy != null && !enemy.IsHidden && !enemy.IsPossessed && !enemies.Contains(enemy)) enemies.Add(enemy);
            }

            if (enemies.Count == 0) return true;

            float share = TotalSeconds * ctx.LevelScale / enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
            {
                bool elite = enemies[i].Health != null && enemies[i].Health.IsElite;
                BanishTimer.Banish(enemies[i], share * (elite ? 1f - EliteResistance : 1f));
            }
            return true;
        }

        public override string Describe() => string.Format("banishes for {0:0}s, shared", TotalSeconds);
    }

    /// <summary>Brings a banished enemy back where it left. Counts down on world time, so Stop Time extends it.</summary>
    public class BanishTimer : MonoBehaviour
    {
        public float Remaining;

        private EnemyController _enemy;
        private Vector3 _where;

        public static BanishTimer Banish(EnemyController enemy, float seconds)
        {
            if (enemy == null || seconds <= 0f) return null;

            BanishTimer timer = enemy.GetComponent<BanishTimer>();
            if (timer == null) timer = enemy.gameObject.AddComponent<BanishTimer>();

            timer._enemy = enemy;
            timer.Remaining = Mathf.Max(timer.Remaining, seconds);

            if (!enemy.IsHidden)
            {
                timer._where = enemy.transform.position;

                var color = new Color(0.6f, 0.45f, 1f, 0.5f);
                GameObject pop = Build.Sphere(null, "BanishPop", enemy.transform.position + Vector3.up * 0.9f, 1.4f,
                    MaterialLibrary.Transparent(color), collider: false);
                FadeAndDie.Attach(pop, 0.35f, color, Vector3.one * 3f);

                enemy.Hide();
            }
            return timer;
        }

        private void Update() => Step(WorldClock.DeltaTime);

        public void Step(float dt)
        {
            if (_enemy == null)
            {
                Remove();
                return;
            }

            Remaining -= dt;
            if (Remaining > 0f) return;

            _enemy.Reveal(_where);
            Remove();
        }

        private void Remove()
        {
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }
    }

    /// <summary>Marks an enemy that has been split, so neither half can be split again.</summary>
    public class SplitMarker : MonoBehaviour { }

    /// <summary>
    /// Splits the first enemy caught into two copies of itself, each with half the original's maximum health,
    /// both silenced and disarmed for a moment and pushed apart. Underneath, one of them is the original, so
    /// nothing is left pointing at a deleted enemy. The death mark is not copied, the safer default for that
    /// open question. Elites can be split; nothing can be split twice. Superego Death.
    /// </summary>
    [System.Serializable]
    public class SplitEnemyEffect : AbilityEffect
    {
        public float ControlSeconds = 1.5f;
        public float PushSpeed = 7f;

        /// <summary>The copy made by the last split, for tooling.</summary>
        public static EnemyController LastCopy;

        public override bool Execute(AbilityContext ctx)
        {
            EnemyController enemy = null;
            for (int i = 0; i < ctx.Targets.Count && enemy == null; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target == null || !target.IsAlive || target.Transform == null) continue;

                EnemyController candidate = target.Transform.GetComponent<EnemyController>();
                if (candidate != null && !candidate.IsHidden && !candidate.IsPossessed
                    && candidate.GetComponent<SplitMarker>() == null)
                    enemy = candidate;
            }

            if (enemy == null || enemy.Health == null) return true;

            Health health = enemy.Health;
            float half = health.Max * 0.5f;

            // Halved first, so the copy takes the halved maximum and current health, and splitting never heals.
            if (enemy.Sheet != null) enemy.Sheet.SetBaseOverride(Attr.MaxHealth, half);
            health.ConfigureMaxHealth(half, refill: false);
            health.SetCurrent(Mathf.Min(health.Current, half));

            Vector3 side = enemy.transform.right;
            EnemyController copy = EnemyFactory.Copy(enemy, enemy.transform.position + side * 1.2f,
                id => id == StatusId.Deathmark);
            LastCopy = copy;

            Mark(enemy, -side, ctx);
            if (copy != null) Mark(copy, side, ctx);
            return true;
        }

        private void Mark(EnemyController enemy, Vector3 push, AbilityContext ctx)
        {
            enemy.gameObject.AddComponent<SplitMarker>();

            StatusController status = enemy.Status;
            if (status != null)
            {
                status.Apply(StatusLibrary.Silence(ControlSeconds), ctx.Caster, ctx.Team);
                status.Apply(StatusLibrary.Disarm(ControlSeconds), ctx.Caster, ctx.Team);
            }

            // An impulse rather than knockback, so the halves drift apart without striking each other for damage.
            enemy.AddImpulse(push * PushSpeed);
        }

        public override string Describe() => "splits the enemy in two";
    }

    /// <summary>
    /// Tethers the first enemy selected to the nearest other enemy. They cannot move far apart, and kinetic or
    /// energy damage to one deals the same as psychic to the other. Refused with nobody to tether. Prismatic Chains.
    /// </summary>
    [System.Serializable]
    public class TetherEffect : AbilityEffect
    {
        public float PartnerRange = 10f;
        public float Leash = 5f;
        public float Seconds = 8f;

        public override bool Execute(AbilityContext ctx)
        {
            Health first = null;
            for (int i = 0; i < ctx.Targets.Count && first == null; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target != null && target.IsAlive && target.Transform != null) first = target.Transform.GetComponent<Health>();
            }

            if (first == null) return false;

            Collider[] near = Physics.OverlapSphere(first.transform.position, PartnerRange, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);

            Health partner = null;
            float best = float.MaxValue;
            for (int i = 0; i < near.Length; i++)
            {
                Health candidate = near[i].GetComponentInParent<Health>();
                if (candidate == null || candidate == first || !candidate.IsAlive) continue;

                float distance = (candidate.transform.position - first.transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    partner = candidate;
                }
            }

            if (partner == null) return false;

            TetherLink.Create(first, partner, Leash, Seconds * ctx.LevelScale, ctx.Caster, ctx.Team, ctx.Tint);
            return true;
        }

        public override string Describe() => "chains two enemies together";
    }

    /// <summary>The chain between two enemies. Lives on its own object and breaks when either dies.</summary>
    public class TetherLink : MonoBehaviour
    {
        private const float PullStrength = 30f;

        public Health A { get; private set; }
        public Health B { get; private set; }

        private float _leash;
        private float _left;
        private GameObject _source;
        private Team _team;
        private GameObject _line;
        private bool _bound;

        public static TetherLink Create(Health a, Health b, float leash, float seconds, GameObject source, Team team, Color tint)
        {
            var link = new GameObject("PrismaticChain").AddComponent<TetherLink>();
            link.A = a;
            link.B = b;
            link._leash = leash;
            link._left = seconds;
            link._source = source;
            link._team = team;
            link._line = Build.Cube(link.transform, "Chain", Vector3.zero, Vector3.one,
                MaterialLibrary.Emissive(tint, 2.5f), collider: false);

            a.Damaged += link.OnDamagedA;
            b.Damaged += link.OnDamagedB;
            link._bound = true;
            link.DrawLine();
            return link;
        }

        private void OnDamagedA(DamageInfo info, float amount) => Transfer(info, amount, B);
        private void OnDamagedB(DamageInfo info, float amount) => Transfer(info, amount, A);

        /// <summary>
        /// Kinetic and energy pass on as psychic, and psychic never passes on. That is what stops two chained
        /// enemies passing one hit back and forth forever; any change letting psychic through breaks it.
        /// Executes report their own type, so a whole health bar never crosses either.
        /// </summary>
        private void Transfer(DamageInfo info, float amount, Health other)
        {
            if (this == null || amount <= 0f || other == null || !other.IsAlive) return;
            if (info.Type != DamageType.Kinetic && info.Type != DamageType.Energy) return;

            DamageInfo echo = DamageInfo.Create(amount, DamageType.Psychic, _team, _source);
            echo.CanCrit = false;
            echo.Origin = DamageOrigin.Spell;
            other.TakeDamage(echo.At(other.transform.position + Vector3.up, Vector3.up));
        }

        private void Update() => Step(WorldClock.DeltaTime);

        public void Step(float dt)
        {
            _left -= dt;
            if (_left <= 0f || A == null || B == null || !A.IsAlive || !B.IsAlive)
            {
                Break();
                return;
            }

            Vector3 delta = B.transform.position - A.transform.position;
            delta.y = 0f;
            float over = delta.magnitude - _leash;

            if (over > 0f)
            {
                Vector3 toward = delta.normalized * (Mathf.Min(over, 4f) * PullStrength * dt);
                EnemyController ea = A.GetComponent<EnemyController>();
                EnemyController eb = B.GetComponent<EnemyController>();
                if (ea != null) ea.AddImpulse(toward);
                if (eb != null) eb.AddImpulse(-toward);
            }

            DrawLine();
        }

        private void DrawLine()
        {
            if (_line == null || A == null || B == null) return;

            Vector3 from = A.transform.position + Vector3.up;
            Vector3 to = B.transform.position + Vector3.up;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return;

            _line.transform.position = from + delta * 0.5f;
            _line.transform.rotation = Quaternion.LookRotation(delta);
            _line.transform.localScale = new Vector3(0.06f, 0.06f, delta.magnitude);
        }

        public void Break()
        {
            Unbind();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            if (A != null) A.Damaged -= OnDamagedA;
            if (B != null) B.Damaged -= OnDamagedB;
        }

        private void OnDestroy() => Unbind();
    }

    /// <summary>
    /// Sends a soul bouncing between recent deaths, exploding each for area damage. Refused with no corpse
    /// in range. The first is the corpse nearest the aim; each bounce takes the nearest one not yet used.
    /// Every recorded death counts, your own minions' included. Corpse Explosion.
    /// </summary>
    [System.Serializable]
    public class ChainCorpsesEffect : AbilityEffect
    {
        public float FirstRange = 30f;
        public float JumpRange = 12f;
        public int MaxExplosions = 5;
        public float Damage = 40f;
        public float Radius = 3f;
        public float Knockback = 6f;

        public override bool Execute(AbilityContext ctx)
        {
            var records = new List<DeathRecords.Record>();
            DeathRecords.Collect(records);

            var corpses = new List<Vector3>();
            for (int i = 0; i < records.Count; i++)
                if ((records[i].Position - ctx.Origin).sqrMagnitude <= FirstRange * FirstRange) corpses.Add(records[i].Position);

            if (corpses.Count == 0) return false;

            int current = 0;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < corpses.Count; i++)
            {
                float angle = Vector3.Angle(ctx.Forward, corpses[i] - ctx.Origin);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    current = i;
                }
            }

            DamageInfo template = DamageInfo.Create(Damage * ctx.Power, ctx.DamageType, ctx.Team, ctx.Caster);
            template.CanCrit = false;
            template.Origin = ctx.DamageOrigin;
            template = template.WithStatuses(ctx.Payload);

            Vector3 from = ctx.Origin + ctx.Forward * 0.6f + Vector3.down * 0.25f;
            int explosions = Mathf.Max(1, MaxExplosions + ctx.Level - 1);

            for (int n = 0; n < explosions && current >= 0; n++)
            {
                Vector3 at = corpses[current] + Vector3.up * 0.5f;
                corpses.RemoveAt(current);

                Combat.SpawnTracer(from, at, ctx.Tint, 0.1f, 0.2f);
                Combat.Explode(at, Radius, template, ctx.HitMask, 0.4f, Knockback);

                var color = new Color(ctx.Tint.r, ctx.Tint.g, ctx.Tint.b, 0.5f);
                GameObject pop = Build.Sphere(null, "CorpseBurst", at, Radius, MaterialLibrary.Transparent(color), collider: false);
                FadeAndDie.Attach(pop, 0.3f, color, Vector3.one * Radius);

                from = at;
                ctx.Point = at;

                current = -1;
                float nearest = JumpRange * JumpRange;
                for (int i = 0; i < corpses.Count; i++)
                {
                    float distance = (corpses[i] - at).sqrMagnitude;
                    if (distance <= nearest)
                    {
                        nearest = distance;
                        current = i;
                    }
                }
            }
            return true;
        }

        public override string Describe() => "bounces between corpses, exploding each";
    }

    /// <summary>
    /// Turns every nearby enemy projectile onto the caster's side and sends it straight back along its path,
    /// the default for where reflected shots go. A psi charge is spent only when something is actually
    /// reflected, the kinder default. Force of Will.
    /// </summary>
    [System.Serializable]
    public class ReflectProjectilesEffect : AbilityEffect
    {
        public float Radius = 5f;
        public bool SpendsPsi = true;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return true;

            var found = new List<Projectile>();
            Projectile.FindNear(ctx.Caster.transform.position + Vector3.up * 0.9f, Radius, Team.Enemy, found);
            if (found.Count == 0) return true;

            if (SpendsPsi)
            {
                PsiBladesMastery psi = SpellCosts.Psi(ctx);
                if (psi == null || !psi.TrySpend(1f)) return true;
            }

            for (int i = 0; i < found.Count; i++)
                found[i].SwitchSide(ctx.Team, ctx.Caster, ctx.Sheet, -found[i].transform.forward);

            return true;
        }

        public override string Describe() => "reflects nearby shots";
    }

    /// <summary>
    /// Takes over the first enemy selected. Refused when it is the only enemy left in its room, the default
    /// for where that rule counts. Afterwards the enemy goes back to its own side. Assume Identity.
    /// </summary>
    [System.Serializable]
    public class PossessEffect : AbilityEffect
    {
        public float Seconds = 10f;

        public override bool Execute(AbilityContext ctx)
        {
            PossessionController possession = ctx.Caster != null ? ctx.Caster.GetComponent<PossessionController>() : null;
            if (possession == null) return false;

            EnemyController enemy = null;
            for (int i = 0; i < ctx.Targets.Count && enemy == null; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target != null && target.IsAlive && target.Transform != null)
                    enemy = target.Transform.GetComponent<EnemyController>();
            }

            if (enemy == null || enemy.IsHidden) return false;

            RoomRuntime room = RoomRuntime.Holding(enemy);
            if (room != null && room.EnemiesRemaining <= 1) return false;

            return possession.Begin(enemy, Seconds);
        }

        public override string Describe() => string.Format("controls an enemy for {0:0}s", Seconds);
    }
}
