using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Assembles an enemy GameObject from an <see cref="EnemyDefinition"/>. The roster itself
    /// lives in <see cref="EnemyLibrary"/>; this only knows how to turn one entry into
    /// primitives, components and attack chains.
    ///
    /// Floor scaling is applied here as multipliers rather than baked into authored damage: the
    /// health multiplier lands on the sheet and the damage multiplier is handed to each attack,
    /// which passes it through the ability context. That is what lets an asset author say what
    /// an attack is worth without knowing the floor formula.
    /// </summary>
    public static class EnemyFactory
    {
        public static float HealthScale(int floor) => 1f + (floor - 1) * 0.28f;
        public static float DamageScale(int floor) => 1f + (floor - 1) * 0.16f;

        public static EnemyController Spawn(string id, Vector3 position, int floor, bool elite = false)
        {
            EnemyDefinition def = EnemyLibrary.Get(id);
            if (def == null)
            {
                Debug.LogWarning("No enemy definition with id \"" + id + "\"; spawning nothing.");
                return null;
            }
            return Spawn(def, position, floor, elite);
        }

        public static EnemyController Spawn(EnemyDefinition def, Vector3 position, int floor, bool elite = false)
        {
            if (def == null) return null;

            // A flier is placed at its hover height rather than on the floor. Rising into
            // position at the start of a fight looks like a bug rather than an entrance.
            Vector3 spawnAt = def.Flying ? position + Vector3.up * def.HoverHeight : position;

            EnemyController enemy = CreateChassis(def, spawnAt, floor, elite);
            BuildLook(enemy, def, elite);
            ApplyAffinity(enemy, def);
            AttachAttacks(enemy, def, floor, elite);

            return enemy;
        }

        // ---------------------------------------------------------------- chassis

        private static EnemyController CreateChassis(EnemyDefinition def, Vector3 position, int floor, bool elite)
        {
            var root = new GameObject(def.DisplayName);
            root.transform.position = position + Vector3.up * 0.1f;
            root.layer = Layers.Enemy;

            var controller = root.AddComponent<CharacterController>();
            controller.height = def.BodyHeight;
            controller.radius = def.Radius;
            controller.center = new Vector3(0f, def.BodyHeight * 0.5f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 55f;
            controller.skinWidth = 0.03f;

            var sheet = root.AddComponent<CharacterSheet>();
            float health = def.Health * HealthScale(floor) * (elite ? def.EliteHealthMultiplier : 1f);
            sheet.SetBaseOverride(Attr.MaxHealth, health);
            sheet.SetBaseOverride(Attr.MoveSpeed, def.MoveSpeed * (elite ? def.EliteSpeedMultiplier : 1f));
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
            visuals.Configure(def.BodyColor);

            var enemy = root.AddComponent<EnemyController>();
            enemy.DisplayName = elite ? "Elite " + def.DisplayName : def.DisplayName;
            enemy.PreferredRange = def.PreferredRange;
            enemy.MinComfortRange = def.MinComfortRange;
            enemy.StrafeInterval = def.StrafeInterval;
            enemy.TurnSpeed = def.TurnSpeed;
            enemy.Flying = def.Flying;
            enemy.HoverHeight = def.HoverHeight;
            enemy.EyeHeight = def.ResolvedEyeHeight;

            return enemy;
        }

        private static void BuildLook(EnemyController enemy, EnemyDefinition def, bool elite)
        {
            bool crowned = elite || def.Crowned;

            if (def.Shape == BodyShape.Eyeball) BuildEyeball(enemy, def, crowned);
            else BuildBody(enemy, def, crowned);

            Layers.SetRecursively(enemy.gameObject, Layers.Enemy);
        }

        private static void ApplyAffinity(EnemyController enemy, EnemyDefinition def)
        {
            CharacterSheet sheet = enemy.Sheet != null ? enemy.Sheet : enemy.GetComponent<CharacterSheet>();
            if (sheet == null) return;

            if (def.ResistsEverything)
            {
                for (int i = 0; i < DamageTypes.Elemental.Length; i++)
                    sheet.SetBaseResistance(DamageTypes.Elemental[i], def.BroadResistance);
                return;
            }

            // Strong against its own school, soft against the one that counters it. This is
            // what makes carrying a second damage school worth doing.
            if (def.Resistance != 0f) sheet.SetBaseResistance(def.Resists, def.Resistance);
            if (def.Vulnerability != 0f) sheet.SetBaseResistance(def.WeakTo, -def.Vulnerability);
        }

        private static void AttachAttacks(EnemyController enemy, EnemyDefinition def, int floor, bool elite)
        {
            float damage = DamageScale(floor) * (elite ? def.EliteDamageMultiplier : 1f);
            float cooldown = elite ? def.EliteCooldownMultiplier : 1f;

            for (int i = 0; i < def.Attacks.Count; i++)
            {
                AttackDefinition attack = def.Attacks[i];
                if (attack == null) continue;

                AbilityAttack added = AbilityAttack.Add(enemy.gameObject, attack);
                added.DamageMultiplier = damage;
                added.Cooldown *= cooldown;
            }
        }

        // ---------------------------------------------------------------- shapes

        /// <summary>A body with an obvious front, so the player can read where it is looking.</summary>
        private static void BuildBody(EnemyController enemy, EnemyDefinition def, bool crowned)
        {
            Transform root = enemy.transform;
            float height = def.BodyHeight;
            float width = def.BodyWidth;

            Material bodyMaterial = MaterialLibrary.Lit(def.BodyColor, 0.2f);
            Material eyeMaterial = MaterialLibrary.Emissive(def.EyeColor, 3f);

            Build.Cube(root, "Torso", new Vector3(0f, height * 0.55f, 0f),
                new Vector3(width, height * 0.8f, width * 0.75f), bodyMaterial, collider: false);

            Build.Cube(root, "Head", new Vector3(0f, height * 1.02f, 0f),
                new Vector3(width * 0.62f, width * 0.62f, width * 0.62f), bodyMaterial, collider: false);

            Build.Cube(root, "Eye", new Vector3(0f, height * 1.02f, width * 0.34f),
                new Vector3(width * 0.42f, width * 0.13f, 0.06f), eyeMaterial, collider: false);

            if (crowned)
            {
                Build.Cube(root, "Crown", new Vector3(0f, height * 1.28f, 0f),
                    new Vector3(width * 0.3f, width * 0.5f, width * 0.3f),
                    MaterialLibrary.Emissive(new Color(1f, 0.85f, 0.3f), 2.5f), collider: false);
            }

            var muzzle = Build.Empty(root, "Muzzle", new Vector3(0f, height * 0.85f, width * 0.55f));
            enemy.Muzzle = muzzle.transform;
        }

        /// <summary>
        /// A floating eyeball. No torso, no legs - a sclera, an iris that faces wherever it is
        /// looking, and trailing nerve cords for a silhouette. The muzzle sits in the pupil so
        /// the laser leaves the eye rather than the middle of the body.
        /// </summary>
        private static void BuildEyeball(EnemyController enemy, EnemyDefinition def, bool crowned)
        {
            Transform root = enemy.transform;
            float diameter = def.BodyWidth;
            float centre = def.ResolvedEyeHeight;
            float front = diameter * 0.42f;

            Material scleraMaterial = MaterialLibrary.Lit(def.BodyColor, 0.25f, 0.15f);
            Material irisMaterial = MaterialLibrary.Emissive(def.EyeColor, 3.5f);
            Material cordMaterial = MaterialLibrary.Lit(Color.Lerp(def.BodyColor, Color.black, 0.55f), 0.3f);

            Build.Sphere(root, "Sclera", new Vector3(0f, centre, 0f), diameter, scleraMaterial, collider: false);

            // Flattened against the eye so it reads as a disc on a sphere, not a second ball.
            GameObject irisBall = Build.Sphere(root, "Iris", new Vector3(0f, centre, front),
                diameter * 0.5f, irisMaterial, collider: false);
            irisBall.transform.localScale = new Vector3(diameter * 0.5f, diameter * 0.5f, diameter * 0.22f);

            Build.Sphere(root, "Pupil", new Vector3(0f, centre, front + diameter * 0.06f),
                diameter * 0.22f, MaterialLibrary.Lit(new Color(0.04f, 0.03f, 0.06f), 0.1f), collider: false);

            // Nerve cords trailing behind. Purely silhouette - they give the thing an
            // orientation from behind, where the iris cannot be seen.
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle) * diameter * 0.26f,
                    centre + Mathf.Sin(angle) * diameter * 0.26f, -diameter * 0.48f);

                GameObject cord = Build.Cube(root, "Cord" + i, offset,
                    new Vector3(0.05f, 0.05f, diameter * 0.85f), cordMaterial, collider: false);
                cord.transform.localRotation = Quaternion.Euler(Mathf.Cos(angle) * 22f, Mathf.Sin(angle) * 22f, 0f);
            }

            if (crowned)
            {
                Build.Sphere(root, "Halo", new Vector3(0f, centre + diameter * 0.62f, 0f),
                    diameter * 0.26f, MaterialLibrary.Emissive(new Color(1f, 0.85f, 0.3f), 2.5f), collider: false);
            }

            var muzzle = Build.Empty(root, "Muzzle", new Vector3(0f, centre, front + diameter * 0.14f));
            enemy.Muzzle = muzzle.transform;
        }
    }
}
