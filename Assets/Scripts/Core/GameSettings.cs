using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The player's settings, kept in PlayerPrefs between sessions: mouse sensitivity, field of view and volume.
    /// Fullscreen goes straight to Unity, which remembers the window mode itself. The UI size lives on
    /// <see cref="UIStyles.UserScale"/>, beside the scaling it drives.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultSensitivity = 2.2f;
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 8f;
        public const float SensitivityStep = 0.2f;

        public const float DefaultFieldOfView = 90f;
        public const float MinFieldOfView = 70f;
        public const float MaxFieldOfView = 110f;
        public const float FieldOfViewStep = 5f;

        public const float VolumeStep = 0.1f;

        private const string SensitivityKey = "mouse_sensitivity";
        private const string FieldOfViewKey = "field_of_view";
        private const string VolumeKey = "master_volume";

        private static float _sensitivity = -1f;
        private static float _fieldOfView = -1f;
        private static float _volume = -1f;

        /// <summary>Degrees turned per unit of mouse movement.</summary>
        public static float Sensitivity
        {
            get
            {
                if (_sensitivity < 0f) _sensitivity = Load(SensitivityKey, DefaultSensitivity, MinSensitivity, MaxSensitivity);
                return _sensitivity;
            }
            set => _sensitivity = Save(SensitivityKey, value, SensitivityStep, MinSensitivity, MaxSensitivity);
        }

        /// <summary>The unzoomed vertical field of view, before the speed stretch.</summary>
        public static float FieldOfView
        {
            get
            {
                if (_fieldOfView < 0f) _fieldOfView = Load(FieldOfViewKey, DefaultFieldOfView, MinFieldOfView, MaxFieldOfView);
                return _fieldOfView;
            }
            set => _fieldOfView = Save(FieldOfViewKey, value, FieldOfViewStep, MinFieldOfView, MaxFieldOfView);
        }

        /// <summary>Master volume, 0 to 1.</summary>
        public static float Volume
        {
            get
            {
                if (_volume < 0f) _volume = Load(VolumeKey, 1f, 0f, 1f);
                return _volume;
            }
            set
            {
                _volume = Save(VolumeKey, value, VolumeStep, 0f, 1f);
                AudioListener.volume = _volume;
            }
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        /// <summary>Puts the saved settings into effect. Call once at startup; the camera reads its own each frame.</summary>
        public static void Apply() => AudioListener.volume = Volume;

        /// <summary>Closes the game, or leaves play mode in the editor.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static float Load(string key, float fallback, float min, float max)
            => Mathf.Clamp(PlayerPrefs.GetFloat(key, fallback), min, max);

        /// <summary>Rounded to the step so repeated presses never drift, clamped, saved, and returned.</summary>
        private static float Save(string key, float value, float step, float min, float max)
        {
            value = Mathf.Clamp(Mathf.Round(value / step) * step, min, max);
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            return value;
        }
    }
}
