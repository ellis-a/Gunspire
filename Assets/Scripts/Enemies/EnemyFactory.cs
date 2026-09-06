using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    public enum EnemyKind
    {
        Cultist,      // ranged projectile volleys
        Hound,        // melee, must be kited
        Warden,       // telegraphed sweeping beam
        Sentinel,     // telegraphed ground areas
        Frostcaller,  // ice projectiles and a freezing cone
        TowerWarden   // floor boss, uses everything
    }

    /// <summary>
    /// Builds enemies from primitives and composes their attacks out of the same
    /// <see cref="AbilityEffect"/>s the player's spells use. Floor number scales health and
    /// damage so later floors bite harder without new archetypes.
    /// </summary>
    public static class EnemyFactory
    {
        public static readonly EnemyKind[] StandardRoster =
        {
            EnemyKind.Cultist, EnemyKind.Hound, EnemyKind.Warden,
            EnemyKind.Sentinel, EnemyKind.Frostcaller
        };

        public static float HealthScale(int floor) => 1f + (floor - 1) * 0.28f;
        public static float DamageScale(int floor) => 1f + (floor - 1) * 0.16f;

        public static EnemyController Spawn(EnemyKind kind, Vector3 position, int floor, bool elite = false)
        {
            switch (kind)
            {
                case EnemyKind.Hound: return BuildHound(position, floor, elite);
                case EnemyKind.Warden: return BuildWarden(position, floor, elite);
                case EnemyKind.Sentinel: return BuildSentinel(position, floor, elite);
                case EnemyKind.Frostcaller: return BuildFrostcaller(position, floor, elite);
                case EnemyKind.TowerWarden: return BuildBoss(position, floor);
                default: return BuildCultist(position, floor, elite);
            }
        }

        // ---------------------------------------------------------------- shared chassis

        private static EnemyController CreateBase(string name, Vector3 position, float health,
            float moveSpeed, float radius, float height, Color color, int floor, bool elite)
        {
            var root = new GameObject(name);
            root.transform.position = position + Vector3.up * 0.1f;
            root.layer = Layers.Enemy;

            var controller = root.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 55f;
            controller.skinWidth = 0.03f;

            var sheet = root.AddComponent<CharacterSheet>();
            float scaledHealth = health * HealthScale(floor) * (elite ? 2.6f : 1f);
            sheet.SetBaseOverride(Attr.MaxHealth, scaledHealth);
            sheet.SetBaseOverride(Attr.MoveSpeed, moveSpeed * (elite ? 1.08f : 1f));
            sheet.SetBaseOverride(Attr.HealthRegen, 0f);
            sheet.SetBaseOverride(Attr.CritChance, 0f);

            // Enemy attack damage is authored directly, so neutralise the stat-derived
            // multipliers the player gets from Strength and Intellect.
            sheet.SetBaseOverride(Attr.GunDamage, 1f);
            sheet.SetBaseOverride(Attr.SpellPower, 1f);

            root.AddComponent<StatusController>();

            var healthComponent = root.AddComponent<Health>();
            healthComponent.Team = Team.Enemy;

            var visuals = root.AddComponent<EnemyVisuals>();
            visuals.Configure(color);

            var enemy = root.AddComponent<EnemyController>();
            enemy.DisplayName = elite ? "Elite " + name : name;

            return enemy;
        }

        /// <summary>A body with an obvious front, so the player can read where it is looking.</summary>
        private static void BuildBody(EnemyController enemy, Color color, float height, float width,
            Color eyeColor, bool elite)
        {
            Transform root = enemy.transform;
            Material bodyMaterial = MaterialLibrary.Lit(color, 0.2f);
            Material eyeMaterial = MaterialLibrary.Emissive(eyeColor, 3f);

            Build.Cube(root, "Torso", new Vector3(0f, height * 0.55f, 0f),
                new Vector3(width, height * 0.8f, width * 0.75f), bodyMaterial, collider: false);

            Build.Cube(root, "Head", new Vector3(0f, height * 1.02f, 0f),
                new Vector3(width * 0.62f, width * 0.62f, width * 0.62f), bodyMaterial, collider: false);

            Build.Cube(root, "Eye", new Vector3(0f, height * 1.02f, width * 0.34f),
                new Vector3(width * 0.42f, width * 0.13f, 0.06f), eyeMaterial, collider: false);

            if (elite)
            {
                Build.Cube(root, "Crown", new Vector3(0f, height * 1.28f, 0f),
                    new Vector3(width * 0.3f, width * 0.5f, width * 0.3f),
                    MaterialLibrary.Emissive(new Color(1f, 0.85f, 0.3f), 2.5f), collider: false);
            }

            var muzzle = Build.Empty(root, "Muzzle", new Vector3(0f, height * 0.85f, width * 0.55f));
            enemy.Muzzle = muzzle.transform;

            Layers.SetRecursively(enemy.gameObject, Layers.Enemy);
        }

        /// <summary>
        /// Gives an enemy an affinity: strong against its own school, soft against the one that
        /// counters it. This is what makes carrying a second damage school worth doing.
        /// </summary>
        private static void SetAffinity(EnemyController enemy, DamageType resists, DamageType weakTo,
            float resistance = 0.40f, float vulnerability = 0.30f)
        {
            CharacterSheet sheet = enemy.Sheet != null ? enemy.Sheet : enemy.GetComponent<CharacterSheet>();
            if (sheet == null) return;

            sheet.SetBaseResistance(resists, resistance);
            sheet.SetBaseResistance(weakTo, -vulnerability);
        }

        // ---------------------------------------------------------------- archetypes

        private static EnemyController BuildCultist(Vector3 position, int floor, bool elite)
        {
            EnemyController e = CreateBase("Cultist", position, 55f, 4.2f, 0.42f, 1.8f,
                Palette.EnemyRanged, floor, elite);
            e.PreferredRange = 13f;
            e.MinComfortRange = 8f;
            BuildBody(e, Palette.EnemyRanged, 1.8f, 0.8f, new Color(1f, 0.5f, 1f), elite);

            EnemyAttack.Add(e, "Arcane Volley", DamageType.Astral, Palette.Arcane,
                minRange: 0f, maxRange: 26f, cooldown: 2.6f, priority: 0,
                new TelegraphFlashEffect { Duration = 0.55f, Radius = 0.35f, Height = 1.35f },
                new WaitEffect { Seconds = 0.55f },
                new RepeatEffect
                {
                    Times = elite ? 5 : 3, Interval = 0.16f,
                    Body =
                    {
                        new AimAtTargetEffect(),
                        new SpawnProjectileEffect
                        {
                            Damage = 9f * DamageScale(floor), Speed = 20f, Radius = 0.24f,
                            Lifetime = 6f, SpreadDegrees = 1.5f
                        }
                    }
                },
                new WaitEffect { Seconds = 0.45f });

            SetAffinity(e, DamageType.Astral, DamageType.Nature);
            return e;
        }

        private static EnemyController BuildHound(Vector3 position, int floor, bool elite)
        {
            EnemyController e = CreateBase("Hound", position, 46f, 6.9f, 0.4f, 1.2f,
                Palette.EnemyMelee, floor, elite);
            e.PreferredRange = 1.6f;
            e.MinComfortRange = 0f;
            e.StrafeInterval = 2.6f;
            BuildBody(e, Palette.EnemyMelee, 1.2f, 0.75f, new Color(1f, 0.4f, 0.2f), elite);

            EnemyAttack.Add(e, "Lunge", DamageType.Normal, Palette.EnemyMelee,
                minRange: 0f, maxRange: 4.6f, cooldown: 1.9f, priority: 0,
                new TelegraphFlashEffect { Duration = 0.55f, Radius = 0.55f, Height = 1.1f },
                new WaitEffect { Seconds = 0.55f },
                // Direction locks here, so strafing during the wind-up beats it.
                new AimAtTargetEffect { Flatten = true },
                new ImpulseSelfEffect { Speed = elite ? 20f : 16f },
                new RepeatEffect
                {
                    Times = 6, Interval = 0.04f,
                    Body =
                    {
                        // The swing follows the hound, but not the player.
                        new OriginFromCasterEffect(),
                        new SelectConeEffect { Range = 3.4f, HalfAngle = 60f, RequireLineOfSight = false },
                        new DealDamageEffect
                        {
                            Amount = 17f * DamageScale(floor), Knockback = 6f, OnlyOncePerCast = true
                        }
                    }
                },
                new WaitEffect { Seconds = 0.6f });

            SetAffinity(e, DamageType.Normal, DamageType.Frost);
            return e;
        }

        private static EnemyController BuildWarden(Vector3 position, int floor, bool elite)
        {
            EnemyController e = CreateBase("Warden", position, 95f, 3.1f, 0.5f, 2.2f,
                Palette.EnemyBeam, floor, elite);
            e.PreferredRange = 18f;
            e.MinComfortRange = 12f;
            BuildBody(e, Palette.EnemyBeam, 2.2f, 0.95f, new Color(0.5f, 0.9f, 1f), elite);

            EnemyAttack.Add(e, "Sweeping Beam", DamageType.Astral, Palette.Lightning,
                minRange: 6f, maxRange: 38f, cooldown: 5.5f, priority: 0,
                new AimAtTargetEffect { Flatten = true },
                new TelegraphLineEffect { Length = 40f, Width = 0.33f, Duration = 1.1f },
                new WaitEffect { Seconds = 1.1f },
                new StatusPayloadEffect
                {
                    Status = StatusId.Shock, Duration = 3f, Stacks = 1, Magnitude = 0.12f
                },
                new BeamEffect
                {
                    Duration = elite ? 2.0f : 1.4f,
                    DamagePerTick = 5.5f * DamageScale(floor),
                    SweepDegreesPerSecond = elite ? 34f : 26f
                },
                new WaitEffect { Seconds = 0.9f });

            SetAffinity(e, DamageType.Astral, DamageType.Shadow);
            return e;
        }

        private static EnemyController BuildSentinel(Vector3 position, int floor, bool elite)
        {
            EnemyController e = CreateBase("Sentinel", position, 84f, 3.6f, 0.55f, 2.0f,
                Palette.EnemyCaster, floor, elite);
            e.PreferredRange = 11f;
            e.MinComfortRange = 6f;
            BuildBody(e, Palette.EnemyCaster, 2.0f, 1.05f, new Color(1f, 0.8f, 0.3f), elite);

            EnemyAttack.Add(e, "Cinder Slam", DamageType.Fire, Palette.Fire,
                minRange: 0f, maxRange: 24f, cooldown: 4.6f, priority: 0,
                new StatusPayloadEffect
                {
                    Status = StatusId.Burn, Duration = 4f, Stacks = 2, Magnitude = 4f
                },
                new RepeatEffect
                {
                    Times = elite ? 4 : 3, Interval = 0.45f,
                    Body =
                    {
                        // Each circle lands where the player was, so standing still is fatal.
                        new TargetGroundPointEffect { LeadDistance = 3f },
                        new TelegraphCircleEffect { Radius = 3.5f, Duration = 0.9f, ScaleWithLevel = false },
                        new DelayedBlastEffect
                        {
                            Delay = 0.9f, Radius = 3.5f, Damage = 22f * DamageScale(floor),
                            Knockback = 7f, ScaleRadiusWithLevel = false
                        }
                    }
                },
                new WaitEffect { Seconds = 1.6f });

            SetAffinity(e, DamageType.Fire, DamageType.Frost);
            return e;
        }

        private static EnemyController BuildFrostcaller(Vector3 position, int floor, bool elite)
        {
            var body = new Color(0.35f, 0.55f, 0.75f);
            EnemyController e = CreateBase("Frostcaller", position, 68f, 4.6f, 0.45f, 1.9f,
                body, floor, elite);
            e.PreferredRange = 12f;
            e.MinComfortRange = 7f;
            BuildBody(e, body, 1.9f, 0.85f, Palette.Ice, elite);

            EnemyAttack.Add(e, "Ice Shards", DamageType.Frost, Palette.Ice,
                minRange: 5f, maxRange: 28f, cooldown: 3.2f, priority: 0,
                new TelegraphFlashEffect { Duration = 0.5f, Radius = 0.35f, Height = 1.35f },
                new WaitEffect { Seconds = 0.5f },
                new StatusPayloadEffect
                {
                    Status = StatusId.Chill, Duration = 3f, Stacks = 1, Magnitude = 0.11f
                },
                new RepeatEffect
                {
                    Times = 4, Interval = 0.14f,
                    Body =
                    {
                        new AimAtTargetEffect(),
                        new SpawnProjectileEffect
                        {
                            Damage = 8f * DamageScale(floor), Speed = 24f, Radius = 0.22f,
                            Lifetime = 6f, SpreadDegrees = 2f
                        }
                    }
                },
                new WaitEffect { Seconds = 0.45f });

            EnemyAttack.Add(e, "Frost Breath", DamageType.Frost, Palette.Ice,
                minRange: 0f, maxRange: 10f, cooldown: 6.5f, priority: 1,
                // Aim first so the warning cone shows exactly where the breath will go.
                new AimAtTargetEffect { Flatten = true },
                new TelegraphConeEffect { Range = 11f, HalfAngle = 32f, Duration = 0.85f },
                new WaitEffect { Seconds = 0.85f },
                new StatusPayloadEffect
                {
                    Status = StatusId.Chill, Duration = 4f, Stacks = 2, Magnitude = 0.11f
                },
                new SelectConeEffect { Range = 11f, HalfAngle = 32f, RequireLineOfSight = false },
                new DealDamageEffect { Amount = 16f * DamageScale(floor) },
                new VfxConeEffect { Range = 11f, HalfAngle = 32f, Alpha = 0.4f, Lifetime = 0.3f },
                new WaitEffect { Seconds = 0.8f });

            SetAffinity(e, DamageType.Frost, DamageType.Fire);
            return e;
        }

        private static EnemyController BuildBoss(Vector3 position, int floor)
        {
            EnemyController e = CreateBase("Tower Warden", position, 620f, 4.0f, 0.9f, 3.2f,
                Palette.EnemyBoss, floor, false);
            e.PreferredRange = 14f;
            e.MinComfortRange = 8f;
            e.StrafeInterval = 2.2f;
            BuildBody(e, Palette.EnemyBoss, 3.2f, 1.6f, new Color(1f, 0.4f, 0.6f), true);

            EnemyAttack.Add(e, "Arcane Fan", DamageType.Astral, Palette.Arcane,
                minRange: 0f, maxRange: 34f, cooldown: 3.4f, priority: 0,
                new TelegraphFlashEffect { Duration = 0.6f, Radius = 0.6f, Height = 2.4f },
                new WaitEffect { Seconds = 0.6f },
                new AimAtTargetEffect(),
                new SpawnProjectileEffect
                {
                    Damage = 12f * DamageScale(floor), Speed = 22f, Radius = 0.3f,
                    Lifetime = 6f, Count = 7, ArcSpreadDegrees = 40f
                },
                new WaitEffect { Seconds = 0.5f });

            EnemyAttack.Add(e, "Warden Beam", DamageType.Astral, Palette.Lightning,
                minRange: 5f, maxRange: 40f, cooldown: 7.5f, priority: 1,
                new AimAtTargetEffect { Flatten = true },
                new TelegraphLineEffect { Length = 40f, Width = 0.5f, Duration = 1.1f },
                new WaitEffect { Seconds = 1.1f },
                new BeamEffect
                {
                    Duration = 2.4f, Width = 0.8f,
                    DamagePerTick = 7f * DamageScale(floor), SweepDegreesPerSecond = 30f
                },
                new WaitEffect { Seconds = 0.9f });

            EnemyAttack.Add(e, "Cinder Rain", DamageType.Fire, Palette.Fire,
                minRange: 0f, maxRange: 28f, cooldown: 6.5f, priority: 1,
                new StatusPayloadEffect
                {
                    Status = StatusId.Burn, Duration = 5f, Stacks = 2, Magnitude = 5f
                },
                new RepeatEffect
                {
                    Times = 5, Interval = 0.4f,
                    Body =
                    {
                        new TargetGroundPointEffect { LeadDistance = 4f },
                        new TelegraphCircleEffect { Radius = 4.2f, Duration = 0.9f, ScaleWithLevel = false },
                        new DelayedBlastEffect
                        {
                            Delay = 0.9f, Radius = 4.2f, Damage = 26f * DamageScale(floor),
                            Knockback = 8f, ScaleRadiusWithLevel = false
                        }
                    }
                },
                new WaitEffect { Seconds = 1.4f });

            EnemyAttack.Add(e, "Withering Breath", DamageType.Nature, Palette.Poison,
                minRange: 0f, maxRange: 13f, cooldown: 8f, priority: 2,
                new AimAtTargetEffect { Flatten = true },
                new TelegraphConeEffect { Range = 14f, HalfAngle = 40f, Duration = 0.85f },
                new WaitEffect { Seconds = 0.85f },
                new StatusPayloadEffect
                {
                    Status = StatusId.Blight, Duration = 8f, Stacks = 3, Magnitude = 4f
                },
                new SelectConeEffect { Range = 14f, HalfAngle = 40f, RequireLineOfSight = false },
                new DealDamageEffect { Amount = 30f * DamageScale(floor) },
                new VfxConeEffect { Range = 14f, HalfAngle = 40f, Alpha = 0.4f, Lifetime = 0.3f },
                new WaitEffect { Seconds = 0.8f });

            // The boss shrugs off every school a little, and is soft to none of them.
            CharacterSheet bossSheet = e.GetComponent<CharacterSheet>();
            for (int i = 0; i < DamageTypes.Elemental.Length; i++)
                bossSheet.SetBaseResistance(DamageTypes.Elemental[i], 0.18f);

            return e;
        }
    }
}
