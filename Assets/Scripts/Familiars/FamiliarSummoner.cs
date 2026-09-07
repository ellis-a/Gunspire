using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Builds familiars and puts them next to the player.
    ///
    /// Summoning happens on entering a room rather than on a timer, which is what makes a
    /// familiar's death a real but bounded loss: you fight the rest of the room without it and
    /// get it back at the next door.
    /// </summary>
    public static class FamiliarSummoner
    {
        /// <summary>Clears whatever is out and summons the run's familiars fresh.</summary>
        public static void Resummon(RunState run)
        {
            FamiliarController.DespawnAll();
            if (run == null || run.Player == null) return;

            for (int i = 0; i < run.Familiars.Count; i++)
            {
                RunState.OwnedFamiliar owned = run.Familiars[i];
                Summon(owned.Id, owned.Level, run.Player);
            }
        }

        public static FamiliarController Summon(string id, int level, PlayerRig owner)
        {
            FamiliarDefinition def = FamiliarLibrary.Get(id);
            if (def == null)
            {
                Debug.LogWarning("No familiar definition with id \"" + id + "\".");
                return null;
            }
            if (owner == null) return null;

            ApplyLevel(def, level);
            return Construct(def, owner);
        }

        /// <summary>
        /// Later picks of the same boon deepen the familiar rather than adding another. The
        /// increments live on the granting effect, so this only has to read them off.
        /// </summary>
        private static void ApplyLevel(FamiliarDefinition def, int level)
        {
            int extra = Mathf.Max(0, level - 1);
            if (extra == 0) return;

            Boon boon = FindGrantingBoon(def.Id);
            float damageStep = 0.35f;
            float healthStep = 0.30f;

            if (boon != null)
            {
                for (int i = 0; i < boon.Effects.Count; i++)
                {
                    if (!(boon.Effects[i] is GrantFamiliarEffect grant)) continue;
                    if (grant.FamiliarId != def.Id) continue;

                    damageStep = grant.DamagePerLevel;
                    healthStep = grant.HealthPerLevel;
                    break;
                }
            }

            def.DamageMultiplier *= 1f + damageStep * extra;
            def.Health *= 1f + healthStep * extra;
        }

        private static Boon FindGrantingBoon(string familiarId)
        {
            for (int i = 0; i < BoonLibrary.All.Count; i++)
            {
                Boon boon = BoonLibrary.All[i];
                for (int e = 0; e < boon.Effects.Count; e++)
                    if (boon.Effects[e] is GrantFamiliarEffect grant && grant.FamiliarId == familiarId)
                        return boon;
            }
            return null;
        }

        private static FamiliarController Construct(FamiliarDefinition def, PlayerRig owner)
        {
            var root = new GameObject("Familiar_" + def.Id);
            root.layer = Layers.Familiar;
            root.transform.position = owner.transform.position
                                      + Vector3.up * def.HoverHeight
                                      + owner.transform.right * def.FollowDistance;

            // A sphere collider rather than a CharacterController: it needs to be hittable,
            // not to walk. The collision matrix already keeps it out of the player's way.
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = Mathf.Max(0.1f, def.Radius);

            var sheet = root.AddComponent<CharacterSheet>();
            sheet.SetBaseOverride(Attr.MaxHealth, def.Health);
            sheet.SetBaseOverride(Attr.HealthRegen, 0f);
            sheet.SetBaseOverride(Attr.CritChance, 0f);

            // Familiar damage is authored directly, the same as enemy damage, so the player's
            // Strength and Intellect must not multiply it a second time through the aura.
            sheet.SetBaseOverride(Attr.GunDamage, 1f);
            sheet.SetBaseOverride(Attr.SpellPower, 1f);

            root.AddComponent<StatusController>();

            var health = root.AddComponent<Health>();
            health.Team = Team.Player;

            Transform muzzle = BuildLook(root.transform, def);

            var controller = root.AddComponent<FamiliarController>();
            controller.Muzzle = muzzle;

            for (int i = 0; i < def.Attacks.Count; i++)
            {
                if (def.Attacks[i] == null) continue;
                AbilityAttack.Add(root, def.Attacks[i]);
            }

            Layers.SetRecursively(root, Layers.Familiar);

            // After the attacks exist, since Initialise binds them to this owner.
            controller.Initialise(def, owner);
            return controller;
        }

        /// <summary>
        /// A small glowing body with a bright core, so it reads as a mote rather than a
        /// monster. Returns the muzzle, because the controller does not exist yet when this
        /// runs and wiring it here would silently attach to nothing.
        /// </summary>
        private static Transform BuildLook(Transform root, FamiliarDefinition def)
        {
            float d = def.BodyDiameter;

            Build.Sphere(root, "Body", Vector3.zero, d,
                MaterialLibrary.Transparent(new Color(def.BodyColor.r, def.BodyColor.g, def.BodyColor.b, 0.55f)),
                collider: false);

            Build.Sphere(root, "Core", Vector3.zero, d * 0.5f,
                MaterialLibrary.Emissive(def.EyeColor, 3f), collider: false);

            // Three motes circling the core. Purely so it does not read as a static ball.
            for (int i = 0; i < 3; i++)
            {
                float angle = (i / 3f) * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle) * d * 0.7f, 0f, Mathf.Sin(angle) * d * 0.7f);

                Build.Sphere(root, "Mote" + i, offset, d * 0.2f,
                    MaterialLibrary.Emissive(def.BodyColor, 2f), collider: false);
            }

            var muzzle = Build.Empty(root, "Muzzle", new Vector3(0f, 0f, d * 0.6f));
            return muzzle.transform;
        }
    }
}
