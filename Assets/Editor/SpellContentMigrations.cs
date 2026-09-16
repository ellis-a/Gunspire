#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Blink was decided to pass through walls, landing on the furthest walkable floor rather than stopping at
    /// the first wall. Its asset still carries the old sweep. Keyed to the old sweep effect being in the chain,
    /// so once replaced there is nothing left to find.
    /// </summary>
    public class BlinkThroughWalls : ObjectMigration
    {
        public override string Name => "Blink passes through walls";

        public override void Migrate(bool apply, List<string> changes)
        {
            Spell builtIn = null;
            foreach (Spell spell in SpellLibrary.BuiltIn())
                if (spell.Id == "blink") builtIn = spell;
            if (builtIn == null) return;

            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                Spell def = asset.Definition;
                if (def == null || def.Id != builtIn.Id || !HasSweep(def)) continue;

                changes.Add(def.Id + ": the forward sweep becomes a walkable landing through walls");
                if (!apply) continue;

                def.OnCast = new List<AbilityEffect>(builtIn.OnCast);
                def.Description = builtIn.Description;
                EditorUtility.SetDirty(asset);
            }
        }

        private static bool HasSweep(Spell spell)
        {
            foreach (AbilityEffect effect in spell.OnCast)
                if (effect is SweepForwardEffect) return true;
            return false;
        }
    }

    /// <summary>
    /// Shock became uncapped stacks at a fixed 1% each, where it had been up to three stacks at 8-12% each. Every
    /// shock an asset applies is multiplied up to match: twelve stacks for each old one, and twelve more per level
    /// where it grew with level. Keyed to the old per-stack strength, which only an unmigrated shock carries; a
    /// migrated one is set to the new 1%.
    ///
    /// Walks every serialised field rather than naming the effects, because shock sits in several shapes: a status
    /// payload's fields, and status lists on bolts, bullets, stance modes and weapons.
    /// </summary>
    public class ShockStacks : ObjectMigration
    {
        public override string Name => "Shock is uncapped stacks at 1% each";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                // Rapid sources apply fewer: the storm form's bullets and Hailstorm's ticks.
                string id = asset.Definition != null ? asset.Definition.Id : null;
                int perOldStack = id == "elemental_form" || id == "hailstorm" ? 6 : ShockStatus.SpellStacks;
                MigrateAsset(asset, apply, changes, perOldStack);
            }

            foreach (WeaponAsset asset in AssetsOf<WeaponAsset>.All()) MigrateAsset(asset, apply, changes, ShockStatus.SpellStacks);
            foreach (BoonAsset asset in AssetsOf<BoonAsset>.All()) MigrateAsset(asset, apply, changes, ShockStatus.SpellStacks);
            foreach (EnemyAsset asset in AssetsOf<EnemyAsset>.All()) MigrateAsset(asset, apply, changes, ShockStatus.SpellStacks);
            foreach (FamiliarAsset asset in AssetsOf<FamiliarAsset>.All()) MigrateAsset(asset, apply, changes, ShockStatus.SpellStacks);
        }

        private static void MigrateAsset(UnityEngine.Object asset, bool apply, List<string> changes, int perOldStack)
        {
            var serialized = new SerializedObject(asset);

            var magnitudes = new List<string>();
            SerializedProperty cursor = serialized.GetIterator();
            while (cursor.NextVisible(true))
                if (cursor.name == "Magnitude" && cursor.propertyType == SerializedPropertyType.Float)
                    magnitudes.Add(cursor.propertyPath);

            bool changed = false;
            foreach (string path in magnitudes)
            {
                int split = path.LastIndexOf('.');
                if (split < 0) continue;
                string parent = path.Substring(0, split);

                SerializedProperty id = serialized.FindProperty(parent + ".Id") ?? serialized.FindProperty(parent + ".Status");
                if (id == null || id.intValue != (int)StatusId.Shock) continue;

                SerializedProperty magnitude = serialized.FindProperty(path);
                if (magnitude.floatValue <= ShockStatus.DamagePerStack * 2f) continue;

                SerializedProperty stacks = serialized.FindProperty(parent + ".Stacks");
                SerializedProperty perLevel = serialized.FindProperty(parent + ".StacksPerLevel");
                int oldStacks = stacks != null ? Mathf.Max(1, stacks.intValue) : 1;

                changes.Add(asset.name + ": shock at " + parent + " goes from " + oldStacks + " stack(s) at "
                            + Mathf.RoundToInt(magnitude.floatValue * 100f) + "% to " + oldStacks * perOldStack + " at 1%");
                if (!apply) continue;

                if (stacks != null) stacks.intValue = oldStacks * perOldStack;
                if (perLevel != null && perLevel.floatValue > 0f) perLevel.floatValue *= perOldStack;
                magnitude.floatValue = ShockStatus.DamagePerStack;
                changed = true;
            }

            if (!changed) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>Spider Legs took its designed name, Spider Gravity, when it moved to Bestial. Keyed to the old name.</summary>
    public class SpiderGravityName : ObjectMigration
    {
        public override string Name => "Spider Legs renamed Spider Gravity";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                Spell def = asset.Definition;
                if (def == null || def.Id != "spider_legs" || def.DisplayName != "Spider Legs") continue;

                changes.Add(def.Id + ": display name becomes Spider Gravity");
                if (!apply) continue;

                def.DisplayName = "Spider Gravity";
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
#endif
