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
            Dictionary<string, BoonAsset> existingById = AssetsById();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                Boon boon = builtIn[i];

                // Matched by id wherever it sits, so an asset moved to another folder is not made twice.
                if (existingById.TryGetValue(boon.Id, out BoonAsset existing))
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                string path = EnsureFolder(FolderFor(boon)) + "/Boon_" + boon.Id + ".asset";

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
        /// Where a new boon's asset goes: a folder per family, and a folder per school inside School, the way the
        /// spells are laid out. Any folder under Resources works; this only keeps 229 files browsable.
        /// </summary>
        private static string FolderFor(Boon boon) =>
            boon.Family == BoonFamily.School
                ? FolderPath + "/School/" + boon.Group
                : FolderPath + "/" + boon.Family;

        /// <summary>Creates each missing folder along a path under Assets and returns the path.</summary>
        private static string EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return path;
        }

        /// <summary>Every boon asset under the boon folder, by the id it carries.</summary>
        private static Dictionary<string, BoonAsset> AssetsById()
        {
            var found = new Dictionary<string, BoonAsset>();
            if (!AssetDatabase.IsValidFolder(FolderPath)) return found;

            foreach (string guid in AssetDatabase.FindAssets("t:BoonAsset", new[] { FolderPath }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BoonAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.Boon != null && !string.IsNullOrEmpty(asset.Boon.Id))
                    found[asset.Boon.Id] = asset;
            }
            return found;
        }

        public const string IconFolder = "Assets/Art/Icons/Boons";

        /// <summary>
        /// Gives each boon asset without an icon the image in <see cref="IconFolder"/> (any subfolder) whose file
        /// name is the boon's id. Dashes and spaces count as underscores and case is ignored, so
        /// "dead-mans-hand.png" finds dead_mans_hand, matching how the spell icons are named. An icon already
        /// assigned is never replaced.
        /// </summary>
        [MenuItem("Gunspire/Assign Boon Icons")]
        public static void AssignBoonIcons()
        {
            var icons = new Dictionary<string, Texture2D>();
            var duplicates = new List<string>();

            if (AssetDatabase.IsValidFolder(IconFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string key = IconKey(System.IO.Path.GetFileNameWithoutExtension(path));
                    if (icons.ContainsKey(key))
                    {
                        duplicates.Add(path);
                        continue;
                    }
                    icons[key] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            Dictionary<string, BoonAsset> assets = AssetsById();
            int assigned = 0;
            var unused = new HashSet<string>(icons.Keys);

            foreach (KeyValuePair<string, BoonAsset> pair in assets)
            {
                string key = IconKey(pair.Key);
                if (!icons.TryGetValue(key, out Texture2D icon)) continue;
                unused.Remove(key);

                if (pair.Value.Boon.Icon != null) continue;
                Undo.RecordObject(pair.Value, "Assign boon icon");
                pair.Value.Boon.Icon = icon;
                EditorUtility.SetDirty(pair.Value);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            BoonLibrary.Reload();

            var text = new System.Text.StringBuilder();
            text.AppendLine("Assign Boon Icons: " + assigned + " assigned from " + icons.Count + " images in " + IconFolder
                            + (assets.Count == 0 ? ". There are no boon assets yet; run Create Boon Assets first." : "."));
            foreach (string key in unused) text.AppendLine("    no boon has the id \"" + key + "\"");
            foreach (string path in duplicates) text.AppendLine("    ignored, same id as another image: " + path);
            text.Append("Log Missing Icons lists the boons still without one.");
            Debug.Log(text.ToString());
        }

        private static string IconKey(string name) => name.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

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

            text.AppendLine("id                     family   group         rarity     max  effects");

            IReadOnlyList<Boon> all = BoonLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                Boon b = all[i];

                perRarity.TryGetValue(b.Rarity, out int count);
                perRarity[b.Rarity] = count + 1;

                string gates = b.RequirementSummary();
                string gate = gates.Length > 0 ? "  [" + gates + "]" : string.Empty;
                text.AppendLine(string.Format("{0,-22} {1,-8} {2,-13} {3,-10} {4,3}  {5}{6}",
                    b.Id, b.Family, b.Group, b.Rarity, b.MaxLevel, b.EffectSummary(), gate));
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
