using System;
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
    /// The live copy is a shallow copy of the template. A subclass holding mutable state in a
    /// reference type (a list, a set) must create it fresh in <see cref="OnBind"/>.
    /// </summary>
    [Serializable]
    public abstract class BoonBehaviour
    {
        [NonSerialized] private RunState _run;
        [NonSerialized] private int _level;

        public RunState Run => _run;
        public int Level => _level;
        public PlayerRig Player => _run != null ? _run.Player : null;

        internal BoonBehaviour Spawn(RunState run)
        {
            var live = (BoonBehaviour)MemberwiseClone();
            live._run = run;
            live._level = 0;
            return live;
        }

        internal void SetLevel(int level)
        {
            int previous = _level;
            _level = level;

            if (previous == 0)
            {
                OnBind();
                CombatRules.Register(this);
            }
            OnLevelChanged(previous);
        }

        internal void Release()
        {
            CombatRules.Unregister(this);
            OnUnbind();
        }

        /// <summary>Taken for the first time. Subscribe to events here.</summary>
        protected virtual void OnBind() { }

        /// <summary>The run is ending. Unsubscribe from everything <see cref="OnBind"/> subscribed to.</summary>
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
