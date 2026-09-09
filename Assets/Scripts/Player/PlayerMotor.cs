using UnityEngine;

namespace Gunspire
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

        [Header("Wall zip")]
        [SerializeField] private float zipSpeed = 46f;

        /// <summary>Give up and hand back to normal gravity if nothing is hit by this distance.</summary>
        [SerializeField] private float zipMaxDistance = 70f;

        /// <summary>
        /// How long the player can be off an attached surface with nothing under them at all
        /// before gravity gives up and reverts to normal. Without this, wandering past the edge
        /// of a wall leaves you drifting forever in a direction that used to be down.
        /// </summary>
        [SerializeField] private float attachedFallGrace = 0.5f;

        /// <summary>How far below still counts as being over the surface, jumping included.</summary>
        [SerializeField] private float attachedReach = 12f;

        /// <summary>How fast the view rolls over when a surface becomes the new floor.</summary>
        [SerializeField] private float reorientDegreesPerSecond = 540f;

        /// <summary>Whether Spider Legs is off, mid-flight to a surface, or stuck to one.</summary>
        public enum WallZipState { Off, Zipping, Attached }

        private Vector3 _velocity;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _dashTimer;
        private Vector3 _dashDirection;
        private float _dashRechargeTimer;
        private Vector3 _wishDirection;

        private Vector3 _zipDirection;
        private float _zipTraveled;
        private bool _zipHit;
        private Vector3 _zipHitNormal;
        private float _ungroundedWhileAttached;
        private bool _surfaceContact;

        /// <summary>
        /// Which way is up for gravity, jumping and ground checks. Held separately from
        /// transform.up because the view rolls into place over a few frames while the physics
        /// has to commit the instant the surface is hit - and because the two genuinely differ
        /// during that roll.
        /// </summary>
        private Vector3 _upAxis = Vector3.up;

        public WallZipState ZipState { get; private set; } = WallZipState.Off;

        public Vector3 Velocity => _velocity;

        /// <summary>
        /// Speed across the current surface - FOV stretch, crosshair bloom and the moving-spread
        /// penalty all key off this. Projected against the up axis rather than read off world
        /// X/Z, or sprinting across a wall would report as standing still: most of that velocity
        /// sits in world Y once the surface you are running across is vertical.
        /// </summary>
        public float HorizontalSpeed => Vector3.ProjectOnPlane(_velocity, _upAxis).magnitude;
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

            RollTowardsUpAxis(dt);

            // isGrounded reports the result of the last Move, so it is already this frame's
            // truth. Reading it here rather than after moving means the jump can be resolved
            // before movement runs, which is what lets the hop frame skip friction.
            //
            // On a wall it cannot be used at all. A CharacterController's capsule stays
            // world-upright no matter how its transform is turned, so isGrounded only ever
            // answers "is something directly below me in world Y" - which is false the entire
            // time you are stood on a wall. _surfaceContact is that same question asked about
            // the axis that currently counts, and answered from the collisions the last Move
            // actually reported.
            IsGrounded = ZipState == WallZipState.Attached ? _surfaceContact : _controller.isGrounded;

            if (IsGrounded)
            {
                _coyoteTimer = coyoteTime;
                float vertical = Vector3.Dot(_velocity, _upAxis);
                if (vertical < 0f) _velocity += _upAxis * (-2f - vertical);   // pinned to the surface
            }
            else
            {
                _coyoteTimer -= dt;
            }

            if (ZipState == WallZipState.Attached)
            {
                // Walked off the edge of the surface, rather than jumping off it - nothing to
                // land back on, so let go before the player drifts forever in a direction that
                // used to be down and no longer points at anything.
                //
                // Being airborne cannot be the test on its own: a jump keeps you off the
                // surface for about two thirds of a second, comfortably longer than any grace
                // short enough to be useful. Asking whether the surface is still underneath
                // separates the two cleanly - jumping leaves it below you, walking off the
                // edge does not.
                if (IsGrounded || SurfaceBelow()) _ungroundedWhileAttached = 0f;
                else if ((_ungroundedWhileAttached += dt) > attachedFallGrace) EndWallZip();
            }

            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;

            bool hopping = ZipState != WallZipState.Zipping
                           && _jumpBufferTimer > 0f && _coyoteTimer > 0f && _dashTimer <= 0f;

            if (ZipState == WallZipState.Zipping) UpdateZip(dt);
            else if (_dashTimer > 0f) UpdateDash(dt);
            else UpdateNormalMovement(_wishDirection, dt, skipFriction: hopping);

            if (hopping) DoJump();

            // OnControllerColliderHit fires synchronously inside Move, but reorienting the
            // transform from inside that callback is asking for trouble - the controller has
            // not finished this Move call yet. Recording the hit and acting on it once Move
            // has returned is the same deferral Teleport already relies on elsewhere in this
            // file: mutate the transform between frames, never mid-physics-step.
            _zipHit = false;
            _surfaceContact = false;
            _controller.Move(_velocity * dt);
            if (_zipHit) AttachToWall(_zipHitNormal);
        }

        /// <summary>
        /// Is the surface being stood on still underneath, jump or no jump? Cast from the
        /// capsule centre so it starts clear of the surface itself.
        /// </summary>
        private bool SurfaceBelow()
        {
            Vector3 origin = transform.TransformPoint(_controller.center);
            return Physics.Raycast(origin, -_upAxis, attachedReach,
                Layers.BlockingMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Rolls the view over to stand on the current up axis, a slice at a time.
        ///
        /// Applied as a correcting delta rather than a slerp towards a stored target, because
        /// mouse look is turning this same transform every frame - a fixed target would fight
        /// it and swallow the player's yaw for the length of the transition.
        ///
        /// The capsule never tips, but its centre offset does turn with the transform, so
        /// rotating alone would sweep the collider through an arc a metre wide. Holding that
        /// centre still keeps the roll purely a change of view.
        /// </summary>
        private void RollTowardsUpAxis(float dt)
        {
            Vector3 currentUp = transform.up;
            float error = Vector3.Angle(currentUp, _upAxis);
            if (error < 0.01f) return;

            Vector3 axis = Vector3.Cross(currentUp, _upAxis);

            // Exactly upside down: every perpendicular is an equally correct way round, so
            // take one rather than normalising a zero vector.
            if (axis.sqrMagnitude < 0.000001f) axis = transform.forward;

            float step = Mathf.Min(error, reorientDegreesPerSecond * dt);

            Vector3 pinned = transform.TransformPoint(_controller.center);
            transform.rotation = Quaternion.AngleAxis(step, axis.normalized) * transform.rotation;
            transform.position += pinned - transform.TransformPoint(_controller.center);
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

        /// <summary>
        /// Flattened against the physics up axis, not world Y - zeroing Y here, as this used to,
        /// would be actively wrong once up is a wall normal. The projection is only doing real
        /// work during the roll onto a new surface, when the view has not finished catching up
        /// with the axis movement is already resolved against; the rest of the time the basis
        /// is perpendicular to it already.
        /// </summary>
        private Vector3 WishDirection(Vector2 input)
        {
            Vector3 dir = transform.right * input.x + transform.forward * input.y;
            dir = Vector3.ProjectOnPlane(dir, _upAxis);
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private void UpdateNormalMovement(Vector3 wishDir, float dt, bool skipFriction = false)
        {
            float wishSpeed = _sheet != null ? _sheet.Get(Attr.MoveSpeed) : 7f;

            Vector3 up = _upAxis;
            float vertical = Vector3.Dot(_velocity, up);
            Vector3 horizontal = _velocity - up * vertical;

            if (IsGrounded)
            {
                // A frame spent grounded scrubs about a tenth of your speed, so a chain that
                // touches down and takes off in the same frame must not pay it. This is the
                // difference between a hop chain that builds and one that bleeds out.
                if (!skipFriction) horizontal = ApplyFriction(horizontal, dt);

                horizontal = Accelerate(horizontal, wishDir, wishSpeed, groundAcceleration, dt);
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

                vertical += gravity * dt;
            }

            _velocity = horizontal + up * vertical;
        }

        // ---------------------------------------------------------------- wall zip

        /// <summary>
        /// Launches the player along <paramref name="direction"/> - normally wherever the
        /// camera is looking - at a fixed speed with no steering, like a web pulling them in.
        /// The first surface it hits becomes the new floor; see <see cref="AttachToWall"/>.
        /// </summary>
        public void BeginWallZip(Vector3 direction)
        {
            _zipDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            _zipTraveled = 0f;
            _zipHit = false;
            _dashTimer = 0f;   // a zip overrides an in-flight dash rather than fighting it
            ZipState = WallZipState.Zipping;
            _velocity = Vector3.zero;
        }

        /// <summary>Lets go, whether stuck to a wall or still mid-flight to one.</summary>
        public void EndWallZip()
        {
            if (ZipState == WallZipState.Off) return;

            // Gravity goes back to normal immediately; the view rolls back upright over the
            // next few frames on its own. Velocity is left alone - whatever speed the player
            // had walking on the wall carries into the fall, the same way a dash bleeds out
            // into normal movement rather than stopping dead.
            _upAxis = Vector3.up;
            ZipState = WallZipState.Off;
        }

        private void UpdateZip(float dt)
        {
            _velocity = _zipDirection * zipSpeed;
            _zipTraveled += zipSpeed * dt;

            if (_zipTraveled < zipMaxDistance) return;

            // Sailed past everything without finding a wall. Hand back to gravity with a
            // little of the speed carried over, rather than leaving the player parked in mid-
            // air the instant the line runs out.
            ZipState = WallZipState.Off;
            _velocity = _zipDirection * (_sheet != null ? _sheet.Get(Attr.MoveSpeed) : 7f);
        }

        /// <summary>
        /// Makes the wall's outward normal the new up. Gravity, jumping and the ground check
        /// all read that axis rather than a hardcoded world Y, so from here the wall behaves
        /// like a floor: walking runs across its surface, and jumping arcs away and falls
        /// straight back onto it, because "down" is now into the wall.
        ///
        /// The axis changes at once, the view catches up over the next few frames.
        /// </summary>
        private void AttachToWall(Vector3 wallNormal)
        {
            _upAxis = wallNormal.normalized;

            ZipState = WallZipState.Attached;
            _velocity = Vector3.zero;
            _coyoteTimer = coyoteTime;   // a jump thrown right on impact should not be swallowed
            _jumpBufferTimer = 0f;
            _ungroundedWhileAttached = 0f;

            // Assume contact until the next Move says otherwise. Without this the first frame
            // reads as ungrounded and starts the walked-off-the-edge timer running against a
            // player who has only just landed.
            _surfaceContact = true;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (ZipState == WallZipState.Zipping)
            {
                _zipHit = true;
                _zipHitNormal = hit.normal;
                return;
            }

            // Standing on a wall, "underfoot" means a face pointing the same way the current
            // up does. This is the check the CharacterController cannot make for us, since its
            // own notion of below is welded to world Y.
            if (ZipState == WallZipState.Attached && Vector3.Dot(hit.normal, _upAxis) > 0.5f)
                _surfaceContact = true;
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
            float jumpSpeed = Mathf.Sqrt(2f * Mathf.Abs(gravity) * height);

            // Along the current up axis rather than world Y: off a real floor that is straight
            // up as always, but off an attached wall it is away from the surface, with gravity
            // pulling back along the same axis - the jump-falls-back-to-the-wall part.
            _velocity += _upAxis * (jumpSpeed - Vector3.Dot(_velocity, _upAxis));

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

            // _wishDirection is already flattened against the up axis; transform.forward is a
            // close enough stand-in when there is no input to take a direction from.
            Vector3 dir = (_wishDirection.sqrMagnitude > 0.01f ? _wishDirection : transform.forward).normalized;

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
            _velocity = _dashDirection * speed;   // already flat; see TryDash

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

            if (preserveVelocity) return;

            // A relocation that throws away momentum is a hard reset - loading a room, most of
            // all - so it throws away the gravity direction too. Otherwise, taking the exit
            // while stuck to a wall carries that wall's idea of down into the next floor. The
            // ability itself notices the motor has let go and ends on its next tick.
            _velocity = Vector3.zero;
            EndWallZip();
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
