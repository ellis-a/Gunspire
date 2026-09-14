using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Elemental's mastery: burn, frost and shock meeting on one enemy react.
    ///
    /// - 1 spell: applying an element to an enemy already carrying a different one sets off a modest
    ///   burst of the incoming element's damage type.
    /// - 2: the burst is worth real damage.
    /// - 3: a third element on one target detonates across an area instead, consuming all three.
    /// - 4: the detonation puts the incoming element back on everything it catches, so a crowd chains.
    ///
    /// Only elements the player applied react, and each enemy reacts at most once a second of world
    /// time, which is what keeps a ticking Hailstorm from becoming a wall of explosions. Fire and storm
    /// burst as energy; ice as kinetic, the type thrown ice already is, so a reaction can shatter a
    /// frozen neighbour. Every number is a placeholder.
    /// </summary>
    public class ConfluxMastery : Mastery
    {
        public const float ReactionCooldown = 1f;
        public const float BurstDamage = 12f;
        public const float StrongBurstDamage = 30f;
        public const float DetonationDamage = 40f;
        public const float DetonationRadius = 4f;

        public override SpellSchool School => SpellSchool.Elemental;

        /// <summary>Reactions and detonations set off, for tooling.</summary>
        public int Reactions { get; private set; }
        public int Detonations { get; private set; }

        private readonly Dictionary<StatusController, float> _nextReaction = new Dictionary<StatusController, float>();
        private readonly List<IDamageable> _caught = new List<IDamageable>();
        private bool _bound;

        public static bool IsElement(StatusId id) => id == StatusId.Burn || id == StatusId.Frost || id == StatusId.Shock;

        public static DamageType DamageTypeOf(StatusId element)
            => element == StatusId.Frost ? DamageType.Kinetic : DamageType.Energy;

        /// <summary>What a rank-four detonation puts back on everything it catches.</summary>
        public static StatusApplication Reapplied(StatusId element)
        {
            switch (element)
            {
                case StatusId.Frost: return StatusLibrary.Frost(stacks: 10);
                case StatusId.Shock: return StatusLibrary.Shock();
                default: return StatusLibrary.Burn(amount: 10f);
            }
        }

        /// <summary>How many elements other than the incoming one the target is carrying.</summary>
        public static int OtherElements(StatusController target, StatusId incoming)
        {
            if (target == null) return 0;

            int count = 0;
            if (incoming != StatusId.Burn && target.Has(StatusId.Burn)) count++;
            if (incoming != StatusId.Frost && target.Has(StatusId.Frost)) count++;
            if (incoming != StatusId.Shock && target.Has(StatusId.Shock)) count++;
            return count;
        }

        public override void Bind(PlayerRig rig)
        {
            base.Bind(rig);
            if (_bound) return;

            StatusController.AnyApplied += OnApplied;
            _bound = true;
        }

        public override void Unbind()
        {
            if (!_bound) return;
            StatusController.AnyApplied -= OnApplied;
            _bound = false;
        }

        public override void OnFloorEntered(RoomRuntime room) => _nextReaction.Clear();

        public override void ResetForRun()
        {
            _nextReaction.Clear();
            Reactions = 0;
            Detonations = 0;
        }

        private void OnApplied(StatusController target, StatusId id, GameObject source, Team team)
        {
            if (this == null || Rank <= 0 || team != Team.Player || !IsElement(id) || target == null) return;

            Health health = target.Health;
            if (health == null || !health.IsAlive || health.Team == Team.Player) return;

            int others = OtherElements(target, id);
            if (others == 0) return;

            float now = WorldClock.Now;
            if (_nextReaction.TryGetValue(target, out float next) && now < next) return;

            // Set before anything is dealt, so a chain that comes back round to this enemy finds it spent.
            _nextReaction[target] = now + ReactionCooldown;

            if (Rank >= 3 && others >= 2) Detonate(target, id);
            else Burst(target, id);
        }

        private void Burst(StatusController target, StatusId element)
        {
            Reactions++;

            Vector3 at = target.transform.position + Vector3.up * 0.9f;
            target.Health.TakeDamage(Hit(Rank >= 2 ? StrongBurstDamage : BurstDamage, element, at));
        }

        private void Detonate(StatusController target, StatusId element)
        {
            Reactions++;
            Detonations++;

            Vector3 centre = target.transform.position + Vector3.up * 0.9f;

            target.Remove(StatusId.Burn);
            target.Remove(StatusId.Frost);
            target.Remove(StatusId.Shock);

            _caught.Clear();
            Combat.Explode(centre, DetonationRadius, Hit(DetonationDamage, element, centre), Layers.PlayerHitMask,
                0.5f, 0f, (victim, info) => _caught.Add(victim));

            if (Rank < 4) return;

            // Applied once the blast has finished, since a reaction it sets off may explode too, and the
            // blast is still walking a shared overlap buffer while its callback runs.
            var caught = new List<IDamageable>(_caught);
            StatusApplication again = Reapplied(element);
            GameObject source = Rig != null ? Rig.gameObject : null;

            for (int i = 0; i < caught.Count; i++)
            {
                IDamageable victim = caught[i];
                if (victim == null || !victim.IsAlive || victim.Transform == null) continue;

                StatusController status = victim.Transform.GetComponent<StatusController>();
                if (status != null) status.Apply(again, source, Team.Player);
            }
        }

        private DamageInfo Hit(float amount, StatusId element, Vector3 at)
        {
            DamageInfo info = DamageInfo.Create(amount, DamageTypeOf(element), Team.Player, Rig != null ? Rig.gameObject : null);
            info.CanCrit = false;
            info.Origin = DamageOrigin.Mastery;
            return info.At(at, Vector3.up);
        }
    }
}
