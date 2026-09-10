#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Writes the built-in loadouts out as assets so they can be edited in the Inspector.
    /// Doing it through the menu rather than by hand avoids the obvious trap of creating them
    /// outside a Resources folder, where they would never be found.
    /// </summary>
    public static class LoadoutTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Loadouts";

        [MenuItem("Gunspire/Create Starting Loadout Assets")]
        public static void CreateLoadoutAssets()
        {
            // Its own folder, alongside Weapons and Spells. Resources.LoadAll searches
            // recursively, so nesting costs nothing and keeps the root readable.
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Loadouts");

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
                ? "Created " + created + " loadout asset(s) in " + FolderPath +
                  ". Edit them in the Inspector; ids matching a built-in replace it."
                : "All loadout assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        /// <summary>
        /// Prints the loadouts side by side. They are meant to be balanced against each other
        /// - the same 25 stat points and guns tuned to roughly the same damage - and that is
        /// only checkable by comparing them, which reading three object initializers is not.
        /// </summary>
        [MenuItem("Gunspire/Log Loadout Table")]
        public static void LogLoadoutTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id            name          STR INT AGI VIT LCK  pts  gun            move    spell");

            IReadOnlyList<LoadoutDefinition> all = LoadoutLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                LoadoutDefinition l = all[i];
                text.AppendLine(string.Format(
                    "{0,-13} {1,-13} {2,3} {3,3} {4,3} {5,3} {6,3} {7,4}  {8,-14} {9,-7} {10}",
                    l.Id, l.DisplayName, l.Strength, l.Intellect, l.Agility, l.Vitality, l.Luck,
                    l.TotalStatPoints, l.WeaponId, l.MovementAbilityId, l.SpellId));
            }

            // Ids are plain strings, so a typo in the Inspector is a silent fallback to the
            // first gun or spell in the roster. Say so here rather than at runtime.
            text.AppendLine();
            for (int i = 0; i < all.Count; i++)
            {
                LoadoutDefinition l = all[i];
                if (WeaponLibrary.Peek(l.WeaponId) == null)
                    text.AppendLine("PROBLEM: \"" + l.Id + "\" wants unknown gun \"" + l.WeaponId + "\"");

                // An empty spell id means a spell-less start, which is now the norm: the first
                // floor's guaranteed reward is what fills the slot.
                if (!string.IsNullOrEmpty(l.SpellId) && SpellLibrary.Get(l.SpellId) == null)
                    text.AppendLine("PROBLEM: \"" + l.Id + "\" wants unknown spell \"" + l.SpellId + "\"");

                if (SpellLibrary.Get(l.MovementAbilityId) == null)
                    text.AppendLine("PROBLEM: \"" + l.Id + "\" wants unknown movement \""
                                    + l.MovementAbilityId + "\"");
            }

            // The real invariant is that the classes agree with each other, not that they hit
            // any particular number - the budget is a design choice and can move.
            if (all.Count > 1)
            {
                int budget = all[0].TotalStatPoints;
                for (int i = 1; i < all.Count; i++)
                {
                    if (all[i].TotalStatPoints == budget) continue;

                    text.AppendLine("PROBLEM: \"" + all[i].Id + "\" has " + all[i].TotalStatPoints
                                    + " stat points but \"" + all[0].Id + "\" has " + budget
                                    + "; one class opens ahead of the other");
                }
            }

            Debug.Log(text.ToString());
        }

        [MenuItem("Gunspire/Log Valid Ids")]
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
