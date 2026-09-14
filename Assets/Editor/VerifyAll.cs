#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Runs every verifier in one pass, so a change can be checked against everything at once,
    /// including from batch mode with -executeMethod.
    ///
    /// Goes through the menu paths rather than calling methods, so a verifier can be renamed or
    /// moved without this list breaking silently: a path that no longer exists is reported.
    /// Each verifier logs its own "no problems" or "PROBLEMS" line; search the log for the latter.
    /// </summary>
    public static class VerifyAll
    {
        private static readonly string[] MenuPaths =
        {
            "Gunspire/Migrations/3 - Verify Nothing Pending",
            "Gunspire/Verify Stats",
            "Gunspire/Verify Debuffs",
            "Gunspire/Verify Combat Core",
            "Gunspire/Verify Enemy AI",
            "Gunspire/Verify World Systems",
            "Gunspire/Verify Spell Slots",
            "Gunspire/Verify Holster",
            "Gunspire/Verify Shot Spread",
            "Gunspire/Verify Enemy Roster",
            "Gunspire/Verify Perception",
            "Gunspire/Verify Wall Zip Math",
            "Gunspire/Verify Maze Generator",
            "Gunspire/Verify Maze Navigation",
            "Gunspire/Log Loadout Table"
        };

        [MenuItem("Gunspire/Verify All")]
        public static void Run()
        {
            int missing = 0;
            foreach (string path in MenuPaths)
            {
                if (EditorApplication.ExecuteMenuItem(path)) continue;
                missing++;
                Debug.LogError("Verify All: PROBLEMS - no menu item \"" + path + "\"");
            }

            Debug.Log("Verify All: ran " + (MenuPaths.Length - missing) + " of " + MenuPaths.Length
                      + " checks. Search the log for PROBLEMS.");
        }
    }
}
#endif
