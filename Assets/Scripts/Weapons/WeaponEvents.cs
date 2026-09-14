using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>One round leaving a gun, raised by <see cref="Weapon.Fired"/>. Echo records these.</summary>
    public struct WeaponShot
    {
        public Weapon Weapon;
        public ShotSpec Spec;

        /// <summary>Where the round was aimed from, and the aim before spread was applied.</summary>
        public Vector3 Origin;
        public Vector3 Direction;

        public int AmmoCost;
    }

    /// <summary>
    /// A round from a gun landing on something that can be hurt, raised by
    /// <see cref="Weapon.Hit"/> after its damage has been dealt, for hitscan and projectiles alike.
    /// </summary>
    public struct WeaponHit
    {
        public Weapon Weapon;
        public IDamageable Target;
        public DamageInfo Damage;
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction;

        /// <summary>
        /// Deals extra damage as its own instance rather than folding it into this hit. An ethereal
        /// target throws a kinetic hit away before anything else is considered, so a bonus riding
        /// inside that hit would be thrown away with it. Raises no further hit of its own.
        /// </summary>
        public void DealBonus(float amount, DamageType type)
        {
            if (Target == null || !Target.IsAlive || amount <= 0f) return;

            DamageInfo bonus = DamageInfo.Create(amount, type, Damage.SourceTeam, Damage.Source);
            bonus.CanCrit = false;
            bonus.Origin = Damage.Origin;
            Target.TakeDamage(bonus.At(Point, Normal));
        }
    }

    /// <summary>
    /// A temporary charge on the player's bullets: statuses every round carries, and an optional
    /// effect when a round lands. Lasts for a time, for a number of rounds, or both, ending at
    /// whichever runs out first.
    ///
    /// It lives on the weapon component, which the holster keeps and re-equips rather than
    /// replaces, so an infusion carries across a swap by construction.
    /// </summary>
    public class BulletInfusion
    {
        /// <summary>Infusing again with the same id replaces the old charge rather than stacking it.</summary>
        public string Id;

        public readonly List<StatusApplication> Statuses = new List<StatusApplication>();
        public Action<WeaponHit> OnHit;

        public bool HasTimeLimit;
        public float SecondsLeft;

        public bool HasRoundLimit;
        public int RoundsLeft;

        public bool Expired => (HasTimeLimit && SecondsLeft <= 0f) || (HasRoundLimit && RoundsLeft <= 0);

        public static BulletInfusion ForSeconds(string id, float seconds, params StatusApplication[] statuses)
        {
            var infusion = new BulletInfusion { Id = id, HasTimeLimit = true, SecondsLeft = seconds };
            infusion.Statuses.AddRange(statuses);
            return infusion;
        }

        public static BulletInfusion ForRounds(string id, int rounds, params StatusApplication[] statuses)
        {
            var infusion = new BulletInfusion { Id = id, HasRoundLimit = true, RoundsLeft = rounds };
            infusion.Statuses.AddRange(statuses);
            return infusion;
        }
    }
}
