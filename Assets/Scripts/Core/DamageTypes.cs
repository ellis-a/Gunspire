using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Metadata for each damage school: what it is called, what colour it reads as, and
    /// whether resistance applies to it.
    ///
    /// Adding a school is two edits - a member on <see cref="DamageType"/> and a case here.
    /// Everything else (resistances, boons, the character sheet, VFX tinting) walks
    /// <see cref="Elemental"/> and picks the new one up for free.
    /// </summary>
    public static class DamageTypes
    {
        public static readonly DamageType[] All = (DamageType[])Enum.GetValues(typeof(DamageType));

        /// <summary>Every school except True, which is bookkeeping rather than an element.</summary>
        public static readonly DamageType[] Elemental = BuildElemental();

        private static DamageType[] BuildElemental()
        {
            var list = new System.Collections.Generic.List<DamageType>();
            for (int i = 0; i < All.Length; i++)
                if (IsResistable(All[i])) list.Add(All[i]);
            return list.ToArray();
        }

        /// <summary>True damage bypasses resistance, vulnerability and invulnerability.</summary>
        public static bool IsResistable(DamageType type) => type != DamageType.True;

        public static string Name(DamageType type)
        {
            switch (type)
            {
                case DamageType.Kinetic:  return "Kinetic";
                case DamageType.Energy:   return "Energy";
                case DamageType.Psychic:  return "Psychic";
                case DamageType.Necrotic: return "Necrotic";
                default:                  return "True";
            }
        }

        public static Color Tint(DamageType type)
        {
            switch (type)
            {
                case DamageType.Kinetic:  return new Color(1.00f, 0.92f, 0.70f);
                case DamageType.Energy:   return new Color(1.00f, 0.58f, 0.20f);
                case DamageType.Psychic:  return new Color(0.85f, 0.45f, 0.95f);
                case DamageType.Necrotic: return new Color(0.45f, 0.80f, 0.40f);
                default:                  return Color.white;
            }
        }
    }
}
