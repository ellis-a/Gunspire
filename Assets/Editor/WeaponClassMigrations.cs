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
}
#endif
