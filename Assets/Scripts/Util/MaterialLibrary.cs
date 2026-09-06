using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Builds materials at runtime without shipping any asset files, and without caring
    /// whether the project is on the Built-in pipeline or URP. Shader names are probed in
    /// order and the first one that exists wins.
    /// </summary>
    public static class MaterialLibrary
    {
        private static Shader _lit;
        private static Shader _unlit;
        private static Shader _transparent;
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        private static Shader FindShader(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader s = Shader.Find(names[i]);
                if (s != null) return s;
            }
            return null;
        }

        public static Shader LitShader
        {
            get
            {
                if (_lit == null)
                    _lit = FindShader("Universal Render Pipeline/Lit", "Standard", "Legacy Shaders/Diffuse");
                return _lit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null)
                    _unlit = FindShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default");
                return _unlit;
            }
        }

        public static Shader TransparentShader
        {
            get
            {
                // URP Unlit can be switched into alpha blending; on Built-in, Sprites/Default is
                // the one always-included shader that takes a tint colour with alpha.
                if (_transparent == null)
                    _transparent = FindShader("Universal Render Pipeline/Unlit", "Sprites/Default",
                        "UI/Default", "Unlit/Color");
                return _transparent;
            }
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void SetFloatIfPresent(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        /// <summary>Opaque, lit surface. Use for level geometry and characters.</summary>
        public static Material Lit(Color color, float smoothness = 0.15f, float metallic = 0f)
        {
            int key = Hash("lit", color, smoothness, metallic);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var m = new Material(LitShader) { name = "Lit " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            SetFloatIfPresent(m, "_Smoothness", smoothness);
            SetFloatIfPresent(m, "_Glossiness", smoothness);
            SetFloatIfPresent(m, "_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        /// <summary>Lit surface with an emissive glow, for runes, portals and enemy eyes.</summary>
        public static Material Emissive(Color color, float intensity = 2f)
        {
            int key = Hash("emissive", color, intensity, 0f);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var m = new Material(LitShader) { name = "Emissive " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Flat unlit colour, unaffected by scene lighting.</summary>
        public static Material Unlit(Color color)
        {
            int key = Hash("unlit", color, 0f, 0f);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            var m = new Material(UnlitShader) { name = "Unlit " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            Cache[key] = m;
            return m;
        }

        /// <summary>
        /// Alpha-blended unlit colour for telegraphs, beams and area markers.
        /// Not cached: callers animate the colour per instance.
        /// </summary>
        public static Material Transparent(Color color)
        {
            var m = new Material(TransparentShader) { name = "Transparent" };
            SetColor(m, color);

            // URP Unlit needs to be pushed into transparent mode by hand.
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        public static void SetMaterialColor(Material m, Color c)
        {
            if (m == null) return;
            SetColor(m, c);
        }

        private static int Hash(string tag, Color c, float a, float b)
        {
            unchecked
            {
                int h = tag.GetHashCode();
                h = h * 397 ^ c.GetHashCode();
                h = h * 397 ^ a.GetHashCode();
                h = h * 397 ^ b.GetHashCode();
                return h;
            }
        }
    }

    /// <summary>Shared palette so the whole game reads as one place.</summary>
    public static class Palette
    {
        public static readonly Color Floor = new Color(0.16f, 0.15f, 0.20f);
        public static readonly Color Wall = new Color(0.11f, 0.10f, 0.15f);
        public static readonly Color Trim = new Color(0.35f, 0.30f, 0.48f);
        public static readonly Color Prop = new Color(0.28f, 0.24f, 0.30f);
        public static readonly Color Crate = new Color(0.42f, 0.30f, 0.18f);

        public static readonly Color Arcane = new Color(0.60f, 0.45f, 1.00f);
        public static readonly Color Ice = new Color(0.55f, 0.85f, 1.00f);
        public static readonly Color Fire = new Color(1.00f, 0.48f, 0.15f);
        public static readonly Color Poison = new Color(0.55f, 0.90f, 0.35f);
        public static readonly Color Lightning = new Color(0.75f, 0.80f, 1.00f);

        public static readonly Color EnemyRanged = new Color(0.55f, 0.25f, 0.55f);
        public static readonly Color EnemyMelee = new Color(0.65f, 0.22f, 0.22f);
        public static readonly Color EnemyBeam = new Color(0.25f, 0.45f, 0.70f);
        public static readonly Color EnemyCaster = new Color(0.75f, 0.55f, 0.20f);
        public static readonly Color EnemyBoss = new Color(0.85f, 0.25f, 0.45f);

        public static readonly Color Telegraph = new Color(1.00f, 0.25f, 0.25f);
        public static readonly Color Portal = new Color(0.45f, 0.85f, 1.00f);

        /// <summary>Damage school colours live in the damage registry, so a new school gets one for free.</summary>
        public static Color ForDamageType(DamageType type) => DamageTypes.Tint(type);
    }
}
