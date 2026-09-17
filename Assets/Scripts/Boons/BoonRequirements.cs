using System.Collections.Generic;

namespace Gunspire
{
    // Gates on whether a boon is offered. Each reads the run as it stands; none of them ever
    // switches a taken boon off. "Equipped" means in a cast slot, the movement slot or the melee
    // slot, matching how masteries count.

    /// <summary>At least one spell of a school is equipped. Every school boon carries one.</summary>
    [System.Serializable]
    public class SchoolEquippedRequirement : BoonRequirement
    {
        public SpellSchool School = SpellSchool.Elemental;

        public override bool IsMet(RunState run)
        {
            SpellBook book = BoonGates.Book(run);
            return book != null && book.SchoolCount(School) > 0;
        }

        public override string Describe() => "needs a " + School + " spell";
    }

    /// <summary>A particular spell is equipped. Spell boons carry one.</summary>
    [System.Serializable]
    public class SpellEquippedRequirement : BoonRequirement
    {
        public string SpellId = string.Empty;

        public override bool IsMet(RunState run) => BoonGates.Equipped(run, SpellId);

        public override string Describe() => "needs " + SpellId;
    }

    /// <summary>A gun of a class is in either hand.</summary>
    [System.Serializable]
    public class WeaponClassCarriedRequirement : BoonRequirement
    {
        public WeaponClass Class = WeaponClass.Handgun;

        public override bool IsMet(RunState run)
        {
            Holster holster = run != null && run.Player != null ? run.Player.Holster : null;
            if (holster == null) return false;

            for (int i = 0; i < Holster.SlotCount; i++)
            {
                WeaponDefinition gun = holster.GetSlot(i);
                if (gun != null && gun.Class == Class) return true;
            }
            return false;
        }

        public override string Describe() => "needs a " + Class;
    }

    /// <summary>Something is in the movement or melee slot.</summary>
    [System.Serializable]
    public class SlotFilledRequirement : BoonRequirement
    {
        public SpellSlot Slot = SpellSlot.Movement;

        public override bool IsMet(RunState run)
        {
            SpellBook book = BoonGates.Book(run);
            return book != null && book.GetEquipped(Slot) != null;
        }

        public override string Describe() => "needs a " + Slot.ToString().ToLowerInvariant() + " spell";
    }

    /// <summary>
    /// The build has minions to strengthen: a summon spell equipped, the Bestial mastery (whose
    /// companion is one), or a boon that brings its own.
    /// </summary>
    [System.Serializable]
    public class CanSummonRequirement : BoonRequirement
    {
        public List<string> SummonSpellIds = new List<string>
        {
            "raise_dead", "stitched_monstrosity", "eye_of_epheraxx", "phantasmal_mimic", "apocalypse"
        };

        /// <summary>Boons carrying this tag count as a summon, such as Beetle Swarm.</summary>
        public string SummonTag = BoonTags.Summon;

        public override bool IsMet(RunState run)
        {
            if (run == null) return false;
            if (run.HasTaggedBoon(SummonTag)) return true;

            SpellBook book = BoonGates.Book(run);
            if (book == null) return false;
            if (book.SchoolCount(SpellSchool.Bestial) > 0) return true;

            for (int i = 0; i < SummonSpellIds.Count; i++)
                if (BoonGates.Equipped(run, SummonSpellIds[i])) return true;
            return false;
        }

        public override string Describe() => "needs something to summon";
    }

    /// <summary>A gun that fires projectiles is in either hand.</summary>
    [System.Serializable]
    public class CarriesProjectileGunRequirement : BoonRequirement
    {
        public override bool IsMet(RunState run)
        {
            Holster holster = run != null && run.Player != null ? run.Player.Holster : null;
            if (holster == null) return false;

            for (int i = 0; i < Holster.SlotCount; i++)
            {
                WeaponDefinition gun = holster.GetSlot(i);
                if (gun != null && gun.Delivery == DeliveryKind.Projectile) return true;
            }
            return false;
        }

        public override string Describe() => "needs a projectile gun";
    }

    /// <summary>Only once another boon has been taken.</summary>
    [System.Serializable]
    public class HasBoonRequirement : BoonRequirement
    {
        public string BoonId = string.Empty;

        public override bool IsMet(RunState run) => run != null && run.HasBoon(BoonId);

        public override string Describe() => "needs the " + BoonId + " boon";
    }

    /// <summary>Never alongside a particular boon.</summary>
    [System.Serializable]
    public class NotWithBoonRequirement : BoonRequirement
    {
        public string BoonId = string.Empty;

        public override bool IsMet(RunState run) => run != null && !run.HasBoon(BoonId);

        public override string Describe() => "not with " + BoonId;
    }

    /// <summary>Only once a boon with a tag has been taken. Familiarity waits for a familiar.</summary>
    [System.Serializable]
    public class HasTagRequirement : BoonRequirement
    {
        public string Tag = string.Empty;

        public override bool IsMet(RunState run) => run != null && run.HasTaggedBoon(Tag);

        public override string Describe() => "needs a " + Tag + " boon";
    }

    /// <summary>Never alongside a boon with a tag: Cocky keeps health boons out while it is held.</summary>
    [System.Serializable]
    public class NotWithTagRequirement : BoonRequirement
    {
        public string Tag = string.Empty;

        public override bool IsMet(RunState run) => run != null && !run.HasTaggedBoon(Tag);

        public override string Describe() => "not with a " + Tag + " boon";
    }

    /// <summary>
    /// Caps how many boons with a tag can be held: one familiar, plus one for each level of the boon
    /// that raises the limit. Put it on the tagged boons themselves; a boon already held can still
    /// level up, since it is not a new one.
    /// </summary>
    [System.Serializable]
    public class TagLimitRequirement : BoonRequirement
    {
        public string Tag = BoonTags.Familiar;
        public int Limit = 1;

        /// <summary>Each level of this boon raises the limit by one. Empty for a fixed limit.</summary>
        public string RaisedByBoon = string.Empty;

        /// <summary>The boon this requirement sits on, so levelling a held one is not refused.</summary>
        public string OwnBoonId = string.Empty;

        public override bool IsMet(RunState run)
        {
            if (run == null) return false;
            if (!string.IsNullOrEmpty(OwnBoonId) && run.HasBoon(OwnBoonId)) return true;

            int limit = Limit + (string.IsNullOrEmpty(RaisedByBoon) ? 0 : run.BoonLevel(RaisedByBoon));
            return run.CountTaggedBoons(Tag) < limit;
        }

        public override string Describe() =>
            "at most " + Limit + " " + Tag + (string.IsNullOrEmpty(RaisedByBoon) ? "" : " (+" + RaisedByBoon + ")");
    }

    /// <summary>Tags the gates and limits read, kept in one place so a typo cannot split them.</summary>
    public static class BoonTags
    {
        public const string Familiar = "familiar";
        public const string Health = "health";
        public const string Summon = "summon";
    }

    /// <summary>Lookups the gates share.</summary>
    public static class BoonGates
    {
        public static SpellBook Book(RunState run) => run != null && run.Player != null ? run.Player.Book : null;

        public static bool Equipped(RunState run, string spellId)
        {
            SpellBook book = Book(run);
            if (book == null || string.IsNullOrEmpty(spellId)) return false;

            Spell spell = SpellLibrary.Get(spellId);
            return spell != null && book.IsEquipped(spell);
        }
    }
}
