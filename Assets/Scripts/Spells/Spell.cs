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

        /// <summary>Set by <see cref="SpellBook"/> immediately before a cast.</summary>
        public Spell CurrentSpell;

        /// <summary>Level of the spell being cast, 1-based. Set alongside <see cref="CurrentSpell"/>.</summary>
        public int SpellLevel = 1;

        /// <summary>
        /// Outgoing multiplier for the spell currently being cast, including the sheet bonuses
        /// for its damage school and its category.
        /// </summary>
        public float SpellPower
        {
            get
            {
                if (Sheet == null) return 1f;
                DamageType damage = CurrentSpell != null ? CurrentSpell.DamageType : DamageType.Astral;
                SpellType category = CurrentSpell != null ? CurrentSpell.Type : SpellType.Attack;
                return Combat.OutgoingMultiplier(Sheet, true, damage, category);
            }
        }

        /// <summary>Spell power folded together with the level growth of the spell being cast.</summary>
        public float ScaledPower
        {
            get
            {
                float growth = CurrentSpell != null ? CurrentSpell.LevelMultiplier(SpellLevel) : 1f;
                return SpellPower * growth;
            }
        }

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
    /// caster, so keep per-cast state on the context or in spawned objects. The level lives
    /// on the caster's <see cref="SpellBook"/>, not here.
    /// </summary>
    public abstract class Spell
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        public virtual float ManaCost => 20f;
        public virtual float Cooldown => 6f;
        public virtual Rarity Rarity => Rarity.Common;
        public virtual SpellType Type => SpellType.Attack;
        public virtual DamageType DamageType => DamageType.Astral;

        /// <summary>How many times it can be taken. Each pick past the first raises the level.</summary>
        public virtual int MaxLevel => 5;

        /// <summary>Damage and area growth per level past the first.</summary>
        public virtual float GrowthPerLevel => 0.22f;

        public virtual Color Tint => DamageTypes.Tint(DamageType);

        /// <summary>Short label drawn on the HUD slot.</summary>
        public virtual string ShortName => DisplayName.Length <= 4 ? DisplayName : DisplayName.Substring(0, 4);

        /// <summary>Multiplier applied to the spell's own numbers at a given level.</summary>
        public float LevelMultiplier(int level) => 1f + Mathf.Max(0, level - 1) * GrowthPerLevel;

        /// <summary>Cooldowns shorten slightly as a spell levels, to a floor of 60% of base.</summary>
        public float CooldownAtLevel(int level)
            => Cooldown * Mathf.Max(0.6f, 1f - Mathf.Max(0, level - 1) * 0.06f);

        /// <summary>One line describing what the next level buys, for the level-up screen.</summary>
        public virtual string LevelUpSummary(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return "Already at maximum level.";
            return string.Format("Level {0} to {1}: effect +{2:0}%, cooldown {3:0.0}s to {4:0.0}s",
                currentLevel, currentLevel + 1, GrowthPerLevel * 100f,
                CooldownAtLevel(currentLevel), CooldownAtLevel(currentLevel + 1));
        }

        /// <summary>Return false to refund the cast (no cooldown, no mana spent).</summary>
        public abstract bool Cast(SpellContext ctx);
    }
}
