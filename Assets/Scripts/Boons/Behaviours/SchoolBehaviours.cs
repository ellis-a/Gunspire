using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Behaviours for the School boons, school by school. Each is offered only while a spell of its school is held.

    /// <summary>A school's spells put a status on what they hit, at most once per enemy per cooldown. Hemorrhage, Blinding Light, Enfeeble, Displacement.</summary>
    [System.Serializable]
    public class SchoolHitStatusBehaviour : BoonBehaviour
    {
        public SpellSchool School = SpellSchool.Abyssal;
        public StatusId Status = StatusId.Bleed;

        /// <summary>Seconds before the same enemy can be given it again. Zero for every hit.</summary>
        public float Cooldown;

        /// <summary>The status's strength at level 1 and what each further level adds: dps, share or seconds by status.</summary>
        public float Base = 3f;
        public float PerLevel = 1.5f;

        [System.NonSerialized] private Dictionary<Health, float> _ready;

        protected override void OnBind() => _ready = new Dictionary<Health, float>();
        public override void OnFloorEntered(RoomRuntime room) => _ready.Clear();

        protected override void OnAnyDamaged(Health victim, DamageInfo hit, float amount)
        {
            if (victim == null || !victim.IsAlive || victim.Team == Team.Player) return;
            if (!IsOwnHit(hit) || hit.Origin != DamageOrigin.Spell || hit.Spell == null || hit.Spell.School != School) return;
            StatusController status = StatusOf(victim);
            if (status == null) return;

            if (Cooldown > 0f)
            {
                if (_ready.TryGetValue(victim, out float at) && Now < at) return;
                _ready[victim] = Now + Cooldown;
            }

            status.Apply(Build(), Player.gameObject, Team.Player);
        }

        private StatusApplication Build()
        {
            float value = Base + PerLevel * (Level - 1);
            switch (Status)
            {
                case StatusId.Bleed: return StatusLibrary.Bleed(dps: value);
                case StatusId.Blind: return StatusLibrary.Blind(value);
                case StatusId.Weaken:
                    StatusApplication weaken = StatusLibrary.Weaken();
                    weaken.Magnitude = value;
                    return weaken;
                case StatusId.Exposed: return StatusLibrary.Exposed(extra: value);
                default: return new StatusApplication(Status, 4f, 1, value);
            }
        }

        public override string Describe() => School + " spells apply " + Status;
    }

    // ---------------------------------------------------------------- Elemental

    /// <summary>Elemental spells deal more for each element already on the target. Tri-Attuned.</summary>
    [System.Serializable]
    public class TriAttunedBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerElementPerLevel = 0.1f;

        private static readonly StatusId[] Elements = { StatusId.Burn, StatusId.Frost, StatusId.Shock };

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            if (!IsOwnHit(hit) || hit.Spell == null || hit.Spell.School != SpellSchool.Elemental) return 1f;
            StatusController status = StatusOf(target);
            if (status == null) return 1f;

            int count = 0;
            foreach (StatusId element in Elements)
                if (status.Has(element)) count++;
            return 1f + count * PerElementPerLevel * Level;
        }
    }

    /// <summary>An enemy dying with your burn, frost or shock on it passes them to a nearby enemy. Excess Force.</summary>
    [System.Serializable]
    public class ExcessForceBehaviour : BoonBehaviour
    {
        public float BaseRange = 6f;
        public float RangePerLevel = 2f;

        public int Jumps { get; private set; }

        private static readonly StatusId[] Elements = { StatusId.Burn, StatusId.Frost, StatusId.Shock };

        protected override void OnBind() => Health.AnyDying += OnDying;
        protected override void OnUnbind() => Health.AnyDying -= OnDying;

        private void OnDying(Health victim, DamageInfo killer)
        {
            if (victim == null || victim.Team != Team.Enemy || Player == null) return;
            StatusController status = StatusOf(victim);
            if (status == null) return;

            Health next = null;
            foreach (StatusId element in Elements)
            {
                ActiveStatus active = status.Find(element);
                if (active == null || !IsPlayer(active.Source)) continue;

                if (next == null) next = NearestEnemy(victim.transform.position, BaseRange + RangePerLevel * (Level - 1), victim);
                StatusController nextStatus = next != null ? StatusOf(next) : null;
                if (nextStatus == null) return;

                var app = new StatusApplication(element, Mathf.Max(0.5f, active.Remaining), active.Stacks, active.Magnitude);
                nextStatus.Apply(app, Player.gameObject, Team.Player, scaleForElite: false);
                Jumps++;
            }
        }
    }

    /// <summary>Gun hits carry the element of the last Elemental spell that landed. Imbued Rounds.</summary>
    [System.Serializable]
    public class ImbuedRoundsBehaviour : BoonBehaviour
    {
        public int FrostStacksPerLevel = 1;
        public float BurnPerLevel = 2f;
        public int ShockStacksPerLevel = 1;

        [System.NonSerialized] private StatusId? _element;
        public StatusId? Element => _element;

        protected override void OnBind() => StatusController.AnyApplied += OnApplied;
        protected override void OnUnbind() => StatusController.AnyApplied -= OnApplied;

        // An element the player's spell applies names the element. Guns' own applications are ignored, or the
        // imbued rounds would keep choosing themselves.
        private void OnApplied(StatusController target, StatusId id, GameObject source, Team team)
        {
            if (!ConfluxMastery.IsElement(id) || !IsPlayer(source) || _applying) return;
            if (Player.Book == null || Player.Book.LastCast.Spell == null) return;
            if (Player.Book.LastCast.Spell.School != SpellSchool.Elemental) return;
            _element = id;
        }

        [System.NonSerialized] private bool _applying;

        protected override void OnGunHit(WeaponHit hit)
        {
            if (_element == null || hit.Target == null || hit.Target.Transform == null) return;
            StatusController status = hit.Target.Transform.GetComponent<StatusController>();
            if (status == null) return;

            StatusApplication app;
            switch (_element)
            {
                case StatusId.Burn: app = StatusLibrary.Burn(amount: BurnPerLevel * Level); break;
                case StatusId.Frost: app = StatusLibrary.Frost(stacks: FrostStacksPerLevel * Level); break;
                default: app = StatusLibrary.Shock(stacks: ShockStacksPerLevel * Level); break;
            }

            _applying = true;
            try { status.Apply(app, Player.gameObject, Team.Player); }
            finally { _applying = false; }
        }
    }

    // ---------------------------------------------------------------- Bestial

    /// <summary>You deal more to whatever your companion is attacking. Pack Tactics.</summary>
    [System.Serializable]
    public class PackTacticsBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerLevel = 0.1f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            if (!IsOwnHit(hit)) return 1f;
            BeastMastery beast = GetMastery<BeastMastery>();
            MinionController companion = beast != null ? beast.Companion : null;
            return companion != null && companion.Target == target.transform ? 1f + PerLevel * Level : 1f;
        }
    }

    /// <summary>When your companion dies, you are empowered for a while. Vengeful Rage.</summary>
    [System.Serializable]
    public class VengefulRageBehaviour : BoonBehaviour
    {
        public float Seconds = 10f;
        public float PerLevel = 1f;

        [System.NonSerialized] private BeastMastery _beast;

        protected override void OnBind()
        {
            _beast = GetMastery<BeastMastery>();
            if (_beast != null) _beast.CompanionDied += OnCompanionDied;
        }

        protected override void OnUnbind()
        {
            if (_beast != null) _beast.CompanionDied -= OnCompanionDied;
        }

        private void OnCompanionDied()
        {
            if (Player == null || Player.Status == null) return;
            Player.Status.Remove(StatusId.Empowered);
            Player.Status.Apply(StatusLibrary.Empowered(Seconds, PerLevel * Level), Player.gameObject, Team.Player);
        }
    }

    /// <summary>Your companion's hits carry your gun enchantments, at a share of their strength. Shared Instinct.</summary>
    [System.Serializable]
    public class SharedInstinctBehaviour : BoonBehaviour
    {
        public float SharePerLevel = 0.5f;

        protected override void OnAnyDamaged(Health victim, DamageInfo hit, float amount)
        {
            if (victim == null || victim.Team == Team.Player || hit.Origin == DamageOrigin.StatusTick) return;

            BeastMastery beast = GetMastery<BeastMastery>();
            if (beast == null || !beast.IsCompanionSource(hit.Source)) return;

            IReadOnlyList<EnchantmentBehaviour> enchantments = EnchantmentBehaviour.Active;
            for (int i = 0; i < enchantments.Count; i++)
                if (enchantments[i].Run == Run) enchantments[i].ApplyTo(victim, amount * SharePerLevel * Level);
        }
    }

    /// <summary>Kills by your companion shorten your Bestial cooldowns. Blooded.</summary>
    [System.Serializable]
    public class BloodedBehaviour : BoonBehaviour
    {
        public float SecondsPerLevel = 1f;

        protected override void OnKill(Health victim, DamageInfo info)
        {
            BeastMastery beast = GetMastery<BeastMastery>();
            if (beast == null || !beast.IsCompanionSource(info.Source) || Player.Book == null) return;
            Player.Book.ReduceCooldowns(SecondsPerLevel * Level, SpellSchool.Bestial);
        }
    }

    // ---------------------------------------------------------------- Abyssal

    /// <summary>Faster the deeper in debt, up to a cap. Riding the Current.</summary>
    [System.Serializable]
    public class RidingTheCurrentBehaviour : BoonBehaviour
    {
        public float DebtPerStep = 20f;
        public float SpeedPerStepPerLevel = 0.03f;
        public float Cap = 0.45f;

        [System.NonSerialized] private StatModifier _speed;

        public override void Tick(float dt)
        {
            BloodDebtMastery debt = GetMastery<BloodDebtMastery>();
            float steps = debt != null ? Mathf.Floor(debt.Debt / Mathf.Max(1f, DebtPerStep)) : 0f;
            SetPercent(ref _speed, Attr.MoveSpeed, Mathf.Min(Cap, steps * SpeedPerStepPerLevel * Level));
        }
    }

    /// <summary>Clearing the debt makes the killing blow explode for what that kill repaid. Foreclosure.</summary>
    [System.Serializable]
    public class ForeclosureBehaviour : BoonBehaviour
    {
        public float Radius = 4f;
        public float ScalePerLevel = 1f;

        private static readonly Color Tint = new Color(0.7f, 0.1f, 0.2f);

        [System.NonSerialized] private BloodDebtMastery _debt;
        public int Blasts { get; private set; }

        protected override void OnBind()
        {
            _debt = GetMastery<BloodDebtMastery>();
            if (_debt != null) _debt.Repaid += OnRepaid;
        }

        protected override void OnUnbind()
        {
            if (_debt != null) _debt.Repaid -= OnRepaid;
        }

        private void OnRepaid(float amount, bool cleared, Health victim)
        {
            if (!cleared || victim == null || amount <= 0f) return;
            Blasts++;
            Blast(victim.transform.position + Vector3.up, Radius, amount * ScalePerLevel * Level, DamageType.Necrotic, Tint);
        }
    }

    /// <summary>Repaying debt sends out a wave that knocks enemies back. Tidal Surge.</summary>
    [System.Serializable]
    public class TidalSurgeBehaviour : BoonBehaviour
    {
        public float Radius = 5f;
        public float RadiusPerLevel = 1.5f;
        public float Force = 12f;

        private static readonly Color Tint = new Color(0.2f, 0.5f, 0.9f);

        [System.NonSerialized] private BloodDebtMastery _debt;
        public int Waves { get; private set; }

        protected override void OnBind()
        {
            _debt = GetMastery<BloodDebtMastery>();
            if (_debt != null) _debt.Repaid += OnRepaid;
        }

        protected override void OnUnbind()
        {
            if (_debt != null) _debt.Repaid -= OnRepaid;
        }

        private void OnRepaid(float amount, bool cleared, Health victim)
        {
            if (Player == null || amount <= 0f) return;
            Waves++;
            Blast(Player.transform.position + Vector3.up, Radius + RadiusPerLevel * (Level - 1), 0f, DamageType.Kinetic, Tint, Force);
        }
    }

    // ---------------------------------------------------------------- Divination

    /// <summary>Headshots on an enemy your Divination spell hit recently deal more. Guided Shots.</summary>
    [System.Serializable]
    public class GuidedShotsBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Window = 5f;
        public float PerLevel = 0.15f;

        [System.NonSerialized] private Dictionary<Health, float> _guided;

        protected override void OnBind() => _guided = new Dictionary<Health, float>();
        public override void OnFloorEntered(RoomRuntime room) => _guided.Clear();

        protected override void OnAnyDamaged(Health victim, DamageInfo hit, float amount)
        {
            if (victim == null || victim.Team == Team.Player || !IsOwnHit(hit)) return;
            if (hit.Origin == DamageOrigin.Spell && hit.Spell != null && hit.Spell.School == SpellSchool.Divination)
                _guided[victim] = Now;
        }

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            if (!IsOwnHit(hit) || hit.Origin != DamageOrigin.Gun || !hit.IsHeadshot) return 1f;
            return _guided.TryGetValue(target, out float at) && Now - at <= Window ? 1f + PerLevel * Level : 1f;
        }
    }

    /// <summary>Using the movement spell as an attack is about to land refunds it and blinds the attacker. Perfect Timing.</summary>
    [System.Serializable]
    public class PerfectTimingBehaviour : BoonBehaviour
    {
        public float Range = 20f;
        public float Window = 0.35f;
        public float WindowPerLevel = 0.15f;
        public float BlindSeconds = 3f;

        [System.NonSerialized] private MovementController _movement;
        public int Refunds { get; private set; }

        protected override void OnBind()
        {
            _movement = Player.Movement;
            if (_movement != null) _movement.Used += OnUsed;
        }

        protected override void OnUnbind()
        {
            if (_movement != null) _movement.Used -= OnUsed;
        }

        private void OnUsed(Spell spell, Vector3 from)
        {
            float window = Window + WindowPerLevel * (Level - 1);
            Collider[] found = Physics.OverlapSphere(from, Range, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<EnemyController>();
            bool refunded = false;

            for (int i = 0; i < found.Length; i++)
            {
                EnemyController enemy = found[i].GetComponentInParent<EnemyController>();
                if (enemy == null || !seen.Add(enemy) || enemy.Health == null || !enemy.Health.IsAlive) continue;
                if (enemy.Target != Player.transform && enemy.Target != null) continue;
                if (!enemy.IsAttacking && enemy.NextAttackIn > window) continue;

                if (enemy.Status != null) enemy.Status.Apply(StatusLibrary.Blind(BlindSeconds), Player.gameObject, Team.Player);
                refunded = true;
            }

            if (!refunded) return;
            Refunds++;
            _movement.RefundUse();
        }
    }

    /// <summary>Each enemy's first attacks on you each floor miss. Prophecy.</summary>
    [System.Serializable]
    public class ProphecyBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public int MissesPerLevel = 1;

        [System.NonSerialized] private Dictionary<GameObject, int> _attacks;
        public int Misses { get; private set; }

        protected override void OnBind() => _attacks = new Dictionary<GameObject, int>();
        public override void OnFloorEntered(RoomRuntime room) => _attacks.Clear();

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            if (hit.Source == null || hit.SourceTeam != Team.Enemy) return amount;
            if (hit.Origin == DamageOrigin.StatusTick || hit.Origin == DamageOrigin.Environment) return amount;

            _attacks.TryGetValue(hit.Source, out int count);
            _attacks[hit.Source] = count + 1;
            if (count >= MissesPerLevel * Level) return amount;

            Misses++;
            return 0f;
        }
    }

    // ---------------------------------------------------------------- Death

    /// <summary>Kills sometimes leave an extra soul. Grave Hunger.</summary>
    [System.Serializable]
    public class GraveHungerBehaviour : BoonBehaviour
    {
        public float ChancePerLevel = 0.05f;

        [System.NonSerialized] public System.Func<bool> RollOverride;

        protected override void OnKill(Health victim, DamageInfo info)
        {
            SoulsMastery souls = GetMastery<SoulsMastery>();
            if (souls == null || souls.Rank <= 0) return;

            bool hit = RollOverride != null ? RollOverride() : Random.value < ChancePerLevel * Level;
            if (hit) souls.AddSouls(1);
        }
    }

    /// <summary>Spending a soul sends a ghost at an enemy that bursts for necrotic damage. Soul Slave.</summary>
    [System.Serializable]
    public class SoulSlaveBehaviour : BoonBehaviour
    {
        public float Damage = 15f;
        public float DamagePerLevel = 10f;
        public float Radius = 2.5f;
        public float Range = 30f;

        private static readonly Color Tint = new Color(0.55f, 0.9f, 0.7f);

        [System.NonSerialized] private SoulsMastery _souls;
        public int Ghosts { get; private set; }

        protected override void OnBind()
        {
            _souls = GetMastery<SoulsMastery>();
            if (_souls != null) _souls.Spent += OnSpent;
        }

        protected override void OnUnbind()
        {
            if (_souls != null) _souls.Spent -= OnSpent;
        }

        private void OnSpent(int count)
        {
            if (Player == null) return;
            Vector3 from = Player.transform.position + Vector3.up * 1.6f;

            float scale = 1f + (AllyBoosts.Active != null ? AllyBoosts.Active.Damage : 0f);
            for (int i = 0; i < count; i++)
            {
                Health target = NearestEnemy(from, Range);
                Vector3 direction = target != null ? target.transform.position + Vector3.up - from : Player.transform.forward;

                Projectile ghost = Projectile.Create(from, direction + Random.insideUnitSphere * 0.3f, Tint, 0.25f);
                ghost.OwnerTeam = Team.Player;
                ghost.Owner = Player.gameObject;
                ghost.IsSpell = true;
                ghost.CanCrit = false;
                ghost.Damage = 0f;
                ghost.DamageType = DamageType.Necrotic;
                ghost.Origin = DamageOrigin.Minion;
                ghost.SplashRadius = Radius;
                ghost.SplashDamage = (Damage + DamagePerLevel * (Level - 1)) * scale;
                ghost.Speed = 14f;
                ghost.Lifetime = 4f;
                ghost.HomingEnabled = true;
                ghost.HomingStrength = 6f;
                ghost.HomingTarget = target != null ? target.transform : null;
                ghost.Launch();
                Ghosts++;
            }
        }
    }

    /// <summary>A killing blow takes a soul instead, a set number of times per floor. Runs before Phylactery. Soul Shield.</summary>
    [System.Serializable]
    public class SoulShieldBehaviour : BoonBehaviour, ILethalHitRule
    {
        [System.NonSerialized] private int _used;

        public int LethalPriority => 0;

        public override void OnFloorEntered(RoomRuntime room) => _used = 0;

        public bool TrySurvive(in DamageInfo hit, Health player, float amount)
        {
            if (_used >= Level) return false;
            SoulsMastery souls = GetMastery<SoulsMastery>();
            if (souls == null || !souls.TrySpend(1)) return false;

            _used++;
            return true;
        }
    }

    /// <summary>A soul that would be wasted at the cap explodes where the enemy died. Overflowing Souls.</summary>
    [System.Serializable]
    public class OverflowingSoulsBehaviour : BoonBehaviour
    {
        public float Radius = 3.5f;
        public float Damage = 25f;
        public float DamagePerLevel = 15f;

        private static readonly Color Tint = new Color(0.45f, 0.95f, 0.75f);

        [System.NonSerialized] private SoulsMastery _souls;
        public int Blasts { get; private set; }

        protected override void OnBind()
        {
            _souls = GetMastery<SoulsMastery>();
            if (_souls != null) _souls.WastedAtCap += OnWasted;
        }

        protected override void OnUnbind()
        {
            if (_souls != null) _souls.WastedAtCap -= OnWasted;
        }

        private void OnWasted(Health victim)
        {
            if (victim == null) return;
            Blasts++;
            Blast(victim.transform.position + Vector3.up, Radius, Damage + DamagePerLevel * (Level - 1), DamageType.Necrotic, Tint);
        }
    }

    // ---------------------------------------------------------------- Psionic

    /// <summary>Spending psi makes your guns hit harder for a while; spending again refreshes it. Overflow.</summary>
    [System.Serializable]
    public class OverflowBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Seconds = 4f;
        public float PerLevel = 0.15f;

        [System.NonSerialized] private PsiBladesMastery _psi;
        [System.NonSerialized] private float _until = float.NegativeInfinity;

        public bool Active => Now < _until;

        protected override void OnBind()
        {
            _psi = GetMastery<PsiBladesMastery>();
            if (_psi != null) _psi.Spent += OnSpent;
        }

        protected override void OnUnbind()
        {
            if (_psi != null) _psi.Spent -= OnSpent;
        }

        private void OnSpent(float amount) => _until = Now + Seconds;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => Active && IsOwnHit(hit) && hit.Origin == DamageOrigin.Gun ? 1f + PerLevel * Level : 1f;
    }

    /// <summary>Confused enemies take more from you. Fractured Mind.</summary>
    [System.Serializable]
    public class FracturedMindBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerLevel = 0.15f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) && StatusOf(target) != null && StatusOf(target).IsConfused ? 1f + PerLevel * Level : 1f;
    }

    /// <summary>An empowered melee also throws a piercing blade wave forward. Psychic Wave.</summary>
    [System.Serializable]
    public class PsychicWaveBehaviour : BoonBehaviour
    {
        public float Damage = 20f;
        public float DamagePerLevel = 15f;

        private static readonly Color Tint = new Color(0.8f, 0.4f, 1f);

        [System.NonSerialized] private PsiBladesMastery _psi;
        public int Waves { get; private set; }

        protected override void OnBind()
        {
            _psi = GetMastery<PsiBladesMastery>();
            if (_psi != null) _psi.EmpoweredMeleeLanded += OnLanded;
        }

        protected override void OnUnbind()
        {
            if (_psi != null) _psi.EmpoweredMeleeLanded -= OnLanded;
        }

        private void OnLanded(IReadOnlyList<Health> struck)
        {
            Waves++;
            PsiProjectile.Throw(Player, Damage + DamagePerLevel * (Level - 1), Tint, pierce: 99);
        }
    }

    /// <summary>Melee with full charges throws one blade that spends them all. Thrown Blade.</summary>
    [System.Serializable]
    public class ThrownBladeBehaviour : BoonBehaviour
    {
        public float DamagePerCharge = 25f;
        public float ExtraPerLevel = 0.5f;

        private static readonly Color Tint = new Color(0.85f, 0.5f, 1f);

        [System.NonSerialized] private PlayerCombat _combat;
        public int Throws { get; private set; }

        protected override void OnBind()
        {
            _combat = Player.CombatInput;
            if (_combat != null) _combat.MeleeOverride = Throw;
        }

        protected override void OnUnbind()
        {
            if (_combat != null && _combat.MeleeOverride == (System.Func<Spell, Vector3, bool>)Throw) _combat.MeleeOverride = null;
        }

        private bool Throw(Spell melee, Vector3 forward)
        {
            PsiBladesMastery psi = GetMastery<PsiBladesMastery>();
            if (psi == null || psi.Max <= 0 || psi.Charge + 0.0001f < psi.Max) return false;

            float charges = psi.Charge;
            if (!psi.TrySpend(charges)) return false;

            Throws++;
            float damage = DamagePerCharge * charges * (1f + ExtraPerLevel * (Level - 1));
            PsiProjectile.Throw(Player, damage, Tint, pierce: 0, forward: forward);
            return true;
        }
    }

    /// <summary>The psi blades Psychic Wave and Thrown Blade send out.</summary>
    public static class PsiProjectile
    {
        public static Projectile Throw(PlayerRig player, float damage, Color tint, int pierce, Vector3? forward = null)
        {
            if (player == null) return null;

            Transform aim = player.Weapon != null && player.Weapon.AimOrigin != null ? player.Weapon.AimOrigin : player.transform;
            Vector3 direction = forward ?? aim.forward;

            Projectile blade = Projectile.Create(aim.position + direction * 0.8f, direction, tint, 0.3f);
            blade.OwnerTeam = Team.Player;
            blade.Owner = player.gameObject;
            blade.OwnerSheet = player.Sheet;
            blade.IsSpell = true;
            blade.CanCrit = false;
            blade.Damage = damage;
            blade.DamageType = DamageType.Psychic;
            blade.Origin = DamageOrigin.Melee;
            blade.Pierce = pierce;
            blade.Speed = 35f;
            blade.Lifetime = 1.2f;
            blade.Launch();
            return blade;
        }
    }

    // ---------------------------------------------------------------- Aetherics

    /// <summary>Faster the emptier your mana. Empty Vessel.</summary>
    [System.Serializable]
    public class EmptyVesselBehaviour : BoonBehaviour
    {
        public float PerLevel = 0.1f;

        [System.NonSerialized] private StatModifier _speed;

        public override void Tick(float dt)
        {
            Mana mana = Player != null ? Player.Mana : null;
            float empty = mana != null && mana.Max > 0f ? 1f - mana.Fraction : 0f;
            SetPercent(ref _speed, Attr.MoveSpeed, empty * PerLevel * Level);
        }
    }

    /// <summary>Below half mana, your gun rounds pierce. Void Rounds.</summary>
    [System.Serializable]
    public class VoidRoundsBehaviour : BoonBehaviour
    {
        public float Threshold = 0.5f;

        [System.NonSerialized] private StatModifier _pierce;

        public bool Active => _pierce != null;

        public override void Tick(float dt)
        {
            CharacterSheet sheet = Sheet;
            Mana mana = Player != null ? Player.Mana : null;
            if (sheet == null || mana == null) return;

            bool low = mana.Fraction < Threshold;
            if (low == (_pierce != null)) return;

            if (low) _pierce = sheet.AddFlat(Attr.Pierce, Level, this, "Void Rounds");
            else
            {
                sheet.RemoveModifier(_pierce);
                _pierce = null;
            }
        }

        protected override void OnLevelChanged(int previous)
        {
            if (_pierce == null || Sheet == null) return;
            Sheet.RemoveModifier(_pierce);
            _pierce = Sheet.AddFlat(Attr.Pierce, Level, this, "Void Rounds");
        }
    }
}
