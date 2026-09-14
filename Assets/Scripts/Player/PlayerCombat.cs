using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Reads the combat half of the player input and drives the gun, the spell slots,
    /// the melee spell, and interaction.
    ///
    /// On the player, silence stops spells - the cast slots and Shift - and disarm stops weapons: the
    /// gun and the melee slot. That mirrors enemies, where silence stops ranged attacks and disarm
    /// stops melee.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        /// <summary>Keys read here. Named, so Verify Spell Slots can prove no two actions share one.</summary>
        public static readonly KeyCode InteractKey = KeyCode.X;
        public static readonly KeyCode MeleeKey = KeyCode.V;
        public static readonly KeyCode ReloadKey = KeyCode.R;

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
        /// <see cref="SpellSlot.Melee"/>, swapped or levelled at a pedestal.
        /// </summary>
        public Spell MeleeSpell { get; private set; }

        /// <summary>Shared with the spell book, so melee runs the same effects as everything else.</summary>
        public AbilityContext Context { get; set; }

        /// <summary>A melee attack is about to run its chain. Psi Blades spends its charge here.</summary>
        public event Action<Spell> MeleeStarting;

        /// <summary>A melee attack finished, and whether it actually happened rather than aborting.</summary>
        public event Action<Spell, bool> MeleeFinished;

        private float _bashTimer;
        private float _bashCooldownFull;
        private float _swapCooldown;
        private IInteractable _focus;
        private Component _focusComponent;

        public bool InputEnabled { get; set; } = true;

        /// <summary>What the crosshair is currently pointed at, for the HUD prompt.</summary>
        public string InteractPrompt => _focus != null && _focus.CanInteract(gameObject) ? _focus.Prompt : null;

        /// <summary>Fraction of the melee cooldown still to run, for the HUD.</summary>
        public float MeleeCooldownFraction => _bashCooldownFull <= 0f ? 0f : Mathf.Clamp01(_bashTimer / _bashCooldownFull);

        public int MeleeLevel => Book != null && MeleeSpell != null ? Mathf.Max(1, Book.GetLevel(MeleeSpell)) : 1;

        /// <summary>The direction of the last melee attack, for anything recording it.</summary>
        public Vector3 LastMeleeForward { get; private set; }

        public bool IsDisarmed => Status != null && Status.IsDisarmed;
        public bool IsSilenced => Status != null && Status.IsSilenced;

        /// <summary>Whether the trigger reaches the gun: not disarmed, and not firing itself.</summary>
        public bool CanShoot => !IsDisarmed && (Weapon == null || !Weapon.AutoFire);

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
            _bashCooldownFull = 0f;

            if (Book != null) Book.SetEquipped(SpellSlot.Melee, spell);
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
                if (CanShoot)
                {
                    Weapon.HandleInput(Input.GetMouseButton(0), Input.GetMouseButton(1), Input.GetKeyDown(ReloadKey));
                    if (Input.GetMouseButtonDown(1)) ReportAltFire();
                }
                else
                {
                    // A gun firing itself needs no trigger, and a disarmed one gets a released frame so
                    // nothing held down carries through.
                    Weapon.HandleInput(false, false, false);
                    if (IsDisarmed && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                        Notify(SpellCosts.Refusal(CastOutcome.Disarmed, null) + " - cannot shoot");
                }

                ApplyZoom();
            }

            if (Book != null)
            {
                for (int i = 0; i < SpellBook.SlotCount; i++)
                {
                    if (Input.GetKeyDown(SpellBook.SlotKeys[i])) ReportSlot(i, Book.PressSlot(i));
                    if (Input.GetKeyUp(SpellBook.SlotKeys[i])) ReportSlot(i, Book.ReleaseSlot(i));
                }
            }

            // Right click is alt fire now, so the bash lives on V alone - it was already
            // bound there, and sharing a button with a gun's second trigger is worse than
            // moving it.
            if (Input.GetKeyDown(MeleeKey)) TryBash();

            if (Input.GetKeyDown(InteractKey) && _focus != null && _focus.CanInteract(gameObject))
                _focus.Interact(gameObject);
        }

        /// <summary>
        /// Says why a spell slot did not fire. Pressing a key and getting silence reads as a broken
        /// game, especially now that a run opens with Q empty.
        /// </summary>
        private void ReportSlot(int slot, CastOutcome outcome)
        {
            string label = SpellBook.SlotLabels[slot];
            switch (outcome)
            {
                // Cooldowns, charging and toggles already read clearly on the HUD slot, so they stay quiet.
                case CastOutcome.Cast:
                case CastOutcome.Ready:
                case CastOutcome.OnCooldown:
                case CastOutcome.Charging:
                case CastOutcome.ToggledOff:
                    return;

                case CastOutcome.StanceChanged:
                    StanceMode mode = Book.ActiveStanceMode(slot);
                    if (mode != null) Notify(Book.GetSlot(slot).DisplayName + ": " + mode.Name);
                    return;

                case CastOutcome.NoSpell:
                    Notify("Nothing bound to " + label + " - learn a spell at a Rune Shrine");
                    return;

                default:
                    string text = SpellCosts.Refusal(outcome, Book.GetSlot(slot));
                    if (text != null) Notify(text);
                    return;
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
            if (GameDirector.Instance != null && !string.IsNullOrEmpty(message)) GameDirector.Instance.Notify(message, 1.6f);
        }

        // ---------------------------------------------------------------- melee bash

        /// <summary>Why the melee slot can or cannot be used right now.</summary>
        public CastOutcome EvaluateMelee()
        {
            if (MeleeSpell == null || Context == null) return CastOutcome.NoSpell;
            if (_bashTimer > 0f) return CastOutcome.OnCooldown;
            if (IsDisarmed) return CastOutcome.Disarmed;
            return SpellCosts.Check(MeleeSpell, Context);
        }

        private void TryBash()
        {
            if (Aim == null) return;

            CastOutcome outcome = TryCastMelee();
            switch (outcome)
            {
                case CastOutcome.Cast:
                case CastOutcome.OnCooldown:
                case CastOutcome.NoSpell:
                case CastOutcome.NoRoom:
                    return;
                case CastOutcome.NotEnoughMana:
                    Notify("Not enough mana for " + MeleeSpell.DisplayName);
                    return;
                default:
                    Notify(SpellCosts.Refusal(outcome, MeleeSpell));
                    return;
            }
        }

        /// <summary>
        /// Casts whatever is bound to the melee slot. The swing itself is an effect chain like
        /// any other spell, so its reach, damage and knockback are all authored on the asset rather
        /// than hardcoded here. Public so tooling can swing without a key.
        /// </summary>
        public CastOutcome TryCastMelee()
        {
            CastOutcome outcome = EvaluateMelee();
            if (outcome != CastOutcome.Ready) return outcome;

            Spell spell = MeleeSpell;
            int level = MeleeLevel;
            Vector3 forward = Aim != null ? Aim.forward : transform.forward;

            MeleeStarting?.Invoke(spell);

            // Charged only once the chain commits, so a swing that aborts costs nothing - the
            // same refund rule the cast slots and the Shift slot already follow.
            Context.SoulsSpent = SpellCosts.SoulsFor(spell, Context);
            bool cast = spell.Cast(Context, level);
            if (cast) SpellCosts.Pay(spell, Context);
            Context.SoulsSpent = 0;

            if (cast)
            {
                _bashCooldownFull = spell.CooldownAtLevel(level)
                                    / Mathf.Max(0.25f, Sheet != null ? Sheet.Get(Attr.AttackSpeed) : 1f);
                _bashTimer = _bashCooldownFull;
                LastMeleeForward = forward;

                if (Aim != null) SpawnBashVisual(Aim.position, Aim.forward);
            }

            MeleeFinished?.Invoke(spell, cast);
            return cast ? CastOutcome.Cast : CastOutcome.NoRoom;
        }

        /// <summary>A repeat of an earlier swing for Echo: free, with no cooldown, raising no melee events.</summary>
        public bool CastMeleeEcho(Spell spell, int level, Vector3 forward)
        {
            if (Context == null || spell == null) return false;

            Context.IsEcho = true;
            Context.ForwardOverride = forward.sqrMagnitude > 0.0001f ? forward.normalized : (Vector3?)null;

            try
            {
                return spell.Cast(Context, Mathf.Max(1, level));
            }
            finally
            {
                Context.IsEcho = false;
                Context.ForwardOverride = null;
            }
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
        /// C, or a flick of the wheel either way. With only two guns there is no next and
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
