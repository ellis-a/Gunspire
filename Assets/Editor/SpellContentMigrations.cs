#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

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
