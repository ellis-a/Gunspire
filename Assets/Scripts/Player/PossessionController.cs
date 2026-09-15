using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>One frame of the player's input, handed to a possessed body.</summary>
    public struct PossessionInput
    {
        /// <summary>Strafe and forward, each from -1 to 1.</summary>
        public Vector2 Move;

        /// <summary>Where the view points: yaw in world degrees, pitch up and down.</summary>
        public float Yaw;
        public float Pitch;

        /// <summary>Up and down for something that flies, from -1 to 1.</summary>
        public float Rise;

        /// <summary>One bit per action whose key went down this frame.</summary>
        public int ActionsPressed;

        /// <summary>One bit per action whose key is held down. The Alpha Stag's charge.</summary>
        public int ActionsHeld;

        public Vector3 AimOrigin;
        public Vector3 AimForward;
    }

    /// <summary>
    /// A body the player can take over: an enemy for Assume Identity, an animal form for Shapeshift. It
    /// changes side and takes orders while possessed, and goes back to what it was afterwards. What
    /// happens to it next is the spell's decision, through <see cref="PossessionController.Ended"/>.
    /// </summary>
    public interface IPossessable
    {
        Transform Body { get; }
        Health Health { get; }
        float EyeHeight { get; }
        bool IsPossessed { get; }

        /// <summary>How many separate actions it has, bound to left click, right click, then Q, E and F.</summary>
        int ActionCount { get; }
        string ActionName(int index);

        void BeginPossession();
        void EndPossession();
        void Drive(in PossessionInput input, float dt);
    }

    public enum PossessionEnd { Expired, BodyDamaged, BodyDied, Cancelled }

    /// <summary>
    /// The player's side of possession, built once for Assume Identity and Shapeshift alike.
    ///
    /// While it lasts, the player's own controls are off, the camera sits at the possessed body's eyes,
    /// and <see cref="TargetRegistry"/> treats that body as the player, so enemies fight it and the flow
    /// field leads to it. The real body is immune and hidden from sight, hearing and targeting.
    ///
    /// It ends after its time, when the body takes a direct hit (a status tick does not count), when the
    /// body dies, or when cancelled. The camera then flies straight home through whatever is in the way,
    /// with the controls still locked and the real body still immune until it arrives.
    /// </summary>
    public class PossessionController : MonoBehaviour
    {
        public static readonly KeyCode[] ActionKeys = { KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Q, KeyCode.E, KeyCode.F };

        public const float ReturnSeconds = 0.6f;
        private const float PitchLimit = 80f;

        public PlayerRig Rig { get; private set; }
        public IPossessable Current { get; private set; }

        public bool IsPossessing => Current != null;
        public bool IsReturning { get; private set; }
        public bool IsBusy => IsPossessing || IsReturning;
        public float TimeLeft { get; private set; }

        /// <summary>Raised when control ends, with the body that was held and why, before the camera sets off home.</summary>
        public event Action<IPossessable, PossessionEnd> Ended;

        private bool _endOnDirectDamage;
        private Health _bodyHealth;
        private Transform _eye;
        private float _yaw;
        private float _pitch;
        private float _returnTime;
        private Vector3 _returnFrom;
        private Quaternion _returnFromRotation;
        private readonly object _concealKey = new object();

        /// <summary>Action keys already down when control began, ignored until they are let go.</summary>
        private int _heldAtBegin;

        public void Bind(PlayerRig rig)
        {
            Rig = rig;

            // A player who dies while in another body is not left watching through its eyes.
            if (rig != null && rig.Health != null) rig.Health.Died += info => { if (this != null) Cancel(); };
        }

        /// <summary>Takes over a body. Refused while already possessing or returning, or if the body is dead.</summary>
        public bool Begin(IPossessable target, float seconds, bool endOnDirectDamage = true)
        {
            if (Rig == null || target == null || IsBusy || Gone(target) || target.IsPossessed) return false;

            Health health = target.Health;
            if (health == null || !health.IsAlive) return false;

            Current = target;
            TimeLeft = seconds;
            _endOnDirectDamage = endOnDirectDamage;
            _bodyHealth = health;

            Rig.SetControlSuppressed(true);
            if (Rig.Concealment != null) Rig.Concealment.Hide(_concealKey, fromSight: true, fromHearing: true, untargetable: true);
            KeepRealBodySafe();

            TargetRegistry.SetPlayerBody(target.Body, health);
            target.BeginPossession();

            health.Damaged += OnBodyDamaged;
            health.Died += OnBodyDied;

            _yaw = target.Body.eulerAngles.y;
            _pitch = 0f;

            // The key that cast the spell is still down this frame. Read as a press, it would fire the body's attack,
            // or end a shapeshift the moment it began.
            _heldAtBegin = 0;
            if (Application.isPlaying)
                for (int i = 0; i < ActionKeys.Length; i++)
                    if (Input.GetKey(ActionKeys[i])) _heldAtBegin |= 1 << i;

            _eye = new GameObject("Possessed Eye").transform;
            _eye.SetParent(target.Body, false);
            _eye.localPosition = Vector3.up * target.EyeHeight;

            MoveCameraTo(_eye);
            SetViewmodelVisible(false);
            return true;
        }

        private void Update()
        {
            if (IsBusy) Tick(Time.deltaTime, ReadInput());
        }

        /// <summary>One frame, on the player's clock, with the given input. Update reads the keyboard; tooling feeds its own.</summary>
        public void Tick(float dt, PossessionInput input)
        {
            if (Rig == null) return;
            KeepRealBodySafe();

            if (IsReturning)
            {
                TickReturn(dt);
                return;
            }

            if (!IsPossessing) return;

            if (Gone(Current))
            {
                End(PossessionEnd.BodyDied);
                return;
            }

            TimeLeft -= dt;
            if (TimeLeft <= 0f)
            {
                End(PossessionEnd.Expired);
                return;
            }

            _yaw = input.Yaw;
            _pitch = Mathf.Clamp(input.Pitch, -PitchLimit, PitchLimit);

            if (_eye != null)
            {
                _eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                input.AimOrigin = _eye.position;
            }

            input.AimForward = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
            Current.Drive(input, dt);
        }

        private PossessionInput ReadInput()
        {
            var input = new PossessionInput { Yaw = _yaw, Pitch = _pitch };
            if (!IsPossessing || !Rig.MenuInputEnabled) return input;

            float sensitivity = Rig.Look != null ? Rig.Look.Sensitivity : 2.2f;
            input.Yaw = _yaw + Input.GetAxisRaw("Mouse X") * sensitivity;
            input.Pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -PitchLimit, PitchLimit);

            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            input.Move = new Vector2(x, y);

            if (Input.GetKey(KeyCode.Space)) input.Rise += 1f;
            if (Input.GetKey(KeyCode.LeftControl)) input.Rise -= 1f;

            for (int i = 0; i < ActionKeys.Length; i++)
            {
                int bit = 1 << i;
                bool held = Input.GetKey(ActionKeys[i]);

                if ((_heldAtBegin & bit) != 0)
                {
                    if (held) continue;
                    _heldAtBegin &= ~bit;
                }

                if (Input.GetKeyDown(ActionKeys[i])) input.ActionsPressed |= bit;
                if (held) input.ActionsHeld |= bit;
            }

            return input;
        }

        private void KeepRealBodySafe()
        {
            if (IsBusy && Rig.Health != null)
                Rig.Health.InvulnerabilityTimer = Mathf.Max(Rig.Health.InvulnerabilityTimer, 0.25f);
        }

        private void OnBodyDamaged(DamageInfo info, float amount)
        {
            // Only a direct hit: the burn you had already put on it must not end control on its first tick.
            if (IsPossessing && _endOnDirectDamage && info.Origin != DamageOrigin.StatusTick) End(PossessionEnd.BodyDamaged);
        }

        private void OnBodyDied(DamageInfo info)
        {
            if (IsPossessing) End(PossessionEnd.BodyDied);
        }

        public void End(PossessionEnd reason)
        {
            if (!IsPossessing) return;

            IPossessable body = Current;
            Current = null;

            if (_bodyHealth != null)
            {
                _bodyHealth.Damaged -= OnBodyDamaged;
                _bodyHealth.Died -= OnBodyDied;
                _bodyHealth = null;
            }

            if (!Gone(body)) body.EndPossession();
            TargetRegistry.SetPlayerBody(null, null);

            // Unparented before the eye goes, so the camera is never destroyed along with the body it was in.
            Camera camera = Rig.Camera;
            if (camera != null)
            {
                _returnFrom = camera.transform.position;
                _returnFromRotation = camera.transform.rotation;
                camera.transform.SetParent(null, true);
            }

            DestroyEye();
            _returnTime = 0f;
            IsReturning = true;

            Ended?.Invoke(body, reason);

            if (reason == PossessionEnd.Cancelled) FinishReturn();
        }

        /// <summary>Ends at once, camera and all, as a room change needs.</summary>
        public void Cancel()
        {
            if (IsPossessing) End(PossessionEnd.Cancelled);
            else if (IsReturning) FinishReturn();
        }

        private void TickReturn(float dt)
        {
            _returnTime += dt;
            float t = Mathf.Clamp01(_returnTime / ReturnSeconds);
            float eased = t * t * (3f - 2f * t);

            Camera camera = Rig.Camera;
            Transform home = Rig.CameraPivot;
            if (camera != null && home != null)
            {
                camera.transform.position = Vector3.Lerp(_returnFrom, home.position, eased);
                camera.transform.rotation = Quaternion.Slerp(_returnFromRotation, home.rotation, eased);
            }

            if (t >= 1f) FinishReturn();
        }

        private void FinishReturn()
        {
            IsReturning = false;

            if (Rig.CameraPivot != null) MoveCameraTo(Rig.CameraPivot);
            SetViewmodelVisible(true);

            if (Rig.Concealment != null) Rig.Concealment.Release(_concealKey);
            Rig.SetControlSuppressed(false);
        }

        private void MoveCameraTo(Transform parent)
        {
            Camera camera = Rig.Camera;
            if (camera == null) return;

            camera.transform.SetParent(parent, false);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
        }

        private void SetViewmodelVisible(bool visible)
        {
            if (Rig.Weapon != null) Rig.Weapon.gameObject.SetActive(visible);
        }

        private void DestroyEye()
        {
            if (_eye == null) return;

            GameObject eye = _eye.gameObject;
            _eye = null;

            if (Application.isPlaying) Destroy(eye);
            else DestroyImmediate(eye);
        }

        /// <summary>A body whose component has been destroyed, which a plain null check on the interface misses.</summary>
        private static bool Gone(IPossessable body) => body == null || (body is UnityEngine.Object unityObject && unityObject == null);
    }
}
