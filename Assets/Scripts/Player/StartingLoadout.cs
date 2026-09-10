using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
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

        /// <summary>
        /// Binds the one spell a run opens with. The other slot is deliberately left empty and
        /// gets filled at a shrine. Empties the book first, so a restart carries nothing over.
        /// </summary>
        public static void ApplySpells(SpellBook book)
        {
            LoadoutDefinition loadout = Selected;
            if (book == null || loadout == null) return;

            book.ResetBook();

            string id = loadout.SpellId;
            if (string.IsNullOrEmpty(id)) return;

            Spell spell = SpellLibrary.Get(id);
            if (spell == null)
            {
                // Silently leaving the slot empty is how a typo used to hide, so say so.
                Debug.LogWarning("Loadout \"" + loadout.Id + "\": no spell with id \"" + id
                                 + "\". The slot will be empty. Known ids: " + KnownSpellIds());
                return;
            }

            int slot = Mathf.Clamp(loadout.SpellSlot, 0, SpellBook.SlotCount - 1);
            book.Bind(spell, slot);
        }

        /// <summary>Puts the loadout's movement spell on Shift, defaulting to Dash.</summary>
        public static void ApplyMovement(MovementController controller)
        {
            LoadoutDefinition loadout = Selected;
            if (controller == null || loadout == null) return;

            Spell spell = Resolve(loadout.MovementAbilityId, SpellSlot.Movement,
                SpellLibrary.DefaultMovement, loadout.Id, "movement");

            controller.ResetState();
            controller.Equip(spell);
        }

        /// <summary>Puts the loadout's melee spell in the melee slot, defaulting to Bash.</summary>
        public static void ApplyMelee(PlayerCombat combat)
        {
            LoadoutDefinition loadout = Selected;
            if (combat == null || loadout == null) return;

            combat.EquipMelee(Resolve(loadout.MeleeSpellId, SpellSlot.Melee,
                SpellLibrary.DefaultMelee, loadout.Id, "melee"));
        }

        /// <summary>
        /// Looks an id up and checks it belongs in the slot it is being bound to. A wrong-slot
        /// id is the mistake a hand-edited loadout asset is most likely to make, and it would
        /// otherwise present as an empty slot with no explanation.
        /// </summary>
        private static Spell Resolve(string id, SpellSlot slot, Spell fallback,
            string loadoutId, string label)
        {
            // A blank id is a field nobody filled in rather than a typo. Loadout assets written
            // before this slot existed keep the default from the field initialiser, so this is
            // belt and braces - but a warning for an unset field would be noise, not a finding.
            if (string.IsNullOrEmpty(id)) return fallback;

            Spell spell = SpellLibrary.Get(id);

            if (spell == null)
            {
                Debug.LogWarning("Loadout \"" + loadoutId + "\": no spell with id \"" + id
                                 + "\". Falling back to " + (fallback != null ? fallback.DisplayName : "nothing") + ".");
                return fallback;
            }

            if (spell.Slot != slot)
            {
                Debug.LogWarning("Loadout \"" + loadoutId + "\": \"" + id + "\" is a " + spell.Slot
                                 + " spell, not " + label + ". Falling back to "
                                 + (fallback != null ? fallback.DisplayName : "nothing") + ".");
                return fallback;
            }

            return spell;
        }

        public static void ApplyTo(PlayerRig rig)
        {
            if (rig == null) return;
            ApplyStats(rig.Sheet);
            ApplyWeapon(rig.Weapon);
            ApplySpells(rig.Book);
            ApplyMovement(rig.Movement);
            ApplyMelee(rig.CombatInput);
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
