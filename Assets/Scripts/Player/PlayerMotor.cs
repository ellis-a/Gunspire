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

        /// <summary>
        /// Ceiling on the wish speed used in the air, in metres per second. This is the strafe
        /// jumping dial: it caps how much speed one frame of perfect steering can add, so
        /// raising it makes chains build faster and lowering it makes them demand tighter
        /// mouse control. It has no effect on how sharply you can steer, only on how much
        /// speed steering can create.
        /// </summary>
        [SerializeField] private float airWishSpeed = 1.1f;
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

        [Header("Wall cling")]
        [SerializeField] private float wallSlideSpeed = 1.5f;
        [SerializeField] private float wallContactMemory = 0.15f;

        private Vector3 _velocity;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _dashTimer;
        private Vector3 _dashDirection;
        private float _dashRechargeTimer;
        private Vector3 _wishDirection;
        private Vector3 _wallNormal;
        private float _wallContactTime = -99f;

        /// <summary>Set by Spider Legs. Turns walls into surfaces you can hold on to and run along.</summary>
        public bool WallClingEnabled { get; set; }

        /// <summary>True while a wall is close enough to cling to, and the ability allows it.</summary>
        public bool IsWallClinging => WallClingEnabled && !IsGrounded &&
                                      Time.time - _wallContactTime < wallContactMemory;

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
            _wishDirection = WishDirection(input);

            // Held, not pressed. Chaining hops by re-tapping on the exact landing frame is a
            // reflex test rather than a skill, and it is the one thing that has to be
            // effortless for a speed chain to be about steering.
            if (InputEnabled && !frozen && Input.GetKey(KeyCode.Space))
                _jumpBufferTimer = jumpBuffer;

            // isGrounded reports the result of the last Move, so it is already this frame's
            // truth. Reading it here rather than after moving means the jump can be resolved
            // before movement runs, which is what lets the hop frame skip friction.
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

            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;

            bool hopping = _jumpBufferTimer > 0f && _coyoteTimer > 0f && _dashTimer <= 0f;

            if (_dashTimer > 0f) UpdateDash(dt);
            else UpdateNormalMovement(_wishDirection, dt, skipFriction: hopping);

            if (hopping) DoJump();

            _controller.Move(_velocity * dt);
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

        private void UpdateNormalMovement(Vector3 wishDir, float dt, bool skipFriction = false)
        {
            float wishSpeed = _sheet != null ? _sheet.Get(Attr.MoveSpeed) : 7f;
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);

            if (IsGrounded)
            {
                // A frame spent grounded scrubs about a tenth of your speed, so a chain that
                // touches down and takes off in the same frame must not pay it. This is the
                // difference between a hop chain that builds and one that bleeds out.
                if (!skipFriction) horizontal = ApplyFriction(horizontal, dt);

                horizontal = Accelerate(horizontal, wishDir, wishSpeed, groundAcceleration, dt);
            }
            else if (IsWallClinging)
            {
                // On a wall the climber keeps near-full control and only slides, rather than
                // falling. Movement is projected along the surface, so you run across it.
                Vector3 along = Vector3.ProjectOnPlane(wishDir, _wallNormal);
                horizontal = Accelerate(horizontal, along.normalized, wishSpeed,
                    groundAcceleration * 0.6f, dt);

                _velocity.y = Mathf.Max(_velocity.y + gravity * 0.15f * dt, -wallSlideSpeed);
                _coyoteTimer = coyoteTime;   // so the wall can be kicked off
            }
            else
            {
                float airControl = _sheet != null ? _sheet.Get(Attr.AirControl) : 0.4f;

                // The clamp is the whole mechanic, not the acceleration.
                //
                // Accelerate measures how much room is left along the wish direction, so a
                // wish direction perpendicular to your motion always has room and always
                // gains - which is why turning while strafing builds speed. Clamping the wish
                // speed is what keeps that gain small enough to be a technique. Without it the
                // same formula hands out most of a ground stop-and-turn in mid-air, and
                // strafing does nothing special because you already had total control.
                float airWish = Mathf.Min(wishSpeed, airWishSpeed);
                horizontal = Accelerate(horizontal, wishDir, airWish, airAcceleration * airControl, dt);

                _velocity.y += gravity * dt;
            }

            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
        }

        /// <summary>
        /// Remembers the last near-vertical surface touched. Cheaper and more reliable than
        /// probing for walls every frame, since the controller already reports its collisions.
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) > 0.4f) return;   // floor or ceiling, not a wall

            _wallNormal = hit.normal;
            _wallContactTime = Time.time;
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

        /// <summary>
        /// Quake acceleration. Adds speed along the wish direction only up to the room left
        /// between the wish speed and how fast you are already going *in that direction* -
        /// which is why a wish direction across your motion always has room, and is the reason
        /// strafe jumping exists at all.
        ///
        /// Internal rather than private so the movement maths can be checked outside Unity.
        /// It is a pure function and the behaviour it encodes is the one thing here worth
        /// proving; everything around it needs a play session to judge.
        /// </summary>
        internal static Vector3 Accelerate(Vector3 horizontal, Vector3 wishDir, float wishSpeed, float accel, float dt)
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

        /// <summary>
        /// Fires a dash in the direction being held, or straight ahead when standing still.
        /// Returns false with no charges left, which lets the ability refund itself.
        /// </summary>
        public bool TryDash()
        {
            if (DashCharges <= 0) return false;

            Vector3 dir = _wishDirection.sqrMagnitude > 0.01f
                ? _wishDirection.normalized
                : transform.forward;
            dir.y = 0f;
            dir.Normalize();

            DashCharges--;
            if (_dashRechargeTimer <= 0f) _dashRechargeTimer = dashRechargeSeconds;

            _dashDirection = dir;
            _dashTimer = dashDuration;

            if (_health != null)
                _health.InvulnerabilityTimer = Mathf.Max(_health.InvulnerabilityTimer, dashInvulnerability);

            return true;
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
