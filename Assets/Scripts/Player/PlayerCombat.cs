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

        // Qualified because this class also has a field called Health, and an unqualified
        // Health.AnyDamaged reads as the field rather than the type.
        private void OnEnable() => WizardGun.Health.AnyDamaged += OnAnythingDamaged;
        private void OnDisable() => WizardGun.Health.AnyDamaged -= OnAnythingDamaged;

        /// <summary>
        /// The hitmarker. Combat.SpawnImpact already covers where a shot *landed*, but that
        /// fires against walls too; this is the separate, quieter confirmation that the thing
        /// you hit was alive. Guns, spells and the bash all route through Health, so one
        /// subscription covers every way the player can deal damage.
        /// </summary>
        private void OnAnythingDamaged(WizardGun.Health victim, DamageInfo info, float amount)
        {
            if (info.Source != gameObject || victim == null) return;
            if (victim.gameObject == gameObject) return;

            Sfx.PlayFlat(SoundLibrary.Get(SoundLibrary.HitConfirmId),
                info.IsCrit ? 1f : 0.7f, pitchVariance: 0.03f);
        }

        private void Update()
        {
            if (_bashTimer > 0f) _bashTimer -= Time.deltaTime;

            ScanForInteractable();

            if (!InputEnabled)
            {
                // Feed a released frame rather than just skipping. Focus is a held state, so
                // stopping here mid-zoom would leave the gun scoped and the camera narrowed
                // for as long as the boon screen is open.
                if (Weapon != null)
                {
                    Weapon.HandleInput(false, false, false);
                    ApplyZoom();
                }
                return;
            }

            if (Weapon != null)
            {
                bool alt = Input.GetMouseButton(1);
                Weapon.HandleInput(Input.GetMouseButton(0), alt, Input.GetKeyDown(KeyCode.R));

                if (Input.GetMouseButtonDown(1)) ReportAltFire();
                ApplyZoom();
            }

            if (Book != null)
            {
                for (int i = 0; i < SpellBook.SlotCount; i++)
                    if (Input.GetKeyDown(SpellBook.SlotKeys[i])) UseSpellSlot(i);
            }

            // Right click is alt fire now, so the bash lives on V alone - it was already
            // bound there, and sharing a button with a gun's second trigger is worse than
            // moving it.
            if (Input.GetKeyDown(KeyCode.V)) TryBash();

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

        /// <summary>
        /// Says why right click did nothing. Same reasoning as the spell slots: a button that
        /// silently does nothing reads as a bug, and "this gun has no alt fire" is information
        /// the player needs when deciding whether to pick a gun up.
        /// </summary>
        private void ReportAltFire()
        {
            switch (Weapon.EvaluateAlt())
            {
                case Weapon.AltOutcome.None:
                    Notify(Weapon.Definition.DisplayName + " has no alt fire");
                    break;
                case Weapon.AltOutcome.Locked:
                    Notify(Weapon.Alt.Name + " is locked - find a Gunsmith boon");
                    break;
                case Weapon.AltOutcome.NoAmmo:
                    Notify("Not enough ammo for " + Weapon.Alt.Name);
                    break;
                case Weapon.AltOutcome.NotEnoughMana:
                    Notify("Not enough mana");
                    break;
                // Cooldown and mid-burst both read clearly on the HUD, so they stay quiet.
            }
        }

        /// <summary>Drives the camera from whether the gun is currently focusing.</summary>
        private void ApplyZoom()
        {
            if (Look == null) return;

            AltFireProfile alt = Weapon.Alt;
            Look.ZoomFov = Weapon.IsFocusing && alt != null ? alt.FocusFov : 0f;
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

        /// <summary>
        /// Takes <paramref name="incoming"/> and hands back whatever was being held, along with
        /// the rounds left in it. Returns null if there was nothing to give up.
        /// </summary>
        public WeaponDefinition SwapWeapon(WeaponDefinition incoming, int incomingAmmo, out int outgoingAmmo)
        {
            outgoingAmmo = -1;
            if (Weapon == null || incoming == null) return null;

            WeaponDefinition outgoing = Weapon.Definition;
            if (outgoing != null) outgoingAmmo = Weapon.AmmoInMagazine;

            Weapon.Equip(incoming, incomingAmmo);
            return outgoing;
        }
    }
}
