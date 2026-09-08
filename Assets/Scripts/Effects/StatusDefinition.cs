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
        /// The applier's sheet, cached when the effect lands so periodic damage can pick up
        /// their per-school bonuses without a component lookup every tick.
        /// </summary>
        public CharacterSheet SourceSheet;

        /// <summary>Stat modifiers this instance owns. Rebuilt whenever the stack count changes.</summary>
        public readonly List<StatModifier> Mods = new List<StatModifier>();

        public float Normalized => Duration <= 0f ? 0f : Mathf.Clamp01(Remaining / Duration);
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

        /// <summary>Statuses cleared when this one lands, e.g. fire thaws ice.</summary>
        public virtual StatusId[] Cleanses => null;

        /// <summary>Rebuild the stat modifiers for the current stack count. Called on apply and on every restack.</summary>
        public virtual void BuildModifiers(StatusController c, ActiveStatus s) { }

        public virtual void OnApplied(StatusController c, ActiveStatus s) { }
        public virtual void OnTick(StatusController c, ActiveStatus s) { }
        public virtual void OnRemoved(StatusController c, ActiveStatus s) { }
    }
}
