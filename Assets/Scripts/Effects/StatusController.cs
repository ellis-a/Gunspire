using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Runs the status effects on one entity: stacking, durations, ticks, and the stat
    /// modifiers each effect owns. Anything that wants to slow, burn or poison something
    /// calls <see cref="Apply"/> here.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatusController : MonoBehaviour
    {
        private readonly List<ActiveStatus> _active = new List<ActiveStatus>();
        private readonly List<ActiveStatus> _scratch = new List<ActiveStatus>();

        private CharacterSheet _sheet;
        private Health _health;

        // Resolved lazily: Health and StatusController each look the other up, so neither can
        // rely on being added first when an entity is assembled from code.
        public CharacterSheet Sheet => _sheet != null ? _sheet : (_sheet = GetComponent<CharacterSheet>());
        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());

        public IReadOnlyList<ActiveStatus> Active => _active;

        /// <summary>Raised when effects are added or removed, for HUD refreshes.</summary>
        public event Action Changed;

        /// <summary>
        /// Raised for every application, fresh or a top-up, after it has landed: the entity, which status,
        /// the source and the side it came from. Conflux listens for elements meeting on one target.
        /// </summary>
        public static event Action<StatusController, StatusId, GameObject, Team> AnyApplied;

        /// <summary>
        /// Frost at full stacks: a hundred percent slowed, so frozen in every sense that used to
        /// be a separate status. This is the state that kinetic damage finishes.
        /// </summary>
        public bool IsFrozen => Stacks(StatusId.Frost) >= FrostStatus.FullStacks;

        /// <summary>True while the entity can neither move nor act: frozen, asleep or stunned.</summary>
        public bool IsControlImpaired => IsFrozen || IsAsleep || IsStunned;

        public bool IsAsleep => Has(StatusId.Sleep);

        /// <summary>Cannot see. An enemy keeps fighting wherever it last saw its target.</summary>
        public bool IsBlind => Has(StatusId.Blind);

        /// <summary>A snare at full strength.</summary>
        public bool IsStunned => SnareStatus.Stuns(Find(StatusId.Snare));

        public bool IsFeared => Has(StatusId.Fear);
        public bool IsSilenced => Has(StatusId.Silence);
        public bool IsDisarmed => Has(StatusId.Disarm);
        public bool IsConfused => Has(StatusId.Confusion);

        /// <summary>Whether it may move at all. A snare short of a stun only slows.</summary>
        public bool CanMove => !IsControlImpaired;

        /// <summary>
        /// Whether it may start an attack of the given reach. Fear stops every attack. Silence stops
        /// ranged attacks and disarm stops melee, since enemies have attacks rather than spells and guns.
        /// </summary>
        public bool CanAttack(AttackReach reach)
        {
            if (IsControlImpaired || IsFeared) return false;
            return reach == AttackReach.Melee ? !IsDisarmed : !IsSilenced;
        }

        /// <summary>
        /// Held still by something outside time, such as banishment: durations and ticks stop where
        /// they are, and the modifiers the statuses own stay in place.
        /// </summary>
        public bool Paused { get; set; }

        public bool IsEthereal => Has(StatusId.Ethereal);

        /// <summary>Removes the death mark if present, and reports whether there was one.</summary>
        public bool ConsumeDeathmark()
        {
            ActiveStatus s = Find(StatusId.Deathmark);
            if (s == null) return false;
            RemoveInstance(s);
            return true;
        }

        /// <summary>
        /// Called when the entity is healed. Bleeding never expires on a timer, so mending it is
        /// the only way it ends - and healing through a bleed should feel like it accomplished
        /// something rather than being immediately undone.
        /// </summary>
        public void OnHealed()
        {
            Remove(StatusId.Bleed);
        }

        /// <summary>
        /// How fast this entity is actually moving, from its own position rather than from any
        /// motor. Poison reads it to fall off faster while the victim holds still, and the
        /// player and the enemies have entirely different movement code to ask otherwise.
        /// </summary>
        public float Speed { get; private set; }

        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        // The player's statuses keep real time; everyone else's stop with the world.
        private void Update() => Tick(WorldClock.DeltaFor(gameObject));

        /// <summary>One step of decay and ticks. Update calls it every frame; public so tooling can step time.</summary>
        public void Tick(float dt)
        {

            if (dt > 0f)
            {
                Speed = _hasLastPosition ? Vector3.Distance(transform.position, _lastPosition) / dt : 0f;
                _lastPosition = transform.position;
                _hasLastPosition = true;
            }

            if (Paused || _active.Count == 0) return;

            // Copy first: a tick can kill the entity, which clears the live list.
            _scratch.Clear();
            _scratch.AddRange(_active);

            for (int i = 0; i < _scratch.Count; i++)
            {
                ActiveStatus s = _scratch[i];
                if (!_active.Contains(s)) continue;

                s.Remaining -= dt * Mathf.Max(0f, s.Def.DecayScale(this, s));

                float interval = Mathf.Max(0.05f, s.Def.TickInterval);
                s.TickTimer += dt;
                while (s.TickTimer >= interval)
                {
                    s.TickTimer -= interval;
                    s.Def.OnTick(this, s);
                    if (Health != null && !Health.IsAlive) return;
                }

                if (s.Remaining <= 0f) RemoveInstance(s);
            }
        }

        // ---------------------------------------------------------------- queries

        public bool Has(StatusId id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Def.Id == id) return true;
            return false;
        }

        public int Stacks(StatusId id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Def.Id == id) return _active[i].Stacks;
            return 0;
        }

        public ActiveStatus Find(StatusId id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Def.Id == id) return _active[i];
            return null;
        }

        // ---------------------------------------------------------------- mutation

        /// <summary>
        /// Puts a status on this entity, or tops up the one already running. Elites take the status's
        /// shorter elite duration unless <paramref name="scaleForElite"/> is off, which copying a
        /// status that was already scaled needs.
        /// </summary>
        public void Apply(in StatusApplication app, GameObject source, Team sourceTeam, bool scaleForElite = true)
        {
            if (Health != null && !Health.IsAlive) return;

            StatusDefinition def = StatusLibrary.Get(app.Id);
            if (def == null) return;

            CharacterSheet sourceSheet = source != null ? source.GetComponent<CharacterSheet>() : null;

            // Frost needs no special case any more: saturating at a hundred stacks IS the
            // freeze, so the clamp below does what a second status used to have to.
            StatusId[] cleanses = def.Cleanses;
            if (cleanses != null)
                for (int i = 0; i < cleanses.Length; i++) Remove(cleanses[i]);

            float duration = app.Duration
                             * (scaleForElite && Health != null && Health.IsElite ? def.EliteDurationScale : 1f);

            ActiveStatus existing = Find(app.Id);
            if (existing != null)
            {
                existing.Stacks = Mathf.Clamp(existing.Stacks + Mathf.Max(1, app.Stacks), 1, def.MaxStacks);
                existing.Duration = Mathf.Max(existing.Duration, duration);
                existing.Remaining = Mathf.Max(existing.Remaining, duration);
                existing.Magnitude = Mathf.Max(existing.Magnitude, app.Magnitude);
                existing.Source = source;
                existing.SourceSheet = sourceSheet;
                existing.SourceTeam = sourceTeam;

                // A top-up without a source keeps the last known position, rather than forgetting it.
                if (source != null)
                {
                    existing.SourcePosition = source.transform.position;
                    existing.HasSourcePosition = true;
                }

                RebuildModifiers(existing);
                Changed?.Invoke();
                AnyApplied?.Invoke(this, app.Id, source, sourceTeam);
                return;
            }

            var status = new ActiveStatus
            {
                Def = def,
                Stacks = Mathf.Clamp(Mathf.Max(1, app.Stacks), 1, def.MaxStacks),
                Duration = duration,
                Remaining = duration,
                Magnitude = app.Magnitude,
                Source = source,
                SourceSheet = sourceSheet,
                SourceTeam = sourceTeam,
                SourcePosition = source != null ? source.transform.position : Vector3.zero,
                HasSourcePosition = source != null
            };

            _active.Add(status);
            RebuildModifiers(status);
            def.OnApplied(this, status);
            Changed?.Invoke();
            AnyApplied?.Invoke(this, app.Id, source, sourceTeam);
        }

        public void ApplyAll(List<StatusApplication> apps, GameObject source, Team sourceTeam)
        {
            if (apps == null) return;
            for (int i = 0; i < apps.Count; i++) Apply(apps[i], source, sourceTeam);
        }

        /// <summary>
        /// Ends every status that direct damage breaks, except any the same hit just applied.
        /// Health calls this once damage has landed, and never for a status tick.
        /// </summary>
        public void EndOnDamage(List<StatusApplication> appliedByHit)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (i >= _active.Count) continue;

                ActiveStatus s = _active[i];
                if (s.Def.EndsOnDamage && !AppliedBy(appliedByHit, s.Def.Id)) RemoveInstance(s);
            }
        }

        /// <summary>
        /// Applies every status another entity has, as it stands right now, credited to the same
        /// appliers. Durations are already scaled for whoever carries them, so they are not scaled
        /// again. <paramref name="skip"/> leaves out statuses the copy should not inherit.
        /// </summary>
        public void CopyFrom(StatusController other, Predicate<StatusId> skip = null)
        {
            if (other == null) return;

            for (int i = 0; i < other._active.Count; i++)
            {
                ActiveStatus s = other._active[i];
                if (skip != null && skip(s.Def.Id)) continue;

                Apply(new StatusApplication(s.Def.Id, s.Remaining, s.Stacks, s.Magnitude),
                    s.Source, s.SourceTeam, scaleForElite: false);

                ActiveStatus copied = Find(s.Def.Id);
                if (copied != null && s.HasSourcePosition)
                {
                    copied.SourcePosition = s.SourcePosition;
                    copied.HasSourcePosition = true;
                }
            }
        }

        private static bool AppliedBy(List<StatusApplication> apps, StatusId id)
        {
            if (apps == null) return false;
            for (int i = 0; i < apps.Count; i++)
                if (apps[i].Id == id) return true;
            return false;
        }

        public void Remove(StatusId id)
        {
            ActiveStatus s = Find(id);
            if (s != null) RemoveInstance(s);
        }

        /// <summary>Removes one debuff chosen at random, if there is one. Flicker and Embiggen. Returns what went.</summary>
        public StatusDefinition RemoveRandomDebuff()
        {
            _scratch.Clear();
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Def.IsDebuff) _scratch.Add(_active[i]);

            if (_scratch.Count == 0) return null;

            ActiveStatus pick = _scratch[UnityEngine.Random.Range(0, _scratch.Count)];
            RemoveInstance(pick);
            return pick.Def;
        }

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) RemoveInstance(_active[i]);
        }

        /// <summary>Removes Mark if present and reports the damage bonus it grants.</summary>
        public float ConsumeMark()
        {
            ActiveStatus s = Find(StatusId.Mark);
            if (s == null) return 0f;
            float bonus = s.Magnitude;
            RemoveInstance(s);
            return bonus;
        }

        private void RemoveInstance(ActiveStatus s)
        {
            if (!_active.Remove(s)) return;
            ClearModifiers(s);
            s.Def.OnRemoved(this, s);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- helpers used by definitions

        /// <summary>Registers a modifier owned by this status instance. Removed automatically on expiry.</summary>
        public void AddModifier(ActiveStatus s, StatModifier mod)
        {
            if (Sheet == null || mod == null) return;
            mod.Source = s;
            Sheet.AddModifier(mod);
            s.Mods.Add(mod);
        }

        /// <summary>
        /// Damage dealt by a periodic effect, credited back to whoever applied it. Scales on
        /// the applier's bonuses for that school, so Fire Mastery makes your burns burn.
        /// </summary>
        public void DealTickDamage(ActiveStatus s, float amount, DamageType type)
        {
            if (Health == null || !Health.IsAlive || amount <= 0f) return;

            if (s.SourceSheet != null)
                amount *= s.SourceSheet.Get(Attr.DamageDealt) * s.SourceSheet.DamageTypeMultiplier(type);

            DamageInfo info = DamageInfo.Create(amount, type, s.SourceTeam, s.Source);
            info.CanCrit = false;
            info.Origin = DamageOrigin.StatusTick;
            info.HitPoint = transform.position + Vector3.up;
            info.HitNormal = Vector3.up;
            Health.TakeDamage(info);
        }

        private void RebuildModifiers(ActiveStatus s)
        {
            ClearModifiers(s);
            s.Def.BuildModifiers(this, s);
        }

        private void ClearModifiers(ActiveStatus s)
        {
            if (Sheet != null)
                for (int i = 0; i < s.Mods.Count; i++) Sheet.RemoveModifier(s.Mods[i]);
            s.Mods.Clear();
        }
    }
}
