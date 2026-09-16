#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Fills in each stance mode's icon from a file sitting next to the spell's own icon and named after it, so
    /// elemental-form.png finds elemental-form-fire.png, elemental-form-ice.png and elemental-form-storm.png.
    ///
    /// Never replaces an icon already assigned, so it is safe to run again after adding a stance or an icon.
    /// </summary>
    public static class StanceIconTools
    {
        [MenuItem("Gunspire/Assign Stance Mode Icons")]
        public static void AssignStanceModeIcons()
        {
            var report = new List<string>();
            int assigned = 0;

            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                Spell spell = asset.Definition;
                if (spell == null || !spell.IsStance) continue;

                if (spell.Icon == null)
                {
                    report.Add(spell.Id + ": has no icon of its own, so there is nothing to name the mode icons after");
                    continue;
                }

                string iconPath = AssetDatabase.GetAssetPath(spell.Icon);
                string folder = Path.GetDirectoryName(iconPath);
                string stem = Path.GetFileNameWithoutExtension(iconPath);
                string extension = Path.GetExtension(iconPath);
                bool changed = false;

                foreach (StanceMode mode in spell.Stance.Modes)
                {
                    if (mode.Icon != null) continue;

                    string modeName = mode.Name.Trim().ToLowerInvariant().Replace(' ', '-');
                    string path = Path.Combine(folder, stem + "-" + modeName + extension).Replace('\\', '/');
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                    if (texture == null)
                    {
                        report.Add(spell.Id + " " + mode.Name + ": no icon at " + path);
                        continue;
                    }

                    mode.Icon = texture;
                    changed = true;
                    assigned++;
                    report.Add(spell.Id + " " + mode.Name + ": " + path);
                }

                if (changed) EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            SpellLibrary.Reload();

            Debug.Log("Stance mode icons: " + assigned + " assigned.\n  " +
                      (report.Count == 0 ? "no stances needed any." : string.Join("\n  ", report)));
        }
    }
}
#endif
