using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Result of trying to use a spell slot, so the HUD can say why nothing happened. Never serialized;
    /// new members go at the end all the same.
    /// </summary>
    public enum CastOutcome
    {
        Cast,
        Ready,           // only returned by Evaluate
        NoSpell,         // the slot is empty
        OnCooldown,
        NotEnoughMana,
        NoRoom,          // the spell itself refused, e.g. a wall in the way
        NotEnoughSouls,
        NotEnoughHealth,
        NotEnoughPsi,
        Silenced,
        Disarmed,
        Charging,        // a charged spell started charging, or was let go too early to count
        ToggledOff,
        StanceChanged
    }

    /// <summary>
    /// The player's spells: the three cast slots, what is equipped in the movement and melee slots, and
    /// the level of every spell learned. Three slots, on Q, E and F; the slot count is data, so changing
    /// it means changing <see cref="SlotCount"/> and the key and label lists together. Verify Spell Slots
    /// checks they agree.
    ///
    /// Levels are keyed by spell id rather than by slot, so a spell keeps its level if it is moved to
    /// another slot, and the movement and melee spells level the same way. Mastery counts read
    /// <see cref="CollectEquipped"/>.
    ///
    /// A cast slot holds one of four kinds of spell: an ordinary cast, a toggle, a spell held to
    /// charge, or a stance that is always on and steps through its modes.
    /// </summary>
    public class SpellBook : MonoBehaviour
    {
        public const int SlotCount = 3;
        public static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.E, KeyCode.F };
        public static readonly string[] SlotLabels = { "Q", "E", "F" };

        private readonly Spell[] _slots = new Spell[SlotCount];
        private readonly float[] _cooldowns = new float[SlotCount];
        private readonly SustainRunner[] _sustains = new SustainRunner[SlotCount];
        private readonly bool[] _charging = new bool[SlotCount];
        private readonly float[] _chargeHeld = new float[SlotCount];
        private readonly int[] _stanceModes = new int[SlotCount];
        private readonly List<Spell> _known = new List<Spell>();
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        private readonly List<Spell> _equippedScratch = new List<Spell>();

        private Spell _movement;
        private Spell _melee;

        public AbilityContext Context { get; set; }

        /// <summary>The gun a stance's modes charge. Set when the rig is built.</summary>
        public Weapon Weapon { get; set; }

        public event Action Changed;
        public event Action<Spell, int> SpellCast;

        /// <summary>How the last successful cast went, for anything recording it to replay later.</summary>
        public struct CastRecord
        {
            public Spell Spell;
            public int Slot;
            public int Level;
            public Vector3 Forward;
            public float Charge;
            public int SoulsSpent;
        }

        public CastRecord LastCast { get; private set; }

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

        // ---------------------------------------------------------------- what is equipped

        /// <summary>The spell in the movement or melee slot. Those slots are driven elsewhere; the book records them.</summary>
        public Spell GetEquipped(SpellSlot slot)
            => slot == SpellSlot.Movement ? _movement : slot == SpellSlot.Melee ? _melee : null;

        /// <summary>
        /// Records the movement or melee spell. The controllers for those slots call this when they
        /// equip, so the spell gains a level to grow and counts toward its school's mastery.
        /// </summary>
        public void SetEquipped(SpellSlot slot, Spell spell)
        {
            if (slot == SpellSlot.Cast || (spell != null && spell.Slot != slot)) return;

            if (slot == SpellSlot.Movement) _movement = spell;
            else _melee = spell;

            if (spell != null && !_levels.ContainsKey(spell.Id)) _levels[spell.Id] = 1;
            Changed?.Invoke();
        }

        public bool IsEquipped(Spell spell)
        {
            if (spell == null) return false;

            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] != null && _slots[i].Id == spell.Id) return true;

            return (_movement != null && _movement.Id == spell.Id) || (_melee != null && _melee.Id == spell.Id);
        }

        /// <summary>Every spell equipped: the cast slots, then movement, then melee.</summary>
        public void CollectEquipped(List<Spell> into)
        {
            into.Clear();
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] != null) into.Add(_slots[i]);

            if (_movement != null) into.Add(_movement);
            if (_melee != null) into.Add(_melee);
        }

        /// <summary>The capped mastery count for a school.</summary>
        public int SchoolCount(SpellSchool school)
        {
            CollectEquipped(_equippedScratch);
            return Masteries.Count(_equippedScratch, school);
        }

        /// <summary>A different version of this spell, from the same variant group, is already equipped.</summary>
        public bool HasOtherVariant(Spell spell)
        {
            if (spell == null || string.IsNullOrEmpty(spell.VariantGroup)) return false;

            CollectEquipped(_equippedScratch);
            for (int i = 0; i < _equippedScratch.Count; i++)
            {
                Spell other = _equippedScratch[i];
                if (other.Id != spell.Id && other.VariantGroup == spell.VariantGroup) return true;
            }
            return false;
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

            if (slot < 0) slot = FirstEmptySlot();
            Bind(spell, slot);
        }

        /// <summary>
        /// The lowest empty slot, or the last slot if the book is full. Callers that bind
        /// without asking the player need this so they can name the slot afterwards.
        /// </summary>
        public int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] == null) return i;

            return SlotCount - 1;
        }

        /// <summary>
        /// Places a spell in a slot, or empties it with null. Known at level one if it was not known
        /// before. A spell occupies one slot, and one version of a variant group occupies at most one.
        /// A stance starts in its first mode.
        /// </summary>
        public void Bind(Spell spell, int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;

            if (spell != null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    if (i == slot || _slots[i] == null) continue;

                    bool duplicate = _slots[i].Id == spell.Id;
                    bool sameVariant = !string.IsNullOrEmpty(spell.VariantGroup) && _slots[i].VariantGroup == spell.VariantGroup;
                    if (duplicate || sameVariant) ClearSlot(i);
                }
            }

            ClearSlot(slot);
            _slots[slot] = spell;
            _cooldowns[slot] = 0f;

            if (spell != null)
            {
                if (!_levels.ContainsKey(spell.Id)) _levels[spell.Id] = 1;
                if (!KnownContains(spell)) _known.Add(spell);
                if (spell.IsStance) EnterStance(slot, 0);
            }

            Changed?.Invoke();
        }

        /// <summary>Empties a slot, switching off whatever the spell there was keeping on.</summary>
        private void ClearSlot(int slot)
        {
            Spell old = _slots[slot];
            if (old == null) return;

            if (_sustains[slot] != null) _sustains[slot].End();
            _charging[slot] = false;
            _chargeHeld[slot] = 0f;
            if (old.IsStance) LeaveStance(slot);

            _slots[slot] = null;
        }

        private bool KnownContains(Spell spell)
        {
            for (int i = 0; i < _known.Count; i++)
                if (_known[i].Id == spell.Id) return true;
            return false;
        }

        // ---------------------------------------------------------------- state per slot

        public bool IsSustainActive(int slot)
            => slot >= 0 && slot < SlotCount && _sustains[slot] != null && _sustains[slot].IsActive;

        public float SustainTime(int slot) => IsSustainActive(slot) ? _sustains[slot].ActiveTime : 0f;

        public bool IsCharging(int slot) => slot >= 0 && slot < SlotCount && _charging[slot];

        public float ChargeFraction(int slot)
        {
            Spell spell = GetSlot(slot);
            if (spell == null || !spell.IsCharged || !_charging[slot]) return 0f;
            return Mathf.Clamp01(_chargeHeld[slot] / spell.Charge.SecondsToFull);
        }

        public int GetStanceMode(int slot) => slot >= 0 && slot < SlotCount ? _stanceModes[slot] : 0;

        public StanceMode ActiveStanceMode(int slot)
        {
            Spell spell = GetSlot(slot);
            if (spell == null || !spell.IsStance) return null;
            return spell.Stance.Modes[Mathf.Clamp(_stanceModes[slot], 0, spell.Stance.Modes.Count - 1)];
        }

        public static string StanceInfusionId(Spell spell) => "stance:" + spell.Id;

        private void EnterStance(int slot, int mode)
        {
            Spell spell = _slots[slot];
            int count = spell.Stance.Modes.Count;
            _stanceModes[slot] = ((mode % count) + count) % count;

            StanceMode active = spell.Stance.Modes[_stanceModes[slot]];
            SetStanceModifiers(slot, active);

            if (Weapon == null) return;

            var infusion = new BulletInfusion { Id = StanceInfusionId(spell) };
            if (active.BulletStatuses != null) infusion.Statuses.AddRange(active.BulletStatuses);

            // No time or round limit, so it lasts exactly as long as the stance is bound.
            Weapon.Infuse(infusion);
        }

        private void LeaveStance(int slot)
        {
            if (Weapon != null && _slots[slot] != null) Weapon.RemoveInfusion(StanceInfusionId(_slots[slot]));
            SetStanceModifiers(slot, null);
            _stanceModes[slot] = 0;
        }

        private readonly object[] _stanceSources = new object[SlotCount];
        private readonly float[] _auraTimers = new float[SlotCount];
        private const float AuraInterval = 0.5f;

        /// <summary>Swaps the attribute changes of whichever mode a stance slot is in, or clears them with null.</summary>
        private void SetStanceModifiers(int slot, StanceMode mode)
        {
            CharacterSheet sheet = Context != null ? Context.Sheet : null;
            if (sheet == null) return;

            if (_stanceSources[slot] == null) _stanceSources[slot] = new object();
            object source = _stanceSources[slot];

            sheet.RemoveModifiersFrom(source);
            if (mode == null || mode.Modifiers == null) return;

            for (int i = 0; i < mode.Modifiers.Count; i++)
                if (mode.Modifiers[i] != null) sheet.AddPercent(mode.Modifiers[i].Attr, mode.Modifiers[i].Percent, source, mode.Name);
        }

        /// <summary>A stance mode's damage aura, dealt twice a second to every enemy around the caster.</summary>
        private void TickStanceAura(int slot, float dt)
        {
            StanceMode mode = ActiveStanceMode(slot);
            if (mode == null || mode.AuraDamagePerSecond <= 0f || Context == null || Context.Caster == null) return;
            if ((_auraTimers[slot] -= dt) > 0f) return;
            _auraTimers[slot] = AuraInterval;

            Spell spell = _slots[slot];
            float damage = mode.AuraDamagePerSecond * AuraInterval
                           * Combat.OutgoingMultiplier(Context.Sheet, true, spell.DamageType, spell.Type);

            Collider[] found = Physics.OverlapSphere(Context.Caster.transform.position + Vector3.up * 0.9f, mode.AuraRadius,
                Context.TargetMask, QueryTriggerInteraction.Ignore);

            var struck = new HashSet<IDamageable>();
            for (int i = 0; i < found.Length; i++)
            {
                IDamageable target = Combat.FindDamageable(found[i]);
                if (target == null || !target.IsAlive || !struck.Add(target)) continue;

                DamageInfo info = DamageInfo.Create(damage, spell.DamageType, Context.Team, Context.Caster);
                info.CanCrit = false;
                info.Origin = DamageOrigin.Spell;
                target.TakeDamage(info.At(AbilityContext.CenterOf(target), Vector3.up));
            }
        }

        private Health _dodgeSource;

        /// <summary>Follows the caster's health for dodges, which refund a little of any spell asking for it.</summary>
        private void HookDodges()
        {
            Health health = Context != null ? Context.Health : null;
            if (health == _dodgeSource) return;

            if (_dodgeSource != null) _dodgeSource.Dodged -= OnDodged;
            _dodgeSource = health;
            if (health != null) health.Dodged += OnDodged;
        }

        private void OnDodged(DamageInfo info)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                Spell spell = _slots[i];
                if (spell != null && spell.DodgeCooldownRefund > 0f)
                    _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - spell.DodgeCooldownRefund);
            }
        }

        private SustainRunner Runner(int slot)
        {
            if (_sustains[slot] != null) return _sustains[slot];

            var runner = new SustainRunner();
            runner.Ended += ended => OnSustainEnded(slot, ended);
            _sustains[slot] = runner;
            return runner;
        }

        /// <summary>A toggle's cooldown starts when it goes off, however that happened.</summary>
        private void OnSustainEnded(int slot, SustainRunner runner)
        {
            Spell spell = runner.Spell;
            if (spell != null && _slots[slot] == spell)
                _cooldowns[slot] = spell.CooldownAtLevel(Mathf.Max(1, GetLevel(spell)));

            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- casting

        private void Update() => Tick(Time.deltaTime);

        /// <summary>One frame of cooldowns, drains and charging. Update calls it; public so tooling can step time.</summary>
        public void Tick(float dt)
        {
            HookDodges();

            float rate = Context != null && Context.Sheet != null ? Context.Sheet.Get(Attr.CooldownRate) : 1f;
            float step = dt * rate;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_sustains[i] != null && _sustains[i].IsActive) _sustains[i].Tick(dt);
                else if (_cooldowns[i] > 0f) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - step);

                if (_charging[i]) _chargeHeld[i] += dt;
                TickStanceAura(i, dt);
            }
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

            // A stance is a state rather than a cast, and switching a toggle off is always allowed.
            if (spell.IsStance || IsSustainActive(slot)) return CastOutcome.Ready;

            if (Context.Status != null && Context.Status.IsSilenced) return CastOutcome.Silenced;
            if (_cooldowns[slot] > 0f) return CastOutcome.OnCooldown;

            return spell.IsSustained ? SustainRunner.CheckStart(spell, Context) : SpellCosts.Check(spell, Context);
        }

        /// <summary>
        /// Uses a slot at once and reports what happened: casts, switches a toggle, or steps a stance. A
        /// charged spell cast this way goes off at full charge, which is what tooling wants.
        /// </summary>
        public CastOutcome TryCastSlot(int slot)
        {
            CastOutcome outcome = Evaluate(slot);
            if (outcome != CastOutcome.Ready) return outcome;

            Spell spell = _slots[slot];
            if (spell.IsStance)
            {
                CycleStance(slot);
                return CastOutcome.StanceChanged;
            }

            return spell.IsSustained ? ToggleSustain(slot) : CastNow(slot, 1f);
        }

        /// <summary>The key went down. A charged spell starts charging; anything else is used at once.</summary>
        public CastOutcome PressSlot(int slot)
        {
            Spell spell = GetSlot(slot);
            if (spell == null || !spell.IsCharged) return TryCastSlot(slot);

            CastOutcome outcome = Evaluate(slot);
            if (outcome != CastOutcome.Ready) return outcome;

            _charging[slot] = true;
            _chargeHeld[slot] = 0f;
            return CastOutcome.Charging;
        }

        /// <summary>
        /// The key came up. Casts a charging spell at the charge reached, or nothing if let go below its
        /// minimum. The costs are checked again, since the pool may have changed while it was held.
        /// </summary>
        public CastOutcome ReleaseSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount || !_charging[slot]) return CastOutcome.Ready;

            float fraction = ChargeFraction(slot);
            _charging[slot] = false;
            _chargeHeld[slot] = 0f;

            Spell spell = _slots[slot];
            if (spell == null) return CastOutcome.NoSpell;
            if (fraction < spell.Charge.MinimumFraction) return CastOutcome.Charging;

            CastOutcome outcome = Evaluate(slot);
            return outcome != CastOutcome.Ready ? outcome : CastNow(slot, fraction);
        }

        private CastOutcome CastNow(int slot, float charge)
        {
            Spell spell = _slots[slot];
            int level = Mathf.Max(1, GetLevel(spell));
            Vector3 forward = Context.Aim != null ? Context.Aim.forward : Context.Caster.transform.forward;

            Context.Charge = charge;
            Context.SoulsSpent = SpellCosts.SoulsFor(spell, Context);
            int souls = Context.SoulsSpent;

            // The spell sets up the context, runs its effect chain, and tidies up after itself.
            bool cast = spell.Cast(Context, level);
            Context.Charge = 1f;

            if (!cast)
            {
                Context.SoulsSpent = 0;
                return CastOutcome.NoRoom;   // an effect aborted
            }

            SpellCosts.Pay(spell, Context);
            Context.SoulsSpent = 0;
            _cooldowns[slot] = spell.CooldownAtLevel(level);

            LastCast = new CastRecord
            {
                Spell = spell, Slot = slot, Level = level, Forward = forward, Charge = charge, SoulsSpent = souls
            };
            SpellCast?.Invoke(spell, slot);
            return CastOutcome.Cast;
        }

        private CastOutcome ToggleSustain(int slot)
        {
            SustainRunner runner = Runner(slot);
            if (runner.IsActive)
            {
                runner.End();
                return CastOutcome.ToggledOff;
            }

            Spell spell = _slots[slot];
            int level = Mathf.Max(1, GetLevel(spell));
            if (!runner.Begin(spell, Context, level)) return CastOutcome.NoRoom;

            LastCast = new CastRecord { Spell = spell, Slot = slot, Level = level, Charge = 1f };
            SpellCast?.Invoke(spell, slot);
            Changed?.Invoke();
            return CastOutcome.Cast;
        }

        /// <summary>Steps a stance to its next mode, wrapping round. It has no off.</summary>
        public void CycleStance(int slot)
        {
            Spell spell = GetSlot(slot);
            if (spell == null || !spell.IsStance) return;

            EnterStance(slot, _stanceModes[slot] + 1);
            Changed?.Invoke();
        }

        /// <summary>
        /// A repeat of an earlier cast for Echo: along the original direction, from where the caster is
        /// now, costing nothing, starting no cooldown and raising no cast event of its own, so it is
        /// never recorded to be echoed again. Toggles, stances and spells that never echo are refused.
        /// </summary>
        public bool CastEcho(Spell spell, int level, Vector3 forward, float charge = 1f, int soulsSpent = 0)
        {
            if (Context == null || !ActionLog.Echoable(spell)) return false;

            Context.IsEcho = true;
            Context.ForwardOverride = forward.sqrMagnitude > 0.0001f ? forward.normalized : (Vector3?)null;
            Context.Charge = charge;
            Context.SoulsSpent = soulsSpent;

            try
            {
                return spell.Cast(Context, Mathf.Max(1, level));
            }
            finally
            {
                Context.IsEcho = false;
                Context.ForwardOverride = null;
                Context.Charge = 1f;
                Context.SoulsSpent = 0;
            }
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

        /// <summary>Switches off every running toggle and drops every charge in progress.</summary>
        public void EndAllSustains()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_sustains[i] != null) _sustains[i].End();
                _charging[i] = false;
                _chargeHeld[i] = 0f;
            }
        }

        /// <summary>Forgets every spell and empties every slot, for the start of a fresh run.</summary>
        public void ResetBook()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                ClearSlot(i);
                _cooldowns[i] = 0f;
            }

            _known.Clear();
            _levels.Clear();
            _movement = null;
            _melee = null;
            Changed?.Invoke();
        }
    }
}
