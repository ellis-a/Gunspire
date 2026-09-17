using System;
using System.Reflection;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A boon that keeps doing something after it is picked: reacting to hits, kills, reloads or
    /// floors. The serialized instance on the boon is a template holding only parameters; taking the
    /// boon makes a live copy for the run, which stays bound until the run ends.
    ///
    /// A behaviour that also implements a rule interface (<see cref="IOutgoingDamageRule"/>,
    /// <see cref="IIncomingDamageRule"/>, <see cref="ILethalHitRule"/>, <see cref="ICritRule"/>) is
    /// registered for that rule automatically while it is bound.
    ///
    /// The common event hooks (<see cref="OnKill"/>, <see cref="OnAnyDamaged"/>, <see cref="OnGunHit"/>,
    /// <see cref="OnGunFired"/>, <see cref="OnGunMissed"/>, <see cref="OnSpellCast"/>) are subscribed only when
    /// a subclass overrides them. When the run ends, every sheet modifier and stat bonus this behaviour added
    /// with itself as the source is taken off.
    ///
    /// The live copy is a shallow copy of the template. A subclass holding mutable state in a
    /// reference type (a list, a set) must create it fresh in <see cref="OnBind"/>.
    /// </summary>
    [Serializable]
    public abstract class BoonBehaviour
    {
        [NonSerialized] private RunState _run;
        [NonSerialized] private int _level;
        [NonSerialized] private Hooks _hooks;
        [NonSerialized] private Weapon _hookedWeapon;
        [NonSerialized] private SpellBook _hookedBook;

        [Flags]
        private enum Hooks { None = 0, Kill = 1, Damaged = 2, GunHit = 4, GunFired = 8, GunMissed = 16, SpellCast = 32 }

        public RunState Run => _run;
        public int Level => _level;
        public PlayerRig Player => _run != null ? _run.Player : null;

        protected CharacterSheet Sheet => Player != null ? Player.Sheet : null;

        /// <summary>The run's clock, for windows and cooldowns. Advances only while the run is ticked.</summary>
        protected float Now => _run != null ? _run.Clock : 0f;
        protected Health PlayerHealth => Player != null ? Player.Health : null;

        internal BoonBehaviour Spawn(RunState run)
        {
            var live = (BoonBehaviour)MemberwiseClone();
            live._run = run;
            live._level = 0;
            live._hooks = Hooks.None;
            live._hookedWeapon = null;
            live._hookedBook = null;
            return live;
        }

        internal void SetLevel(int level)
        {
            int previous = _level;
            _level = level;

            if (previous == 0)
            {
                Subscribe();
                OnBind();
                CombatRules.Register(this);
            }
            OnLevelChanged(previous);
        }

        internal void Release()
        {
            CombatRules.Unregister(this);
            Unsubscribe();
            OnUnbind();

            CharacterSheet sheet = Sheet;
            if (sheet != null)
            {
                sheet.RemoveModifiersFrom(this);
                sheet.RemoveTypedModifiersFrom(this);
                sheet.RemoveStatBonuses(this);
            }
        }

        /// <summary>Taken for the first time. Subscribe to anything the common hooks do not cover here.</summary>
        protected virtual void OnBind() { }

        /// <summary>The run is ending. Undo whatever <see cref="OnBind"/> did beyond sheet modifiers.</summary>
        protected virtual void OnUnbind() { }

        /// <summary>After every pick, the first included. <see cref="Level"/> is already the new level.</summary>
        protected virtual void OnLevelChanged(int previous) { }

        public virtual void OnFloorEntered(RoomRuntime room) { }
        public virtual void OnFloorLeaving(int floor) { }
        public virtual void OnFloorCleared(RoomRuntime room) { }
        public virtual void OnFloorCompleted(RoomRuntime room) { }

        /// <summary>Every frame while playing, on game time. Only for the few that poll.</summary>
        public virtual void Tick(float dt) { }

        public virtual string Describe() => GetType().Name.Replace("Behaviour", "");

        // ---------------------------------------------------------------- common hooks

        /// <summary>An enemy died, whoever killed it. See <see cref="RunState.CountsAsKill"/>.</summary>
        protected virtual void OnKill(Health victim, DamageInfo info) { }

        /// <summary>Anything took damage.</summary>
        protected virtual void OnAnyDamaged(Health victim, DamageInfo hit, float amount) { }

        /// <summary>One of the player's gun rounds landed on something alive. Phantom and echoed rounds included; check.</summary>
        protected virtual void OnGunHit(WeaponHit hit) { }

        /// <summary>The player's gun fired a round. Phantom and echoed rounds included; check.</summary>
        protected virtual void OnGunFired(WeaponShot shot) { }

        /// <summary>One of the player's gun rounds struck nothing alive.</summary>
        protected virtual void OnGunMissed(Weapon weapon, int round) { }

        /// <summary>A spell was cast from a cast slot.</summary>
        protected virtual void OnSpellCast(Spell spell, int slot) { }

        private void Subscribe()
        {
            Type type = GetType();
            if (Overrides(type, nameof(OnKill))) _hooks |= Hooks.Kill;
            if (Overrides(type, nameof(OnAnyDamaged))) _hooks |= Hooks.Damaged;
            if (Overrides(type, nameof(OnGunHit))) _hooks |= Hooks.GunHit;
            if (Overrides(type, nameof(OnGunFired))) _hooks |= Hooks.GunFired;
            if (Overrides(type, nameof(OnGunMissed))) _hooks |= Hooks.GunMissed;
            if (Overrides(type, nameof(OnSpellCast))) _hooks |= Hooks.SpellCast;

            if ((_hooks & Hooks.Kill) != 0) Health.AnyDied += HandleDied;
            if ((_hooks & Hooks.Damaged) != 0) Health.AnyDamaged += OnAnyDamaged;

            _hookedWeapon = Player != null ? Player.Weapon : null;
            if (_hookedWeapon != null)
            {
                if ((_hooks & Hooks.GunHit) != 0) _hookedWeapon.Hit += OnGunHit;
                if ((_hooks & Hooks.GunFired) != 0) _hookedWeapon.Fired += OnGunFired;
                if ((_hooks & Hooks.GunMissed) != 0) _hookedWeapon.Missed += OnGunMissed;
            }

            _hookedBook = Player != null ? Player.Book : null;
            if (_hookedBook != null && (_hooks & Hooks.SpellCast) != 0) _hookedBook.SpellCast += OnSpellCast;
        }

        private void Unsubscribe()
        {
            Health.AnyDied -= HandleDied;
            Health.AnyDamaged -= OnAnyDamaged;

            if (_hookedWeapon != null)
            {
                _hookedWeapon.Hit -= OnGunHit;
                _hookedWeapon.Fired -= OnGunFired;
                _hookedWeapon.Missed -= OnGunMissed;
            }
            if (_hookedBook != null) _hookedBook.SpellCast -= OnSpellCast;

            _hooks = Hooks.None;
            _hookedWeapon = null;
            _hookedBook = null;
        }

        private void HandleDied(Health victim, DamageInfo info)
        {
            if (Player != null && RunState.CountsAsKill(victim)) OnKill(victim, info);
        }

        private static bool Overrides(Type type, string method)
        {
            MethodInfo found = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return found != null && found.DeclaringType != typeof(BoonBehaviour);
        }

        // ---------------------------------------------------------------- helpers

        protected bool IsPlayer(GameObject go) => go != null && Player != null && go == Player.gameObject;

        /// <summary>A hit the player's own body dealt: gun, spell, melee or a status they applied. Never an execute.</summary>
        protected bool IsOwnHit(in DamageInfo hit) => IsPlayer(hit.Source) && hit.Type != DamageType.Execute;

        /// <summary>An own hit that was a gun round, a spell or a swing, not a tick.</summary>
        protected bool IsDirectOwnHit(in DamageInfo hit)
            => IsOwnHit(hit) && (hit.Origin == DamageOrigin.Gun || hit.Origin == DamageOrigin.Spell || hit.Origin == DamageOrigin.Melee);

        /// <summary>A round from the player's own gun (not a phantom copy) of a class.</summary>
        protected static bool IsGunOfClass(Weapon weapon, WeaponClass weaponClass)
            => weapon != null && weapon.Definition != null && weapon.Definition.Class == weaponClass;

        protected static StatusController StatusOf(Health health) => health != null ? health.GetComponent<StatusController>() : null;

        protected T GetMastery<T>() where T : Mastery
            => Player != null && Player.Masteries != null ? Player.Masteries.Get<T>() : null;

        /// <summary>Keeps one percent modifier at a value, owned by this behaviour, replacing it only when it changes.</summary>
        protected void SetPercent(ref StatModifier modifier, Attr attr, float value)
        {
            CharacterSheet sheet = Sheet;
            if (sheet == null) return;
            if (modifier != null && Mathf.Approximately(modifier.Value, value)) return;

            if (modifier != null) sheet.RemoveModifier(modifier);
            modifier = Mathf.Approximately(value, 0f) ? null : sheet.AddPercent(attr, value, this, Describe());
        }

        /// <summary>Damage dealt on the player's behalf, reported as the given origin.</summary>
        protected DamageInfo PlayerDamage(float amount, DamageType type, DamageOrigin origin, Vector3 at)
        {
            DamageInfo info = DamageInfo.Create(amount, type, Team.Player, Player != null ? Player.gameObject : null);
            info.CanCrit = false;
            info.Origin = origin;
            return info.At(at, Vector3.up);
        }

        /// <summary>A blast on the player's side at a point, with a brief flash.</summary>
        protected int Blast(Vector3 at, float radius, float damage, DamageType type, Color tint, float knockback = 0f)
        {
            int hits = Combat.Explode(at, radius, PlayerDamage(damage, type, DamageOrigin.Mastery, at),
                Layers.PlayerHitMask, 0.5f, knockback);

            if (Application.isPlaying)
            {
                var colour = new Color(tint.r, tint.g, tint.b, 0.45f);
                GameObject flash = Build.Sphere(null, "BoonBlast", at, radius * 0.5f, MaterialLibrary.Transparent(colour), collider: false);
                FadeAndDie.Attach(flash, 0.25f, colour, Vector3.one * radius * 2.5f);
            }
            return hits;
        }

        /// <summary>The nearest living enemy to a point within a range, other than one to skip.</summary>
        protected static Health NearestEnemy(Vector3 around, float range, Health skip = null)
        {
            Collider[] found = Physics.OverlapSphere(around, range, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < found.Length; i++)
            {
                Health candidate = found[i].GetComponentInParent<Health>();
                if (candidate == null || candidate == skip || !candidate.IsAlive || candidate.Team != Team.Enemy) continue;

                float distance = (candidate.transform.position - around).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }
            return best;
        }

        protected static int EnemiesWithin(Vector3 around, float range)
        {
            Collider[] found = Physics.OverlapSphere(around, range, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            var seen = new System.Collections.Generic.HashSet<Health>();
            for (int i = 0; i < found.Length; i++)
            {
                Health candidate = found[i].GetComponentInParent<Health>();
                if (candidate != null && candidate.IsAlive && candidate.Team == Team.Enemy) seen.Add(candidate);
            }
            return seen.Count;
        }
    }

    /// <summary>Starts a <see cref="BoonBehaviour"/> on the first pick and raises its level on later ones.</summary>
    [Serializable]
    public class BoonBehaviourEffect : BoonEffect
    {
        [SerializeReference] public BoonBehaviour Behaviour;

        public override void Apply(RunState run, int level)
        {
            if (run != null && Behaviour != null) run.LevelBehaviour(Behaviour, level);
        }

        public override string Describe() => Behaviour != null ? Behaviour.Describe() : "no behaviour";
    }
}
