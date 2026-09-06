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
    /// Builds enemies from primitives and bolts the right attack modules on. Floor number
    /// scales health and damage so later floors bite harder without new archetypes.
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

            root.AddComponent<StatusController>();

            var healthComponent = root.AddComponent<Health>();
            healthComponent.Team = Team.Enemy;

            var visuals = root.AddComponent<EnemyVisuals>();
            visuals.Configure(color);

            var enemy = root.AddComponent<EnemyController>();
            enemy.DisplayName = elite ? "Elite " + name : name;

            return enemy;
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

        // ---------------------------------------------------------------- archetypes

        private static EnemyController BuildCultist(Vector3 position, int floor, bool elite)
        {
            EnemyController e = CreateBase("Cultist", position, 55f, 4.2f, 0.42f, 1.8f,
                Palette.EnemyRanged, floor, elite);
            e.PreferredRange = 13f;
            e.MinComfortRange = 8f;
            BuildBody(e, Palette.EnemyRanged, 1.8f, 0.8f, new Color(1f, 0.5f, 1f), elite);

            var volley = e.gameObject.AddComponent<ProjectileVolleyAttack>();
            volley.MinRange = 0f;
            volley.MaxRange = 26f;
            volley.Cooldown = 2.6f;
            volley.Damage = 9f * DamageScale(floor);
            volley.DamageType = DamageType.Astral;
            volley.Tint = Palette.Arcane;
            volley.ProjectileCount = elite ? 5 : 3;
            volley.ProjectileSpeed = 20f;
            volley.ArcSpreadDegrees = 12f;

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

            var lunge = e.gameObject.AddComponent<MeleeLungeAttack>();
            lunge.MinRange = 0f;
            lunge.MaxRange = 4.6f;
            lunge.Cooldown = 1.9f;
            lunge.Damage = 17f * DamageScale(floor);
            lunge.DamageType = DamageType.Normal;
            lunge.Tint = Palette.EnemyMelee;
            lunge.LungeSpeed = elite ? 20f : 16f;

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

            var beam = e.gameObject.AddComponent<BeamAttack>();
            beam.MinRange = 6f;
            beam.MaxRange = 38f;
            beam.Cooldown = 5.5f;
            beam.DamagePerTick = 5.5f * DamageScale(floor);
            beam.DamageType = DamageType.Astral;
            beam.Tint = Palette.Lightning;
            beam.BeamDuration = elite ? 2.0f : 1.4f;
            beam.SweepDegreesPerSecond = elite ? 34f : 26f;
            beam.Statuses = new List<StatusApplication> { StatusLibrary.Shock(3f) };

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

            var slam = e.gameObject.AddComponent<GroundSlamAttack>();
            slam.MinRange = 0f;
            slam.MaxRange = 24f;
            slam.Cooldown = 4.6f;
            slam.Damage = 22f * DamageScale(floor);
            slam.DamageType = DamageType.Fire;
            slam.Tint = Palette.Fire;
            slam.Impacts = elite ? 4 : 3;
            slam.Radius = 3.5f;
            slam.LeadDistance = 3f;
            slam.Statuses = new List<StatusApplication> { StatusLibrary.Burn(4f, 2, 4f) };

            SetAffinity(e, DamageType.Fire, DamageType.Frost);
            return e;
        }

        private static EnemyController BuildFrostcaller(Vector3 position, int floor, bool elite)
        {
            EnemyController e = CreateBase("Frostcaller", position, 68f, 4.6f, 0.45f, 1.9f,
                new Color(0.35f, 0.55f, 0.75f), floor, elite);
            e.PreferredRange = 12f;
            e.MinComfortRange = 7f;
            BuildBody(e, new Color(0.35f, 0.55f, 0.75f), 1.9f, 0.85f, Palette.Ice, elite);

            var shards = e.gameObject.AddComponent<ProjectileVolleyAttack>();
            shards.MinRange = 5f;
            shards.MaxRange = 28f;
            shards.Cooldown = 3.2f;
            shards.Damage = 8f * DamageScale(floor);
            shards.DamageType = DamageType.Frost;
            shards.Tint = Palette.Ice;
            shards.ProjectileCount = 4;
            shards.ProjectileSpeed = 24f;
            shards.ArcSpreadDegrees = 18f;
            shards.Statuses = new List<StatusApplication> { StatusLibrary.Chill(3f, 1) };

            var breath = e.gameObject.AddComponent<ConeBreathAttack>();
            breath.MinRange = 0f;
            breath.MaxRange = 10f;
            breath.Cooldown = 6.5f;
            breath.Priority = 1;
            breath.Damage = 16f * DamageScale(floor);
            breath.DamageType = DamageType.Frost;
            breath.Tint = Palette.Ice;
            breath.Statuses = new List<StatusApplication> { StatusLibrary.Chill(4f, 2) };

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

            var volley = e.gameObject.AddComponent<ProjectileVolleyAttack>();
            volley.MaxRange = 34f;
            volley.Cooldown = 3.4f;
            volley.Damage = 12f * DamageScale(floor);
            volley.DamageType = DamageType.Astral;
            volley.Tint = Palette.Arcane;
            volley.ProjectileCount = 7;
            volley.ArcSpreadDegrees = 40f;
            volley.ProjectileSpeed = 22f;

            var beam = e.gameObject.AddComponent<BeamAttack>();
            beam.MinRange = 5f;
            beam.MaxRange = 40f;
            beam.Cooldown = 7.5f;
            beam.Priority = 1;
            beam.DamagePerTick = 7f * DamageScale(floor);
            beam.DamageType = DamageType.Astral;
            beam.Tint = Palette.Lightning;
            beam.BeamDuration = 2.4f;
            beam.SweepDegreesPerSecond = 30f;

            var slam = e.gameObject.AddComponent<GroundSlamAttack>();
            slam.MaxRange = 28f;
            slam.Cooldown = 6.5f;
            slam.Priority = 1;
            slam.Damage = 26f * DamageScale(floor);
            slam.DamageType = DamageType.Fire;
            slam.Tint = Palette.Fire;
            slam.Impacts = 5;
            slam.Radius = 4.2f;
            slam.LeadDistance = 4f;
            slam.Statuses = new List<StatusApplication> { StatusLibrary.Burn(5f, 2, 5f) };

            var breath = e.gameObject.AddComponent<ConeBreathAttack>();
            breath.MaxRange = 13f;
            breath.Cooldown = 8f;
            breath.Priority = 2;
            breath.Damage = 30f * DamageScale(floor);
            breath.DamageType = DamageType.Nature;
            breath.Tint = Palette.Poison;
            breath.Range = 14f;
            breath.HalfAngle = 40f;
            breath.Statuses = new List<StatusApplication> { StatusLibrary.Blight(8f, 3, 4f) };

            // The boss shrugs off every school a little, and is soft to none of them.
            CharacterSheet bossSheet = e.GetComponent<CharacterSheet>();
            for (int i = 0; i < DamageTypes.Elemental.Length; i++)
                bossSheet.SetBaseResistance(DamageTypes.Elemental[i], 0.18f);

            return e;
        }
    }
}
