#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Writes the built-in guns out as assets so they can be tuned in the Inspector without a
    /// recompile. As with loadouts, the menu is the way to do it: an asset created anywhere
    /// outside a Resources folder is never found, and would silently do nothing.
    /// </summary>
    public static class WeaponTools
    {
        private const string RootFolder = "Assets/Resources";
        private const string FolderPath = RootFolder + "/Weapons";

        [MenuItem("Wizard with a Gun/Create Weapon Assets")]
        public static void CreateWeaponAssets()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder(RootFolder, "Weapons");

            List<WeaponDefinition> builtIn = WeaponLibrary.BuiltIn();
            Object last = null;
            int created = 0;

            for (int i = 0; i < builtIn.Count; i++)
            {
                WeaponDefinition def = builtIn[i];
                string path = FolderPath + "/Weapon_" + def.Id + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<WeaponAsset>(path);
                if (existing != null)
                {
                    last = existing;
                    continue;   // never overwrite something already tuned
                }

                var asset = ScriptableObject.CreateInstance<WeaponAsset>();
                asset.Definition = def.Clone();

                AssetDatabase.CreateAsset(asset, path);
                last = asset;
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WeaponLibrary.Reload();

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            Debug.Log(created > 0
                ? "Created " + created + " weapon asset(s) in " + FolderPath +
                  ". Edit them in the Inspector; ids matching a built-in replace it."
                : "All weapon assets already exist in " + FolderPath + "; nothing was overwritten.");
        }

        /// <summary>
        /// Copies just the alt fire block from the code roster onto existing weapon assets.
        ///
        /// Adding a field to WeaponDefinition leaves it at its default in every asset already
        /// on disk, and the assets are the source of truth - so without this every gun would
        /// silently have no alt fire. Regenerating the assets wholesale would fix that and
        /// throw away any Inspector tuning with it, which is not a trade worth making when the
        /// tuning is the entire reason the assets exist.
        ///
        /// Only touches assets whose alt fire is still empty, so running it twice is safe and
        /// an alt fire edited in the Inspector is never overwritten.
        /// </summary>
        [MenuItem("Wizard with a Gun/Sync Alt Fires To Assets")]
        public static void SyncAltFires()
        {
            List<WeaponDefinition> code = WeaponLibrary.BuiltIn();
            var text = new System.Text.StringBuilder();
            int written = 0;
            int skipped = 0;

            for (int i = 0; i < code.Count; i++)
            {
                WeaponDefinition source = code[i];
                string path = FolderPath + "/Weapon_" + source.Id + ".asset";

                var asset = AssetDatabase.LoadAssetAtPath<WeaponAsset>(path);
                if (asset == null || asset.Definition == null) continue;

                if (asset.Definition.HasAltFire)
                {
                    text.AppendLine("  kept   " + source.Id + ": already has \""
                                    + asset.Definition.AltFire.Name + "\"");
                    skipped++;
                    continue;
                }

                asset.Definition.AltFire = source.AltFire != null
                    ? source.AltFire.Clone()
                    : new AltFireProfile();

                EditorUtility.SetDirty(asset);
                text.AppendLine("  wrote  " + source.Id + ": " + asset.Definition.AltLine(99));
                written++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WeaponLibrary.Reload();

            Debug.Log("Alt fire sync: " + written + " written, " + skipped
                      + " left alone.\n" + text);
        }

        /// <summary>
        /// Prints the roster as a table. Balancing a gun means comparing it with the others,
        /// which is hard to do reading object initializers one at a time.
        /// </summary>
        [MenuItem("Wizard with a Gun/Log Weapon Balance Table")]
        public static void LogBalanceTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("id                 rarity      school   dps   dmg splash  rpm  mag pierce  shape");

            IReadOnlyList<WeaponDefinition> all = WeaponLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                WeaponDefinition w = all[i];

                // A launcher puts most of its damage in the blast, so leaving splash out of
                // this understates it badly enough to mislead a balance pass.
                float perTrigger = w.Damage * w.RoundsPerTrigger + w.SplashDamage;
                float dps = perTrigger * (w.RoundsPerMinute / 60f);

                string shape = w.Mode == FireMode.Burst
                    ? w.BurstCount + "-burst"
                    : (w.Delivery == DeliveryKind.Hitscan ? "hitscan" : "projectile");

                text.AppendLine(string.Format(
                    "{0,-18} {1,-11} {2,-8} {3,5:0} {4,5:0.#} {5,6:0} {6,4:0} {7,4} {8,6} {9}",
                    w.Id, w.Rarity, DamageTypes.Name(w.DamageType), dps, w.Damage, w.SplashDamage,
                    w.RoundsPerMinute, w.MagazineSize, w.MaxPierce, shape));
            }

            text.AppendLine();
            text.AppendLine("alt fire (right click)");
            for (int i = 0; i < all.Count; i++)
            {
                WeaponDefinition w = all[i];
                text.AppendLine(string.Format("{0,-18} tier {1}  {2,-22} {3}",
                    w.Id,
                    w.HasAltFire ? w.AltFire.UnlockTier.ToString() : "-",
                    w.HasAltFire ? w.AltFire.Name : "(none)",
                    w.HasAltFire ? w.AltFire.Summary() : string.Empty));
            }

            text.AppendLine();
            text.AppendLine("dps is sustained single-target, ignoring reloads. It counts splash at full "
                            + "value, so a launcher reads high against one target and higher against a "
                            + "group; pierce is not folded in at all. Alt fire is not in the dps column "
                            + "at all - it is on its own cooldown and does not sustain.");

            Debug.Log(text.ToString());
        }
    }
}
#endif
