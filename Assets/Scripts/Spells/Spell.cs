using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>Everything a spell needs to know about whoever is casting it.</summary>
    public class SpellContext
    {
        public GameObject Caster;
        public Team Team = Team.Player;
        public CharacterSheet Sheet;
        public Mana Mana;
        public Health Health;
        public PlayerMotor Motor;
        public PlayerLook Look;
        public Transform Aim;               // camera transform: cast origin and direction
        public CharacterController Controller;

        /// <summary>Extra on-hit statuses granted by boons, applied by damaging spells.</summary>
        public List<StatusApplication> ExtraStatuses;

        public float SpellPower => Sheet != null ? Combat.OutgoingMultiplier(Sheet, true) : 1f;
        public Vector3 Origin => Aim != null ? Aim.position : Caster.transform.position + Vector3.up * 1.5f;
        public Vector3 Forward => Aim != null ? Aim.forward : Caster.transform.forward;

        public List<StatusApplication> Combine(params StatusApplication[] own)
        {
            var list = new List<StatusApplication>();
            if (own != null) list.AddRange(own);
            if (ExtraStatuses != null) list.AddRange(ExtraStatuses);
            return list;
        }
    }

    /// <summary>
    /// A castable ability bound to a spell slot. Stateless: one instance is shared by every
    /// caster, so keep per-cast state on the context or in spawned objects.
    /// </summary>
    public abstract class Spell
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        public virtual float ManaCost => 20f;
        public virtual float Cooldown => 6f;
        public virtual Color Tint => Palette.Arcane;

        /// <summary>Short label drawn on the HUD slot.</summary>
        public virtual string ShortName => DisplayName.Length <= 4 ? DisplayName : DisplayName.Substring(0, 4);

        /// <summary>Return false to refund the cast (no cooldown, no mana spent).</summary>
        public abstract bool Cast(SpellContext ctx);
    }
}
