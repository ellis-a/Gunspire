using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The character sheet. Holds the five core stats, derives every gameplay number from
    /// them, and lets boons / status effects layer modifiers on top.
    ///
    /// Final value = (formulaBase + sum(flat)) * (1 + sum(percent)), then clamped.
    /// </summary>
    public class CharacterSheet : MonoBehaviour
    {
        [Header("Core stats")]
        [SerializeField] private int strength = 5;
        [SerializeField] private int intellect = 5;
        [SerializeField] private int agility = 5;
        [SerializeField] private int vitality = 5;
        [SerializeField] private int luck = 5;

        private readonly Dictionary<StatType, int> _coreBonus = new Dictionary<StatType, int>();
        private readonly Dictionary<Attr, float> _baseOverrides = new Dictionary<Attr, float>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        private readonly Dictionary<Attr, float> _cache = new Dictionary<Attr, float>();
        private bool _dirty = true;

        // Per-category channels. These sit alongside the Attr modifiers rather than inside
        // them, because they are keyed by a damage school or spell type rather than by Attr.
        private readonly TypedModifierSet<DamageType> _damageByType = new TypedModifierSet<DamageType>();
        private readonly TypedModifierSet<SpellType> _spellByType = new TypedModifierSet<SpellType>();
        private readonly TypedModifierSet<DamageType> _resistances = new TypedModifierSet<DamageType>();

        /// <summary>Raised whenever any stat or modifier changes. Health listens so it can rescale.</summary>
        public event Action Changed;

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        // ---------------------------------------------------------------- core stats

        public int GetStat(StatType stat)
        {
            int b;
            switch (stat)
            {
                case StatType.Strength: b = strength; break;
                case StatType.Intellect: b = intellect; break;
                case StatType.Agility: b = agility; break;
                case StatType.Vitality: b = vitality; break;
                default: b = luck; break;
            }
            _coreBonus.TryGetValue(stat, out int bonus);
            return Mathf.Max(0, b + bonus);
        }

        public void SetBaseStat(StatType stat, int value)
        {
            switch (stat)
            {
                case StatType.Strength: strength = value; break;
                case StatType.Intellect: intellect = value; break;
                case StatType.Agility: agility = value; break;
                case StatType.Vitality: vitality = value; break;
                default: luck = value; break;
            }
            MarkDirty();
        }

        /// <summary>Permanent core-stat gain, e.g. from a boon or a shrine.</summary>
        public void AddStat(StatType stat, int amount)
        {
            _coreBonus.TryGetValue(stat, out int bonus);
            _coreBonus[stat] = bonus + amount;
            MarkDirty();
        }

        // ---------------------------------------------------------------- modifiers

        public StatModifier AddModifier(StatModifier mod)
        {
            if (mod == null) return null;
            _modifiers.Add(mod);
            MarkDirty();
            return mod;
        }

        public StatModifier AddFlat(Attr attr, float value, object source = null, string label = null)
            => AddModifier(StatModifier.Flat(attr, value, source, label));

        public StatModifier AddPercent(Attr attr, float value, object source = null, string label = null)
            => AddModifier(StatModifier.Percent(attr, value, source, label));

        public void RemoveModifier(StatModifier mod)
        {
            if (mod != null && _modifiers.Remove(mod)) MarkDirty();
        }

        public void RemoveModifiersFrom(object source)
        {
            int removed = _modifiers.RemoveAll(m => Equals(m.Source, source));
            if (removed > 0) MarkDirty();
        }

        /// <summary>
        /// Wipes every modifier and stat gain. Used when a new run reuses the player object.
        /// Intrinsic bases (an enemy resisting its own element) are kept.
        /// </summary>
        public void ResetToBase()
        {
            _modifiers.Clear();
            _coreBonus.Clear();
            _damageByType.ClearModifiers();
            _spellByType.ClearModifiers();
            _resistances.ClearModifiers();
            MarkDirty();
        }

        // ---------------------------------------------------------------- per-type channels

        /// <summary>Outgoing multiplier for one damage school. 1.0 when nothing has buffed it.</summary>
        public float DamageTypeMultiplier(DamageType type)
            => Mathf.Max(0.05f, 1f + _damageByType.Sum(type));

        /// <summary>Outgoing multiplier for one spell category.</summary>
        public float SpellTypeMultiplier(SpellType type)
            => Mathf.Max(0.05f, 1f + _spellByType.Sum(type));

        /// <summary>
        /// Fraction of incoming damage of this school that is ignored. Negative means
        /// vulnerable, which is how an enemy can be weak to the element that counters it.
        /// </summary>
        public float Resistance(DamageType type)
            => DamageTypes.IsResistable(type) ? Mathf.Clamp(_resistances.Sum(type), -0.9f, 0.9f) : 0f;

        public TypedModifier<DamageType> AddDamagePercent(DamageType type, float value, object source = null)
        {
            var mod = _damageByType.Add(type, value, source);
            MarkDirty();
            return mod;
        }

        public TypedModifier<SpellType> AddSpellPercent(SpellType type, float value, object source = null)
        {
            var mod = _spellByType.Add(type, value, source);
            MarkDirty();
            return mod;
        }

        public TypedModifier<DamageType> AddResistance(DamageType type, float value, object source = null)
        {
            var mod = _resistances.Add(type, value, source);
            MarkDirty();
            return mod;
        }

        /// <summary>An entity's innate resistance, set when it is built.</summary>
        public void SetBaseResistance(DamageType type, float value)
        {
            _resistances.SetBase(type, value);
            MarkDirty();
        }

        public void RemoveTypedModifiersFrom(object source)
        {
            _damageByType.RemoveFrom(source);
            _spellByType.RemoveFrom(source);
            _resistances.RemoveFrom(source);
            MarkDirty();
        }

        /// <summary>Used by enemies, which do not want the player stat formulas.</summary>
        public void SetBaseOverride(Attr attr, float value)
        {
            _baseOverrides[attr] = value;
            MarkDirty();
        }

        public void MarkDirty()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- derived values

        public float Get(Attr attr)
        {
            if (_dirty) Recalculate();
            return _cache.TryGetValue(attr, out float v) ? v : BaseValue(attr);
        }

        public int GetInt(Attr attr) => Mathf.RoundToInt(Get(attr));

        private void Recalculate()
        {
            _dirty = false;
            _cache.Clear();

            Attr[] all = EnumCache.Attrs;
            for (int a = 0; a < all.Length; a++)
            {
                Attr attr = all[a];
                float flat = 0f;
                float pct = 0f;
                for (int i = 0; i < _modifiers.Count; i++)
                {
                    StatModifier m = _modifiers[i];
                    if (m.Attr != attr) continue;
                    if (m.IsPercent) pct += m.Value; else flat += m.Value;
                }

                float value = (BaseValue(attr) + flat) * (1f + pct);
                _cache[attr] = Clamp(attr, value);
            }
        }

        private float BaseValue(Attr attr)
        {
            if (_baseOverrides.TryGetValue(attr, out float over)) return over;

            int str = GetStat(StatType.Strength);
            int wit = GetStat(StatType.Intellect);
            int agi = GetStat(StatType.Agility);
            int vit = GetStat(StatType.Vitality);
            int lck = GetStat(StatType.Luck);

            switch (attr)
            {
                case Attr.MaxHealth:        return 80f + vit * 12f;
                case Attr.HealthRegen:      return vit * 0.04f;
                case Attr.MaxMana:          return 80f + wit * 8f;
                case Attr.ManaRegen:        return 5f + wit * 0.8f;

                case Attr.MoveSpeed:        return 7.0f + agi * 0.18f;
                case Attr.JumpHeight:       return 1.25f + agi * 0.06f;
                case Attr.AirControl:       return 0.35f + agi * 0.015f;
                case Attr.DashCharges:      return 1f + Mathf.Floor(agi / 6f);
                case Attr.DashSpeed:        return 22f + agi * 0.30f;

                case Attr.DamageDealt:      return 1f;
                case Attr.GunDamage:        return 1f + str * 0.025f;
                case Attr.SpellPower:       return 1f + wit * 0.045f;
                case Attr.DamageTaken:      return 1f;
                case Attr.HealingReceived:  return 1f;

                case Attr.CooldownRate:     return 1f + wit * 0.020f;
                case Attr.AttackSpeed:      return 1f + agi * 0.012f;
                case Attr.ReloadSpeed:      return 1f + agi * 0.020f;

                case Attr.CritChance:       return 0.03f + lck * 0.012f;
                case Attr.CritDamage:       return 1.75f + lck * 0.020f;
                case Attr.SmashPower:       return str;
                case Attr.Lifesteal:        return 0f;
            }
            return 0f;
        }

        private static float Clamp(Attr attr, float v)
        {
            switch (attr)
            {
                case Attr.MaxHealth:       return Mathf.Max(1f, v);
                case Attr.MaxMana:         return Mathf.Max(0f, v);
                case Attr.MoveSpeed:       return Mathf.Max(1f, v);
                case Attr.JumpHeight:      return Mathf.Max(0.2f, v);
                case Attr.AirControl:      return Mathf.Clamp(v, 0f, 1f);
                case Attr.DashCharges:     return Mathf.Clamp(v, 0f, 6f);
                case Attr.DamageTaken:     return Mathf.Max(0.05f, v);
                case Attr.HealingReceived: return Mathf.Max(0f, v);
                case Attr.CooldownRate:    return Mathf.Max(0.25f, v);
                case Attr.AttackSpeed:     return Mathf.Max(0.25f, v);
                case Attr.ReloadSpeed:     return Mathf.Max(0.25f, v);
                case Attr.CritChance:      return Mathf.Clamp01(v);
                case Attr.CritDamage:      return Mathf.Max(1f, v);
                case Attr.SpellPower:      return Mathf.Max(0.05f, v);
                case Attr.GunDamage:       return Mathf.Max(0.05f, v);
                case Attr.DamageDealt:     return Mathf.Max(0.05f, v);
                default:                   return v;
            }
        }

        /// <summary>Human readable line for the character sheet screen.</summary>
        public string DescribeStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:
                    return string.Format("Gun damage +{0:0}%   Smash power {1:0}",
                        (Get(Attr.GunDamage) - 1f) * 100f, Get(Attr.SmashPower));
                case StatType.Intellect:
                    return string.Format("Spell power +{0:0}%   Cooldown rate +{1:0}%   Mana {2:0}",
                        (Get(Attr.SpellPower) - 1f) * 100f, (Get(Attr.CooldownRate) - 1f) * 100f, Get(Attr.MaxMana));
                case StatType.Agility:
                    return string.Format("Speed {0:0.0}   Jump {1:0.00}m   Dashes {2:0}",
                        Get(Attr.MoveSpeed), Get(Attr.JumpHeight), Get(Attr.DashCharges));
                case StatType.Vitality:
                    return string.Format("Health {0:0}   Regen {1:0.00}/s",
                        Get(Attr.MaxHealth), Get(Attr.HealthRegen));
                default:
                    return string.Format("Crit {0:0}% for {1:0}%   Rare finds x{2:0.00}",
                        Get(Attr.CritChance) * 100f, Get(Attr.CritDamage) * 100f,
                        1f + GetStat(StatType.Luck) * Rarities.LuckScaling);
            }
        }

        /// <summary>Non-zero resistances, formatted for the character sheet. Empty when there are none.</summary>
        public string DescribeResistances()
        {
            string text = "";
            for (int i = 0; i < DamageTypes.Elemental.Length; i++)
            {
                DamageType type = DamageTypes.Elemental[i];
                float value = Resistance(type);
                if (Mathf.Approximately(value, 0f)) continue;

                if (text.Length > 0) text += "   ";
                text += string.Format("{0} {1}{2:0}%", DamageTypes.Name(type),
                    value > 0f ? "+" : "", value * 100f);
            }
            return text;
        }

        /// <summary>Non-zero outgoing damage bonuses by school, formatted for the character sheet.</summary>
        public string DescribeDamageBonuses()
        {
            string text = "";
            for (int i = 0; i < DamageTypes.Elemental.Length; i++)
            {
                DamageType type = DamageTypes.Elemental[i];
                float value = DamageTypeMultiplier(type) - 1f;
                if (Mathf.Approximately(value, 0f)) continue;

                if (text.Length > 0) text += "   ";
                text += string.Format("{0} +{1:0}%", DamageTypes.Name(type), value * 100f);
            }
            return text;
        }
    }

    /// <summary>Enum.GetValues allocates, so cache the arrays we walk every recalculation.</summary>
    public static class EnumCache
    {
        public static readonly Attr[] Attrs = (Attr[])Enum.GetValues(typeof(Attr));
        public static readonly StatType[] Stats = (StatType[])Enum.GetValues(typeof(StatType));
        public static readonly StatusId[] Statuses = (StatusId[])Enum.GetValues(typeof(StatusId));
    }
}
