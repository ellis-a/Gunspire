using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Every kind of walking minion. Code only for now: nothing is tuned in the Inspector yet. Assets
    /// arrive with the first summoning spell, as the same hybrid every other roster uses.
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

        public static List<MinionDefinition> BuiltIn() => new List<MinionDefinition>
        {
            Zombie(),
            Jackalope(),
            Fox(),
            Wolf(),
            Bear()
        };

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
            Attacks = { Strike("Claw", 9f, 1.4f, 2.2f, new Color(0.6f, 0.8f, 0.45f)) }
        };

        // ---------------------------------------------------------------- the Bestial ladder

        // The mastery companion at each rank. The first spell is worth the most: jackalope to fox is a
        // large jump, wolf to bear a small one. None of them persists through the roster; the mastery
        // summons its own on every floor, and one that dies stays gone until the next. Every number is
        // a placeholder.

        private static MinionDefinition Jackalope() => Beast("jackalope", "Jackalope", 30f, 7.5f, 0.3f, 0.7f, 0.5f,
            new Color(0.72f, 0.6f, 0.45f), Strike("Antlers", 5f, 0.9f, 1.8f, new Color(0.9f, 0.8f, 0.6f)));

        private static MinionDefinition Fox() => Beast("fox", "Fox", 90f, 7f, 0.35f, 0.9f, 0.6f,
            new Color(0.85f, 0.42f, 0.18f), Strike("Bite", 15f, 0.9f, 2f, new Color(1f, 0.6f, 0.3f)));

        private static MinionDefinition Wolf() => Beast("wolf", "Wolf", 120f, 6.5f, 0.4f, 1.1f, 0.7f,
            new Color(0.45f, 0.45f, 0.5f), Strike("Maul", 19f, 1f, 2.2f, new Color(0.8f, 0.8f, 0.9f)));

        private static MinionDefinition Bear() => Beast("bear", "Bear", 140f, 5f, 0.55f, 1.6f, 1.1f,
            new Color(0.35f, 0.24f, 0.16f), Strike("Swipe", 22f, 1.2f, 2.6f, new Color(0.7f, 0.5f, 0.35f)));

        private static MinionDefinition Beast(string id, string name, float health, float speed, float radius,
            float height, float width, Color body, AttackDefinition attack) => new MinionDefinition
        {
            Id = id,
            DisplayName = name,
            Health = health,
            MoveSpeed = speed,
            Radius = radius,
            BodyHeight = height,
            BodyWidth = width,
            BodyColor = body,
            EyeColor = new Color(1f, 0.9f, 0.4f),
            FollowDistance = 2.5f,
            EngageRange = 14f,
            PreferredRange = 1.4f,
            Persistent = false,
            ReviveSeconds = 0f,
            Attacks = { attack }
        };

        private static AttackDefinition Strike(string name, float damage, float cooldown, float reach, Color tint)
            => new AttackDefinition
            {
                Name = name, DamageType = DamageType.Kinetic, Reach = AttackReach.Melee,
                Tint = tint,
                MinRange = 0f, MaxRange = reach, Cooldown = cooldown, InitialDelay = 0.3f,
                RequiresLineOfSight = false,
                Sequence =
                {
                    new WaitEffect { Seconds = 0.25f },
                    new AimAtTargetEffect { Flatten = true },
                    new OriginFromCasterEffect(),
                    new SelectConeEffect { Range = reach + 0.2f, HalfAngle = 60f, RequireLineOfSight = false },
                    new DealDamageEffect { Amount = damage }
                }
            };
    }
}
