#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// One change to authored assets.
    ///
    /// Every migration must be safe to run twice: the second run finds nothing left to do and
    /// says so. That is what lets Report be trusted as "nothing pending", and what makes Apply
    /// harmless on a project that is already up to date. The usual way to get there is to key
    /// each change to evidence only an unmigrated asset has - an old field name, an old exact
    /// value - rather than to a value a later deliberate edit could also produce.
    /// </summary>
    public abstract class AssetMigration
    {
        /// <summary>Short name, shown in the report.</summary>
        public abstract string Name { get; }
    }

    /// <summary>
    /// Edits asset text directly. Needed whenever loading an asset would destroy the evidence: a
    /// field Unity no longer knows about, or an enum integer already reinterpreted by the time C#
    /// can see it.
    /// </summary>
    public abstract class TextMigration : AssetMigration
    {
        /// <summary>Folder under Assets/Resources holding the assets this touches. Empty for all of them.</summary>
        public abstract string Folder { get; }

        /// <summary>
        /// Returns the new text and adds one line per change. Returning the text unchanged means
        /// nothing to do. A change it cannot make safely should be reported without editing, so
        /// the migration stays pending until a person resolves it.
        /// </summary>
        public abstract string Migrate(string path, string text, List<string> changes);
    }

    /// <summary>
    /// Edits loaded assets through the AssetDatabase. Safe for any field whose stored value still
    /// means what it meant, and far less fragile than text for strings and nested data.
    ///
    /// Must not modify anything when <c>apply</c> is false. A loaded asset is live in the editor,
    /// and a dry run that changed one would leave it silently edited.
    /// </summary>
    public abstract class ObjectMigration : AssetMigration
    {
        public abstract void Migrate(bool apply, List<string> changes);
    }

    /// <summary>
    /// Runs every registered migration in order: text migrations, then a reimport, then object
    /// migrations, so object migrations see what the text ones changed.
    ///
    /// To add one, write the class and add it to <see cref="Steps"/> in the order it must run.
    /// Leave finished migrations registered. An old branch or a stale clone still needs them, and
    /// on an up-to-date project each costs one report line saying there is nothing to do.
    ///
    /// The older one-off tools (Sync Alt Fires, Sync Perception, Migrate Damage Types) predate
    /// this and have already been applied; they can move in here if they are ever needed again.
    /// </summary>
    public static class MigrationRunner
    {
        private static readonly AssetMigration[] Steps =
        {
            new LoadoutStatRename(),
            new SweepIntellectFold(),
            new SpellSchoolsFromCode(),
            new MeleeStrengthFold(),
            new SpellRetune(),
            new BoonStatText(),
            new LoadoutRetiredSpells(),
            new RetireLegacySpells()
        };

        [MenuItem("Gunspire/Migrations/1 - Report")]
        public static void Report()
        {
            int count = Run(apply: false, out string text);
            Debug.Log(text + (count == 0 ? "" : "  Run Apply to make these changes."));
        }

        [MenuItem("Gunspire/Migrations/2 - Apply")]
        public static void Apply()
        {
            Run(apply: true, out string text);
            Debug.Log(text);
        }

        /// <summary>Fails when anything is still pending, so it can sit alongside the other verifiers.</summary>
        [MenuItem("Gunspire/Migrations/3 - Verify Nothing Pending")]
        public static void VerifyNothingPending()
        {
            int count = Run(apply: false, out string text);
            if (count == 0) Debug.Log("Migrations: every asset is up to date.\n  no problems.");
            else Debug.LogError("Migrations: " + count + " PROBLEMS - changes still pending. Run Apply.\n" + text);
        }

        private static int Run(bool apply, out string report)
        {
            var log = new StringBuilder(apply
                ? "Asset migrations - applied\n"
                : "Asset migrations - REPORT ONLY, nothing written\n");
            int total = 0;

            foreach (AssetMigration step in Steps)
            {
                if (!(step is TextMigration textStep)) continue;

                var changes = new List<string>();
                string root = Path.Combine(Application.dataPath, "Resources", textStep.Folder);

                if (Directory.Exists(root))
                {
                    foreach (string path in Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories))
                    {
                        string original = File.ReadAllText(path);
                        string updated = textStep.Migrate(path, original, changes);
                        if (apply && updated != original) File.WriteAllText(path, updated);
                    }
                }

                total += Append(log, step, changes);
            }

            if (apply) AssetDatabase.Refresh();

            foreach (AssetMigration step in Steps)
            {
                if (!(step is ObjectMigration objectStep)) continue;

                var changes = new List<string>();
                objectStep.Migrate(apply, changes);
                total += Append(log, step, changes);
            }

            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                SpellLibrary.Reload();
                BoonLibrary.Reload();
                LoadoutLibrary.Reload();
            }

            log.AppendLine(total == 0
                ? "  Nothing pending."
                : "  " + total + " change(s) " + (apply ? "made." : "pending."));

            report = log.ToString();
            return total;
        }

        private static int Append(StringBuilder log, AssetMigration step, List<string> changes)
        {
            log.AppendLine("  " + step.Name + ": " + (changes.Count == 0 ? "nothing to do" : changes.Count + " change(s)"));
            for (int i = 0; i < changes.Count && i < 60; i++) log.AppendLine("    " + changes[i]);
            return changes.Count;
        }
    }

    /// <summary>Every loaded asset of one type in the project.</summary>
    internal static class AssetsOf<T> where T : UnityEngine.Object
    {
        public static List<T> All() => AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .ToList();
    }
}
#endif
