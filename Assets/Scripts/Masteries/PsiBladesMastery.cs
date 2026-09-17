using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Psionic's mastery: gunfire charges a meter, and melee spends it. A quarter charge per bullet hit,
    /// at most once every quarter second, so one charge a second at most whatever the gun; a shotgun's
    /// pellets count as one hit in a window. Each melee attack with a charge available spends one for
    /// bonus psychic damage on everything it strikes. Two charges at one spell, one more per spell after.
    ///
    /// Bonus charge - the Phantasmal Mimic's hits - arrives through <see cref="AddBonus"/>, outside the
    /// quarter-second limit. Mind Spike and Force of Will spend charge through the psi cost.
    /// </summary>
    public class PsiBladesMastery : Mastery
    {
        public const float ChargePerHit = 0.25f;
        public const float HitWindow = 0.25f;
        public const int BaseMax = 2;

        /// <summary>Psychic damage a charged melee attack adds to each thing it strikes. A first guess.</summary>
        public const float MeleeBonusDamage = 12f;

        public override SpellSchool School => SpellSchool.Psionic;

        public float Charge { get; private set; }
        public int Max => MaxFor(Rank);

        public static int MaxFor(int rank) => rank <= 0 ? 0 : BaseMax + (rank - 1);

        /// <summary>A melee attack in progress spent a charge, and what it strikes takes the bonus.</summary>
        public bool MeleeEmpowered { get; private set; }

        // ---- set by boons, cleared each run ----

        /// <summary>Multiplies the charge a gun hit builds. Keen Mind.</summary>
        public float ChargeMultiplier = 1f;

        /// <summary>Extra psychic damage an empowered melee attack deals. Sharpened Will.</summary>
        public float MeleeBonusExtra;

        /// <summary>Charge spent, by any means. Overflow.</summary>
        public event System.Action<float> Spent;

        /// <summary>An empowered melee attack finished, with what it struck. The list is only valid during the call. Psychic Wave, Mind Break.</summary>
        public event System.Action<IReadOnlyList<Health>> EmpoweredMeleeLanded;

        private float _lastGainAt = float.NegativeInfinity;
        private readonly List<Health> _struck = new List<Health>();
        private Weapon _weapon;
        private PlayerCombat _combat;
        private bool _bound;

        public override void Bind(PlayerRig rig)
        {
            base.Bind(rig);
            if (_bound || rig == null) return;

            _weapon = rig.Weapon;
            _combat = rig.CombatInput;

            if (_weapon != null) _weapon.Hit += OnGunHit;
            if (_combat != null)
            {
                _combat.MeleeStarting += OnMeleeStarting;
                _combat.MeleeFinished += OnMeleeFinished;
            }
            Health.AnyDamaged += OnAnyDamaged;
            _bound = true;
        }

        public override void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            if (_weapon != null) _weapon.Hit -= OnGunHit;
            if (_combat != null)
            {
                _combat.MeleeStarting -= OnMeleeStarting;
                _combat.MeleeFinished -= OnMeleeFinished;
            }
            Health.AnyDamaged -= OnAnyDamaged;
        }

        // ---------------------------------------------------------------- charging

        /// <summary>
        /// Extra charge for a round landing on a split enemy, outside the quarter-second limit like the mimic's.
        /// Whether it should be was left open; it is, since inside the limit it adds nothing once you charge at the cap.
        /// </summary>
        public const float SplitEnemyBonus = 0.15f;

        private void OnGunHit(WeaponHit hit)
        {
            if (this == null || hit.IsPhantom) return;
            AddFromHit(Time.time);

            if (hit.Target != null && hit.Target.Transform != null && hit.Target.Transform.GetComponent<SplitMarker>() != null)
                AddBonus(SplitEnemyBonus);
        }

        /// <summary>A bullet hit at the given time. Gains nothing inside the quarter-second window.</summary>
        public bool AddFromHit(float now)
        {
            if (Rank <= 0 || Charge >= Max) return false;
            if (now - _lastGainAt < HitWindow) return false;

            _lastGainAt = now;
            Add(ChargePerHit * Mathf.Max(0f, ChargeMultiplier));
            return true;
        }

        /// <summary>Charge from outside the limit, such as the mimic's hits.</summary>
        public void AddBonus(float amount)
        {
            if (Rank > 0 && amount > 0f) Add(amount);
        }

        private void Add(float amount)
        {
            float before = Charge;
            Charge = Mathf.Min(Max, Charge + amount);
            if (!Mathf.Approximately(before, Charge)) RaiseChanged();
        }

        public bool TrySpend(float amount = 1f)
        {
            if (amount <= 0f) return true;
            if (Charge + 0.0001f < amount) return false;

            Charge = Mathf.Max(0f, Charge - amount);
            RaiseChanged();
            Spent?.Invoke(amount);
            return true;
        }

        protected override void OnRankChanged(int previous)
        {
            if (Charge > Max) Charge = Max;
        }

        public override void ResetForRun()
        {
            Charge = 0f;
            _lastGainAt = float.NegativeInfinity;
            MeleeEmpowered = false;
            _struck.Clear();
            ChargeMultiplier = 1f;
            MeleeBonusExtra = 0f;
            RaiseChanged();
        }

        // ---------------------------------------------------------------- spending on melee

        private void OnMeleeStarting(Spell spell)
        {
            if (this == null) return;

            _struck.Clear();
            MeleeEmpowered = Rank > 0 && TrySpend(1f);
        }

        /// <summary>
        /// Collects what the swing strikes rather than hitting it again from inside its own damage event,
        /// then deals the bonus once the swing is over.
        /// </summary>
        private void OnAnyDamaged(Health victim, DamageInfo info, float amount)
        {
            if (this == null || !MeleeEmpowered || Rig == null) return;
            if (info.Source != Rig.gameObject || info.Origin != DamageOrigin.Melee) return;
            if (victim == null || victim.Team == Team.Player || _struck.Contains(victim)) return;

            _struck.Add(victim);
        }

        private void OnMeleeFinished(Spell spell, bool cast)
        {
            if (this == null || !MeleeEmpowered) return;
            MeleeEmpowered = false;

            // A swing that aborted spent nothing, the charge included.
            if (!cast)
            {
                Add(1f);
                _struck.Clear();
                return;
            }

            for (int i = 0; i < _struck.Count; i++)
            {
                Health victim = _struck[i];
                if (victim == null || !victim.IsAlive) continue;

                DamageInfo bonus = DamageInfo.Create(MeleeBonusDamage + Mathf.Max(0f, MeleeBonusExtra), DamageType.Psychic,
                    Team.Player, Rig.gameObject);
                bonus.CanCrit = false;
                bonus.Origin = DamageOrigin.Mastery;
                victim.TakeDamage(bonus.At(victim.transform.position + Vector3.up, Vector3.up));
            }

            EmpoweredMeleeLanded?.Invoke(_struck);
            _struck.Clear();
        }
    }
}
