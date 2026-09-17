using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Abyssal's mastery: life spent on costs becomes debt, and kills repay it.
    ///
    /// - 1 spell: health costs are recorded as debt, and each kill repays some of it at face value.
    /// - 2: repayment comes back with interest.
    /// - 3: carrying debt raises spell power in proportion to it.
    /// - 4: repayment past full health becomes a temporary shield.
    ///
    /// It can never kill: health costs are refused below one hit point, in <see cref="SpellCosts"/>.
    /// Every number here is a placeholder; the Blood Debt's numbers are still open.
    /// </summary>
    public class BloodDebtMastery : Mastery
    {
        /// <summary>Debt one kill repays.</summary>
        public const float RepayPerKill = 20f;

        /// <summary>Extra healing on repayment from two spells, as a fraction of what was repaid.</summary>
        public const float Interest = 0.5f;

        /// <summary>Spell power per point of debt carried, from three spells.</summary>
        public const float SpellPowerPerDebt = 0.005f;

        public const float ShieldSeconds = 6f;

        public override SpellSchool School => SpellSchool.Abyssal;

        public float Debt { get; private set; }

        // ---- set by boons, cleared each run ----

        /// <summary>Multiplies the interest repayment earns. Low Interest.</summary>
        public float InterestMultiplier = 1f;

        /// <summary>Extra debt each kill repays. Deep Pockets.</summary>
        public float RepayBonus;

        /// <summary>A kill repaid debt: how much, and whether that cleared it. Foreclosure, Tidal Surge.</summary>
        public event System.Action<float, bool> Repaid;

        private StatModifier _power;
        private bool _bound;

        public override void Bind(PlayerRig rig)
        {
            base.Bind(rig);
            if (_bound) return;

            Health.AnyDied += OnAnyDied;
            _bound = true;
        }

        public override void Unbind()
        {
            if (!_bound) return;
            Health.AnyDied -= OnAnyDied;
            _bound = false;
        }

        /// <summary>Health paid as a cost. Only becomes debt while the mastery is held.</summary>
        public void Record(float paid)
        {
            if (Rank <= 0 || paid <= 0f) return;

            Debt += paid;
            RefreshPower();
            RaiseChanged();
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (this == null) return;
            if (RunState.CountsAsKill(victim)) RepayOnKill();
        }

        /// <summary>One kill's repayment. Returns the healing it was worth.</summary>
        public float RepayOnKill()
        {
            if (Debt <= 0f || Rig == null || Rig.Health == null || !Rig.Health.IsAlive) return 0f;

            float repaid = Mathf.Min(Debt, RepayPerKill + Mathf.Max(0f, RepayBonus));
            Debt -= repaid;

            float owed = repaid * (Rank >= 2 ? 1f + Interest * Mathf.Max(0f, InterestMultiplier) : 1f);
            float missing = Rig.Health.Max - Rig.Health.Current;
            Rig.Health.Heal(owed);

            float over = owed - missing;
            if (Rank >= 4 && over > 0f) Rig.Health.AddShield(over, ShieldSeconds);

            RefreshPower();
            RaiseChanged();
            Repaid?.Invoke(repaid, Debt <= 0f);
            return owed;
        }

        protected override void OnRankChanged(int previous) => RefreshPower();

        public override void ResetForRun()
        {
            Debt = 0f;
            InterestMultiplier = 1f;
            RepayBonus = 0f;
            RefreshPower();
            RaiseChanged();
        }

        private void RefreshPower()
        {
            if (Rig == null) return;
            SetPercent(Rig.Sheet, ref _power, Attr.SpellPower, Rank >= 3 ? Debt * SpellPowerPerDebt : 0f, this, "Blood Debt");
        }
    }
}
