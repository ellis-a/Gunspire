#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Enemies are assembled at runtime with no prefab, so there is nothing to open and look
    /// at. This builds one of every kind, reports what came out, and throws it away.
    ///
    /// It exists mostly for one failure the compiler cannot see: EnemyFactory.Spawn switches
    /// on EnemyKind with a default case, so a kind that is never given its own branch does not
    /// error - it quietly spawns a Cultist instead. Checking the name that comes back catches
    /// that, and it is exactly the sort of thing that would otherwise be noticed three
    /// playtests later.
    /// </summary>
    public static class EnemyTools
    {
        [MenuItem("Wizard with a Gun/Verify Enemy Roster")]
        public static void VerifyRoster()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("kind          spawned as      hp   speed  flying  hover  attacks");

            int problems = 0;

            foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
            {
                EnemyController enemy = null;
                try
                {
                    enemy = EnemyFactory.Spawn(kind, Vector3.zero, floor: 1);
                    if (enemy == null)
                    {
                        text.AppendLine(kind + ": FAILED, Spawn returned null");
                        problems++;
                        continue;
                    }

                    var sheet = enemy.GetComponent<CharacterSheet>();
                    EnemyAttack[] attacks = enemy.GetComponents<EnemyAttack>();

                    text.AppendLine(string.Format("{0,-13} {1,-14} {2,4:0} {3,6:0.0} {4,7} {5,6:0.0}  {6}",
                        kind, enemy.DisplayName,
                        sheet != null ? sheet.Get(Attr.MaxHealth) : 0f,
                        sheet != null ? sheet.Get(Attr.MoveSpeed) : 0f,
                        enemy.Flying, enemy.Flying ? enemy.HoverHeight : 0f,
                        DescribeAttacks(attacks)));

                    // The silent-fallthrough check. Every kind must build its own archetype.
                    if (!enemy.DisplayName.Replace(" ", "").ToLowerInvariant()
                            .Contains(kind.ToString().ToLowerInvariant()))
                    {
                        text.AppendLine("    PROBLEM: " + kind + " spawned as \"" + enemy.DisplayName
                                        + "\" - it has no case in EnemyFactory.Spawn and fell through to the default");
                        problems++;
                    }

                    if (attacks.Length == 0)
                    {
                        text.AppendLine("    PROBLEM: " + kind + " has no attacks and will stand there");
                        problems++;
                    }

                    if (enemy.Muzzle == null)
                    {
                        text.AppendLine("    PROBLEM: " + kind + " has no muzzle; shots will come from its feet");
                        problems++;
                    }

                    // A flier that hovers below head height is not meaningfully flying, and one
                    // above the 11m walls would be unreachable.
                    if (enemy.Flying && (enemy.HoverHeight < 1.5f || enemy.HoverHeight > 9f))
                    {
                        text.AppendLine("    PROBLEM: " + kind + " hovers at " + enemy.HoverHeight + "m");
                        problems++;
                    }
                }
                finally
                {
                    if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
                }
            }

            text.AppendLine();
            text.AppendLine(problems == 0
                ? "Roster verified: " + System.Enum.GetValues(typeof(EnemyKind)).Length + " kinds, no problems."
                : problems + " problem(s) found.");

            if (problems == 0) Debug.Log(text.ToString());
            else Debug.LogError(text.ToString());
        }

        private static string DescribeAttacks(EnemyAttack[] attacks)
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
