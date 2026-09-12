#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
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

        /// <summary>
        /// Exercises carrying two guns.
        ///
        /// The bug worth proving absent is the magazine: the rounds in the gun you are holding
        /// live on the weapon component, not in the holster's array, so a swap has to bank them
        /// on the way out. Get that wrong and a half-spent gun quietly comes back full - which
        /// nobody reports as a bug, they just stop noticing they are reloading less.
        /// </summary>
        [MenuItem("Gunspire/Verify Holster")]
        public static void VerifyHolster()
        {
            var problems = new List<string>();

            IReadOnlyList<WeaponDefinition> guns = WeaponLibrary.All;
            if (guns.Count < 2)
            {
                Debug.LogError("Holster: need at least two guns in the library to test with.");
                return;
            }

            WeaponDefinition first = guns[0];
            WeaponDefinition second = guns[1];

            var root = new GameObject("HolsterProbe");

            try
            {
                var weapon = root.AddComponent<Weapon>();
                var holster = root.AddComponent<Holster>();
                holster.Weapon = weapon;

                // One gun: nothing to swap to, and the swap must be refused rather than
                // silently drawing the same gun again and resetting its magazine.
                holster.SetSlot(0, first, 3);
                if (holster.CanSwap) problems.Add("claims it can swap while carrying one gun");

                holster.Swap();
                if (weapon.AmmoInMagazine != 3)
                    problems.Add("a refused swap changed the magazine to " + weapon.AmmoInMagazine);

                // A free hand takes a gun rather than trading one away.
                WeaponDefinition given = holster.Take(second, 5, out int givenAmmo);
                if (given != null) problems.Add("gave up " + given.Id + " while a hand was free");
                if (holster.FilledSlots != 2) problems.Add("a free hand did not get filled");
                if (holster.GetSlot(holster.ActiveIndex) != second)
                    problems.Add("a picked-up gun was not drawn");

                // The banked magazine has to survive the round trip in both directions.
                if (weapon.AmmoInMagazine != 5)
                    problems.Add("picked up with 5 rounds but holds " + weapon.AmmoInMagazine);

                // Spend rounds the way firing does: straight on the weapon, leaving the
                // holster's own array stale. This is the divergence the write-back exists for,
                // and setting ammo through the holster would never produce it - which is
                // exactly how a test can pass while the banking is missing entirely.
                weapon.Equip(second, 2);

                holster.Swap();
                if (holster.AmmoIn(1) != 2)
                    problems.Add("spent gun banked " + holster.AmmoIn(1) + " rounds instead of the 2 it held");

                if (weapon.AmmoInMagazine != 3)
                    problems.Add("swapped back to the first gun and found " + weapon.AmmoInMagazine + " rounds, not 3");

                holster.Swap();
                if (weapon.AmmoInMagazine != 2)
                    problems.Add("the spent gun came back with " + weapon.AmmoInMagazine + " rounds, not 2");

                // Both hands full: now a pickup is a trade, and it must hand back the gun in
                // hand along with its live count, not whatever the array last held.
                WeaponDefinition inHand = holster.GetSlot(holster.ActiveIndex);
                int liveBefore = weapon.AmmoInMagazine;

                WeaponDefinition traded = holster.Take(first, -1, out int tradedAmmo);
                if (traded != inHand)
                    problems.Add("a full holster traded away " + (traded == null ? "nothing" : traded.Id)
                                 + " rather than the " + inHand.Id + " in hand");
                if (tradedAmmo != liveBefore)
                    problems.Add("handed back " + tradedAmmo + " rounds but the gun held " + liveBefore);

                // Refilling has to reach the gun that is put away, not only the one in hand.
                holster.SetSlot(0, first, 1);
                holster.SetSlot(1, second, 1);
                holster.SetActive(0);
                holster.RefillAll();

                if (holster.AmmoIn(0) != first.MagazineSize)
                    problems.Add("refill left the drawn gun at " + holster.AmmoIn(0));
                if (holster.AmmoIn(1) != second.MagazineSize)
                    problems.Add("refill missed the holstered gun, left at " + holster.AmmoIn(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            if (problems.Count == 0)
            {
                Debug.Log("Holster: two-gun carry, swapping, trading and refilling all hold up.\n  no problems.");
                return;
            }

            var report = new System.Text.StringBuilder("Holster: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 25; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        /// <summary>
        /// A shot cone has to be the same shape whichever way the player is facing. It was not:
        /// spread was applied by rotating about the world axes, so a shot down world X was
        /// rotated about its own direction and the cone collapsed to a horizontal line.
        ///
        /// Measured rather than eyeballed because the failure was invisible from three of the
        /// four compass points, which is exactly how it survived being played.
        /// </summary>
        [MenuItem("Gunspire/Verify Shot Spread")]
        public static void VerifySpread()
        {
            const int samples = 4000;
            const float degrees = 7f;
            const int bearings = 12;

            var report = new System.Text.StringBuilder();
            report.AppendLine("Shot spread at " + degrees + " degrees, "
                              + samples + " samples per facing");
            report.AppendLine("  bearing   sideways   vertical   ratio");

            float worstRatio = 1f;
            string worstAt = "";

            for (int b = 0; b < bearings; b++)
            {
                float yaw = b * (360f / bearings);

                // A pitched facing as well, because straight up is the one direction with no
                // horizontal axis to build the cone from.
                float pitch = b % 3 == 0 ? 0f : (b % 3 == 1 ? 35f : -60f);
                Vector3 forward = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

                Vector3 right = Vector3.Cross(Vector3.up, forward);
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(forward, right);

                float sideways = 0f, vertical = 0f;

                for (int i = 0; i < samples; i++)
                {
                    Vector3 shot = Weapon.ApplySpread(forward, degrees);
                    sideways += Mathf.Abs(Vector3.Dot(shot, right));
                    vertical += Mathf.Abs(Vector3.Dot(shot, up));
                }

                sideways /= samples;
                vertical /= samples;
                float ratio = sideways > 0.0001f ? vertical / sideways : 0f;

                if (ratio < worstRatio) { worstRatio = ratio; worstAt = "yaw " + yaw + " pitch " + pitch; }

                report.AppendLine("  " + yaw.ToString("000") + "/" + pitch.ToString("+00;-00")
                                  + "     " + sideways.ToString("0.0000")
                                  + "     " + vertical.ToString("0.0000")
                                  + "     " + ratio.ToString("0.000"));
            }

            // A round cone spreads as far up and down as it does side to side. Anything under
            // 0.85 is the cone flattening out, not sampling noise.
            if (worstRatio < 0.85f)
            {
                report.Append("  FLAT: worst vertical/sideways ratio " + worstRatio.ToString("0.000")
                              + " at " + worstAt);
                Debug.LogError(report.ToString());
                return;
            }

            report.Append("  round at every facing, worst ratio " + worstRatio.ToString("0.000"));
            Debug.Log(report.ToString());
        }

        [MenuItem("Gunspire/Create Weapon Assets")]
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
        [MenuItem("Gunspire/Sync Alt Fires To Assets")]
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
        [MenuItem("Gunspire/Log Weapon Balance Table")]
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
