using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // The effect library the school spells are built from: the general-purpose steps that Phase 5 of the
    // architecture plan lists. Controllers that live on in the world after a cast are in SpellWorld.cs.

    /// <summary>
    /// A single instant line along the aim, selecting the first thing it strikes. Spells have no hitscan of
    /// their own; this is it. Mind Spike and Telekinesis.
    /// </summary>
    [System.Serializable]
    public class InstantRayEffect : SelectorEffect
    {
        public float Range = 40f;
        public float Radius = 0.15f;
        public bool DrawLine = true;

        public override bool Execute(AbilityContext ctx)
        {
            ctx.Targets.Clear();
            ctx.Point = ctx.Origin + ctx.Forward * Range;

            if (Physics.SphereCast(ctx.Origin, Radius, ctx.Forward, out RaycastHit hit, Range, ctx.HitMask,
                    QueryTriggerInteraction.Ignore))
            {
                ctx.Point = hit.point;

                IDamageable target = Combat.FindDamageable(hit.collider);
                if (target != null && target.IsAlive && (target.Team != ctx.Team || target.Team == Team.Neutral))
                    ctx.Targets.Add(target);
            }

            // Started ahead of and below the eye, or a line from the camera is edge-on and all but invisible.
            if (DrawLine)
                Combat.SpawnTracer(ctx.Origin + ctx.Forward * 0.6f + Vector3.down * 0.25f, ctx.Point, ctx.Tint, 0.06f, 0.12f);

            return true;
        }

        public override string Describe() => string.Format("an instant ray up to {0:0}m", Range);
    }

    /// <summary>Changes the damage type for the rest of the chain. Star Comet's energy then kinetic.</summary>
    [System.Serializable]
    public class SetDamageTypeEffect : AbilityEffect
    {
        public DamageType Type = DamageType.Kinetic;

        public override bool Execute(AbilityContext ctx)
        {
            ctx.DamageType = Type;
            return true;
        }

        public override string Describe() => "then " + DamageTypes.Name(Type);
    }

    /// <summary>Moves the origin back along the aim and up. Storm Blast starts behind the caster, so the enemies beside them are caught.</summary>
    [System.Serializable]
    public class OriginOffsetEffect : AbilityEffect
    {
        public float Back = 3f;
        public float Up;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 flat = new Vector3(ctx.Forward.x, 0f, ctx.Forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = ctx.Caster != null ? ctx.Caster.transform.forward : Vector3.forward;

            ctx.Origin += -flat.normalized * Back + Vector3.up * Up;
            return true;
        }

        public override string Describe() => null;
    }

    /// <summary>
    /// Runs its steps a second time from the Bestial companion's position, along the caster's aim. With no
    /// companion the steps are skipped and the cast still stands. Fang and Claw.
    /// </summary>
    [System.Serializable]
    public class FromCompanionEffect : AbilityEffect
    {
        [SerializeReference] public List<AbilityEffect> Body = new List<AbilityEffect>();

        public override bool Execute(AbilityContext ctx)
        {
            MinionController companion = CompanionOf(ctx);
            if (companion == null || Body == null || Body.Count == 0) return true;

            Vector3 origin = ctx.Origin;
            Vector3 point = ctx.Point;
            var targets = new List<IDamageable>(ctx.Targets);

            ctx.Origin = companion.transform.position + Vector3.up * 0.9f;
            ctx.Targets.Clear();
            AbilityRunner.Run(Body, ctx);

            ctx.Origin = origin;
            ctx.Point = point;
            ctx.Targets.Clear();
            ctx.Targets.AddRange(targets);
            return true;
        }

        public static MinionController CompanionOf(AbilityContext ctx)
        {
            if (ctx == null || ctx.Caster == null) return null;

            MasteryHost host = ctx.Caster.GetComponent<MasteryHost>();
            BeastMastery beast = host != null ? host.Get<BeastMastery>() : null;
            MinionController companion = beast != null ? beast.Companion : null;
            return companion != null && companion.Health != null && companion.Health.IsAlive ? companion : null;
        }

        public override string Describe() => "and again from your companion";
    }

    /// <summary>Drags everything selected toward the point, as knockback pointed inward. Collapse Space.</summary>
    [System.Serializable]
    public class PullTowardPointEffect : AbilityEffect
    {
        public float Speed = 12f;

        public override bool Execute(AbilityContext ctx)
        {
            for (int i = 0; i < ctx.Targets.Count; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target != null && target.IsAlive && target.Transform != null)
                    Combat.PullToward(target.Transform, ctx.Point, Speed, ctx.Caster, ctx.Team);
            }
            return true;
        }

        public override string Describe() => "pulls inward";
    }

    /// <summary>
    /// Everything in a narrow, tall box along the aim, for a vertical strike. The melee cone is as wide as it
    /// is tall. Slice.
    /// </summary>
    [System.Serializable]
    public class SelectBoxEffect : SelectorEffect
    {
        public float Range = 4f;
        public float Width = 0.9f;
        public float Height = 3f;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 centre = ctx.Origin + ctx.Forward * (Range * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(ctx.Forward, Vector3.up);

            Collider[] found = Physics.OverlapBox(centre, new Vector3(Width * 0.5f, Height * 0.5f, Range * 0.5f), rotation,
                ctx.TargetMask, QueryTriggerInteraction.Ignore);

            ctx.Targets.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target != null && target.IsAlive && !ctx.Targets.Contains(target)) ctx.Targets.Add(target);
            }

            ctx.Point = centre;
            return true;
        }

        public override string Describe() => string.Format("a {0:0.0}m-wide strike {1:0}m ahead", Width, Range);
    }

    /// <summary>
    /// Casts spells picked at random from one school and rarity, at this cast's level, never itself. The
    /// caster pays for this spell only. Elemental Chaos: two uncommon Elemental spells, repeats allowed.
    /// </summary>
    [System.Serializable]
    public class CastRandomSpellEffect : AbilityEffect
    {
        public SpellSchool School = SpellSchool.Elemental;
        public Rarity Rarity = Rarity.Uncommon;
        public int Count = 2;
        public bool AllowRepeats = true;

        /// <summary>Stops a spell that casts random spells from ever casting itself through another.</summary>
        private static int _depth;

        /// <summary>The ids cast by the last run, for tooling.</summary>
        public static readonly List<string> LastCast = new List<string>();

        public override bool Execute(AbilityContext ctx)
        {
            if (_depth > 0) return false;

            Spell caller = ctx.Spell;
            var pool = new List<Spell>();
            foreach (Spell spell in SpellLibrary.All)
            {
                if (spell.Slot != SpellSlot.Cast || spell.School != School || spell.Rarity != Rarity) continue;
                if (spell.IsSustained || spell.IsStance || spell.IsCharged) continue;
                if (caller != null && spell.Id == caller.Id) continue;
                pool.Add(spell);
            }

            if (pool.Count == 0) return false;

            int level = ctx.Level;
            LastCast.Clear();
            _depth++;

            try
            {
                for (int i = 0; i < Count && pool.Count > 0; i++)
                {
                    Spell pick = pool[Random.Range(0, pool.Count)];
                    if (!AllowRepeats) pool.Remove(pick);

                    LastCast.Add(pick.Id);
                    pick.Cast(ctx, level);
                }
            }
            finally
            {
                _depth--;
            }

            return true;
        }

        public override string Describe() => string.Format("casts {0} random {1} {2} spells", Count, Rarity, School);
    }

    /// <summary>
    /// A set amount of one status shared out between everything selected. Burn is one pool per target and a
    /// thinner pool does nothing to a target already burning hotter, so the aim is to catch just one. Judgement.
    /// </summary>
    [System.Serializable]
    public class SplitStatusEffect : AbilityEffect
    {
        public StatusId Status = StatusId.Burn;
        public float Duration = 12f;
        public float TotalMagnitude = 60f;
        public int Stacks = 1;

        public override bool Execute(AbilityContext ctx)
        {
            var hit = new List<StatusController>();
            for (int i = 0; i < ctx.Targets.Count; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target == null || !target.IsAlive || target.Transform == null) continue;

                StatusController status = target.Transform.GetComponent<StatusController>();
                if (status != null && !hit.Contains(status)) hit.Add(status);
            }

            if (hit.Count == 0) return true;

            StatusApplication share = ctx.Empower(new StatusApplication(Status, Duration, Stacks, TotalMagnitude));
            share.Magnitude /= hit.Count;

            for (int i = 0; i < hit.Count; i++) hit[i].Apply(share, ctx.Caster, ctx.Team);
            return true;
        }

        public override string Describe() => string.Format("{0:0} {1} split between everything caught", TotalMagnitude, Status);
    }

    /// <summary>Removes one random debuff from the caster, and from the companion if asked. Flicker, Embiggen.</summary>
    [System.Serializable]
    public class RemoveRandomDebuffEffect : AbilityEffect
    {
        public bool FromCompanion;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Status != null) ctx.Status.RemoveRandomDebuff();

            if (FromCompanion)
            {
                MinionController companion = FromCompanionEffect.CompanionOf(ctx);
                if (companion != null && companion.Status != null) companion.Status.RemoveRandomDebuff();
            }
            return true;
        }

        public override string Describe() => "shrugs off a debuff";
    }

    /// <summary>Stops the world clock for everything but the player. Stop Time.</summary>
    [System.Serializable]
    public class StopTimeEffect : AbilityEffect
    {
        public static readonly object Key = new object();

        public float Seconds = 6f;

        public override bool Execute(AbilityContext ctx)
        {
            WorldClock.StopFor(Key, Seconds);
            return true;
        }

        public override string Describe() => string.Format("stops time for {0:0}s", Seconds);
    }

    /// <summary>Puts a status on the Bestial companion. Howl, Embiggen.</summary>
    [System.Serializable]
    public class CompanionStatusEffect : AbilityEffect
    {
        public StatusId Status = StatusId.Quickened;
        public float Duration = 8f;
        public int Stacks = 1;
        public float Magnitude = 0.3f;

        public override bool Execute(AbilityContext ctx)
        {
            MinionController companion = FromCompanionEffect.CompanionOf(ctx);
            if (companion != null && companion.Status != null)
                companion.Status.Apply(ctx.Empower(new StatusApplication(Status, Duration, Stacks, Magnitude)), ctx.Caster, ctx.Team);
            return true;
        }

        public override string Describe() => "and your companion";
    }

    /// <summary>Spends a psi charge, when there is one, for bonus damage on everything selected. Mind Spike.</summary>
    [System.Serializable]
    public class PsiBonusEffect : AbilityEffect
    {
        public float BonusDamage = 20f;

        public override bool Execute(AbilityContext ctx)
        {
            PsiBladesMastery psi = SpellCosts.Psi(ctx);
            if (psi == null || ctx.Targets.Count == 0 || !psi.TrySpend(1f)) return true;

            for (int i = 0; i < ctx.Targets.Count; i++)
            {
                IDamageable target = ctx.Targets[i];
                if (target == null || !target.IsAlive) continue;

                DamageInfo bonus = ctx.BuildDamage(BonusDamage * ctx.Power, AbilityContext.CenterOf(target), -ctx.Forward);
                bonus.Type = DamageType.Psychic;
                target.TakeDamage(bonus);
            }
            return true;
        }

        public override string Describe() => "spends psi for more";
    }

    /// <summary>
    /// Charges the player's bullets: statuses on every round, for a time, a number of rounds, or both, and
    /// optionally a bolt of damage called down on whatever a round hits. Can reach the companion's attacks
    /// too. Viper's Sting, Smite.
    /// </summary>
    [System.Serializable]
    public class InfuseBulletsEffect : AbilityEffect
    {
        public string InfusionId = "infusion";
        public float Seconds;
        public int Rounds;
        public List<StatusApplication> Statuses = new List<StatusApplication>();

        /// <summary>Damage called down on whatever a round lands on, as its own hit.</summary>
        public float BoltDamage;
        public DamageType BoltType = DamageType.Energy;
        public List<StatusApplication> BoltStatuses = new List<StatusApplication>();

        public bool IncludeCompanion;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            Weapon weapon = rig != null ? rig.Weapon : null;
            if (weapon == null || (Seconds <= 0f && Rounds <= 0)) return false;

            var infusion = new BulletInfusion
            {
                Id = InfusionId,
                HasTimeLimit = Seconds > 0f, SecondsLeft = Seconds,
                HasRoundLimit = Rounds > 0, RoundsLeft = Rounds
            };
            infusion.Statuses.AddRange(ctx.EmpowerAll(Statuses));

            if (BoltDamage > 0f)
            {
                float damage = BoltDamage * ctx.Power;
                List<StatusApplication> boltStatuses = ctx.EmpowerAll(BoltStatuses);
                GameObject caster = ctx.Caster;
                Team team = ctx.Team;
                DamageType type = BoltType;
                Color tint = ctx.Tint;

                infusion.OnHit = hit =>
                {
                    if (hit.Target == null || !hit.Target.IsAlive) return;

                    Vector3 at = AbilityContext.CenterOf(hit.Target);
                    DamageInfo bolt = DamageInfo.Create(damage, type, team, caster);
                    bolt.CanCrit = false;
                    bolt.Origin = DamageOrigin.Spell;
                    hit.Target.TakeDamage(bolt.At(at, Vector3.up).WithStatuses(boltStatuses));
                    Combat.SpawnTracer(at + Vector3.up * 12f, at, tint, 0.14f, 0.18f);
                };
            }

            weapon.Infuse(infusion);

            if (IncludeCompanion && Seconds > 0f)
            {
                MinionController companion = FromCompanionEffect.CompanionOf(ctx);
                if (companion != null) companion.InfuseAttacks(ctx.EmpowerAll(Statuses), Seconds);
            }
            return true;
        }

        public override string Describe() => Rounds > 0 ? "charges your next bullet" : "charges your bullets";
    }

    /// <summary>
    /// Runs its steps after a delay, from a copy of the cast as it stood. Spells are otherwise instant. Lunge's
    /// strike lands once the leap is underway.
    /// </summary>
    [System.Serializable]
    public class DelayedEffect : AbilityEffect
    {
        public float Seconds = 0.25f;
        [SerializeReference] public List<AbilityEffect> Body = new List<AbilityEffect>();

        public override bool Execute(AbilityContext ctx)
        {
            if (Body != null && Body.Count > 0 && ctx.Caster != null) DelayedChain.Schedule(ctx, Body, Seconds);
            return true;
        }

        public override string Describe() => "then " + AbilityRunner.Describe(Body);
    }

    /// <summary>Runs a chain later, on its own object, on the caster's clock.</summary>
    public class DelayedChain : MonoBehaviour
    {
        private AbilityContext _ctx;
        private List<AbilityEffect> _body;
        private float _left;

        public static DelayedChain Schedule(AbilityContext source, List<AbilityEffect> body, float seconds)
        {
            var chain = new GameObject("DelayedChain").AddComponent<DelayedChain>();
            chain._ctx = Snapshot(source);
            chain._body = body;
            chain._left = seconds;
            return chain;
        }

        /// <summary>A copy of a cast's context, since the caster's own is reused by every later cast.</summary>
        public static AbilityContext Snapshot(AbilityContext source)
        {
            var copy = new AbilityContext
            {
                Caster = source.Caster, Team = source.Team, Sheet = source.Sheet, Mana = source.Mana,
                Health = source.Health, Status = source.Status, Motor = source.Motor, Controller = source.Controller,
                Aim = source.Aim, Level = source.Level, Power = source.Power, StatusPower = source.StatusPower,
                DamageOrigin = source.DamageOrigin, LevelScale = source.LevelScale, DamageType = source.DamageType,
                Category = source.Category, Tint = source.Tint, Origin = source.Origin, Forward = source.Forward,
                Point = source.Point, Charge = source.Charge, IsEcho = source.IsEcho, SoulsSpent = source.SoulsSpent
            };
            copy.Payload.AddRange(source.Payload);
            copy.Targets.AddRange(source.Targets);
            return copy;
        }

        private void Update() => Step(WorldClock.DeltaFor(_ctx != null ? _ctx.Caster : null));

        public void Step(float dt)
        {
            _left -= dt;
            if (_left > 0f) return;

            if (_ctx != null && _ctx.Caster != null) AbilityRunner.Run(_body, _ctx);

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    /// <summary>A wall of nether across the caster's path, just in front of them. Nether Wall.</summary>
    [System.Serializable]
    public class NetherWallEffect : AbilityEffect
    {
        public float Distance = 3f;

        /// <summary>Size and duration were left open; these are placeholders.</summary>
        public Vector3 Size = new Vector3(6f, 3.5f, 0.4f);
        public float Seconds = 8f;

        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Caster == null) return false;

            Vector3 flat = new Vector3(ctx.Forward.x, 0f, ctx.Forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = ctx.Caster.transform.forward;
            flat.Normalize();

            Vector3 centre = ctx.Caster.transform.position + flat * Distance + Vector3.up * (Size.y * 0.5f);
            WorldVolumes.NetherWall(centre, Quaternion.LookRotation(flat), Size, Seconds * ctx.LevelScale, ctx.Tint);
            ctx.Point = centre;
            return true;
        }

        public override string Describe() => "a wall that stops shots";
    }

    /// <summary>
    /// A cloud of nether smoke at the point: damage over time and blindness inside, and every player shot into
    /// it lands on a random enemy inside. Not a hazard and not a sight blocker, the defaults for both open questions.
    /// </summary>
    [System.Serializable]
    public class SmokeCloudEffect : AbilityEffect
    {
        public float Radius = 4f;
        public float Height = 4f;
        public float Seconds = 8f;
        public float DamagePerTick = 4f;
        public float BlindSeconds = 1.5f;

        public override bool Execute(AbilityContext ctx)
        {
            Vector3 ground = ctx.Point;
            if (Physics.Raycast(ctx.Point + Vector3.up, Vector3.down, out RaycastHit hit, 8f, Layers.BlockingMask,
                    QueryTriggerInteraction.Ignore))
                ground = hit.point;

            WorldVolumes.NetherSmoke(ground + Vector3.up * (Height * 0.5f), new Vector3(Radius * 2f, Height, Radius * 2f),
                Seconds, ctx.Tint);

            var blind = new List<StatusApplication> { ctx.Empower(StatusLibrary.Blind(BlindSeconds)) };
            LingeringZone.Spawn(ground, Radius, Seconds, DamagePerTick * ctx.Power, 0.5f, ctx.DamageType, ctx.Team,
                ctx.Caster, blind, ctx.Tint, ctx.DamageOrigin).NotAHazard();
            return true;
        }

        public override string Describe() => "a cloud of smoke";
    }

    /// <summary>For a while, every action the player takes happens again a moment later. Echo.</summary>
    [System.Serializable]
    public class EchoEffect : AbilityEffect
    {
        public float Seconds = 6f;
        public float Delay = 0.35f;

        public override bool Execute(AbilityContext ctx)
        {
            ActionLog log = ctx.Caster != null ? ctx.Caster.GetComponent<ActionLog>() : null;
            if (log == null) return false;

            log.BeginEchoing(Seconds, Delay);
            return true;
        }

        public override string Describe() => string.Format("echoes everything for {0:0}s", Seconds);
    }
}
