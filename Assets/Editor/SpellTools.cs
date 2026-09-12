#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Writes the built-in spells out as assets. The effect chain is a SerializeReference list,
    /// so the Inspector shows a type picker with every effect in the game available from it.
    /// </summary>
    public static class SpellTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Spells";

        [MenuItem("Gunspire/Create Spell Assets")]
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

        /// <summary>
        /// Checks the things that go wrong once spells are split across slots. Ids are plain
        /// strings the compiler never sees, so a loadout pointing its Shift slot at a fireball
        /// does not fail to build - it just quietly leaves the slot empty at runtime.
        /// </summary>
        [MenuItem("Gunspire/Verify Spell Slots")]
        public static void VerifySlots()
        {
            var problems = new List<string>();
            var counts = new Dictionary<SpellSlot, int>();
            var seen = new Dictionary<string, string>();

            foreach (Spell spell in SpellLibrary.All)
            {
                counts.TryGetValue(spell.Slot, out int n);
                counts[spell.Slot] = n + 1;

                if (seen.TryGetValue(spell.Id, out string other))
                    problems.Add("duplicate id \"" + spell.Id + "\" (" + other + " and " + spell.Slot + ")");
                seen[spell.Id] = spell.Slot.ToString();

                // Every slot costs mana now, one way or the other. A free spell in any slot is
                // almost always a value that never got filled in.
                float upfront = spell.IsSustained ? spell.Sustain.ManaPerSecond : spell.ManaCost;
                if (upfront <= 0f)
                    problems.Add(spell.Id + " (" + spell.Slot + ") costs no mana");

                if (spell.IsSustained && spell.Slot != SpellSlot.Movement)
                    problems.Add(spell.Id + " is sustained but sits in the " + spell.Slot + " slot");

                if (spell.Slot != SpellSlot.Cast && spell.OnCast.Count == 0 && !spell.IsSustained)
                    problems.Add(spell.Id + " has an empty effect chain");
            }

            // Offers must never cross slots, or a shrine hands you a dash for Q.
            foreach (SpellSlot slot in System.Enum.GetValues(typeof(SpellSlot)))
            {
                foreach (Spell spell in SpellLibrary.ForSlot(slot))
                    if (spell.Slot != slot) problems.Add(spell.Id + " leaked into the " + slot + " offer pool");
            }

            CheckDefault(problems, SpellLibrary.DefaultMovementId, SpellSlot.Movement);
            CheckDefault(problems, SpellLibrary.DefaultMeleeId, SpellSlot.Melee);

            foreach (LoadoutDefinition loadout in LoadoutLibrary.All)
            {
                CheckLoadout(problems, loadout.Id, loadout.MovementAbilityId, SpellSlot.Movement);
                CheckLoadout(problems, loadout.Id, loadout.MeleeSpellId, SpellSlot.Melee);
            }

            var report = new System.Text.StringBuilder("Spell slots: ");
            foreach (KeyValuePair<SpellSlot, int> pair in counts)
                report.Append(pair.Key + " " + pair.Value + "   ");
            report.AppendLine();

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

        private static void CheckDefault(List<string> problems, string id, SpellSlot slot)
        {
            Spell spell = SpellLibrary.Get(id);
            if (spell == null) problems.Add("default " + slot + " spell \"" + id + "\" does not exist");
            else if (spell.Slot != slot)
                problems.Add("default " + slot + " spell \"" + id + "\" is actually " + spell.Slot);
        }

        private static void CheckLoadout(List<string> problems, string loadoutId, string spellId,
            SpellSlot slot)
        {
            Spell spell = SpellLibrary.Get(spellId);
            if (spell == null)
                problems.Add("loadout \"" + loadoutId + "\": no spell with id \"" + spellId + "\"");
            else if (spell.Slot != slot)
                problems.Add("loadout \"" + loadoutId + "\": \"" + spellId + "\" is "
                             + spell.Slot + ", not " + slot);
        }

        /// <summary>Prints the roster with its effect chains, which is how you read a spell at a glance.</summary>
        [MenuItem("Gunspire/Log Spell Table")]
        public static void LogSpellTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id              slot      school       rarity     dmg       mana  cd    chain");

            IReadOnlyList<Spell> all = SpellLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                Spell s = all[i];
                text.AppendLine(string.Format("{0,-15} {1,-9} {2,-12} {3,-10} {4,-9} {5,4:0} {6,5:0.#}  {7}",
                    s.Id, s.Slot, s.School, s.Rarity, DamageTypes.Name(s.DamageType),
                    s.ManaCost, s.Cooldown, s.EffectSummary()));
            }

            Debug.Log(text.ToString());
        }
    }
}
#endif
