using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Divination's mastery: information about enemies, drawn over the world.
    ///
    /// - 1 spell: health bars.
    /// - 2: sight cones and hearing ranges, shown only before an enemy has noticed anything.
    /// - 3: a timer on each enemy's next attack.
    /// - 4: deferred; it grants nothing beyond three for now.
    ///
    /// Bars and timers are drawn by the HUD from <see cref="Collect"/>. Cones and rings are world
    /// objects kept here, so they sit on the floor where the enemy's senses actually reach.
    /// </summary>
    public class DivineKnowledgeMastery : Mastery
    {
        public const float Range = 45f;

        /// <summary>Multiplies how far the readouts reach. Set by boons, cleared each run. Farsight.</summary>
        public float RangeMultiplier = 1f;

        /// <summary>How far the readouts reach now.</summary>
        public float CurrentRange => Range * Mathf.Max(0f, RangeMultiplier);

        public override void ResetForRun() => RangeMultiplier = 1f;
        private const float EnemyRefreshSeconds = 0.5f;

        public override SpellSchool School => SpellSchool.Divination;

        public bool ShowsHealthBars => Rank >= 1;
        public bool ShowsPerception => Rank >= 2;
        public bool ShowsAttackTimers => Rank >= 3;

        public struct Readout
        {
            public EnemyController Enemy;
            public float HealthFraction;

            /// <summary>Its senses are shown: the rank allows it and it has not noticed anything yet.</summary>
            public bool ShowPerception;
            public float SightRange;
            public float SightHalfAngle;
            public float HearingRange;

            public bool ShowTimer;
            public float NextAttackIn;
        }

        private readonly List<EnemyController> _enemies = new List<EnemyController>();
        private readonly List<Readout> _readouts = new List<Readout>();
        private float _refreshTimer;

        private readonly Dictionary<EnemyController, GameObject> _markers = new Dictionary<EnemyController, GameObject>();
        private readonly List<EnemyController> _markerScratch = new List<EnemyController>();
        private Material _coneMaterial;
        private Material _ringMaterial;

        /// <summary>
        /// Everything this rank reveals about the enemies near a point. Pass the enemies to look at, or
        /// null to use the ones found in the scene.
        /// </summary>
        public void Collect(Vector3 around, List<Readout> into, IReadOnlyList<EnemyController> enemies = null,
            Vector3? eye = null)
        {
            into.Clear();
            if (Rank <= 0) return;

            IReadOnlyList<EnemyController> source = enemies ?? _enemies;
            for (int i = 0; i < source.Count; i++)
            {
                EnemyController enemy = source[i];
                if (enemy == null || enemy.IsHidden || enemy.IsPossessed) continue;

                Health health = enemy.Health;
                if (health == null || !health.IsAlive) continue;
                if ((enemy.transform.position - around).sqrMagnitude > CurrentRange * CurrentRange) continue;
                if (eye.HasValue && !InSight(eye.Value, enemy)) continue;

                into.Add(new Readout
                {
                    Enemy = enemy,
                    HealthFraction = health.Fraction,
                    ShowPerception = ShowsPerception && !enemy.IsAlerted,
                    SightRange = enemy.SightRange,
                    SightHalfAngle = enemy.SightHalfAngle,
                    HearingRange = enemy.HearingRange,
                    ShowTimer = ShowsAttackTimers,
                    NextAttackIn = enemy.NextAttackIn
                });
            }
        }

        /// <summary>
        /// Whether the eye can see any of the enemy, its head or its middle. Knowledge is about what is in front of you,
        /// so a wall hides it; two heights keep an enemy half behind cover readable.
        /// </summary>
        public static bool InSight(Vector3 eye, EnemyController enemy)
        {
            Vector3 feet = enemy.transform.position;
            return ClearLine(eye, feet + Vector3.up * enemy.EyeHeight)
                   || ClearLine(eye, feet + Vector3.up * (enemy.EyeHeight * 0.5f));
        }

        private static bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.3f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.3f, Layers.SightBlockMask,
                QueryTriggerInteraction.Ignore);
        }

        protected override void OnRankChanged(int previous)
        {
            if (!ShowsPerception) ClearMarkers();
        }

        public override void OnFloorEntered(RoomRuntime room)
        {
            ClearMarkers();
            _refreshTimer = 0f;
        }

        public override void Unbind() => ClearMarkers();

        // ---------------------------------------------------------------- drawing

        private void LateUpdate()
        {
            if (!Application.isPlaying || Rig == null) return;

            if ((_refreshTimer -= Time.deltaTime) <= 0f)
            {
                _refreshTimer = EnemyRefreshSeconds;
                _enemies.Clear();
                if (Rank > 0) _enemies.AddRange(FindObjectsByType<EnemyController>(FindObjectsSortMode.None));
            }

            Vector3? eye = Rig.Camera != null ? Rig.Camera.transform.position : (Vector3?)null;
            Collect(Rig.transform.position, _readouts, null, eye);
            SyncMarkers();
        }

        /// <summary>Bars over heads and attack timers. Called from the HUD's OnGUI.</summary>
        public void DrawGUI(Camera camera)
        {
            if (camera == null || Rank <= 0) return;

            for (int i = 0; i < _readouts.Count; i++)
            {
                Readout readout = _readouts[i];
                if (readout.Enemy == null) continue;

                Vector3 head = readout.Enemy.transform.position + Vector3.up * (readout.Enemy.EyeHeight + 0.6f);
                Vector3 screen = camera.WorldToScreenPoint(head);
                if (screen.z <= 0f) continue;

                float x = screen.x;
                float y = Screen.height - screen.y;

                if (ShowsHealthBars)
                {
                    var bar = new Rect(x - 26f, y - 4f, 52f, 6f);
                    UIStyles.Bar(bar, readout.HealthFraction, UIStyles.HealthColor, new Color(0f, 0f, 0f, 0.6f));
                }

                if (readout.ShowTimer)
                {
                    string timer = readout.NextAttackIn <= 0.05f ? "!" : readout.NextAttackIn.ToString("0.0");
                    UIStyles.Text(new Rect(x - 30f, y - 24f, 60f, 18f), timer, UIStyles.Center,
                        readout.NextAttackIn <= 0.5f ? UIStyles.Warning : UIStyles.Ink);
                }
            }
        }

        private void SyncMarkers()
        {
            _markerScratch.Clear();

            for (int i = 0; i < _readouts.Count; i++)
            {
                Readout readout = _readouts[i];
                if (!readout.ShowPerception) continue;

                _markerScratch.Add(readout.Enemy);
                if (!_markers.TryGetValue(readout.Enemy, out GameObject marker) || marker == null)
                {
                    marker = BuildMarker(readout);
                    _markers[readout.Enemy] = marker;
                }

                Transform enemy = readout.Enemy.transform;
                marker.transform.position = enemy.position;
                Vector3 forward = new Vector3(enemy.forward.x, 0f, enemy.forward.z);
                if (forward.sqrMagnitude > 0.001f) marker.transform.rotation = Quaternion.LookRotation(forward);
            }

            var stale = new List<EnemyController>();
            foreach (KeyValuePair<EnemyController, GameObject> pair in _markers)
                if (pair.Key == null || !_markerScratch.Contains(pair.Key)) stale.Add(pair.Key);

            for (int i = 0; i < stale.Count; i++)
            {
                if (_markers[stale[i]] != null) Destroy(_markers[stale[i]]);
                _markers.Remove(stale[i]);
            }
        }

        private GameObject BuildMarker(Readout readout)
        {
            if (_coneMaterial == null) _coneMaterial = MaterialLibrary.Transparent(new Color(1f, 0.95f, 0.6f, 0.08f));
            if (_ringMaterial == null) _ringMaterial = MaterialLibrary.Transparent(new Color(0.6f, 0.8f, 1f, 0.06f));

            var root = new GameObject("Divine Knowledge Senses");

            GameObject cone = MeshFactory.SpawnCone(Vector3.zero, Vector3.forward, readout.SightRange,
                Mathf.Min(readout.SightHalfAngle, 80f), _coneMaterial, root.transform);
            cone.transform.localPosition = new Vector3(0f, readout.Enemy.EyeHeight, 0f);
            cone.transform.localRotation = Quaternion.identity;

            Build.GroundDisc(root.transform, "Hearing", new Vector3(0f, 0.05f, 0f), readout.HearingRange, _ringMaterial);
            return root;
        }

        private void ClearMarkers()
        {
            foreach (GameObject marker in _markers.Values)
            {
                if (marker == null) continue;
                if (Application.isPlaying) Destroy(marker);
                else DestroyImmediate(marker);
            }
            _markers.Clear();
        }
    }
}
