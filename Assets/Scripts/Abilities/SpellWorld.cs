using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Summons and the things spells leave running in the world: minions, the mimic, the kraken, soul storms,
    // defiled ground, whirlpools, stars, reality shards, and the route to the exit. Each owns its own object
    // and lifetime, so it outlives the cast that made it.

    /// <summary>
    /// Summons minions near the caster or at the point. One limited to a single instance refuses while one is
    /// out, which keeps its cost; that was the open question for the Stitched Monstrosity. Raise Dead, Eye of
    /// E'pheraxx.
    /// </summary>
    [System.Serializable]
    public class SummonMinionEffect : AbilityEffect
    {
        public string MinionId = "zombie";
        public int Count = 1;
        public bool OneAtATime;
        public bool AtPoint;
        public float Distance = 2.5f;

        /// <summary>At most this many of the minion out at once per level of the spell; zero for no limit. Raise Dead.</summary>
        public int CapPerLevel;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return false;
            if (OneAtATime && MinionController.CountOf(MinionId) > 0) return false;
            if (CapPerLevel > 0 && !UnderCap(ctx)) return false;

            Vector3 flat = new Vector3(ctx.Forward.x, 0f, ctx.Forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = ctx.Caster.transform.forward;

            Vector3 around = AtPoint ? ctx.Point : ctx.Caster.transform.position + flat.normalized * Distance;
            NavField field = NavField.Current;

            for (int i = 0; i < Mathf.Max(1, Count); i++)
            {
                Vector3 spot = around;
                if (i > 0 && field != null && field.IsBuilt)
                    field.TryFindSpot(new Rng(Random.Range(0, int.MaxValue)), around, 2f, out spot);

                if (MinionSummoner.Spawn(MinionId, spot) == null) return false;
            }

            ctx.Point = around;
            return true;
        }

        /// <summary>
        /// Refuses a summon that would go over the cap, which keeps its cost, and says why when the player cast it: a
        /// spell that does nothing and says nothing reads as broken.
        /// </summary>
        private bool UnderCap(AbilityContext ctx)
        {
            int cap = CapPerLevel * Mathf.Max(1, ctx.Level);
            if (MinionController.CountOf(MinionId) + Mathf.Max(1, Count) <= cap) return true;

            if (GameDirector.Instance != null && ctx.Caster.GetComponent<PlayerRig>() != null)
            {
                MinionDefinition def = MinionLibrary.Get(MinionId);
                GameDirector.Instance.Notify("Already at " + cap + " " + (def != null ? def.DisplayName : MinionId)
                                             + "s - level the spell for more", 2f);
            }
            return false;
        }

        public override string Describe() => CapPerLevel > 0
            ? "summons " + MinionId + ", up to " + CapPerLevel + " per level"
            : "summons " + MinionId;
    }

    /// <summary>
    /// Summons the Phantasmal Mimic: an immobile illusion firing the gun you hold, with none of your resources
    /// or buffs. Its hits add psi charge outside your quarter-second limit.
    /// </summary>
    [System.Serializable]
    public class SummonMimicEffect : AbilityEffect
    {
        public float Distance = 2f;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            if (rig == null || rig.Holster == null || rig.Weapon == null || rig.Weapon.Definition == null) return false;

            Vector3 flat = new Vector3(ctx.Forward.x, 0f, ctx.Forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = rig.transform.forward;

            MinionController body = MinionSummoner.Spawn("mimic", rig.transform.position + Vector3.Cross(Vector3.up, flat.normalized) * Distance);
            if (body == null) return false;

            body.gameObject.AddComponent<MimicGunner>().Setup(rig, body);
            return true;
        }

        public override string Describe() => "summons an illusion that shoots";
    }

    /// <summary>The mimic's gun: aims itself at the enemy nearest its view and fires a phantom of the held gun.</summary>
    public class MimicGunner : MonoBehaviour
    {
        public const float ChargePerHit = 0.25f;
        public const float Range = 30f;

        public PhantomWeapon Gun { get; private set; }
        private MinionController _body;
        private PsiBladesMastery _psi;
        private Transform _eye;

        public void Setup(PlayerRig rig, MinionController body)
        {
            _body = body;
            _eye = Build.Empty(body.transform, "MimicEye", Vector3.up * 1.5f).transform;
            _psi = rig.Masteries != null ? rig.Masteries.Get<PsiBladesMastery>() : null;

            // The gun's own stats and nothing of the player's, as decided.
            Gun = PhantomWeapon.Create(rig.Holster, _eye, body.gameObject, useOwnerStats: false, shareInfusions: false);
            Gun.Weapon.Hit += OnHit;
        }

        private void OnHit(WeaponHit hit)
        {
            if (_psi != null) _psi.AddBonus(ChargePerHit);
        }

        private void Update() => Step();

        public void Step()
        {
            if (_body == null || Gun == null || Gun.Weapon == null) return;

            IDamageable target = AutoAim.PickTarget(_eye.position, _eye.forward, Range, 180f, Team.Player);
            Gun.Weapon.ForcedTarget = target != null ? target.Transform : null;
            if (target == null) return;

            _eye.LookAt(AbilityContext.CenterOf(target));
            Gun.Weapon.TryFire();
        }

        private void OnDestroy()
        {
            if (Gun != null && Gun.Weapon != null) Gun.Weapon.Hit -= OnHit;
        }
    }

    /// <summary>
    /// Swaps places with the raised zombie nearest the crosshair within a long range, leaving it where you
    /// stood. Refused with none. Only Raise Dead's zombies are swap targets, the default for that open
    /// question. The landing is safe by construction: the zombie was standing there. Lich Guise.
    /// </summary>
    [System.Serializable]
    public class SwapWithMinionEffect : AbilityEffect
    {
        public string MinionId = "zombie";
        public float Range = 40f;
        public float MaxAngle = 35f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Motor == null || ctx.Caster == null) return false;

            MinionController best = null;
            float bestAngle = MaxAngle;

            foreach (MinionController minion in MinionController.Live)
            {
                if (minion.Definition == null || minion.Definition.Id != MinionId || minion.IsDown) continue;
                if (minion.Health == null || !minion.Health.IsAlive) continue;

                Vector3 to = minion.transform.position + Vector3.up - ctx.Origin;
                if (to.sqrMagnitude > Range * Range) continue;

                float angle = Vector3.Angle(ctx.Forward, to);
                if (angle > bestAngle) continue;

                best = minion;
                bestAngle = angle;
            }

            if (best == null) return false;

            Vector3 playerAt = ctx.Caster.transform.position;
            Vector3 zombieAt = best.transform.position;

            LandingCheck.Place(best.transform, playerAt);
            ctx.Motor.Teleport(zombieAt, preserveVelocity: false);
            ctx.Point = zombieAt;
            return true;
        }

        public override string Describe() => "swaps places with a zombie";
    }

    /// <summary>
    /// Summons the Unspeakable One at the point. Its tentacles burst up beside random enemies nearby, each
    /// fearing the nearest enemy not already afraid, and its maw rises to eat any enemy that comes close. The
    /// maw is an execute that takes a chunk from elites, and does not fear, so fear herds enemies into it.
    /// </summary>
    [System.Serializable]
    public class KrakenEffect : AbilityEffect
    {
        public float Seconds = 12f;
        public float Radius = 14f;
        public float TentacleDamage = 24f;

        public override bool Execute(AbilityContext ctx)
        {
            KrakenSummon.Spawn(ctx.Point, Seconds * ctx.LevelScale, Radius, TentacleDamage * ctx.Power, ctx.Team, ctx.Caster, ctx.Tint);
            return true;
        }

        public override string Describe() => "summons the Unspeakable One";
    }

    public class KrakenSummon : MonoBehaviour
    {
        public const float TentacleInterval = 0.9f;
        public const float TentacleRadius = 1.8f;
        public const float FearSeconds = 2.5f;
        public const float MawRadius = 2.5f;
        public const float MawInterval = 1.2f;

        private float _left;
        private float _radius;
        private float _damage;
        private float _tentacleTimer;
        private float _mawTimer;
        private Team _team;
        private GameObject _source;
        private Color _tint;

        public int Tentacles { get; private set; }
        public int Eaten { get; private set; }

        public static KrakenSummon Spawn(Vector3 at, float seconds, float radius, float damage, Team team, GameObject source, Color tint)
        {
            var go = new GameObject("UnspeakableOne");
            go.transform.position = at;
            Build.GroundDisc(go.transform, "Maw", Vector3.up * 0.05f, MawRadius,
                MaterialLibrary.Transparent(new Color(0.05f, 0.08f, 0.12f, 0.75f)));

            var kraken = go.AddComponent<KrakenSummon>();
            kraken._left = seconds;
            kraken._radius = radius;
            kraken._damage = damage;
            kraken._team = team;
            kraken._source = source;
            kraken._tint = tint;
            return kraken;
        }

        private void Update() => Step(WorldClock.DeltaTime);

        public void Step(float dt)
        {
            _left -= dt;
            if (_left <= 0f)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            if ((_tentacleTimer -= dt) <= 0f)
            {
                _tentacleTimer = TentacleInterval;
                Tentacle();
            }

            if ((_mawTimer -= dt) <= 0f)
            {
                _mawTimer = MawInterval;
                Maw();
            }
        }

        public void Tentacle()
        {
            List<Health> enemies = EnemiesWithin(transform.position, _radius);
            if (enemies.Count == 0) return;

            Health near = enemies[Random.Range(0, enemies.Count)];
            Vector2 offset = Random.insideUnitCircle.normalized * 1.5f;
            Vector3 spot = near.transform.position + new Vector3(offset.x, 0f, offset.y);

            GameObject tentacle = Build.Cylinder(null, "Tentacle", spot + Vector3.up * 1.2f, new Vector3(0.5f, 1.2f, 0.5f),
                MaterialLibrary.Lit(new Color(0.2f, 0.35f, 0.35f)), collider: false);
            FadeAndDie.Attach(tentacle, 0.6f, new Color(0.2f, 0.35f, 0.35f));
            Tentacles++;

            DamageInfo template = DamageInfo.Create(_damage, DamageType.Kinetic, _team, _source);
            template.CanCrit = false;
            template.Origin = DamageOrigin.Spell;
            Combat.Explode(spot + Vector3.up, TentacleRadius, template, Layers.HitMaskFor(_team), 0.5f, 7f);

            // Fear from the tentacle, remembered as where it stood, so the enemy flees it even once it has gone.
            Health nearest = null;
            float best = float.MaxValue;
            foreach (Health enemy in EnemiesWithin(spot, 8f))
            {
                StatusController status = enemy.GetComponent<StatusController>();
                if (status == null || status.IsFeared) continue;

                float distance = (enemy.transform.position - spot).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = enemy;
                }
            }

            if (nearest != null)
                nearest.GetComponent<StatusController>().Apply(StatusLibrary.Fear(FearSeconds), tentacle, _team);
        }

        public void Maw()
        {
            foreach (Health enemy in EnemiesWithin(transform.position, MawRadius))
            {
                DamageInfo cause = DamageInfo.Create(0f, DamageType.Execute, _team, _source);
                cause.Origin = DamageOrigin.Spell;
                if (enemy.Execute(cause, DeathmarkStatus.EliteFraction))
                {
                    Eaten++;
                    return;
                }
            }
        }

        private static List<Health> EnemiesWithin(Vector3 at, float radius)
        {
            var list = new List<Health>();
            Collider[] found = Physics.OverlapSphere(at, radius, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found.Length; i++)
            {
                Health health = found[i].GetComponentInParent<Health>();
                if (health != null && health.IsAlive && health.Team == Team.Enemy && !list.Contains(health)) list.Add(health);
            }
            return list;
        }
    }

    /// <summary>
    /// Souls rain around the caster, the storm moving with them. Every kill while it lasts extends it, with no
    /// cap: the goal is to keep it going for the whole floor. Each kill binds its soul the ordinary way, through
    /// the Souls mastery, rather than a second one. It ends with the floor. Soul Storm.
    /// </summary>
    [System.Serializable]
    public class SoulStormEffect : AbilityEffect
    {
        public float Seconds = 8f;
        public float ExtendPerKill = 2.5f;
        public float Radius = 9f;
        public float StrikeDamage = 18f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return false;
            SoulStorm.Spawn(ctx.Caster.transform, Seconds * ctx.LevelScale, ExtendPerKill, Radius, StrikeDamage * ctx.Power,
                ctx.DamageType, ctx.Team, ctx.Caster, ctx.Tint);
            return true;
        }

        public override string Describe() => "a storm of souls that lasts while you kill";
    }

    public class SoulStorm : MonoBehaviour
    {
        public const float StrikeInterval = 0.45f;
        public const float StrikeRadius = 2f;

        public float Remaining { get; private set; }

        private Transform _follow;
        private float _extend;
        private float _radius;
        private float _damage;
        private float _timer;
        private DamageType _type;
        private Team _team;
        private GameObject _source;
        private Color _tint;
        private bool _bound;

        public static SoulStorm Spawn(Transform follow, float seconds, float extend, float radius, float damage, DamageType type,
            Team team, GameObject source, Color tint)
        {
            var storm = new GameObject("SoulStorm").AddComponent<SoulStorm>();
            storm._follow = follow;
            storm.Remaining = seconds;
            storm._extend = extend;
            storm._radius = radius;
            storm._damage = damage;
            storm._type = type;
            storm._team = team;
            storm._source = source;
            storm._tint = tint;

            Health.AnyDied += storm.OnAnyDied;
            LevelEvents.FloorLeaving += storm.OnFloorLeaving;
            storm._bound = true;
            return storm;
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (this != null && RunState.CountsAsKill(victim)) Remaining += _extend;
        }

        private void OnFloorLeaving(int floor)
        {
            if (this != null) Remove();
        }

        private void Update() => Step(WorldClock.DeltaTime);

        public void Step(float dt)
        {
            Remaining -= dt;
            if (Remaining <= 0f || _follow == null)
            {
                Remove();
                return;
            }

            transform.position = _follow.position;
            if ((_timer -= dt) > 0f) return;
            _timer = StrikeInterval;

            Vector2 offset = Random.insideUnitCircle * _radius;
            Vector3 at = transform.position + new Vector3(offset.x, 0f, offset.y);

            DamageInfo template = DamageInfo.Create(_damage, _type, _team, _source);
            template.CanCrit = false;
            template.Origin = DamageOrigin.Spell;
            Combat.Explode(at + Vector3.up * 0.5f, StrikeRadius, template, Layers.HitMaskFor(_team), 0.5f);
            Combat.SpawnTracer(at + Vector3.up * 10f, at, _tint, 0.12f, 0.2f);
        }

        private void Remove()
        {
            Unbind();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void Unbind()
        {
            if (!_bound) return;
            _bound = false;
            Health.AnyDied -= OnAnyDied;
            LevelEvents.FloorLeaving -= OnFloorLeaving;
        }

        private void OnDestroy() => Unbind();
    }

    /// <summary>
    /// Defiles the ground. The caster's bullets deal extra necrotic damage while they stand in it, and against
    /// enemies standing in it, both when both are inside. The bonus is its own hit, so it still lands on an
    /// ethereal enemy that throws the bullet away. Desecrate.
    /// </summary>
    [System.Serializable]
    public class DesecrateEffect : AbilityEffect
    {
        public float Radius = 5f;
        public float Seconds = 10f;
        public float BonusDamage = 5f;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            if (rig == null || rig.Weapon == null) return false;

            DesecratedGround.Spawn(rig.transform.position, Radius, Seconds * ctx.LevelScale, BonusDamage * ctx.Power, rig.Weapon, ctx.Tint);
            return true;
        }

        public override string Describe() => "defiles the ground";
    }

    public class DesecratedGround : MonoBehaviour
    {
        private float _radius;
        private float _left;
        private float _bonus;
        private Weapon _weapon;

        public static DesecratedGround Spawn(Vector3 at, float radius, float seconds, float bonus, Weapon weapon, Color tint)
        {
            var go = new GameObject("DesecratedGround");
            go.transform.position = at;
            Build.GroundDisc(go.transform, "Defiled", Vector3.up * 0.05f, radius,
                MaterialLibrary.Transparent(new Color(tint.r, tint.g, tint.b, 0.3f)));

            var ground = go.AddComponent<DesecratedGround>();
            ground._radius = radius;
            ground._left = seconds;
            ground._bonus = bonus;
            ground._weapon = weapon;
            weapon.Hit += ground.OnHit;
            return ground;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= _radius * _radius;
        }

        private void OnHit(WeaponHit hit)
        {
            if (this == null || hit.Target == null || hit.Target.Transform == null) return;

            int inside = 0;
            if (hit.Weapon != null && hit.Weapon.Owner != null && Contains(hit.Weapon.Owner.transform.position)) inside++;
            if (Contains(hit.Target.Transform.position)) inside++;

            if (inside > 0) hit.DealBonus(_bonus * inside, DamageType.Necrotic);
        }

        private void Update() => Step(WorldClock.DeltaTime);

        public void Step(float dt)
        {
            if ((_left -= dt) > 0f) return;

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            if (_weapon != null) _weapon.Hit -= OnHit;
        }
    }

    /// <summary>
    /// A whirlpool that drags enemies toward its centre while tentacles lash them. Its centre is a hazard, so
    /// enemies know to move away from it and fight the pull. Fathomless Gate.
    /// </summary>
    [System.Serializable]
    public class WhirlpoolEffect : AbilityEffect
    {
        public float Radius = 6f;
        public float Seconds = 6f;
        public float PullSpeed = 5f;
        public float DamagePerTick = 6f;

        public override bool Execute(AbilityContext ctx)
        {
            Whirlpool.Spawn(ctx.Point, Radius * ctx.LevelScale, Seconds, PullSpeed, DamagePerTick * ctx.Power, ctx.DamageType,
                ctx.Team, ctx.Caster, ctx.Tint);
            return true;
        }

        public override string Describe() => "a whirlpool that drags enemies in";
    }

    public class Whirlpool : MonoBehaviour
    {
        public const float PullInterval = 0.25f;
        public const float DamageInterval = 0.5f;
        public const float CentreRadius = 2f;

        private float _radius;
        private float _left;
        private float _pull;
        private float _damage;
        private float _pullTimer;
        private float _damageTimer;
        private DamageType _type;
        private Team _team;
        private GameObject _source;
        private Hazards.Hazard _hazard;

        public static Whirlpool Spawn(Vector3 at, float radius, float seconds, float pull, float damage, DamageType type,
            Team team, GameObject source, Color tint)
        {
            var go = new GameObject("Whirlpool");
            go.transform.position = at;
            Build.GroundDisc(go.transform, "Water", Vector3.up * 0.05f, radius,
                MaterialLibrary.Transparent(new Color(tint.r, tint.g, tint.b, 0.3f)));

            var pool = go.AddComponent<Whirlpool>();
            pool._radius = radius;
            pool._left = seconds;
            pool._pull = pull;
            pool._damage = damage;
            pool._type = type;
            pool._team = team;
            pool._source = source;
            pool._hazard = Hazards.Register(go.transform, CentreRadius, team);
            return pool;
        }

        private void Update() => Step(WorldClock.DeltaTime);

        public void Step(float dt)
        {
            _left -= dt;
            if (_left <= 0f)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            bool pull = (_pullTimer -= dt) <= 0f;
            bool lash = (_damageTimer -= dt) <= 0f;
            if (!pull && !lash) return;
            if (pull) _pullTimer = PullInterval;
            if (lash) _damageTimer = DamageInterval;

            Collider[] found = Physics.OverlapSphere(transform.position, _radius, Layers.TargetMaskFor(_team),
                QueryTriggerInteraction.Ignore);

            var struck = new HashSet<IDamageable>();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target == null || !target.IsAlive || !struck.Add(target)) continue;

                if (pull) Combat.PullToward(target.Transform, transform.position, _pull, _source, _team);

                if (lash && _damage > 0f)
                {
                    DamageInfo info = DamageInfo.Create(_damage, _type, _team, _source);
                    info.CanCrit = false;
                    info.Origin = DamageOrigin.Spell;
                    target.TakeDamage(info.At(target.Transform.position + Vector3.up, Vector3.up));
                }
            }
        }

        private void OnDestroy() => Hazards.Unregister(_hazard);
    }

    /// <summary>
    /// A slow star that hunts the highest-health enemy in combat with you and explodes on it. Refused when
    /// nothing is in combat. Walls stop it, as decided. Divine Star.
    /// </summary>
    [System.Serializable]
    public class DivineStarEffect : AbilityEffect
    {
        public float Range = 45f;
        public float Speed = 7f;
        public float Damage = 60f;
        public float SplashRadius = 3f;
        public float Lifetime = 14f;

        /// <summary>The enemy the last star was sent after, for tooling.</summary>
        public static Transform LastTarget;

        public override bool Execute(AbilityContext ctx)
        {
            EnemyController best = null;
            float bestHealth = -1f;

            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.IsAlerted || enemy.IsHidden || enemy.IsPossessed) continue;
                if (enemy.Health == null || !enemy.Health.IsAlive || enemy.Health.Team == Team.Player) continue;
                if ((enemy.transform.position - ctx.Origin).sqrMagnitude > Range * Range) continue;

                if (enemy.Health.Current > bestHealth)
                {
                    bestHealth = enemy.Health.Current;
                    best = enemy;
                }
            }

            if (best == null) return false;
            LastTarget = best.transform;

            Projectile star = Projectile.Create(ctx.Origin + ctx.Forward * 0.8f, ctx.Forward, ctx.Tint, 0.35f);
            star.OwnerTeam = ctx.Team;
            star.Owner = ctx.Caster;
            star.OwnerSheet = ctx.Sheet;
            star.IsSpell = true;
            star.SourceSpell = ctx.Spell;
            star.CanCrit = false;
            star.Damage = Damage * ctx.Power;
            star.SplashRadius = SplashRadius;
            star.SplashDamage = Damage * ctx.Power;
            star.DamageType = ctx.DamageType;
            star.Origin = ctx.DamageOrigin;
            star.Speed = Speed;
            star.Lifetime = Lifetime;
            star.HomingEnabled = true;
            star.HomingStrength = 4f;
            star.HomingTarget = best.transform;
            star.Statuses = new List<StatusApplication>(ctx.Payload);
            star.Launch();
            return true;
        }

        public override string Describe() => "a star that hunts the healthiest enemy";
    }

    /// <summary>
    /// Spends every point of mana and starts generating warped spheres that follow the caster, each firing
    /// once at a nearby enemy at a semi-random moment. More spheres with levels. Casting again replaces them,
    /// and the spheres not yet fired last until the floor ends. Their hits restore no mana. Reality Shards.
    /// </summary>
    [System.Serializable]
    public class RealityShardsEffect : AbilityEffect
    {
        public int Spheres = 3;
        public int SpheresPerLevel = 1;
        public float Damage = 26f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return false;

            if (ctx.Mana != null && ctx.Mana.Current > 0f) ctx.Mana.TrySpend(ctx.Mana.Current);

            RealityShards shards = ctx.Caster.GetComponent<RealityShards>();
            if (shards == null) shards = ctx.Caster.AddComponent<RealityShards>();

            shards.Begin(Spheres + (ctx.Level - 1) * SpheresPerLevel, Damage * ctx.Power, ctx.DamageType, ctx.Team, ctx.Caster, ctx.Tint);
            return true;
        }

        public override string Describe() => "spends all your mana on spheres that fire later";
    }

    public class RealityShards : MonoBehaviour
    {
        public const float SpawnInterval = 1.2f;
        public const float Range = 25f;
        public const float OrbitRadius = 1.4f;

        private sealed class Sphere
        {
            public GameObject Visual;
            public float FireTimer;
        }

        private readonly List<Sphere> _spheres = new List<Sphere>();
        private int _pending;
        private float _spawnTimer;
        private float _damage;
        private float _orbit;
        private DamageType _type;
        private Team _team;
        private GameObject _source;
        private Color _tint;
        private bool _bound;

        public int Count => _spheres.Count;
        public int Pending => _pending;
        public int Fired { get; private set; }

        public void Begin(int spheres, float damage, DamageType type, Team team, GameObject source, Color tint)
        {
            Clear();
            _pending = Mathf.Max(1, spheres);
            _spawnTimer = 0f;
            _damage = damage;
            _type = type;
            _team = team;
            _source = source;
            _tint = tint;

            if (_bound) return;
            LevelEvents.FloorLeaving += OnFloorLeaving;
            _bound = true;
        }

        private void OnFloorLeaving(int floor)
        {
            if (this != null) Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < _spheres.Count; i++) RemoveVisual(_spheres[i].Visual);
            _spheres.Clear();
            _pending = 0;
        }

        private void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (_pending > 0 && (_spawnTimer -= dt) <= 0f)
            {
                _spawnTimer = SpawnInterval;
                _pending--;
                _spheres.Add(new Sphere
                {
                    Visual = Build.Sphere(null, "RealityShard", transform.position, 0.3f, MaterialLibrary.Emissive(_tint, 3f), collider: false),
                    FireTimer = Random.Range(0.8f, 2f)
                });
            }

            _orbit += dt * 120f;
            for (int i = _spheres.Count - 1; i >= 0; i--)
            {
                Sphere sphere = _spheres[i];
                float angle = (_orbit + i * 360f / Mathf.Max(1, _spheres.Count)) * Mathf.Deg2Rad;
                Vector3 at = transform.position + Vector3.up * 1.8f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * OrbitRadius;
                if (sphere.Visual != null) sphere.Visual.transform.position = at;

                if ((sphere.FireTimer -= dt) > 0f) continue;
                sphere.FireTimer = Random.Range(0.5f, 1.2f);

                // A sphere with nothing to fire at waits.
                IDamageable target = AutoAim.PickTarget(at, Vector3.forward, Range, 180f, _team);
                if (target == null) continue;

                Fire(at, AbilityContext.CenterOf(target));
                RemoveVisual(sphere.Visual);
                _spheres.RemoveAt(i);
            }
        }

        private void Fire(Vector3 from, Vector3 at)
        {
            Projectile shot = Projectile.Create(from, at - from, _tint, 0.15f);
            shot.OwnerTeam = _team;
            shot.Owner = _source;
            shot.IsSpell = true;
            shot.CanCrit = false;
            shot.Damage = _damage;
            shot.DamageType = _type;
            shot.Origin = DamageOrigin.Spell;
            shot.Speed = 40f;
            shot.Lifetime = 3f;
            shot.Launch();
            Fired++;
        }

        private static void RemoveVisual(GameObject visual)
        {
            if (visual == null) return;
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
        }

        private void OnDestroy()
        {
            Clear();
            if (_bound) LevelEvents.FloorLeaving -= OnFloorLeaving;
        }
    }

    /// <summary>Lights the route to the floor's exit ahead of the caster with glowing floor markers, for a while. Path of Light.</summary>
    [System.Serializable]
    public class RouteMarkersEffect : AbilityEffect
    {
        public float Seconds = 10f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return false;
            RouteMarkers.Show(ctx.Caster.transform, Seconds, ctx.Tint);
            return true;
        }

        public override string Describe() => "lights the way to the exit";
    }

    public class RouteMarkers : MonoBehaviour
    {
        public const int Length = 12;
        public const float Refresh = 0.3f;

        private readonly List<GameObject> _markers = new List<GameObject>();
        private Transform _follow;
        private float _left;
        private float _timer;
        private Color _tint;

        /// <summary>Overrides the room's route, for tooling.</summary>
        public ExitRouteMap RouteOverride;

        public int Shown { get; private set; }

        public static RouteMarkers Show(Transform follow, float seconds, Color tint)
        {
            RouteMarkers existing = Object.FindAnyObjectByType<RouteMarkers>();
            RouteMarkers markers = existing != null ? existing : new GameObject("PathOfLight").AddComponent<RouteMarkers>();
            markers._follow = follow;
            markers._left = seconds;
            markers._tint = tint;
            markers._timer = 0f;
            return markers;
        }

        private void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            _left -= dt;
            if (_left <= 0f || _follow == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            if ((_timer -= dt) > 0f) return;
            _timer = Refresh;
            Redraw();
        }

        public void Redraw()
        {
            ExitRouteMap route = RouteOverride;
            if (route == null)
            {
                GameDirector director = GameDirector.Instance;
                RoomRuntime room = director != null ? director.CurrentRoom : null;
                route = room != null ? room.ExitRoute : null;
            }

            Shown = 0;
            if (route != null)
            {
                Vector2Int cell = route.WorldToCell(_follow.position);
                for (int i = 0; i < Length && route.TryNextCell(cell, out Vector2Int next); i++)
                {
                    cell = next;
                    Place(Shown++, route.Maze.CellCentre(cell));
                }
            }

            for (int i = Shown; i < _markers.Count; i++)
                if (_markers[i] != null) _markers[i].SetActive(false);
        }

        private void Place(int index, Vector3 at)
        {
            while (_markers.Count <= index)
            {
                GameObject marker = Build.Cube(transform, "Marker", Vector3.zero, new Vector3(0.35f, 0.08f, 0.35f),
                    MaterialLibrary.Emissive(_tint, 3f), collider: false);
                _markers.Add(marker);
            }

            _markers[index].SetActive(true);
            _markers[index].transform.position = new Vector3(at.x, _follow.position.y + 0.08f, at.z);
        }
    }
}
