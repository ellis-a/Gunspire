using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Animates a warning shape: it brightens and fills as the attack winds up, so the
    /// player can read the danger and be somewhere else when it lands.
    /// </summary>
    public class TelegraphVisual : MonoBehaviour
    {
        public float Duration = 1f;
        public Color FromColor = new Color(1f, 0.25f, 0.25f, 0.10f);
        public Color ToColor = new Color(1f, 0.25f, 0.25f, 0.55f);
        public Transform FillTarget;
        public Vector3 FillFromScale;
        public Vector3 FillToScale;
        public bool DestroyOnComplete = true;

        private float _age;
        private Material _material;
        private Material _fillMaterial;

        private void Awake()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) _material = mr.material;
        }

        private void Start()
        {
            if (FillTarget != null)
            {
                var mr = FillTarget.GetComponent<MeshRenderer>();
                if (mr != null) _fillMaterial = mr.material;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, Duration));

            Color c = Color.Lerp(FromColor, ToColor, t);
            MaterialLibrary.SetMaterialColor(_material, c);
            MaterialLibrary.SetMaterialColor(_fillMaterial, c);

            if (FillTarget != null)
                FillTarget.localScale = Vector3.Lerp(FillFromScale, FillToScale, t);

            if (t >= 1f && DestroyOnComplete) Destroy(gameObject);
        }
    }

    /// <summary>Factory for the three warning shapes the game uses.</summary>
    public static class Telegraph
    {
        public static readonly Color Danger = new Color(1f, 0.28f, 0.25f);

        /// <summary>Ground circle with an outline and a filling interior. Used for slams and drops.</summary>
        public static GameObject Circle(Vector3 center, float radius, float duration, Color? color = null)
        {
            Color c = color ?? Danger;
            var root = new GameObject("Telegraph_Circle");
            root.transform.position = center + Vector3.up * 0.04f;

            var outlineColor = new Color(c.r, c.g, c.b, 0.16f);
            GameObject outline = Build.GroundDisc(root.transform, "Outline", Vector3.zero, radius,
                MaterialLibrary.Transparent(outlineColor));

            var fillColor = new Color(c.r, c.g, c.b, 0.45f);
            GameObject fill = Build.GroundDisc(root.transform, "Fill", new Vector3(0f, 0.01f, 0f), radius,
                MaterialLibrary.Transparent(fillColor));

            var vis = outline.AddComponent<TelegraphVisual>();
            vis.Duration = duration;
            vis.FromColor = new Color(c.r, c.g, c.b, 0.12f);
            vis.ToColor = new Color(c.r, c.g, c.b, 0.5f);
            vis.FillTarget = fill.transform;
            vis.FillFromScale = new Vector3(0.02f, 0.02f, 0.02f);
            vis.FillToScale = fill.transform.localScale;
            vis.DestroyOnComplete = false;

            Object.Destroy(root, duration + 0.12f);
            return root;
        }

        /// <summary>A thin line from the caster along the firing direction. Used before beams.</summary>
        public static GameObject Line(Vector3 from, Vector3 direction, float length, float width,
            float duration, Color? color = null)
        {
            Color c = color ?? Danger;
            Vector3 center = from + direction.normalized * (length * 0.5f);

            GameObject go = Build.Cube(null, "Telegraph_Line", center,
                new Vector3(width, width, length),
                MaterialLibrary.Transparent(new Color(c.r, c.g, c.b, 0.12f)), collider: false);
            go.transform.rotation = Quaternion.LookRotation(direction);

            var vis = go.AddComponent<TelegraphVisual>();
            vis.Duration = duration;
            vis.FromColor = new Color(c.r, c.g, c.b, 0.10f);
            vis.ToColor = new Color(c.r, c.g, c.b, 0.60f);
            return go;
        }

        /// <summary>A warning cone for breath attacks and shotgun blasts.</summary>
        public static GameObject Cone(Vector3 origin, Vector3 forward, float range, float halfAngle,
            float duration, Color? color = null)
        {
            Color c = color ?? Danger;
            GameObject go = MeshFactory.SpawnCone(origin, forward, range, halfAngle,
                MaterialLibrary.Transparent(new Color(c.r, c.g, c.b, 0.12f)));
            go.name = "Telegraph_Cone";

            var vis = go.AddComponent<TelegraphVisual>();
            vis.Duration = duration;
            vis.FromColor = new Color(c.r, c.g, c.b, 0.08f);
            vis.ToColor = new Color(c.r, c.g, c.b, 0.45f);
            return go;
        }

        /// <summary>A brief flash on the caster so melee wind-ups read at close range.</summary>
        public static GameObject Flash(Transform parent, Vector3 localOffset, float radius, float duration, Color color)
        {
            GameObject go = Build.Sphere(parent, "Telegraph_Flash", localOffset, radius * 2f,
                MaterialLibrary.Transparent(new Color(color.r, color.g, color.b, 0.15f)), collider: false);

            var vis = go.AddComponent<TelegraphVisual>();
            vis.Duration = duration;
            vis.FromColor = new Color(color.r, color.g, color.b, 0.12f);
            vis.ToColor = new Color(color.r, color.g, color.b, 0.7f);
            return go;
        }
    }
}
