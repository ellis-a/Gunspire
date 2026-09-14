using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Every spell in the game, as data. A spell is its identity plus a chain of
    /// <see cref="AbilityEffect"/>s; the effects themselves are shared with enemy attacks.
    ///
    /// Adding a spell is one entry here. It needs new C# only when it wants behaviour no
    /// existing effect provides.
    ///
    /// The built-ins live in code so a fresh clone runs with nothing authored. Any
    /// <see cref="SpellAsset"/> found under a Resources folder is merged in: a matching id
    /// replaces the built-in, a new id is added to the roster.
    /// </summary>
    public static class SpellLibrary
    {
        private static List<Spell> _all;

        public static IReadOnlyList<Spell> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        /// <summary>What every run opens with in the slots that are never empty.</summary>
        public const string DefaultMovementId = "dash";
        public const string DefaultMeleeId = "bash";

        public static Spell DefaultMovement => Get(DefaultMovementId);
        public static Spell DefaultMelee => Get(DefaultMeleeId);

        /// <summary>Drops the cached roster so authored assets are picked up again.</summary>
        public static void Reload() => _all = null;

        private static void Build()
        {
            // Assign before merging: anything that reads the roster while assets are loading
            // gets the built-ins rather than recursing into a half-built list.
            _all = BuiltIn();

            SpellAsset[] authored = Resources.LoadAll<SpellAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                Spell def = authored[i] != null ? authored[i].Definition : null;
                if (def == null) continue;

                if (string.IsNullOrEmpty(def.Id))
                {
                    Debug.LogWarning("Spell asset \"" + authored[i].name + "\" has no Id and was ignored.");
                    continue;
                }

                int existing = _all.FindIndex(s => s.Id == def.Id);
                if (existing >= 0) _all[existing] = def;
                else _all.Add(def);
            }
        }

        public static Spell Get(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>
        /// Spells a shrine can still offer: never learned, or learned but below their level
        /// cap. Taking one you already know levels it instead of rebinding it.
        /// </summary>
        public static List<Spell> Offerable(SpellBook book, SpellSlot slot = SpellSlot.Cast)
        {
            var list = new List<Spell>();

            for (int i = 0; i < All.Count; i++)
            {
                Spell spell = All[i];
                if (spell.Slot != slot) continue;

                // Only the cast slots stack levels, and only they are tracked by the book.
                // A movement or melee spell is a straight swap, so the caller drops whichever
                // one is already bound rather than the book knowing about it.
                if (slot == SpellSlot.Cast && book != null && !book.CanTake(spell)) continue;

                list.Add(spell);
            }

            return list;
        }

        /// <summary>Every spell that can go in one slot, ignoring what is already bound.</summary>
        public static List<Spell> ForSlot(SpellSlot slot)
        {
            var list = new List<Spell>();
            for (int i = 0; i < All.Count; i++)
                if (All[i].Slot == slot) list.Add(All[i]);
            return list;
        }

        /// <summary>Rolls a rarity from Luck, then picks an offerable spell at that tier.</summary>
        public static Spell RollOffer(Rng rng, SpellBook book, float luck, float rarityBonus = 1f,
            SpellSlot slot = SpellSlot.Cast)
        {
            List<Spell> pool = Offerable(book, slot);
            if (pool.Count == 0) return null;

            Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
            return Rarities.PickOfRarity(rng, pool, s => s.Rarity, rolled);
        }

        /// <summary>
        /// Rolls several distinct spells at once, for a pick-one-of-N screen. Returns fewer
        /// than asked for only if the roster genuinely runs out, so a caller that needs a
        /// guaranteed offer should check the count rather than assume it.
        /// </summary>
        public static List<Spell> OfferDistinct(Rng rng, SpellBook book, float luck, int count,
            float rarityBonus = 1f, SpellSlot slot = SpellSlot.Cast)
        {
            var chosen = new List<Spell>();
            List<Spell> pool = Offerable(book, slot);

            int guard = 0;
            while (chosen.Count < count && pool.Count > 0 && guard++ < 200)
            {
                Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
                Spell pick = Rarities.PickOfRarity(rng, pool, s => s.Rarity, rolled);
                if (pick == null) break;

                chosen.Add(pick);
                pool.Remove(pick);
            }

            return chosen;
        }

        // ---------------------------------------------------------------- roster

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<Spell> BuiltIn()
        {
            return new List<Spell>
            {
                // No cast spells until the designed ones are built. The spells from before the
                // school designs were retired, and the editor's RetiredSpells lists them so none
                // quietly comes back.

                // Shift slot.
                Dash(),
                Blink(),
                SpiderLegs(),

                // Melee slot.
                Bash()
            };
        }

        // ---------------------------------------------------------------- movement

        private static Spell Dash() => new Spell
        {
            Id = "dash",
            School = SpellSchool.Petty,
            DisplayName = "Dash",
            ShortName = "DASH",
            Description = "A short burst in the direction you are moving, with a sliver of " +
                          "invulnerability. Charges recover on their own.",
            Slot = SpellSlot.Movement,
            Type = SpellType.Mobility,
            Rarity = Rarity.Common,
            ManaCost = 6f,
            Cooldown = 0f,
            MaxLevel = 1,
            TintOverride = new Color(0.6f, 0.95f, 1f),
            OnCast = { new DashEffect() }
        };

        private static Spell Blink() => new Spell
        {
            Id = "blink",
            School = SpellSchool.Aetherics,
            DisplayName = "Blink",
            ShortName = "BLNK",
            Description = "Teleport forward, passing through anything in the way. Stops at the " +
                          "first wall rather than dropping you inside it.",
            Slot = SpellSlot.Movement,
            Type = SpellType.Mobility,
            Rarity = Rarity.Common,
            ManaCost = 16f,
            Cooldown = 4f,
            MaxLevel = 1,
            TintOverride = Palette.Arcane,
            OnCast =
            {
                // Aborts against a wall, which refunds the mana and the cooldown.
                // The reach the old Intellect scaling gave every loadout, which opened at 3.
                new SweepForwardEffect { BaseDistance = 9.54f, MaxDistance = 22f },
                new VfxGhostTrailEffect(),
                new TeleportEffect(),
                new GrantInvulnerabilityEffect { Seconds = 0.18f }
            }
        };

        private static Spell SpiderLegs() => new Spell
        {
            Id = "spider_legs",
            School = SpellSchool.Bestial,
            DisplayName = "Spider Legs",
            ShortName = "SPDR",
            Description = "Fires a line at whatever you are looking at and hauls you to it. " +
                          "Stick, and that surface becomes your new floor - jump and gravity " +
                          "pulls you straight back down onto it, not away.",
            Slot = SpellSlot.Movement,
            Type = SpellType.Mobility,
            Rarity = Rarity.Rare,
            MaxLevel = 1,
            TintOverride = new Color(0.55f, 0.9f, 0.5f),
            Sustain = new SustainProfile { ManaPerSecond = 11f, WallZip = true }
        };

        // ---------------------------------------------------------------- melee

        private static Spell Bash() => new Spell
        {
            Id = "bash",
            School = SpellSchool.Petty,
            DisplayName = "Bash",
            ShortName = "BASH",
            Description = "A quick, close swing. Cheap enough to fall back on when everything else " +
                          "is spent.",
            Slot = SpellSlot.Melee,
            Type = SpellType.Attack,
            DamageType = DamageType.Kinetic,
            Rarity = Rarity.Common,
            ManaCost = 4f,
            Cooldown = 0.7f,
            MaxLevel = 1,
            TintOverride = new Color(1f, 0.85f, 0.6f),
            OnCast =
            {
                new SelectConeEffect { Range = 3.2f, HalfAngle = 55f, RequireLineOfSight = false },

                // Damage and nothing else. The flat amount is what the old Strength scaling gave
                // every loadout, which all opened at 3 Strength.
                new DealDamageEffect { Amount = 20.5f, CanCrit = true }
            }
        };
    }
}
