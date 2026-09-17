using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Owns the Shift slot: one <see cref="Spell"/> with <see cref="SpellSlot.Movement"/>, its
    /// cooldown, and the on/off state when that spell is a sustained one.
    ///
    /// Movement spells are ordinary spells - same asset, same effect chains, same library - so
    /// the only thing this adds over the cast slots is the key. A spell carrying a
    /// <see cref="SustainProfile"/> stays on and drains while it runs; one without it fires its
    /// effect chain once and goes on cooldown, exactly as Q and E do.
    ///
    /// The spell levels like any other, through the book: Dash gains a charge per level past the first.
    /// </summary>
    public class MovementController : MonoBehaviour
    {
        public static readonly KeyCode ActivateKey = KeyCode.LeftShift;

        public Spell Current { get; private set; }
        public AbilityContext Context { get; set; }

        public PlayerMotor Motor;
        public Mana Mana;
        public Health Health;
        public CharacterSheet Sheet;
        public SpellBook Book;

        public bool InputEnabled { get; set; } = true;

        private readonly SustainRunner _sustain = new SustainRunner();
        private bool _hooked;
        private float _cooldownFull;

        public bool IsActive => _sustain.IsActive;

        /// <summary>Seconds left before a one-shot movement spell can fire again.</summary>
        public float Cooldown { get; private set; }

        /// <summary>How long a sustained spell has been running, for its duration cap.</summary>
        public float ActiveTime => _sustain.ActiveTime;

        public event Action Changed;

        /// <summary>Why the last activation failed, for the HUD to explain.</summary>
        public string LastRefusal { get; private set; }

        public int Level => Book != null && Current != null ? Mathf.Max(1, Book.GetLevel(Current)) : 1;

        public float CooldownFraction => _cooldownFull <= 0f ? 0f : Mathf.Clamp01(Cooldown / _cooldownFull);

        private void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            _sustain.Ended += _ => Changed?.Invoke();
        }

        /// <summary>Refuses anything that is not a movement spell, rather than binding it silently.</summary>
        public void Equip(Spell spell)
        {
            if (spell != null && spell.Slot != SpellSlot.Movement)
            {
                Debug.LogWarning("MovementController was handed " + spell.Id
                                 + ", which is a " + spell.Slot + " spell. Ignoring it.");
                return;
            }

            Hook();
            if (IsActive) Deactivate();

            Current = spell;
            Cooldown = 0f;
            _cooldownFull = 0f;

            if (Book != null) Book.SetEquipped(SpellSlot.Movement, spell);
            SyncDashCharges();
            Changed?.Invoke();
        }

        public bool Has(string id) => Current != null && Current.Id == id;

        private void Update() => Step(Time.deltaTime, Input.GetKeyDown(ActivateKey));

        /// <summary>One frame, given whether the key went down. Update calls it; public so tooling can drive the slot.</summary>
        public void Step(float dt, bool pressed)
        {
            Hook();
            if (Cooldown > 0f)
                Cooldown = Mathf.Max(0f, Cooldown - dt * (Current != null && Sheet != null ? Sheet.SchoolCooldownMultiplier(Current.School) : 1f));

            SyncDashCharges();
            if (Current == null) return;

            bool impaired = Context != null && Context.Status != null && Context.Status.IsControlImpaired;

            if (IsActive && (impaired || !_sustain.Tick(dt))) Deactivate();

            if (!InputEnabled || impaired || !pressed) return;

            if (Current.IsSustained && IsActive) Deactivate();
            else TryActivate();
        }

        /// <summary>Dash's charges grow with its level. Nothing else on Shift uses them.</summary>
        private void SyncDashCharges()
        {
            if (Motor != null) Motor.BonusDashCharges = Current != null && Current.UsesDashCharges ? Level - 1 : 0;
        }

        // ---------------------------------------------------------------- activation

        public bool TryActivate()
        {
            LastRefusal = null;
            if (Current == null)
            {
                LastRefusal = "Nothing bound to Shift";
                return false;
            }

            if (IsActive) return true;

            if (Context == null)
            {
                LastRefusal = Current.DisplayName + " has no caster";
                return false;
            }

            if (Cooldown > 0f)
            {
                LastRefusal = Current.DisplayName + " is recharging";
                return false;
            }

            if (Context.Status != null && Context.Status.IsSilenced)
            {
                LastRefusal = SpellCosts.Refusal(CastOutcome.Silenced, Current);
                return false;
            }

            CastOutcome outcome = Current.IsSustained
                ? SustainRunner.CheckStart(Current, Context)
                : SpellCosts.Check(Current, Context);

            if (outcome != CastOutcome.Ready)
            {
                LastRefusal = outcome == CastOutcome.NotEnoughMana
                    ? "Not enough mana for " + Current.DisplayName
                    : SpellCosts.Refusal(outcome, Current);
                return false;
            }

            return Current.IsSustained ? BeginSustained() : FireOnce();
        }

        private bool FireOnce()
        {
            int level = Level;

            Context.SoulsSpent = SpellCosts.SoulsFor(Current, Context);
            bool cast = Current.Cast(Context, level);
            if (cast) SpellCosts.Pay(Current, Context);
            Context.SoulsSpent = 0;

            if (!cast)
            {
                // The chain refused - no dash charges, or a wall in the way. Charge nothing.
                LastRefusal = Current.DisplayName + " has no room";
                return false;
            }

            _cooldownFull = Current.CooldownAtLevel(level);
            Cooldown = _cooldownFull;

            Changed?.Invoke();
            return true;
        }

        private bool BeginSustained()
        {
            if (!_sustain.Begin(Current, Context, Level))
            {
                LastRefusal = Current.DisplayName + " has no room";
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        public void Deactivate()
        {
            if (IsActive) _sustain.End();
        }

        /// <summary>Called between rooms and on death, so nothing carries a mode across.</summary>
        public void ResetState()
        {
            Deactivate();
            Cooldown = 0f;
            LastRefusal = null;
        }

        private void OnDisable() => Deactivate();
    }
}
