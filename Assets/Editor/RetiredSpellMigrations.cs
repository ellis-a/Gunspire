#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// The spells from before the school designs. None appears in any designed list, so they
    /// were retired rather than mapped onto a designed spell. The effects they were built from
    /// stay, since designed spells compose the same pieces.
    /// </summary>
    internal static class RetiredSpells
    {
        public static readonly HashSet<string> Ids = new HashSet<string>
        {
            "arcane_ward", "blightbloom", "chain_lightning", "cleave", "cone_of_cold", "eventide",
            "fel_empowerment", "firebolt", "glacial_prison", "kinetic_slam", "lava_splash", "leech",
            "shield", "sprint"
        };
    }

    /// <summary>
    /// Deletes the assets of retired spells. Removing the built-in is not enough: an asset adds
    /// its id to the roster whether or not a built-in exists, so each would keep its spell alive.
    /// Keyed to the id, so once the asset is gone there is nothing left to find.
    /// </summary>
    public class RetireLegacySpells : ObjectMigration
    {
        public override string Name => "Retired spell assets deleted";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                string id = asset.Definition != null ? asset.Definition.Id : null;
                if (id == null || !RetiredSpells.Ids.Contains(id)) continue;

                string path = AssetDatabase.GetAssetPath(asset);
                changes.Add(id + ": " + path + " deleted");
                if (apply) AssetDatabase.DeleteAsset(path);
            }
        }
    }

    /// <summary>
    /// Loadouts that open with a retired spell. Each slot moves to what every run opens with:
    /// Dash, Bash, or no cast spell. The game would fall back the same way at runtime, but with
    /// a warning every run and a class card showing nothing on Shift.
    /// </summary>
    public class LoadoutRetiredSpells : ObjectMigration
    {
        public override string Name => "Loadouts moved off retired spells";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (StartingLoadoutAsset asset in AssetsOf<StartingLoadoutAsset>.All())
            {
                LoadoutDefinition loadout = asset.Loadout;
                if (loadout == null) continue;

                bool changed = false;

                if (RetiredSpells.Ids.Contains(loadout.MovementAbilityId ?? ""))
                {
                    changes.Add(loadout.Id + ": movement " + loadout.MovementAbilityId + " becomes " + SpellLibrary.DefaultMovementId);
                    if (apply) loadout.MovementAbilityId = SpellLibrary.DefaultMovementId;
                    changed = true;
                }

                if (RetiredSpells.Ids.Contains(loadout.MeleeSpellId ?? ""))
                {
                    changes.Add(loadout.Id + ": melee " + loadout.MeleeSpellId + " becomes " + SpellLibrary.DefaultMeleeId);
                    if (apply) loadout.MeleeSpellId = SpellLibrary.DefaultMeleeId;
                    changed = true;
                }

                if (RetiredSpells.Ids.Contains(loadout.SpellId ?? ""))
                {
                    changes.Add(loadout.Id + ": starting spell " + loadout.SpellId + " removed");
                    if (apply) loadout.SpellId = string.Empty;
                    changed = true;
                }

                if (changed && apply) EditorUtility.SetDirty(asset);
            }
        }
    }
}
#endif
