using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Death's mastery: souls bound by kills, held up to a cap, spent by Death spells. Every enemy
    /// death counts, whoever dealt it. Kills at the cap bind nothing, which is pressure to spend rather
    /// than hoard. Souls carry between floors; a cap lowered below what you hold loses the excess, with
    /// a line saying so.
    ///
    /// The cap shares Psi Blades' curve: two at one spell, one more for each spell after.
    /// </summary>
    public class SoulsMastery : Mastery
    {
        public const int BaseCap = 2;

        private const float OrbitRadius = 0.85f;
        private const float OrbitHeight = 1.35f;
        private const float OrbitDegreesPerSecond = 90f;

        public override SpellSchool School => SpellSchool.Death;

        public int Souls { get; private set; }
        public int Cap => Rank <= 0 ? 0 : CapFor(Rank) + Mathf.Max(0, CapBonus);

        // ---- set by boons, cleared each run ----

        /// <summary>Extra souls held while the mastery is active. Soul Jar.</summary>
        public int CapBonus;

        /// <summary>Souls spent, by any means.</summary>
        public event System.Action<int> Spent;

        /// <summary>A kill that bound nothing because the cap was full, with the enemy it came from. Overflowing Souls.</summary>
        public event System.Action<Health> WastedAtCap;

        public static int CapFor(int rank) => rank <= 0 ? 0 : BaseCap + (rank - 1);

        private bool _bound;
        private readonly List<GameObject> _orbs = new List<GameObject>();
        private float _orbitAngle;

        public override void Bind(PlayerRig rig)
        {
            base.Bind(rig);
            if (_bound) return;

            Health.AnyDied += OnAnyDied;
            _bound = true;
        }

        public override void Unbind()
        {
            if (!_bound) return;
            Health.AnyDied -= OnAnyDied;
            _bound = false;
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (this == null) return;
            if (!RunState.CountsAsKill(victim)) return;
            if (AddSouls(1) == 0 && Rank > 0) WastedAtCap?.Invoke(victim);
        }

        /// <summary>Binds up to the cap. Returns how many were bound; the rest are wasted.</summary>
        public int AddSouls(int count)
        {
            int room = Mathf.Max(0, Cap - Souls);
            int bound = Mathf.Clamp(count, 0, room);
            if (bound <= 0) return 0;

            Souls += bound;
            RaiseChanged();
            return bound;
        }

        /// <summary>Refused, spending nothing, when there are too few.</summary>
        public bool TrySpend(int count)
        {
            if (count <= 0) return true;
            if (Souls < count) return false;

            Souls -= count;
            RaiseChanged();
            Spent?.Invoke(count);
            return true;
        }

        /// <summary>Spends every soul held, for Bone Shards. Returns how many that was.</summary>
        public int SpendAll()
        {
            int spent = Souls;
            if (spent <= 0) return 0;

            Souls = 0;
            RaiseChanged();
            Spent?.Invoke(spent);
            return spent;
        }

        protected override void OnRankChanged(int previous)
        {
            if (Souls <= Cap) return;

            int lost = Souls - Cap;
            Souls = Cap;
            GameDirector.Instance?.Notify(lost + (lost == 1 ? " soul slips" : " souls slip")
                                          + " away - too few Death spells to hold them", 2.5f);
        }

        public override void ResetForRun()
        {
            Souls = 0;
            CapBonus = 0;
            RaiseChanged();
        }

        // ---------------------------------------------------------------- the orbiting souls

        private void Update()
        {
            if (!Application.isPlaying || Rig == null) return;

            while (_orbs.Count < Souls) _orbs.Add(BuildOrb());
            while (_orbs.Count > Souls)
            {
                GameObject last = _orbs[_orbs.Count - 1];
                _orbs.RemoveAt(_orbs.Count - 1);
                if (last != null) Destroy(last);
            }

            if (_orbs.Count == 0) return;

            _orbitAngle += OrbitDegreesPerSecond * Time.deltaTime;
            Vector3 centre = Rig.transform.position + Vector3.up * OrbitHeight;

            for (int i = 0; i < _orbs.Count; i++)
            {
                if (_orbs[i] == null) continue;

                float angle = (_orbitAngle + i * 360f / _orbs.Count) * Mathf.Deg2Rad;
                _orbs[i].transform.position = centre
                    + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 2f) * 0.12f, Mathf.Sin(angle)) * OrbitRadius;
            }
        }

        private GameObject BuildOrb()
        {
            // Parented to the player so a restart or a destroyed rig takes them along; placed in world space.
            return Build.Sphere(Rig.transform, "Soul", Vector3.zero, 0.16f,
                MaterialLibrary.Emissive(new Color(0.55f, 0.95f, 0.75f), 3f), collider: false);
        }
    }
}
