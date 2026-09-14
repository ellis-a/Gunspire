using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>A request to apply a status effect, carried along with damage.</summary>
    [System.Serializable]
    public struct StatusApplication
    {
        public StatusId Id;
        public float Duration;
        public int Stacks;
        public float Magnitude;   // meaning depends on the effect (dps, slow %, ...)

        public StatusApplication(StatusId id, float duration, int stacks = 1, float magnitude = 1f)
        {
            Id = id;
            Duration = duration;
            Stacks = stacks;
            Magnitude = magnitude;
        }
    }

    /// <summary>
    /// How a hit was delivered. Everything on the player's side shares one team, so without this a
    /// familiar's bite, a burn tick and a bullet look identical - and sleep, lifesteal and the
    /// mastery meters all need to tell them apart. Never serialized, so members can be reordered.
    /// </summary>
    public enum DamageOrigin
    {
        /// <summary>Nothing set it: a cost, a scripted kill, or a source not yet tagged.</summary>
        Unspecified,
        Gun,
        Spell,
        Melee,

        /// <summary>An enemy's own attack.</summary>
        Attack,
        Minion,
        StatusTick,

        /// <summary>Something knocked into something else.</summary>
        Collision,
        Environment,

        /// <summary>A mastery's own damage: a Conflux reaction, a Psi Blades bonus. Never lifesteal.</summary>
        Mastery
    }

    /// <summary>
    /// Everything a hit needs to know. Built by the attacker, consumed by <see cref="Health"/>.
    /// Pass by 'in' - it is a fat struct and gets thrown around a lot.
    /// </summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public Team SourceTeam;
        public GameObject Source;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public Vector3 Knockback;
        public bool IsCrit;
        public bool CanCrit;
        public DamageOrigin Origin;

        public List<StatusApplication> Statuses;

        public static DamageInfo Create(float amount, DamageType type, Team team, GameObject source)
        {
            return new DamageInfo
            {
                Amount = amount,
                Type = type,
                SourceTeam = team,
                Source = source,
                HitNormal = Vector3.up,
                CanCrit = true,
                Statuses = null
            };
        }

        /// <summary>
        /// Damage dealt on someone else's account, such as an enemy knocked into another enemy.
        /// Issued from the instigator's side, so friendly fire does not refuse it and the hit is
        /// credited to whoever caused it rather than to the body that happened to connect.
        /// </summary>
        public static DamageInfo OnBehalfOf(float amount, DamageType type, Team instigatorTeam,
            GameObject instigator, DamageOrigin origin)
        {
            DamageInfo info = Create(amount, type, instigatorTeam, instigator);
            info.Origin = origin;
            return info;
        }

        public DamageInfo WithStatus(StatusApplication status)
        {
            if (Statuses == null) Statuses = new List<StatusApplication>(2);
            Statuses.Add(status);
            return this;
        }

        public DamageInfo WithStatuses(List<StatusApplication> statuses)
        {
            if (statuses == null || statuses.Count == 0) return this;
            if (Statuses == null) Statuses = new List<StatusApplication>(statuses.Count);
            Statuses.AddRange(statuses);
            return this;
        }

        public DamageInfo At(Vector3 point, Vector3 normal)
        {
            HitPoint = point;
            HitNormal = normal;
            return this;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        Team Team { get; }
        Transform Transform { get; }
        void TakeDamage(in DamageInfo info);
    }
}
