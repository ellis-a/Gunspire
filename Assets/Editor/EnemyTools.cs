#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Enemies are assembled at runtime with no prefab, so there is nothing to open and look
    /// at. These write the roster out as assets, print it, and build one of each to check that
    /// what comes back is what was asked for.
    /// </summary>
    public static class EnemyTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Enemies";

        [MenuItem("Wizard with a Gun/Create Enemy Assets")]
        public static void CreateEnemyAssets()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Enemies");

            List<EnemyDefinition> builtIn = EnemyLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                EnemyDefinition def = builtIn[i];
                string path = FolderPath + "/Enemy_" + def.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<EnemyAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<EnemyAsset>();
                asset.Enemy = def.Clone();

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " enemy asset(s) in " + FolderPath +
                  ". Edit them in the Inspector; ids matching a built-in replace it."
                : "All enemy assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        [MenuItem("Wizard with a Gun/Log Enemy Table")]
        public static void LogEnemyTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id             name           hp  speed  flying  roster  resists");

            IReadOnlyList<EnemyDefinition> all = EnemyLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                EnemyDefinition e = all[i];
                string affinity = e.ResistsEverything
                    ? "all " + (e.BroadResistance * 100f).ToString("0") + "%"
                    : DamageTypes.Name(e.Resists) + ", weak " + DamageTypes.Name(e.WeakTo);

                text.AppendLine(string.Format("{0,-14} {1,-13} {2,4:0} {3,6:0.0} {4,7} {5,7}  {6}",
                    e.Id, e.DisplayName, e.Health, e.MoveSpeed,
                    e.Flying ? e.HoverHeight.ToString("0.#") + "m" : "-",
                    e.InStandardRoster ? "yes" : "no", affinity));
            }

            text.AppendLine();
            text.AppendLine("attacks (damage shown is floor 1, before the floor and elite multipliers)");
            for (int i = 0; i < all.Count; i++)
            {
                EnemyDefinition e = all[i];
                text.AppendLine("  " + e.Id);
                for (int a = 0; a < e.Attacks.Count; a++)
                    text.AppendLine("    " + e.Attacks[a].Summary());
            }

            Debug.Log(text.ToString());
        }

        /// <summary>
        /// Builds one of every enemy and reports what came out.
        ///
        /// The failure worth catching is an id that resolves to nothing: spawning is by string
        /// now, so a renamed enemy does not fail to compile, it just quietly stops appearing.
        /// Building each one also exercises the attack chains, which is where a malformed
        /// [SerializeReference] entry would surface as a null effect rather than an error.
        /// </summary>
        [MenuItem("Wizard with a Gun/Verify Enemy Roster")]
        public static void VerifyRoster()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id             spawned as      hp   speed  flying  hover  attacks");

            int problems = 0;
            IReadOnlyList<EnemyDefinition> all = EnemyLibrary.All;

            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].Id;
                EnemyController enemy = null;

                try
                {
                    enemy = EnemyFactory.Spawn(id, Vector3.zero, floor: 1);
                    if (enemy == null)
                    {
                        text.AppendLine(id + ": FAILED, Spawn returned null");
                        problems++;
                        continue;
                    }

                    var sheet = enemy.GetComponent<CharacterSheet>();
                    AbilityAttack[] attacks = enemy.GetComponents<AbilityAttack>();

                    text.AppendLine(string.Format("{0,-14} {1,-14} {2,4:0} {3,6:0.0} {4,7} {5,6:0.0}  {6}",
                        id, enemy.DisplayName,
                        sheet != null ? sheet.Get(Attr.MaxHealth) : 0f,
                        sheet != null ? sheet.Get(Attr.MoveSpeed) : 0f,
                        enemy.Flying, enemy.Flying ? enemy.HoverHeight : 0f,
                        DescribeAttacks(attacks)));

                    if (attacks.Length != all[i].Attacks.Count)
                    {
                        text.AppendLine("    PROBLEM: " + id + " defines " + all[i].Attacks.Count
                                        + " attack(s) but built " + attacks.Length);
                        problems++;
                    }

                    if (attacks.Length == 0)
                    {
                        text.AppendLine("    PROBLEM: " + id + " has no attacks and will stand there");
                        problems++;
                    }

                    for (int a = 0; a < attacks.Length; a++)
                    {
                        // A null entry here is a serialization failure, not a design choice.
                        List<AbilityEffect> sequence = attacks[a].Sequence;
                        if (sequence == null || sequence.Count == 0)
                        {
                            text.AppendLine("    PROBLEM: " + id + " attack \"" + attacks[a].Name
                                            + "\" has an empty sequence");
                            problems++;
                            continue;
                        }

                        for (int s = 0; s < sequence.Count; s++)
                        {
                            if (sequence[s] != null) continue;
                            text.AppendLine("    PROBLEM: " + id + " attack \"" + attacks[a].Name
                                            + "\" has a null effect at index " + s);
                            problems++;
                        }
                    }

                    if (enemy.Muzzle == null)
                    {
                        text.AppendLine("    PROBLEM: " + id + " has no muzzle; shots come from its feet");
                        problems++;
                    }

                    if (enemy.Flying && (enemy.HoverHeight < 1.5f || enemy.HoverHeight > 9f))
                    {
                        text.AppendLine("    PROBLEM: " + id + " hovers at " + enemy.HoverHeight + "m");
                        problems++;
                    }
                }
                finally
                {
                    if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
                }
            }

            // Combat rooms roll from this pool, so an empty one is a tower with no enemies.
            int roster = EnemyLibrary.StandardRoster().Count;
            text.AppendLine();
            text.AppendLine(roster + " of " + all.Count + " are in the standard roster.");
            if (roster == 0)
            {
                text.AppendLine("PROBLEM: nothing is in the standard roster; combat rooms would be empty");
                problems++;
            }

            if (EnemyLibrary.Peek(EnemyLibrary.BossId) == null)
            {
                text.AppendLine("PROBLEM: no enemy with the boss id \"" + EnemyLibrary.BossId + "\"");
                problems++;
            }

            text.AppendLine(problems == 0
                ? "Roster verified: " + all.Count + " enemies, no problems."
                : problems + " problem(s) found.");

            if (problems == 0) Debug.Log(text.ToString());
            else Debug.LogError(text.ToString());
        }

        private static string DescribeAttacks(AbilityAttack[] attacks)
        {
            if (attacks.Length == 0) return "(none)";

            var parts = new string[attacks.Length];
            for (int i = 0; i < attacks.Length; i++)
                parts[i] = attacks[i].Name + " [" + attacks[i].MinRange.ToString("0")
                           + "-" + attacks[i].MaxRange.ToString("0") + "m]";

            return string.Join(", ", parts);
        }
    }
}
#endif
