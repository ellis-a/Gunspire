#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Which content still needs art.
    ///
    /// Icons are assigned on the assets, not in code, so there is no compile error and no
    /// runtime warning when one is missing - the UI just draws a placeholder. This is the
    /// only thing that will tell you what is left.
    /// </summary>
    public static class IconTools
    {
        [MenuItem("Gunspire/Log Missing Icons")]
        public static void LogMissingIcons()
        {
            var text = new System.Text.StringBuilder();

            int gunsMissing = Report(text, "guns", "Assets/Resources/Weapons",
                Names(WeaponLibrary.All, w => w.Icon == null, w => w.Id), WeaponLibrary.All.Count);

            int spellsMissing = Report(text, "spells", "Assets/Resources/Spells",
                Names(SpellLibrary.All, s => s.Icon == null, s => s.Id), SpellLibrary.All.Count);

            int boonsMissing = Report(text, "boons", "Assets/Resources/Boons",
                Names(BoonLibrary.All, b => b.Icon == null, b => b.Id), BoonLibrary.All.Count);

            int total = gunsMissing + spellsMissing + boonsMissing;

            text.AppendLine();
            text.AppendLine(total == 0
                ? "Every gun, spell and boon has an icon."
                : total + " still need one. Assign them on the asset; a missing icon draws a "
                  + "tinted plate with initials rather than an empty square, so the layout is "
                  + "already the right shape.");

            text.AppendLine("Import icons as ordinary textures - the fields are Texture2D, so a "
                            + "PNG works as dropped in without changing its import type.");

            Debug.Log(text.ToString());
        }

        private static int Report(System.Text.StringBuilder text, string label, string folder,
            List<string> missing, int total)
        {
            text.AppendLine(label + ": " + (total - missing.Count) + " of " + total
                            + " have icons   (" + folder + ")");

            for (int i = 0; i < missing.Count; i++) text.AppendLine("    " + missing[i]);
            if (missing.Count > 0) text.AppendLine();

            return missing.Count;
        }

        private static List<string> Names<T>(IReadOnlyList<T> items,
            System.Func<T, bool> isMissing, System.Func<T, string> idOf)
        {
            var names = new List<string>();
            for (int i = 0; i < items.Count; i++)
                if (isMissing(items[i])) names.Add(idOf(items[i]));

            return names;
        }
    }
}
#endif
