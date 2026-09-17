using UnityEngine;

namespace Gunspire
{
    // The statuses the boon designs added. Every number is a first guess.

    /// <summary>
    /// Fights for the player until the floor ends: its side changes, it hunts the other enemies, the
    /// other enemies hunt it, and it stops holding the room shut. Elites, bosses and anything already on
    /// the player's side refuse it. The work is in <see cref="EnemyController"/>, which watches for it.
    /// </summary>
    public class CharmedStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Charmed;
        public override string DisplayName => "Charmed";
        public override string Description => "Fights for you until the floor ends.";
        public override Color Tint => new Color(1f, 0.55f, 0.8f);
        public override int MaxStacks => 1;

        /// <summary>Lasts the floor: the room's teardown is what ends it.</summary>
        public override float DecayScale(StatusController c, ActiveStatus s) => 0f;

        public override DebuffResistance Resisted => DebuffResistance.None;

        public override bool CanApplyTo(StatusController c)
        {
            Health health = c.Health;
            return health != null && health.Team == Team.Enemy && !health.IsElite && c.GetComponent<EnemyController>() != null;
        }
    }

    /// <summary>
    /// Stores a share of every hit the carrier takes, and when the hex ends, deals the total again as
    /// psychic damage. Each application adds its amount to the share, up to a cap, and refreshes the
    /// timer. If the carrier dies first, the stored damage is lost.
    /// </summary>
    public class HexStatus : StatusDefinition
    {
        /// <summary>The largest share of damage a hex can store.</summary>
        public const float MaxShare = 1f;

        public override StatusId Id => StatusId.Hex;
        public override string DisplayName => "Hexed";
        public override string Description => "Stores a share of the damage it takes, and takes it again when the hex ends.";
        public override Color Tint => new Color(0.55f, 0.25f, 0.75f);
        public override int MaxStacks => 1;
        public override DebuffResistance Resisted => DebuffResistance.Magnitude;

        public override void OnApplied(StatusController c, ActiveStatus s) => s.Magnitude = Mathf.Clamp(s.Magnitude, 0f, MaxShare);

        public override void OnTopUp(StatusController c, ActiveStatus s, in StatusApplication app, float previousMagnitude)
            => s.Magnitude = Mathf.Clamp(previousMagnitude + app.Magnitude, 0f, MaxShare);

        public override void OnOwnerDamaged(StatusController c, ActiveStatus s, in DamageInfo hit, float amount)
        {
            if (amount > 0f) s.Stored += amount * s.Magnitude;
        }

        public override void OnRemoved(StatusController c, ActiveStatus s)
        {
            // Every status is cleared on death, so a hex removed from something dead is simply lost.
            Health health = c.Health;
            if (health == null || !health.IsAlive || s.Stored <= 0f) return;

            float stored = s.Stored;
            s.Stored = 0f;
            c.DealTickDamage(s, stored, DamageType.Psychic, scaleWithSource: false);
        }
    }

    /// <summary>
    /// Builds up with every application. At <see cref="Threshold"/>, the carrier explodes, dealing energy
    /// damage to the enemies around it, and the buildup resets. The blast carries no statuses, so it
    /// cannot set off another volatile enemy, and it is on the applier's side, so it never hurts them.
    /// </summary>
    public class VolatileStatus : StatusDefinition
    {
        public const float Threshold = 100f;
        public const float BlastRadius = 4f;
        public const float BlastDamage = 40f;

        public override StatusId Id => StatusId.Volatile;
        public override string DisplayName => "Volatile";
        public override string Description => "Explodes once enough builds up, hurting the enemies around it.";
        public override Color Tint => new Color(1f, 0.7f, 0.2f);
        public override int MaxStacks => 1;
        public override DebuffResistance Resisted => DebuffResistance.Magnitude;

        /// <summary>Explosions set off, for tooling.</summary>
        public static int Explosions { get; private set; }

        public override void OnApplied(StatusController c, ActiveStatus s)
        {
            s.Stored = s.Magnitude;
            Check(c, s);
        }

        public override void OnTopUp(StatusController c, ActiveStatus s, in StatusApplication app, float previousMagnitude)
        {
            s.Stored += app.Magnitude;
            Check(c, s);
        }

        private void Check(StatusController c, ActiveStatus s)
        {
            if (s.Stored < Threshold) return;

            Explosions++;
            Vector3 centre = c.transform.position + Vector3.up * 0.9f;
            float damage = BlastDamage;
            if (s.SourceSheet != null)
                damage *= s.SourceSheet.Get(Attr.DamageDealt) * s.SourceSheet.DamageTypeMultiplier(DamageType.Energy);

            DamageInfo blast = DamageInfo.Create(damage, DamageType.Energy, s.SourceTeam, s.Source);
            blast.CanCrit = false;
            blast.Origin = DamageOrigin.StatusTick;

            // Reset before the blast, which may kill the carrier and clear its statuses.
            c.Remove(Id);
            Combat.Explode(centre, BlastRadius, blast.At(centre, Vector3.up), Layers.HitMaskFor(s.SourceTeam), 0.5f);
        }
    }

    /// <summary>
    /// Shillings riding on an enemy. Each application adds its amount, up to <see cref="MaxShillings"/>, and
    /// it never wears off. When the enemy dies they drop, unless the enemy pays no reward at all.
    /// </summary>
    public class GildedStatus : StatusDefinition
    {
        public const float MaxShillings = 10f;

        public override StatusId Id => StatusId.Gilded;
        public override string DisplayName => "Gilded";
        public override string Description => "Drops shillings when it dies.";
        public override Color Tint => new Color(1f, 0.85f, 0.3f);
        public override int MaxStacks => 1;
        public override DebuffResistance Resisted => DebuffResistance.None;
        public override float DecayScale(StatusController c, ActiveStatus s) => 0f;

        public override void OnApplied(StatusController c, ActiveStatus s) => s.Stored = Mathf.Min(MaxShillings, s.Magnitude);

        public override void OnTopUp(StatusController c, ActiveStatus s, in StatusApplication app, float previousMagnitude)
            => s.Stored = Mathf.Min(MaxShillings, s.Stored + app.Magnitude);

        public override void OnRemoved(StatusController c, ActiveStatus s)
        {
            Health health = c.Health;
            if (health == null || health.IsAlive || health.Team != Team.Enemy) return;

            EnemyController enemy = c.GetComponent<EnemyController>();
            if (enemy != null && enemy.PaysNoReward) return;

            int shillings = Mathf.FloorToInt(s.Stored);
            if (shillings > 0) ShillingPickup.Scatter(c.transform.position + Vector3.up * 0.8f, shillings, ShillingSource.Gilded);
        }
    }

    /// <summary>
    /// Builds up with every application and drains over time. When full, the carrier is feared and the
    /// buildup resets. Elites fill at half the rate.
    /// </summary>
    public class DreadStatus : StatusDefinition
    {
        public const float Threshold = 100f;
        public const float DrainPerSecond = 15f;
        public const float EliteRate = 0.5f;
        public const float FearSeconds = 3f;

        public override StatusId Id => StatusId.Dread;
        public override string DisplayName => "Dreading";
        public override string Description => "Fear builds up, draining over time. When full, it flees.";
        public override Color Tint => new Color(0.4f, 0.25f, 0.55f);
        public override int MaxStacks => 1;
        public override float TickInterval => 0.25f;
        public override DebuffResistance Resisted => DebuffResistance.Magnitude;

        public override void OnApplied(StatusController c, ActiveStatus s)
        {
            s.Stored = Fill(c, s.Magnitude);
            Check(c, s);
        }

        public override void OnTopUp(StatusController c, ActiveStatus s, in StatusApplication app, float previousMagnitude)
        {
            s.Stored += Fill(c, app.Magnitude);
            Check(c, s);
        }

        public override void OnTick(StatusController c, ActiveStatus s)
        {
            s.Stored -= DrainPerSecond * TickInterval;
            if (s.Stored <= 0f) c.Remove(Id);
        }

        private static float Fill(StatusController c, float amount)
            => amount * (c.Health != null && c.Health.IsElite ? EliteRate : 1f);

        private void Check(StatusController c, ActiveStatus s)
        {
            if (s.Stored < Threshold) return;

            GameObject source = s.Source;
            Team team = s.SourceTeam;
            c.Remove(Id);
            c.Apply(StatusLibrary.Fear(FearSeconds), source, team);
        }
    }
}
