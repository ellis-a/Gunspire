using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>Every spell in the game. Slot-bindable via <see cref="SpellBook"/>.</summary>
    public static class SpellLibrary
    {
        private static List<Spell> _all;

        public static IReadOnlyList<Spell> All
        {
            get
            {
                if (_all == null)
                {
                    _all = new List<Spell>
                    {
                        new BlinkSpell(),
                        new ConeOfColdSpell(),
                        new FireboltSpell(),
                        new KineticSlamSpell(),
                        new ArcaneWardSpell(),
                        new ChainLightningSpell(),
                        new GlacialPrisonSpell(),
                        new EventideSpell()
                    };
                }
                return _all;
            }
        }

        public static Spell Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>
        /// Spells a shrine can still offer: never learned, or learned but below their level
        /// cap. Taking one you already know levels it instead of rebinding it.
        /// </summary>
        public static List<Spell> Offerable(SpellBook book)
        {
            var list = new List<Spell>();
            for (int i = 0; i < All.Count; i++)
                if (book == null || book.CanTake(All[i])) list.Add(All[i]);
            return list;
        }

        /// <summary>Rolls a rarity from Luck, then picks an offerable spell at that tier.</summary>
        public static Spell RollOffer(Rng rng, SpellBook book, float luck, float rarityBonus = 1f)
        {
            List<Spell> pool = Offerable(book);
            if (pool.Count == 0) return null;

            Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
            return Rarities.PickOfRarity(rng, pool, s => s.Rarity, rolled);
        }
    }

    // ================================================================================ Blink

    /// <summary>Short forward teleport. Stops at the first wall and grants a sliver of invulnerability.</summary>
    public class BlinkSpell : Spell
    {
        public override string Id => "blink";
        public override string DisplayName => "Blink";
        public override string ShortName => "BLNK";
        public override string Description =>
            "Teleport a short distance forward, passing through anything in the way. Brief invulnerability on arrival.";
        public override float ManaCost => 16f;
        public override float Cooldown => 4.0f;
        public override Rarity Rarity => Rarity.Common;
        public override SpellType Type => SpellType.Mobility;
        public override DamageType DamageType => DamageType.Astral;

        private const float BaseDistance = 9f;
        private const float MaxDistance = 22f;

        public override string LevelUpSummary(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return "Already at maximum level.";
            return string.Format("Level {0} to {1}: range {2:0.0}m to {3:0.0}m, cooldown {4:0.0}s to {5:0.0}s",
                currentLevel, currentLevel + 1, DistanceAt(currentLevel, 5), DistanceAt(currentLevel + 1, 5),
                CooldownAtLevel(currentLevel), CooldownAtLevel(currentLevel + 1));
        }

        private float DistanceAt(int level, int intellect)
            => Mathf.Min(MaxDistance, (BaseDistance + intellect * 0.18f) * LevelMultiplier(level));

        public override bool Cast(SpellContext ctx)
        {
            if (ctx.Motor == null || ctx.Controller == null) return false;

            Vector3 direction = ctx.Forward;
            direction.y = Mathf.Clamp(direction.y, -0.25f, 0.45f);
            if (direction.sqrMagnitude < 0.001f) direction = ctx.Caster.transform.forward;
            direction.Normalize();

            int intellect = ctx.Sheet != null ? ctx.Sheet.GetStat(StatType.Intellect) : 5;
            float distance = DistanceAt(ctx.SpellLevel, intellect);

            Vector3 start = ctx.Caster.transform.position;
            distance = ClampToGeometry(ctx.Controller, start, direction, distance);
            if (distance < 0.4f) return false;   // nothing but wall in front, refund the cast

            Vector3 destination = start + direction * distance;

            SpawnTrail(start, destination);
            ctx.Motor.Teleport(destination);

            if (ctx.Health != null)
                ctx.Health.InvulnerabilityTimer = Mathf.Max(ctx.Health.InvulnerabilityTimer, 0.18f);

            SpellEvents.RaiseCast(this, ctx, destination);
            return true;
        }

        /// <summary>Sweeps the character capsule so the player never lands inside a wall.</summary>
        private static float ClampToGeometry(CharacterController cc, Vector3 origin, Vector3 direction, float distance)
        {
            float radius = cc.radius * 0.92f;
            Vector3 center = origin + cc.center;
            float half = Mathf.Max(0.01f, cc.height * 0.5f - cc.radius);
            Vector3 bottom = center - Vector3.up * half;
            Vector3 top = center + Vector3.up * half;

            if (Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit, distance,
                    Layers.BlockingMask, QueryTriggerInteraction.Ignore))
                return Mathf.Max(0f, hit.distance - 0.12f);

            return distance;
        }

        private void SpawnTrail(Vector3 from, Vector3 to)
        {
            const int ghosts = 6;
            for (int i = 0; i <= ghosts; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)ghosts);
                Color c = Tint;
                c.a = 0.5f * (1f - i / (float)ghosts) + 0.15f;

                GameObject ghost = Build.Cube(null, "BlinkGhost", p + Vector3.up * 0.9f,
                    new Vector3(0.5f, 1.7f, 0.5f), MaterialLibrary.Transparent(c), collider: false);
                FadeAndDie.Attach(ghost, 0.28f, c);
            }
        }
    }

    // ================================================================================ Cone of Cold

    /// <summary>A wide fan of freezing air. Stacks Chill, and enough stacks freeze the target solid.</summary>
    public class ConeOfColdSpell : Spell
    {
        public override string Id => "cone_of_cold";
        public override string DisplayName => "Cone of Cold";
        public override string ShortName => "COLD";
        public override string Description =>
            "A freezing cone that damages and heavily chills everything in front of you. Chilled enemies that saturate freeze solid.";
        public override float ManaCost => 26f;
        public override float Cooldown => 6.5f;
        public override Rarity Rarity => Rarity.Common;
        public override SpellType Type => SpellType.Control;
        public override DamageType DamageType => DamageType.Frost;

        private const float Range = 13f;
        private const float HalfAngle = 34f;
        private const float BaseDamage = 17f;

        private static int ChillStacksAt(int level) => 2 + Mathf.FloorToInt((level - 1) * 0.5f);

        public override string LevelUpSummary(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return "Already at maximum level.";
            return string.Format("Level {0} to {1}: damage +{2:0}%, chill {3} to {4} stacks",
                currentLevel, currentLevel + 1, GrowthPerLevel * 100f,
                ChillStacksAt(currentLevel), ChillStacksAt(currentLevel + 1));
        }

        public override bool Cast(SpellContext ctx)
        {
            Vector3 origin = ctx.Origin;
            Vector3 forward = ctx.Forward;

            List<IDamageable> targets = Combat.ConeTargets(origin, forward, Range, HalfAngle,
                Layers.TargetMaskFor(ctx.Team));

            float damage = BaseDamage * ctx.ScaledPower;
            List<StatusApplication> statuses = ctx.Combine(
                StatusLibrary.Chill(4.5f, ChillStacksAt(ctx.SpellLevel)));

            for (int i = 0; i < targets.Count; i++)
            {
                IDamageable target = targets[i];
                if (!HasLineOfSight(origin, target)) continue;

                DamageInfo info = DamageInfo.Create(damage, DamageType, ctx.Team, ctx.Caster);
                info.CanCrit = false;
                info = info.At(target.Transform.position + Vector3.up, -forward).WithStatuses(statuses);
                target.TakeDamage(info);
            }

            SpawnVisual(origin, forward);
            SpellEvents.RaiseCast(this, ctx, origin + forward * (Range * 0.5f));
            return true;
        }

        private static bool HasLineOfSight(Vector3 origin, IDamageable target)
        {
            Vector3 point = target.Transform.position + Vector3.up * 0.9f;
            Vector3 delta = point - origin;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(origin, delta / distance, distance - 0.3f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }

        private void SpawnVisual(Vector3 origin, Vector3 forward)
        {
            Color c = Tint;
            c.a = 0.35f;

            GameObject cone = MeshFactory.SpawnCone(origin, forward, Range, HalfAngle,
                MaterialLibrary.Transparent(c));
            FadeAndDie.Attach(cone, 0.4f, c);

            for (int i = 0; i < 10; i++)
            {
                Vector3 dir = Quaternion.Euler(
                    Random.Range(-HalfAngle, HalfAngle),
                    Random.Range(-HalfAngle, HalfAngle), 0f) * forward;

                GameObject shard = Build.Cube(null, "Shard", origin + dir * Random.Range(1f, Range),
                    Vector3.one * Random.Range(0.12f, 0.3f), MaterialLibrary.Emissive(Tint, 2f), collider: false);
                shard.transform.rotation = Random.rotation;
                Build.Ephemeral(shard, 0.35f);
            }
        }
    }

    // ================================================================================ Firebolt

    public class FireboltSpell : Spell
    {
        public override string Id => "firebolt";
        public override string DisplayName => "Firebolt";
        public override string ShortName => "FIRE";
        public override string Description => "Hurl a bolt of fire that detonates on impact and sets the target alight.";
        public override float ManaCost => 22f;
        public override float Cooldown => 3.5f;
        public override Rarity Rarity => Rarity.Uncommon;
        public override SpellType Type => SpellType.Attack;
        public override DamageType DamageType => DamageType.Fire;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 origin = ctx.Origin + ctx.Forward * 0.8f;
            float power = ctx.ScaledPower;

            Projectile p = Projectile.Create(origin, ctx.Forward, Tint, 0.28f);
            p.OwnerTeam = ctx.Team;
            p.Owner = ctx.Caster;
            p.OwnerSheet = ctx.Sheet;
            p.IsSpell = true;
            p.CanCrit = false;
            p.Damage = 30f * power;
            p.DamageType = DamageType;
            p.Speed = 42f;
            p.Lifetime = 5f;
            p.SplashRadius = 3.2f * LevelMultiplier(ctx.SpellLevel);
            p.SplashDamage = 30f * power;
            p.Knockback = 2f;
            p.Statuses = ctx.Combine(StatusLibrary.Burn(5f, 2 + ctx.SpellLevel, 5f));
            p.Launch();

            SpellEvents.RaiseCast(this, ctx, origin);
            return true;
        }
    }

    // ================================================================================ Chain Lightning

    public class ChainLightningSpell : Spell
    {
        public override string Id => "chain_lightning";
        public override string DisplayName => "Chain Lightning";
        public override string ShortName => "ARC";
        public override string Description => "An arc that leaps between nearby enemies, shocking each one.";
        public override float ManaCost => 30f;
        public override float Cooldown => 7f;
        public override Rarity Rarity => Rarity.Rare;
        public override SpellType Type => SpellType.Attack;
        public override DamageType DamageType => DamageType.Astral;

        private const float JumpRange = 10f;
        private const float FirstRange = 45f;

        private static int JumpsAt(int level) => 3 + level;

        public override string LevelUpSummary(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return "Already at maximum level.";
            return string.Format("Level {0} to {1}: damage +{2:0}%, {3} to {4} targets",
                currentLevel, currentLevel + 1, GrowthPerLevel * 100f,
                JumpsAt(currentLevel), JumpsAt(currentLevel + 1));
        }

        public override bool Cast(SpellContext ctx)
        {
            Vector3 origin = ctx.Origin;
            if (!Physics.Raycast(origin, ctx.Forward, out RaycastHit hit, FirstRange,
                    Layers.HitMaskFor(ctx.Team), QueryTriggerInteraction.Ignore))
                return false;

            IDamageable first = Combat.FindDamageable(hit.collider);
            if (first == null || !first.IsAlive || first.Team == ctx.Team) return false;

            float damage = 26f * ctx.ScaledPower;
            int jumps = JumpsAt(ctx.SpellLevel);

            var hitAlready = new HashSet<IDamageable>();
            IDamageable current = first;
            Vector3 from = origin;

            for (int jump = 0; jump < jumps && current != null; jump++)
            {
                hitAlready.Add(current);

                DamageInfo info = DamageInfo.Create(damage, DamageType, ctx.Team, ctx.Caster);
                info.CanCrit = false;
                info = info.At(current.Transform.position + Vector3.up, Vector3.up)
                           .WithStatuses(ctx.Combine(StatusLibrary.Shock(4f)));
                current.TakeDamage(info);

                Vector3 to = current.Transform.position + Vector3.up;
                Combat.SpawnTracer(from, to, Tint, 0.08f, 0.16f);
                from = to;

                damage *= 0.8f;
                current = FindNextLink(from, ctx.Team, hitAlready);
            }

            SpellEvents.RaiseCast(this, ctx, from);
            return true;
        }

        private static IDamageable FindNextLink(Vector3 from, Team castingTeam, HashSet<IDamageable> visited)
        {
            Collider[] found = Physics.OverlapSphere(from, JumpRange, Layers.TargetMaskFor(castingTeam),
                QueryTriggerInteraction.Ignore);

            IDamageable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < found.Length; i++)
            {
                IDamageable d = Combat.FindDamageable(found[i]);
                if (d == null || !d.IsAlive || visited.Contains(d)) continue;

                float distance = Vector3.SqrMagnitude(d.Transform.position - from);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = d;
                }
            }
            return best;
        }
    }

    // ================================================================================ Arcane Ward

    public class ArcaneWardSpell : Spell
    {
        public override string Id => "arcane_ward";
        public override string DisplayName => "Arcane Ward";
        public override string ShortName => "WARD";
        public override string Description => "Sheathe yourself in force: take much less damage for a few seconds and mend a wound.";
        public override float ManaCost => 28f;
        public override float Cooldown => 14f;
        public override Rarity Rarity => Rarity.Uncommon;
        public override SpellType Type => SpellType.Ward;
        public override DamageType DamageType => DamageType.Astral;

        public override bool Cast(SpellContext ctx)
        {
            var status = ctx.Caster.GetComponent<StatusController>();
            if (status != null)
                status.Apply(StatusLibrary.Fortify(4f + ctx.SpellLevel, 2, 0.18f), ctx.Caster, ctx.Team);

            if (ctx.Health != null)
                ctx.Health.Heal(ctx.Health.Max * 0.12f * ctx.ScaledPower);

            GameObject bubble = Build.Sphere(ctx.Caster.transform, "Ward", Vector3.up * 0.9f, 2.4f,
                MaterialLibrary.Transparent(new Color(Tint.r, Tint.g, Tint.b, 0.22f)), collider: false);
            FadeAndDie.Attach(bubble, 0.6f, new Color(Tint.r, Tint.g, Tint.b, 0.22f));

            SpellEvents.RaiseCast(this, ctx, ctx.Caster.transform.position);
            return true;
        }
    }

    // ================================================================================ Kinetic Slam

    public class KineticSlamSpell : Spell
    {
        public override string Id => "kinetic_slam";
        public override string DisplayName => "Kinetic Slam";
        public override string ShortName => "SLAM";
        public override string Description => "A shockwave around you that hurls enemies back and shatters weak barriers.";
        public override float ManaCost => 24f;
        public override float Cooldown => 8f;
        public override Rarity Rarity => Rarity.Uncommon;
        public override SpellType Type => SpellType.Control;
        public override DamageType DamageType => DamageType.Normal;

        private const float BaseRadius = 6.5f;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 center = ctx.Caster.transform.position + Vector3.up * 0.9f;
            float radius = BaseRadius * LevelMultiplier(ctx.SpellLevel);

            DamageInfo template = DamageInfo.Create(22f * ctx.ScaledPower, DamageType, ctx.Team, ctx.Caster);
            template.CanCrit = false;
            template.SmashPower = ctx.Sheet != null ? ctx.Sheet.Get(Attr.SmashPower) : 5f;
            template = template.WithStatuses(ctx.Combine(StatusLibrary.Weaken(4f)));

            Combat.Explode(center, radius, template, Layers.HitMaskFor(ctx.Team), 0.5f, 9f);

            GameObject ring = Build.GroundDisc(null, "SlamRing",
                ctx.Caster.transform.position + Vector3.up * 0.06f, radius,
                MaterialLibrary.Transparent(new Color(Tint.r, Tint.g, Tint.b, 0.35f)));
            FadeAndDie.Attach(ring, 0.3f, new Color(Tint.r, Tint.g, Tint.b, 0.35f));

            SpellEvents.RaiseCast(this, ctx, center);
            return true;
        }
    }

    // ================================================================================ Glacial Prison

    /// <summary>Mythic control: freezes everything around you outright.</summary>
    public class GlacialPrisonSpell : Spell
    {
        public override string Id => "glacial_prison";
        public override string DisplayName => "Glacial Prison";
        public override string ShortName => "PRSN";
        public override string Description =>
            "Every enemy around you is frozen solid and takes heavy frost damage. Frozen targets shatter for bonus damage when struck.";
        public override float ManaCost => 42f;
        public override float Cooldown => 18f;
        public override Rarity Rarity => Rarity.Mythic;
        public override SpellType Type => SpellType.Control;
        public override DamageType DamageType => DamageType.Frost;
        public override int MaxLevel => 4;

        private const float BaseRadius = 14f;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 center = ctx.Caster.transform.position + Vector3.up * 0.9f;
            float radius = BaseRadius * LevelMultiplier(ctx.SpellLevel);
            float freezeSeconds = 2.0f + ctx.SpellLevel * 0.35f;

            Collider[] found = Physics.OverlapSphere(center, radius, Layers.TargetMaskFor(ctx.Team),
                QueryTriggerInteraction.Ignore);

            var seen = new HashSet<IDamageable>();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target == null || !target.IsAlive || !seen.Add(target)) continue;

                DamageInfo info = DamageInfo.Create(40f * ctx.ScaledPower, DamageType, ctx.Team, ctx.Caster);
                info.CanCrit = false;
                info = info.At(target.Transform.position + Vector3.up, Vector3.up)
                           .WithStatuses(ctx.Combine(
                               new StatusApplication(StatusId.Freeze, freezeSeconds, 1, 1f)));
                target.TakeDamage(info);

                GameObject ice = Build.Cube(null, "IceBlock",
                    target.Transform.position + Vector3.up * 0.9f, new Vector3(1.4f, 2.2f, 1.4f),
                    MaterialLibrary.Transparent(new Color(Tint.r, Tint.g, Tint.b, 0.4f)), collider: false);
                FadeAndDie.Attach(ice, freezeSeconds, new Color(Tint.r, Tint.g, Tint.b, 0.4f));
            }

            GameObject burst = Build.GroundDisc(null, "PrisonRing",
                ctx.Caster.transform.position + Vector3.up * 0.06f, radius,
                MaterialLibrary.Transparent(new Color(Tint.r, Tint.g, Tint.b, 0.3f)));
            FadeAndDie.Attach(burst, 0.5f, new Color(Tint.r, Tint.g, Tint.b, 0.3f));

            SpellEvents.RaiseCast(this, ctx, center);
            return true;
        }
    }

    // ================================================================================ Eventide

    /// <summary>Legendary: a delayed collapse of shadow at the point you are looking at.</summary>
    public class EventideSpell : Spell
    {
        public override string Id => "eventide";
        public override string DisplayName => "Eventide";
        public override string ShortName => "EVEN";
        public override string Description =>
            "Mark a point in the world. A moment later the light there collapses, tearing apart everything inside and withering the survivors.";
        public override float ManaCost => 55f;
        public override float Cooldown => 22f;
        public override Rarity Rarity => Rarity.Legendary;
        public override SpellType Type => SpellType.Attack;
        public override DamageType DamageType => DamageType.Shadow;
        public override int MaxLevel => 3;
        public override float GrowthPerLevel => 0.35f;

        private const float BaseRadius = 9f;
        private const float Delay = 0.9f;
        private const float CastRange = 60f;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 point = ctx.Origin + ctx.Forward * CastRange;
            if (Physics.Raycast(ctx.Origin, ctx.Forward, out RaycastHit hit, CastRange,
                    Layers.HitMaskFor(ctx.Team), QueryTriggerInteraction.Ignore))
                point = hit.point;

            float radius = BaseRadius * LevelMultiplier(ctx.SpellLevel);
            float damage = 95f * ctx.ScaledPower;

            Telegraph.Circle(point, radius, Delay, Tint);

            // The caster may die before it lands, so the detonation is driven by its own
            // object rather than by a coroutine on the player.
            DelayedDetonation.Spawn(point, radius, Delay, damage, DamageType, ctx.Team, ctx.Caster,
                ctx.Combine(StatusLibrary.Weaken(6f, 2), StatusLibrary.Blight(6f, 3, 4f)), Tint);

            SpellEvents.RaiseCast(this, ctx, point);
            return true;
        }
    }

    /// <summary>A standalone explosion on a timer, independent of whoever created it.</summary>
    public class DelayedDetonation : MonoBehaviour
    {
        private float _timer;
        private float _radius;
        private float _damage;
        private DamageType _damageType;
        private Team _team;
        private GameObject _source;
        private List<StatusApplication> _statuses;
        private Color _tint;

        public static DelayedDetonation Spawn(Vector3 point, float radius, float delay, float damage,
            DamageType damageType, Team team, GameObject source, List<StatusApplication> statuses, Color tint)
        {
            var go = new GameObject("DelayedDetonation");
            go.transform.position = point;

            var d = go.AddComponent<DelayedDetonation>();
            d._timer = delay;
            d._radius = radius;
            d._damage = damage;
            d._damageType = damageType;
            d._team = team;
            d._source = source;
            d._statuses = statuses;
            d._tint = tint;
            return d;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            DamageInfo template = DamageInfo.Create(_damage, _damageType, _team, _source);
            template.CanCrit = false;
            template = template.WithStatuses(_statuses);

            Combat.Explode(transform.position, _radius, template, Layers.HitMaskFor(_team), 0.5f, 10f);

            var color = new Color(_tint.r, _tint.g, _tint.b, 0.55f);
            GameObject pop = Build.Sphere(null, "EventidePop", transform.position + Vector3.up * 0.9f,
                _radius * 0.5f, MaterialLibrary.Transparent(color), collider: false);
            FadeAndDie.Attach(pop, 0.4f, color, Vector3.one * (_radius * 2f));

            Destroy(gameObject);
        }
    }
}
