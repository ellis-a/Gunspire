#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Writes the built-in guns out as assets so they can be tuned in the Inspector without a
    /// recompile. As with loadouts, the menu is the way to do it: an asset created anywhere
    /// outside a Resources folder is never found, and would silently do nothing.
    /// </summary>
    public static class WeaponTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Weapons";

        [MenuItem("Wizard with a Gun/Create Weapon Assets")]
        public static void CreateWeaponAssets()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Weapons");

            List<WeaponDefinition> builtIn = WeaponLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                WeaponDefinition def = builtIn[i];
                string path = FolderPath + "/Weapon_" + def.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<WeaponAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<WeaponAsset>();
                asset.Definition = def.Clone();

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WeaponLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " weapon asset(s) in " + FolderPath +
                  ". Edit them in the Inspector; ids matching a built-in replace it."
                : "All weapon assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        /// <summary>
        /// Prints the roster as a table. Balancing a gun means comparing it with the others,
        /// which is hard to do reading object initializers one at a time.
        /// </summary>
        [MenuItem("Wizard with a Gun/Log Weapon Balance Table")]
        public static void LogBalanceTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id                 rarity      school   dps   dmg splash  rpm  mag pierce  shape");

            IReadOnlyList<WeaponDefinition> all = WeaponLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                WeaponDefinition w = all[i];

                // A launcher puts most of its damage in the blast, so leaving splash out of
                // this understates it badly enough to mislead a balance pass.
                float perTrigger = w.Damage * w.RoundsPerTrigger + w.SplashDamage;
                float dps = perTrigger * (w.RoundsPerMinute / 60f);

                string shape = w.Mode == FireMode.Burst
                    ? w.BurstCount + "-burst"
                    : (w.Delivery == DeliveryKind.Hitscan ? "hitscan" : "projectile");

                text.AppendLine(string.Format(
                    "{0,-18} {1,-11} {2,-8} {3,5:0} {4,5:0.#} {5,6:0} {6,4:0} {7,4} {8,6} {9}",
                    w.Id, w.Rarity, DamageTypes.Name(w.DamageType), dps, w.Damage, w.SplashDamage,
                    w.RoundsPerMinute, w.MagazineSize, w.MaxPierce, shape));
            }

            text.AppendLine();
            text.AppendLine("dps is sustained single-target, ignoring reloads. It counts splash at full "
                            + "value, so a launcher reads high against one target and higher against a "
                            + "group; pierce is not folded in at all.");

            Debug.Log(text.ToString());
        }
    }
}
#endif
