using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
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

        public bool IsFrozen => Has(StatusId.Freeze);

        /// <summary>True while the entity should not be allowed to move or attack.</summary>
        public bool IsControlImpaired => IsFrozen;

        private void Update()
        {
            if (_active.Count == 0) return;
            float dt = Time.deltaTime;

            // Copy first: a tick can kill the entity, which clears the live list.
            _scratch.Clear();
            _scratch.AddRange(_active);

            for (int i = 0; i < _scratch.Count; i++)
            {
                ActiveStatus s = _scratch[i];
                if (!_active.Contains(s)) continue;

                s.Remaining -= dt;

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

        public void Apply(in StatusApplication app, GameObject source, Team sourceTeam)
        {
            if (Health != null && !Health.IsAlive) return;

            StatusDefinition def = StatusLibrary.Get(app.Id);
            if (def == null) return;

            // Chill saturating into a hard freeze is the ice payoff, so check before stacking.
            if (app.Id == StatusId.Chill)
            {
                int current = Stacks(StatusId.Chill);
                if (current + app.Stacks >= def.MaxStacks && !Has(StatusId.Freeze))
                {
                    Remove(StatusId.Chill);
                    var freeze = new StatusApplication(StatusId.Freeze, 1.6f, 1, 1f);
                    Apply(freeze, source, sourceTeam);
                    return;
                }
            }

            StatusId[] cleanses = def.Cleanses;
            if (cleanses != null)
                for (int i = 0; i < cleanses.Length; i++) Remove(cleanses[i]);

            ActiveStatus existing = Find(app.Id);
            if (existing != null)
            {
                existing.Stacks = Mathf.Clamp(existing.Stacks + Mathf.Max(1, app.Stacks), 1, def.MaxStacks);
                existing.Duration = Mathf.Max(existing.Duration, app.Duration);
                existing.Remaining = Mathf.Max(existing.Remaining, app.Duration);
                existing.Magnitude = Mathf.Max(existing.Magnitude, app.Magnitude);
                existing.Source = source;
                existing.SourceTeam = sourceTeam;
                RebuildModifiers(existing);
                Changed?.Invoke();
                return;
            }

            var status = new ActiveStatus
            {
                Def = def,
                Stacks = Mathf.Clamp(Mathf.Max(1, app.Stacks), 1, def.MaxStacks),
                Duration = app.Duration,
                Remaining = app.Duration,
                Magnitude = app.Magnitude,
                Source = source,
                SourceTeam = sourceTeam
            };

            _active.Add(status);
            RebuildModifiers(status);
            def.OnApplied(this, status);
            Changed?.Invoke();
        }

        public void ApplyAll(List<StatusApplication> apps, GameObject source, Team sourceTeam)
        {
            if (apps == null) return;
            for (int i = 0; i < apps.Count; i++) Apply(apps[i], source, sourceTeam);
        }

        public void Remove(StatusId id)
        {
            ActiveStatus s = Find(id);
            if (s != null) RemoveInstance(s);
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

        /// <summary>Damage dealt by a periodic effect, credited back to whoever applied it.</summary>
        public void DealTickDamage(ActiveStatus s, float amount, DamageType type)
        {
            if (Health == null || !Health.IsAlive || amount <= 0f) return;

            DamageInfo info = DamageInfo.Create(amount, type, s.SourceTeam, s.Source);
            info.CanCrit = false;
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
