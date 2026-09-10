#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
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

        [MenuItem("Gunspire/Create Enemy Assets")]
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

        [MenuItem("Gunspire/Log Enemy Table")]
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
        /// Copies the perception fields onto enemy assets authored before they existed.
        ///
        /// The assets shadow the code roster by id, so writing senses into BuiltIn() alone does
        /// nothing at all once they are committed - the whole roster keeps deserialising to
        /// zeroes and standing still. This is the same one-way migration WeaponTools.SyncAltFires
        /// does: copy only onto assets that have not been given a value, never overwrite tuning.
        ///
        /// An asset counts as untouched only when all three ranges are zero together. That
        /// matters for Idle, where the default is a real choice rather than an absent one -
        /// Stand is what a blank field and a deliberate decision both look like, and only the
        /// ranges can tell them apart.
        /// </summary>
        [MenuItem("Gunspire/Sync Perception To Assets")]
        public static void SyncPerception()
        {
            var builtIn = new Dictionary<string, EnemyDefinition>();
            foreach (EnemyDefinition def in EnemyLibrary.BuiltIn()) builtIn[def.Id] = def;

            EnemyAsset[] assets = AssetDatabase.FindAssets("t:EnemyAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EnemyAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null && a.Enemy != null)
                .ToArray();

            int updated = 0;
            var skipped = new List<string>();

            foreach (EnemyAsset asset in assets)
            {
                EnemyDefinition def = asset.Enemy;

                bool untouched = def.SightRange <= 0f && def.SightHalfAngle <= 0f && def.HearingRange <= 0f;
                if (!untouched)
                {
                    skipped.Add(def.Id);
                    continue;
                }

                if (!builtIn.TryGetValue(def.Id, out EnemyDefinition source))
                {
                    // An authored enemy with no built-in to copy from still needs usable
                    // senses, so give it the resolved defaults rather than leaving it deaf.
                    def.SightRange = def.ResolvedSightRange;
                    def.SightHalfAngle = def.ResolvedSightHalfAngle;
                    def.HearingRange = def.ResolvedHearingRange;
                }
                else
                {
                    def.Idle = source.Idle;
                    def.SightRange = source.ResolvedSightRange;
                    def.SightHalfAngle = source.ResolvedSightHalfAngle;
                    def.HearingRange = source.ResolvedHearingRange;
                }

                EditorUtility.SetDirty(asset);
                updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyLibrary.Reload();

            string note = skipped.Count > 0
                ? "  Left alone (already tuned): " + string.Join(", ", skipped)
                : "  Nothing was already tuned.";

            Debug.Log("Perception sync: wrote senses onto " + updated + " of " + assets.Length
                      + " enemy asset(s).\n" + note);
        }

        /// <summary>
        /// Checks that everything can actually notice the player.
        ///
        /// The whole point of this check is the enemy assets committed before enemies had
        /// senses at all: they store zero for every perception field, and zero read literally
        /// is an enemy that is blind, deaf, and will stand in a corner for the rest of the run.
        /// That failure looks exactly like a working stealth system until you walk up to one.
        /// </summary>
        [MenuItem("Gunspire/Verify Perception")]
        public static void VerifyPerception()
        {
            var problems = new List<string>();
            var idles = new Dictionary<IdleActivity, int>();
            int storedAsZero = 0;

            foreach (EnemyDefinition def in EnemyLibrary.All)
            {
                idles.TryGetValue(def.Idle, out int n);
                idles[def.Idle] = n + 1;

                if (def.SightRange <= 0f || def.HearingRange <= 0f) storedAsZero++;

                if (def.ResolvedSightRange <= 0f)
                    problems.Add(def.Id + " resolves to no sight at all");

                if (def.ResolvedHearingRange <= 0f)
                    problems.Add(def.Id + " resolves to no hearing at all");

                float angle = def.ResolvedSightHalfAngle;
                if (angle <= 0f || angle > 180f)
                    problems.Add(def.Id + " has a " + angle.ToString("0") + " degree half-angle, which is not a cone");

                // Something that can neither see nor hear further than it can be shot from is
                // scenery. Not fatal, but it is never the intent.
                if (def.ResolvedSightRange < 3f && def.ResolvedHearingRange < 3f)
                    problems.Add(def.Id + " cannot notice anything further than 3m away");
            }

            // A silent gun cannot give the player away, which reads in play as hearing being
            // broken rather than as one weapon being mis-authored.
            foreach (WeaponDefinition weapon in WeaponLibrary.All)
                if (weapon.NoiseMultiplier <= 0f)
                    problems.Add("weapon " + weapon.Id + " makes no noise when fired");

            var report = new System.Text.StringBuilder("Perception: ");
            foreach (KeyValuePair<IdleActivity, int> pair in idles)
                report.Append(pair.Key + " " + pair.Value + "   ");
            report.AppendLine();
            report.AppendLine("  " + storedAsZero + " definition(s) store zeroes and fall back to the defaults");

            if (problems.Count == 0)
            {
                report.Append("  no problems.");
                Debug.Log(report.ToString());
                return;
            }

            report.AppendLine("  " + problems.Count + " PROBLEMS:");
            for (int i = 0; i < problems.Count && i < 25; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        /// <summary>
        /// Builds one of every enemy and reports what came out.
        ///
        /// The failure worth catching is an id that resolves to nothing: spawning is by string
        /// now, so a renamed enemy does not fail to compile, it just quietly stops appearing.
        /// Building each one also exercises the attack chains, which is where a malformed
        /// [SerializeReference] entry would surface as a null effect rather than an error.
        /// </summary>
        [MenuItem("Gunspire/Verify Enemy Roster")]
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
