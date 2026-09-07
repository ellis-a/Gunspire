using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
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
            if (_model != null) Destroy(_model);

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

            if (AmmoInMagazine <= 0)
            {
                StartReload();
                return;
            }
            if (_cooldown > 0f) return;

            if (Definition.ManaPerShot > 0f && (OwnerMana == null || !OwnerMana.TrySpend(Definition.ManaPerShot)))
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
                if (AmmoInMagazine <= 0) break;
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
        private void FireRound(ShotSpec spec, int ammoCost)
        {
            AmmoInMagazine -= Mathf.Max(0, ammoCost);

            Vector3 origin = AimOrigin != null ? AimOrigin.position : transform.position;
            Vector3 forward = AimOrigin != null ? AimOrigin.forward : transform.forward;

            for (int i = 0; i < spec.Pellets; i++)
            {
                Vector3 direction = ApplySpread(forward, spec.SpreadDegrees);
                if (spec.Delivery == DeliveryKind.Hitscan) FireHitscan(spec, origin, direction);
                else FireProjectile(spec, origin, direction);
            }

            MuzzleFlash();
            PlayFireSound();

            if (Look != null) Look.AddRecoil(spec.RecoilPitch, Random.Range(-1f, 1f) * spec.RecoilYaw);
            if (AmmoInMagazine <= 0) StartReload();
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

        private static Vector3 ApplySpread(Vector3 forward, float degrees)
        {
            if (degrees <= 0.001f) return forward;
            Vector2 offset = Random.insideUnitCircle * degrees;
            return Quaternion.Euler(offset.y, offset.x, 0f) * forward;
        }

        // ---------------------------------------------------------------- delivery

        private void FireHitscan(in ShotSpec spec, Vector3 origin, Vector3 direction)
        {
            int mask = Layers.HitMaskFor(OwnerTeam);
            float range = spec.Range;
            Vector3 endPoint = origin + direction * range;

            int count = Physics.RaycastNonAlloc(new Ray(origin, direction), HitBuffer, range, mask,
                QueryTriggerInteraction.Ignore);
            if (count > 1) SortHitsByDistance(count);

            int pierceBudget = spec.MaxPierce;
            bool anythingHit = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
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

                target.TakeDamage(BuildHitscanDamage(spec, hit.point, hit.normal, direction));
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

        private DamageInfo BuildHitscanDamage(in ShotSpec spec, Vector3 point, Vector3 normal, Vector3 direction)
        {
            float amount = spec.Damage * Combat.OutgoingMultiplier(OwnerSheet, false, spec.DamageType);
            bool crit = false;
            if (Combat.RollCrit(OwnerSheet, out float critMultiplier))
            {
                amount *= critMultiplier;
                crit = true;
            }

            DamageInfo info = DamageInfo.Create(amount, spec.DamageType, OwnerTeam, Owner);
            info.IsCrit = crit;
            info.Knockback = direction * spec.Knockback;
            info = info.At(point, normal)
                       .WithStatuses(spec.Statuses)
                       .WithStatuses(ExtraStatuses);
            return info;
        }

        private void FireProjectile(in ShotSpec spec, Vector3 origin, Vector3 direction)
        {
            Vector3 spawn = Muzzle != null ? Muzzle.position : origin + direction * 0.6f;

            // Aim the projectile at whatever the crosshair is actually over, so the muzzle
            // offset does not throw shots off at close range.
            Vector3 aimPoint = origin + direction * 200f;
            if (Physics.Raycast(origin, direction, out RaycastHit look, 200f, Layers.HitMaskFor(OwnerTeam),
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
            p.Speed = spec.ProjectileSpeed;
            p.Gravity = spec.ProjectileGravity;
            p.Lifetime = spec.ProjectileLifetime;
            p.Knockback = spec.Knockback;
            p.SplashRadius = spec.SplashRadius;
            p.SplashDamage = spec.SplashDamage * outgoing;
            p.Pierce = spec.MaxPierce;
            p.HomingEnabled = spec.ProjectileHoming;

            var statuses = new List<StatusApplication>(spec.Statuses);
            if (ExtraStatuses != null) statuses.AddRange(ExtraStatuses);
            p.Statuses = statuses;

            p.Launch();
        }

        /// <summary>
        /// The player's own gun plays flat so it does not pan as they turn; anyone else's is
        /// positional, which is how you hear where you are being shot from.
        /// </summary>
        private void PlayFireSound()
        {
            if (_fireClip == null) return;

            if (OwnerTeam == Team.Player) Sfx.PlayFlat(_fireClip);
            else Sfx.PlayAt(_fireClip, Muzzle != null ? Muzzle.position : transform.position);
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
            if (IsReloading || Definition == null) return;
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
