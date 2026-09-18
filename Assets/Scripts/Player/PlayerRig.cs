using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Assembles the player from code and holds every component reference the rest of the
    /// game needs. <see cref="Instance"/> is the one global the gameplay code leans on.
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public static PlayerRig Instance { get; private set; }

        public CharacterController Controller;
        public CharacterSheet Sheet;
        public Health Health;
        public Mana Mana;
        public StatusController Status;
        public PlayerMotor Motor;
        public PlayerLook Look;
        public PlayerCombat CombatInput;
        public SpellBook Book;
        public MovementController Movement;
        public Weapon Weapon;
        public Holster Holster;
        public Camera Camera;
        public Transform CameraPivot;

        public MasteryHost Masteries;
        public PlayerConcealment Concealment;
        public PossessionController Possession;
        public RewindRecorder Rewind;
        public PlayerBuffs Buffs;
        public ActionLog Actions;
        public AutoFireDriver AutoFire;

        public AbilityContext SpellContext { get; private set; }

        private bool _menuInputEnabled = true;
        private bool _controlsSuppressed;

        /// <summary>Whether the game state allows input at all: false behind a menu, a choice screen or death.</summary>
        public bool MenuInputEnabled => _menuInputEnabled;

        /// <summary>Whether something has taken the player's own controls away, as possession does.</summary>
        public bool ControlsSuppressed => _controlsSuppressed;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Enables or disables every input surface at once, for menus and death.</summary>
        public void SetInputEnabled(bool enabled)
        {
            _menuInputEnabled = enabled;
            ApplyInput();
            PlayerLook.LockCursor(enabled);
        }

        /// <summary>
        /// Takes the player's own controls away, or gives them back, independently of menus: a pause during
        /// possession must not hand the controls back when it closes.
        /// </summary>
        public void SetControlSuppressed(bool suppressed)
        {
            _controlsSuppressed = suppressed;
            ApplyInput();
        }

        private void ApplyInput()
        {
            bool on = _menuInputEnabled && !_controlsSuppressed;
            if (Motor != null) Motor.InputEnabled = on;
            if (Look != null) Look.InputEnabled = on;
            if (CombatInput != null) CombatInput.InputEnabled = on;
            if (Movement != null) Movement.InputEnabled = on;
        }

        public void FullRestore()
        {
            if (Health != null) Health.Heal(Health.Max);
            if (Mana != null) Mana.Add(Mana.Max);
            if (Motor != null) Motor.RefillDashes();
            if (Holster != null) Holster.RefillAll();
            if (Book != null) Book.ResetCooldowns();
            if (Status != null) Status.ClearAll();
            if (Movement != null) Movement.ResetState();
        }

        /// <summary>
        /// Clears everything a run leaves on the player's own systems: possession, auto-fire, concealment,
        /// buffs, rewind history, the action log, and what the masteries built up. A restart reuses the
        /// player object, so none of it may carry into the next run.
        /// </summary>
        public void ResetRunSystems()
        {
            if (Possession != null) Possession.Cancel();
            if (AutoFire != null) AutoFire.End();
            if (Concealment != null) Concealment.Clear();
            if (Buffs != null) Buffs.Clear();
            if (Rewind != null) Rewind.Clear();
            if (Actions != null) Actions.Clear();
            if (Masteries != null) Masteries.ResetForRun();
        }

        // ---------------------------------------------------------------- construction

        public static PlayerRig Spawn(Vector3 position)
        {
            var root = new GameObject("Player");
            root.transform.position = position;
            root.layer = Layers.Player;

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 55f;
            controller.stepOffset = 0.45f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            var sheet = root.AddComponent<CharacterSheet>();
            var status = root.AddComponent<StatusController>();
            var health = root.AddComponent<Health>();
            health.Team = Team.Player;
            var mana = root.AddComponent<Mana>();

            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            var book = root.AddComponent<SpellBook>();
            var movement = root.AddComponent<MovementController>();
            var combat = root.AddComponent<PlayerCombat>();

            // Camera rig
            var pivot = Build.Empty(root.transform, "CameraPivot", new Vector3(0f, 1.62f, 0f));
            var cameraObject = Build.Empty(pivot.transform, "Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = GameSettings.FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.035f, 0.06f);
            cameraObject.AddComponent<AudioListener>();

            // Weapon rig, parented to the camera so the viewmodel follows the aim
            var weaponHolder = Build.Empty(cameraObject.transform, "WeaponHolder",
                new Vector3(0.26f, -0.20f, 0.30f));
            var weapon = weaponHolder.AddComponent<Weapon>();
            var holster = root.AddComponent<Holster>();
            holster.Weapon = weapon;
            weapon.OwnerTeam = Team.Player;
            weapon.Owner = root;
            weapon.OwnerSheet = sheet;
            weapon.OwnerMana = mana;
            weapon.Look = look;
            weapon.AimOrigin = cameraObject.transform;

            look.Initialise(pivot.transform, camera, motor);

            combat.Weapon = weapon;
            combat.Holster = holster;
            combat.Status = status;
            combat.Book = book;
            combat.Look = look;
            combat.Motor = motor;
            combat.Health = health;
            combat.Mana = mana;
            combat.Sheet = sheet;
            combat.Aim = cameraObject.transform;

            var rig = root.AddComponent<PlayerRig>();
            rig.Controller = controller;
            rig.Sheet = sheet;
            rig.Health = health;
            rig.Mana = mana;
            rig.Status = status;
            rig.Motor = motor;
            rig.Look = look;
            rig.CombatInput = combat;
            rig.Book = book;
            rig.Movement = movement;
            rig.Weapon = weapon;
            rig.Holster = holster;
            rig.Camera = camera;
            rig.CameraPivot = pivot.transform;

            rig.SpellContext = new AbilityContext
            {
                Caster = root,
                Team = Team.Player,
                Sheet = sheet,
                Mana = mana,
                Health = health,
                Status = status,
                Motor = motor,
                Aim = cameraObject.transform,
                Controller = controller
            };
            book.Context = rig.SpellContext;
            book.Weapon = weapon;

            combat.Context = rig.SpellContext;
            movement.Context = rig.SpellContext;
            movement.Motor = motor;
            movement.Mana = mana;
            movement.Health = health;
            movement.Sheet = sheet;
            movement.Book = book;

            // Stats, gun and spells all come from the one loadout definition, which a restart
            // reapplies to this same object.
            StartingLoadout.ApplyTo(rig);

            health.Damaged += (info, amount) => OnPlayerDamaged(rig, info, amount);

            rig.AttachPlayerSystems();
            return rig;
        }

        /// <summary>
        /// Adds and binds the systems spells build on: concealment, buffs, rewind history, the action log,
        /// possession, auto-fire and the masteries. After the loadout, so the masteries rank what it equipped.
        /// Public so tooling can assemble a rig in edit mode, where Awake never runs.
        /// </summary>
        public void AttachPlayerSystems()
        {
            GameObject root = gameObject;

            Concealment = Ensure<PlayerConcealment>(root);
            Concealment.Bind(this);

            Buffs = Ensure<PlayerBuffs>(root);
            Buffs.Bind(this);

            Rewind = Ensure<RewindRecorder>(root);
            Rewind.Bind(this);

            Actions = Ensure<ActionLog>(root);
            Actions.Bind(this);

            Possession = Ensure<PossessionController>(root);
            Possession.Bind(this);

            AutoFire = Ensure<AutoFireDriver>(root);
            AutoFire.Weapon = Weapon;
            AutoFire.Aim = SpellContext != null ? SpellContext.Aim : null;
            AutoFire.Status = Status;

            Masteries = Ensure<MasteryHost>(root);
            Masteries.Initialise(this);
        }

        /// <summary>Drops every static subscription the player systems hold. Edit mode never calls OnDestroy.</summary>
        public void DetachPlayerSystems()
        {
            if (Masteries != null) Masteries.Unbind();
            if (Concealment != null) Concealment.Unbind();
            if (Rewind != null) Rewind.Unbind();
            if (Actions != null) Actions.Unbind();
        }

        private static T Ensure<T>(GameObject root) where T : Component
        {
            T found = root.GetComponent<T>();
            return found != null ? found : root.AddComponent<T>();
        }

        private static void OnPlayerDamaged(PlayerRig rig, DamageInfo info, float amount)
        {
            if (info.Knockback.sqrMagnitude > 0.01f && rig.Motor != null)
                rig.Motor.AddKnockback(info.Knockback, info.Source, info.SourceTeam);
        }
    }
}
