using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The single definition of what a wizard begins a run with.
    ///
    /// Values come from a <see cref="StartingLoadoutAsset"/> in a Resources folder if one
    /// exists, and from the constants here if not. That keeps the kit editable in the
    /// Inspector without breaking the property that a fresh clone runs with no assets at all.
    ///
    /// Both the first spawn and every restart go through here. Restart-specific cleanup
    /// (clearing boons, reviving, refilling) stays in <see cref="GameDirector"/>; this type
    /// only decides what the kit *is*.
    /// </summary>
    public static class StartingLoadout
    {
        /// <summary>Where the asset is looked for, relative to any Resources folder.</summary>
        public const string ResourcePath = "StartingLoadout";

        // ---- used when no asset is present ----
        public const int DefaultBaseStat = 5;
        public const string DefaultWeaponId = "arcanum";
        public static readonly string[] DefaultSpellIdsBySlot = { "blink", "cone_of_cold" };

        private static StartingLoadoutAsset _asset;
        private static bool _resolved;

        /// <summary>The authored loadout, or null when the game is running on code defaults.</summary>
        public static StartingLoadoutAsset Asset
        {
            get
            {
                if (!_resolved) Resolve();
                return _asset;
            }
        }

        private static void Resolve()
        {
            _resolved = true;

            _asset = Resources.Load<StartingLoadoutAsset>(ResourcePath);
            if (_asset != null) return;

            // Be forgiving about the filename: any loadout anywhere under Resources will do.
            StartingLoadoutAsset[] found = Resources.LoadAll<StartingLoadoutAsset>("");
            if (found != null && found.Length > 0) _asset = found[0];
        }

        /// <summary>Drops the cached asset so the next read picks up a change.</summary>
        public static void Reload()
        {
            _resolved = false;
            _asset = null;
        }

        // ---------------------------------------------------------------- values

        public static int StatFor(StatType stat)
            => Asset != null ? Mathf.Max(0, Asset.StatFor(stat)) : DefaultBaseStat;

        /// <summary>Id from <see cref="WeaponLibrary"/>. Also the gun excluded from world drops.</summary>
        public static string WeaponId
        {
            get
            {
                StartingLoadoutAsset asset = Asset;
                if (asset == null || string.IsNullOrEmpty(asset.WeaponId)) return DefaultWeaponId;
                return asset.WeaponId;
            }
        }

        /// <summary>Spell id per slot, indexed to match <see cref="SpellBook.SlotKeys"/> (Q, E).</summary>
        public static IReadOnlyList<string> SpellIdsBySlot
        {
            get
            {
                StartingLoadoutAsset asset = Asset;
                if (asset == null || asset.SpellIdsBySlot == null || asset.SpellIdsBySlot.Length == 0)
                    return DefaultSpellIdsBySlot;
                return asset.SpellIdsBySlot;
            }
        }

        // ---------------------------------------------------------------- application

        public static void ApplyStats(CharacterSheet sheet)
        {
            if (sheet == null) return;

            for (int i = 0; i < EnumCache.Stats.Length; i++)
            {
                StatType stat = EnumCache.Stats[i];
                sheet.SetBaseStat(stat, StatFor(stat));
            }
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

            IReadOnlyList<string> ids = SpellIdsBySlot;
            for (int slot = 0; slot < ids.Count && slot < SpellBook.SlotCount; slot++)
            {
                string id = ids[slot];
                Spell spell = SpellLibrary.Get(id);

                if (spell == null)
                {
                    // Silently leaving the slot empty is how a typo used to hide, so say so.
                    Debug.LogWarning("StartingLoadout: no spell with id \"" + id + "\" for slot "
                                     + SpellBook.SlotLabels[slot] + ". That slot will be empty. "
                                     + "Known ids: " + KnownSpellIds());
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
