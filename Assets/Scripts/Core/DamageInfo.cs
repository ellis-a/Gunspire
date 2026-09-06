using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
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

        /// <summary>Compared against <c>Smashable.Hardness</c>. Guns leave this at zero; a melee bash uses Strength.</summary>
        public float SmashPower;

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
