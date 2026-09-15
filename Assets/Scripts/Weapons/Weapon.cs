using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Runtime firing logic for one gun. Handles rate of fire, magazines, reloads, spread,
    /// recoil, and both delivery kinds. Input is pushed in by <see cref="PlayerCombat"/>.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        public WeaponDefinition Definition { get; private set; }

        public Team OwnerTeam = Team.Player;
        public GameObject Owner;
        public CharacterSheet OwnerSheet;
        public Mana OwnerMana;
        public PlayerLook Look;
        public Transform AimOrigin;    // usually the camera
        public Transform Muzzle;

        /// <summary>Run-wide on-hit effects granted by boons. Merged with the weapon list on every shot.</summary>
        public List<StatusApplication> ExtraStatuses;

        /// <summary>
        /// A phantom copy of a gun: no ammo, no reloads, no recoil pushed into the look controller, and
        /// its shots and hits say what they are. Divine Assistance, Phantasmal Mimic.
        /// </summary>
        public bool IsPhantom;

        /// <summary>Never spends a round and never reloads. Superid; phantoms behave this way already.</summary>
        public bool InfiniteAmmo;

        /// <summary>Reads another gun's infusions instead of its own, without spending them. A phantom mirroring the real gun.</summary>
        public Weapon InfusionSource;

        /// <summary>While set, every round goes straight at this with no spread, and projectiles steer onto it. Superid.</summary>
        public Transform ForcedTarget;

        /// <summary>The gun is firing itself; the trigger is ignored. <see cref="AutoFireDriver"/> pulls it.</summary>
        public bool AutoFire;

        private int _nextRound;
        private bool _firingEcho;

        private bool FreeRounds => IsPhantom || InfiniteAmmo || _firingEcho;

        /// <summary>Raised for every round, after it leaves the barrel.</summary>
        public event System.Action<WeaponShot> Fired;

        /// <summary>
        /// Raised when a round from this gun lands on something alive, hitscan or projectile alike,
        /// after its damage is dealt. Smite, Desecrate and the mastery meters hang off this.
        /// </summary>
        public event System.Action<WeaponHit> Hit;

        /// <summary>Temporary charges on this gun's rounds. Kept across a holster swap, which re-equips this component.</summary>
        private readonly List<BulletInfusion> _infusions = new List<BulletInfusion>();

        /// <summary>The infusions live for the round being fired right now.</summary>
        private List<BulletInfusion> _roundInfusions;

        public int AmmoInMagazine { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress { get; private set; }

        private float _cooldown;
        private float _altCooldown;
        private bool _triggerWasDown;
        private bool _altWasDown;
        private Coroutine _reloadRoutine;
        private Coroutine _burstRoutine;
        private Coroutine _salvoRoutine;
        private GameObject _model;

        /// <summary>Synthesised once on equip rather than per shot, so the cost lands on pickup.</summary>
        private AudioClip _fireClip;

        public bool IsEmpty => AmmoInMagazine <= 0;

        /// <summary>
        /// Takes up a gun. <paramref name="ammoInMagazine"/> below zero loads a full magazine;
        /// a swap passes the count the gun was put down with, so trading back and forth at a
        /// pedestal is not a free reload.
        /// </summary>
        public void Equip(WeaponDefinition definition, int ammoInMagazine = -1)
        {
            StopAllRunningRoutines();

            Definition = definition;
            AmmoInMagazine = ammoInMagazine < 0
                ? definition.MagazineSize
                : Mathf.Clamp(ammoInMagazine, 0, definition.MagazineSize);
            IsReloading = false;
            _cooldown = 0f;
            _altCooldown = 0f;
            IsFocusing = false;
            _altWasDown = false;
            _spin = 0f;
            _spinSoundPlayed = false;
            _fireClip = SoundLibrary.ForWeapon(definition);

            BuildModel();
        }

        private void StopAllRunningRoutines()
        {
            if (_reloadRoutine != null) { StopCoroutine(_reloadRoutine); _reloadRoutine = null; }
            if (_burstRoutine != null) { StopCoroutine(_burstRoutine); _burstRoutine = null; }
            if (_salvoRoutine != null) { StopCoroutine(_salvoRoutine); _salvoRoutine = null; }
        }

        /// <summary>Blocky viewmodel so the player can see which gun they are holding.</summary>
        private void BuildModel()
        {
            if (_model != null)
            {
                if (Application.isPlaying) Destroy(_model);
                else DestroyImmediate(_model);
            }

            _model = Build.Empty(transform, "Model");
            Material body = MaterialLibrary.Lit(new Color(0.15f, 0.15f, 0.18f), 0.4f, 0.6f);
            Material accent = MaterialLibrary.Emissive(Definition.Tint, 2f);

            Build.Cube(_model.transform, "Frame", new Vector3(0f, 0f, 0.30f),
                new Vector3(0.09f, 0.11f, 0.42f), body, collider: false);
            Build.Cube(_model.transform, "Grip", new Vector3(0f, -0.12f, 0.14f),
                new Vector3(0.075f, 0.20f, 0.11f), body, collider: false);
            Build.Cube(_model.transform, "Rune", new Vector3(0f, 0.055f, 0.34f),
                new Vector3(0.03f, 0.02f, 0.22f), accent, collider: false);

            var muzzle = Build.Empty(_model.transform, "Muzzle", new Vector3(0f, 0f, 0.52f));
            Muzzle = muzzle.transform;
        }

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            if (_altCooldown > 0f) _altCooldown -= Time.deltaTime;
            if (_infusions.Count > 0) TickInfusions(Time.deltaTime);
        }

        // ---------------------------------------------------------------- infusions

        public IReadOnlyList<BulletInfusion> Infusions => _infusions;

        /// <summary>Adds a charge to every round. One with the same id replaces the old one.</summary>
        public void Infuse(BulletInfusion infusion)
        {
            if (infusion == null) return;
            if (!string.IsNullOrEmpty(infusion.Id)) _infusions.RemoveAll(i => i.Id == infusion.Id);
            if (!infusion.Expired) _infusions.Add(infusion);
        }

        public void ClearInfusions() => _infusions.Clear();

        public void RemoveInfusion(string id) => _infusions.RemoveAll(i => i.Id == id);

        /// <summary>Puts the magazine at a count, as Rewind restores it. Ends any reload in progress.</summary>
        public void SetAmmo(int rounds)
        {
            if (Definition == null) return;

            StopAllRunningRoutines();
            IsReloading = false;
            ReloadProgress = 0f;
            AmmoInMagazine = Mathf.Clamp(rounds, 0, Definition.MagazineSize);
        }

        /// <summary>
        /// One ordinary round now, ignoring the rate of fire, for a copy firing whenever its real gun does.
        /// Divine Assistance's mirrored gun. Spends ammo only if this gun spends ammo at all.
        /// </summary>
        public void FireNow()
        {
            if (Definition == null || IsReloading) return;
            if (!FreeRounds && AmmoInMagazine <= 0) return;

            FireRound(ShotSpec.Primary(Definition, CurrentSpread()), 1);
        }

        private List<BulletInfusion> InfusionList
            => InfusionSource != null && InfusionSource != this ? InfusionSource._infusions : _infusions;

        /// <summary>Runs infusion timers down. Public so tooling can drive it without waiting.</summary>
        public void TickInfusions(float seconds)
        {
            for (int i = _infusions.Count - 1; i >= 0; i--)
            {
                BulletInfusion infusion = _infusions[i];
                if (infusion.HasTimeLimit) infusion.SecondsLeft -= seconds;
                if (infusion.Expired) _infusions.RemoveAt(i);
            }
        }

        private List<BulletInfusion> ActiveInfusions()
        {
            List<BulletInfusion> source = InfusionList;
            var active = new List<BulletInfusion>(source.Count);
            for (int i = 0; i < source.Count; i++)
                if (!source[i].Expired) active.Add(source[i]);
            return active;
        }

        private void SpendInfusionRound()
        {
            // A copy's rounds carry a charge without using it up, or Smite's one round would go to the phantom.
            if (IsPhantom || _firingEcho || InfusionSource != null) return;

            for (int i = _infusions.Count - 1; i >= 0; i--)
            {
                BulletInfusion infusion = _infusions[i];
                if (infusion.HasRoundLimit) infusion.RoundsLeft--;
                if (infusion.Expired) _infusions.RemoveAt(i);
            }
        }

        /// <summary>
        /// Tells listeners a round from this gun landed on something alive. Hitscan calls this
        /// directly; a projectile calls it when it arrives, passing the infusions the gun carried
        /// when it fired, so a one-round infusion still lands after it has been spent.
        /// </summary>
        public void ReportHit(IDamageable target, in DamageInfo damage, Vector3 point, Vector3 normal,
            Vector3 direction, List<BulletInfusion> infusions, int round = 0, bool echo = false)
        {
            if (target == null) return;

            var hit = new WeaponHit
            {
                Weapon = this,
                Target = target,
                Damage = damage,
                Point = point,
                Normal = normal,
                Direction = direction,
                Round = round,
                IsPhantom = IsPhantom,
                IsEcho = echo
            };

            if (infusions != null)
                for (int i = 0; i < infusions.Count; i++) infusions[i].OnHit?.Invoke(hit);

            Hit?.Invoke(hit);
        }

        // ---------------------------------------------------------------- input

        public void HandleInput(bool triggerDown, bool altDown, bool reloadPressed)
        {
            if (Definition == null) return;

            if (reloadPressed) StartReload();

            bool pressedThisFrame = triggerDown && !_triggerWasDown;
            _triggerWasDown = triggerDown;

            bool altPressedThisFrame = altDown && !_altWasDown;
            _altWasDown = altDown;

            HandleAltInput(altDown, altPressedThisFrame);

            // Pulling the trigger mid-reload should click rather than do nothing at all.
            // Gated on the press so holding an automatic down does not machine-gun the click.
            if (pressedThisFrame && IsReloading && OwnerTeam == Team.Player)
                Sfx.PlayFlat(SoundLibrary.Get(SoundLibrary.DryFireId));

            UpdateSpin(triggerDown);

            bool wantsToShoot = Definition.Mode == FireMode.Auto ? triggerDown : pressedThisFrame;
            if (wantsToShoot && IsSpunUp) TryFire();
        }

        public void TryFire()
        {
            if (Definition == null || IsReloading) return;

            if (!FreeRounds && AmmoInMagazine <= 0)
            {
                StartReload();
                return;
            }
            if (_cooldown > 0f) return;

            // A phantom uses none of its owner's resources.
            if (!IsPhantom && Definition.ManaPerShot > 0f && (OwnerMana == null || !OwnerMana.TrySpend(Definition.ManaPerShot)))
                return;

            float attackSpeed = OwnerSheet != null ? OwnerSheet.Get(Attr.AttackSpeed) : 1f;

            // Focus trades rate of fire for accuracy and damage, so it is a choice rather than
            // a strictly better way to hold the gun.
            if (IsFocusing) attackSpeed *= Definition.AltFire.FocusRateMultiplier;

            _cooldown = Definition.SecondsBetweenShots / Mathf.Max(0.1f, attackSpeed);

            if (Definition.Mode == FireMode.Burst && Definition.BurstCount > 1)
            {
                _burstRoutine = StartCoroutine(FireBurst());
            }
            else
            {
                FireOnce();
            }
        }

        // ---------------------------------------------------------------- wind-up

        private float _spin;
        private bool _spinSoundPlayed;

        /// <summary>0 to 1. Always 1 for a gun with no wind-up, so callers need no special case.</summary>
        public float SpinProgress =>
            Definition == null || Definition.SpinUpSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(_spin / Definition.SpinUpSeconds);

        public bool HasSpinUp => Definition != null && Definition.SpinUpSeconds > 0f;
        public bool IsSpunUp => SpinProgress >= 1f;

        /// <summary>
        /// Winds the barrels up while the trigger is held and back down when it is not.
        /// Reloading resets it, because the reload animation is the barrels stopping.
        /// </summary>
        private void UpdateSpin(bool triggerDown)
        {
            if (!HasSpinUp)
            {
                _spin = 0f;
                return;
            }

            bool winding = triggerDown && !IsReloading && AmmoInMagazine > 0;

            if (winding)
            {
                if (_spin <= 0f && !_spinSoundPlayed && OwnerTeam == Team.Player)
                {
                    Sfx.PlayFlat(SoundLibrary.Get(SoundLibrary.SpinUpId), 0.8f, pitchVariance: 0.02f);
                    _spinSoundPlayed = true;
                }

                _spin = Mathf.Min(_spin + Time.deltaTime, Definition.SpinUpSeconds);
                return;
            }

            _spin = Mathf.Max(0f, _spin - Time.deltaTime * Mathf.Max(0.1f, Definition.SpinDownMultiplier));
            if (_spin <= 0f) _spinSoundPlayed = false;
        }

        // ---------------------------------------------------------------- alt fire

        /// <summary>Why an alt fire did not happen, so the HUD can say something useful.</summary>
        public enum AltOutcome { Fired, Held, None, Locked, OnCooldown, NoAmmo, NotEnoughMana, Busy }

        public bool IsFocusing { get; private set; }

        public AltFireProfile Alt => Definition != null ? Definition.AltFire : null;

        /// <summary>Available on this gun and unlocked for this run.</summary>
        public bool AltUnlocked =>
            Alt != null && Alt.Exists && RunState.Current != null &&
            RunState.Current.AltFireTier >= Alt.UnlockTier;

        public float AltCooldownRemaining => _altCooldown;

        /// <summary>
        /// Reports rather than acts when it cannot fire, so the caller can tell the player why.
        /// A gun with no alt fire has to say so - a dead button reads as a broken game.
        /// </summary>
        public AltOutcome EvaluateAlt()
        {
            if (Definition == null || Alt == null || !Alt.Exists) return AltOutcome.None;
            if (!AltUnlocked) return AltOutcome.Locked;
            if (IsReloading || _burstRoutine != null || _salvoRoutine != null) return AltOutcome.Busy;
            if (_altCooldown > 0f) return AltOutcome.OnCooldown;
            if (AmmoInMagazine < Alt.AmmoCost) return AltOutcome.NoAmmo;
            if (Alt.ManaCost > 0f && (OwnerMana == null || OwnerMana.Current < Alt.ManaCost))
                return AltOutcome.NotEnoughMana;

            return Alt.Kind == AltFireKind.Focus ? AltOutcome.Held : AltOutcome.Fired;
        }

        private void HandleAltInput(bool altDown, bool altPressedThisFrame)
        {
            bool focusable = Alt != null && Alt.Kind == AltFireKind.Focus && AltUnlocked && !IsReloading;
            IsFocusing = focusable && altDown;

            if (!altPressedThisFrame || Alt == null || Alt.IsHeld) return;
            if (EvaluateAlt() != AltOutcome.Fired) return;

            TriggerAlt();
        }

        /// <summary>Fires the alt. Assumes <see cref="EvaluateAlt"/> already said yes.</summary>
        private void TriggerAlt()
        {
            if (Alt.ManaCost > 0f && (OwnerMana == null || !OwnerMana.TrySpend(Alt.ManaCost))) return;

            _altCooldown = Alt.Cooldown;

            if (Alt.Kind == AltFireKind.Salvo) _salvoRoutine = StartCoroutine(FireSalvo());
            else FireRound(ShotSpec.Alt(Definition, Alt), Alt.AmmoCost);
        }

        /// <summary>
        /// Empties the magazine at speed. Capped, because a 34-round drum would otherwise
        /// produce a salvo you cannot cancel and cannot aim.
        /// </summary>
        private IEnumerator FireSalvo()
        {
            ShotSpec spec = ShotSpec.Primary(Definition, CurrentSpread(), Alt.SalvoDamageMultiplier);
            float interval = Definition.SecondsBetweenShots / Mathf.Max(0.1f, Alt.SalvoRateMultiplier);
            int fired = 0;

            while (AmmoInMagazine > 0 && fired < Alt.SalvoMaxRounds)
            {
                FireRound(spec, 1);
                fired++;

                // FireRound starts a reload when the magazine runs dry, which ends the salvo.
                if (IsReloading) break;
                yield return new WaitForSeconds(interval);
            }

            _salvoRoutine = null;
        }

        private IEnumerator FireBurst()
        {
            for (int i = 0; i < Definition.BurstCount; i++)
            {
                if (!FreeRounds && AmmoInMagazine <= 0) break;
                FireOnce();
                yield return new WaitForSeconds(Definition.BurstInterval);
            }
            _burstRoutine = null;
        }

        private void FireOnce()
        {
            float damageMultiplier = IsFocusing ? Definition.AltFire.FocusDamageMultiplier : 1f;
            FireRound(ShotSpec.Primary(Definition, CurrentSpread(), damageMultiplier), 1);
        }

        /// <summary>
        /// Puts one round downrange and pays for it. Both triggers come through here, so
        /// impacts, tracers, recoil, the muzzle flash and the auto-reload behave identically
        /// whichever button asked.
        /// </summary>
        private void FireRound(ShotSpec spec, int ammoCost, Vector3? directionOverride = null)
        {
            if (!FreeRounds) AmmoInMagazine -= Mathf.Max(0, ammoCost);

            Vector3 origin = AimOrigin != null ? AimOrigin.position : transform.position;
            Vector3 forward = directionOverride ?? (AimOrigin != null ? AimOrigin.forward : transform.forward);

            // Auto-aim: straight at the target's centre, every pellet, with no spread at all.
            bool forced = ForcedTarget != null && !_firingEcho;
            if (forced)
            {
                Vector3 toTarget = ForcedTarget.position + Vector3.up * 0.9f - origin;
                if (toTarget.sqrMagnitude > 0.0001f) forward = toTarget.normalized;
            }

            // Dexterity steadies the hands: one multiplier on the cone, one on the kick. Applied
            // here rather than to the definition, so both triggers pick them up.
            float spreadScale = OwnerSheet != null ? OwnerSheet.Get(Attr.Spread) : 1f;
            float recoilScale = OwnerSheet != null ? OwnerSheet.Get(Attr.Recoil) : 1f;

            // Taken once per round, so every pellet carries the same charge and a one-round
            // infusion is spent by the whole round rather than by its first pellet.
            _roundInfusions = ActiveInfusions();

            for (int i = 0; i < spec.Pellets; i++)
            {
                // Every pellet is its own round, so a round striking several things still counts once.
                int round = ++_nextRound;
                Vector3 direction = forced ? forward : ApplySpread(forward, spec.SpreadDegrees * spreadScale);
                if (spec.Delivery == DeliveryKind.Hitscan) FireHitscan(spec, origin, direction, round);
                else FireProjectile(spec, origin, direction, round);
            }

            Fired?.Invoke(new WeaponShot
            {
                Weapon = this, Spec = spec, Origin = origin, Direction = forward, AmmoCost = ammoCost,
                IsPhantom = IsPhantom, IsEcho = _firingEcho
            });
            SpendInfusionRound();

            MuzzleFlash();
            PlayFireSound();

            // A copy of a gun firing alongside yours must not double your recoil.
            if (Look != null && !IsPhantom && !_firingEcho)
                Look.AddRecoil(spec.RecoilPitch * recoilScale, Random.Range(-1f, 1f) * spec.RecoilYaw * recoilScale);
            if (!FreeRounds && AmmoInMagazine <= 0) StartReload();
        }

        /// <summary>
        /// A repeat of an earlier round for Echo: the recorded shot along its recorded direction, from
        /// where the gun is now. No ammo, no recoil and no infusion spent, and its shot event says it is
        /// an echo so it is never recorded to be repeated again.
        /// </summary>
        public void FireEcho(ShotSpec spec, Vector3 direction)
        {
            if (Definition == null) return;

            _firingEcho = true;
            try
            {
                FireRound(spec, 0, direction.sqrMagnitude > 0.0001f ? direction.normalized : (Vector3?)null);
            }
            finally
            {
                _firingEcho = false;
            }
        }

        private float CurrentSpread()
        {
            var motor = Owner != null ? Owner.GetComponent<PlayerMotor>() : null;
            bool moving = motor != null && motor.HorizontalSpeed > 1.5f;
            float spread = moving ? Definition.MovingSpreadDegrees : Definition.SpreadDegrees;

            // Holding focus is what actually makes an inaccurate gun usable at range.
            if (IsFocusing) spread *= Definition.AltFire.FocusSpreadMultiplier;
            return spread;
        }

        /// <summary>
        /// Scatters a shot inside a cone around <paramref name="forward"/>.
        ///
        /// The axes have to come from the shot, not from the world. Quaternion.Euler turns
        /// about the world axes, so a shot travelling down world X was being rotated about its
        /// own direction - which does nothing at all, and collapsed the cone into a flat
        /// horizontal line for anyone facing that way.
        /// </summary>
        public static Vector3 ApplySpread(Vector3 forward, float degrees)
        {
            if (degrees <= 0.001f) return forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Straight up or straight down leaves no horizontal axis to work from, so any
            // perpendicular will do - the cone is symmetric about the barrel either way.
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            Vector3 up = Vector3.Cross(forward, right);

            Vector2 offset = Random.insideUnitCircle * degrees;
            return Quaternion.AngleAxis(offset.x, up) * Quaternion.AngleAxis(offset.y, right) * forward;
        }

        // ---------------------------------------------------------------- delivery

        private void FireHitscan(in ShotSpec spec, Vector3 origin, Vector3 direction, int round)
        {
            int mask = Layers.ShotMaskFor(OwnerTeam);
            float range = spec.Range;
            ShotTargets.Clear();
            Vector3 endPoint = origin + direction * range;

            int count = Physics.RaycastNonAlloc(new Ray(origin, direction), HitBuffer, range, mask,
                QueryTriggerInteraction.Ignore);
            if (count > 1) SortHitsByDistance(count);

            int pierceBudget = spec.MaxPierce;
            bool anythingHit = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];

                // Smoke that hands shots on: the round lands on someone inside instead, and with
                // nobody inside it carries straight on through. Looked up through parents, so a
                // collider on the smoke's own visuals counts as the smoke rather than as a wall.
                var redirect = hit.collider.GetComponentInParent<ShotRedirectVolume>();
                if (redirect != null)
                {
                    if (!redirect.TryPickTarget(OwnerTeam, out IDamageable handed)) continue;

                    Vector3 at = AbilityContext.CenterOf(handed);
                    DamageInfo handedDamage = BuildShotDamage(spec, at, -direction, direction, _roundInfusions);
                    handed.TakeDamage(handedDamage);
                    ReportHit(handed, handedDamage, at, -direction, direction, _roundInfusions, round, _firingEcho);
                    Combat.SpawnImpact(at, -direction, spec.Tint, 0.3f, spec.DamageType);

                    endPoint = hit.point;
                    anythingHit = true;
                    break;
                }
                IDamageable target = Combat.FindDamageable(hit.collider);
                bool isTarget = target != null && target.IsAlive &&
                                (target.Team != OwnerTeam || target.Team == Team.Neutral);

                if (!isTarget)
                {
                    // A wall stops the shot dead.
                    endPoint = hit.point;
                    Combat.SpawnImpact(hit.point, hit.normal, spec.Tint, 0.22f, spec.DamageType);
                    anythingHit = true;
                    break;
                }

                // A head and its body are two colliders on one target, and a round through both is one hit.
                if (ShotTargets.Contains(target)) continue;
                ShotTargets.Add(target);

                DamageInfo damage = BuildShotDamage(spec, hit.point, hit.normal, direction, _roundInfusions);
                if (CrossesHead(count, target))
                {
                    damage.Amount *= Combat.HeadshotMultiplier;
                    damage.IsHeadshot = true;
                }

                target.TakeDamage(damage);
                ReportHit(target, damage, hit.point, hit.normal, direction, _roundInfusions, round, _firingEcho);
                Combat.SpawnImpact(hit.point, hit.normal, spec.Tint, 0.3f, spec.DamageType);
                endPoint = hit.point;
                anythingHit = true;

                if (pierceBudget <= 0) break;
                pierceBudget--;
            }

            if (!anythingHit) endPoint = origin + direction * range;

            Vector3 tracerStart = Muzzle != null ? Muzzle.position : origin;
            Combat.SpawnTracer(tracerStart, endPoint, spec.Tint, 0.035f, 0.05f);
        }

        private static readonly List<IDamageable> ShotTargets = new List<IDamageable>();

        /// <summary>
        /// Whether the round's line passes through this target's head anywhere. The top of the body overlaps the bottom
        /// of the head, so a round aimed at the head can clip the body first and must still count.
        /// </summary>
        private static bool CrossesHead(int count, IDamageable target)
        {
            for (int i = 0; i < count; i++)
                if (Combat.IsHead(HitBuffer[i].collider) && ReferenceEquals(Combat.FindDamageable(HitBuffer[i].collider), target))
                    return true;
            return false;
        }

        private static void SortHitsByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = HitBuffer[i];
                int j = i - 1;
                while (j >= 0 && HitBuffer[j].distance > key.distance)
                {
                    HitBuffer[j + 1] = HitBuffer[j];
                    j--;
                }
                HitBuffer[j + 1] = key;
            }
        }

        /// <summary>
        /// The hit one round deals on arrival: the gun's statuses, the run's boon statuses, and those
        /// of every infusion given, or of the gun's live infusions when none are given. Public so
        /// tooling can inspect a shot without firing it.
        /// </summary>
        public DamageInfo BuildShotDamage(in ShotSpec spec, Vector3 point, Vector3 normal, Vector3 direction,
            List<BulletInfusion> infusions = null)
        {
            if (infusions == null) infusions = ActiveInfusions();

            float amount = spec.Damage * Combat.OutgoingMultiplier(OwnerSheet, false, spec.DamageType);
            bool crit = false;
            if (Combat.RollCrit(OwnerSheet, out float critMultiplier))
            {
                amount *= critMultiplier;
                crit = true;
            }

            DamageInfo info = DamageInfo.Create(amount, spec.DamageType, OwnerTeam, Owner);
            info.IsCrit = crit;
            info.Origin = DamageOrigin.Gun;
            info.Knockback = direction * spec.Knockback;
            info = info.At(point, normal)
                       .WithStatuses(spec.Statuses)
                       .WithStatuses(ExtraStatuses);

            for (int i = 0; i < infusions.Count; i++) info = info.WithStatuses(infusions[i].Statuses);
            return info;
        }

        private void FireProjectile(in ShotSpec spec, Vector3 origin, Vector3 direction, int round)
        {
            Vector3 spawn = Muzzle != null ? Muzzle.position : origin + direction * 0.6f;

            // Aim the projectile at whatever the crosshair is actually over, so the muzzle
            // offset does not throw shots off at close range.
            Vector3 aimPoint = origin + direction * 200f;
            if (Physics.Raycast(origin, direction, out RaycastHit look, 200f, Layers.ShotMaskFor(OwnerTeam),
                    QueryTriggerInteraction.Ignore))
                aimPoint = look.point;

            Vector3 travelDirection = (aimPoint - spawn).normalized;

            float outgoing = Combat.OutgoingMultiplier(OwnerSheet, false, spec.DamageType);

            Projectile p = Projectile.Create(spawn, travelDirection, spec.Tint, spec.ProjectileRadius);
            p.OwnerTeam = OwnerTeam;
            p.Owner = Owner;
            p.OwnerSheet = OwnerSheet;
            p.Damage = spec.Damage * outgoing;
            p.DamageType = spec.DamageType;
            p.Origin = DamageOrigin.Gun;
            p.Speed = spec.ProjectileSpeed;
            p.Gravity = spec.ProjectileGravity;
            p.Lifetime = spec.ProjectileLifetime;
            p.Knockback = spec.Knockback;
            p.SplashRadius = spec.SplashRadius;
            p.SplashDamage = spec.SplashDamage * outgoing;
            p.Pierce = spec.MaxPierce;
            p.HomingEnabled = spec.ProjectileHoming;

            List<BulletInfusion> infusions = _roundInfusions ?? ActiveInfusions();

            var statuses = new List<StatusApplication>(spec.Statuses);
            if (ExtraStatuses != null) statuses.AddRange(ExtraStatuses);
            for (int i = 0; i < infusions.Count; i++) statuses.AddRange(infusions[i].Statuses);
            p.Statuses = statuses;

            p.SourceWeapon = this;
            p.Infusions = infusions;
            p.Round = round;
            p.IsEcho = _firingEcho;

            // "Never misses" for a projectile gun means its rounds steer onto the target.
            if (ForcedTarget != null && !_firingEcho)
            {
                p.HomingTarget = ForcedTarget;
                p.HomingEnabled = true;
                p.HomingStrength = Mathf.Max(p.HomingStrength, 14f);
            }

            p.Launch();
        }

        /// <summary>
        /// The player's own gun plays flat so it does not pan as they turn; anyone else's is
        /// positional, which is how you hear where you are being shot from.
        /// </summary>
        private void PlayFireSound()
        {
            Vector3 where = Muzzle != null ? Muzzle.position : transform.position;

            // Only the player gives themselves away. An enemy firing does not need to alert
            // the room it is already fighting in, and would otherwise wake every other enemy
            // in earshot the moment one of them noticed anything.
            if (OwnerTeam == Team.Player && Definition != null)
                Noise.Emit(where, Definition.NoiseMultiplier);

            if (_fireClip == null) return;

            if (OwnerTeam == Team.Player) Sfx.PlayFlat(_fireClip);
            else Sfx.PlayAt(_fireClip, where);
        }

        private void MuzzleFlash()
        {
            if (Muzzle == null) return;
            GameObject flash = Build.Sphere(null, "MuzzleFlash", Muzzle.position, 0.22f,
                MaterialLibrary.Transparent(Definition.Tint), collider: false);
            FadeAndDie.Attach(flash, 0.05f, Definition.Tint);
        }

        // ---------------------------------------------------------------- reload

        public void StartReload()
        {
            // A gun with no magazine to empty has nothing to reload, and must not pause to do it.
            if (IsReloading || Definition == null || IsPhantom || InfiniteAmmo) return;
            if (AmmoInMagazine >= Definition.MagazineSize) return;
            _reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            ReloadProgress = 0f;

            float speed = OwnerSheet != null ? OwnerSheet.Get(Attr.ReloadSpeed) : 1f;
            float duration = Definition.ReloadTime / Mathf.Max(0.1f, speed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            AmmoInMagazine = Definition.MagazineSize;
            IsReloading = false;
            ReloadProgress = 0f;
            _reloadRoutine = null;
        }

        public void RefillMagazine()
        {
            StopAllRunningRoutines();
            IsReloading = false;
            if (Definition != null) AmmoInMagazine = Definition.MagazineSize;
        }
    }
}
