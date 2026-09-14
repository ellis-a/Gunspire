#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// The numbers behind the stat rework's migrations.
    ///
    /// Every shipped loadout opened at 3 in each old stat. The formulas were rebased so the new
    /// baseline of 10 gives exactly what 3 used to, which makes 7 the distance between an old
    /// stat value and the new one, and 3 the stat value to fold any per-point bonus at.
    /// </summary>
    internal static class StatRework
    {
        public const int OldLoadoutStat = 3;
        public static int Offset => CharacterSheet.Baseline - OldLoadoutStat;
    }

    /// <summary>
    /// Loadouts store each stat under a field named after it. Renames the four reworked stats to
    /// the ones that took their positions, and moves every value up by the rebasing offset so
    /// each class plays exactly as before.
    ///
    /// Luck keeps its name but still needs the offset, so a file is only touched while it still
    /// holds an old stat name. That is what stops a second run adding seven again.
    /// </summary>
    public class LoadoutStatRename : TextMigration
    {
        private static readonly Dictionary<string, string> Renames = new Dictionary<string, string>
        {
            { "Strength", "Dexterity" },
            { "Intellect", "Power" },
            { "Agility", "Athletics" },
            { "Vitality", "Endurance" },
            { "Luck", "Luck" }
        };

        private static readonly Regex OldName =
            new Regex(@"^[ \t]+(Strength|Intellect|Agility|Vitality):[ \t]*-?\d+", RegexOptions.Multiline);

        private static readonly Regex StatLine =
            new Regex(@"^([ \t]+)(Strength|Intellect|Agility|Vitality|Luck):([ \t]*)(-?\d+)", RegexOptions.Multiline);

        public override string Name => "Loadout stats renamed and rebased";
        public override string Folder => "Loadouts";

        public override string Migrate(string path, string text, List<string> changes)
        {
            if (!OldName.IsMatch(text)) return text;

            string file = Path.GetFileNameWithoutExtension(path);
            return StatLine.Replace(text, match =>
            {
                string oldName = match.Groups[2].Value;
                int oldValue = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
                string newName = Renames[oldName];
                int newValue = oldValue + StatRework.Offset;

                changes.Add(file + ": " + oldName + " " + oldValue + " becomes " + newName + " " + newValue);
                return match.Groups[1].Value + newName + ":" + match.Groups[3].Value + newValue;
            });
        }
    }

    /// <summary>
    /// The sweep selector used to add reach per point of Intellect. That field is gone, so an asset
    /// still holding it would quietly lose the reach. Folds the reach it gave a loadout at the old
    /// stat of 3 into the base distance, and deletes the orphaned line.
    /// </summary>
    public class SweepIntellectFold : TextMigration
    {
        private static readonly Regex PerIntellect =
            new Regex(@"^[ \t]*PerIntellect:[ \t]*(-?[\d.]+)[ \t]*\r?\n", RegexOptions.Multiline);

        private static readonly Regex BaseDistance =
            new Regex(@"^([ \t]*BaseDistance:[ \t]*)(-?[\d.]+)", RegexOptions.Multiline);

        public override string Name => "Sweep reach per Intellect folded into base distance";
        public override string Folder => "";

        public override string Migrate(string path, string text, List<string> changes)
        {
            MatchCollection perPoint = PerIntellect.Matches(text);
            if (perPoint.Count == 0) return text;

            string file = Path.GetFileNameWithoutExtension(path);
            MatchCollection bases = BaseDistance.Matches(text);

            if (perPoint.Count != 1 || bases.Count != 1)
            {
                changes.Add(file + ": " + perPoint.Count + " PerIntellect and " + bases.Count
                            + " BaseDistance entries - too ambiguous to fold automatically, do it by hand");
                return text;
            }

            float per = float.Parse(perPoint[0].Groups[1].Value, CultureInfo.InvariantCulture);
            float oldBase = float.Parse(bases[0].Groups[2].Value, CultureInfo.InvariantCulture);
            float newBase = oldBase + StatRework.OldLoadoutStat * per;

            changes.Add(file + ": BaseDistance " + oldBase.ToString(CultureInfo.InvariantCulture)
                        + " plus 3 x " + per.ToString(CultureInfo.InvariantCulture) + " becomes "
                        + newBase.ToString("0.###", CultureInfo.InvariantCulture));

            string updated = BaseDistance.Replace(text,
                match => match.Groups[1].Value + newBase.ToString("0.###", CultureInfo.InvariantCulture), 1);
            return PerIntellect.Replace(updated, "", 1);
        }
    }

    /// <summary>
    /// No spell asset stored a school, so every authored spell read as Elemental whatever its
    /// built-in said. Writes each asset the school its built-in declares. Only assets with no
    /// school stored at all are touched, since one that has a school was set deliberately.
    /// </summary>
    public class SpellSchoolsFromCode : ObjectMigration
    {
        private static readonly Regex HasSchool = new Regex(@"^[ \t]+School:", RegexOptions.Multiline);

        public override string Name => "Spell schools written from the code roster";

        public override void Migrate(bool apply, List<string> changes)
        {
            var builtIn = new Dictionary<string, Spell>();
            foreach (Spell spell in SpellLibrary.BuiltIn()) builtIn[spell.Id] = spell;

            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                if (asset.Definition == null) continue;

                string path = AssetDatabase.GetAssetPath(asset);
                if (HasSchool.IsMatch(File.ReadAllText(path))) continue;

                if (!builtIn.TryGetValue(asset.Definition.Id, out Spell source))
                {
                    changes.Add(asset.Definition.Id + ": no built-in to take a school from - set it in the Inspector");
                    continue;
                }

                changes.Add(asset.Definition.Id + ": school " + source.School);
                if (!apply) continue;

                asset.Definition.School = source.School;
                EditorUtility.SetDirty(asset);
            }
        }
    }

    /// <summary>
    /// Melee damage used to add so much per point of Strength, and to carry smash power for
    /// reinforced barriers. Both are gone, and spells already scale with Power through spell
    /// power, so the per-point bonus is folded into the flat amount at the old loadout stat of 3.
    ///
    /// Keyed to the smash power field, which only an asset authored before the rework still
    /// holds. A spell authored since that scales on a stat on purpose is never touched. Bash also
    /// loses its knockback here, since it became a Petty spell that only deals damage.
    /// </summary>
    public class MeleeStrengthFold : ObjectMigration
    {
        private static readonly Regex HasSmashPower = new Regex(@"^[ \t]+UseSmashPower:", RegexOptions.Multiline);

        public override string Name => "Melee Strength scaling folded into flat damage";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                if (asset.Definition == null) continue;

                string path = AssetDatabase.GetAssetPath(asset);
                if (!HasSmashPower.IsMatch(File.ReadAllText(path))) continue;

                bool touched = false;
                foreach (AbilityEffect effect in asset.Definition.OnCast)
                {
                    if (!(effect is DealDamageEffect damage)) continue;

                    if (damage.PerStatPoint != 0f)
                    {
                        float folded = damage.Amount + StatRework.OldLoadoutStat * damage.PerStatPoint;
                        changes.Add(asset.Definition.Id + ": damage " + damage.Amount + " plus 3 x "
                                    + damage.PerStatPoint + " becomes " + folded + " flat");
                        if (apply)
                        {
                            damage.Amount = folded;
                            damage.PerStatPoint = 0f;
                        }
                    }

                    if (asset.Definition.Id == "bash" && damage.Knockback != 0f)
                    {
                        changes.Add("bash: knockback " + damage.Knockback + " removed");
                        if (apply) damage.Knockback = 0f;
                    }

                    touched = true;
                }

                // Saving drops the orphaned smash power line even when nothing else changed,
                // which is what makes this migration finish rather than report forever.
                if (!touched) changes.Add(asset.Definition.Id + ": orphaned smash power field removed");
                if (apply) EditorUtility.SetDirty(asset);
            }
        }
    }

    /// <summary>
    /// Specific values the rework changed on specific spells. Each is only changed while the asset
    /// still holds the exact old value, so anything retuned in the Inspector since is left alone,
    /// and a second run finds nothing.
    /// </summary>
    public class SpellRetune : ObjectMigration
    {
        private const string OldDash = "A short burst in the direction you are moving, with a sliver of " +
                                       "invulnerability. Charges recover on their own, and Agility grants more.";
        private const string NewDash = "A short burst in the direction you are moving, with a sliver of " +
                                       "invulnerability. Charges recover on their own.";

        private const string OldBash = "A close swing that scales on Strength. The smash power behind it is " +
                                       "what opens reinforced barriers, so it stays useful with nothing to hit.";
        private const string NewBash = "A quick, close swing. Cheap enough to fall back on when everything else " +
                                       "is spent.";

        private const string OldSlam = "A shockwave around you that hurls enemies back and shatters weak barriers.";
        private const string NewSlam = "A shockwave around you that hurls enemies back.";

        public override string Name => "Spell text and rarity retuned for the rework";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (SpellAsset asset in AssetsOf<SpellAsset>.All())
            {
                Spell spell = asset.Definition;
                if (spell == null) continue;

                bool changed = false;
                switch (spell.Id)
                {
                    case "dash":         changed = Description(spell, OldDash, NewDash, apply, changes); break;
                    case "bash":         changed = Description(spell, OldBash, NewBash, apply, changes); break;
                    case "kinetic_slam": changed = Description(spell, OldSlam, NewSlam, apply, changes); break;

                    case "blink":
                        if (spell.Rarity == Rarity.Uncommon)
                        {
                            changes.Add("blink: rarity Uncommon becomes Common");
                            if (apply) spell.Rarity = Rarity.Common;
                            changed = true;
                        }
                        break;
                }

                if (changed && apply) EditorUtility.SetDirty(asset);
            }
        }

        private static bool Description(Spell spell, string oldText, string newText, bool apply, List<string> changes)
        {
            if (spell.Description != oldText) return false;

            changes.Add(spell.Id + ": description no longer mentions removed stats or barriers");
            if (apply) spell.Description = newText;
            return true;
        }
    }

    /// <summary>
    /// The stat boons keep their stored stat number, which now points at the renamed stat. Their
    /// descriptions still name the old ones, and assets override the code text, so they are
    /// rewritten here - only while they still hold the exact old wording.
    /// </summary>
    public class BoonStatText : ObjectMigration
    {
        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
        {
            { "titan_grip", new[] { "+3 Strength. Heavier blows, and heavier things you can break.",
                                    "+3 Dexterity. Tighter spread, less recoil and faster reloads." } },
            { "scholar", new[] { "+3 Intellect.", "+3 Power." } },
            { "windrunner", new[] { "+3 Agility.", "+3 Athletics." } },
            { "second_wind", new[] { "+2 Vitality, and heal to full right now.",
                                     "+2 Endurance, and heal to full right now." } }
        };

        public override string Name => "Stat boon descriptions renamed";

        public override void Migrate(bool apply, List<string> changes)
        {
            foreach (BoonAsset asset in AssetsOf<BoonAsset>.All())
            {
                Boon boon = asset.Boon;
                if (boon == null || !Text.TryGetValue(boon.Id, out string[] pair)) continue;
                if (boon.Description != pair[0]) continue;

                changes.Add(boon.Id + ": \"" + pair[0] + "\" becomes \"" + pair[1] + "\"");
                if (!apply) continue;

                boon.Description = pair[1];
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
#endif
