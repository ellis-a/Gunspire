#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Proves the stat rework changed nothing it was not meant to.
    ///
    /// The new formulas are checked against the old ones, written out below exactly as they were
    /// before the rework. Where a new stat inherited an old stat's job, a character at new value N
    /// must match the old formula at N minus 7. Where a stat stopped reaching an attribute, the
    /// attribute must not move whatever the stats are.
    /// </summary>
    public static class StatTools
    {
        private const float Tolerance = 0.0005f;

        [MenuItem("Gunspire/Verify Stats")]
        public static void VerifyStats()
        {
            var problems = new List<string>();

            foreach (int[] stats in Samples())
                CheckAgainstOldFormulas(problems, stats);

            CheckRemovedInfluences(problems);
            CheckDexterityHandling(problems);
            CheckLuck(problems);
            CheckLoadouts(problems);

            if (problems.Count == 0)
            {
                Debug.Log("Stats: every attribute matches the old formula at the rebased stat, and "
                          + "removed influences stay fixed.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Stats: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 30; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- the old formulas

        // Verbatim from before the rework, in terms of the old stats.
        private static float OldMaxHealth(int vitality) => 80f + vitality * 12f;
        private static float OldMaxMana(int intellect) => 80f + intellect * 8f;
        private static float OldSpellPower(int intellect) => 1f + intellect * 0.045f;
        private static float OldMoveSpeed(int agility) => 7.0f + agility * 0.18f;
        private static float OldJumpHeight(int agility) => 1.25f + agility * 0.06f;
        private static float OldAirControl(int agility) => 0.35f + agility * 0.015f;
        private static float OldCritChance(int luck) => 0.03f + luck * 0.012f;

        // Formulas whose per-point numbers moved to a different stat. The numbers are the old
        // ones; only the stat feeding them changed.
        private static float OldManaRegenStep(int stat) => 5f + stat * 0.8f;
        private static float OldCooldownRateStep(int stat) => 1f + stat * 0.020f;
        private static float OldReloadSpeedStep(int stat) => 1f + stat * 0.020f;

        // ---------------------------------------------------------------- checks

        /// <summary>Dexterity, Power, Athletics, Endurance, Luck.</summary>
        private static IEnumerable<int[]> Samples()
        {
            yield return new[] { 10, 10, 10, 10, 10 };
            yield return new[] { 11, 10, 10, 10, 10 };
            yield return new[] { 7, 13, 9, 15, 12 };
            yield return new[] { 16, 8, 14, 7, 20 };

            foreach (LoadoutDefinition loadout in LoadoutLibrary.All)
            {
                yield return new[]
                {
                    loadout.Dexterity, loadout.Power, loadout.Athletics, loadout.Endurance, loadout.Luck
                };
            }
        }

        private static void CheckAgainstOldFormulas(List<string> problems, int[] s)
        {
            GameObject go = Sheet(s, out CharacterSheet sheet);
            try
            {
                int offset = CharacterSheet.Baseline - 3;
                int dex = s[0] - offset, pow = s[1] - offset, ath = s[2] - offset, end = s[3] - offset, lck = s[4] - offset;
                string at = " at " + Describe(s);

                Expect(problems, sheet, Attr.MaxHealth, OldMaxHealth(end), at);
                Expect(problems, sheet, Attr.MaxMana, OldMaxMana(pow), at);
                Expect(problems, sheet, Attr.SpellPower, OldSpellPower(pow), at);
                Expect(problems, sheet, Attr.MoveSpeed, OldMoveSpeed(ath), at);
                Expect(problems, sheet, Attr.JumpHeight, OldJumpHeight(ath), at);
                Expect(problems, sheet, Attr.AirControl, OldAirControl(ath), at);
                Expect(problems, sheet, Attr.CritChance, OldCritChance(lck), at);

                Expect(problems, sheet, Attr.ManaRegen, OldManaRegenStep(end), at);
                Expect(problems, sheet, Attr.CooldownRate, OldCooldownRateStep(ath), at);
                Expect(problems, sheet, Attr.ReloadSpeed, OldReloadSpeedStep(dex), at);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Attributes no stat reaches any more must hold still across very different characters.</summary>
        private static void CheckRemovedInfluences(List<string> problems)
        {
            var fixedValues = new Dictionary<Attr, float>
            {
                { Attr.HealthRegen, 0f },
                { Attr.GunDamage, 1f },
                { Attr.AttackSpeed, 1f },
                { Attr.CritDamage, 1.75f },
                { Attr.DashCharges, 1f },
                { Attr.DashSpeed, 22f },
                { Attr.SmashPower, 0f },
                { Attr.GravityScale, 1f }
            };

            foreach (int[] stats in new[] { new[] { 0, 0, 0, 0, 0 }, new[] { 10, 10, 10, 10, 10 }, new[] { 30, 30, 30, 30, 30 } })
            {
                GameObject go = Sheet(stats, out CharacterSheet sheet);
                try
                {
                    foreach (KeyValuePair<Attr, float> pair in fixedValues)
                        Expect(problems, sheet, pair.Key, pair.Value, " at " + Describe(stats) + " (no stat should move it)");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        /// <summary>Dexterity is the only stat on spread and recoil: neutral at the baseline, tighter above it.</summary>
        private static void CheckDexterityHandling(List<string> problems)
        {
            float previousSpread = float.MaxValue;
            float previousRecoil = float.MaxValue;

            for (int dex = 4; dex <= 20; dex++)
            {
                GameObject go = Sheet(new[] { dex, 10, 10, 10, 10 }, out CharacterSheet sheet);
                try
                {
                    float spread = sheet.Get(Attr.Spread);
                    float recoil = sheet.Get(Attr.Recoil);

                    if (dex == CharacterSheet.Baseline)
                    {
                        if (Mathf.Abs(spread - 1f) > Tolerance) problems.Add("spread at Dexterity 10 is " + spread + ", expected 1");
                        if (Mathf.Abs(recoil - 1f) > Tolerance) problems.Add("recoil at Dexterity 10 is " + recoil + ", expected 1");
                    }

                    if (spread >= previousSpread) problems.Add("spread did not tighten going to Dexterity " + dex);
                    if (recoil >= previousRecoil) problems.Add("recoil did not lessen going to Dexterity " + dex);

                    previousSpread = spread;
                    previousRecoil = recoil;
                }
                finally { Object.DestroyImmediate(go); }
            }

            // Any other stat must leave them alone.
            GameObject other = Sheet(new[] { 10, 20, 20, 20, 20 }, out CharacterSheet otherSheet);
            try
            {
                Expect(problems, otherSheet, Attr.Spread, 1f, " with only non-Dexterity stats raised");
                Expect(problems, otherSheet, Attr.Recoil, 1f, " with only non-Dexterity stats raised");
            }
            finally { Object.DestroyImmediate(other); }
        }

        /// <summary>Rarity odds at new Luck N must equal the old odds at N minus 7.</summary>
        private static void CheckLuck(List<string> problems)
        {
            for (int luck = 0; luck <= 25; luck++)
            {
                int old = Mathf.Max(0, luck - (CharacterSheet.Baseline - 3));
                float expected = 1f + old * 0.08f;
                float actual = Rarities.LuckFactor(luck);

                if (Mathf.Abs(actual - expected) > Tolerance)
                    problems.Add("Luck " + luck + " multiplies rarity by " + actual + ", expected " + expected
                                 + " (the old factor at Luck " + old + ")");
            }
        }

        /// <summary>
        /// A loadout below 7 in any stat was almost certainly never migrated: its old value was
        /// carried across rather than moved up. Classes must also share one budget.
        /// </summary>
        private static void CheckLoadouts(List<string> problems)
        {
            IReadOnlyList<LoadoutDefinition> all = LoadoutLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                LoadoutDefinition l = all[i];
                int[] stats = { l.Dexterity, l.Power, l.Athletics, l.Endurance, l.Luck };
                for (int s = 0; s < stats.Length; s++)
                {
                    if (stats[s] < CharacterSheet.Baseline - 3)
                        problems.Add("loadout \"" + l.Id + "\" has " + (StatType)s + " " + stats[s]
                                     + ", below anything a migrated loadout could hold");
                }

                if (i > 0 && l.TotalStatPoints != all[0].TotalStatPoints)
                    problems.Add("loadout \"" + l.Id + "\" has " + l.TotalStatPoints + " stat points but \""
                                 + all[0].Id + "\" has " + all[0].TotalStatPoints);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Sheet(int[] stats, out CharacterSheet sheet)
        {
            var go = new GameObject("StatSubject");
            sheet = go.AddComponent<CharacterSheet>();
            for (int i = 0; i < stats.Length; i++) sheet.SetBaseStat((StatType)i, stats[i]);
            return go;
        }

        private static void Expect(List<string> problems, CharacterSheet sheet, Attr attr, float expected, string context)
        {
            float actual = sheet.Get(attr);
            if (Mathf.Abs(actual - expected) > Tolerance)
                problems.Add(attr + " is " + actual.ToString("0.####") + ", expected "
                             + expected.ToString("0.####") + context);
        }

        private static string Describe(int[] s) =>
            "DEX " + s[0] + " POW " + s[1] + " ATH " + s[2] + " END " + s[3] + " LCK " + s[4];
    }
}
#endif
