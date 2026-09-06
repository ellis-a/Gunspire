#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Creates the starting loadout asset in the one place <see cref="StartingLoadout"/> looks
    /// for it. Doing it through the menu rather than by hand avoids the obvious trap of
    /// creating the asset outside a Resources folder, where it would never be found.
    /// </summary>
    public static class LoadoutTools
    {
        private const string FolderPath = "Assets/Resources";
        private const string AssetPath = FolderPath + "/StartingLoadout.asset";

        [MenuItem("Wizard with a Gun/Create Starting Loadout Asset")]
        public static void CreateStartingLoadout()
        {
            var existing = AssetDatabase.LoadAssetAtPath<StartingLoadoutAsset>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("Starting loadout already exists at " + AssetPath + ". Selected it.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<StartingLoadoutAsset>();

            // Seed from the code defaults, so the asset starts as the game already plays.
            asset.Strength = StartingLoadout.DefaultBaseStat;
            asset.Intellect = StartingLoadout.DefaultBaseStat;
            asset.Agility = StartingLoadout.DefaultBaseStat;
            asset.Vitality = StartingLoadout.DefaultBaseStat;
            asset.Luck = StartingLoadout.DefaultBaseStat;
            asset.WeaponId = StartingLoadout.DefaultWeaponId;
            asset.SpellIdsBySlot = (string[])StartingLoadout.DefaultSpellIdsBySlot.Clone();

            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StartingLoadout.Reload();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("Created " + AssetPath + ". Edit it to change the starting kit.");
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

            Debug.Log("Weapon ids: " + guns + "\nSpell ids: " + spells);
        }
    }
}
#endif
