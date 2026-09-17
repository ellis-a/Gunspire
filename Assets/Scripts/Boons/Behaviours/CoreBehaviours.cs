using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Behaviours for the Core boons: damage, recovery, the shop, minions, combat and the world. Every number is
    // a placeholder that play will settle.

    // ---------------------------------------------------------------- damage

    /// <summary>Your hits on an enemy below a share of its health deal more. Executioner.</summary>
    [System.Serializable]
    public class ExecutionerBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Threshold = 0.3f;
        public float PerLevel = 0.1f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) && target.Fraction < Threshold ? 1f + PerLevel * Level : 1f;
    }

    /// <summary>Your hits on elites and the boss deal more. Big Game.</summary>
    [System.Serializable]
    public class BigGameBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerLevel = 0.08f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            if (!IsOwnHit(hit)) return 1f;
            EnemyController enemy = target.GetComponent<EnemyController>();
            bool boss = enemy != null && enemy.Definition != null && enemy.Definition.Id == EnemyLibrary.BossId;
            return target.IsElite || boss ? 1f + PerLevel * Level : 1f;
        }
    }

    /// <summary>Your hits deal more within a band of distance from where they came from. Brawler and Longshot.</summary>
    [System.Serializable]
    public class RangeDamageBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float MinDistance;
        public float MaxDistance = 6f;
        public float PerLevel = 0.08f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            if (!IsDirectOwnHit(hit)) return 1f;

            Vector3 from = hit.HasSourcePosition ? hit.SourcePosition : Player.transform.position;
            float distance = Vector3.Distance(from, target.transform.position);
            return distance >= MinDistance && distance <= MaxDistance ? 1f + PerLevel * Level : 1f;
        }

        public override string Describe() => "more damage between " + MinDistance + " and " + MaxDistance + " m";
    }

    /// <summary>Your hits deal more while you move. Momentum.</summary>
    [System.Serializable]
    public class MomentumBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float MinSpeed = 1.5f;
        public float PerLevel = 0.06f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) && Player.Motor != null && Player.Motor.HorizontalSpeed > MinSpeed ? 1f + PerLevel * Level : 1f;
    }

    /// <summary>Your hits deal more while you are off the ground. High Ground.</summary>
    [System.Serializable]
    public class HighGroundBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerLevel = 0.08f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) && Player.Motor != null && !Player.Motor.IsGrounded ? 1f + PerLevel * Level : 1f;
    }

    /// <summary>Kills in quick succession stack a damage bonus that lapses when the kills stop. Kill Streak.</summary>
    [System.Serializable]
    public class KillStreakBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Window = 3f;
        public int MaxStacks = 10;
        public float PerStackPerLevel = 0.02f;

        [System.NonSerialized] private int _stacks;
        [System.NonSerialized] private float _lastKill = float.NegativeInfinity;

        public int Stacks => Now - _lastKill <= Window ? _stacks : 0;

        protected override void OnKill(Health victim, DamageInfo info)
        {
            _stacks = Now - _lastKill <= Window ? Mathf.Min(MaxStacks, _stacks + 1) : 1;
            _lastKill = Now;
        }

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) ? 1f + Stacks * PerStackPerLevel * Level : 1f;
    }

    /// <summary>Every enemy that has died on this floor adds to your damage. Blasphemous Act.</summary>
    [System.Serializable]
    public class BlasphemousActBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerDeathPerLevel = 0.02f;

        [System.NonSerialized] private int _deaths;
        public int Deaths => _deaths;

        protected override void OnKill(Health victim, DamageInfo info) => _deaths++;
        public override void OnFloorEntered(RoomRuntime room) => _deaths = 0;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) ? 1f + _deaths * PerDeathPerLevel * Level : 1f;
    }

    /// <summary>Your direct hits on an enemy at full health are doubled. Only the first pellet of a volley finds it full. First Blood.</summary>
    [System.Serializable]
    public class FirstBloodBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Multiplier = 2f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsDirectOwnHit(hit) && target.Current >= target.Max - 0.001f ? Multiplier : 1f;
    }

    /// <summary>Double damage on the floor after it is taken, then gone for good. Charge Up.</summary>
    [System.Serializable]
    public class ChargeUpBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Multiplier = 2f;

        [System.NonSerialized] private bool _armed;
        [System.NonSerialized] private bool _active;

        public bool Active => _active;

        protected override void OnBind() => _armed = true;

        public override void OnFloorEntered(RoomRuntime room)
        {
            if (!_armed) return;
            _armed = false;
            _active = true;
        }

        public override void OnFloorCompleted(RoomRuntime room) => Spend();
        public override void OnFloorLeaving(int floor) => Spend();

        private void Spend()
        {
            if (!_active) return;
            _active = false;
            Run.ConsumeBoon(BoonIds.ChargeUp);
        }

        public float OutgoingMultiplier(in DamageInfo hit, Health target) => _active && IsOwnHit(hit) ? Multiplier : 1f;
    }

    /// <summary>At full health, your crit chance is doubled. Cold Blood.</summary>
    [System.Serializable]
    public class ColdBloodBehaviour : BoonBehaviour, ICritRule
    {
        public float Multiplier = 2f;

        public void AdjustCrit(CharacterSheet attacker, IDamageable target, Weapon weapon, ref float chance, ref bool forced)
        {
            if (attacker == Sheet && PlayerHealth != null && PlayerHealth.Fraction >= 0.999f) chance *= Multiplier;
        }
    }

    // ---------------------------------------------------------------- recovery

    /// <summary>Taking the exit heals a share of your health. Rest a Moment.</summary>
    [System.Serializable]
    public class RestAMomentBehaviour : BoonBehaviour
    {
        public float PerLevel = 0.08f;

        public override void OnFloorCompleted(RoomRuntime room)
        {
            if (PlayerHealth != null) PlayerHealth.Heal(PlayerHealth.Max * PerLevel * Level);
        }
    }

    /// <summary>Every kill drops a minor orb. Vampire Sight and Demon Sight.</summary>
    [System.Serializable]
    public class MinorOrbBehaviour : BoonBehaviour
    {
        public bool Mana;

        protected override void OnKill(Health victim, DamageInfo info)
        {
            Vector3 at = victim.transform.position + Vector3.up * 0.8f;
            OrbPickup.SpawnMinor(at, Mana, Mana ? KillRewards.ManaOrb : KillRewards.HealthOrb);
        }

        public override string Describe() => "kills drop a minor " + (Mana ? "mana" : "health") + " orb";
    }

    /// <summary>Health orbs you cannot use are stored, one per level, and drunk when you drop low. Bottled Orb.</summary>
    [System.Serializable]
    public class BottledOrbBehaviour : BoonBehaviour
    {
        public float LowHealth = 0.35f;

        [System.NonSerialized] private List<float> _bottles;
        public int Stored => _bottles != null ? _bottles.Count : 0;

        protected override void OnBind()
        {
            _bottles = new List<float>();
            OrbPickup.FullHealthCollector = Bottle;
        }

        protected override void OnUnbind()
        {
            if (OrbPickup.FullHealthCollector == (System.Func<OrbPickup, PlayerRig, bool>)Bottle) OrbPickup.FullHealthCollector = null;
        }

        private bool Bottle(OrbPickup orb, PlayerRig rig)
        {
            if (rig != Player || _bottles.Count >= Level) return false;
            _bottles.Add(orb.Amount);
            return true;
        }

        public override void Tick(float dt)
        {
            if (_bottles.Count == 0 || PlayerHealth == null || !PlayerHealth.IsAlive || PlayerHealth.Fraction >= LowHealth) return;

            float amount = _bottles[_bottles.Count - 1];
            _bottles.RemoveAt(_bottles.Count - 1);
            PlayerHealth.Heal(amount * (Sheet != null ? Sheet.Get(Attr.OrbPotency) : 1f));
        }
    }

    /// <summary>Every orb on the floor flies to you, and each also restores a little of the other resource. Magnetism.</summary>
    [System.Serializable]
    public class MagnetismBehaviour : BoonBehaviour
    {
        public float CrossShare = 0.25f;

        protected override void OnBind() => OrbPickup.Collected += OnCollected;
        protected override void OnUnbind() => OrbPickup.Collected -= OnCollected;

        public override void Tick(float dt)
        {
            foreach (OrbPickup orb in OrbPickup.Live) orb.Attracted = true;
        }

        private void OnCollected(OrbPickup orb, PlayerRig rig)
        {
            if (rig != Player) return;
            float amount = orb.Amount * CrossShare;
            if (orb.IsMana) rig.Health.Heal(amount);
            else rig.Mana.Add(amount);
        }
    }

    // ---------------------------------------------------------------- the shop

    /// <summary>Your melee hits knock shillings loose, a few times per enemy each floor. Pickpocket.</summary>
    [System.Serializable]
    public class PickpocketBehaviour : BoonBehaviour
    {
        public float PerHitPerLevel = 1f;
        public int TimesPerEnemy = 3;

        [System.NonSerialized] private Dictionary<Health, int> _picked;

        protected override void OnBind() => _picked = new Dictionary<Health, int>();
        public override void OnFloorEntered(RoomRuntime room) => _picked.Clear();

        protected override void OnAnyDamaged(Health victim, DamageInfo hit, float amount)
        {
            if (victim == null || victim.Team != Team.Enemy || !IsOwnHit(hit) || hit.Origin != DamageOrigin.Melee) return;

            EnemyController enemy = victim.GetComponent<EnemyController>();
            if (enemy == null || enemy.PaysNoReward) return;

            _picked.TryGetValue(victim, out int times);
            if (times >= TimesPerEnemy) return;
            _picked[victim] = times + 1;

            ShillingPickup.Scatter(victim.transform.position + Vector3.up, PerHitPerLevel * Level, ShillingSource.Pickpocket);
        }
    }

    /// <summary>Every hundred shillings held gives a point of Luck. Hoarder.</summary>
    [System.Serializable]
    public class HoarderBehaviour : BoonBehaviour
    {
        public int ShillingsPerLuck = 100;

        protected override void OnBind()
        {
            Run.Wallet.Changed += Refresh;
            Refresh();
        }

        protected override void OnUnbind() => Run.Wallet.Changed -= Refresh;

        private void Refresh()
        {
            if (Sheet != null) Sheet.SetStatBonus(this, StatType.Luck, Run.Wallet.Balance / Mathf.Max(1, ShillingsPerLuck));
        }
    }

    // ---------------------------------------------------------------- minions

    /// <summary>A beetle every few seconds, each living a short while. Beetle Swarm.</summary>
    [System.Serializable]
    public class BeetleSwarmBehaviour : BoonBehaviour
    {
        public float Interval = 5f;

        [System.NonSerialized] private float _timer;
        public int Spawned { get; private set; }

        public override void Tick(float dt)
        {
            if (Player == null || (_timer -= dt) > 0f) return;
            _timer = Interval;

            Vector2 offset = Random.insideUnitCircle.normalized * 1.5f;
            MinionController beetle = MinionSummoner.Spawn(MinionLibrary.BeetleId,
                Player.transform.position + new Vector3(offset.x, 0f, offset.y));
            if (beetle != null) Spawned++;
        }
    }

    /// <summary>Damage your minions deal heals you for a share of it. Blood Pact.</summary>
    [System.Serializable]
    public class BloodPactBehaviour : BoonBehaviour
    {
        public float SharePerLevel = 0.04f;

        protected override void OnAnyDamaged(Health victim, DamageInfo hit, float amount)
        {
            if (victim == null || victim.Team == Team.Player || hit.Origin != DamageOrigin.Minion || hit.SourceTeam != Team.Player) return;
            if (PlayerHealth != null) PlayerHealth.Heal(amount * SharePerLevel * Level, silent: true);
        }
    }

    // ---------------------------------------------------------------- combat

    /// <summary>Each floor starts with a shield that lasts until it breaks. Padding.</summary>
    [System.Serializable]
    public class PaddingBehaviour : BoonBehaviour
    {
        public float PerLevel = 12f;

        public override void OnFloorEntered(RoomRuntime room)
        {
            if (PlayerHealth != null) PlayerHealth.AddShield(PerLevel * Level, 99999f);
        }
    }

    /// <summary>Kills shorten your spell cooldowns. Rhythm.</summary>
    [System.Serializable]
    public class RhythmBehaviour : BoonBehaviour
    {
        public float SecondsPerLevel = 0.25f;

        protected override void OnKill(Health victim, DamageInfo info)
        {
            if (Player.Book != null) Player.Book.ReduceCooldowns(SecondsPerLevel * Level);
        }
    }

    /// <summary>At full health, cooldowns run faster. Cool Head.</summary>
    [System.Serializable]
    public class CoolHeadBehaviour : BoonBehaviour
    {
        public float PerLevel = 0.1f;

        [System.NonSerialized] private StatModifier _rate;

        public override void Tick(float dt)
        {
            bool full = PlayerHealth != null && PlayerHealth.Fraction >= 0.999f;
            SetPercent(ref _rate, Attr.CooldownRate, full ? PerLevel * Level : 0f);
        }
    }

    /// <summary>With enough enemies near, you take less damage. Cornered.</summary>
    [System.Serializable]
    public class CorneredBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float Range = 6f;
        public int Count = 3;
        public float PerLevel = 0.06f;

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
            => EnemiesWithin(player.transform.position, Range) >= Count ? amount * Mathf.Max(0f, 1f - PerLevel * Level) : amount;
    }

    /// <summary>Direct hits you take are dealt back to the attacker as psychic damage. Every Reaction.</summary>
    [System.Serializable]
    public class EveryReactionBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float Share = 1f;

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            if (hit.Source == null) return amount;
            if (hit.Origin == DamageOrigin.StatusTick || hit.Origin == DamageOrigin.Environment || hit.Origin == DamageOrigin.Collision)
                return amount;

            Health attacker = hit.Source.GetComponent<Health>();
            if (attacker == null || !attacker.IsAlive || attacker.Team == Team.Player) return amount;

            attacker.TakeDamage(PlayerDamage(amount * Share, DamageType.Psychic, DamageOrigin.Mastery,
                attacker.transform.position + Vector3.up));
            return amount;
        }
    }

    /// <summary>
    /// Faster cooldowns and mana regeneration, lost when hurt until every enemy that hurt you is dead. Damage with no
    /// attacker does not break it. Monarch.
    /// </summary>
    [System.Serializable]
    public class MonarchBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float CooldownBonus = 0.3f;
        public float ManaRegenBonus = 0.5f;

        [System.NonSerialized] private HashSet<Health> _grudges;
        [System.NonSerialized] private StatModifier _cooldown;
        [System.NonSerialized] private StatModifier _regen;

        public bool IsActive => _grudges != null && _grudges.Count == 0;

        protected override void OnBind()
        {
            _grudges = new HashSet<Health>();
            Refresh();
        }

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            Health attacker = hit.Source != null ? hit.Source.GetComponent<Health>() : null;
            if (attacker != null && attacker.IsAlive && attacker.Team != Team.Player && _grudges.Add(attacker)) Refresh();
            return amount;
        }

        protected override void OnKill(Health victim, DamageInfo info)
        {
            if (_grudges.Remove(victim)) Refresh();
        }

        public override void OnFloorEntered(RoomRuntime room)
        {
            _grudges.Clear();
            Refresh();
        }

        public override void Tick(float dt)
        {
            if (_grudges.RemoveWhere(h => h == null || !h.IsAlive) > 0) Refresh();
        }

        private void Refresh()
        {
            SetPercent(ref _cooldown, Attr.CooldownRate, IsActive ? CooldownBonus : 0f);
            SetPercent(ref _regen, Attr.ManaRegen, IsActive ? ManaRegenBonus : 0f);
        }
    }

    /// <summary>Half of every hit drains mana instead, while there is mana. Maximum health is halved. Mana Shield.</summary>
    [System.Serializable]
    public class ManaShieldBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float Share = 0.5f;
        public float HealthPenalty = -0.5f;

        protected override void OnBind()
        {
            if (Sheet != null) Sheet.AddPercent(Attr.MaxHealth, HealthPenalty, this, "Mana Shield");
        }

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            Mana mana = Player.Mana;
            if (mana == null || mana.Current <= 0f) return amount;

            float drained = Mathf.Min(amount * Share, mana.Current);
            mana.TrySpend(drained);
            return amount - drained;
        }
    }

    /// <summary>Health fixed at one, and triple damage. Cocky.</summary>
    [System.Serializable]
    public class CockyBehaviour : BoonBehaviour
    {
        public float DamageBonus = 2f;

        protected override void OnBind()
        {
            if (Sheet == null) return;
            Sheet.AddPercent(Attr.MaxHealth, -1000f, this, "Cocky");
            Sheet.AddPercent(Attr.DamageDealt, DamageBonus, this, "Cocky");
        }
    }

    /// <summary>Once per run, a killing blow leaves you at half health instead. Phylactery.</summary>
    [System.Serializable]
    public class PhylacteryBehaviour : BoonBehaviour, ILethalHitRule
    {
        public float HealthShare = 0.5f;

        [System.NonSerialized] private bool _spent;

        public int LethalPriority => 100;

        public bool TrySurvive(in DamageInfo hit, Health player, float amount)
        {
            if (_spent) return false;
            _spent = true;
            player.SetCurrent(player.Max * HealthShare);
            Run.ConsumeBoon(BoonIds.Phylactery);
            return true;
        }
    }

    /// <summary>Taking damage makes you immune for a moment. Panic Barrier.</summary>
    [System.Serializable]
    public class PanicBarrierBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float SecondsPerLevel = 0.5f;

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            player.InvulnerabilityTimer = Mathf.Max(player.InvulnerabilityTimer, SecondsPerLevel * Level);
            return amount;
        }
    }

    /// <summary>Health fixed at one, with a shield that refills after a few seconds unhurt. Glass Soul.</summary>
    [System.Serializable]
    public class GlassSoulBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float Capacity = 100f;
        public float RefillDelay = 3f;

        [System.NonSerialized] private float _lastHit = float.NegativeInfinity;

        protected override void OnBind()
        {
            if (Sheet != null) Sheet.AddPercent(Attr.MaxHealth, -1000f, this, "Glass Soul");
            if (PlayerHealth != null) PlayerHealth.AddShield(Capacity, 99999f);
        }

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            _lastHit = Now;
            return amount;
        }

        public override void Tick(float dt)
        {
            Health health = PlayerHealth;
            if (health == null || !health.IsAlive || Now - _lastHit < RefillDelay) return;
            if (health.Shield < Capacity) health.AddShield(Capacity - health.Shield, 99999f);
        }
    }

    /// <summary>Below a quarter health, cooldowns run far faster and you deal more. Last Stand.</summary>
    [System.Serializable]
    public class LastStandBehaviour : BoonBehaviour
    {
        public float Threshold = 0.25f;
        public float CooldownBonus = 2f;
        public float DamageBonus = 0.5f;

        [System.NonSerialized] private StatModifier _cooldown;
        [System.NonSerialized] private StatModifier _damage;

        public override void Tick(float dt)
        {
            bool low = PlayerHealth != null && PlayerHealth.IsAlive && PlayerHealth.Fraction < Threshold;
            SetPercent(ref _cooldown, Attr.CooldownRate, low ? CooldownBonus : 0f);
            SetPercent(ref _damage, Attr.DamageDealt, low ? DamageBonus : 0f);
        }
    }

    /// <summary>No knockback, slows or stuns, at the cost of speed. Juggernaut.</summary>
    [System.Serializable]
    public class JuggernautBehaviour : BoonBehaviour
    {
        public float SpeedPenalty = -0.15f;

        private static readonly StatusId[] Immune = { StatusId.Snare, StatusId.Frost };

        protected override void OnBind()
        {
            if (Sheet != null) Sheet.AddPercent(Attr.MoveSpeed, SpeedPenalty, this, "Juggernaut");
            if (Player.Motor != null) Player.Motor.KnockbackImmunity++;
            if (Player.Status != null)
                foreach (StatusId id in Immune) Player.Status.AddImmunity(id);
        }

        protected override void OnUnbind()
        {
            if (Player == null) return;
            if (Player.Motor != null) Player.Motor.KnockbackImmunity = Mathf.Max(0, Player.Motor.KnockbackImmunity - 1);
            if (Player.Status != null)
                foreach (StatusId id in Immune) Player.Status.RemoveImmunity(id);
        }
    }

    /// <summary>The enemy that last hurt you is marked; killing it heals you. Vendetta.</summary>
    [System.Serializable]
    public class VendettaBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float HealShare = 0.25f;

        [System.NonSerialized] private Health _marked;
        public Health Marked => _marked;

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            Health attacker = hit.Source != null ? hit.Source.GetComponent<Health>() : null;
            if (attacker != null && attacker.IsAlive && attacker.Team != Team.Player) _marked = attacker;
            return amount;
        }

        protected override void OnKill(Health victim, DamageInfo info)
        {
            if (victim != _marked || PlayerHealth == null) return;
            _marked = null;
            PlayerHealth.Heal(PlayerHealth.Max * HealShare);
        }
    }

    /// <summary>Your knockback impacts deal more. The knockback itself is a sheet modifier on the boon. Pinball Wizard.</summary>
    [System.Serializable]
    public class PinballWizardBehaviour : BoonBehaviour
    {
        public float ImpactPerLevel = 0.25f;

        protected override void OnLevelChanged(int previous) => KnockbackImpacts.PlayerImpactScale = 1f + ImpactPerLevel * Level;
        protected override void OnUnbind() => KnockbackImpacts.PlayerImpactScale = 1f;
    }

    // ---------------------------------------------------------------- the world

    /// <summary>Every enemy is ethereal. Ghostrealm.</summary>
    [System.Serializable]
    public class GhostrealmBehaviour : BoonBehaviour
    {
        protected override void OnBind()
        {
            EnemyFactory.Spawned += Haunt;
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>()) Haunt(enemy);
        }

        protected override void OnUnbind() => EnemyFactory.Spawned -= Haunt;

        private static void Haunt(EnemyController enemy)
        {
            if (enemy != null && enemy.Health != null) enemy.Health.EtherealByNature = true;
        }
    }

    /// <summary>Each floor, several ordinary enemies are death-marked for good. Doomed.</summary>
    [System.Serializable]
    public class DoomedBehaviour : BoonBehaviour
    {
        public int Count = 5;

        public override void OnFloorEntered(RoomRuntime room)
        {
            if (room == null) return;

            var eligible = new List<EnemyController>();
            foreach (EnemyController enemy in room.Enemies)
                if (enemy != null && !enemy.PaysNoReward && enemy.Health != null && !enemy.Health.IsElite) eligible.Add(enemy);

            Run.Rng.Shuffle(eligible);
            for (int i = 0; i < eligible.Count && i < Count; i++)
                eligible[i].Status.Apply(StatusLibrary.Deathmark(99999f), Player.gameObject, Team.Player);
        }
    }

    /// <summary>The first enemy each floor to see you, that can be charmed, is. Otherworldly Beauty.</summary>
    [System.Serializable]
    public class OtherworldlyBeautyBehaviour : BoonBehaviour
    {
        [System.NonSerialized] private bool _charmedThisFloor;

        protected override void OnBind() => EnemyController.SawPlayer += OnSeen;
        protected override void OnUnbind() => EnemyController.SawPlayer -= OnSeen;
        public override void OnFloorEntered(RoomRuntime room) => _charmedThisFloor = false;

        private void OnSeen(EnemyController enemy)
        {
            if (_charmedThisFloor || enemy == null || enemy.Status == null) return;

            enemy.Status.Apply(StatusLibrary.Charmed(), Player != null ? Player.gameObject : null, Team.Player);
            if (enemy.Status.Has(StatusId.Charmed)) _charmedThisFloor = true;
        }
    }

    /// <summary>Enemies are stronger, and your Luck is much higher. Courageous.</summary>
    [System.Serializable]
    public class CourageousBehaviour : BoonBehaviour
    {
        public int Luck = 5;
        public float EnemyHealth = 0.3f;
        public float EnemyDamage = 0.2f;

        protected override void OnBind()
        {
            if (Sheet != null) Sheet.SetStatBonus(this, StatType.Luck, Luck);
            EnemyFactory.Spawned += Embolden;
        }

        protected override void OnUnbind() => EnemyFactory.Spawned -= Embolden;

        private void Embolden(EnemyController enemy)
        {
            CharacterSheet sheet = enemy != null ? enemy.Sheet : null;
            if (sheet == null) return;

            sheet.AddPercent(Attr.MaxHealth, EnemyHealth, this, "Courageous");
            sheet.AddPercent(Attr.DamageDealt, EnemyDamage, this, "Courageous");
            if (enemy.Health != null) enemy.Health.ConfigureMaxHealth(sheet.Get(Attr.MaxHealth));
        }
    }

    /// <summary>Ids the behaviours refer to, kept beside the roster that defines them.</summary>
    public static class BoonIds
    {
        public const string ChargeUp = "charge_up";
        public const string Phylactery = "phylactery";
        public const string Familiarity = "familiarity";
        public const string GlassSoul = "glass_soul";
        public const string Cocky = "cocky";
    }
}
