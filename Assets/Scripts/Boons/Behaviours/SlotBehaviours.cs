using UnityEngine;

namespace Gunspire
{
    // Behaviours for the Slots boons: the cast slots, the movement slot and the melee slot.

    /// <summary>Whatever is cast from one slot is a level higher, past its cap. Quiss, Esarl and Fex.</summary>
    [System.Serializable]
    public class SlotLevelBehaviour : BoonBehaviour
    {
        public int Slot;

        [System.NonSerialized] private SpellBook _book;

        protected override void OnBind()
        {
            _book = Player.Book;
            if (_book != null) _book.AddSlotLevelBonus(Slot, 1);
        }

        protected override void OnUnbind()
        {
            if (_book != null) _book.AddSlotLevelBonus(Slot, -1);
        }

        public override string Describe() => SpellBook.SlotLabels[Mathf.Clamp(Slot, 0, SpellBook.SlotCount - 1)] + " slot +1 level";
    }

    /// <summary>Firing the last round in the magazine casts the spell on Q for free, cooldown or not. Spell Magazine.</summary>
    [System.Serializable]
    public class SpellMagazineBehaviour : BoonBehaviour
    {
        public int Slot;

        public int Casts { get; private set; }

        protected override void OnGunFired(WeaponShot shot)
        {
            if (shot.IsEcho || shot.IsPhantom || shot.Weapon == null || shot.AmmoCost <= 0) return;
            if (shot.Weapon.AmmoInMagazine > 0 || shot.Weapon.InfiniteAmmo) return;

            // A multi-pellet shot raises one event per pellet; only the first one empties the magazine.
            if (_lastFrame == Time.frameCount && Application.isPlaying) return;
            _lastFrame = Time.frameCount;

            SpellBook book = Player.Book;
            Spell spell = book != null ? book.GetSlot(Slot) : null;
            if (spell == null) return;

            Vector3 forward = Player.Weapon != null && Player.Weapon.AimOrigin != null
                ? Player.Weapon.AimOrigin.forward
                : Player.transform.forward;
            if (book.CastEcho(spell, Mathf.Max(1, book.GetSlotLevel(Slot)), forward)) Casts++;
        }

        [System.NonSerialized] private int _lastFrame = -1;
    }

    /// <summary>Each spell cast has a chance to go off again. Twincast.</summary>
    [System.Serializable]
    public class TwincastBehaviour : BoonBehaviour
    {
        public float Chance = 0.25f;

        public int Repeats { get; private set; }

        /// <summary>Set by tests to decide the roll.</summary>
        [System.NonSerialized] public System.Func<bool> RollOverride;

        protected override void OnSpellCast(Spell spell, int slot)
        {
            SpellBook book = Player.Book;
            if (book == null || spell == null) return;

            bool hit = RollOverride != null ? RollOverride() : Random.value < Chance;
            if (!hit) return;

            SpellBook.CastRecord last = book.LastCast;
            int level = last.Spell == spell ? last.Level : book.GetSlotLevel(slot);
            Vector3 forward = last.Spell == spell ? last.Forward : Player.transform.forward;
            float charge = last.Spell == spell ? last.Charge : 1f;
            if (book.CastEcho(spell, Mathf.Max(1, level), forward, charge)) Repeats++;
        }
    }

    /// <summary>The movement spell leaves a decoy where it started. Afterimage.</summary>
    [System.Serializable]
    public class AfterimageBehaviour : BoonBehaviour
    {
        [System.NonSerialized] private MovementController _movement;
        public AfterimageDecoy LastDecoy { get; private set; }

        protected override void OnBind()
        {
            _movement = Player.Movement;
            if (_movement != null) _movement.Used += OnUsed;
        }

        protected override void OnUnbind()
        {
            if (_movement != null) _movement.Used -= OnUsed;
        }

        private void OnUsed(Spell spell, Vector3 from) => LastDecoy = AfterimageDecoy.Spawn(from, Player.gameObject);
    }

    /// <summary>Kills reset the movement spell's cooldown. Tailwind.</summary>
    [System.Serializable]
    public class TailwindBehaviour : BoonBehaviour
    {
        protected override void OnKill(Health victim, DamageInfo info)
        {
            if (Player.Movement != null) Player.Movement.ResetCooldown();
        }
    }

    /// <summary>Melee damage heals you for a share of it. Gorelust.</summary>
    [System.Serializable]
    public class GorelustBehaviour : BoonBehaviour
    {
        public float SharePerLevel = 0.1f;

        protected override void OnAnyDamaged(Health victim, DamageInfo hit, float amount)
        {
            if (victim == null || victim.Team == Team.Player || hit.Origin != DamageOrigin.Melee || !IsOwnHit(hit)) return;
            if (PlayerHealth != null) PlayerHealth.Heal(amount * SharePerLevel * Level, silent: true);
        }
    }
}
