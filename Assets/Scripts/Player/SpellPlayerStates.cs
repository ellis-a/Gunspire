using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    // Spells that put the player into a state for a while: rewinding, a mirrored gun, a shade's lunge, a
    // reversing dash, and an animal form.

    /// <summary>
    /// Snaps the player back along their path to where they were three seconds ago, with health and ammo reset
    /// to exactly what they were. Refused with no history. Never echoed. Rewind.
    /// </summary>
    [System.Serializable]
    public class RewindEffect : AbilityEffect
    {
        public float Seconds = 0.75f;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            if (rig == null || rig.Rewind == null || rig.Rewind.Count < 2) return false;
            if (rig.GetComponent<RewindPlayback>() != null) return false;

            RewindPlayback.Begin(rig, Seconds);
            return true;
        }

        public override string Describe() => "rewinds three seconds";
    }

    /// <summary>
    /// The playback. The controller is off and the body moved directly back through the snapshots, so nothing
    /// blocks the old path, and the player is invulnerable and hidden throughout. The camera keeps its aim.
    /// On landing, whatever stands on the spot is shoved aside, then health and both guns' ammo come back.
    /// </summary>
    public class RewindPlayback : MonoBehaviour
    {
        private readonly List<RewindRecorder.Snapshot> _path = new List<RewindRecorder.Snapshot>();
        private readonly object _concealKey = new object();
        private PlayerRig _rig;
        private float _elapsed;
        private float _duration;

        public bool Landed { get; private set; }

        public static RewindPlayback Begin(PlayerRig rig, float seconds)
        {
            var playback = rig.gameObject.AddComponent<RewindPlayback>();
            playback._rig = rig;
            playback._duration = Mathf.Max(0.05f, seconds);

            for (int i = 0; i < rig.Rewind.Count; i++) playback._path.Add(rig.Rewind[i]);

            rig.Rewind.Paused = true;
            if (rig.Movement != null) rig.Movement.Deactivate();
            rig.Motor.EndWallZip();
            rig.Motor.BeginKinematic();
            if (rig.Concealment != null) rig.Concealment.Hide(playback._concealKey, fromSight: true, fromHearing: true, untargetable: true);

            playback.Step(0f);
            return playback;
        }

        private void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (Landed || _rig == null) return;

            _elapsed += dt;
            if (_rig.Health != null) _rig.Health.InvulnerabilityTimer = Mathf.Max(_rig.Health.InvulnerabilityTimer, 0.2f);

            float t = Mathf.Clamp01(_elapsed / _duration);
            float eased = t * t * (3f - 2f * t);

            // Newest to oldest. Blending between snapshots, so a sprint flashes past smoothly rather than stepping.
            float along = (_path.Count - 1) * (1f - eased);
            int lower = Mathf.FloorToInt(along);
            int upper = Mathf.Min(_path.Count - 1, lower + 1);
            Vector3 at = Vector3.Lerp(_path[lower].Position, _path[upper].Position, along - lower);
            _rig.Motor.MoveKinematic(at);

            if (t >= 1f) Land();
        }

        private void Land()
        {
            Landed = true;
            RewindRecorder.Snapshot oldest = _path[0];

            CharacterController body = _rig.Controller;
            LandingCheck.ShoveClear(oldest.Position, body != null ? body.radius : 0.4f, body != null ? body.height : 1.8f, _rig.gameObject);

            _rig.Motor.MoveKinematic(oldest.Position);
            _rig.Motor.EndKinematic();

            if (_rig.Health != null) _rig.Health.SetCurrent(oldest.Health);

            Holster holster = _rig.Holster;
            if (holster != null)
            {
                holster.SetActive(oldest.ActiveGun);
                holster.SetAmmo(0, oldest.AmmoFirst);
                holster.SetAmmo(1, oldest.AmmoSecond);
            }

            if (_rig.Concealment != null) _rig.Concealment.Release(_concealKey);
            _rig.Rewind.Clear();
            _rig.Rewind.Paused = false;

            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }
    }

    /// <summary>
    /// Summons your other weapon beside you, firing whenever you fire, with infinite ammo, while you cannot
    /// change weapons. Refused with an empty second hand. Carries your bullet infusions without spending them.
    /// Divine Assistance.
    /// </summary>
    [System.Serializable]
    public class MirrorGunEffect : AbilityEffect
    {
        public float Seconds = 8f;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            if (rig == null || rig.Holster == null || rig.Weapon == null) return false;
            if (rig.Holster.GetSlot((rig.Holster.ActiveIndex + 1) % Holster.SlotCount) == null) return false;

            MirrorGun.Begin(rig, Seconds * ctx.LevelScale);
            return true;
        }

        public override string Describe() => "your other gun fires beside you";
    }

    public class MirrorGun : MonoBehaviour
    {
        private PlayerRig _rig;
        private PhantomWeapon _phantom;
        private float _left;

        public PhantomWeapon Phantom => _phantom;
        public int Shots { get; private set; }

        public static MirrorGun Begin(PlayerRig rig, float seconds)
        {
            MirrorGun mirror = rig.GetComponent<MirrorGun>();
            if (mirror != null)
            {
                mirror._left = Mathf.Max(mirror._left, seconds);
                return mirror;
            }

            mirror = rig.gameObject.AddComponent<MirrorGun>();
            mirror._rig = rig;
            mirror._left = seconds;
            mirror._phantom = PhantomWeapon.Create(rig.Holster, rig.SpellContext != null ? rig.SpellContext.Aim : rig.transform,
                rig.gameObject, useOwnerStats: true, shareInfusions: true, followOtherHand: true);

            rig.Weapon.Fired += mirror.OnFired;
            rig.Holster.LockSwap(mirror);
            return mirror;
        }

        private void OnFired(WeaponShot shot)
        {
            if (this == null || shot.IsEcho || shot.IsPhantom || _phantom == null) return;
            _phantom.Weapon.FireNow();
            Shots++;
        }

        private void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if ((_left -= dt) > 0f) return;
            End();
        }

        public void End()
        {
            if (_rig != null)
            {
                if (_rig.Weapon != null) _rig.Weapon.Fired -= OnFired;
                if (_rig.Holster != null) _rig.Holster.UnlockSwap(this);
            }

            if (_phantom != null) _phantom.Dismiss();
            _phantom = null;

            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }
    }

    /// <summary>
    /// A quick lunge as a shade: passing through enemies without colliding, and weakening every enemy passed
    /// through. Anything left standing where it ends is shoved aside. Gravewalk.
    /// </summary>
    [System.Serializable]
    public class GravewalkEffect : AbilityEffect
    {
        public float Speed = 26f;
        public float Seconds = 0.3f;
        public float WeakenSeconds = 5f;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            if (rig == null || ctx.Motor == null) return false;

            ShadeLunge.Begin(rig, ctx.Forward, Speed, Seconds, ctx.Empower(StatusLibrary.Weaken(WeakenSeconds)), ctx.Team);
            return true;
        }

        public override string Describe() => "lunges through enemies, weakening them";
    }

    public class ShadeLunge : MonoBehaviour
    {
        private readonly HashSet<StatusController> _weakened = new HashSet<StatusController>();
        private PlayerRig _rig;
        private StatusApplication _weaken;
        private Team _team;
        private float _left;

        public int Weakened => _weakened.Count;

        public static ShadeLunge Begin(PlayerRig rig, Vector3 forward, float speed, float seconds, StatusApplication weaken, Team team)
        {
            ShadeLunge lunge = rig.GetComponent<ShadeLunge>();
            if (lunge == null) lunge = rig.gameObject.AddComponent<ShadeLunge>();

            lunge._rig = rig;
            lunge._left = seconds;
            lunge._weaken = weaken;
            lunge._team = team;

            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = rig.transform.forward;

            rig.Motor.SetPassThroughEnemies(true);
            rig.Motor.SuppressFriction(seconds);
            rig.Motor.AddImpulse(flat.normalized * speed);
            return lunge;
        }

        private void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (_rig == null) return;

            Collider[] found = Physics.OverlapSphere(_rig.transform.position + Vector3.up * 0.9f, 1f, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found.Length; i++)
            {
                StatusController status = found[i].GetComponentInParent<StatusController>();
                if (status != null && _weakened.Add(status)) status.Apply(_weaken, _rig.gameObject, _team);
            }

            if ((_left -= dt) > 0f) return;

            _rig.Motor.SetPassThroughEnemies(false);
            CharacterController body = _rig.Controller;
            LandingCheck.ShoveClear(_rig.transform.position, body != null ? body.radius : 0.4f, body != null ? body.height : 1.8f, _rig.gameObject);

            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }
    }

    /// <summary>
    /// A dash in the reverse direction that also turns around the forces on you. The open questions take
    /// their calmer defaults: standing still it goes backwards from where you face, only horizontal momentum
    /// is inverted, the camera does not turn, and it keeps Dash's sliver of invulnerability. Repulse.
    /// </summary>
    [System.Serializable]
    public class RepulseEffect : AbilityEffect
    {
        public override bool Execute(AbilityContext ctx)
        {
            if (ctx.Motor == null || ctx.Motor.DashCharges <= 0 || ctx.Caster == null) return false;

            Vector3 horizontal = Vector3.ProjectOnPlane(ctx.Motor.Velocity, ctx.Motor.UpAxis);
            Vector3 reverse = horizontal.sqrMagnitude > 1f
                ? -horizontal
                : -Vector3.ProjectOnPlane(ctx.Caster.transform.forward, ctx.Motor.UpAxis);

            ctx.Motor.InvertVelocity(includeVertical: false);
            return ctx.Motor.TryDash(reverse);
        }

        public override string Describe() => "dashes back, reversing your momentum";
    }

    /// <summary>
    /// Shifts into an animal until any spell key is pressed again. While shifted you cannot cast spells or use
    /// your gun; the animal's two actions are on the mouse buttons. Damage the animal takes is yours.
    /// Shapeshift, one spell per form.
    /// </summary>
    [System.Serializable]
    public class ShapeshiftEffect : AbilityEffect
    {
        public AnimalKind Form = AnimalKind.TyrantLizard;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig rig = ctx.Caster != null ? ctx.Caster.GetComponent<PlayerRig>() : null;
            if (rig == null || rig.Possession == null || rig.Possession.IsBusy) return false;

            AnimalForm body = AnimalForm.Spawn(Form, rig);
            if (rig.Possession.Begin(body, float.PositiveInfinity, endOnDirectDamage: false)) return true;

            Object.DestroyImmediate(body.gameObject);
            return false;
        }

        public override string Describe() => "shifts into " + Form;
    }
}
