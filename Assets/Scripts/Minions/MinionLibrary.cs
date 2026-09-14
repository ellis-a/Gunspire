using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Every kind of walking minion. Code only for now: nothing summons one yet, so there is nothing to
    /// tune in the Inspector. Assets arrive with the first summoning spell, as the same hybrid every
    /// other roster uses.
    /// </summary>
    public static class MinionLibrary
    {
        private static List<MinionDefinition> _all;

        public static IReadOnlyList<MinionDefinition> All => _all ?? (_all = BuiltIn());

        /// <summary>A fresh copy, since a summon may adjust its definition without retuning every other.</summary>
        public static MinionDefinition Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i].Clone();
            return null;
        }

        public static List<MinionDefinition> BuiltIn() => new List<MinionDefinition> { Zombie() };

        /// <summary>The reference minion: slow, fragile, and a body in the way. Raise Dead's zombie.</summary>
        private static MinionDefinition Zombie() => new MinionDefinition
        {
            Id = "zombie",
            DisplayName = "Zombie",
            Health = 40f,
            MoveSpeed = 3.6f,
            Radius = 0.4f,
            BodyHeight = 1.7f,
            BodyWidth = 0.7f,
            BodyColor = new Color(0.42f, 0.52f, 0.38f),
            EyeColor = new Color(0.8f, 1f, 0.5f),
            FollowDistance = 3f,
            EngageRange = 12f,
            PreferredRange = 1.4f,
            Persistent = true,
            Attacks =
            {
                new AttackDefinition
                {
                    Name = "Claw", DamageType = DamageType.Kinetic, Reach = AttackReach.Melee,
                    Tint = new Color(0.6f, 0.8f, 0.45f),
                    MinRange = 0f, MaxRange = 2.2f, Cooldown = 1.4f, InitialDelay = 0.3f,
                    RequiresLineOfSight = false,
                    Sequence =
                    {
                        new WaitEffect { Seconds = 0.25f },
                        new AimAtTargetEffect { Flatten = true },
                        new OriginFromCasterEffect(),
                        new SelectConeEffect { Range = 2.4f, HalfAngle = 60f, RequireLineOfSight = false },
                        new DealDamageEffect { Amount = 9f }
                    }
                }
            }
        };
    }
}
