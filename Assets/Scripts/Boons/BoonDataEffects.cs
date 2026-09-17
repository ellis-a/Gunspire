using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Effects for the boons that are pure numbers. Each adds its per-level increment on every pick, onto
    // something a new run starts fresh (the sheet, the run, the masteries), so none of them needs undoing.

    public enum SchoolChannel { Damage, Cost, Cooldown }

    /// <summary>One school's spell damage, cost or cooldown rate. The template school Commons.</summary>
    [System.Serializable]
    public class ModifySchoolEffect : BoonEffect
    {
        public SpellSchool School = SpellSchool.Elemental;
        public SchoolChannel Channel = SchoolChannel.Damage;

        /// <summary>Per level. Negative for a cost cut.</summary>
        public float Amount = 0.08f;

        public override void Apply(RunState run, int level)
        {
            CharacterSheet sheet = run.Sheet;
            if (sheet == null) return;

            switch (Channel)
            {
                case SchoolChannel.Damage: sheet.AddSchoolDamage(School, Amount, this); break;
                case SchoolChannel.Cost: sheet.AddSchoolCost(School, Amount, this); break;
                default: sheet.AddSchoolCooldown(School, Amount, this); break;
            }
        }

        public override string Describe() =>
            (Amount * 100f).ToString("+0.#;-0.#") + "% " + School + " " + Channel.ToString().ToLowerInvariant();
    }

    /// <summary>An attribute for one class of gun only. The class Commons.</summary>
    [System.Serializable]
    public class ModifyGunClassEffect : BoonEffect
    {
        public WeaponClass Class = WeaponClass.Handgun;
        public Attr Attribute = Attr.ReloadSpeed;
        public ModifierMode Mode = ModifierMode.Percent;
        public float Amount = 0.1f;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet == null) return;

            StatModifier mod = Mode == ModifierMode.Percent
                ? StatModifier.Percent(Attribute, Amount, this)
                : StatModifier.Flat(Attribute, Amount, this);
            run.Sheet.AddClassModifier(Class, mod);
        }

        public override string Describe()
        {
            string value = Mode == ModifierMode.Percent
                ? (Amount * 100f).ToString("+0.#;-0.#") + "%"
                : Amount.ToString("+0.##;-0.##");
            return value + " " + Attribute + " for " + Class + "s";
        }
    }

    /// <summary>
    /// How spells' statuses and zones come out: their amount, their duration, or a zone's duration. A school or
    /// status left unset means every one.
    /// </summary>
    [System.Serializable]
    public class ModifySpellEffectEffect : BoonEffect
    {
        public SpellEffectChannel Channel = SpellEffectChannel.StatusAmount;

        public bool AnySchool;
        public SpellSchool School = SpellSchool.Elemental;

        /// <summary>The statuses it touches. Empty means every status.</summary>
        public List<StatusId> Statuses = new List<StatusId>();

        public float Amount = 0.1f;

        public override void Apply(RunState run, int level)
        {
            if (run.Sheet == null) return;

            SpellSchool? school = AnySchool ? (SpellSchool?)null : School;
            if (Statuses == null || Statuses.Count == 0)
            {
                run.Sheet.AddSpellEffect(Channel, school, null, Amount, this);
                return;
            }

            for (int i = 0; i < Statuses.Count; i++)
                run.Sheet.AddSpellEffect(Channel, school, Statuses[i], Amount, this);
        }

        public override string Describe() =>
            (Amount * 100f).ToString("+0.#;-0.#") + "% " + Channel + " for " + (AnySchool ? "every school" : School.ToString())
            + (Statuses != null && Statuses.Count > 0 ? " (" + string.Join(", ", Statuses) + ")" : "");
    }

    /// <summary>What everything summoned for the player gets. Master Summoner and the companion Commons.</summary>
    [System.Serializable]
    public class ModifyAlliesEffect : BoonEffect
    {
        public float Health;
        public float Damage;
        public float MoveSpeed;
        public float CompanionHealth;
        public float CompanionDamage;
        public float CompanionMoveSpeed;
        public float UndeadHealth;

        public override void Apply(RunState run, int level)
        {
            AllyBoosts allies = run.Allies;
            allies.Health += Health;
            allies.Damage += Damage;
            allies.MoveSpeed += MoveSpeed;
            allies.CompanionHealth += CompanionHealth;
            allies.CompanionDamage += CompanionDamage;
            allies.CompanionMoveSpeed += CompanionMoveSpeed;
            allies.UndeadHealth += UndeadHealth;
        }

        public override string Describe()
        {
            var parts = new List<string>();
            if (Health != 0f) parts.Add(Pct(Health) + " ally health");
            if (Damage != 0f) parts.Add(Pct(Damage) + " ally damage");
            if (MoveSpeed != 0f) parts.Add(Pct(MoveSpeed) + " ally speed");
            if (CompanionHealth != 0f) parts.Add(Pct(CompanionHealth) + " companion health");
            if (CompanionDamage != 0f) parts.Add(Pct(CompanionDamage) + " companion damage");
            if (CompanionMoveSpeed != 0f) parts.Add(Pct(CompanionMoveSpeed) + " companion speed");
            if (UndeadHealth != 0f) parts.Add(Pct(UndeadHealth) + " undead health");
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        }

        private static string Pct(float v) => (v * 100f).ToString("+0.#;-0.#") + "%";
    }

    public enum RunSetting { ExtraOfferChoices, ExtraBoonPicks, OfferRerolls, ShopPriceScale, ShopExtraItems }

    /// <summary>A setting on the run: offer size and picks, rerolls, and what the shop will read.</summary>
    [System.Serializable]
    public class RunSettingEffect : BoonEffect
    {
        public RunSetting Setting = RunSetting.ExtraBoonPicks;

        /// <summary>Per level. Whole numbers for counts; a share for the price scale, which it lowers.</summary>
        public float Amount = 1f;

        public override void Apply(RunState run, int level)
        {
            int whole = Mathf.RoundToInt(Amount);
            switch (Setting)
            {
                case RunSetting.ExtraOfferChoices: run.ExtraOfferChoices += whole; break;
                case RunSetting.ExtraBoonPicks: run.ExtraBoonPicks += whole; break;
                case RunSetting.OfferRerolls: run.OfferRerolls += whole; break;
                case RunSetting.ShopPriceScale: run.ShopPriceScale = Mathf.Max(0f, run.ShopPriceScale - Amount); break;
                default: run.ShopExtraItems += whole; break;
            }
        }

        public override string Describe() => Setting + " " + Amount.ToString("+0.##;-0.##");
    }

    public enum MasterySetting
    {
        SoulCap, DebtInterest, DebtRepay, PsiCharge, PsiMeleeDamage, WarpRate, WarpManaPerHit, WarpLinger,
        KnowledgeRange, KnowledgeWarning, ConfluxKeepsElements, ConfluxCooldown
    }

    /// <summary>A setting on one mastery. The school Commons that change a mastery's numbers, and some Rares.</summary>
    [System.Serializable]
    public class MasterySettingEffect : BoonEffect
    {
        public MasterySetting Setting = MasterySetting.SoulCap;

        /// <summary>Per level. For a multiplier, added to it; for Conflux's cooldown, taken from it.</summary>
        public float Amount = 1f;

        /// <summary>The first level this applies at. A second-level upgrade sets it to 2.</summary>
        public int FromLevel = 1;

        public override void Apply(RunState run, int level)
        {
            if (level < FromLevel) return;
            MasteryHost host = run.Player != null ? run.Player.Masteries : null;
            if (host == null) return;

            switch (Setting)
            {
                case MasterySetting.SoulCap:
                    Adjust<SoulsMastery>(host, m => m.CapBonus += Mathf.RoundToInt(Amount));
                    break;
                case MasterySetting.DebtInterest:
                    Adjust<BloodDebtMastery>(host, m => m.InterestMultiplier += Amount);
                    break;
                case MasterySetting.DebtRepay:
                    Adjust<BloodDebtMastery>(host, m => m.RepayBonus += Amount);
                    break;
                case MasterySetting.PsiCharge:
                    Adjust<PsiBladesMastery>(host, m => m.ChargeMultiplier += Amount);
                    break;
                case MasterySetting.PsiMeleeDamage:
                    Adjust<PsiBladesMastery>(host, m => m.MeleeBonusExtra += Amount);
                    break;
                case MasterySetting.WarpRate:
                    Adjust<ArcaneWarpMastery>(host, m => { m.RateMultiplier += Amount; m.Refresh(); });
                    break;
                case MasterySetting.WarpManaPerHit:
                    Adjust<ArcaneWarpMastery>(host, m => m.ManaPerHitBonus += Amount);
                    break;
                case MasterySetting.WarpLinger:
                    Adjust<ArcaneWarpMastery>(host, m => m.LingerSeconds += Amount);
                    break;
                case MasterySetting.KnowledgeRange:
                    Adjust<DivineKnowledgeMastery>(host, m => m.RangeMultiplier += Amount);
                    break;
                case MasterySetting.KnowledgeWarning:
                    Adjust<DivineKnowledgeMastery>(host, m => m.WarningLead += Amount);
                    break;
                case MasterySetting.ConfluxKeepsElements:
                    Adjust<ConfluxMastery>(host, m => m.KeepsElements = true);
                    break;
                case MasterySetting.ConfluxCooldown:
                    Adjust<ConfluxMastery>(host, m => m.ReactionCooldownScale = Mathf.Max(0.1f, m.ReactionCooldownScale - Amount));
                    break;
            }
        }

        private static void Adjust<T>(MasteryHost host, System.Action<T> change) where T : Mastery
        {
            T mastery = host.Get<T>();
            if (mastery != null) change(mastery);
        }

        public override string Describe() => Setting + " " + Amount.ToString("+0.##;-0.##");
    }
}
