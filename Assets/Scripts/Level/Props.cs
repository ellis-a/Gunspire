using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Breakable scenery. Reinforced pieces need enough Strength behind the blow, which is
    /// what makes a melee bash from a strong wizard worth having.
    /// </summary>
    public class Smashable : MonoBehaviour, IDamageable
    {
        public float Hardness = 0f;        // required SmashPower; 0 means any damage works
        public float MaxHealth = 30f;
        public Color BodyColor = Palette.Crate;
        public bool DropsReward = true;

        private float _health;
        private bool _broken;

        public bool IsAlive => !_broken;
        public Team Team => Team.Neutral;
        public Transform Transform => transform;

        private void Awake() => _health = MaxHealth;

        public void TakeDamage(in DamageInfo info)
        {
            if (_broken) return;

            if (Hardness > 0f && info.SmashPower < Hardness)
            {
                // Not strong enough: it rings and holds.
                Combat.SpawnImpact(info.HitPoint, info.HitNormal, new Color(1f, 0.9f, 0.6f), 0.2f);
                return;
            }

            _health -= Mathf.Max(1f, info.Amount);
            if (_health <= 0f) Break(info);
            else Combat.SpawnImpact(info.HitPoint, info.HitNormal, BodyColor, 0.25f);
        }

        private void Break(DamageInfo info)
        {
            _broken = true;

            for (int i = 0; i < 8; i++)
            {
                GameObject shard = Build.Cube(null, "Splinter",
                    transform.position + Random.insideUnitSphere * 0.4f,
                    Vector3.one * Random.Range(0.08f, 0.22f),
                    MaterialLibrary.Lit(BodyColor), collider: false);
                shard.transform.rotation = Random.rotation;
                FadeAndDie.Attach(shard, 0.5f, BodyColor);
            }

            if (DropsReward) DropReward();
            Destroy(gameObject);
        }

        private void DropReward()
        {
            Vector3 at = transform.position + Vector3.up * 0.6f;
            if (Random.value < 0.55f) OrbPickup.SpawnHealth(at, 18f);
            else OrbPickup.SpawnMana(at, 30f);
        }

        /// <summary>Prompt shown by the HUD when the object needs more Strength than the player has.</summary>
        public string RequirementText => Hardness > 0f
            ? "Reinforced - Strength " + Hardness.ToString("0") + " to smash"
            : null;
    }

    /// <summary>A floating orb that tops the player up on contact.</summary>
    public class OrbPickup : MonoBehaviour
    {
        public bool IsMana;
        public float Amount = 20f;

        private Vector3 _home;
        private float _phase;

        public static OrbPickup SpawnHealth(Vector3 position, float amount)
            => Spawn(position, amount, false, new Color(1f, 0.35f, 0.4f));

        public static OrbPickup SpawnMana(Vector3 position, float amount)
            => Spawn(position, amount, true, new Color(0.45f, 0.6f, 1f));

        private static OrbPickup Spawn(Vector3 position, float amount, bool mana, Color color)
        {
            GameObject go = Build.Sphere(null, mana ? "ManaOrb" : "HealthOrb", position, 0.45f,
                MaterialLibrary.Emissive(color, 3f), collider: true);
            go.layer = Layers.Prop;

            var collider = go.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.4f;

            var orb = go.AddComponent<OrbPickup>();
            orb.IsMana = mana;
            orb.Amount = amount;
            orb._home = position;
            orb._phase = Random.Range(0f, 10f);
            return orb;
        }

        private void Update()
        {
            _phase += Time.deltaTime;
            transform.position = _home + Vector3.up * (Mathf.Sin(_phase * 2.2f) * 0.16f);
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerRig rig = other.GetComponentInParent<PlayerRig>();
            if (rig == null) return;

            if (IsMana) rig.Mana.Add(Amount);
            else if (rig.Health.Heal(Amount) <= 0f && rig.Health.Fraction >= 1f) return;

            var color = IsMana ? new Color(0.45f, 0.6f, 1f) : new Color(1f, 0.35f, 0.4f);
            GameObject pop = Build.Sphere(null, "OrbPop", transform.position, 0.8f,
                MaterialLibrary.Transparent(new Color(color.r, color.g, color.b, 0.5f)), collider: false);
            FadeAndDie.Attach(pop, 0.2f, new Color(color.r, color.g, color.b, 0.5f), Vector3.one * 3f);

            Destroy(gameObject);
        }
    }

    /// <summary>A gun on a pedestal. Interacting swaps the player weapon for this one.</summary>
    /// <summary>
    /// A gun on a plinth. Taking it puts the one you were holding in its place rather than
    /// consuming the pedestal, so you can try a gun on the enemies in the room and swap back
    /// if you do not like it. Leaving the room destroys the pedestal, which is what makes the
    /// decision final - you commit at the door, not at the plinth.
    /// </summary>
    public class WeaponPickup : MonoBehaviour, IInteractable
    {
        public WeaponDefinition Definition;

        /// <summary>Rounds in the gun sitting here. Below zero means a full magazine.</summary>
        public int Ammo = -1;

        private Renderer _ring;
        private Renderer _model;

        public string Prompt
        {
            get
            {
                if (Definition == null) return null;

                string held = HeldName();
                string verb = string.IsNullOrEmpty(held) ? "Take " : "Swap for ";

                string text = verb + Definition.DisplayName
                              + "  [" + Rarities.Name(Definition.Rarity) + "]  -  "
                              + Definition.StatLine() + "  |  "
                              + Definition.AltLine(RunState.Current != null ? RunState.Current.AltFireTier : 0);

                // Naming what you would be putting down is the whole point of a swap.
                if (!string.IsNullOrEmpty(held)) text += "   (leaves your " + held + " here)";
                return text;
            }
        }

        private static string HeldName()
        {
            PlayerRig rig = PlayerRig.Instance;
            WeaponDefinition held = rig != null && rig.Weapon != null ? rig.Weapon.Definition : null;
            return held != null ? held.DisplayName : null;
        }

        public bool CanInteract(GameObject interactor) => Definition != null;

        public void Interact(GameObject interactor)
        {
            var rig = interactor.GetComponentInParent<PlayerRig>();
            if (rig == null || Definition == null) return;

            string taken = Definition.DisplayName;
            WeaponDefinition given = rig.CombatInput.SwapWeapon(Definition, Ammo, out int givenAmmo);

            if (given == null)
            {
                // Nothing to trade back, so behave the way a plain pickup always did.
                GameDirector.Instance?.Notify("Equipped " + taken);
                Destroy(gameObject);
                return;
            }

            Definition = given;
            Ammo = givenAmmo;
            Refresh();

            GameDirector.Instance?.Notify("Equipped " + taken + " - your " + given.DisplayName
                                          + " is on the plinth", 2.5f);
        }

        /// <summary>Repaints the plinth for whatever is now sitting on it.</summary>
        private void Refresh()
        {
            if (Definition == null) return;

            if (_ring != null)
                _ring.sharedMaterial = MaterialLibrary.Emissive(Rarities.Tint(Definition.Rarity), 3f);

            if (_model != null)
                _model.sharedMaterial = MaterialLibrary.Emissive(Definition.Tint, 2.5f);
        }

        public static WeaponPickup Spawn(Vector3 position, WeaponDefinition definition)
        {
            var root = new GameObject("WeaponPickup");
            root.transform.position = position;
            root.layer = Layers.Prop;

            Build.Cylinder(root.transform, "Pedestal", new Vector3(0f, 0.35f, 0f),
                new Vector3(0.9f, 0.35f, 0.9f), MaterialLibrary.Lit(Palette.Trim), collider: true);

            // A ring in the rarity colour, so how good the drop is reads from across the room.
            GameObject ring = Build.GroundDisc(root.transform, "RarityRing", new Vector3(0f, 0.72f, 0f), 0.62f,
                MaterialLibrary.Emissive(Rarities.Tint(definition.Rarity), 3f));

            GameObject model = Build.Cube(root.transform, "Gun", new Vector3(0f, 1.1f, 0f),
                new Vector3(0.14f, 0.14f, 0.7f), MaterialLibrary.Emissive(definition.Tint, 2.5f), collider: false);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;
            trigger.center = Vector3.up;

            var pickup = root.AddComponent<WeaponPickup>();
            pickup.Definition = definition;

            // Held so the plinth can repaint itself when a swap puts a different gun on it.
            pickup._ring = ring.GetComponent<Renderer>();
            pickup._model = model.GetComponent<Renderer>();

            root.AddComponent<Bobber>();
            return pickup;
        }
    }

    /// <summary>A rune pedestal that teaches a spell. Interacting opens the slot binding screen.</summary>
    public class SpellPedestal : MonoBehaviour, IInteractable
    {
        public Spell Spell;

        public string Prompt
        {
            get
            {
                if (Spell == null) return null;

                PlayerRig rig = PlayerRig.Instance;
                int level = rig != null && rig.Book != null ? rig.Book.GetLevel(Spell) : 0;

                if (level <= 0)
                    return "Learn " + Spell.DisplayName + "  [" + Rarities.Name(Spell.Rarity) + "]  -  "
                           + Spell.Type + " / " + DamageTypes.Name(Spell.DamageType);

                return "Study " + Spell.DisplayName + "  -  " + Spell.LevelUpSummary(level);
            }
        }

        public bool CanInteract(GameObject interactor) => Spell != null;

        public void Interact(GameObject interactor)
        {
            if (Spell == null) return;
            GameDirector.Instance?.OfferSpellBinding(Spell);
            Destroy(gameObject);
        }

        public static SpellPedestal Spawn(Vector3 position, Spell spell)
        {
            var root = new GameObject("SpellPedestal");
            root.transform.position = position;
            root.layer = Layers.Prop;

            Build.Cylinder(root.transform, "Pedestal", new Vector3(0f, 0.4f, 0f),
                new Vector3(1f, 0.4f, 1f), MaterialLibrary.Lit(Palette.Trim), collider: true);

            Build.GroundDisc(root.transform, "RarityRing", new Vector3(0f, 0.82f, 0f), 0.68f,
                MaterialLibrary.Emissive(Rarities.Tint(spell.Rarity), 3f));

            Build.Sphere(root.transform, "Rune", new Vector3(0f, 1.35f, 0f), 0.55f,
                MaterialLibrary.Emissive(spell.Tint, 3.5f), collider: false);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;
            trigger.center = Vector3.up;

            var pedestal = root.AddComponent<SpellPedestal>();
            pedestal.Spell = spell;
            root.AddComponent<Bobber>();
            return pedestal;
        }
    }

    /// <summary>
    /// A rune that rebinds the Shift slot. There is only one movement slot, so taking one is
    /// a straight swap with no screen in between.
    /// </summary>
    public class MovementPedestal : MonoBehaviour, IInteractable
    {
        public MovementAbility Ability;

        public string Prompt
        {
            get
            {
                if (Ability == null) return null;

                PlayerRig rig = PlayerRig.Instance;
                MovementAbility current = rig != null && rig.Movement != null ? rig.Movement.Current : null;

                string replaces = current != null ? "  -  replaces " + current.DisplayName : "";
                return "Bind " + Ability.DisplayName + " to SHIFT  [" + Rarities.Name(Ability.Rarity)
                       + "]  " + Ability.CostLine() + replaces;
            }
        }

        public bool CanInteract(GameObject interactor) => Ability != null;

        public void Interact(GameObject interactor)
        {
            var rig = interactor.GetComponentInParent<PlayerRig>();
            if (rig == null || rig.Movement == null || Ability == null) return;

            rig.Movement.Equip(Ability);
            GameDirector.Instance?.Notify(Ability.DisplayName + " bound to SHIFT");
            Destroy(gameObject);
        }

        public static MovementPedestal Spawn(Vector3 position, MovementAbility ability)
        {
            var root = new GameObject("MovementPedestal");
            root.transform.position = position;
            root.layer = Layers.Prop;

            Build.Cylinder(root.transform, "Pedestal", new Vector3(0f, 0.4f, 0f),
                new Vector3(1f, 0.4f, 1f), MaterialLibrary.Lit(Palette.Trim), collider: true);

            Build.GroundDisc(root.transform, "RarityRing", new Vector3(0f, 0.82f, 0f), 0.68f,
                MaterialLibrary.Emissive(Rarities.Tint(ability.Rarity), 3f));

            // A pair of boots rather than a rune sphere, so it reads differently at a glance.
            Build.Cube(root.transform, "Rune", new Vector3(0f, 1.25f, 0f),
                new Vector3(0.5f, 0.22f, 0.7f), MaterialLibrary.Emissive(ability.Tint, 3.5f), collider: false);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;
            trigger.center = Vector3.up;

            var pedestal = root.AddComponent<MovementPedestal>();
            pedestal.Ability = ability;
            root.AddComponent<Bobber>();
            return pedestal;
        }
    }

    /// <summary>A standing stone that permanently raises one core stat.</summary>
    public class StatShrine : MonoBehaviour, IInteractable
    {
        public StatType Stat;
        public int Amount = 1;

        public string Prompt => "Touch the stone  +" + Amount + " " + Stat;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            var rig = interactor.GetComponentInParent<PlayerRig>();
            if (rig == null) return;

            rig.Sheet.AddStat(Stat, Amount);
            GameDirector.Instance?.Notify("+" + Amount + " " + Stat);

            GameObject pop = Build.Sphere(null, "ShrinePop", transform.position + Vector3.up, 1.2f,
                MaterialLibrary.Transparent(new Color(1f, 0.9f, 0.5f, 0.5f)), collider: false);
            FadeAndDie.Attach(pop, 0.4f, new Color(1f, 0.9f, 0.5f, 0.5f), Vector3.one * 3f);

            Destroy(gameObject);
        }

        public static StatShrine Spawn(Vector3 position, StatType stat, int amount = 1)
        {
            var root = new GameObject("StatShrine");
            root.transform.position = position;
            root.layer = Layers.Prop;

            Build.Cube(root.transform, "Stone", new Vector3(0f, 0.9f, 0f),
                new Vector3(0.7f, 1.8f, 0.7f), MaterialLibrary.Lit(Palette.Trim), collider: true);

            Build.Cube(root.transform, "Glyph", new Vector3(0f, 1.3f, 0.38f),
                new Vector3(0.35f, 0.35f, 0.06f),
                MaterialLibrary.Emissive(ColorForStat(stat), 3f), collider: false);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;
            trigger.center = Vector3.up;

            var shrine = root.AddComponent<StatShrine>();
            shrine.Stat = stat;
            shrine.Amount = amount;
            return shrine;
        }

        public static Color ColorForStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return new Color(1f, 0.45f, 0.35f);
                case StatType.Intellect: return new Color(0.55f, 0.65f, 1f);
                case StatType.Agility: return new Color(0.5f, 1f, 0.7f);
                case StatType.Vitality: return new Color(1f, 0.35f, 0.55f);
                default: return new Color(1f, 0.85f, 0.4f);
            }
        }
    }

    /// <summary>Simple idle animation for pickups.</summary>
    public class Bobber : MonoBehaviour
    {
        public float Speed = 1.6f;
        public float Height = 0.12f;
        public float Spin = 45f;

        private Vector3 _home;
        private float _phase;

        private void Start()
        {
            _home = transform.position;
            _phase = Random.Range(0f, 10f);
        }

        private void Update()
        {
            _phase += Time.deltaTime * Speed;
            transform.position = _home + Vector3.up * (Mathf.Sin(_phase) * Height);
            transform.Rotate(Vector3.up, Spin * Time.deltaTime, Space.World);
        }
    }

    /// <summary>The way out. Opens once the room is cleared.</summary>
    public class ExitPortal : MonoBehaviour, IInteractable
    {
        private bool _open;
        private Material _material;
        private Transform _ring;

        public string Prompt => _open ? "Ascend" : "Sealed until the room is clear";

        public bool CanInteract(GameObject interactor) => _open;

        public void Interact(GameObject interactor)
        {
            if (!_open) return;
            GameDirector.Instance?.CompleteRoom();
        }

        public void SetOpen(bool open)
        {
            _open = open;
            Color color = open ? Palette.Portal : new Color(0.3f, 0.3f, 0.35f);
            MaterialLibrary.SetMaterialColor(_material, color);
            if (_material != null && _material.HasProperty("_EmissionColor"))
                _material.SetColor("_EmissionColor", color * (open ? 3f : 0.3f));
        }

        private void Update()
        {
            if (_ring != null)
                _ring.Rotate(Vector3.forward, (_open ? 90f : 12f) * Time.deltaTime, Space.Self);
        }

        public static ExitPortal Spawn(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("ExitPortal");
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.layer = Layers.Prop;

            Material portalMaterial = new Material(MaterialLibrary.LitShader);
            MaterialLibrary.SetMaterialColor(portalMaterial, Palette.Portal);
            if (portalMaterial.HasProperty("_EmissionColor"))
            {
                portalMaterial.EnableKeyword("_EMISSION");
                portalMaterial.SetColor("_EmissionColor", Palette.Portal * 3f);
            }

            GameObject ring = Build.Cylinder(root.transform, "Ring", Vector3.up * 1.6f,
                new Vector3(2.6f, 0.16f, 2.6f), portalMaterial, collider: false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Build.Cube(root.transform, "Base", Vector3.up * 0.1f,
                new Vector3(3f, 0.2f, 1.2f), MaterialLibrary.Lit(Palette.Trim), collider: true);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3.4f, 3.4f, 3.4f);
            trigger.center = Vector3.up * 1.4f;

            var portal = root.AddComponent<ExitPortal>();
            portal._material = portalMaterial;
            portal._ring = ring.transform;
            portal.SetOpen(false);
            return portal;
        }
    }
}
