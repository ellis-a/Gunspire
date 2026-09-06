using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Reads the combat half of the player input and drives the gun, the spell slots,
    /// the Strength-powered melee bash, and interaction.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Melee bash")]
        [SerializeField] private float bashCooldown = 0.7f;
        [SerializeField] private float bashRange = 3.2f;
        [SerializeField] private float bashHalfAngle = 55f;

        [Header("Interaction")]
        [SerializeField] private float interactRange = 3.5f;

        public Weapon Weapon;
        public SpellBook Book;
        public PlayerLook Look;
        public PlayerMotor Motor;
        public Health Health;
        public Mana Mana;
        public CharacterSheet Sheet;
        public Transform Aim;

        private float _bashTimer;
        private IInteractable _focus;
        private Component _focusComponent;

        public bool InputEnabled { get; set; } = true;

        /// <summary>What the crosshair is currently pointed at, for the HUD prompt.</summary>
        public string InteractPrompt => _focus != null && _focus.CanInteract(gameObject) ? _focus.Prompt : null;

        private void Update()
        {
            if (_bashTimer > 0f) _bashTimer -= Time.deltaTime;

            ScanForInteractable();

            if (!InputEnabled) return;

            if (Weapon != null)
                Weapon.HandleInput(Input.GetMouseButton(0), Input.GetKeyDown(KeyCode.R));

            if (Book != null)
            {
                for (int i = 0; i < SpellBook.SlotCount; i++)
                    if (Input.GetKeyDown(SpellBook.SlotKeys[i])) UseSpellSlot(i);
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.V)) TryBash();

            if (Input.GetKeyDown(KeyCode.F) && _focus != null && _focus.CanInteract(gameObject))
                _focus.Interact(gameObject);
        }

        /// <summary>
        /// Uses a spell slot and says why if it does not fire. Pressing a key and getting
        /// silence reads as a broken game, especially now that a run opens with Q empty.
        /// </summary>
        private void UseSpellSlot(int slot)
        {
            CastOutcome outcome = Book.TryCastSlot(slot);
            if (outcome == CastOutcome.Cast) return;

            string label = SpellBook.SlotLabels[slot];
            switch (outcome)
            {
                case CastOutcome.NoSpell:
                    Notify("Nothing bound to " + label + " - learn a spell at a Rune Shrine");
                    break;
                case CastOutcome.NotEnoughMana:
                    Notify("Not enough mana");
                    break;
                case CastOutcome.NoRoom:
                    Notify("No room to cast that");
                    break;
                // A cooldown already reads clearly on the HUD slot, so it stays quiet.
            }
        }

        private static void Notify(string message)
        {
            if (GameDirector.Instance != null) GameDirector.Instance.Notify(message, 1.6f);
        }

        // ---------------------------------------------------------------- melee bash

        /// <summary>
        /// A close-range swing. Damage and, more importantly, smash power scale on Strength,
        /// which is what lets a strong wizard break reinforced barriers open.
        /// </summary>
        private void TryBash()
        {
            if (_bashTimer > 0f || Aim == null) return;
            _bashTimer = bashCooldown / Mathf.Max(0.25f, Sheet != null ? Sheet.Get(Attr.AttackSpeed) : 1f);

            int strength = Sheet != null ? Sheet.GetStat(StatType.Strength) : 5;
            float damage = (10f + strength * 3.5f) * (Sheet != null ? Sheet.Get(Attr.DamageDealt) : 1f);
            float smashPower = Sheet != null ? Sheet.Get(Attr.SmashPower) : strength;

            Vector3 origin = Aim.position;
            Vector3 forward = Aim.forward;

            var targets = Combat.ConeTargets(origin, forward, bashRange, bashHalfAngle,
                Layers.PlayerHitMask);

            for (int i = 0; i < targets.Count; i++)
            {
                IDamageable target = targets[i];
                if (target.Team == Team.Player) continue;

                DamageInfo info = DamageInfo.Create(damage, DamageType.Normal, Team.Player, gameObject);
                info.SmashPower = smashPower;
                info.Knockback = forward * (4f + strength * 0.5f);
                info = info.At(target.Transform.position + Vector3.up, -forward);
                target.TakeDamage(info);
            }

            SpawnBashVisual(origin, forward);
        }

        private void SpawnBashVisual(Vector3 origin, Vector3 forward)
        {
            var color = new Color(1f, 0.85f, 0.6f, 0.28f);
            GameObject cone = MeshFactory.SpawnCone(origin, forward, bashRange, bashHalfAngle,
                MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(cone, 0.16f, color);
        }

        // ---------------------------------------------------------------- interaction

        private void ScanForInteractable()
        {
            _focus = null;
            _focusComponent = null;
            if (Aim == null) return;

            // Look-at first, then a forgiving sphere so pickups do not need pixel-perfect aim.
            if (Physics.Raycast(Aim.position, Aim.forward, out RaycastHit hit, interactRange,
                    ~0, QueryTriggerInteraction.Collide))
            {
                var found = hit.collider.GetComponentInParent<IInteractable>();
                if (found != null)
                {
                    SetFocus(found);
                    return;
                }
            }

            Collider[] nearby = Physics.OverlapSphere(transform.position, interactRange,
                ~0, QueryTriggerInteraction.Collide);

            float best = float.MaxValue;
            for (int i = 0; i < nearby.Length; i++)
            {
                var found = nearby[i].GetComponentInParent<IInteractable>();
                if (found == null || !found.CanInteract(gameObject)) continue;

                float distance = Vector3.SqrMagnitude(nearby[i].transform.position - transform.position);
                if (distance < best)
                {
                    best = distance;
                    SetFocus(found);
                }
            }
        }

        private void SetFocus(IInteractable interactable)
        {
            _focus = interactable;
            _focusComponent = interactable as Component;
        }

        public Component FocusComponent => _focusComponent;

        /// <summary>Swaps the held gun, keeping the run modifiers attached to the weapon.</summary>
        public void EquipWeapon(WeaponDefinition definition)
        {
            if (Weapon == null || definition == null) return;
            Weapon.Equip(definition);
        }
    }
}
