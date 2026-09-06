#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Writes the built-in spells out as assets. The effect chain is a SerializeReference list,
    /// so the Inspector shows a type picker with every effect in the game available from it.
    /// </summary>
    public static class SpellTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Spells";

        [MenuItem("Wizard with a Gun/Create Spell Assets")]
        public static void CreateSpellAssets()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Spells");

            List<Spell> builtIn = SpellLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                Spell def = builtIn[i];
                string path = FolderPath + "/Spell_" + def.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<SpellAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<SpellAsset>();
                asset.Definition = def;   // built fresh by BuiltIn(), so it is safe to hand over

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SpellLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " spell asset(s) in " + FolderPath +
                  ". The OnCast list takes any effect in the game via the type picker."
                : "All spell assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        /// <summary>Prints the roster with its effect chains, which is how you read a spell at a glance.</summary>
        [MenuItem("Wizard with a Gun/Log Spell Table")]
        public static void LogSpellTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id              rarity     type      school   mana  cd    chain");

            IReadOnlyList<Spell> all = SpellLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                Spell s = all[i];
                text.AppendLine(string.Format("{0,-15} {1,-10} {2,-9} {3,-8} {4,4:0} {5,5:0.#}  {6}",
                    s.Id, s.Rarity, s.Type, DamageTypes.Name(s.DamageType),
                    s.ManaCost, s.Cooldown, s.EffectSummary()));
            }

            Debug.Log(text.ToString());
        }
    }
}
#endif
