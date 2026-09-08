#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Writes the built-in boons out as assets so they can be tuned in the Inspector without a
    /// recompile. As with the other rosters, the menu is the way to do it: an asset created
    /// outside a Resources folder is never found and would silently do nothing.
    /// </summary>
    public static class BoonTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Boons";

        [MenuItem("Gunspire/Create Boon Assets")]
        public static void CreateBoonAssets()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Boons");

            List<Boon> builtIn = BoonLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                Boon boon = builtIn[i];
                string path = FolderPath + "/Boon_" + boon.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<BoonAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<BoonAsset>();
                asset.Boon = boon.Clone();

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BoonLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " boon asset(s) in " + FolderPath +
                  ". Edit them in the Inspector; ids matching a built-in replace it."
                : "All boon assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        /// <summary>
        /// Prints the pool with what each boon actually does, derived from its effect chain
        /// rather than from its written description. Those two drifting apart is the failure
        /// this is here to catch - the description is what the player reads, and nothing else
        /// in the game ever checks it against the effects.
        /// </summary>
        [MenuItem("Gunspire/Log Boon Table")]
        public static void LogBoonTable()
        {
            var text = new System.Text.StringBuilder();
            var perRarity = new Dictionary<Rarity, int>();

            text.AppendLine("id                     rarity     max  effects");

            IReadOnlyList<Boon> all = BoonLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                Boon b = all[i];

                perRarity.TryGetValue(b.Rarity, out int count);
                perRarity[b.Rarity] = count + 1;

                string gate = b.Requirement != null ? "  [" + b.Requirement.Describe() + "]" : string.Empty;
                text.AppendLine(string.Format("{0,-22} {1,-10} {2,3}  {3}{4}",
                    b.Id, b.Rarity, b.MaxLevel, b.EffectSummary(), gate));
            }

            text.AppendLine();
            for (int i = 0; i < Rarities.All.Length; i++)
            {
                Rarity rarity = Rarities.All[i];
                perRarity.TryGetValue(rarity, out int count);
                text.AppendLine(string.Format("{0,-10} {1,3} boons", rarity, count));
            }

            text.AppendLine();
            text.AppendLine("The effects column is generated from each boon's effect chain. Where it "
                            + "disagrees with the boon's own description, the description is the thing "
                            + "that is wrong - the player reads that one.");

            Debug.Log(text.ToString());
        }
    }
}
#endif
