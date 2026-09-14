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
        /// Spells a shrine can still offer: never learned, or learned but below their level cap, and
        /// never one the run has eliminated. Taking one you already know levels it instead of rebinding
        /// it. A movement or melee spell is offered unless it is the one equipped and already at its cap,
        /// since taking one that is not equipped is a swap. A variant is not offered while another
        /// version of it is equipped.
        ///
        /// Eliminated ids default to the current run's.
        /// </summary>
        public static List<Spell> Offerable(SpellBook book, SpellSlot slot = SpellSlot.Cast,
            ICollection<string> eliminated = null)
        {
            var list = new List<Spell>();
            if (eliminated == null && RunState.Current != null) eliminated = RunState.Current.EliminatedSpells;

            for (int i = 0; i < All.Count; i++)
            {
                Spell spell = All[i];
                if (spell.Slot != slot) continue;
                if (eliminated != null && eliminated.Contains(spell.Id)) continue;

                if (book != null)
                {
                    if (slot == SpellSlot.Cast ? !book.CanTake(spell) : book.IsEquipped(spell) && !book.CanTake(spell))
                        continue;

                    if (book.HasOtherVariant(spell)) continue;
                }

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
            => OfferDistinct(rng, Offerable(book, slot), luck, count, rarityBonus);

        /// <summary>
        /// Rolls distinct offers from a given pool. At most one Petty spell per offering while school
        /// spells remain to fill it; once they run out, Petty spells fill the rest, so a roster that is
        /// still mostly Petty does not shrink every offer to a single card.
        /// </summary>
        public static List<Spell> OfferDistinct(Rng rng, List<Spell> pool, float luck, int count,
            float rarityBonus = 1f)
        {
            var chosen = new List<Spell>();
            var remaining = new List<Spell>(pool);
            var candidates = new List<Spell>();

            int guard = 0;
            while (chosen.Count < count && remaining.Count > 0 && guard++ < 200)
            {
                bool pettyTaken = chosen.Exists(s => s.School == SpellSchool.Petty);

                candidates.Clear();
                for (int i = 0; i < remaining.Count; i++)
                    if (!pettyTaken || remaining[i].School != SpellSchool.Petty) candidates.Add(remaining[i]);
                if (candidates.Count == 0) candidates.AddRange(remaining);

                Rarity rolled = Rarities.Roll(rng, luck, rarityBonus);
                Spell pick = Rarities.PickOfRarity(rng, candidates, s => s.Rarity, rolled);
                if (pick == null) break;

                chosen.Add(pick);
                remaining.Remove(pick);
            }

            return chosen;
        }

        // ---------------------------------------------------------------- roster

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<Spell> BuiltIn()
        {
            return new List<Spell>
            {
                // Cast slots. Only the Petty spells so far. The spells from before the school
                // designs were retired, and the editor's RetiredSpells lists them so none
                // quietly comes back.
                Dart(),
                Orb(),
                Sleep(),
                Dazzle(),
                Shock(),
                Rot(),

                // Shift slot.
                Dash(),
                Blink(),
                SpiderLegs(),

                // Melee slot.
                Bash()
            };
        }

        // ---------------------------------------------------------------- cast: Petty

        // Filler and starter spells that belong to no school, so they feed no mastery. Every
        // number here is a first guess that has not been tuned in play.

        private static Spell Dart() => new Spell
        {
            Id = "dart",
            School = SpellSchool.Petty,
            DisplayName = "Dart",
            ShortName = "DART",
            Description = "A quick bolt that flies straight and hits hard for its price.",
            Type = SpellType.Attack,
            DamageType = DamageType.Kinetic,
            Rarity = Rarity.Common,
            ManaCost = 8f,
            Cooldown = 1.2f,
            MaxLevel = 3,
            OnCast =
            {
                new SpawnProjectileEffect { Damage = 22f, Speed = 70f, Radius = 0.12f, Lifetime = 2.5f }
            }
        };

        private static Spell Orb() => new Spell
        {
            Id = "orb",
            School = SpellSchool.Petty,
            DisplayName = "Orb",
            ShortName = "ORB",
            Description = "Lob a crackling orb that bursts where it lands.",
            Type = SpellType.Attack,
            DamageType = DamageType.Energy,
            Rarity = Rarity.Common,
            ManaCost = 18f,
            Cooldown = 5f,
            MaxLevel = 3,
            OnCast =
            {
                new SpawnProjectileEffect
                {
                    // The orb itself carries nothing; the burst is the whole spell.
                    Damage = 0f,
                    Speed = 24f,
                    Gravity = 12f,
                    Radius = 0.3f,
                    Lifetime = 4f,
                    OnHit =
                    {
                        new SelectSphereEffect { Radius = 3.5f, AroundPoint = true, ScaleRadiusWithLevel = true },
                        new DealDamageEffect { Amount = 30f, FalloffFromPoint = true, FalloffRadius = 3.5f, MinFraction = 0.4f },
                        new VfxSphereEffect { Diameter = 1.4f, Alpha = 0.5f, Lifetime = 0.25f, GrowPerSecond = 9f }
                    }
                }
            }
        };

        private static Spell Sleep() => new Spell
        {
            Id = "sleep",
            School = SpellSchool.Petty,
            DisplayName = "Sleep",
            ShortName = "SLEP",
            Description = "A soft bolt that puts one enemy to sleep for ten seconds. Any damage wakes it.",
            Type = SpellType.Control,
            DamageType = DamageType.Energy,
            Rarity = Rarity.Common,
            ManaCost = 16f,
            Cooldown = 12f,
            MaxLevel = 3,
            GrowthPerLevel = 0f,
            NoiseMultiplier = 0.3f,
            TintOverride = new Color(0.5f, 0.6f, 0.9f),
            OnCast =
            {
                // No damage, as designed. A hit with nothing in it only delivers the sleep, so it
                // neither alerts the target nor sets off a death mark.
                new StatusPayloadEffect { Status = StatusId.Sleep, Duration = 10f, Stacks = 1, Magnitude = 1f },
                new SpawnProjectileEffect { Damage = 0f, Speed = 40f, Radius = 0.2f, Lifetime = 3f }
            }
        };

        private static Spell Dazzle() => new Spell
        {
            Id = "dazzle",
            School = SpellSchool.Petty,
            DisplayName = "Dazzle",
            ShortName = "DAZL",
            Description = "A flash in one enemy's eyes. It loses sight of you, and keeps fighting " +
                          "wherever it last saw you.",
            Type = SpellType.Control,
            DamageType = DamageType.Energy,
            Rarity = Rarity.Common,
            ManaCost = 14f,
            Cooldown = 10f,
            MaxLevel = 3,
            GrowthPerLevel = 0f,
            LevelUpNote = "and a longer blind",
            NoiseMultiplier = 0.5f,
            TintOverride = new Color(1f, 0.95f, 0.7f),
            OnCast =
            {
                new SelectNearestAimedEffect { Range = 30f, MaxAngle = 12f },
                new StatusPayloadEffect { Status = StatusId.Blind, Duration = 4f, DurationPerLevel = 0.5f, Stacks = 1, Magnitude = 1f },
                new ApplyPayloadEffect(),
                new VfxLineToTargetsEffect(),
                new VfxOnTargetsEffect { Size = new Vector3(1f, 1f, 1f), Alpha = 0.6f, Lifetime = 0.25f }
            }
        };

        private static Spell Shock() => new Spell
        {
            Id = "shock",
            School = SpellSchool.Petty,
            DisplayName = "Shock",
            ShortName = "SHCK",
            Description = "A crackling fan in front of you. Shocked enemies take more damage and hear far less.",
            Type = SpellType.Control,
            DamageType = DamageType.Energy,
            Rarity = Rarity.Uncommon,
            ManaCost = 20f,
            Cooldown = 7f,
            MaxLevel = 3,
            GrowthPerLevel = 0f,
            LevelUpNote = "and one more stack",
            TintOverride = new Color(0.7f, 0.75f, 1f),
            OnCast =
            {
                new SelectConeEffect { Range = 11f, HalfAngle = 30f },
                new StatusPayloadEffect { Status = StatusId.Shock, Duration = 5f, Stacks = 1, StacksPerLevel = 1f, Magnitude = 0.12f },
                new ApplyPayloadEffect(),
                new VfxConeEffect { Range = 11f, HalfAngle = 30f },
                new VfxShardsEffect { Range = 11f, SpreadDegrees = 30f }
            }
        };

        private static Spell Rot() => new Spell
        {
            Id = "rot",
            School = SpellSchool.Petty,
            DisplayName = "Rot",
            ShortName = "ROT",
            Description = "A breath of rot. Poisoned enemies cannot hold their aim, though standing " +
                          "still shakes it off faster.",
            Type = SpellType.Control,
            DamageType = DamageType.Necrotic,
            Rarity = Rarity.Uncommon,
            ManaCost = 20f,
            Cooldown = 7f,
            MaxLevel = 3,
            GrowthPerLevel = 0f,
            LevelUpNote = "and one more stack",
            TintOverride = new Color(0.55f, 0.9f, 0.35f),
            OnCast =
            {
                new SelectConeEffect { Range = 11f, HalfAngle = 30f },
                new StatusPayloadEffect { Status = StatusId.Poison, Duration = 7f, Stacks = 3, StacksPerLevel = 1f, Magnitude = 1.4f },
                new ApplyPayloadEffect(),
                new VfxConeEffect { Range = 11f, HalfAngle = 30f }
            }
        };

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

            // Each level past the first adds a charge. The DashLevels migration carries this onto the asset.
            MaxLevel = 3,
            LevelUpNote = "and one more charge",
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
