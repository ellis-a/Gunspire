using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Which loadout the current run is being played with, and the one place it is applied.
    ///
    /// The roster lives in <see cref="LoadoutLibrary"/>; this holds the selection and applies
    /// it. Both the first spawn and every restart go through here. Restart-specific cleanup
    /// (clearing boons, reviving, refilling) stays in <see cref="GameDirector"/>.
    /// </summary>
    public static class StartingLoadout
    {
        private static LoadoutDefinition _selected;

        /// <summary>
        /// The chosen loadout. Falls back to the first on the roster so anything that reads it
        /// before the player has picked - a weapon drop roll, say - still gets a sane answer.
        /// </summary>
        public static LoadoutDefinition Selected
        {
            get
            {
                if (_selected == null && LoadoutLibrary.All.Count > 0) _selected = LoadoutLibrary.All[0];
                return _selected;
            }
        }

        public static void Select(LoadoutDefinition loadout)
        {
            if (loadout != null) _selected = loadout;
        }

        public static void Select(string id) => Select(LoadoutLibrary.Get(id));

        /// <summary>Id from <see cref="WeaponLibrary"/>. Also the gun excluded from world drops.</summary>
        public static string WeaponId
        {
            get
            {
                LoadoutDefinition loadout = Selected;
                return loadout != null && !string.IsNullOrEmpty(loadout.WeaponId)
                    ? loadout.WeaponId
                    : "arcanum";
            }
        }

        // ---------------------------------------------------------------- application

        public static void ApplyStats(CharacterSheet sheet)
        {
            LoadoutDefinition loadout = Selected;
            if (sheet == null || loadout == null) return;

            for (int i = 0; i < EnumCache.Stats.Length; i++)
            {
                StatType stat = EnumCache.Stats[i];
                sheet.SetBaseStat(stat, Mathf.Max(0, loadout.StatFor(stat)));
            }
        }

        public static void ApplyWeapon(Weapon weapon)
        {
            if (weapon != null) weapon.Equip(WeaponLibrary.Get(WeaponId));
        }

        /// <summary>Empties the book first, so a restart cannot carry spells over from the last run.</summary>
        public static void ApplySpells(SpellBook book)
        {
            LoadoutDefinition loadout = Selected;
            if (book == null || loadout == null) return;

            book.ResetBook();

            string[] ids = loadout.SpellIdsBySlot;
            if (ids == null) return;

            for (int slot = 0; slot < ids.Length && slot < SpellBook.SlotCount; slot++)
            {
                string id = ids[slot];
                Spell spell = SpellLibrary.Get(id);

                if (spell == null)
                {
                    // Silently leaving the slot empty is how a typo used to hide, so say so.
                    Debug.LogWarning("Loadout \"" + loadout.Id + "\": no spell with id \"" + id
                                     + "\" for slot " + SpellBook.SlotLabels[slot]
                                     + ". That slot will be empty. Known ids: " + KnownSpellIds());
                    continue;
                }

                book.Bind(spell, slot);
            }
        }

        public static void ApplyTo(PlayerRig rig)
        {
            if (rig == null) return;
            ApplyStats(rig.Sheet);
            ApplyWeapon(rig.Weapon);
            ApplySpells(rig.Book);
        }

        private static string KnownSpellIds()
        {
            string text = "";
            IReadOnlyList<Spell> all = SpellLibrary.All;
            for (int i = 0; i < all.Count; i++)
                text += (i > 0 ? ", " : "") + all[i].Id;
            return text;
        }
    }
}
