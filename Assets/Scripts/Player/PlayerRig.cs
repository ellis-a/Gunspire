using UnityEngine;

namespace WizardGun
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
        public Weapon Weapon;
        public Camera Camera;
        public Transform CameraPivot;

        public AbilityContext SpellContext { get; private set; }

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
            if (Motor != null) Motor.InputEnabled = enabled;
            if (Look != null) Look.InputEnabled = enabled;
            if (CombatInput != null) CombatInput.InputEnabled = enabled;
            PlayerLook.LockCursor(enabled);
        }

        public void FullRestore()
        {
            if (Health != null) Health.Heal(Health.Max);
            if (Mana != null) Mana.Add(Mana.Max);
            if (Motor != null) Motor.RefillDashes();
            if (Weapon != null) Weapon.RefillMagazine();
            if (Book != null) Book.ResetCooldowns();
            if (Status != null) Status.ClearAll();
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
            var combat = root.AddComponent<PlayerCombat>();

            // Camera rig
            var pivot = Build.Empty(root.transform, "CameraPivot", new Vector3(0f, 1.62f, 0f));
            var cameraObject = Build.Empty(pivot.transform, "Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = 90f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.035f, 0.06f);
            cameraObject.AddComponent<AudioListener>();

            // Weapon rig, parented to the camera so the viewmodel follows the aim
            var weaponHolder = Build.Empty(cameraObject.transform, "WeaponHolder",
                new Vector3(0.26f, -0.20f, 0.30f));
            var weapon = weaponHolder.AddComponent<Weapon>();
            weapon.OwnerTeam = Team.Player;
            weapon.Owner = root;
            weapon.OwnerSheet = sheet;
            weapon.OwnerMana = mana;
            weapon.Look = look;
            weapon.AimOrigin = cameraObject.transform;

            look.Initialise(pivot.transform, camera, motor);

            combat.Weapon = weapon;
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
            rig.Weapon = weapon;
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

            // Stats, gun and spells all come from the one loadout definition, which a restart
            // reapplies to this same object.
            StartingLoadout.ApplyTo(rig);

            health.Damaged += (info, amount) => OnPlayerDamaged(rig, info, amount);

            return rig;
        }

        private static void OnPlayerDamaged(PlayerRig rig, DamageInfo info, float amount)
        {
            if (info.Knockback.sqrMagnitude > 0.01f && rig.Motor != null)
                rig.Motor.AddImpulse(info.Knockback);
        }
    }
}
