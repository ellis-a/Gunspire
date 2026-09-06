using UnityEngine;
using UnityEngine.Rendering;

namespace WizardGun
{
    /// <summary>
    /// Builds the whole game at runtime. Drop this on an object in any scene, or just press
    /// Play in an empty scene and it will bootstrap itself. There are no prefabs or scene
    /// assets to keep in sync.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool takeOverScene = true;

        /// <summary>
        /// Spawns a bootstrap object if the loaded scene has none, so an empty scene is
        /// enough to play. Set <c>AutoBootEnabled</c> to false if you would rather place it
        /// yourself.
        /// </summary>
        public static bool AutoBootEnabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (!AutoBootEnabled) return;
            if (FindObjectOfType<GameBootstrap>() != null) return;
            if (FindObjectOfType<GameDirector>() != null) return;

            var go = new GameObject("[Wizard with a Gun]");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (takeOverScene) ClearDefaultSceneObjects();

            ConfigureRendering();
            ConfigureQuality();
            CreateKeyLight();

            var director = new GameObject("GameDirector");
            director.transform.SetParent(transform, false);
            director.AddComponent<GameDirector>();
            director.AddComponent<HudUI>();
            director.AddComponent<ScreenUI>();
        }

        /// <summary>
        /// A fresh Unity scene ships with a camera and a light. The player rig brings its own,
        /// so the stock ones are removed to avoid duplicate audio listeners and doubled lighting.
        /// </summary>
        private static void ClearDefaultSceneObjects()
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].GetComponentInParent<PlayerRig>() != null) continue;
                Destroy(cameras[i].gameObject);
            }

            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].GetComponentInParent<PlayerRig>() != null) continue;
                Destroy(listeners[i]);
            }
        }

        private static void ConfigureRendering()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.10f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.08f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.09f);
            RenderSettings.fogDensity = 0.012f;
        }

        private static void ConfigureQuality()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadowDistance = 60f;
        }

        private void CreateKeyLight()
        {
            var go = new GameObject("KeyLight");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(52f, 34f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.72f, 0.74f, 0.95f);
            light.intensity = 0.75f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
        }
    }
}
