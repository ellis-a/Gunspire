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
                        new ChainLightningSpell(),
                        new ArcaneWardSpell(),
                        new KineticSlamSpell()
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

        /// <summary>Spells the player has not learned yet, for shrine and boon offers.</summary>
        public static List<Spell> Unknown(SpellBook book)
        {
            var list = new List<Spell>();
            for (int i = 0; i < All.Count; i++)
                if (book == null || !book.Knows(All[i])) list.Add(All[i]);
            return list;
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
        public override Color Tint => Palette.Arcane;

        private const float BaseDistance = 9f;
        private const float MaxDistance = 17f;

        public override bool Cast(SpellContext ctx)
        {
            if (ctx.Motor == null || ctx.Controller == null) return false;

            Vector3 direction = ctx.Forward;
            direction.y = Mathf.Clamp(direction.y, -0.25f, 0.45f);
            if (direction.sqrMagnitude < 0.001f) direction = ctx.Caster.transform.forward;
            direction.Normalize();

            int intellect = ctx.Sheet != null ? ctx.Sheet.GetStat(StatType.Intellect) : 5;
            float distance = Mathf.Min(MaxDistance, BaseDistance + intellect * 0.18f);

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
        public override Color Tint => Palette.Ice;

        private const float Range = 13f;
        private const float HalfAngle = 34f;
        private const float BaseDamage = 17f;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 origin = ctx.Origin;
            Vector3 forward = ctx.Forward;

            List<IDamageable> targets = Combat.ConeTargets(origin, forward, Range, HalfAngle,
                Layers.TargetMaskFor(ctx.Team));

            float damage = BaseDamage * ctx.SpellPower;
            List<StatusApplication> statuses = ctx.Combine(StatusLibrary.Chill(4.5f, 2));

            for (int i = 0; i < targets.Count; i++)
            {
                IDamageable target = targets[i];
                if (!HasLineOfSight(origin, target)) continue;

                DamageInfo info = DamageInfo.Create(damage, DamageType.Ice, ctx.Team, ctx.Caster);
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

            // A few shards riding the blast for readability.
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
        public override Color Tint => Palette.Fire;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 origin = ctx.Origin + ctx.Forward * 0.8f;

            Projectile p = Projectile.Create(origin, ctx.Forward, Tint, 0.28f);
            p.OwnerTeam = ctx.Team;
            p.Owner = ctx.Caster;
            p.OwnerSheet = ctx.Sheet;
            p.IsSpell = true;
            p.CanCrit = false;
            p.Damage = 30f * ctx.SpellPower;
            p.DamageType = DamageType.Fire;
            p.Speed = 42f;
            p.Lifetime = 5f;
            p.SplashRadius = 3.2f;
            p.SplashDamage = 30f * ctx.SpellPower;
            p.Knockback = 2f;
            p.Statuses = ctx.Combine(StatusLibrary.Burn(5f, 3, 5f));
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
        public override string Description => "An arc that leaps between up to four enemies, shocking each one.";
        public override float ManaCost => 30f;
        public override float Cooldown => 7f;
        public override Color Tint => Palette.Lightning;

        private const int MaxJumps = 4;
        private const float JumpRange = 10f;
        private const float FirstRange = 45f;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 origin = ctx.Origin;
            if (!Physics.Raycast(origin, ctx.Forward, out RaycastHit hit, FirstRange,
                    Layers.HitMaskFor(ctx.Team), QueryTriggerInteraction.Ignore))
                return false;

            IDamageable first = Combat.FindDamageable(hit.collider);
            if (first == null || !first.IsAlive || first.Team == ctx.Team) return false;

            float damage = 26f * ctx.SpellPower;
            var hitAlready = new HashSet<IDamageable>();
            IDamageable current = first;
            Vector3 from = origin;

            for (int jump = 0; jump < MaxJumps && current != null; jump++)
            {
                hitAlready.Add(current);

                DamageInfo info = DamageInfo.Create(damage, DamageType.Lightning, ctx.Team, ctx.Caster);
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
        public override Color Tint => new Color(0.75f, 0.8f, 1f);

        public override bool Cast(SpellContext ctx)
        {
            var status = ctx.Caster.GetComponent<StatusController>();
            if (status != null)
                status.Apply(StatusLibrary.Fortify(5f, 2, 0.18f), ctx.Caster, ctx.Team);

            if (ctx.Health != null)
                ctx.Health.Heal(ctx.Health.Max * 0.12f * ctx.SpellPower);

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
        public override Color Tint => new Color(0.9f, 0.75f, 0.45f);

        private const float Radius = 6.5f;

        public override bool Cast(SpellContext ctx)
        {
            Vector3 center = ctx.Caster.transform.position + Vector3.up * 0.9f;

            DamageInfo template = DamageInfo.Create(22f * ctx.SpellPower, DamageType.Physical, ctx.Team, ctx.Caster);
            template.CanCrit = false;
            template.SmashPower = ctx.Sheet != null ? ctx.Sheet.Get(Attr.SmashPower) : 5f;
            template = template.WithStatuses(ctx.Combine(StatusLibrary.Weaken(4f)));

            Combat.Explode(center, Radius, template, Layers.HitMaskFor(ctx.Team), 0.5f, 9f);

            GameObject ring = Build.GroundDisc(null, "SlamRing",
                ctx.Caster.transform.position + Vector3.up * 0.06f, Radius,
                MaterialLibrary.Transparent(new Color(Tint.r, Tint.g, Tint.b, 0.35f)));
            FadeAndDie.Attach(ring, 0.3f, new Color(Tint.r, Tint.g, Tint.b, 0.35f));

            SpellEvents.RaiseCast(this, ctx, center);
            return true;
        }
    }
}
