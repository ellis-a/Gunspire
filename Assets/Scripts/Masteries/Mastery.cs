using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A school's standing effect, growing with how many of that school's spells are equipped. The
    /// shared face every mastery shows the host that ranks them.
    /// </summary>
    public interface IMastery
    {
        SpellSchool School { get; }

        /// <summary>The capped count of the school's equipped spells, from nothing to <see cref="Masteries.Cap"/>.</summary>
        int Rank { get; }

        void Bind(PlayerRig rig);
        void Unbind();
        void SetRank(int rank);
        void OnFloorEntered(RoomRuntime room);
        void ResetForRun();
    }

    public static class Masteries
    {
        /// <summary>
        /// Counted up to four, so with five slots one is always free for a second school. Decided with
        /// the slot count.
        /// </summary>
        public const int Cap = 4;

        /// <summary>
        /// How many equipped spells belong to a school, counted from what is equipped rather than
        /// known, capped, and never for Petty spells, which belong to no school.
        /// </summary>
        public static int Count(IReadOnlyList<Spell> equipped, SpellSchool school)
        {
            if (equipped == null || school == SpellSchool.Petty) return 0;

            int count = 0;
            for (int i = 0; i < equipped.Count; i++)
                if (equipped[i] != null && equipped[i].School == school) count++;

            return Mathf.Min(Cap, count);
        }
    }

    /// <summary>
    /// The common body of a mastery component. Each lives on the player and is ranked by
    /// <see cref="MasteryHost"/>; what a rank does is the mastery's own business.
    /// </summary>
    public abstract class Mastery : MonoBehaviour, IMastery
    {
        public abstract SpellSchool School { get; }
        public int Rank { get; private set; }
        public PlayerRig Rig { get; private set; }

        /// <summary>Raised when the rank or anything the mastery holds changes, for the HUD.</summary>
        public event Action Changed;

        public virtual void Bind(PlayerRig rig) => Rig = rig;

        /// <summary>Drops every subscription. Safe to call twice.</summary>
        public virtual void Unbind() { }

        public void SetRank(int rank)
        {
            rank = Mathf.Clamp(rank, 0, Masteries.Cap);
            if (rank == Rank) return;

            int previous = Rank;
            Rank = rank;
            OnRankChanged(previous);
            RaiseChanged();
        }

        protected virtual void OnRankChanged(int previous) { }

        public virtual void OnFloorEntered(RoomRuntime room) { }

        /// <summary>Clears what a run built up. Called on restart, which reuses the player object.</summary>
        public virtual void ResetForRun() { }

        protected void RaiseChanged() => Changed?.Invoke();

        protected virtual void OnDestroy() => Unbind();

        /// <summary>Removes a modifier this mastery added, when the sheet still holds it.</summary>
        protected static void SetPercent(CharacterSheet sheet, ref StatModifier modifier, Attr attr, float value,
            object source, string label)
        {
            if (sheet == null) return;

            bool held = false;
            if (modifier != null)
            {
                IReadOnlyList<StatModifier> all = sheet.Modifiers;
                for (int i = 0; i < all.Count && !held; i++) held = all[i] == modifier;
            }

            if (Mathf.Abs(value) < 0.0001f)
            {
                if (held) sheet.RemoveModifier(modifier);
                modifier = null;
                return;
            }

            // A restart wipes the sheet under us, so a handle the sheet no longer holds is replaced.
            if (!held)
            {
                modifier = sheet.AddPercent(attr, value, source, label);
                return;
            }

            if (Mathf.Abs(modifier.Value - value) < 0.0005f) return;
            modifier.Value = value;
            sheet.MarkDirty();
        }
    }

    /// <summary>
    /// Ranks every mastery from the spells equipped. Recomputed whenever the equipped set changes and
    /// on every floor arrival; swapping is meant to happen only on special floors, so in time the
    /// second is all that matters, but until those floors exist a spell bound mid-floor counts at once.
    /// </summary>
    public class MasteryHost : MonoBehaviour
    {
        private readonly List<Mastery> _masteries = new List<Mastery>();
        private readonly List<Spell> _equipped = new List<Spell>();
        private SpellBook _book;
        private bool _bound;

        public PlayerRig Rig { get; private set; }
        public IReadOnlyList<Mastery> All => _masteries;

        public T Get<T>() where T : Mastery
        {
            for (int i = 0; i < _masteries.Count; i++)
                if (_masteries[i] is T found) return found;
            return null;
        }

        public Mastery For(SpellSchool school)
        {
            for (int i = 0; i < _masteries.Count; i++)
                if (_masteries[i].School == school) return _masteries[i];
            return null;
        }

        /// <summary>Adds one mastery per school and binds them. Called once the rig is assembled.</summary>
        public void Initialise(PlayerRig rig)
        {
            Unbind();
            Rig = rig;

            _masteries.Clear();
            Add<BeastMastery>();
            Add<ConfluxMastery>();
            Add<BloodDebtMastery>();
            Add<DivineKnowledgeMastery>();
            Add<SoulsMastery>();
            Add<PsiBladesMastery>();
            Add<ArcaneWarpMastery>();

            for (int i = 0; i < _masteries.Count; i++) _masteries[i].Bind(rig);

            _book = rig != null ? rig.Book : null;
            if (_book != null) _book.Changed += Recompute;
            LevelEvents.FloorEntered += OnFloorEntered;
            _bound = true;

            Recompute();
        }

        private void Add<T>() where T : Mastery
        {
            T mastery = GetComponent<T>();
            if (mastery == null) mastery = gameObject.AddComponent<T>();
            _masteries.Add(mastery);
        }

        public void Recompute()
        {
            if (this == null || _book == null) return;

            _equipped.Clear();
            _book.CollectEquipped(_equipped);

            for (int i = 0; i < _masteries.Count; i++)
                _masteries[i].SetRank(Masteries.Count(_equipped, _masteries[i].School));
        }

        private void OnFloorEntered(RoomRuntime room)
        {
            if (this == null) return;

            Recompute();
            for (int i = 0; i < _masteries.Count; i++) _masteries[i].OnFloorEntered(room);
        }

        public void ResetForRun()
        {
            for (int i = 0; i < _masteries.Count; i++) _masteries[i].ResetForRun();
            Recompute();
        }

        /// <summary>Drops every subscription, the masteries' included. Edit mode never calls OnDestroy, so tooling calls this.</summary>
        public void Unbind()
        {
            for (int i = 0; i < _masteries.Count; i++)
                if (_masteries[i] != null) _masteries[i].Unbind();

            if (!_bound) return;
            _bound = false;

            if (_book != null) _book.Changed -= Recompute;
            LevelEvents.FloorEntered -= OnFloorEntered;
        }

        private void OnDestroy() => Unbind();
    }
}
