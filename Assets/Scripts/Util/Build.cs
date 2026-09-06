using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Small helpers for assembling geometry from primitives. The whole game is built from
    /// code, so this is the closest thing the project has to a prefab library.
    /// </summary>
    public static class Build
    {
        public static GameObject Primitive(PrimitiveType type, Transform parent, string name,
            Vector3 localPosition, Vector3 localScale, Material material, bool collider = true, int layer = -1)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }

            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (material != null)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = material;
            }

            if (layer >= 0) Layers.SetRecursively(go, layer);
            return go;
        }

        public static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale,
            Material mat, bool collider = true, int layer = -1)
            => Primitive(PrimitiveType.Cube, parent, name, pos, scale, mat, collider, layer);

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, float diameter,
            Material mat, bool collider = true, int layer = -1)
            => Primitive(PrimitiveType.Sphere, parent, name, pos, Vector3.one * diameter, mat, collider, layer);

        public static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale,
            Material mat, bool collider = true, int layer = -1)
            => Primitive(PrimitiveType.Cylinder, parent, name, pos, scale, mat, collider, layer);

        public static GameObject Quad(Transform parent, string name, Vector3 pos, Vector2 size,
            Material mat, bool collider = false, int layer = -1)
            => Primitive(PrimitiveType.Quad, parent, name, pos, new Vector3(size.x, size.y, 1f), mat, collider, layer);

        public static GameObject Empty(Transform parent, string name, Vector3 localPosition = default)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>A flat disc lying on the ground, used for area telegraphs and portals.</summary>
        public static GameObject GroundDisc(Transform parent, string name, Vector3 pos, float radius, Material mat)
        {
            GameObject go = Primitive(PrimitiveType.Cylinder, parent, name, pos,
                new Vector3(radius * 2f, 0.02f, radius * 2f), mat, false);
            return go;
        }

        /// <summary>Destroys the object after a delay. Cheap replacement for particle systems.</summary>
        public static GameObject Ephemeral(GameObject go, float lifetime)
        {
            if (go != null) Object.Destroy(go, lifetime);
            return go;
        }
    }

    /// <summary>Fades an unlit/transparent renderer out and destroys the object. Used for tracers and pops.</summary>
    public class FadeAndDie : MonoBehaviour
    {
        public float Lifetime = 0.12f;
        public Color StartColor = Color.white;
        public Vector3 GrowPerSecond = Vector3.zero;

        private float _age;
        private Material _mat;

        public static FadeAndDie Attach(GameObject go, float lifetime, Color color, Vector3 growPerSecond = default)
        {
            var f = go.AddComponent<FadeAndDie>();
            f.Lifetime = lifetime;
            f.StartColor = color;
            f.GrowPerSecond = growPerSecond;
            return f;
        }

        private void Start()
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null) _mat = mr.material;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, Lifetime));

            if (_mat != null)
            {
                Color c = StartColor;
                c.a = StartColor.a * (1f - t);
                MaterialLibrary.SetMaterialColor(_mat, c);
            }

            if (GrowPerSecond != Vector3.zero)
                transform.localScale += GrowPerSecond * Time.deltaTime;

            if (_age >= Lifetime) Destroy(gameObject);
        }
    }
}
