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
        private bool _triggerWasDown;
        private Coroutine _reloadRoutine;
        private Coroutine _burstRoutine;
        private GameObject _model;

        public bool IsEmpty => AmmoInMagazine <= 0;

        public void Equip(WeaponDefinition definition)
        {
            StopAllRunningRoutines();

            Definition = definition;
            AmmoInMagazine = definition.MagazineSize;
            IsReloading = false;
            _cooldown = 0f;

            BuildModel();
        }

        private void StopAllRunningRoutines()
        {
            if (_reloadRoutine != null) { StopCoroutine(_reloadRoutine); _reloadRoutine = null; }
            if (_burstRoutine != null) { StopCoroutine(_burstRoutine); _burstRoutine = null; }
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
        }

        // ---------------------------------------------------------------- input

        public void HandleInput(bool triggerDown, bool reloadPressed)
        {
            if (Definition == null) return;

            if (reloadPressed) StartReload();

            bool pressedThisFrame = triggerDown && !_triggerWasDown;
            _triggerWasDown = triggerDown;

            bool wantsToShoot = Definition.Mode == FireMode.Auto ? triggerDown : pressedThisFrame;
            if (wantsToShoot) TryFire();
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
            AmmoInMagazine--;

            Vector3 origin = AimOrigin != null ? AimOrigin.position : transform.position;
            Vector3 forward = AimOrigin != null ? AimOrigin.forward : transform.forward;
            float spread = CurrentSpread();

            for (int i = 0; i < Mathf.Max(1, Definition.PelletsPerShot); i++)
            {
                Vector3 direction = ApplySpread(forward, spread);
                if (Definition.Delivery == DeliveryKind.Hitscan) FireHitscan(origin, direction);
                else FireProjectile(origin, direction);
            }

            MuzzleFlash();

            if (Look != null) Look.AddRecoil(Definition.RecoilPitch, Random.Range(-1f, 1f) * Definition.RecoilYaw);
            if (AmmoInMagazine <= 0) StartReload();
        }

        private float CurrentSpread()
        {
            var motor = Owner != null ? Owner.GetComponent<PlayerMotor>() : null;
            bool moving = motor != null && motor.HorizontalSpeed > 1.5f;
            return moving ? Definition.MovingSpreadDegrees : Definition.SpreadDegrees;
        }

        private static Vector3 ApplySpread(Vector3 forward, float degrees)
        {
            if (degrees <= 0.001f) return forward;
            Vector2 offset = Random.insideUnitCircle * degrees;
            return Quaternion.Euler(offset.y, offset.x, 0f) * forward;
        }

        // ---------------------------------------------------------------- delivery

        private void FireHitscan(Vector3 origin, Vector3 direction)
        {
            int mask = Layers.HitMaskFor(OwnerTeam);
            float range = Definition.Range;
            Vector3 endPoint = origin + direction * range;

            int count = Physics.RaycastNonAlloc(new Ray(origin, direction), HitBuffer, range, mask,
                QueryTriggerInteraction.Ignore);
            if (count > 1) SortHitsByDistance(count);

            int pierceBudget = Definition.MaxPierce;
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
                    Combat.SpawnImpact(hit.point, hit.normal, Definition.Tint, 0.22f);
                    anythingHit = true;
                    break;
                }

                target.TakeDamage(BuildHitscanDamage(hit.point, hit.normal, direction));
                Combat.SpawnImpact(hit.point, hit.normal, Definition.Tint, 0.3f);
                endPoint = hit.point;
                anythingHit = true;

                if (pierceBudget <= 0) break;
                pierceBudget--;
            }

            if (!anythingHit) endPoint = origin + direction * range;

            Vector3 tracerStart = Muzzle != null ? Muzzle.position : origin;
            Combat.SpawnTracer(tracerStart, endPoint, Definition.Tint, 0.035f, 0.05f);
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

        private DamageInfo BuildHitscanDamage(Vector3 point, Vector3 normal, Vector3 direction)
        {
            float amount = Definition.Damage * Combat.OutgoingMultiplier(OwnerSheet, false, Definition.DamageType);
            bool crit = false;
            if (Combat.RollCrit(OwnerSheet, out float critMultiplier))
            {
                amount *= critMultiplier;
                crit = true;
            }

            DamageInfo info = DamageInfo.Create(amount, Definition.DamageType, OwnerTeam, Owner);
            info.IsCrit = crit;
            info.Knockback = direction * Definition.Knockback;
            info = info.At(point, normal)
                       .WithStatuses(Definition.OnHitStatuses)
                       .WithStatuses(ExtraStatuses);
            return info;
        }

        private void FireProjectile(Vector3 origin, Vector3 direction)
        {
            Vector3 spawn = Muzzle != null ? Muzzle.position : origin + direction * 0.6f;

            // Aim the projectile at whatever the crosshair is actually over, so the muzzle
            // offset does not throw shots off at close range.
            Vector3 aimPoint = origin + direction * 200f;
            if (Physics.Raycast(origin, direction, out RaycastHit look, 200f, Layers.HitMaskFor(OwnerTeam),
                    QueryTriggerInteraction.Ignore))
                aimPoint = look.point;

            Vector3 travelDirection = (aimPoint - spawn).normalized;

            Projectile p = Projectile.Create(spawn, travelDirection, Definition.Tint, Definition.ProjectileRadius);
            p.OwnerTeam = OwnerTeam;
            p.Owner = Owner;
            p.OwnerSheet = OwnerSheet;
            p.Damage = Definition.Damage * Combat.OutgoingMultiplier(OwnerSheet, false, Definition.DamageType);
            p.DamageType = Definition.DamageType;
            p.Speed = Definition.ProjectileSpeed;
            p.Gravity = Definition.ProjectileGravity;
            p.Lifetime = Definition.ProjectileLifetime;
            p.Knockback = Definition.Knockback;
            p.SplashRadius = Definition.SplashRadius;
            p.SplashDamage = Definition.SplashDamage * Combat.OutgoingMultiplier(OwnerSheet, false, Definition.DamageType);
            p.Pierce = Definition.MaxPierce;
            p.HomingEnabled = Definition.ProjectileHoming;

            var statuses = new List<StatusApplication>(Definition.OnHitStatuses);
            if (ExtraStatuses != null) statuses.AddRange(ExtraStatuses);
            p.Statuses = statuses;

            p.Launch();
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
