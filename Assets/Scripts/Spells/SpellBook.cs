using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The player spell slots. Two for now (Q and E); the slot count is data, so adding
    /// a third key later means changing <see cref="SlotCount"/> and the key list.
    /// </summary>
    public class SpellBook : MonoBehaviour
    {
        public const int SlotCount = 2;
        public static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.E };
        public static readonly string[] SlotLabels = { "Q", "E" };

        private readonly Spell[] _slots = new Spell[SlotCount];
        private readonly float[] _cooldowns = new float[SlotCount];
        private readonly List<Spell> _known = new List<Spell>();

        public SpellContext Context { get; set; }

        public event Action Changed;
        public event Action<Spell, int> SpellCast;

        public IReadOnlyList<Spell> Known => _known;

        public Spell GetSlot(int index) => index >= 0 && index < SlotCount ? _slots[index] : null;

        public float GetCooldown(int index) => index >= 0 && index < SlotCount ? _cooldowns[index] : 0f;

        public float GetCooldownFraction(int index)
        {
            Spell spell = GetSlot(index);
            if (spell == null || spell.Cooldown <= 0f) return 0f;
            return Mathf.Clamp01(_cooldowns[index] / spell.Cooldown);
        }

        public bool Knows(Spell spell)
        {
            if (spell == null) return false;
            for (int i = 0; i < _known.Count; i++)
                if (_known[i].Id == spell.Id) return true;
            return false;
        }

        /// <summary>Adds a spell to the book and puts it in the given slot (or the first free one).</summary>
        public void Learn(Spell spell, int slot = -1)
        {
            if (spell == null) return;
            if (!Knows(spell)) _known.Add(spell);

            if (slot < 0)
            {
                slot = 0;
                for (int i = 0; i < SlotCount; i++)
                {
                    if (_slots[i] == null) { slot = i; break; }
                    if (i == SlotCount - 1) slot = SlotCount - 1;   // no free slot: replace the last
                }
            }

            Bind(spell, slot);
        }

        public void Bind(Spell spell, int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;

            // A spell can only occupy one slot; clear any duplicate binding.
            for (int i = 0; i < SlotCount; i++)
                if (i != slot && _slots[i] != null && spell != null && _slots[i].Id == spell.Id)
                    _slots[i] = null;

            _slots[slot] = spell;
            _cooldowns[slot] = 0f;
            if (spell != null && !Knows(spell)) _known.Add(spell);
            Changed?.Invoke();
        }

        private void Update()
        {
            float rate = Context != null && Context.Sheet != null ? Context.Sheet.Get(Attr.CooldownRate) : 1f;
            float step = Time.deltaTime * rate;

            for (int i = 0; i < SlotCount; i++)
                if (_cooldowns[i] > 0f) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - step);
        }

        public bool CanCast(int slot)
        {
            Spell spell = GetSlot(slot);
            if (spell == null || Context == null) return false;
            if (_cooldowns[slot] > 0f) return false;
            if (Context.Mana != null && !Context.Mana.Has(spell.ManaCost)) return false;
            return true;
        }

        public bool TryCast(int slot)
        {
            if (!CanCast(slot)) return false;

            Spell spell = _slots[slot];
            if (!spell.Cast(Context)) return false;   // spell refunded itself

            if (Context.Mana != null) Context.Mana.TrySpend(spell.ManaCost);
            _cooldowns[slot] = spell.Cooldown;

            SpellCast?.Invoke(spell, slot);
            return true;
        }

        /// <summary>Used by boons like Quickened Casting.</summary>
        public void ReduceCooldowns(float seconds)
        {
            for (int i = 0; i < SlotCount; i++)
                _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - seconds);
        }

        public void ResetCooldowns()
        {
            for (int i = 0; i < SlotCount; i++) _cooldowns[i] = 0f;
        }

        /// <summary>Forgets every spell and empties both slots, for the start of a fresh run.</summary>
        public void ResetBook()
        {
            _known.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = null;
                _cooldowns[i] = 0f;
            }
            Changed?.Invoke();
        }
    }
}
