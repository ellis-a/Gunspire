using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Purely cosmetic effects. They never fail and never touch targets, so they can sit
    /// anywhere in a chain - though a trail wants to run before the teleport it illustrates.
    /// </summary>
    public abstract class VfxEffect : AbilityEffect
    {
        public override string Describe() => null;   // cosmetics do not belong in ability text
    }

    [System.Serializable]
    public class VfxConeEffect : VfxEffect
    {
        public float Range = 13f;
        public float HalfAngle = 34f;
        public float Alpha = 0.35f;
        public float Lifetime = 0.4f;
        public bool ScaleWithLevel;

        public override bool Execute(AbilityContext ctx)
        {
            var color = new Color(ctx.Tint.r, ctx.Tint.g, ctx.Tint.b, Alpha);
            float range = Range * (ScaleWithLevel ? ctx.LevelScale : 1f);

            GameObject cone = MeshFactory.SpawnCone(ctx.Origin, ctx.Forward, range, HalfAngle,
                MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(cone, Lifetime, color);
            return true;
        }
    }

    [System.Serializable]
    public class VfxGroundRingEffect : VfxEffect
    {
        public float Radius = 6.5f;
        public float Alpha = 0.35f;
        public float Lifetime = 0.3f;
        public bool ScaleWithLevel = true;

        public override bool Execute(AbilityContext ctx)
        {
            var color = new Color(ctx.Tint.r, ctx.Tint.g, ctx.Tint.b, Alpha);
            Vector3 at = new Vector3(ctx.Point.x, ctx.Caster.transform.position.y + 0.06f, ctx.Point.z);

            GameObject ring = Build.GroundDisc(null, "AbilityRing", at,
                Radius * (ScaleWithLevel ? ctx.LevelScale : 1f), MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(ring, Lifetime, color);
            return true;
        }
    }

    [System.Serializable]
    public class VfxSphereEffect : VfxEffect
    {
        public float Diameter = 2.4f;
        public float Alpha = 0.22f;
        public float Lifetime = 0.6f;
        public bool AttachToCaster;
        public float GrowPerSecond;

        public override bool Execute(AbilityContext ctx)
        {
            var color = new Color(ctx.Tint.r, ctx.Tint.g, ctx.Tint.b, Alpha);

            GameObject sphere = AttachToCaster
                ? Build.Sphere(ctx.Caster.transform, "AbilityVfx", Vector3.up * 0.9f, Diameter,
                    MaterialLibrary.Transparent(color), collider: false)
                : Build.Sphere(null, "AbilityVfx", ctx.Point, Diameter,
                    MaterialLibrary.Transparent(color), collider: false);

            FadeAndDie.Attach(sphere, Lifetime, color, Vector3.one * GrowPerSecond);
            return true;
        }
    }

    /// <summary>Afterimages between the caster and the selected point. Run it before the teleport.</summary>
    [System.Serializable]
    public class VfxGhostTrailEffect : VfxEffect
    {
        public int Ghosts = 6;
        public float Lifetime = 0.28f;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 from = ctx.Caster.transform.position;
            Vector3 to = ctx.Point;

            for (int i = 0; i <= Ghosts; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)Ghosts);
                Color c = ctx.Tint;
                c.a = 0.5f * (1f - i / (float)Ghosts) + 0.15f;

                GameObject ghost = Build.Cube(null, "Ghost", p + Vector3.up * 0.9f,
                    new Vector3(0.5f, 1.7f, 0.5f), MaterialLibrary.Transparent(c), collider: false);
                FadeAndDie.Attach(ghost, Lifetime, c);
            }
            return true;
        }
    }

    /// <summary>Scatter of shards along the aim direction, for breath and blast effects.</summary>
    [System.Serializable]
    public class VfxShardsEffect : VfxEffect
    {
        public int Count = 10;
        public float Range = 13f;
        public float SpreadDegrees = 34f;
        public float Lifetime = 0.35f;

        public override bool Execute(AbilityContext ctx)
        {
            for (int i = 0; i < Count; i++)
            {
                Vector3 dir = Quaternion.Euler(
                    Random.Range(-SpreadDegrees, SpreadDegrees),
                    Random.Range(-SpreadDegrees, SpreadDegrees), 0f) * ctx.Forward;

                GameObject shard = Build.Cube(null, "Shard",
                    ctx.Origin + dir * Random.Range(1f, Range),
                    Vector3.one * Random.Range(0.12f, 0.3f),
                    MaterialLibrary.Emissive(ctx.Tint, 2f), collider: false);
                shard.transform.rotation = Random.rotation;
                Build.Ephemeral(shard, Lifetime);
            }
            return true;
        }
    }

    /// <summary>A shape on every selected target, such as the ice around a frozen enemy.</summary>
    [System.Serializable]
    public class VfxOnTargetsEffect : VfxEffect
    {
        public Vector3 Size = new Vector3(1.4f, 2.2f, 1.4f);
        public float Alpha = 0.4f;
        public float Lifetime = 2f;
        public float LifetimePerLevel;

        public override bool Execute(AbilityContext ctx)
        {
            var color = new Color(ctx.Tint.r, ctx.Tint.g, ctx.Tint.b, Alpha);
            float life = Lifetime + (ctx.Level - 1) * LifetimePerLevel;

            for (int i = 0; i < ctx.Targets.Count; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target == null || target.Transform == null) continue;

                GameObject shape = Build.Cube(null, "TargetVfx",
                    target.Transform.position + Vector3.up * 0.9f, Size,
                    MaterialLibrary.Transparent(color), collider: false);
                FadeAndDie.Attach(shape, life, color);
            }
            return true;
        }
    }
}
