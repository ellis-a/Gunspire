using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Owns the Shift slot: one <see cref="Spell"/> with <see cref="SpellSlot.Movement"/>, its
    /// cooldown, and the on/off state when that spell is a sustained one.
    ///
    /// Movement spells are ordinary spells - same asset, same effect chains, same library - so
    /// the only thing this adds over the cast slots is the toggle. A spell carrying a
    /// <see cref="SustainProfile"/> stays on and drains mana per second; one without it fires
    /// its effect chain once and goes on cooldown, exactly as Q and E do.
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

        public bool InputEnabled { get; set; } = true;
        public bool IsActive { get; private set; }

        /// <summary>Seconds left before a one-shot movement spell can fire again.</summary>
        public float Cooldown { get; private set; }

        /// <summary>How long a sustained spell has been running, for its duration cap.</summary>
        public float ActiveTime { get; private set; }

        public event Action Changed;

        /// <summary>Why the last activation failed, for the HUD to explain.</summary>
        public string LastRefusal { get; private set; }

        private StatModifier _speedModifier;
        private Vector3 _stationaryAnchor;

        public float CooldownFraction
        {
            get
            {
                if (Current == null || Current.Cooldown <= 0f) return 0f;
                return Mathf.Clamp01(Cooldown / Current.Cooldown);
            }
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

            if (IsActive) Deactivate();

            Current = spell;
            Cooldown = 0f;
            Changed?.Invoke();
        }

        public bool Has(string id) => Current != null && Current.Id == id;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - dt);

            if (Current == null) return;

            bool impaired = Context != null && Context.Status != null && Context.Status.IsControlImpaired;

            if (IsActive)
            {
                if (impaired || !SustainTick(dt)) Deactivate();
            }

            if (!InputEnabled || impaired) return;

            if (Input.GetKeyDown(ActivateKey))
            {
                if (Current.IsSustained)
                {
                    if (IsActive) Deactivate();
                    else TryActivate();
                }
                else
                {
                    TryActivate();
                }
            }
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

            if (Cooldown > 0f)
            {
                LastRefusal = Current.DisplayName + " is recharging";
                return false;
            }

            // A sustained spell pays half a second up front, so flicking it on and off still
            // costs something rather than being free.
            float upfront = Current.IsSustained ? Current.Sustain.ManaPerSecond * 0.5f : Current.ManaCost;
            if (upfront > 0f && (Mana == null || !Mana.Has(upfront)))
            {
                LastRefusal = "Not enough mana for " + Current.DisplayName;
                return false;
            }

            return Current.IsSustained ? BeginSustained() : FireOnce();
        }

        private bool FireOnce()
        {
            if (Context == null) return false;

            if (!Current.Cast(Context, 1))
            {
                // The chain refused - no dash charges, or a wall in the way. Charge nothing.
                LastRefusal = Current.DisplayName + " has no room";
                return false;
            }

            if (Current.ManaCost > 0f && Mana != null) Mana.TrySpend(Current.ManaCost);
            Cooldown = Current.Cooldown;

            Changed?.Invoke();
            return true;
        }

        private bool BeginSustained()
        {
            IsActive = true;
            ActiveTime = 0f;
            _stationaryAnchor = transform.position;

            SustainProfile sustain = Current.Sustain;

            if (sustain.MoveSpeedBonus != 0f && Sheet != null)
                _speedModifier = Sheet.AddPercent(Attr.MoveSpeed, sustain.MoveSpeedBonus, this);

            if (sustain.WallZip && Motor != null)
            {
                // The camera's exact look direction, pitch included, so aiming up at a ledge
                // or down at a floor zips there just as readily as a wall dead ahead.
                Vector3 direction = Context != null && Context.Aim != null ? Context.Aim.forward : transform.forward;
                Motor.BeginWallZip(direction);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Returns false when the spell should stop.</summary>
        private bool SustainTick(float dt)
        {
            SustainProfile sustain = Current.Sustain;
            if (sustain == null) return false;

            ActiveTime += dt;

            if (sustain.MaxDuration > 0f && ActiveTime >= sustain.MaxDuration) return false;

            if (sustain.ManaPerSecond > 0f)
            {
                if (Mana == null || !Mana.TrySpend(sustain.ManaPerSecond * dt)) return false;
            }

            if (sustain.BreakOnMovement)
            {
                // Planting your feet is the cost of the invulnerability, so any real movement
                // ends it. A small tolerance keeps it from dropping on physics jitter.
                if ((transform.position - _stationaryAnchor).sqrMagnitude > 0.25f) return false;
                _stationaryAnchor = Vector3.Lerp(_stationaryAnchor, transform.position, 0.02f);
            }

            if (sustain.Invulnerable && Health != null)
                Health.InvulnerabilityTimer = Mathf.Max(Health.InvulnerabilityTimer, 0.2f);

            // The motor lets go on its own when the zip finds no wall, or the player walks off
            // the edge of one with nothing to land on - either way there is nothing left for
            // this spell to be doing, so it should end rather than sit active and idle.
            if (sustain.WallZip && Motor != null && Motor.ZipState == PlayerMotor.WallZipState.Off)
                return false;

            return true;
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            ActiveTime = 0f;

            if (_speedModifier != null && Sheet != null)
            {
                Sheet.RemoveModifier(_speedModifier);
                _speedModifier = null;
            }

            // Unconditional and harmless for spells that never touched it - EndWallZip is a
            // no-op unless the motor is actually mid-zip or attached to something.
            if (Motor != null) Motor.EndWallZip();
            Changed?.Invoke();
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
