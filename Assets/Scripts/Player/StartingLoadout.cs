namespace WizardGun
{
    /// <summary>
    /// The single definition of what a wizard begins a run with.
    ///
    /// Both the first spawn and every restart go through here, so the opening kit is changed
    /// in exactly one place. Restart-specific cleanup (clearing boons, reviving, refilling)
    /// stays in <see cref="GameDirector"/>; this type only decides what the kit *is*.
    /// </summary>
    public static class StartingLoadout
    {
        /// <summary>Every core stat starts here. See <see cref="CharacterSheet"/> for what each one buys.</summary>
        public const int BaseStat = 5;

        /// <summary>Id from <see cref="WeaponLibrary"/>. Also the gun excluded from world drops.</summary>
        public const string WeaponId = "arcanum";

        /// <summary>Spell id per slot, indexed to match <see cref="SpellBook.SlotKeys"/> (Q, E).</summary>
        public static readonly string[] SpellIdsBySlot = { "blink", "cone_of_cold" };

        public static void ApplyStats(CharacterSheet sheet)
        {
            if (sheet == null) return;
            for (int i = 0; i < EnumCache.Stats.Length; i++)
                sheet.SetBaseStat(EnumCache.Stats[i], BaseStat);
        }

        public static void ApplyWeapon(Weapon weapon)
        {
            if (weapon != null) weapon.Equip(WeaponLibrary.Get(WeaponId));
        }

        /// <summary>Empties the book first, so a restart cannot carry spells over from the last run.</summary>
        public static void ApplySpells(SpellBook book)
        {
            if (book == null) return;

            book.ResetBook();
            for (int slot = 0; slot < SpellIdsBySlot.Length && slot < SpellBook.SlotCount; slot++)
            {
                Spell spell = SpellLibrary.Get(SpellIdsBySlot[slot]);
                if (spell != null) book.Bind(spell, slot);
            }
        }

        public static void ApplyTo(PlayerRig rig)
        {
            if (rig == null) return;
            ApplyStats(rig.Sheet);
            ApplyWeapon(rig.Weapon);
            ApplySpells(rig.Book);
        }
    }
}
