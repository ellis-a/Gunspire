using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Damage feedback: a white flash on hit, a status tint while burning or chilled, and a
    /// burst of debris on death. Each renderer gets its own material instance so flashing one
    /// enemy does not light up the whole room.
    /// </summary>
    public class EnemyVisuals : MonoBehaviour
    {
        [SerializeField] private float flashDuration = 0.08f;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Color> _baseColors = new List<Color>();
        private Health _health;
        private StatusController _status;
        private float _flashTimer;
        private Color _bodyColor = Color.white;

        public void Configure(Color bodyColor) => _bodyColor = bodyColor;

        private void Start()
        {
            _health = GetComponent<Health>();
            _status = GetComponent<StatusController>();

            GetComponentsInChildren(true, _renderers);
            for (int i = 0; i < _renderers.Count; i++)
            {
                Material instance = _renderers[i].material;   // forces a per-renderer copy
                Color c = instance.HasProperty("_BaseColor") ? instance.GetColor("_BaseColor")
                        : instance.HasProperty("_Color") ? instance.GetColor("_Color")
                        : Color.white;
                _baseColors.Add(c);
            }

            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
        }

        private void OnDamaged(DamageInfo info, float amount)
        {
            _flashTimer = flashDuration;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f) ApplyTint(StatusTint());
                else ApplyTint(Color.white);
                return;
            }

            ApplyTint(StatusTint());
        }

        /// <summary>Blends toward the colour of whatever is currently afflicting the enemy.</summary>
        private Color StatusTint()
        {
            if (_status == null || _status.Active.Count == 0) return Color.clear;

            Color accumulated = Color.clear;
            int count = 0;
            for (int i = 0; i < _status.Active.Count; i++)
            {
                ActiveStatus s = _status.Active[i];
                if (!s.Def.IsDebuff) continue;
                accumulated += s.Def.Tint;
                count++;
            }
            if (count == 0) return Color.clear;
            return accumulated / count;
        }

        private void ApplyTint(Color tint)
        {
            bool none = tint == Color.clear;
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;
                Color target = none ? _baseColors[i] : Color.Lerp(_baseColors[i], tint, 0.45f);
                MaterialLibrary.SetMaterialColor(_renderers[i].material, target);
            }
        }

        private void OnDied(DamageInfo info)
        {
            Vector3 center = transform.position + Vector3.up * 0.9f;
            Color color = Palette.ForDamageType(info.Type);

            for (int i = 0; i < 10; i++)
            {
                GameObject shard = Build.Cube(null, "Debris",
                    center + Random.insideUnitSphere * 0.5f,
                    Vector3.one * Random.Range(0.1f, 0.28f),
                    MaterialLibrary.Emissive(Color.Lerp(_bodyColor, color, 0.5f), 1.5f), collider: false);
                shard.transform.rotation = Random.rotation;
                FadeAndDie.Attach(shard, 0.45f, Color.Lerp(_bodyColor, color, 0.5f));
            }

            GameObject pop = Build.Sphere(null, "DeathPop", center, 1.6f,
                MaterialLibrary.Transparent(new Color(color.r, color.g, color.b, 0.4f)), collider: false);
            FadeAndDie.Attach(pop, 0.25f, new Color(color.r, color.g, color.b, 0.4f), Vector3.one * 4f);
        }
    }
}
