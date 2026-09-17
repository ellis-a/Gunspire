using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Aetherics' mastery: an empty mana pool turns into speed. Every missing mana point raises attack
    /// speed and reload speed, by 0.5% at one spell and a quarter of a percent more for each spell after.
    /// A bigger pool is meant to make it better. It is stateless: the bonus is read off the pool
    /// whenever the pool changes.
    ///
    /// Your own gun's hits pull it back: a trigger pull is worth ten mana per second of the gun's base
    /// fire rate, shared across the rounds it fires, and each round that lands restores its share once,
    /// however many things it strikes. Phantom and echoed rounds restore nothing.
    /// </summary>
    public class ArcaneWarpMastery : Mastery
    {
        public const float ManaPerSecondOfFire = 10f;

        private const int RememberedRounds = 128;

        public override SpellSchool School => SpellSchool.Aetherics;

        /// <summary>Bonus per missing mana point at a rank: 0.5%, 0.75%, 1%, 1.25%.</summary>
        public static float RateFor(int rank) => rank <= 0 ? 0f : 0.005f + 0.0025f * (rank - 1);

        /// <summary>Mana one round of this gun restores on landing.</summary>
        public static float ManaPerRound(WeaponDefinition def)
            => def == null ? 0f : ManaPerSecondOfFire * def.SecondsBetweenShots / Mathf.Max(1, def.RoundsPerTrigger);

        // ---- set by boons, cleared each run ----

        /// <summary>Multiplies the bonus per missing mana point. Wide Void.</summary>
        public float RateMultiplier = 1f;

        /// <summary>Extra mana each landed round restores. Aether Tap.</summary>
        public float ManaPerHitBonus;

        /// <summary>How long the last bonus lingers after the pool refills. Void Pocket.</summary>
        public float LingerSeconds;

        /// <summary>The bonus fell to nothing: the pool refilled and any linger ran out.</summary>
        public event System.Action BonusEnded;

        /// <summary>The bonus from the pool as it is right now, before any linger.</summary>
        public float LiveBonus
        {
            get
            {
                Mana mana = Rig != null ? Rig.Mana : null;
                return mana == null ? 0f : Mathf.Max(0f, mana.Max - mana.Current) * RateFor(Rank) * Mathf.Max(0f, RateMultiplier);
            }
        }

        /// <summary>The bonus in effect: the live one, or the last one while it lingers after a refill.</summary>
        public float Bonus => LiveBonus > 0f ? LiveBonus : (_lingerLeft > 0f ? _lastBonus : 0f);

        private float _lastBonus;
        private float _lingerLeft;
        private bool _hadBonus;

        private StatModifier _attackSpeed;
        private StatModifier _reloadSpeed;
        private Weapon _weapon;
        private Mana _mana;
        private bool _bound;

        private readonly HashSet<int> _paidRounds = new HashSet<int>();
        private readonly Queue<int> _paidOrder = new Queue<int>();

        public override void Bind(PlayerRig rig)
        {
            base.Bind(rig);
            if (_bound || rig == null) return;

            _weapon = rig.Weapon;
            _mana = rig.Mana;

            if (_mana != null) _mana.Changed += Refresh;
            if (_weapon != null) _weapon.Hit += OnHit;
            _bound = true;
        }

        public override void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            if (_mana != null) _mana.Changed -= Refresh;
            if (_weapon != null) _weapon.Hit -= OnHit;
        }

        /// <summary>Puts the current bonus on the sheet. Public so tooling can apply it without waiting for the pool to move.</summary>
        public void Refresh()
        {
            if (this == null || Rig == null) return;

            // A linger holds the strongest bonus since the pool was last full, not the sliver left as it refills.
            float live = LiveBonus;
            if (live > 0f)
            {
                _lastBonus = Mathf.Max(_lastBonus, live);
                _lingerLeft = Mathf.Max(0f, LingerSeconds);
            }
            else if (_lingerLeft <= 0f)
            {
                _lastBonus = 0f;
            }

            float bonus = Bonus;
            SetPercent(Rig.Sheet, ref _attackSpeed, Attr.AttackSpeed, bonus, this, "Arcane Warp");
            SetPercent(Rig.Sheet, ref _reloadSpeed, Attr.ReloadSpeed, bonus, this, "Arcane Warp");

            if (_hadBonus && bonus <= 0f) BonusEnded?.Invoke();
            _hadBonus = bonus > 0f;
        }

        /// <summary>Runs a linger down. Update calls it; public so tooling can step time.</summary>
        public void TickLinger(float dt)
        {
            if (_lingerLeft <= 0f || LiveBonus > 0f) return;

            _lingerLeft = Mathf.Max(0f, _lingerLeft - dt);
            if (_lingerLeft <= 0f) Refresh();
        }

        private void Update()
        {
            if (Application.isPlaying) TickLinger(Time.deltaTime);
        }

        private void OnHit(WeaponHit hit)
        {
            if (this == null || Rank <= 0 || _mana == null) return;
            if (hit.IsPhantom || hit.IsEcho || hit.Weapon != _weapon) return;
            if (!MarkPaid(hit.Round)) return;

            _mana.Add(ManaPerRound(hit.Weapon.Definition) + Mathf.Max(0f, ManaPerHitBonus));
        }

        /// <summary>False when this round has already restored its share.</summary>
        private bool MarkPaid(int round)
        {
            if (!_paidRounds.Add(round)) return false;

            _paidOrder.Enqueue(round);
            while (_paidOrder.Count > RememberedRounds) _paidRounds.Remove(_paidOrder.Dequeue());
            return true;
        }

        protected override void OnRankChanged(int previous) => Refresh();

        public override void ResetForRun()
        {
            _paidRounds.Clear();
            _paidOrder.Clear();
            RateMultiplier = 1f;
            ManaPerHitBonus = 0f;
            LingerSeconds = 0f;
            _lingerLeft = 0f;
            _lastBonus = 0f;
            Refresh();
        }
    }
}
