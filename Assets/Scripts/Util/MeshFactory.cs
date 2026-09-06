using UnityEngine;

namespace WizardGun
{
    /// <summary>Procedural meshes for shapes Unity has no primitive for.</summary>
    public static class MeshFactory
    {
        private static Mesh _cone;

        /// <summary>Unit cone: apex at the origin, opening along +Z, height 1, base radius 1.</summary>
        public static Mesh UnitCone(int segments = 24)
        {
            if (_cone != null) return _cone;

            var mesh = new Mesh { name = "Cone" };
            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero;                 // apex
            vertices[1] = new Vector3(0f, 0f, 1f);      // base centre

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                vertices[i + 2] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f);
            }

            var triangles = new int[segments * 6];
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i + 2;
                int next = (i + 1) % segments + 2;

                // side
                triangles[t++] = 0;
                triangles[t++] = next;
                triangles[t++] = current;

                // base cap
                triangles[t++] = 1;
                triangles[t++] = current;
                triangles[t++] = next;
            }

            // Sprites/Default multiplies by vertex colour, so fill the channel explicitly.
            var colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;

            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i++) uvs[i] = Vector2.one * 0.5f;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _cone = mesh;
            return _cone;
        }

        /// <summary>
        /// A cone object pointing along the transform forward axis, sized in world units.
        /// Used for Cone of Cold and enemy breath telegraphs.
        /// </summary>
        public static GameObject SpawnCone(Vector3 origin, Vector3 forward, float range,
            float halfAngleDegrees, Material material, Transform parent = null)
        {
            var go = new GameObject("Cone");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(forward);

            float radius = range * Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad);
            go.transform.localScale = new Vector3(radius, radius, range);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = UnitCone();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }
    }
}
