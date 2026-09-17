using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>A status effect currently running on one entity.</summary>
    public class ActiveStatus
    {
        public StatusDefinition Def;
        public float Remaining;
        public float Duration;
        public int Stacks;
        public float Magnitude;
        public float TickTimer;
        public GameObject Source;
        public Team SourceTeam;

        /// <summary>
        /// Where the applier stood when it landed. Fear runs from this rather than from the source
        /// object, which a despawning tentacle would leave null.
        /// </summary>
        public Vector3 SourcePosition;
        public bool HasSourcePosition;

        /// <summary>
        /// The applier's sheet, cached when the effect lands so periodic damage can pick up
        /// their per-school bonuses without a component lookup every tick.
        /// </summary>
        public CharacterSheet SourceSheet;

        /// <summary>Stat modifiers this instance owns. Rebuilt whenever the stack count changes.</summary>
        public readonly List<StatModifier> Mods = new List<StatModifier>();

        /// <summary>A running total a status keeps for itself: Hex's stored damage, Volatile's and Dread's buildup, Gilded's shillings.</summary>
        public float Stored;

        public float Normalized => Duration <= 0f ? 0f : Mathf.Clamp01(Remaining / Duration);

        /// <summary>
        /// For a status whose stacks expire separately: one entry per application still running. Empty for every
        /// other status, whose stacks share <see cref="Remaining"/>.
        /// </summary>
        public readonly List<StackBatch> Batches = new List<StackBatch>();
    }

    /// <summary>How <see cref="Attr.DebuffPotency"/> weakens a debuff landing on something. Resilience.</summary>
    public enum DebuffResistance
    {
        /// <summary>It lasts less long.</summary>
        Duration,

        /// <summary>It deals less: its amount shrinks, its duration does not.</summary>
        Magnitude,

        /// <summary>Fewer stacks land.</summary>
        Stacks,

        /// <summary>Untouched.</summary>
        None
    }

    /// <summary>Stacks that landed together, and fall off together when their own time runs out.</summary>
    public class StackBatch
    {
        public int Count;
        public float Remaining;
    }

    /// <summary>
    /// Behaviour for one kind of status effect. Definitions are stateless singletons
    /// created once by <see cref="StatusLibrary"/>; per-entity state lives in <see cref="ActiveStatus"/>.
    /// </summary>
    public abstract class StatusDefinition
    {
        public abstract StatusId Id { get; }
        public virtual string DisplayName => Id.ToString();
        public virtual string Description => string.Empty;
        public virtual Color Tint => Color.white;
        public virtual int MaxStacks => 1;
        public virtual float TickInterval => 0.5f;
        public virtual bool IsDebuff => true;

        /// <summary>
        /// Each application's stacks keep their own timer and fall off when it runs out, rather than every stack
        /// sharing one timer that each new application refreshes. For uncapped stacks, where a shared timer would
        /// let steady application climb forever.
        /// </summary>
        public virtual bool StacksExpireSeparately => false;

        /// <summary>
        /// How fast this effect burns through its duration. One is real time; two wears off in
        /// half as long. Poison uses it to reward standing still.
        /// </summary>
        public virtual float DecayScale(StatusController c, ActiveStatus s) => 1f;

        /// <summary>
        /// Statuses cleared when this one lands. Nothing uses it at present: burn and frost
        /// used to cancel each other and no longer do, because they are magic and are allowed
        /// to coexist. Kept for the cleanse and dispel effects that will want it.
        /// </summary>
        public virtual StatusId[] Cleanses => null;

        /// <summary>
        /// Direct damage ends this status. Never the hit that applied it, and never a status tick,
        /// so a sleeping enemy that is also burning stays asleep.
        /// </summary>
        public virtual bool EndsOnDamage => false;

        /// <summary>Duration multiplier on an elite. Control effects bend elites rather than exempting them.</summary>
        public virtual float EliteDurationScale => 1f;

        /// <summary>How a lowered debuff potency on the target weakens this. Only read for debuffs.</summary>
        public virtual DebuffResistance Resisted => DebuffResistance.Duration;

        /// <summary>Whether this can land on the target at all. Charm refuses elites.</summary>
        public virtual bool CanApplyTo(StatusController c) => true;

        /// <summary>
        /// This status as a caster with the given spell power applies it. Power strengthens a
        /// spell's statuses by the same multiplier it strengthens the spell's damage, applied to
        /// whichever number is this status's amount.
        ///
        /// Unchanged by default, so a status with no amount to grow, like a mark that either
        /// lands or does not, never quietly scales. Every override is a straight one-to-one
        /// mapping for now: a point of burn is not worth a point of damage or a stack of frost,
        /// and each is expected to get its own curve once play shows what it should be.
        /// </summary>
        public virtual StatusApplication Empower(StatusApplication app, float spellPower) => app;

        protected static StatusApplication ScaleMagnitude(StatusApplication app, float spellPower)
        {
            app.Magnitude *= spellPower;
            return app;
        }

        /// <summary>Rounded, so a status with only a few stacks needs a real gain in power to earn another.</summary>
        protected static StatusApplication ScaleStacks(StatusApplication app, float spellPower)
        {
            app.Stacks = Mathf.Max(1, Mathf.RoundToInt(app.Stacks * spellPower));
            return app;
        }

        /// <summary>Rebuild the stat modifiers for the current stack count. Called on apply and on every restack.</summary>
        public virtual void BuildModifiers(StatusController c, ActiveStatus s) { }

        public virtual void OnApplied(StatusController c, ActiveStatus s) { }
        public virtual void OnTick(StatusController c, ActiveStatus s) { }
        public virtual void OnRemoved(StatusController c, ActiveStatus s) { }

        /// <summary>
        /// A further application landed on a status already running, after the controller's own top-up.
        /// <paramref name="previousMagnitude"/> is the magnitude before the top-up kept the larger of the two.
        /// </summary>
        public virtual void OnTopUp(StatusController c, ActiveStatus s, in StatusApplication app, float previousMagnitude) { }

        /// <summary>The carrier took damage while this ran. Never called for the carrier's death blow's aftermath.</summary>
        public virtual void OnOwnerDamaged(StatusController c, ActiveStatus s, in DamageInfo hit, float amount) { }
    }
}
