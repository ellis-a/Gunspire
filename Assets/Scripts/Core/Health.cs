using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Hit points, resistances, healing and death for anything that can be shot.
    /// This is the single place damage is actually resolved.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private Team team = Team.Enemy;
        [SerializeField] private float fallbackMaxHealth = 100f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float destroyDelay = 0f;

        private CharacterSheet _sheet;
        private StatusController _statusCache;
        private readonly Dictionary<DamageType, float> _resistances = new Dictionary<DamageType, float>();

        /// <summary>Looked up lazily so component add order does not matter when building entities in code.</summary>
        private StatusController Status
            => _statusCache != null ? _statusCache : (_statusCache = GetComponent<StatusController>());

        public float Current { get; private set; }
        public float Max { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public Team Team { get { return team; } set { team = value; } }
        public Transform Transform => transform;
        public float Fraction => Max <= 0f ? 0f : Mathf.Clamp01(Current / Max);

        /// <summary>The player keeps their body on death so the run-over screen has something to sit behind.</summary>
        public bool DestroyOnDeath { get { return destroyOnDeath; } set { destroyOnDeath = value; } }

        /// <summary>Seconds of remaining invulnerability, e.g. during a dash.</summary>
        public float InvulnerabilityTimer { get; set; }
        public bool IsInvulnerable => InvulnerabilityTimer > 0f;

        public event Action<DamageInfo, float> Damaged;
        public event Action<float> Healed;
        public event Action<DamageInfo> Died;
        public event Action HealthChanged;

        /// <summary>Global hooks. Boons and lifesteal listen here instead of patching every weapon.</summary>
        public static event Action<Health, DamageInfo, float> AnyDamaged;
        public static event Action<Health, DamageInfo> AnyDied;

        private void Awake()
        {
            _sheet = GetComponent<CharacterSheet>();

            Max = ComputeMax();
            Current = Max;

            if (_sheet != null) _sheet.Changed += OnSheetChanged;
        }

        private void OnDestroy()
        {
            if (_sheet != null) _sheet.Changed -= OnSheetChanged;
        }

        private void Update()
        {
            if (!IsAlive) return;

            if (InvulnerabilityTimer > 0f) InvulnerabilityTimer -= Time.deltaTime;

            if (_sheet != null && Current < Max)
            {
                float regen = _sheet.Get(Attr.HealthRegen);
                if (regen > 0f) Heal(regen * Time.deltaTime, silent: true);
            }
        }

        private float ComputeMax()
        {
            return _sheet != null ? _sheet.Get(Attr.MaxHealth) : fallbackMaxHealth;
        }

        private void OnSheetChanged()
        {
            float newMax = ComputeMax();
            if (Mathf.Approximately(newMax, Max)) return;

            // Growing the pool grants the extra hit points outright; shrinking just clamps.
            float delta = newMax - Max;
            Max = newMax;
            if (delta > 0f) Current += delta;
            Current = Mathf.Clamp(Current, 0f, Max);
            HealthChanged?.Invoke();
        }

        public void SetResistance(DamageType type, float value) => _resistances[type] = value;

        /// <summary>
        /// Total resistance to a school: this entity's own value plus anything the character
        /// sheet contributes. Negative is vulnerability, and clamps stop either extreme from
        /// making a target immune or one-shot.
        /// </summary>
        public float GetResistance(DamageType type)
        {
            if (!DamageTypes.IsResistable(type)) return 0f;

            _resistances.TryGetValue(type, out float own);
            float fromSheet = _sheet != null ? _sheet.Resistance(type) : 0f;
            return Mathf.Clamp(own + fromSheet, -0.9f, 0.9f);
        }

        /// <summary>Used when building enemies from code, before Awake stats exist.</summary>
        public void ConfigureMaxHealth(float value, bool refill = true)
        {
            fallbackMaxHealth = value;
            Max = ComputeMax();
            if (refill) Current = Max;
            HealthChanged?.Invoke();
        }

        // ---------------------------------------------------------------- damage

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;
            if (info.SourceTeam == team && team != Team.Neutral) return;   // no friendly fire
            if (IsInvulnerable && info.Type != DamageType.True) return;

            float amount = Mathf.Max(0f, info.Amount);

            if (info.Type != DamageType.True)
            {
                amount *= Mathf.Max(0f, 1f - GetResistance(info.Type));

                if (Status != null)
                {
                    // Frozen targets shatter.
                    if (Status.IsFrozen)
                    {
                        amount *= 1.5f;
                        Status.Remove(StatusId.Freeze);
                    }
                    amount *= 1f + Status.ConsumeMark();
                }

                if (_sheet != null) amount *= _sheet.Get(Attr.DamageTaken);
            }

            if (Status != null && info.Statuses != null)
                Status.ApplyAll(info.Statuses, info.Source, info.SourceTeam);

            if (amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);

            Damaged?.Invoke(info, amount);
            AnyDamaged?.Invoke(this, info, amount);
            HealthChanged?.Invoke();

            if (Current <= 0f) Die(info);
        }

        /// <summary>
        /// Spends health as a cost rather than taking damage. Skips resistances, statuses,
        /// invulnerability, lifesteal and the damage hooks, because none of those should apply
        /// to a price the player agreed to pay - a Fortify buff must not make a blood cost
        /// cheaper, and draining yourself must not proc your own on-hit effects.
        ///
        /// It can kill. Anything that would leave you dead should check first if that is not
        /// wanted.
        /// </summary>
        public float Drain(float amount)
        {
            if (!IsAlive || amount <= 0f) return 0f;

            float before = Current;
            Current = Mathf.Max(0f, Current - amount);
            float paid = before - Current;

            if (paid <= 0f) return 0f;

            HealthChanged?.Invoke();
            if (Current <= 0f) Die(DamageInfo.Create(0f, DamageType.True, Team.Neutral, null));

            return paid;
        }

        public float Heal(float amount, bool silent = false)
        {
            if (!IsAlive || amount <= 0f) return 0f;

            if (_sheet != null) amount *= _sheet.Get(Attr.HealingReceived);  // Blight drives this to zero
            if (amount <= 0f) return 0f;

            float before = Current;
            Current = Mathf.Min(Max, Current + amount);
            float healed = Current - before;

            if (healed > 0f)
            {
                if (!silent) Healed?.Invoke(healed);
                HealthChanged?.Invoke();
            }
            return healed;
        }

        /// <summary>Brings a dead entity back. Used when a new run reuses the player object.</summary>
        public void Revive(float startingHealth = -1f)
        {
            IsAlive = true;
            Max = ComputeMax();
            Current = startingHealth > 0f ? Mathf.Min(startingHealth, Max) : Max;
            InvulnerabilityTimer = 0.5f;
            HealthChanged?.Invoke();
        }

        public void Kill()
        {
            if (!IsAlive) return;
            Current = 0f;
            Die(DamageInfo.Create(0f, DamageType.True, Team.Neutral, null));
        }

        private void Die(DamageInfo info)
        {
            if (!IsAlive) return;
            IsAlive = false;

            if (Status != null) Status.ClearAll();

            Died?.Invoke(info);
            AnyDied?.Invoke(this, info);

            if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        }
    }
}
