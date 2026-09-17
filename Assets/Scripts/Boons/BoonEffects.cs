using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Every effect a boon can have. Deliberately one class per idea rather than one class
    /// with a "what kind" enum: the Inspector's type picker lists these by name, so the list
    /// of boon effects is discoverable by opening the dropdown.
    /// </summary>
    public enum ModifierMode { Flat, Percent }

    // ---------------------------------------------------------------- character sheet

    /// <summary>The workhorse: +12% gun damage, +25 max health, +1 dash charge.</summary>
    [System.Serializable]
    public class ModifyAttributeEffect : BoonEffect
    {
        public Attr Attribute = Attr.MoveSpeed;
        public ModifierMode Mode = ModifierMode.Percent;
        public float Amount = 0.10f;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet == null) return;

            if (Mode == ModifierMode.Percent) run.Sheet.AddPercent(Attribute, Amount, "boon");
            else run.Sheet.AddFlat(Attribute, Amount, "boon");
        }

        public override string Describe()
        {
            string value = Mode == ModifierMode.Percent
                ? (Amount * 100f).ToString("+0.#;-0.#") + "%"
                : Amount.ToString("+0.##;-0.##");
            return value + " " + Attribute;
        }
    }

    /// <summary>Core stat points, which feed the derived attributes rather than replacing them.</summary>
    [System.Serializable]
    public class ModifyStatEffect : BoonEffect
    {
        public StatType Stat = StatType.Dexterity;
        public int Points = 3;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet != null) run.Sheet.AddStat(Stat, Points);
        }

        public override string Describe() => Points.ToString("+0;-0") + " " + Stat;
    }

    [System.Serializable]
    public class ModifyResistanceEffect : BoonEffect
    {
        public DamageType School = DamageType.Energy;
        public float Amount = 0.15f;

        /// <summary>Applies to every elemental school instead of the one named above.</summary>
        public bool AllElemental;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet == null) return;

            if (!AllElemental)
            {
                run.Sheet.AddResistance(School, Amount, "boon");
                return;
            }

            // Walks the registry, so a new damage school is covered without touching this.
            for (int i = 0; i < DamageTypes.Elemental.Length; i++)
                run.Sheet.AddResistance(DamageTypes.Elemental[i], Amount, "boon");
        }

        public override string Describe() =>
            (Amount * 100f).ToString("+0.#;-0.#") + "% "
            + (AllElemental ? "all elemental" : DamageTypes.Name(School)) + " resistance";
    }

    /// <summary>Damage dealt of one school, from any source. The mastery boons.</summary>
    [System.Serializable]
    public class ModifyDamageSchoolEffect : BoonEffect
    {
        public DamageType School = DamageType.Energy;
        public float Percent = 0.18f;

        /// <summary>Applies to every elemental school instead of the one named above.</summary>
        public bool AllElemental;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet == null) return;

            if (!AllElemental)
            {
                run.Sheet.AddDamagePercent(School, Percent, "boon");
                return;
            }

            for (int i = 0; i < DamageTypes.Elemental.Length; i++)
                run.Sheet.AddDamagePercent(DamageTypes.Elemental[i], Percent, "boon");
        }

        public override string Describe() =>
            (Percent * 100f).ToString("+0.#;-0.#") + "% "
            + (AllElemental ? "all elemental" : DamageTypes.Name(School)) + " damage";
    }

    /// <summary>Damage from one category of spell.</summary>
    [System.Serializable]
    public class ModifySpellCategoryEffect : BoonEffect
    {
        public SpellType Category = SpellType.Attack;
        public float Percent = 0.22f;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet != null) run.Sheet.AddSpellPercent(Category, Percent, "boon");
        }

        public override string Describe() =>
            (Percent * 100f).ToString("+0.#;-0.#") + "% " + Category + " spell power";
    }

    // ---------------------------------------------------------------- run state

    /// <summary>Puts a status on everything your bullets, or your spells, land on.</summary>
    [System.Serializable]
    public class AddOnHitStatusEffect : BoonEffect
    {
        public StatusApplication Status = new StatusApplication(StatusId.Burn, 3.5f, 1, 4f);

        /// <summary>False puts it on bullets, true on spells.</summary>
        public bool OnSpells;

        public override void Apply(RunState run, int level)
        {
            if (OnSpells) run.SpellStatuses.Add(Status);
            else run.BulletStatuses.Add(Status);
        }

        public override string Describe() =>
            (OnSpells ? "spells" : "bullets") + " apply " + Status.Id;
    }

    /// <summary>A one-off heal on pickup, for boons that also raise maximum health.</summary>
    [System.Serializable]
    public class FullHealEffect : BoonEffect
    {
        public override void Apply(RunState run, int level)
        {
            if (run.Player != null && run.Player.Health != null)
                run.Player.Health.Heal(run.Player.Health.Max);
        }

        public override string Describe() => "heals you fully";
    }

    /// <summary>
    /// Levels up every spell currently known. Applies again on each pick, so it keeps paying
    /// out as the spell book grows rather than being worth less the earlier it is taken.
    /// </summary>
    [System.Serializable]
    public class LevelUpKnownSpellsEffect : BoonEffect
    {
        public override void Apply(RunState run, int level)
        {
            SpellBook book = run.Player != null ? run.Player.Book : null;
            if (book == null) return;

            System.Collections.Generic.IReadOnlyList<Spell> known = book.Known;
            for (int i = 0; i < known.Count; i++) book.LevelUp(known[i]);
        }

        public override string Describe() => "levels up every spell you know";
    }
}
