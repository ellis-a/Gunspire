using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Bestial's mastery: a companion that climbs a ladder with the count. One spell is a jackalope,
    /// two a fox, three a wolf, four a bear. The first steps are the big ones, which lives in the
    /// definitions in <see cref="MinionLibrary"/>.
    ///
    /// Decided with the user: a companion that dies is gone until the next floor. A new rank mid-floor
    /// replaces a living companion with the new tier, but does not bring back one that died.
    /// </summary>
    public class BeastMastery : Mastery
    {
        public override SpellSchool School => SpellSchool.Bestial;

        public MinionController Companion { get; private set; }

        /// <summary>The companion died on this floor, so nothing is summoned until the next.</summary>
        public bool LostThisFloor { get; private set; }

        /// <summary>The companion died. Vengeful Rage.</summary>
        public event System.Action CompanionDied;

        /// <summary>Whether a hit or kill came from the companion. Blooded.</summary>
        public bool IsCompanionSource(GameObject source) => source != null && Companion != null && source == Companion.gameObject;

        /// <summary>Whether a minion id is one of the companion's forms.</summary>
        public static bool IsCompanion(string minionId)
            => minionId == "jackalope" || minionId == "fox" || minionId == "wolf" || minionId == "bear";

        public static string TierFor(int rank)
        {
            switch (rank)
            {
                case 1: return "jackalope";
                case 2: return "fox";
                case 3: return "wolf";
                default: return rank >= 4 ? "bear" : null;
            }
        }

        protected override void OnRankChanged(int previous) => Resummon();

        public override void OnFloorEntered(RoomRuntime room)
        {
            LostThisFloor = false;

            // The room change has already destroyed every minion, the companion with them, but Destroy only lands at
            // the end of the frame. Until then the old companion still looks alive, and Resummon would keep it.
            Dismiss();
            Resummon();
        }

        public override void ResetForRun()
        {
            LostThisFloor = false;
            Dismiss();
            Resummon();
        }

        public override void Unbind() => Dismiss();

        /// <summary>Makes the companion out match the rank: summoned, replaced by a new tier, or dismissed.</summary>
        public void Resummon()
        {
            string wanted = TierFor(Rank);

            if (Companion != null && Companion.Definition != null && Companion.Definition.Id == wanted
                && Companion.Health != null && Companion.Health.IsAlive)
                return;

            Dismiss();
            if (wanted == null || LostThisFloor || Rig == null) return;

            Companion = MinionSummoner.Spawn(wanted, SpotNearPlayer());
            if (Companion != null && Companion.Health != null) Companion.Health.Died += OnCompanionDied;
        }

        private void OnCompanionDied(DamageInfo info)
        {
            LostThisFloor = true;
            if (Companion != null && Companion.Health != null) Companion.Health.Died -= OnCompanionDied;
            Companion = null;
            CompanionDied?.Invoke();
        }

        private void Dismiss()
        {
            if (Companion == null) return;

            if (Companion.Health != null) Companion.Health.Died -= OnCompanionDied;

            GameObject body = Companion.gameObject;
            Companion = null;

            if (Application.isPlaying) Destroy(body);
            else DestroyImmediate(body);
        }

        private Vector3 SpotNearPlayer()
        {
            Vector3 around = Rig.transform.position;
            NavField field = NavField.Current;

            if (field != null && field.IsBuilt)
            {
                Rng rng = RunState.Current != null ? RunState.Current.Rng : new Rng(Random.Range(0, int.MaxValue));
                if (field.TryFindSpot(rng, around, 3f, out Vector3 spot)) return spot;
            }

            return around - Rig.transform.forward * 1.5f;
        }
    }
}
