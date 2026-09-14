using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Hit points, resistances, healing and death for anything that can be shot.
    /// This is the single place damage is actually resolved.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private Team team = Team.Enemy;
        [SerializeField] private float fallbackMaxHealth = 100f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float destroyDelay = 0f;

        private CharacterSheet _sheet;
        private StatusController _statusCache;
        private readonly Dictionary<DamageType, float> _resistances = new Dictionary<DamageType, float>();

        /// <summary>Looked up lazily so component add order does not matter when building entities in code.</summary>
        private StatusController Status
            => _statusCache != null ? _statusCache : (_statusCache = GetComponent<StatusController>());

        public float Current { get; private set; }
        public float Max { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public Team Team { get { return team; } set { team = value; } }
        public Transform Transform => transform;
        public float Fraction => Max <= 0f ? 0f : Mathf.Clamp01(Current / Max);

        /// <summary>The player keeps their body on death so the run-over screen has something to sit behind.</summary>
        public bool DestroyOnDeath { get { return destroyOnDeath; } set { destroyOnDeath = value; } }

        /// <summary>Seconds of remaining invulnerability, e.g. during a dash.</summary>
        public float InvulnerabilityTimer { get; set; }
        public bool IsInvulnerable => InvulnerabilityTimer > 0f;

        public event Action<DamageInfo, float> Damaged;
        public event Action<float> Healed;
        public event Action<DamageInfo> Died;
        public event Action HealthChanged;

        /// <summary>Global hooks. Boons and lifesteal listen here instead of patching every weapon.</summary>
        public static event Action<Health, DamageInfo, float> AnyDamaged;
        public static event Action<Health, DamageInfo> AnyDied;

        private void Awake()
        {
            _sheet = GetComponent<CharacterSheet>();

            Max = ComputeMax();
            Current = Max;

            if (_sheet != null) _sheet.Changed += OnSheetChanged;
        }

        private void OnDestroy()
        {
            if (_sheet != null) _sheet.Changed -= OnSheetChanged;
        }

        private void Update()
        {
            if (!IsAlive) return;

            // The player's own clock for the player, the world's for everything else.
            float dt = WorldClock.DeltaFor(gameObject);

            if (InvulnerabilityTimer > 0f) InvulnerabilityTimer -= dt;

            if (_sheet != null && Current < Max)
            {
                float regen = _sheet.Get(Attr.HealthRegen);
                if (regen > 0f) Heal(regen * dt, silent: true);
            }
        }

        private float ComputeMax()
        {
            return _sheet != null ? _sheet.Get(Attr.MaxHealth) : fallbackMaxHealth;
        }

        private void OnSheetChanged()
        {
            float newMax = ComputeMax();
            if (Mathf.Approximately(newMax, Max)) return;

            // Growing the pool grants the extra hit points outright; shrinking just clamps.
            float delta = newMax - Max;
            Max = newMax;
            if (delta > 0f) Current += delta;
            Current = Mathf.Clamp(Current, 0f, Max);
            HealthChanged?.Invoke();
        }

        public void SetResistance(DamageType type, float value) => _resistances[type] = value;

        /// <summary>
        /// Total resistance to a school: this entity's own value plus anything the character
        /// sheet contributes. Negative is vulnerability, and clamps stop either extreme from
        /// making a target immune or one-shot.
        /// </summary>
        public float GetResistance(DamageType type)
        {
            if (!DamageTypes.IsResistable(type)) return 0f;

            _resistances.TryGetValue(type, out float own);
            float fromSheet = _sheet != null ? _sheet.Resistance(type) : 0f;
            return Mathf.Clamp(own + fromSheet, -0.9f, 0.9f);
        }

        /// <summary>Used when building enemies from code, before Awake stats exist.</summary>
        public void ConfigureMaxHealth(float value, bool refill = true)
        {
            fallbackMaxHealth = value;
            Max = ComputeMax();
            if (refill) Current = Max;
            HealthChanged?.Invoke();
        }

        // ---------------------------------------------------------------- damage

        /// <summary>
        /// Set from the enemy definition at spawn. Elites are exempt from the instant kills, so
        /// a frost stack or a death mark is a large chunk of their health rather than the whole
        /// fight - otherwise the answer to every elite would be one status and one bullet.
        /// </summary>
        public bool IsElite { get; set; }

        /// <summary>
        /// Ethereal from a spell, or simply what this thing is. Ghosts and cursed cultists take
        /// damage by the same rule; only the source of the state differs.
        /// </summary>
        public bool EtherealByNature { get; set; }

        public bool IsEthereal => EtherealByNature || (Status != null && Status.IsEthereal);

        /// <summary>
        /// The two executes a hit can set off: a full stack of frost taking a kinetic hit, and a
        /// death mark taking any hit at all. Returns true when it has dealt with the hit.
        /// </summary>
        private bool TryExecute(in DamageInfo info)
        {
            if (team == Team.Player) return false;

            bool marked = Status.ConsumeDeathmark();
            bool shattering = info.Type == DamageType.Kinetic && Status.IsFrozen;

            if (!marked && !shattering) return false;

            // A shattered elite survives, so it loses the frost that shattered it, or the very
            // next kinetic hit would shatter it again.
            if (shattering && IsElite) Status.Remove(StatusId.Frost);

            return Execute(info, marked ? DeathmarkStatus.EliteFraction : FrostStatus.EliteShatterFraction);
        }

        /// <summary>Pass as the elite fraction for an execute that finishes elites too, as Wither does.</summary>
        public const float FinishesElites = 1f;

        /// <summary>
        /// Skips the health bar. Reported under <see cref="DamageType.Execute"/> whatever set it
        /// off, so anything that must ignore executes - lifesteal, Prismatic Chains - can tell.
        ///
        /// An elite loses <paramref name="eliteFraction"/> of its maximum health instead of dying,
        /// so a status is a large chunk of an elite rather than the whole fight. Pass
        /// <see cref="FinishesElites"/> to finish elites as well. Never fires on the player: an
        /// instant death from a status the HUD had a second to show would read as the game
        /// breaking rather than as a mistake. Returns false when nothing happened.
        /// </summary>
        public bool Execute(in DamageInfo cause, float eliteFraction)
        {
            if (!IsAlive || team == Team.Player || IsInvulnerable) return false;

            DamageInfo info = cause;
            info.Type = DamageType.Execute;
            info.IsCrit = false;

            float dealt = IsElite && eliteFraction < FinishesElites
                ? Mathf.Min(Current, Max * Mathf.Max(0f, eliteFraction))
                : Current;

            if (dealt <= 0f) return false;

            Current = Mathf.Max(0f, Current - dealt);

            // An elite that survives was still hit, so anything damage breaks, like sleep, ends.
            if (Current > 0f && Status != null && info.Origin != DamageOrigin.StatusTick)
                Status.EndOnDamage(info.Statuses);

            Damaged?.Invoke(info, dealt);
            AnyDamaged?.Invoke(this, info, dealt);
            HealthChanged?.Invoke();

            if (Current <= 0f) Die(info);
            return true;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;
            if (info.SourceTeam == team && team != Team.Neutral) return;   // no friendly fire

            // Nothing is hit by its own attack. The team rule already covers this for the player
            // and enemies; it matters for the neutral team, where a confused enemy's own blast
            // would otherwise catch it.
            if (info.Source != null && info.Source == gameObject) return;

            // While the world is stopped, every hit on anything but the player waits, so nothing dies
            // frozen and the resume lands as one burst, knockback and all.
            if (WorldClock.ShouldHold(this))
            {
                WorldClock.Hold(this, info);
                return;
            }
            if (IsInvulnerable && info.Type != DamageType.True) return;

            float amount = Mathf.Max(0f, info.Amount);

            if (info.Type != DamageType.True)
            {
                // Ethereal comes first and can end the hit outright, before any multiplier has
                // a chance to make "immune" into "immune but for the fire damage on the bullet".
                if (IsEthereal)
                {
                    if (info.Type == DamageType.Kinetic) return;
                    amount *= EtherealStatus.VulnerabilityMultiplier;
                }

                amount *= Mathf.Max(0f, 1f - GetResistance(info.Type));

                if (Status != null && info.Amount > 0f) amount *= 1f + Status.ConsumeMark();

                if (_sheet != null) amount *= _sheet.Get(Attr.DamageTaken);
            }

            // A hit with no damage in it only delivers statuses. Letting it spend a death mark,
            // shatter frost or use up a mark would make a sleep bolt a finishing blow.
            if (Status != null && info.Amount > 0f && TryExecute(in info)) return;

            if (Status != null && info.Statuses != null)
                Status.ApplyAll(info.Statuses, info.Source, info.SourceTeam);

            if (amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);

            // After the damage lands, so the hit that applied sleep does not also end it, and
            // never from a status's own tick, so a sleeping enemy on fire stays asleep.
            if (Status != null && info.Origin != DamageOrigin.StatusTick) Status.EndOnDamage(info.Statuses);

            Damaged?.Invoke(info, amount);
            AnyDamaged?.Invoke(this, info, amount);
            HealthChanged?.Invoke();

            if (Current <= 0f) Die(info);
        }

        /// <summary>
        /// Spends health as a cost rather than taking damage. Skips resistances, statuses,
        /// invulnerability, lifesteal and the damage hooks, because none of those should apply
        /// to a price the player agreed to pay - a Fortify buff must not make a blood cost
        /// cheaper, and draining yourself must not proc your own on-hit effects.
        ///
        /// It can kill. Anything that would leave you dead should check first if that is not
        /// wanted.
        /// </summary>
        public float Drain(float amount)
        {
            if (!IsAlive || amount <= 0f) return 0f;

            float before = Current;
            Current = Mathf.Max(0f, Current - amount);
            float paid = before - Current;

            if (paid <= 0f) return 0f;

            HealthChanged?.Invoke();
            if (Current <= 0f) Die(DamageInfo.Create(0f, DamageType.True, Team.Neutral, null));

            return paid;
        }

        public float Heal(float amount, bool silent = false)
        {
            if (!IsAlive || amount <= 0f) return 0f;

            if (_sheet != null) amount *= _sheet.Get(Attr.HealingReceived);
            if (amount <= 0f) return 0f;

            float before = Current;
            Current = Mathf.Min(Max, Current + amount);
            float healed = Current - before;

            if (healed > 0f)
            {
                // Mending a bleed is the only thing that stops one, so healing has to be what
                // clears it. Regeneration counts: a silent trickle still closes the wound.
                if (Status != null) Status.OnHealed();

                if (!silent) Healed?.Invoke(healed);
                HealthChanged?.Invoke();
            }
            return healed;
        }

        /// <summary>
        /// Puts current health at an exact value, as Rewind needs: no healing scaling, no bleed
        /// cure, no damage or heal events and no statuses. Only the HUD refresh fires. It can never
        /// kill, since the value always comes from a moment the entity was alive, so anything below
        /// a sliver of health is held at that sliver.
        /// </summary>
        public void SetCurrent(float value)
        {
            if (!IsAlive) return;

            Current = Mathf.Clamp(value, Mathf.Min(MinimumSetHealth, Max), Max);
            HealthChanged?.Invoke();
        }

        private const float MinimumSetHealth = 1f;

        /// <summary>Brings a dead entity back. Used when a new run reuses the player object.</summary>
        public void Revive(float startingHealth = -1f)
        {
            IsAlive = true;
            Max = ComputeMax();
            Current = startingHealth > 0f ? Mathf.Min(startingHealth, Max) : Max;
            InvulnerabilityTimer = 0.5f;
            HealthChanged?.Invoke();
        }

        public void Kill()
        {
            if (!IsAlive) return;
            Current = 0f;
            Die(DamageInfo.Create(0f, DamageType.True, Team.Neutral, null));
        }

        private void Die(DamageInfo info)
        {
            if (!IsAlive) return;
            IsAlive = false;

            if (Status != null) Status.ClearAll();

            Died?.Invoke(info);
            AnyDied?.Invoke(this, info);

            if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        }
    }
}
