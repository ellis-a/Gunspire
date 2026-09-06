using UnityEngine;

namespace WizardGun
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
            float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * sensitivity;

            transform.Rotate(Vector3.up, mx, Space.World);
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

            float baseSpeed = 8f;
            float excess = Mathf.Max(0f, _motor.HorizontalSpeed - baseSpeed);
            float target = baseFov + Mathf.Min(maxFovBoost, excess * 0.8f);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, target, fovLerp * Time.deltaTime);
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
