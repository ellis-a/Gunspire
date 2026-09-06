using System;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Owns the Shift slot: one <see cref="MovementAbility"/>, its cooldown, and the on/off
    /// state of the sustained ones.
    ///
    /// Sustained abilities work by adding a stat modifier and flipping a motor flag while they
    /// run, and taking both back when they stop, so nothing else in the game has to know they
    /// exist.
    /// </summary>
    public class MovementController : MonoBehaviour
    {
        public static readonly KeyCode ActivateKey = KeyCode.LeftShift;

        public MovementAbility Current { get; private set; }
        public AbilityContext Context { get; set; }

        public PlayerMotor Motor;
        public Mana Mana;
        public Health Health;
        public CharacterSheet Sheet;

        public bool InputEnabled { get; set; } = true;
        public bool IsActive { get; private set; }

        /// <summary>Seconds left before an instant ability can fire again.</summary>
        public float Cooldown { get; private set; }

        /// <summary>How long a sustained ability has been running, for its duration cap.</summary>
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

        public void Equip(MovementAbility ability)
        {
            if (IsActive) Deactivate();

            Current = ability;
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

            float upfront = Current.IsSustained ? Current.ManaPerSecond * 0.5f : Current.ManaCost;
            if (upfront > 0f && (Mana == null || !Mana.Has(upfront)))
            {
                LastRefusal = "Not enough mana for " + Current.DisplayName;
                return false;
            }

            return Current.IsSustained ? BeginSustained() : FireInstant();
        }

        private bool FireInstant()
        {
            if (Context == null) return false;

            Context.Begin(DamageType.Astral, SpellType.Mobility, Current.Tint,
                level: 1, levelScale: 1f, isSpell: true);

            bool fired = AbilityRunner.Run(Current.OnActivate, Context);
            Vector3 point = Context.Point;
            Context.EndCast();

            if (!fired)
            {
                // The chain refused - no dash charges, or a wall in the way. Charge nothing.
                LastRefusal = Current.DisplayName + " has no room";
                return false;
            }

            if (Current.ManaCost > 0f && Mana != null) Mana.TrySpend(Current.ManaCost);
            Cooldown = Current.Cooldown;

            AbilityEvents.RaiseCast(Current.Id, Context, point);
            Changed?.Invoke();
            return true;
        }

        private bool BeginSustained()
        {
            IsActive = true;
            ActiveTime = 0f;
            _stationaryAnchor = transform.position;

            if (Current.MoveSpeedBonus != 0f && Sheet != null)
                _speedModifier = Sheet.AddPercent(Attr.MoveSpeed, Current.MoveSpeedBonus, this);

            if (Current.WallCling && Motor != null) Motor.WallClingEnabled = true;

            Changed?.Invoke();
            return true;
        }

        /// <summary>Returns false when the ability should stop.</summary>
        private bool SustainTick(float dt)
        {
            ActiveTime += dt;

            if (Current.MaxDuration > 0f && ActiveTime >= Current.MaxDuration) return false;

            if (Current.ManaPerSecond > 0f)
            {
                if (Mana == null || !Mana.TrySpend(Current.ManaPerSecond * dt)) return false;
            }

            if (Current.BreakOnMovement)
            {
                // Planting your feet is the cost of the invulnerability, so any real movement
                // ends it. A small tolerance keeps it from dropping on physics jitter.
                if ((transform.position - _stationaryAnchor).sqrMagnitude > 0.25f) return false;
                _stationaryAnchor = Vector3.Lerp(_stationaryAnchor, transform.position, 0.02f);
            }

            if (Current.Invulnerable && Health != null)
                Health.InvulnerabilityTimer = Mathf.Max(Health.InvulnerabilityTimer, 0.2f);

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

            if (Motor != null) Motor.WallClingEnabled = false;
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
