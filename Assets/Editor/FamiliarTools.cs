#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    public static class FamiliarTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Familiars";

        [MenuItem("Gunspire/Create Familiar Assets")]
        public static void CreateFamiliarAssets()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Familiars");

            List<FamiliarDefinition> builtIn = FamiliarLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                FamiliarDefinition def = builtIn[i];
                string path = FolderPath + "/Familiar_" + def.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<FamiliarAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<FamiliarAsset>();
                asset.Familiar = def.Clone();

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FamiliarLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " familiar asset(s) in " + FolderPath +
                  ". Edit them in the Inspector; ids matching a built-in replace it."
                : "All familiar assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        /// <summary>
        /// Prints the roster with the boon that grants each one.
        ///
        /// That pairing is the failure worth catching: nothing hands familiars out on its own,
        /// so a familiar with no granting boon is content that can never appear in a run, and
        /// a boon pointing at a missing familiar is a pick that does nothing.
        /// </summary>
        [MenuItem("Gunspire/Log Familiar Table")]
        public static void LogFamiliarTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id          name            granted by            stats");

            IReadOnlyList<FamiliarDefinition> all = FamiliarLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                FamiliarDefinition f = all[i];
                Boon granter = FindGranter(f.Id);

                text.AppendLine(string.Format("{0,-11} {1,-15} {2,-21} {3}",
                    f.Id, f.DisplayName,
                    granter != null ? granter.Id : "(NOTHING)", f.StatLine()));

                if (granter == null)
                    text.AppendLine("    PROBLEM: no boon grants \"" + f.Id + "\", so it can never appear");

                for (int a = 0; a < f.Attacks.Count; a++)
                    text.AppendLine("    " + f.Attacks[a].Summary());
            }

            // The other direction: a boon pointing at a familiar that does not exist.
            text.AppendLine();
            for (int i = 0; i < BoonLibrary.All.Count; i++)
            {
                Boon boon = BoonLibrary.All[i];
                for (int e = 0; e < boon.Effects.Count; e++)
                {
                    if (!(boon.Effects[e] is GrantFamiliarEffect grant)) continue;
                    if (FamiliarLibrary.Peek(grant.FamiliarId) != null) continue;

                    text.AppendLine("PROBLEM: boon \"" + boon.Id + "\" grants unknown familiar \""
                                    + grant.FamiliarId + "\"");
                }
            }

            Debug.Log(text.ToString());
        }

        private static Boon FindGranter(string familiarId)
        {
            for (int i = 0; i < BoonLibrary.All.Count; i++)
            {
                Boon boon = BoonLibrary.All[i];
                for (int e = 0; e < boon.Effects.Count; e++)
                    if (boon.Effects[e] is GrantFamiliarEffect grant && grant.FamiliarId == familiarId)
                        return boon;
            }
            return null;
        }
    }
}
#endif
