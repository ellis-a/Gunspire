#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Guns gained a class for the class boons. Existing assets deserialise it as Unassigned, so each
    /// takes its class from <see cref="WeaponLibrary.Classes"/> by id. Keyed to Unassigned, so a class
    /// changed by hand in the Inspector is left alone.
    /// </summary>
    public class WeaponClassesFromTable : ObjectMigration
    {
        public override string Name => "Guns take their class";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (WeaponAsset asset in AssetsOf<WeaponAsset>.All())
            {
                WeaponDefinition def = asset.Definition;
                if (def == null || def.Class != WeaponClass.Unassigned) continue;

                WeaponClass found = WeaponLibrary.ClassOf(def.Id);
                if (found == WeaponClass.Unassigned) continue;

                changes.Add(def.Id + ": class " + found);
                if (!apply) continue;

                def.Class = found;
                EditorUtility.SetDirty(asset);
            }
        }
    }

    /// <summary>
    /// Guns gained a draw time. Existing assets deserialise it as unset (negative), so each takes its
    /// class default from <see cref="WeaponLibrary.DrawTimeFor"/>. Keyed to unset, so a time tuned by hand
    /// is left alone. Runs after the class migration, since the default depends on the class.
    /// </summary>
    public class WeaponDrawTimes : ObjectMigration
    {
        public override string Name => "Guns take a draw time";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (WeaponAsset asset in AssetsOf<WeaponAsset>.All())
            {
                WeaponDefinition def = asset.Definition;
                if (def == null || def.DrawTime >= 0f) continue;

                WeaponClass cls = def.Class != WeaponClass.Unassigned ? def.Class : WeaponLibrary.ClassOf(def.Id);
                float seconds = WeaponLibrary.DrawTimeFor(cls);

                changes.Add(def.Id + ": draw time " + seconds + "s");
                if (!apply) continue;

                def.DrawTime = seconds;
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
#endif
