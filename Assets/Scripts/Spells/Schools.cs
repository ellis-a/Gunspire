using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// What a school looks like and what holding its spells grants. The colours are the icon palette's school
    /// primaries, so a tag in the menus matches the icon art beside it.
    ///
    /// A spell's own <see cref="Spell.Tint"/> stays its damage type or its authored override: that colours the
    /// projectile and the slot, and answers "what does this do". These answer "what is it part of".
    /// </summary>
    public static class Schools
    {
        public static Color Tint(SpellSchool school)
        {
            switch (school)
            {
                case SpellSchool.Elemental:  return new Color(1.00f, 0.42f, 0.18f);   // Ember Orange
                case SpellSchool.Bestial:    return new Color(0.79f, 0.56f, 0.31f);   // Hide Tan
                case SpellSchool.Abyssal:    return new Color(0.18f, 0.64f, 0.61f);   // Abyss Teal
                case SpellSchool.Divination: return new Color(1.00f, 0.95f, 0.76f);   // Halo Ivory
                case SpellSchool.Death:      return new Color(0.48f, 0.89f, 0.63f);   // Grave Green
                case SpellSchool.Psionic:    return new Color(0.89f, 0.36f, 0.88f);   // Psi Magenta
                case SpellSchool.Aetherics:  return new Color(0.43f, 0.39f, 1.00f);   // Warp Indigo
                default:                     return new Color(0.68f, 0.69f, 0.74f);   // Chalk Grey
            }
        }

        /// <summary>The name of the standing effect the school grants, or null for Petty, which grants none.</summary>
        public static string MasteryName(SpellSchool school)
        {
            switch (school)
            {
                case SpellSchool.Elemental:  return "Conflux";
                case SpellSchool.Bestial:    return "Beast Companion";
                case SpellSchool.Abyssal:    return "Blood Debt";
                case SpellSchool.Divination: return "Divine Knowledge";
                case SpellSchool.Death:      return "Souls";
                case SpellSchool.Psionic:    return "Psi Blades";
                case SpellSchool.Aetherics:  return "Arcane Warp";
                default:                     return null;
            }
        }

        /// <summary>What that effect does, in the terms the player sees it in.</summary>
        public static string MasterySummary(SpellSchool school)
        {
            switch (school)
            {
                case SpellSchool.Elemental:
                    return "Two elements on one enemy set off a reaction. Holding more Elemental spells makes the "
                           + "burst bigger, and at three it detonates around the target.";
                case SpellSchool.Bestial:
                    return "A beast fights beside you, climbing from jackalope to fox, wolf and bear as you hold more "
                           + "Bestial spells. One that dies stays gone until the next floor.";
                case SpellSchool.Abyssal:
                    return "Health your spells spend becomes debt, and the debt charges interest. Kills repay it, and "
                           + "while you owe it your spells hit harder.";
                case SpellSchool.Divination:
                    return "Enemies show you their health, then their sight and hearing before they notice you, then a "
                           + "timer on their next attack.";
                case SpellSchool.Death:
                    return "Kills leave souls behind, up to a cap that rises with the school. Death spells spend them.";
                case SpellSchool.Psionic:
                    return "Hits from your gun build psi charge. It empowers your next melee swing, and Force of Will "
                           + "spends it to throw enemy shots back.";
                case SpellSchool.Aetherics:
                    return "The mana you are missing becomes spell power, and every gun hit pays a little mana back.";
                default:
                    return "Petty spells belong to no school and count toward no mastery.";
            }
        }

        /// <summary>How many of the school are equipped, out of the four that count. Zero without a mastery to ask.</summary>
        public static int RankFor(PlayerRig player, SpellSchool school)
        {
            if (player == null || player.Masteries == null || school == SpellSchool.Petty) return 0;

            Mastery mastery = player.Masteries.For(school);
            return mastery != null ? mastery.Rank : 0;
        }
    }
}
