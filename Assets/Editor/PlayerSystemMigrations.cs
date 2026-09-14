#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Movement spells gained levels, and Dash a charge for each level past the first. Its asset was written
    /// with a level cap of one, which would keep it from ever levelling. Copies the cap and the level note
    /// from the built-in, keyed to the old cap of exactly one, so a later deliberate retune is left alone.
    /// </summary>
    public class DashLevels : ObjectMigration
    {
        public override string Name => "Dash levels up, gaining a charge each level";

        public override void Migrate(bool apply, List<string> changes)
        {
            Spell builtIn = null;
            foreach (Spell spell in SpellLibrary.BuiltIn())
                if (spell.Id == SpellLibrary.DefaultMovementId) builtIn = spell;

            if (builtIn == null || builtIn.MaxLevel <= 1) return;

            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                Spell def = asset.Definition;
                if (def == null || def.Id != builtIn.Id || def.MaxLevel != 1) continue;

                changes.Add(def.Id + ": level cap 1 becomes " + builtIn.MaxLevel);
                if (!apply) continue;

                def.MaxLevel = builtIn.MaxLevel;
                def.LevelUpNote = builtIn.LevelUpNote;
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
#endif
