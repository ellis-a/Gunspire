using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Mouse look. Yaw turns the body so movement follows the crosshair; pitch stays on the
    /// camera. Also does the speed-based FOV stretch that sells how fast the player is moving.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private float pitchLimit = 89f;
        [SerializeField] private float baseFov = 90f;
        [SerializeField] private float maxFovBoost = 14f;
        [SerializeField] private float zoomLerp = 16f;

        /// <summary>
        /// Field of view to hold while a gun's focus alt fire is held down. Zero is off.
        /// Driven by <see cref="PlayerCombat"/> rather than read from the weapon here, so the
        /// camera stays unaware of guns.
        /// </summary>
        public float ZoomFov { get; set; }
        [SerializeField] private float fovLerp = 6f;

        private Transform _cameraPivot;
        private Camera _camera;
        private PlayerMotor _motor;
        private float _pitch;
        private float _recoilPitch;
        private float _recoilYaw;
        private float _recoilVelocity;

        public bool InputEnabled { get; set; } = true;
        public Camera Camera => _camera;
        public float Sensitivity { get => sensitivity; set => sensitivity = value; }

        public void Initialise(Transform cameraPivot, Camera cam, PlayerMotor motor)
        {
            _cameraPivot = cameraPivot;
            _camera = cam;
            _motor = motor;
            if (_camera != null) _camera.fieldOfView = baseFov;
        }

        private void Update()
        {
            if (InputEnabled) ReadMouse();
            DecayRecoil();
            ApplyRotation();
            UpdateFov();
        }

        private void ReadMouse()
        {
            float scale = sensitivity * ZoomSensitivityScale();
            float mx = Input.GetAxisRaw("Mouse X") * scale;
            float my = Input.GetAxisRaw("Mouse Y") * scale;

            // The current up axis rather than a hardcoded world one, so turning still happens
            // level relative to whatever surface Spider Legs has the player standing on -
            // world up ordinarily, a wall's outward normal while attached to one.
            transform.Rotate(transform.up, mx, Space.World);
            _pitch = Mathf.Clamp(_pitch - my, -pitchLimit, pitchLimit);
        }

        private void ApplyRotation()
        {
            if (_cameraPivot == null) return;
            _cameraPivot.localRotation = Quaternion.Euler(_pitch + _recoilPitch, _recoilYaw, 0f);
        }

        private void DecayRecoil()
        {
            _recoilPitch = Mathf.SmoothDamp(_recoilPitch, 0f, ref _recoilVelocity, 0.12f);
            _recoilYaw = Mathf.Lerp(_recoilYaw, 0f, 8f * Time.deltaTime);
        }

        private void UpdateFov()
        {
            if (_camera == null || _motor == null) return;

            float target;
            float rate = fovLerp;

            if (ZoomFov > 0f)
            {
                // A zoomed view ignores the speed stretch entirely. Sprinting while scoped
                // should not quietly widen the shot you are lining up.
                target = ZoomFov;
                rate = zoomLerp;
            }
            else
            {
                float baseSpeed = 8f;
                float excess = Mathf.Max(0f, _motor.HorizontalSpeed - baseSpeed);
                target = baseFov + Mathf.Min(maxFovBoost, excess * 0.8f);
            }

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, target, rate * Time.deltaTime);
        }

        /// <summary>
        /// Turning is scaled by how far the view is zoomed in, so the same mouse movement
        /// covers the same distance on screen. Without this a scope makes aiming harder.
        /// </summary>
        private float ZoomSensitivityScale()
        {
            if (ZoomFov <= 0f || _camera == null) return 1f;
            return Mathf.Clamp(_camera.fieldOfView / Mathf.Max(1f, baseFov), 0.35f, 1f);
        }

        /// <summary>Called by weapons on fire.</summary>
        public void AddRecoil(float pitch, float yaw)
        {
            _recoilPitch -= pitch;
            _recoilYaw += yaw;
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
