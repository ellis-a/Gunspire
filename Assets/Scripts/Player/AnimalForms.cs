using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>Shapeshift's three forms. Stored as integers on spell assets: append only.</summary>
    public enum AnimalKind { DeathCobra, AlphaStag, TyrantLizard }

    /// <summary>
    /// The body the player drives while shapeshifted, through the same possession machinery as Assume Identity.
    ///
    /// It is on the player's side and the body layer for it, so enemies fight it as the player. Damage it takes
    /// drains the player's own health, which passes invulnerability by design: the real body is hidden and
    /// carried along underneath the form, so the player comes back wherever the animal stood.
    ///
    /// Left click and right click are its two actions. Any spell key ends the form: casting it again.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AnimalForm : MonoBehaviour, IPossessable
    {
        private const float Gravity = -22f;
        private const float Acceleration = 30f;
        private const float StagChargeInterval = 0.25f;

        private struct FormData
        {
            public string Name;
            public float Speed;
            public float Height;
            public float Radius;
            public float Width;
            public Color Color;
            public string[] Actions;
            public float[] Cooldowns;
        }

        public AnimalKind Kind { get; private set; }
        public PlayerRig Rig { get; private set; }

        public Transform Body => transform;
        public Health Health { get; private set; }
        public float EyeHeight { get; private set; }
        public bool IsPossessed { get; private set; }

        public int ActionCount => 2;
        public string ActionName(int index) => index >= 0 && index < 2 ? _data.Actions[index] : null;

        /// <summary>How many times each action has gone off, for tooling.</summary>
        public int[] Uses { get; } = new int[2];

        private FormData _data;
        private CharacterController _controller;
        private AbilityContext _ctx;
        private Transform _mouth;
        private Vector3 _velocity;
        private readonly float[] _cooldowns = new float[2];
        private float _chargeTimer;
        private float _yaw;
        private float _pitch;

        private static FormData DataFor(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.DeathCobra:
                    return new FormData
                    {
                        Name = "Death Cobra", Speed = 8f, Height = 1f, Radius = 0.4f, Width = 0.6f,
                        Color = new Color(0.2f, 0.35f, 0.2f), Actions = new[] { "Spit Venom", "Leaping Bite" },
                        Cooldowns = new[] { 0.6f, 2.5f }
                    };
                case AnimalKind.AlphaStag:
                    return new FormData
                    {
                        Name = "Alpha Stag", Speed = 9f, Height = 1.9f, Radius = 0.5f, Width = 0.8f,
                        Color = new Color(0.55f, 0.4f, 0.25f), Actions = new[] { "Charge", "Hoof Bash" },
                        Cooldowns = new[] { 0f, 1.2f }
                    };
                default:
                    return new FormData
                    {
                        Name = "Tyrant Lizard", Speed = 7f, Height = 2.4f, Radius = 0.6f, Width = 1.2f,
                        Color = new Color(0.35f, 0.4f, 0.3f), Actions = new[] { "Bite", "Swipe" },
                        Cooldowns = new[] { 0.8f, 1.4f }
                    };
            }
        }

        public static AnimalForm Spawn(AnimalKind kind, PlayerRig rig)
        {
            FormData data = DataFor(kind);

            var root = new GameObject(data.Name);
            root.transform.SetPositionAndRotation(rig.transform.position, Quaternion.Euler(0f, rig.transform.eulerAngles.y, 0f));

            var controller = root.AddComponent<CharacterController>();
            controller.height = data.Height;
            controller.radius = data.Radius;
            controller.center = new Vector3(0f, data.Height * 0.5f, 0f);
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;

            root.AddComponent<CharacterSheet>();
            var status = root.AddComponent<StatusController>();

            var health = root.AddComponent<Health>();
            health.Team = Team.Player;
            health.DestroyOnDeath = false;
            health.ConfigureMaxHealth(1000000f);

            Material body = MaterialLibrary.Lit(data.Color, 0.25f);
            Build.Cube(root.transform, "Body", new Vector3(0f, data.Height * 0.45f, 0f),
                new Vector3(data.Width, data.Height * 0.6f, data.Width * 1.8f), body, collider: false);
            Build.Cube(root.transform, "Head", new Vector3(0f, data.Height * 0.8f, data.Width * 1.1f),
                new Vector3(data.Width * 0.6f, data.Width * 0.5f, data.Width * 0.8f), body, collider: false);
            if (kind == AnimalKind.AlphaStag)
                Build.Cube(root.transform, "Antlers", new Vector3(0f, data.Height * 1.05f, data.Width * 1.1f),
                    new Vector3(data.Width * 1.4f, 0.1f, 0.1f), MaterialLibrary.Lit(new Color(0.9f, 0.85f, 0.7f)), collider: false);

            var mouth = Build.Empty(root.transform, "Mouth", new Vector3(0f, data.Height * 0.8f, data.Width * 1.5f)).transform;
            Layers.SetRecursively(root, Layers.BodyLayerFor(Team.Player));

            var form = root.AddComponent<AnimalForm>();
            form.Kind = kind;
            form.Rig = rig;
            form._data = data;
            form._controller = controller;
            form._mouth = mouth;
            form.Health = health;
            form.EyeHeight = data.Height * 0.9f;
            form._yaw = root.transform.eulerAngles.y;
            form._ctx = new AbilityContext
            {
                Caster = root, Team = Team.Player, Sheet = rig.Sheet, Health = health, Status = status,
                Aim = mouth, Controller = controller
            };

            health.Damaged += (info, amount) =>
            {
                if (form.Rig != null && form.Rig.Health != null && form.Rig.Health.IsAlive) form.Rig.Health.Drain(amount);
            };

            return form;
        }

        public void BeginPossession()
        {
            IsPossessed = true;
            Rig.Motor.BeginKinematic();
            if (Rig.Possession != null) Rig.Possession.Ended += OnPossessionEnded;
        }

        public void EndPossession()
        {
            IsPossessed = false;
            Rig.Motor.MoveKinematic(transform.position);
            Rig.Motor.EndKinematic();
            Rig.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        /// <summary>Removed once possession has taken the camera back out of it, never before.</summary>
        private void OnPossessionEnded(IPossessable body, PossessionEnd reason)
        {
            if (!ReferenceEquals(body, this)) return;
            if (Rig != null && Rig.Possession != null) Rig.Possession.Ended -= OnPossessionEnded;

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        public void Drive(in PossessionInput input, float dt)
        {
            if (!IsPossessed) return;

            // The form has two actions, so the spell keys are free to mean "cast it again".
            if ((input.ActionsPressed & ~0b11) != 0 && Rig.Possession != null)
            {
                Rig.Possession.End(PossessionEnd.Cancelled);
                return;
            }

            _yaw = input.Yaw;
            _pitch = input.Pitch;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _mouth.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            for (int i = 0; i < 2; i++) _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);

            bool charging = Kind == AnimalKind.AlphaStag && (input.ActionsHeld & 1) != 0;

            Vector3 desired = charging
                ? transform.forward * 2.5f
                : Vector3.ClampMagnitude(transform.forward * input.Move.y + transform.right * input.Move.x, 1f);

            Vector3 horizontal = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z), desired * _data.Speed, Acceleration * dt);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
            _velocity.y = _controller.isGrounded && _velocity.y < 0f ? -2f : _velocity.y + Gravity * dt;
            _controller.Move(_velocity * dt);

            Rig.Motor.MoveKinematic(transform.position);

            if (charging)
            {
                if ((_chargeTimer -= dt) <= 0f)
                {
                    _chargeTimer = StagChargeInterval;
                    Act(0);
                }
            }
            else
            {
                if ((input.ActionsPressed & 1) != 0) Act(0);
                if ((input.ActionsPressed & 2) != 0) Act(1);
            }
        }

        /// <summary>Performs one action if it is off cooldown. Public so tooling can act without input.</summary>
        public bool Act(int action)
        {
            if (action < 0 || action > 1 || _cooldowns[action] > 0f) return false;
            _cooldowns[action] = _data.Cooldowns[action];

            DamageType type = Kind == AnimalKind.DeathCobra && action == 0 ? DamageType.Necrotic : DamageType.Kinetic;
            _ctx.Begin(type, SpellType.Attack, _data.Color, 1, 1f, isSpell: true);
            _ctx.DamageOrigin = DamageOrigin.Melee;

            // Strikes come from the front of the body at an enemy's chest height, aimed flat: a tall lizard's
            // mouth is so far up that a cone from it passes over anything standing at its snout. Spit still aims.
            if (!(Kind == AnimalKind.DeathCobra && action == 0))
            {
                _ctx.Origin = transform.position + Vector3.up * Mathf.Min(0.9f, _data.Height * 0.5f) + transform.forward * _data.Radius;
                _ctx.Forward = transform.forward;
            }

            List<AbilityEffect> chain = ChainFor(action);
            if (Kind == AnimalKind.DeathCobra && action == 1) _velocity += transform.forward * 14f + Vector3.up * 4f;

            AbilityRunner.Run(chain, _ctx);
            _ctx.EndCast();
            Uses[action]++;
            return true;
        }

        private List<AbilityEffect> ChainFor(int action)
        {
            switch (Kind)
            {
                case AnimalKind.DeathCobra:
                    return action == 0
                        ? new List<AbilityEffect>
                        {
                            new StatusPayloadEffect { Status = StatusId.Poison, Duration = 6f, Stacks = 3, Magnitude = 1.4f },
                            new SpawnProjectileEffect { Damage = 14f, Speed = 45f, Radius = 0.18f, Lifetime = 2f }
                        }
                        : new List<AbilityEffect>
                        {
                            new StatusPayloadEffect { Status = StatusId.Bleed, Duration = 999f, Stacks = 2, Magnitude = 4f },
                            new SelectConeEffect { Range = 3.5f, HalfAngle = 50f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 26f }
                        };

                case AnimalKind.AlphaStag:
                    return action == 0
                        ? new List<AbilityEffect>
                        {
                            new SelectConeEffect { Range = 2.5f, HalfAngle = 60f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 8f, Knockback = 12f, OnlyOncePerCast = true }
                        }
                        : new List<AbilityEffect>
                        {
                            new SelectConeEffect { Range = 3f, HalfAngle = 55f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 30f, Knockback = 9f }
                        };

                default:
                    return action == 0
                        ? new List<AbilityEffect>
                        {
                            new SelectConeEffect { Range = 3f, HalfAngle = 35f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 42f }
                        }
                        : new List<AbilityEffect>
                        {
                            new SelectConeEffect { Range = 3.5f, HalfAngle = 80f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 24f, Knockback = 8f }
                        };
            }
        }
    }
}
