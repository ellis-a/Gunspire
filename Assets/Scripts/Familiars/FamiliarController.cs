using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A summoned ally. Floats near the player, picks the nearest enemy it can see, and runs
    /// the same <see cref="AbilityAttack"/> chains enemies use - on the player's team.
    ///
    /// Movement is written directly to the transform rather than through a CharacterController.
    /// A familiar has no business colliding with the level: it drifts, it does not walk, and
    /// giving it a physical body only creates ways for it to shove the player or get stuck on a
    /// crate. It still carries a collider so blasts and beams can kill it.
    /// </summary>
    public class FamiliarController : MonoBehaviour, IAbilityOwner
    {
        private static readonly List<FamiliarController> Live = new List<FamiliarController>();
        private static readonly Collider[] SearchBuffer = new Collider[48];

        public static IReadOnlyList<FamiliarController> All => Live;

        public FamiliarDefinition Definition { get; private set; }

        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        public Team Team => Team.Player;
        public DamageOrigin AttackOrigin => DamageOrigin.Minion;

        public Transform Muzzle { get; set; }
        public Transform Target { get; private set; }
        public CharacterSheet Sheet { get; private set; }
        public Health Health { get; private set; }
        public StatusController Status { get; private set; }

        private PlayerRig _owner;
        private readonly List<AbilityAttack> _attacks = new List<AbilityAttack>();
        private float _retargetTimer;
        private float _bobPhase;
        private bool _auraApplied;
        private float _perkTimer;
        private TargetRegistry.Entry _decoyEntry;

        /// <summary>Enemy projectiles this familiar has blocked, for tooling.</summary>
        public int Blocked { get; private set; }

        // ---- empowerment, driven by spells like Fel Empowerment ----
        private float _empowerTimer;
        private float _empowerDamage = 1f;
        private float _empowerHealthCost;

        public bool IsEmpowered => _empowerTimer > 0f;
        public float EmpowermentRemaining => _empowerTimer;

        public bool IsAttacking
        {
            get
            {
                for (int i = 0; i < _attacks.Count; i++)
                    if (_attacks[i].IsExecuting) return true;
                return false;
            }
        }

        public void Initialise(FamiliarDefinition def, PlayerRig owner)
        {
            Definition = def;
            _owner = owner;

            Sheet = GetComponent<CharacterSheet>();
            Health = GetComponent<Health>();
            Status = GetComponent<StatusController>();

            _bobPhase = Random.Range(0f, Mathf.PI * 2f);

            GetComponents(_attacks);
            for (int i = 0; i < _attacks.Count; i++)
            {
                _attacks[i].Initialise(this);
                _attacks[i].DamageMultiplier = def.DamageMultiplier;
            }

            if (Health != null)
            {
                Health.Died += OnDied;
                Health.Damaged += OnDamaged;
            }

            if (def.Perk == FamiliarPerk.Decoy && _decoyEntry == null)
                _decoyEntry = TargetRegistry.RegisterMinion(transform, Health);

            ApplyAura();
        }

        private void OnEnable() => Live.Add(this);

        private void OnDisable()
        {
            Live.Remove(this);

            // Covers being destroyed with the room as well as dying, so the aura can never
            // outlive the familiar that granted it.
            RemoveAura();
        }

        private void OnDestroy()
        {
            ReleaseDecoy();
            if (Health == null) return;
            Health.Died -= OnDied;
            Health.Damaged -= OnDamaged;
        }

        // ---------------------------------------------------------------- aura

        private void ApplyAura()
        {
            if (_auraApplied || Definition == null || _owner == null || _owner.Sheet == null) return;

            for (int i = 0; i < Definition.Auras.Count; i++)
            {
                FamiliarAura aura = Definition.Auras[i];
                if (aura == null) continue;

                if (aura.Mode == ModifierMode.Percent)
                    _owner.Sheet.AddPercent(aura.Attribute, aura.Amount, this, Definition.DisplayName);
                else
                    _owner.Sheet.AddFlat(aura.Attribute, aura.Amount, this, Definition.DisplayName);
            }

            if (Definition.Perk == FamiliarPerk.LuckAura)
                _owner.Sheet.SetStatBonus(this, StatType.Luck, Mathf.RoundToInt(Definition.PerkAmount));

            _auraApplied = true;
        }

        private void RemoveAura()
        {
            if (!_auraApplied || _owner == null || _owner.Sheet == null) return;

            // Keyed on this component, so it lifts exactly this familiar's contribution even
            // with several of them granting the same attribute.
            _owner.Sheet.RemoveModifiersFrom(this);
            _owner.Sheet.RemoveStatBonuses(this);
            _auraApplied = false;
        }

        private void ReleaseDecoy()
        {
            if (_decoyEntry == null) return;
            TargetRegistry.Unregister(_decoyEntry);
            _decoyEntry = null;
        }

        private void OnDied(DamageInfo info)
        {
            RemoveAura();
            ReleaseDecoy();

            Vector3 at = transform.position;
            Color tint = Definition != null ? Definition.BodyColor : Palette.Arcane;

            GameObject pop = Build.Sphere(null, "FamiliarFade", at, Definition != null ? Definition.BodyDiameter * 2f : 1f,
                MaterialLibrary.Transparent(new Color(tint.r, tint.g, tint.b, 0.5f)), collider: false);
            FadeAndDie.Attach(pop, 0.35f, new Color(tint.r, tint.g, tint.b, 0.5f), Vector3.one * 3f);

            if (Definition != null && GameDirector.Instance != null)
                GameDirector.Instance.Notify(Definition.DisplayName + " has fallen", 1.4f);

            Destroy(gameObject);
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            // Nothing to steer, but a familiar being chipped away should be visible.
            Sfx.PlayAt(SoundLibrary.Impact(info.Type), transform.position, 0.4f);
        }

        // ---------------------------------------------------------------- empowerment

        /// <summary>
        /// Applied by buff spells. Re-applying refreshes rather than stacks, so holding two
        /// copies of the same spell is not a damage multiplier.
        /// </summary>
        public void Empower(float damageMultiplier, float duration, float healthCostPerAttack)
        {
            _empowerTimer = Mathf.Max(_empowerTimer, duration);
            _empowerDamage = Mathf.Max(_empowerDamage, damageMultiplier);
            _empowerHealthCost = Mathf.Max(_empowerHealthCost, healthCostPerAttack);

            RefreshAttackDamage();
        }

        private void RefreshAttackDamage()
        {
            float scale = Definition != null ? Definition.DamageMultiplier : 1f;
            if (IsEmpowered) scale *= _empowerDamage;

            for (int i = 0; i < _attacks.Count; i++) _attacks[i].DamageMultiplier = scale;
        }

        /// <summary>
        /// The price of an empowered swing. Called by the familiar itself when an attack
        /// starts, so the cost tracks attacks made rather than seconds elapsed - which is what
        /// makes empowering a fast familiar in an empty room harmless and doing it mid-fight
        /// a real decision.
        /// </summary>
        private void PayForAttack()
        {
            if (!IsEmpowered || _empowerHealthCost <= 0f) return;
            if (_owner == null || _owner.Health == null || !_owner.Health.IsAlive) return;

            // Straight to Current rather than through TakeDamage: this is a price paid, not an
            // attack on the player, and it must not trigger resistances, statuses or lifesteal.
            _owner.Health.Drain(_empowerHealthCost);
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (_owner == null || Health == null || !Health.IsAlive) return;

            // Familiars are yours but not you, so they stop with the world.
            if (WorldClock.IsStopped) return;

            if (_empowerTimer > 0f)
            {
                _empowerTimer -= WorldClock.DeltaTime;
                if (_empowerTimer <= 0f)
                {
                    _empowerDamage = 1f;
                    _empowerHealthCost = 0f;
                    RefreshAttackDamage();
                }
            }

            _retargetTimer -= WorldClock.DeltaTime;
            if (Target == null || _retargetTimer <= 0f) AcquireTarget();

            Drift();
            FaceTarget();
            TryAttack();
            StepPerk(WorldClock.DeltaTime);
        }

        // ---------------------------------------------------------------- perks

        /// <summary>One step of the familiar's perk. Update calls it; public so tooling can drive it.</summary>
        public void StepPerk(float dt)
        {
            if (Definition == null || _owner == null) return;
            Vector3 around = _owner.transform.position;
            float range = Definition.PerkRange;

            switch (Definition.Perk)
            {
                case FamiliarPerk.BlockProjectiles:
                    if ((_perkTimer -= dt) > 0f) return;
                    if (BlockNearestProjectile(around, range)) _perkTimer = Definition.PerkInterval;
                    return;

                case FamiliarPerk.FetchOrbs:
                    foreach (OrbPickup orb in OrbPickup.Live)
                        if ((orb.transform.position - around).sqrMagnitude <= range * range) orb.Attracted = true;
                    return;

                case FamiliarPerk.ManaNearEnemies:
                    if (_owner.Mana != null && EnemyNear(around, range)) _owner.Mana.Add(Definition.PerkAmount * dt);
                    return;

                case FamiliarPerk.ReloadHolstered:
                    if ((_perkTimer -= dt) > 0f) return;
                    _perkTimer = Definition.PerkInterval;
                    ReloadHolstered();
                    return;
            }
        }

        private bool BlockNearestProjectile(Vector3 around, float range)
        {
            Projectile nearest = null;
            float best = range * range;
            foreach (Projectile p in Projectile.Live)
            {
                if (p == null || p.OwnerTeam == Team.Player) continue;
                float distance = (p.transform.position - around).sqrMagnitude;
                if (distance > best) continue;
                best = distance;
                nearest = p;
            }

            if (nearest == null) return false;

            Combat.SpawnImpact(nearest.transform.position, Vector3.up, Definition.BodyColor, 0.4f);
            if (Application.isPlaying) Destroy(nearest.gameObject);
            else DestroyImmediate(nearest.gameObject);
            Blocked++;
            return true;
        }

        private static bool EnemyNear(Vector3 around, float range)
        {
            int count = Physics.OverlapSphereNonAlloc(around, range, SearchBuffer, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                IDamageable candidate = Combat.FindDamageable(SearchBuffer[i]);
                if (candidate != null && candidate.IsAlive && candidate.Team == Team.Enemy) return true;
            }
            return false;
        }

        /// <summary>Puts a share of a magazine back into every gun the player is not holding.</summary>
        private void ReloadHolstered()
        {
            Holster holster = _owner.Holster;
            if (holster == null) return;

            for (int i = 0; i < holster.SlotCount; i++)
            {
                if (i == holster.ActiveIndex || holster.GetSlot(i) == null) continue;

                int full = holster.MagazineIn(i);
                int ammo = holster.AmmoIn(i);
                if (ammo >= full) continue;
                holster.SetAmmo(i, ammo + Mathf.Max(1, Mathf.CeilToInt(full * Definition.PerkAmount)));
            }
        }

        private void AcquireTarget()
        {
            _retargetTimer = 0.4f;
            Target = null;

            if (_owner == null) return;

            // Searched around the player rather than around the familiar, so it never chases
            // something into the next room and leaves you alone.
            int count = Physics.OverlapSphereNonAlloc(_owner.transform.position, Definition.EngageRange,
                SearchBuffer, Layers.EnemyMask, QueryTriggerInteraction.Ignore);

            // The Seer picks by angle from the player's aim instead of by distance.
            bool byCrosshair = Definition.Perk == FamiliarPerk.CrosshairTarget;
            Transform aim = byCrosshair && _owner.SpellContext != null && _owner.SpellContext.Aim != null
                ? _owner.SpellContext.Aim
                : _owner.transform;

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                IDamageable candidate = Combat.FindDamageable(SearchBuffer[i]);
                if (candidate == null || !candidate.IsAlive || candidate.Team != Team.Enemy) continue;

                float distance = byCrosshair
                    ? Vector3.Angle(aim.forward, candidate.Transform.position + Vector3.up * 0.9f - aim.position)
                    : Vector3.SqrMagnitude(candidate.Transform.position - transform.position);
                if (distance >= best) continue;

                best = distance;
                Target = candidate.Transform;
            }
        }

        /// <summary>Floats toward a slot beside and above the player, easing rather than snapping.</summary>
        private void Drift()
        {
            _bobPhase += WorldClock.DeltaTime * 2f;

            Transform player = _owner.transform;
            Vector3 anchor = player.position
                             + Vector3.up * Definition.HoverHeight
                             - player.forward * Definition.FollowDistance * 0.5f
                             + player.right * Definition.FollowDistance;

            anchor += Vector3.up * Mathf.Sin(_bobPhase) * 0.12f;

            // Speed rises with distance so it can catch up after a Blink without drifting at a
            // constant crawl the rest of the time.
            float gap = Vector3.Distance(transform.position, anchor);
            float speed = Definition.MoveSpeed * Mathf.Clamp(gap * 0.5f, 0.4f, 4f);

            transform.position = Vector3.MoveTowards(transform.position, anchor, speed * WorldClock.DeltaTime);
        }

        private void FaceTarget()
        {
            Vector3 lookAt = Target != null
                ? Target.position + Vector3.up * 0.9f
                : transform.position + _owner.transform.forward;

            Vector3 to = lookAt - transform.position;
            if (to.sqrMagnitude < 0.001f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to.normalized, Vector3.up), 8f * WorldClock.DeltaTime);
        }

        private void TryAttack()
        {
            if (Target == null || IsAttacking) return;

            float distance = Vector3.Distance(transform.position, Target.position);
            bool los = HasLineOfSight();

            AbilityAttack best = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < _attacks.Count; i++)
            {
                AbilityAttack attack = _attacks[i];
                if (!attack.CanUse(distance, los)) continue;
                if (attack.Priority <= bestPriority) continue;

                bestPriority = attack.Priority;
                best = attack;
            }

            if (best == null) return;

            best.Begin();
            PayForAttack();
        }

        public bool HasLineOfSight()
        {
            if (Target == null) return false;

            Vector3 from = transform.position;
            Vector3 to = Target.position + Vector3.up * 0.9f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.5f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.25f,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- static helpers

        /// <summary>Empowers every live familiar. Used by buff spells.</summary>
        public static int EmpowerAll(float damageMultiplier, float duration, float healthCostPerAttack)
        {
            for (int i = 0; i < Live.Count; i++)
                Live[i].Empower(damageMultiplier, duration, healthCostPerAttack);

            return Live.Count;
        }

        public static void DespawnAll()
        {
            // Backwards, because each Destroy pulls its entry out of the list on disable.
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] != null) Destroy(Live[i].gameObject);

            Live.Clear();
        }
    }
}
