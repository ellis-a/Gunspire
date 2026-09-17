using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Behaviours for the Arsenal boons: class rules, enchantments, handling and on-hit effects.

    // ---------------------------------------------------------------- class rules

    /// <summary>
    /// A class's rounds deal more under a condition read from the round itself. Quickdraw, Point Blank, Closing
    /// Round, Fresh Mag and Tail End.
    /// </summary>
    [System.Serializable]
    public class ClassRoundBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public enum Condition { FirstAfterDraw, FirstAfterReload, BurstEnd, MagazineTail, WithinRange }

        /// <summary>Unassigned means any gun.</summary>
        public WeaponClass Class = WeaponClass.Unassigned;
        public Condition When = Condition.FirstAfterReload;

        /// <summary>The tail share for <see cref="Condition.MagazineTail"/>, or metres for <see cref="Condition.WithinRange"/>.</summary>
        public float Threshold = 0.25f;

        public float PerLevel = 0.1f;

        /// <summary>A flat multiplier on top of the per-level bonus. Tail End's double damage.</summary>
        public float Multiplier = 1f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            if (!IsOwnHit(hit) || hit.Origin != DamageOrigin.Gun || hit.Weapon == null) return 1f;
            if (Class != WeaponClass.Unassigned && !IsGunOfClass(hit.Weapon, Class)) return 1f;
            if (!Holds(hit, target)) return 1f;
            return Multiplier * (1f + PerLevel * Level);
        }

        private bool Holds(in DamageInfo hit, Health target)
        {
            switch (When)
            {
                case Condition.FirstAfterDraw: return hit.Shot.SinceDraw == 1;
                case Condition.FirstAfterReload: return hit.Shot.SinceReload == 1;
                case Condition.BurstEnd: return hit.Shot.BurstEnd;
                case Condition.MagazineTail: return hit.Shot.MagazineLeft <= Threshold;
                default:
                    Vector3 from = hit.HasSourcePosition ? hit.SourcePosition : Player.transform.position;
                    return Vector3.Distance(from, target.transform.position) <= Threshold;
            }
        }

        public override string Describe() => When + (Class != WeaponClass.Unassigned ? " (" + Class + ")" : "");
    }

    /// <summary>Each handgun hit in a row deals more; a miss resets the run. Dead Man's Hand.</summary>
    [System.Serializable]
    public class DeadMansHandBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float PerHit = 0.1f;
        public int MaxStreak = 10;

        [System.NonSerialized] private int _streak;
        public int Streak => _streak;

        protected override void OnGunHit(WeaponHit hit)
        {
            if (hit.IsPhantom || hit.IsEcho || !IsGunOfClass(hit.Weapon, WeaponClass.Handgun)) return;
            _streak = Mathf.Min(MaxStreak, _streak + 1);
        }

        protected override void OnGunMissed(Weapon weapon, int round)
        {
            if (IsGunOfClass(weapon, WeaponClass.Handgun)) _streak = 0;
        }

        // The hit being dealt counts the streak before it, so the first hit in a row is plain.
        public float OutgoingMultiplier(in DamageInfo hit, Health target)
            => IsOwnHit(hit) && hit.Origin == DamageOrigin.Gun && IsGunOfClass(hit.Weapon, WeaponClass.Handgun)
                ? 1f + PerHit * _streak
                : 1f;
    }

    /// <summary>Every third rifle hit on the same enemy is a critical hit. Pinpoint.</summary>
    [System.Serializable]
    public class PinpointBehaviour : BoonBehaviour, ICritRule
    {
        public int Every = 3;

        [System.NonSerialized] private Dictionary<IDamageable, int> _hits;

        protected override void OnBind() => _hits = new Dictionary<IDamageable, int>();
        public override void OnFloorEntered(RoomRuntime room) => _hits.Clear();

        public void AdjustCrit(CharacterSheet attacker, IDamageable target, Weapon weapon, ref float chance, ref bool forced)
        {
            if (attacker != Sheet || target == null || !IsGunOfClass(weapon, WeaponClass.Rifle) || weapon.IsPhantom) return;

            _hits.TryGetValue(target, out int count);
            count++;
            if (count >= Every)
            {
                forced = true;
                count = 0;
            }
            _hits[target] = count;
        }
    }

    /// <summary>A sniper round fired soon after the scope comes up deals double. Quickscope.</summary>
    [System.Serializable]
    public class QuickscopeBehaviour : BoonBehaviour, IOutgoingDamageRule
    {
        public float Window = 0.3f;
        public float Multiplier = 2f;

        public float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            Weapon weapon = hit.Weapon;
            if (!IsOwnHit(hit) || hit.Origin != DamageOrigin.Gun || !IsGunOfClass(weapon, WeaponClass.Sniper)) return 1f;
            return weapon.IsFocusing && Time.unscaledTime - weapon.FocusStartedAt <= Window ? Multiplier : 1f;
        }
    }

    /// <summary>Standing still, heavy guns fire dead straight and never spin down. Planted.</summary>
    [System.Serializable]
    public class PlantedBehaviour : BoonBehaviour
    {
        public float StillSpeed = 0.5f;

        public override void Tick(float dt)
        {
            Weapon weapon = Player != null ? Player.Weapon : null;
            if (weapon == null) return;

            bool planted = IsGunOfClass(weapon, WeaponClass.Heavy)
                && Player.Motor != null && Player.Motor.IsGrounded && Player.Motor.HorizontalSpeed <= StillSpeed;
            weapon.NoSpread = planted;
            weapon.HoldSpin = planted;
        }

        protected override void OnUnbind()
        {
            Weapon weapon = Player != null ? Player.Weapon : null;
            if (weapon == null) return;
            weapon.NoSpread = false;
            weapon.HoldSpin = false;
        }
    }

    /// <summary>Your launcher blasts near you throw you, without hurting you. Rocket Jump.</summary>
    [System.Serializable]
    public class RocketJumpBehaviour : BoonBehaviour, IIncomingDamageRule
    {
        public float Force = 14f;
        public float ReachBeyondSplash = 0.5f;

        protected override void OnBind() => Projectile.AnyDetonated += OnDetonated;
        protected override void OnUnbind() => Projectile.AnyDetonated -= OnDetonated;

        private void OnDetonated(Projectile projectile, Vector3 at)
        {
            if (Player == null || Player.Motor == null || !IsOwnLauncher(projectile)) return;

            Vector3 centre = Player.transform.position + Vector3.up;
            Vector3 away = centre - at;
            float reach = projectile.SplashRadius + ReachBeyondSplash;
            if (away.sqrMagnitude > reach * reach) return;

            float falloff = 1f - Mathf.Clamp01(away.magnitude / Mathf.Max(0.01f, reach));
            Vector3 push = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up) + Vector3.up;
            Player.Motor.AddImpulse(push.normalized * Force * (0.5f + 0.5f * falloff));
        }

        private bool IsOwnLauncher(Projectile projectile)
            => projectile != null && IsPlayer(projectile.Owner) && IsGunOfClass(projectile.SourceWeapon, WeaponClass.Launcher);

        public float ModifyIncoming(in DamageInfo hit, Health player, float amount)
            => IsPlayer(hit.Source) && hit.Origin == DamageOrigin.Gun && IsGunOfClass(hit.Weapon, WeaponClass.Launcher) ? 0f : amount;
    }

    // ---------------------------------------------------------------- enchantments

    /// <summary>
    /// Gun hits apply a status, its amount the round's damage × <see cref="Multiplier"/> × level. One per status,
    /// any number held. The live ones are listed so Shared Instinct can hand them to the companion.
    /// </summary>
    [System.Serializable]
    public class EnchantmentBehaviour : BoonBehaviour
    {
        public StatusId Status = StatusId.Burn;
        public float Multiplier = 0.25f;

        private static readonly List<EnchantmentBehaviour> Live = new List<EnchantmentBehaviour>();
        public static IReadOnlyList<EnchantmentBehaviour> Active => Live;

        protected override void OnBind() => Live.Add(this);
        protected override void OnUnbind() => Live.Remove(this);

        protected override void OnGunHit(WeaponHit hit)
        {
            // Shock charges only the first round after a reload; a miss wastes it.
            if (Status == StatusId.Shock && hit.Damage.Shot.SinceReload != 1) return;
            ApplyTo(hit.Target, hit.Damage.Amount);
        }

        /// <summary>Puts this enchantment's status on a target, sized from a hit of that much damage.</summary>
        public void ApplyTo(IDamageable target, float damage)
        {
            if (target == null || !target.IsAlive || target.Team == Team.Player || damage <= 0f) return;

            StatusController status = target.Transform != null ? target.Transform.GetComponent<StatusController>() : null;
            if (status == null) return;

            float amount = damage * Multiplier * Level;
            if (TryBuild(amount, out StatusApplication app))
                status.Apply(app, Player != null ? Player.gameObject : null, Team.Player);
        }

        private bool TryBuild(float amount, out StatusApplication app)
        {
            int stacks = Mathf.Max(1, Mathf.RoundToInt(amount));
            switch (Status)
            {
                case StatusId.Burn: app = StatusLibrary.Burn(amount: amount); return true;
                case StatusId.Bleed: app = StatusLibrary.Bleed(dps: amount); return true;
                case StatusId.Poison: app = StatusLibrary.Poison(stacks: stacks); return true;
                case StatusId.Torment: app = StatusLibrary.Torment(dps: amount); return true;
                case StatusId.Shock: app = StatusLibrary.Shock(stacks: stacks); return true;
                case StatusId.Frost: app = StatusLibrary.Frost(stacks: stacks); return true;
                case StatusId.Weaken:
                    app = StatusLibrary.Weaken();
                    app.Magnitude = Mathf.Min(WeakenCap, amount / 100f);
                    return true;
                case StatusId.Hex: app = StatusLibrary.Hex(share: Mathf.Min(HexStatus.MaxShare, amount / 100f)); return true;
                case StatusId.Volatile: app = StatusLibrary.Volatile(amount); return true;
                case StatusId.Gilded: app = StatusLibrary.Gilded(amount); return true;
                case StatusId.Dread: app = StatusLibrary.Dread(amount); return true;
                default:
                    app = default;
                    return false;
            }
        }

        public const float WeakenCap = 0.3f;

        public override string Describe() => Status + " enchantment";
    }

    // ---------------------------------------------------------------- handling

    /// <summary>Kills put a round back in the gun in hand. Lock and Load.</summary>
    [System.Serializable]
    public class LockAndLoadBehaviour : BoonBehaviour
    {
        public int Rounds = 1;

        protected override void OnKill(Health victim, DamageInfo info)
        {
            if (Player.Weapon != null && !Player.Weapon.IsReloading) Player.Weapon.AddRounds(Rounds);
        }
    }

    /// <summary>While there is mana, rounds cost mana instead of ammo. Guns that already cost mana pay only that. Gunmage.</summary>
    [System.Serializable]
    public class GunmageBehaviour : BoonBehaviour
    {
        public float ManaPerRound = 1.5f;

        [System.NonSerialized] private Weapon _weapon;

        protected override void OnBind()
        {
            _weapon = Player.Weapon;
            if (_weapon != null) _weapon.RoundPayer = Pay;
        }

        protected override void OnUnbind()
        {
            if (_weapon != null && _weapon.RoundPayer == (System.Func<Weapon, int, bool>)Pay) _weapon.RoundPayer = null;
        }

        private bool Pay(Weapon weapon, int ammoCost)
        {
            if (ammoCost <= 0 || Player == null || Player.Mana == null) return false;
            if (weapon.Definition != null && weapon.Definition.ManaPerShot > 0f) return true;
            return Player.Mana.TrySpend(ManaPerRound * ammoCost);
        }
    }

    /// <summary>A third gun slot. Third Hand.</summary>
    [System.Serializable]
    public class ThirdHandBehaviour : BoonBehaviour
    {
        protected override void OnBind()
        {
            if (Player.Holster != null) Player.Holster.SetSlotCount(Holster.MaxSlots);
        }

        protected override void OnUnbind()
        {
            if (Player != null && Player.Holster != null) Player.Holster.SetSlotCount(Holster.BaseSlots);
        }
    }

    /// <summary>Each round also fires a second, much less accurate one. Bifurcator.</summary>
    [System.Serializable]
    public class BifurcatorBehaviour : BoonBehaviour
    {
        public float ExtraSpread = 8f;

        protected override void OnGunFired(WeaponShot shot)
        {
            if (shot.IsEcho || shot.IsPhantom || shot.Weapon == null) return;
            Vector3 direction = Weapon.ApplySpread(shot.Direction, shot.Spec.SpreadDegrees + ExtraSpread);
            shot.Weapon.FireEcho(shot.Spec, direction);
        }
    }

    /// <summary>The player's gun projectiles curve toward enemies. Magnetised Ammo.</summary>
    [System.Serializable]
    public class MagnetisedAmmoBehaviour : BoonBehaviour
    {
        public float Strength = 3f;
        public float Range = 20f;

        protected override void OnBind() => Projectile.AnyLaunched += OnLaunched;
        protected override void OnUnbind() => Projectile.AnyLaunched -= OnLaunched;

        private void OnLaunched(Projectile projectile)
        {
            if (projectile == null || projectile.SourceWeapon == null || !IsPlayer(projectile.Owner) || projectile.HomingEnabled) return;
            projectile.HomingEnabled = true;
            projectile.HomingStrength = Strength;
            projectile.HomingRange = Range;
        }
    }

    /// <summary>The holstered gun fires alongside the one in hand, at half damage. Mirror Barrel.</summary>
    [System.Serializable]
    public class MirrorBarrelBehaviour : BoonBehaviour
    {
        public float DamageScale = 0.5f;

        [System.NonSerialized] private PhantomWeapon _phantom;
        public PhantomWeapon Phantom => _phantom;

        protected override void OnBind()
        {
            if (Player.Holster == null || Player.Weapon == null) return;

            _phantom = PhantomWeapon.Create(Player.Holster, Player.Weapon.AimOrigin, Player.gameObject,
                useOwnerStats: true, shareInfusions: true, followOtherHand: true);
            _phantom.Weapon.DamageScale = DamageScale;
        }

        protected override void OnUnbind()
        {
            if (_phantom != null) _phantom.Dismiss();
            _phantom = null;
        }

        protected override void OnGunFired(WeaponShot shot)
        {
            if (shot.IsEcho || shot.IsPhantom || _phantom == null || _phantom.Weapon.Definition == null) return;

            // One copy volley per trigger pull, not one per pellet.
            if (shot.Weapon != Player.Weapon || _firedThisFrame == Time.frameCount && Application.isPlaying) return;
            _firedThisFrame = Time.frameCount;
            _phantom.Weapon.FireNow();
        }

        [System.NonSerialized] private int _firedThisFrame = -1;
    }

    /// <summary>Headshots explode. Every pellet that lands on a head explodes; the blast never hurts you. Birthday Party.</summary>
    [System.Serializable]
    public class BirthdayPartyBehaviour : BoonBehaviour
    {
        public float Radius = 3f;
        public float DamageShare = 1.5f;

        private static readonly Color Tint = new Color(1f, 0.6f, 0.9f);

        protected override void OnGunHit(WeaponHit hit)
        {
            if (!hit.Damage.IsHeadshot) return;
            Blast(hit.Point, Radius, hit.Damage.Amount * DamageShare, DamageType.Energy, Tint);
        }
    }

    /// <summary>A gun hit sends a bolt to a nearby enemy for a share of the hit. The bolt sets off nothing. Chain Static.</summary>
    [System.Serializable]
    public class ChainStaticBehaviour : BoonBehaviour
    {
        public float SharePerLevel = 0.3f;
        public float Range = 8f;
        public float Cooldown = 0.2f;

        [System.NonSerialized] private float _ready = float.NegativeInfinity;
        public int Bolts { get; private set; }

        protected override void OnGunHit(WeaponHit hit)
        {
            if (Now < _ready || hit.Target == null || hit.Target.Transform == null) return;

            Health struck = hit.Target.Transform.GetComponent<Health>();
            Health next = NearestEnemy(hit.Point, Range, struck);
            if (next == null) return;

            _ready = Now + Cooldown;
            Bolts++;

            Vector3 at = next.transform.position + Vector3.up;
            DamageInfo bolt = PlayerDamage(hit.Damage.Amount * SharePerLevel * Level, DamageType.Energy, DamageOrigin.Mastery, at);
            next.TakeDamage(bolt.From(hit.Point));

            if (Application.isPlaying) Combat.SpawnTracer(hit.Point, at, new Color(0.6f, 0.8f, 1f), 0.06f, 0.15f);
        }
    }

    /// <summary>Gun hits push the target back a little. Concussive.</summary>
    [System.Serializable]
    public class ConcussiveBehaviour : BoonBehaviour
    {
        public float PerLevel = 1.5f;

        protected override void OnGunHit(WeaponHit hit)
        {
            if (hit.Target == null || hit.Target.Transform == null) return;
            IKnockable body = hit.Target.Transform.GetComponent<IKnockable>();
            if (body == null) return;

            Vector3 push = Vector3.ProjectOnPlane(hit.Direction, Vector3.up).normalized * PerLevel * Level;
            body.AddKnockback(push, Player.gameObject, Team.Player);
        }
    }
}
