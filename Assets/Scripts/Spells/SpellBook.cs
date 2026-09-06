using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>Result of trying to use a spell slot, so the HUD can say why nothing happened.</summary>
    public enum CastOutcome
    {
        Cast,
        Ready,           // only returned by Evaluate
        NoSpell,         // the slot is empty
        OnCooldown,
        NotEnoughMana,
        NoRoom           // the spell itself refused, e.g. a wall in the way
    }

    /// <summary>
    /// The player spell slots and the level of every spell learned. Two slots for now
    /// (Q and E); the slot count is data, so adding a third key later means changing
    /// <see cref="SlotCount"/> and the key list.
    ///
    /// Levels are keyed by spell id rather than by slot, so a spell keeps its level if it is
    /// moved to the other slot.
    /// </summary>
    public class SpellBook : MonoBehaviour
    {
        public const int SlotCount = 2;
        public static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.E };
        public static readonly string[] SlotLabels = { "Q", "E" };

        private readonly Spell[] _slots = new Spell[SlotCount];
        private readonly float[] _cooldowns = new float[SlotCount];
        private readonly List<Spell> _known = new List<Spell>();
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        public AbilityContext Context { get; set; }

        public event Action Changed;
        public event Action<Spell, int> SpellCast;

        public IReadOnlyList<Spell> Known => _known;

        public Spell GetSlot(int index) => index >= 0 && index < SlotCount ? _slots[index] : null;

        public float GetCooldown(int index) => index >= 0 && index < SlotCount ? _cooldowns[index] : 0f;

        // ---------------------------------------------------------------- levels

        public int GetLevel(Spell spell)
        {
            if (spell == null) return 0;
            return _levels.TryGetValue(spell.Id, out int level) ? level : 0;
        }

        public int GetSlotLevel(int index) => GetLevel(GetSlot(index));

        public bool Knows(Spell spell) => spell != null && _levels.ContainsKey(spell.Id);

        /// <summary>True when the spell is unknown, or known but not yet at its level cap.</summary>
        public bool CanTake(Spell spell) => spell != null && GetLevel(spell) < spell.MaxLevel;

        /// <summary>Raises the level of an already-known spell. Returns the new level.</summary>
        public int LevelUp(Spell spell)
        {
            if (spell == null) return 0;

            int level = Mathf.Min(GetLevel(spell) + 1, spell.MaxLevel);
            _levels[spell.Id] = level;
            Changed?.Invoke();
            return level;
        }

        public float GetCooldownFraction(int index)
        {
            Spell spell = GetSlot(index);
            if (spell == null) return 0f;

            float full = spell.CooldownAtLevel(GetLevel(spell));
            return full <= 0f ? 0f : Mathf.Clamp01(_cooldowns[index] / full);
        }

        // ---------------------------------------------------------------- learning and binding

        /// <summary>
        /// Takes a spell: levels it if already known, otherwise binds it at level one.
        /// This is what a shrine pedestal calls.
        /// </summary>
        public void Learn(Spell spell, int slot = -1)
        {
            if (spell == null) return;

            if (Knows(spell))
            {
                LevelUp(spell);
                return;
            }

            if (slot < 0)
            {
                slot = SlotCount - 1;
                for (int i = 0; i < SlotCount; i++)
                    if (_slots[i] == null) { slot = i; break; }
            }

            Bind(spell, slot);
        }

        /// <summary>Places a spell in a slot. Known at level one if it was not known before.</summary>
        public void Bind(Spell spell, int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;

            // A spell can only occupy one slot; clear any duplicate binding.
            for (int i = 0; i < SlotCount; i++)
                if (i != slot && _slots[i] != null && spell != null && _slots[i].Id == spell.Id)
                    _slots[i] = null;

            _slots[slot] = spell;
            _cooldowns[slot] = 0f;

            if (spell != null && !_levels.ContainsKey(spell.Id)) _levels[spell.Id] = 1;
            if (spell != null && !KnownContains(spell)) _known.Add(spell);

            Changed?.Invoke();
        }

        private bool KnownContains(Spell spell)
        {
            for (int i = 0; i < _known.Count; i++)
                if (_known[i].Id == spell.Id) return true;
            return false;
        }

        // ---------------------------------------------------------------- casting

        private void Update()
        {
            float rate = Context != null && Context.Sheet != null ? Context.Sheet.Get(Attr.CooldownRate) : 1f;
            float step = Time.deltaTime * rate;

            for (int i = 0; i < SlotCount; i++)
                if (_cooldowns[i] > 0f) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - step);
        }

        public bool CanCast(int slot) => Evaluate(slot) == CastOutcome.Ready;

        /// <summary>
        /// Why a slot can or cannot be used. Pressing a key and getting nothing at all is
        /// indistinguishable from a broken game, so the caller reports the reason.
        /// </summary>
        public CastOutcome Evaluate(int slot)
        {
            if (Context == null) return CastOutcome.NoSpell;

            Spell spell = GetSlot(slot);
            if (spell == null) return CastOutcome.NoSpell;
            if (_cooldowns[slot] > 0f) return CastOutcome.OnCooldown;
            if (Context.Mana != null && !Context.Mana.Has(spell.ManaCost)) return CastOutcome.NotEnoughMana;
            return CastOutcome.Ready;
        }

        /// <summary>Casts if it can, and reports what happened either way.</summary>
        public CastOutcome TryCastSlot(int slot)
        {
            CastOutcome outcome = Evaluate(slot);
            if (outcome != CastOutcome.Ready) return outcome;

            Spell spell = _slots[slot];
            int level = Mathf.Max(1, GetLevel(spell));

            // The spell sets up the context, runs its effect chain, and tidies up after itself.
            if (!spell.Cast(Context, level)) return CastOutcome.NoRoom;   // an effect aborted

            if (Context.Mana != null) Context.Mana.TrySpend(spell.ManaCost);
            _cooldowns[slot] = spell.CooldownAtLevel(level);

            SpellCast?.Invoke(spell, slot);
            return CastOutcome.Cast;
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
            _levels.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = null;
                _cooldowns[i] = 0f;
            }
            Changed?.Invoke();
        }
    }
}
