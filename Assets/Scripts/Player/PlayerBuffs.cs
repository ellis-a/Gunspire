using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>When a conditional buff applies. Stored as integers on spell assets: append only.</summary>
    public enum BuffCondition
    {
        Always,

        /// <summary>Moving along the route to the floor's exit. Path of Light.</summary>
        FollowingExitRoute,

        Grounded,
        Airborne,
        Moving
    }

    /// <summary>
    /// Fixed-duration buffs that only take effect while a condition holds. The timer runs regardless;
    /// the modifier goes on while the condition is true and comes off while it is not, so Path of Light
    /// lasts its full length and doubles your speed only for the parts of it spent on the route.
    /// </summary>
    public class PlayerBuffs : MonoBehaviour
    {
        private sealed class Active
        {
            public string Id;
            public BuffCondition Condition;
            public Attr Attr;
            public float Percent;
            public float Remaining;
            public StatModifier Modifier;
        }

        private readonly List<Active> _active = new List<Active>();

        public PlayerRig Rig { get; private set; }

        /// <summary>Answers conditions in tooling, where there is no room, route or movement to read.</summary>
        public Func<BuffCondition, bool> ConditionOverride { get; set; }

        public int Count => _active.Count;

        public void Bind(PlayerRig rig) => Rig = rig;

        /// <summary>Starts a buff, or restarts one with the same id.</summary>
        public void Add(string id, BuffCondition condition, Attr attr, float percent, float seconds)
        {
            if (seconds <= 0f) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Id != id) continue;
                TakeOff(_active[i]);
                _active.RemoveAt(i);
            }

            _active.Add(new Active { Id = id, Condition = condition, Attr = attr, Percent = percent, Remaining = seconds });
            Tick(0f);
        }

        public bool Has(string id) => Find(id) != null;

        /// <summary>Running, and its condition held at the last tick, so the modifier is on the sheet.</summary>
        public bool IsApplied(string id)
        {
            Active buff = Find(id);
            return buff != null && buff.Modifier != null;
        }

        private Active Find(string id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Id == id) return _active[i];
            return null;
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>One frame on the player's clock. Update calls it; public so tooling can step time.</summary>
        public void Tick(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Active buff = _active[i];
                buff.Remaining -= dt;

                if (buff.Remaining <= 0f)
                {
                    TakeOff(buff);
                    _active.RemoveAt(i);
                    continue;
                }

                bool met = Evaluate(buff.Condition);
                if (met && buff.Modifier == null && Rig != null && Rig.Sheet != null)
                    buff.Modifier = Rig.Sheet.AddPercent(buff.Attr, buff.Percent, this, buff.Id);
                else if (!met)
                    TakeOff(buff);
            }
        }

        public bool Evaluate(BuffCondition condition)
        {
            if (ConditionOverride != null) return ConditionOverride(condition);

            PlayerMotor motor = Rig != null ? Rig.Motor : null;
            switch (condition)
            {
                case BuffCondition.Always: return true;
                case BuffCondition.Grounded: return motor != null && motor.IsGrounded;
                case BuffCondition.Airborne: return motor != null && !motor.IsGrounded;
                case BuffCondition.Moving: return motor != null && motor.HorizontalSpeed > 1f;

                case BuffCondition.FollowingExitRoute:
                    GameDirector director = GameDirector.Instance;
                    RoomRuntime room = director != null ? director.CurrentRoom : null;
                    ExitRouteMap route = room != null ? room.ExitRoute : null;
                    return route != null && motor != null && route.IsFollowing(Rig.transform.position, motor.Velocity);

                default: return false;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _active.Count; i++) TakeOff(_active[i]);
            _active.Clear();
        }

        private void TakeOff(Active buff)
        {
            if (buff.Modifier != null && Rig != null && Rig.Sheet != null) Rig.Sheet.RemoveModifier(buff.Modifier);
            buff.Modifier = null;
        }
    }
}
