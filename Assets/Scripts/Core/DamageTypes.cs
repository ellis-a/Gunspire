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
                case DamageType.Normal: return "Normal";
                case DamageType.Fire:   return "Fire";
                case DamageType.Frost:  return "Frost";
                case DamageType.Nature: return "Nature";
                case DamageType.Shadow: return "Shadow";
                case DamageType.Astral: return "Astral";
                default:                return "True";
            }
        }

        public static Color Tint(DamageType type)
        {
            switch (type)
            {
                case DamageType.Fire:   return new Color(1.00f, 0.48f, 0.15f);
                case DamageType.Frost:  return new Color(0.55f, 0.85f, 1.00f);
                case DamageType.Nature: return new Color(0.55f, 0.90f, 0.35f);
                case DamageType.Shadow: return new Color(0.62f, 0.35f, 0.80f);
                case DamageType.Astral: return new Color(0.72f, 0.72f, 1.00f);
                case DamageType.Normal: return new Color(1.00f, 0.92f, 0.70f);
                default:                return Color.white;
            }
        }
    }
}
