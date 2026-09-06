#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Writes the built-in loadouts out as assets so they can be edited in the Inspector.
    /// Doing it through the menu rather than by hand avoids the obvious trap of creating them
    /// outside a Resources folder, where they would never be found.
    /// </summary>
    public static class LoadoutTools
    {
        private const string FolderPath = "Assets/Resources";

        [MenuItem("Wizard with a Gun/Create Starting Loadout Assets")]
        public static void CreateLoadoutAssets()
        {
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets", "Resources");

            List<LoadoutDefinition> builtIn = LoadoutLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                LoadoutDefinition def = builtIn[i];
                string path = FolderPath + "/Loadout_" + def.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<StartingLoadoutAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<StartingLoadoutAsset>();
                asset.Loadout = def.Clone();

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LoadoutLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " loadout asset(s) in " + FolderPath + "."
                : "All loadout assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        [MenuItem("Wizard with a Gun/Log Valid Ids")]
        public static void LogValidIds()
        {
            string guns = "";
            var weapons = WeaponLibrary.All;
            for (int i = 0; i < weapons.Count; i++)
                guns += (i > 0 ? ", " : "") + weapons[i].Id;

            string spells = "";
            var all = SpellLibrary.All;
            for (int i = 0; i < all.Count; i++)
                spells += (i > 0 ? ", " : "") + all[i].Id;

            string loadouts = "";
            var kits = LoadoutLibrary.All;
            for (int i = 0; i < kits.Count; i++)
                loadouts += (i > 0 ? ", " : "") + kits[i].Id;

            Debug.Log("Weapon ids: " + guns + "\nSpell ids: " + spells + "\nLoadout ids: " + loadouts);
        }
    }
}
#endif
