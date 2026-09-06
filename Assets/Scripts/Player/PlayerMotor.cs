using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// First person movement. Quake-style ground friction plus air acceleration, so
    /// strafing and dashing keep momentum. Speed, jump height and dash charges all come
    /// off the character sheet, which is how Agility pays out.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Feel")]
        [SerializeField] private float gravity = -26f;
        [SerializeField] private float groundAcceleration = 90f;
        [SerializeField] private float airAcceleration = 55f;
        [SerializeField] private float friction = 8f;
        [SerializeField] private float stopSpeed = 3f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBuffer = 0.12f;

        [Header("Dash")]
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashRechargeSeconds = 2.6f;
        [SerializeField] private float dashInvulnerability = 0.22f;

        private CharacterController _controller;
        private CharacterSheet _sheet;
        private StatusController _status;
        private Health _health;

        private Vector3 _velocity;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _dashTimer;
        private Vector3 _dashDirection;
        private float _dashRechargeTimer;

        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;
        public bool IsGrounded { get; private set; }
        public bool IsDashing => _dashTimer > 0f;
        public int DashCharges { get; private set; }
        public int MaxDashCharges => _sheet != null ? Mathf.Max(1, _sheet.GetInt(Attr.DashCharges)) : 1;
        public float DashRechargeFraction => Mathf.Clamp01(1f - _dashRechargeTimer / Mathf.Max(0.01f, dashRechargeSeconds));

        /// <summary>Set by cutscene-ish moments (boon screens) to freeze the player in place.</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _sheet = GetComponent<CharacterSheet>();
            _status = GetComponent<StatusController>();
            _health = GetComponent<Health>();
            DashCharges = MaxDashCharges;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            RechargeDashes(dt);

            bool frozen = _status != null && _status.IsControlImpaired;
            Vector2 input = (InputEnabled && !frozen) ? ReadMoveInput() : Vector2.zero;
            Vector3 wishDir = WishDirection(input);

            if (InputEnabled && !frozen)
            {
                if (Input.GetKeyDown(KeyCode.Space)) _jumpBufferTimer = jumpBuffer;
                if (Input.GetKeyDown(KeyCode.LeftShift)) TryDash(wishDir);
            }

            if (_dashTimer > 0f) UpdateDash(dt);
            else UpdateNormalMovement(wishDir, dt);

            _controller.Move(_velocity * dt);
            IsGrounded = _controller.isGrounded;

            if (IsGrounded)
            {
                _coyoteTimer = coyoteTime;
                if (_velocity.y < 0f) _velocity.y = -2f;   // keep the controller pinned to the floor
            }
            else
            {
                _coyoteTimer -= dt;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= dt;
                if (_coyoteTimer > 0f) DoJump();
            }
        }

        private static Vector2 ReadMoveInput()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            return new Vector2(x, y);
        }

        private Vector3 WishDirection(Vector2 input)
        {
            Vector3 dir = transform.right * input.x + transform.forward * input.y;
            dir.y = 0f;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private void UpdateNormalMovement(Vector3 wishDir, float dt)
        {
            float wishSpeed = _sheet != null ? _sheet.Get(Attr.MoveSpeed) : 7f;
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);

            if (IsGrounded)
            {
                horizontal = ApplyFriction(horizontal, dt);
                horizontal = Accelerate(horizontal, wishDir, wishSpeed, groundAcceleration, dt);
            }
            else
            {
                float airControl = _sheet != null ? _sheet.Get(Attr.AirControl) : 0.4f;
                horizontal = Accelerate(horizontal, wishDir, wishSpeed, airAcceleration * airControl, dt);
                _velocity.y += gravity * dt;
            }

            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
        }

        private Vector3 ApplyFriction(Vector3 horizontal, float dt)
        {
            float speed = horizontal.magnitude;
            if (speed < 0.01f) return Vector3.zero;

            float control = Mathf.Max(speed, stopSpeed);
            float drop = control * friction * dt;
            float newSpeed = Mathf.Max(0f, speed - drop);
            return horizontal * (newSpeed / speed);
        }

        private static Vector3 Accelerate(Vector3 horizontal, Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishDir.sqrMagnitude < 0.0001f) return horizontal;

            float currentSpeed = Vector3.Dot(horizontal, wishDir);
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0f) return horizontal;

            float accelSpeed = Mathf.Min(accel * dt * wishSpeed, addSpeed);
            return horizontal + wishDir * accelSpeed;
        }

        private void DoJump()
        {
            float height = _sheet != null ? _sheet.Get(Attr.JumpHeight) : 1.4f;
            _velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * height);
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            IsGrounded = false;
        }

        private void RechargeDashes(float dt)
        {
            int max = MaxDashCharges;
            if (DashCharges > max) DashCharges = max;
            if (DashCharges >= max)
            {
                _dashRechargeTimer = 0f;
                return;
            }

            _dashRechargeTimer -= dt;
            if (_dashRechargeTimer <= 0f)
            {
                DashCharges++;
                _dashRechargeTimer = DashCharges < max ? dashRechargeSeconds : 0f;
            }
        }

        private void TryDash(Vector3 wishDir)
        {
            if (DashCharges <= 0) return;

            Vector3 dir = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
            dir.y = 0f;
            dir.Normalize();

            DashCharges--;
            if (_dashRechargeTimer <= 0f) _dashRechargeTimer = dashRechargeSeconds;

            _dashDirection = dir;
            _dashTimer = dashDuration;

            if (_health != null)
                _health.InvulnerabilityTimer = Mathf.Max(_health.InvulnerabilityTimer, dashInvulnerability);
        }

        private void UpdateDash(float dt)
        {
            _dashTimer -= dt;
            float speed = _sheet != null ? _sheet.Get(Attr.DashSpeed) : 22f;
            _velocity = _dashDirection * speed;
            _velocity.y = 0f;

            if (_dashTimer <= 0f)
            {
                // Bleed out of the dash rather than stopping dead; keeps chained movement fluid.
                _velocity = _dashDirection * Mathf.Max(speed * 0.45f,
                    _sheet != null ? _sheet.Get(Attr.MoveSpeed) : 7f);
            }
        }

        /// <summary>Used by Blink and knockback: hard-set the position, keeping momentum.</summary>
        public void Teleport(Vector3 position, bool preserveVelocity = true)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            if (!preserveVelocity) _velocity = Vector3.zero;
        }

        public void AddImpulse(Vector3 impulse)
        {
            _velocity += impulse;
        }

        public void RefillDashes()
        {
            DashCharges = MaxDashCharges;
            _dashRechargeTimer = 0f;
        }
    }
}
