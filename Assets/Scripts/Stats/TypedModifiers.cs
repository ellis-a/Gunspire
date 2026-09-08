using System.Collections.Generic;

namespace Gunspire
{
    /// <summary>A modifier keyed by an arbitrary category, such as a damage school or a spell type.</summary>
    public class TypedModifier<TKey>
    {
        public TKey Key;
        public float Value;
        public object Source;
        public string Label;
    }

    /// <summary>
    /// A bag of per-category values: a fixed base per key plus any number of removable
    /// modifiers. Used for per-damage-type bonuses, per-spell-type bonuses and resistances,
    /// which all need the same shape but a different key.
    /// </summary>
    public class TypedModifierSet<TKey>
    {
        private readonly Dictionary<TKey, float> _base = new Dictionary<TKey, float>();
        private readonly List<TypedModifier<TKey>> _modifiers = new List<TypedModifier<TKey>>();

        public IReadOnlyList<TypedModifier<TKey>> Modifiers => _modifiers;

        /// <summary>Base plus every modifier registered against this key.</summary>
        public float Sum(TKey key)
        {
            _base.TryGetValue(key, out float total);
            for (int i = 0; i < _modifiers.Count; i++)
                if (EqualityComparer<TKey>.Default.Equals(_modifiers[i].Key, key))
                    total += _modifiers[i].Value;
            return total;
        }

        public bool HasAnything(TKey key) => Sum(key) != 0f;

        /// <summary>The intrinsic value for an entity, e.g. a Frostcaller resisting Frost.</summary>
        public void SetBase(TKey key, float value) => _base[key] = value;

        public float GetBase(TKey key)
        {
            _base.TryGetValue(key, out float v);
            return v;
        }

        public TypedModifier<TKey> Add(TKey key, float value, object source = null, string label = null)
        {
            var mod = new TypedModifier<TKey> { Key = key, Value = value, Source = source, Label = label };
            _modifiers.Add(mod);
            return mod;
        }

        public void Remove(TypedModifier<TKey> mod)
        {
            if (mod != null) _modifiers.Remove(mod);
        }

        public void RemoveFrom(object source)
        {
            _modifiers.RemoveAll(m => Equals(m.Source, source));
        }

        /// <summary>Drops every modifier but keeps the intrinsic bases.</summary>
        public void ClearModifiers() => _modifiers.Clear();

        public void ClearAll()
        {
            _modifiers.Clear();
            _base.Clear();
        }
    }
}
