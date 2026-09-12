#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// One-shot migration for the seven-element damage model collapsing into four.
    ///
    /// DamageType is stored in assets as a bare integer, so the enum could not simply be
    /// rewritten: an asset holding 2 meant Frost before and would mean Psychic after, with
    /// nothing anywhere to show that its meaning had changed. The new enum deliberately keeps
    /// Kinetic, Energy and Necrotic on the indices the old Normal, Fire and Nature held, which
    /// leaves only three values actually needing to move.
    ///
    /// Edits the asset text rather than going through the AssetDatabase, because loading an
    /// asset would already have reinterpreted the old integer as the new enum - by the time
    /// C# can see the value, the evidence of what it used to mean is gone.
    ///
    /// Safe to run twice: every target index is already a valid destination, so a second pass
    /// finds nothing left that needs moving. Run Report first.
    /// </summary>
    public static class DamageTypeMigration
    {
        /// <summary>Old index to new index. Absent entries already mean the right thing.</summary>
        private static readonly Dictionary<int, int> Remap = new Dictionary<int, int>
        {
            // Frost -> Kinetic. Thrown ice arrives as an object, so it is kinetic now.
            { 2, 0 },

            // Shadow -> Necrotic, joining the old Nature which already sits on 3.
            { 4, 3 },

            // Astral -> Energy, joining the old Fire on 1.
            { 5, 1 },

            // True keeps its meaning but moves down, the other four having closed up.
            { 6, 4 }
        };

        private static readonly string[] Fields = { "DamageType", "Resists", "WeakTo" };

        /// <summary>
        /// Two boon effects hold a damage type in a field called School. Only boons, and only
        /// ever boons: Spell.School is a SpellSchool, a different enum on the same field name,
        /// and remapping it would quietly re-school the spell roster instead.
        /// </summary>
        private static readonly string[] BoonOnlyFields = { "School" };

        private static bool IsBoon(string path) => path.Replace('\\', '/').Contains("/Boons/");

        [MenuItem("Gunspire/Migrate Damage Types/1 - Report")]
        public static void Report() => Run(dryRun: true);

        [MenuItem("Gunspire/Migrate Damage Types/2 - Apply")]
        public static void Apply() => Run(dryRun: false);

        private static void Run(bool dryRun)
        {
            string[] paths = Directory.GetFiles(Application.dataPath + "/Resources",
                "*.asset", SearchOption.AllDirectories);

            var report = new StringBuilder(dryRun
                ? "Damage type migration - REPORT ONLY, nothing written\n"
                : "Damage type migration - applied\n");

            int filesTouched = 0;
            int valuesMoved = 0;
            var tally = new Dictionary<string, int>();

            foreach (string path in paths)
            {
                string text = File.ReadAllText(path);
                string updated = text;
                int inFile = 0;

                var fields = new List<string>(Fields);
                if (IsBoon(path)) fields.AddRange(BoonOnlyFields);

                foreach (string field in fields)
                {
                    // Anchored to the field name so only these integers are touched. A bare
                    // number search would happily rewrite damage values and stack counts.
                    updated = Regex.Replace(updated, @"(\b" + field + @":\s*)(\d+)", match =>
                    {
                        int old = int.Parse(match.Groups[2].Value);
                        if (!Remap.TryGetValue(old, out int now)) return match.Value;

                        inFile++;
                        string key = field + " " + old + " -> " + now;
                        tally.TryGetValue(key, out int n);
                        tally[key] = n + 1;

                        return match.Groups[1].Value + now;
                    });
                }

                if (inFile == 0) continue;

                filesTouched++;
                valuesMoved += inFile;

                if (!dryRun) File.WriteAllText(path, updated);
            }

            report.AppendLine("  " + valuesMoved + " value(s) across " + filesTouched
                              + " of " + paths.Length + " asset(s)");

            foreach (KeyValuePair<string, int> pair in tally)
                report.AppendLine("    " + pair.Key + "   x" + pair.Value);

            if (valuesMoved == 0)
                report.AppendLine("  Nothing left to move - either already migrated, or nothing used those schools.");

            if (!dryRun)
            {
                AssetDatabase.Refresh();
                SpellLibrary.Reload();
                EnemyLibrary.Reload();
                report.AppendLine("  Reimported. Check a spell asset's damage type in the Inspector.");
            }

            Debug.Log(report.ToString());
        }
    }
}
#endif
