using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// One held-on spell while it runs: switching it on, draining it, watching what breaks it, and
    /// switching it off. The movement slot and each cast slot own one, so a toggle behaves the same
    /// on Shift as on Q.
    ///
    /// Switching on runs the spell's effect chain once when it has one, so a toggle's own effects
    /// happen at the start; an abort refuses the toggle and costs nothing.
    /// </summary>
    public class SustainRunner
    {
        public Spell Spell { get; private set; }
        public bool IsActive { get; private set; }

        /// <summary>How long it has been running, for its duration cap.</summary>
        public float ActiveTime { get; private set; }

        /// <summary>Raised once whenever it stops, for whatever reason, so the owner can start its cooldown.</summary>
        public event Action<SustainRunner> Ended;

        private AbilityContext _ctx;
        private StatModifier _speedModifier;
        private Vector3 _anchor;
        private PlayerRig _rig;
        private PlayerConcealment _concealment;
        private TrailEmitter _trail;
        private readonly object _concealKey = new object();

        /// <summary>A held spell pays half a second up front, so flicking it on and off still costs something.</summary>
        public static float UpfrontMana(Spell spell) => spell != null && spell.IsSustained ? spell.Sustain.ManaPerSecond * 0.5f : 0f;

        public static float UpfrontHealth(Spell spell) => spell != null && spell.IsSustained ? spell.Sustain.HealthPerSecond * 0.5f : 0f;

        /// <summary>Why it cannot start, or Ready. Upfront drains only; the per-second drain is checked as it runs.</summary>
        public static CastOutcome CheckStart(Spell spell, AbilityContext ctx)
        {
            if (spell == null || !spell.IsSustained) return CastOutcome.NoSpell;

            float mana = UpfrontMana(spell);
            if (mana > 0f && (ctx.Mana == null || !ctx.Mana.Has(mana))) return CastOutcome.NotEnoughMana;
            if (!SpellCosts.CanPayHealth(ctx.Health, UpfrontHealth(spell))) return CastOutcome.NotEnoughHealth;
            return CastOutcome.Ready;
        }

        public bool Begin(Spell spell, AbilityContext ctx, int level)
        {
            if (IsActive || spell == null || !spell.IsSustained || ctx == null || ctx.Caster == null) return false;

            // The switch-on effects. An abort refuses the toggle, costing nothing.
            if (spell.OnCast.Count > 0 && !spell.Cast(ctx, level)) return false;

            Spell = spell;
            _ctx = ctx;
            IsActive = true;
            ActiveTime = 0f;

            Transform caster = ctx.Caster.transform;
            _anchor = caster.position;

            SustainProfile sustain = spell.Sustain;

            float mana = UpfrontMana(spell);
            if (mana > 0f && ctx.Mana != null) ctx.Mana.TrySpend(mana);
            SpellCosts.PayHealth(ctx.Health, UpfrontHealth(spell));

            if (sustain.MoveSpeedBonus != 0f && ctx.Sheet != null)
                _speedModifier = ctx.Sheet.AddPercent(Attr.MoveSpeed, sustain.MoveSpeedBonus, this);

            if (sustain.WallZip && ctx.Motor != null)
            {
                // The camera's exact look direction, pitch included, so aiming up at a ledge
                // or down at a floor zips there just as readily as a wall dead ahead.
                ctx.Motor.BeginWallZip(ctx.Aim != null ? ctx.Aim.forward : caster.forward);
            }

            if (sustain.Buoyant && ctx.Motor != null) ctx.Motor.Buoyant = true;

            if (sustain.Trail != null && sustain.Trail.Exists)
                _trail = sustain.Trail.AttachTo(caster, ctx.Team, ctx.Caster,
                    Combat.OutgoingMultiplier(ctx.Sheet, true, sustain.Trail.DamageType, spell.Type), DamageOrigin.Spell);

            _rig = ctx.Caster.GetComponent<PlayerRig>();
            _concealment = ctx.Caster.GetComponent<PlayerConcealment>();
            if (sustain.HideFromSight && _concealment != null) _concealment.Hide(_concealKey, fromSight: true);

            Subscribe(true);
            return true;
        }

        /// <summary>One frame of running. Returns false once it has stopped.</summary>
        public bool Tick(float dt)
        {
            if (!IsActive) return false;
            if (StillRunning(dt)) return true;

            End();
            return false;
        }

        private bool StillRunning(float dt)
        {
            SustainProfile sustain = Spell.Sustain;
            ActiveTime += dt;

            if (sustain.MaxDuration > 0f && ActiveTime >= sustain.MaxDuration) return false;

            if (sustain.ManaPerSecond > 0f && (_ctx.Mana == null || !_ctx.Mana.TrySpend(sustain.ManaPerSecond * dt)))
                return false;

            // Refused rather than clamped: at the last hit point it ends instead of paying.
            if (sustain.HealthPerSecond > 0f && !SpellCosts.PayHealth(_ctx.Health, sustain.HealthPerSecond * dt))
                return false;

            if (sustain.BreakOnMovement)
            {
                // Planting your feet is the cost of the invulnerability, so any real movement
                // ends it. A small tolerance keeps it from dropping on physics jitter.
                Vector3 here = _ctx.Caster.transform.position;
                if ((here - _anchor).sqrMagnitude > 0.25f) return false;
                _anchor = Vector3.Lerp(_anchor, here, 0.02f);
            }

            if (sustain.Invulnerable && _ctx.Health != null)
                _ctx.Health.InvulnerabilityTimer = Mathf.Max(_ctx.Health.InvulnerabilityTimer, 0.2f);

            // The motor lets go on its own when the zip finds no wall, or the player walks off
            // the edge of one with nothing to land on - either way there is nothing left for
            // this spell to be doing, so it should end rather than sit active and idle.
            if (sustain.WallZip && _ctx.Motor != null && _ctx.Motor.ZipState == PlayerMotor.WallZipState.Off)
                return false;

            return true;
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            Subscribe(false);

            if (_speedModifier != null && _ctx.Sheet != null) _ctx.Sheet.RemoveModifier(_speedModifier);
            _speedModifier = null;

            if (Spell.Sustain.WallZip && _ctx.Motor != null) _ctx.Motor.EndWallZip();
            if (Spell.Sustain.Buoyant && _ctx.Motor != null) _ctx.Motor.Buoyant = false;
            if (_concealment != null) _concealment.Release(_concealKey);

            // What is already down burns out on its own.
            if (_trail != null) _trail.Stop();
            _trail = null;

            ActiveTime = 0f;
            Ended?.Invoke(this);
        }

        // ---------------------------------------------------------------- what breaks it

        private void Subscribe(bool on)
        {
            if (_rig == null || Spell == null) return;
            SustainProfile sustain = Spell.Sustain;

            if (sustain.BreakOnShoot && _rig.Weapon != null)
            {
                if (on) _rig.Weapon.Fired += OnFired;
                else _rig.Weapon.Fired -= OnFired;
            }

            if (sustain.BreakOnCast && _rig.Book != null)
            {
                if (on) _rig.Book.SpellCast += OnCast;
                else _rig.Book.SpellCast -= OnCast;
            }

            if (sustain.BreakOnMelee && _rig.CombatInput != null)
            {
                if (on) _rig.CombatInput.MeleeFinished += OnMelee;
                else _rig.CombatInput.MeleeFinished -= OnMelee;
            }
        }

        private void OnFired(WeaponShot shot)
        {
            if (!shot.IsEcho && !shot.IsPhantom) End();
        }

        // Its own cast is not "casting another spell".
        private void OnCast(Spell spell, int slot)
        {
            if (spell != Spell) End();
        }

        private void OnMelee(Spell spell, bool cast)
        {
            if (cast) End();
        }
    }
}
