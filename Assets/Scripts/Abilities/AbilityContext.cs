using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The scratchpad a chain of <see cref="AbilityEffect"/>s reads from and writes to.
    ///
    /// One instance lives per caster and is reused for every cast, so effects must keep no
    /// state of their own. Selectors fill in <see cref="Targets"/> and <see cref="Point"/>;
    /// the effects after them act on whatever was selected.
    /// </summary>
    public class AbilityContext
    {
        // ---- caster wiring, set once when the context is built ----
        public GameObject Caster;
        public Team Team = Team.Player;
        public CharacterSheet Sheet;
        public Mana Mana;
        public Health Health;
        public StatusController Status;
        public PlayerMotor Motor;
        public CharacterController Controller;

        /// <summary>Where abilities are aimed from: the camera for the player, the muzzle for an enemy.</summary>
        public Transform Aim;

        /// <summary>
        /// Run-wide on-hit statuses granted by boons. Copied into <see cref="Payload"/> at the
        /// start of every cast, so an upgrade written once reaches every ability.
        /// </summary>
        public List<StatusApplication> ExtraStatuses;

        // ---- per-cast state, reset by Begin ----
        public Spell Spell;
        public int Level = 1;

        /// <summary>Outgoing multiplier: sheet bonuses for this school and category, times level growth.</summary>
        public float Power = 1f;

        /// <summary>Level growth on its own, for effects that scale range or radius rather than damage.</summary>
        public float LevelScale = 1f;

        public DamageType DamageType = DamageType.Astral;
        public SpellType Category = SpellType.Attack;
        public Color Tint = Color.white;

        public Vector3 Origin;
        public Vector3 Forward;

        /// <summary>Point of interest: an impact, a blink destination, the centre of a blast.</summary>
        public Vector3 Point;

        /// <summary>Filled in by selector effects. Cleared at the start of every cast.</summary>
        public readonly List<IDamageable> Targets = new List<IDamageable>();

        /// <summary>Statuses that damage dealt by this cast will carry.</summary>
        public readonly List<StatusApplication> Payload = new List<StatusApplication>();

        /// <summary>
        /// Targets already struck this cast, so a swing that is evaluated over several frames
        /// cannot hit the same enemy twice.
        /// </summary>
        public readonly HashSet<IDamageable> AlreadyHit = new HashSet<IDamageable>();

        /// <summary>What an enemy is attacking. Null for the player, who aims with the camera.</summary>
        public Transform TargetTransform;

        /// <summary>Set when a timed effect gives up, e.g. the caster was frozen mid wind-up.</summary>
        public bool Aborted;

        public int HitMask => Layers.HitMaskFor(Team);
        public int TargetMask => Layers.TargetMaskFor(Team);

        /// <summary>False once the caster is dead or crowd-controlled, which interrupts wind-ups.</summary>
        public bool CasterCanAct
        {
            get
            {
                if (Caster == null) return false;
                if (Health != null && !Health.IsAlive) return false;
                if (Status != null && Status.IsControlImpaired) return false;
                return true;
            }
        }

        // ---------------------------------------------------------------- lifecycle

        public void BeginCast(Spell spell, int level)
        {
            Spell = spell;
            Begin(spell.DamageType, spell.Type, spell.Tint, level, spell.LevelMultiplier(level), isSpell: true);
        }

        /// <summary>
        /// Starts an ability. Enemy attacks use this directly, with no <see cref="Spell"/>.
        /// </summary>
        public void Begin(DamageType damageType, SpellType category, Color tint, int level,
            float levelScale, bool isSpell)
        {
            Level = Mathf.Max(1, level);
            LevelScale = levelScale;
            DamageType = damageType;
            Category = category;
            Tint = tint;

            Power = Combat.OutgoingMultiplier(Sheet, isSpell, damageType, category) * levelScale;

            Origin = Aim != null
                ? Aim.position
                : Caster.transform.position + Vector3.up * 1.5f;
            Forward = Aim != null ? Aim.forward : Caster.transform.forward;
            Point = Origin + Forward;

            Targets.Clear();
            Payload.Clear();
            AlreadyHit.Clear();
            Aborted = false;
            if (ExtraStatuses != null) Payload.AddRange(ExtraStatuses);
        }

        public void EndCast()
        {
            Spell = null;
            Targets.Clear();
            Payload.Clear();
        }

        // ---------------------------------------------------------------- helpers for effects

        public void AddPayload(StatusApplication status) => Payload.Add(status);

        /// <summary>Builds a hit carrying this cast's school, source and status payload.</summary>
        public DamageInfo BuildDamage(float amount, Vector3 point, Vector3 normal, bool canCrit = false)
        {
            DamageInfo info = DamageInfo.Create(amount, DamageType, Team, Caster);
            info.CanCrit = canCrit;

            if (canCrit && Combat.RollCrit(Sheet, out float critMultiplier))
            {
                info.Amount = amount * critMultiplier;
                info.IsCrit = true;
            }

            return info.At(point, normal).WithStatuses(Payload);
        }

        /// <summary>Centre of mass of a target, which is what effects should aim at.</summary>
        public static Vector3 CenterOf(IDamageable target) => target.Transform.position + Vector3.up * 0.9f;

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.3f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }
    }
}
