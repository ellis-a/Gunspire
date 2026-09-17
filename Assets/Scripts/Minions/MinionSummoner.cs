using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Builds walking minions from their definitions, and puts the run's kept minions back around the
    /// player when a floor begins.
    /// </summary>
    public static class MinionSummoner
    {
        public static MinionController Spawn(string id, Vector3 position)
        {
            MinionDefinition def = MinionLibrary.Get(id);
            if (def == null)
            {
                Debug.LogWarning("No minion definition with id \"" + id + "\"; summoning nothing.");
                return null;
            }
            return Spawn(def, position);
        }

        public static MinionController Spawn(MinionDefinition def, Vector3 position)
        {
            if (def == null) return null;

            var root = new GameObject("Minion_" + def.Id);
            root.transform.position = position + Vector3.up * 0.1f;
            root.layer = Layers.Minion;

            var controller = root.AddComponent<CharacterController>();
            controller.height = def.BodyHeight;
            controller.radius = def.Radius;
            controller.center = new Vector3(0f, def.BodyHeight * 0.5f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 55f;
            controller.skinWidth = 0.03f;

            // Authored numbers, like enemies and familiars: the player's stat-derived multipliers must
            // not reach a minion's damage a second time.
            var sheet = root.AddComponent<CharacterSheet>();
            sheet.SetBaseOverride(Attr.MaxHealth, def.Health);
            sheet.SetBaseOverride(Attr.MoveSpeed, def.MoveSpeed);
            sheet.SetBaseOverride(Attr.HealthRegen, 0f);
            sheet.SetBaseOverride(Attr.CritChance, 0f);
            sheet.SetBaseOverride(Attr.GunDamage, 1f);
            sheet.SetBaseOverride(Attr.SpellPower, 1f);

            root.AddComponent<StatusController>();

            var health = root.AddComponent<Health>();
            health.Team = Team.Player;

            // A minion that gets back up must keep its body when it goes down.
            health.DestroyOnDeath = def.ReviveSeconds <= 0f;
            health.ConfigureMaxHealth(def.Health);

            Transform muzzle = BuildLook(root.transform, def);

            var minion = root.AddComponent<MinionController>();
            minion.Muzzle = muzzle;

            for (int i = 0; i < def.Attacks.Count; i++)
                if (def.Attacks[i] != null) AbilityAttack.Add(root, def.Attacks[i]);

            Layers.SetRecursively(root, Layers.Minion);

            // Boons strengthen what is summoned from here on, before Initialise so the health it starts with is full.
            if (AllyBoosts.Active != null)
            {
                AllyBoosts.Active.ApplyTo(sheet, BeastMastery.IsCompanion(def.Id));
                health.ConfigureMaxHealth(sheet.Get(Attr.MaxHealth));
            }

            // After the attacks exist, since Initialise binds them to this minion.
            minion.Initialise(def);
            return minion;
        }

        /// <summary>
        /// Puts the run's kept minions back on arrival, placed on free ground around the player. Returns
        /// how many came back.
        /// </summary>
        public static int RespawnKept(RunState run, RoomRuntime room)
        {
            if (run == null || run.Player == null) return 0;

            NavField field = room != null ? room.GetComponent<NavField>() : NavField.Current;
            Vector3 around = run.Player.transform.position;
            int spawned = 0;

            foreach (MinionRoster.Kept kept in run.Minions.Entries)
            {
                for (int i = 0; i < kept.Count; i++)
                {
                    Vector3 spot;
                    if (field != null && field.IsBuilt) field.TryFindSpot(run.Rng, around, 4f, out spot);
                    else
                    {
                        Vector2 offset = Random.insideUnitCircle * 3f;
                        spot = around + new Vector3(offset.x, 0f, offset.y);
                    }

                    if (Spawn(kept.Id, spot) != null) spawned++;
                }
            }

            return spawned;
        }

        /// <summary>A hunched body with a face, so a horde reads as bodies rather than as crates.</summary>
        private static Transform BuildLook(Transform root, MinionDefinition def)
        {
            float height = def.BodyHeight;
            float width = def.BodyWidth;

            Material body = MaterialLibrary.Lit(def.BodyColor, 0.2f);
            Material eyes = MaterialLibrary.Emissive(def.EyeColor, 2.5f);

            Build.Cube(root, "Torso", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height * 0.72f, width * 0.7f), body, collider: false);
            Build.Cube(root, "Head", new Vector3(0f, height * 0.94f, width * 0.12f),
                new Vector3(width * 0.58f, width * 0.58f, width * 0.58f), body, collider: false);
            Build.Cube(root, "Eyes", new Vector3(0f, height * 0.96f, width * 0.42f),
                new Vector3(width * 0.4f, width * 0.1f, 0.05f), eyes, collider: false);

            var muzzle = Build.Empty(root, "Muzzle", new Vector3(0f, height * 0.7f, width * 0.5f));
            return muzzle.transform;
        }
    }
}
