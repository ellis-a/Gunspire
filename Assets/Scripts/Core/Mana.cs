using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>Spell resource. Regenerates continuously, with a short pause after casting.</summary>
    [DisallowMultipleComponent]
    public class Mana : MonoBehaviour
    {
        [SerializeField] private float regenDelayAfterSpend = 0.75f;

        private CharacterSheet _sheet;
        private float _regenLockout;

        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Fraction => Max <= 0f ? 0f : Mathf.Clamp01(Current / Max);

        public event Action Changed;

        private void Awake()
        {
            _sheet = GetComponent<CharacterSheet>();
            Max = _sheet != null ? _sheet.Get(Attr.MaxMana) : 100f;
            Current = Max;
            if (_sheet != null) _sheet.Changed += OnSheetChanged;
        }

        private void OnDestroy()
        {
            if (_sheet != null) _sheet.Changed -= OnSheetChanged;
        }

        private void OnSheetChanged()
        {
            float newMax = _sheet.Get(Attr.MaxMana);
            if (Mathf.Approximately(newMax, Max)) return;
            float delta = newMax - Max;
            Max = newMax;
            if (delta > 0f) Current += delta;
            Current = Mathf.Clamp(Current, 0f, Max);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (_regenLockout > 0f)
            {
                _regenLockout -= Time.deltaTime;
                return;
            }
            if (Current >= Max) return;

            float regen = _sheet != null ? _sheet.Get(Attr.ManaRegen) : 8f;
            Current = Mathf.Min(Max, Current + regen * Time.deltaTime);
            Changed?.Invoke();
        }

        public bool Has(float amount) => Current >= amount;

        public bool TrySpend(float amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            _regenLockout = regenDelayAfterSpend;
            Changed?.Invoke();
            return true;
        }

        public void Add(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke();
        }
    }
}
