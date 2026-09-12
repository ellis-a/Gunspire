using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Reads the combat half of the player input and drives the gun, the spell slots,
    /// the Strength-powered melee bash, and interaction.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon swapping")]
        /// <summary>Shortest gap between wheel-driven swaps, so one flick is one swap.</summary>
        [SerializeField] private float scrollSwapInterval = 0.18f;

        [Header("Interaction")]
        [SerializeField] private float interactRange = 3.5f;

        public Weapon Weapon;
        public Holster Holster;
        public SpellBook Book;
        public PlayerLook Look;
        public PlayerMotor Motor;
        public Health Health;
        public Mana Mana;
        public CharacterSheet Sheet;
        public Transform Aim;
        public StatusController Status;

        /// <summary>
        /// The melee slot. A <see cref="Spell"/> like any other, restricted to
        /// <see cref="SpellSlot.Melee"/> and swapped at a pedestal rather than levelled.
        /// </summary>
        public Spell MeleeSpell { get; private set; }

        /// <summary>Shared with the spell book, so melee runs the same effects as everything else.</summary>
        public AbilityContext Context { get; set; }

        private float _bashTimer;
        private float _swapCooldown;
        private IInteractable _focus;
        private Component _focusComponent;

        public bool InputEnabled { get; set; } = true;

        /// <summary>What the crosshair is currently pointed at, for the HUD prompt.</summary>
        public string InteractPrompt => _focus != null && _focus.CanInteract(gameObject) ? _focus.Prompt : null;

        /// <summary>Fraction of the melee cooldown still to run, for the HUD.</summary>
        public float MeleeCooldownFraction => MeleeSpell == null || MeleeSpell.Cooldown <= 0f
            ? 0f
            : Mathf.Clamp01(_bashTimer / MeleeSpell.Cooldown);

        /// <summary>Refuses anything that is not a melee spell, rather than binding it silently.</summary>
        public void EquipMelee(Spell spell)
        {
            if (spell != null && spell.Slot != SpellSlot.Melee)
            {
                Debug.LogWarning("PlayerCombat was handed " + spell.Id
                                 + ", which is a " + spell.Slot + " spell. Ignoring it.");
                return;
            }

            MeleeSpell = spell;
            _bashTimer = 0f;
        }

        // Qualified because this class also has a field called Health, and an unqualified
        // Health.AnyDamaged reads as the field rather than the type.
        private void OnEnable() => Gunspire.Health.AnyDamaged += OnAnythingDamaged;
        private void OnDisable() => Gunspire.Health.AnyDamaged -= OnAnythingDamaged;

        /// <summary>
        /// The hitmarker. Combat.SpawnImpact already covers where a shot *landed*, but that
        /// fires against walls too; this is the separate, quieter confirmation that the thing
        /// you hit was alive. Guns, spells and the bash all route through Health, so one
        /// subscription covers every way the player can deal damage.
        /// </summary>
        private void OnAnythingDamaged(Gunspire.Health victim, DamageInfo info, float amount)
        {
            if (info.Source != gameObject || victim == null) return;
            if (victim.gameObject == gameObject) return;

            Sfx.PlayFlat(SoundLibrary.Get(SoundLibrary.HitConfirmId),
                info.IsCrit ? 1f : 0.7f, pitchVariance: 0.03f);
        }

        private void Update()
        {
            if (_bashTimer > 0f) _bashTimer -= Time.deltaTime;

            // Shock deafens the player as well as the enemies, so the one place that ticks every
            // frame on the player sets it. Left at one whenever nothing is shocking them.
            Sfx.Muffle = ShockStatus.HearingScale(Status);

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

            ReadSwapInput();

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
        /// Casts whatever is bound to the melee slot. The swing itself is an effect chain like
        /// any other spell, so its reach, damage, knockback and Strength scaling are all
        /// authored on the asset rather than hardcoded here.
        /// </summary>
        private void TryBash()
        {
            if (_bashTimer > 0f || Aim == null || MeleeSpell == null || Context == null) return;

            if (MeleeSpell.ManaCost > 0f && (Mana == null || !Mana.Has(MeleeSpell.ManaCost)))
            {
                Notify("Not enough mana for " + MeleeSpell.DisplayName);
                return;
            }

            // Charged only once the chain commits, so a swing that aborts costs nothing - the
            // same refund rule the cast slots and the Shift slot already follow.
            if (!MeleeSpell.Cast(Context, 1)) return;

            if (MeleeSpell.ManaCost > 0f && Mana != null) Mana.TrySpend(MeleeSpell.ManaCost);

            _bashTimer = MeleeSpell.Cooldown
                         / Mathf.Max(0.25f, Sheet != null ? Sheet.Get(Attr.AttackSpeed) : 1f);

            SpawnBashVisual(Aim.position, Aim.forward);
        }

        private void SpawnBashVisual(Vector3 origin, Vector3 forward)
        {
            float range = 3.2f;
            float halfAngle = 55f;

            // Read back off the selector so the flourish matches whatever the spell actually
            // swept, rather than drawing a fixed cone over an authored one.
            for (int i = 0; i < MeleeSpell.OnCast.Count; i++)
            {
                if (!(MeleeSpell.OnCast[i] is SelectConeEffect cone)) continue;
                range = cone.Range;
                halfAngle = cone.HalfAngle;
                break;
            }

            Color color = MeleeSpell.Tint;
            color.a = 0.28f;

            GameObject visual = MeshFactory.SpawnCone(origin, forward, range, halfAngle,
                MaterialLibrary.Transparent(color));
            FadeAndDie.Attach(visual, 0.16f, color);
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

        // ---------------------------------------------------------------- swapping

        /// <summary>
        /// X, or a flick of the wheel either way. With only two guns there is no next and
        /// previous to tell apart, so both directions do the same thing.
        ///
        /// The wheel is rate-limited because one physical notch does not reliably arrive as one
        /// frame of input - a fast scroll would otherwise swap twice and look like it did
        /// nothing at all.
        /// </summary>
        private void ReadSwapInput()
        {
            if (Holster == null || !Holster.CanSwap) return;

            if (_swapCooldown > 0f) _swapCooldown -= Time.deltaTime;

            bool pressed = Input.GetKeyDown(Holster.SwapKey);
            bool scrolled = _swapCooldown <= 0f
                            && Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.01f;

            if (!pressed && !scrolled) return;

            _swapCooldown = scrollSwapInterval;
            Holster.Swap();

            Sfx.PlayFlat(SoundLibrary.Get(SoundLibrary.DryFireId), 0.35f, pitchVariance: 0.05f);
        }

        /// <summary>Swaps the held gun, keeping the run modifiers attached to the weapon.</summary>
        public void EquipWeapon(WeaponDefinition definition)
        {
            if (Holster == null || definition == null) return;
            Holster.SetSlot(Holster.ActiveIndex, definition);
        }

        /// <summary>
        /// Takes <paramref name="incoming"/> and hands back whatever it displaced, along with
        /// the rounds left in it. Returns null when nothing was given up - which now happens
        /// whenever a hand was free, not only on the very first gun of a run.
        /// </summary>
        public WeaponDefinition SwapWeapon(WeaponDefinition incoming, int incomingAmmo, out int outgoingAmmo)
        {
            outgoingAmmo = -1;
            if (Holster == null || incoming == null) return null;

            return Holster.Take(incoming, incomingAmmo, out outgoingAmmo);
        }
    }
}
