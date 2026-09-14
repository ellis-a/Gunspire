using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Every status effect in the game, plus the lookup used by <see cref="StatusController"/>.
    /// Add a new effect by writing a definition and registering it in the static constructor.
    /// </summary>
    public static class StatusLibrary
    {
        private static readonly Dictionary<StatusId, StatusDefinition> Map =
            new Dictionary<StatusId, StatusDefinition>();

        static StatusLibrary()
        {
            Register(new FrostStatus());
            Register(new BleedStatus());
            Register(new BurnStatus());
            Register(new PoisonStatus());
            Register(new ShockStatus());
            Register(new WeakenStatus());
            Register(new HasteStatus());
            Register(new FortifyStatus());
            Register(new MarkStatus());
            Register(new DeathmarkStatus());
            Register(new EtherealStatus());
            Register(new SleepStatus());
            Register(new BlindStatus());

            // Planned: named so every id resolves, with no behaviour until the systems they need
            // exist. Verify Debuffs lists them, so nobody mistakes one for a finished effect.
            Register(new SnareStatus());
            Register(new SilenceStatus());
            Register(new DisarmStatus());
            Register(new FearStatus());
            Register(new ConfusionStatus());
            Register(new PlagueStatus());
            Register(new TormentStatus());

            // The spell designs' own statuses.
            Register(new EmpoweredStatus());
            Register(new ExposedStatus());
            Register(new QuickenedStatus());
            Register(new WitheredStatus());
            Register(new EnlargedStatus());
            Register(new ScentingStatus());
            Register(new ForetoldStatus());
            Register(new PhasedStatus());
            Register(new UnleashedStatus());
        }

        private static void Register(StatusDefinition def) => Map[def.Id] = def;

        public static StatusDefinition Get(StatusId id) => Map.TryGetValue(id, out var d) ? d : null;

        // Convenience constructors for the payloads weapons and spells hand out.
        /// <summary>Magnitude is the slow per stack, so the stack count is the whole story.</summary>
        public static StatusApplication Frost(float seconds = 4f, int stacks = 10)
            => new StatusApplication(StatusId.Frost, seconds, stacks, FrostStatus.SlowPerStack);

        /// <summary>Magnitude is damage per second, and it runs until something heals it off.</summary>
        public static StatusApplication Bleed(float seconds = 999f, int stacks = 1, float dps = 4f)
            => new StatusApplication(StatusId.Bleed, seconds, stacks, dps);

        /// <summary>Magnitude is the whole pool of burn, which halves every tick until spent.</summary>
        public static StatusApplication Burn(float seconds = 12f, int stacks = 1, float amount = 18f)
            => new StatusApplication(StatusId.Burn, seconds, stacks, amount);

        /// <summary>Magnitude is the sway in degrees per stack.</summary>
        public static StatusApplication Poison(float seconds = 7f, int stacks = 3, float swayPerStack = 1.4f)
            => new StatusApplication(StatusId.Poison, seconds, stacks, swayPerStack);

        public static StatusApplication Shock(float seconds = 4f, int stacks = 1)
            => new StatusApplication(StatusId.Shock, seconds, stacks, 0.12f);

        public static StatusApplication Weaken(float seconds = 5f, int stacks = 1)
            => new StatusApplication(StatusId.Weaken, seconds, stacks, 0.15f);

        public static StatusApplication Haste(float seconds = 3f, int stacks = 1, float perStack = 0.10f)
            => new StatusApplication(StatusId.Haste, seconds, stacks, perStack);

        public static StatusApplication Fortify(float seconds = 4f, int stacks = 1, float perStack = 0.15f)
            => new StatusApplication(StatusId.Fortify, seconds, stacks, perStack);

        public static StatusApplication Mark(float seconds = 6f)
            => new StatusApplication(StatusId.Mark, seconds, 1, 0.30f);

        public static StatusApplication Deathmark(float seconds = 2.5f)
            => new StatusApplication(StatusId.Deathmark, seconds, 1, 1f);

        public static StatusApplication Ethereal(float seconds = 5f)
            => new StatusApplication(StatusId.Ethereal, seconds, 1, 1f);

        /// <summary>Ten seconds by design. Halved on elites, and broken by direct damage.</summary>
        public static StatusApplication Sleep(float seconds = 10f)
            => new StatusApplication(StatusId.Sleep, seconds, 1, 1f);

        public static StatusApplication Blind(float seconds = 4f)
            => new StatusApplication(StatusId.Blind, seconds, 1, 1f);

        /// <summary>Magnitude is the slow, from nothing to everything. Use <see cref="Stun"/> for full strength.</summary>
        public static StatusApplication Snare(float seconds = 3f, float slow = 0.4f)
            => new StatusApplication(StatusId.Snare, seconds, 1, Mathf.Clamp01(slow));

        public static StatusApplication Stun(float seconds = 1.5f)
            => new StatusApplication(StatusId.Snare, seconds, 1, SnareStatus.StunMagnitude);

        /// <summary>Keep these short: a fleeing enemy is out of a short-range build's reach.</summary>
        public static StatusApplication Fear(float seconds = 3f)
            => new StatusApplication(StatusId.Fear, seconds, 1, 1f);

        public static StatusApplication Silence(float seconds = 3f)
            => new StatusApplication(StatusId.Silence, seconds, 1, 1f);

        public static StatusApplication Disarm(float seconds = 3f)
            => new StatusApplication(StatusId.Disarm, seconds, 1, 1f);

        public static StatusApplication Confusion(float seconds = 5f)
            => new StatusApplication(StatusId.Confusion, seconds, 1, 1f);

        public static StatusApplication Plague(float seconds = 12f)
            => new StatusApplication(StatusId.Plague, seconds, 1, 1f);

        /// <summary>Magnitude is psychic damage per second, per stack.</summary>
        public static StatusApplication Torment(float seconds = 6f, int stacks = 1, float dps = 6f)
            => new StatusApplication(StatusId.Torment, seconds, stacks, dps);

        public static StatusApplication Empowered(float seconds = 8f, float bonus = 0.25f)
            => new StatusApplication(StatusId.Empowered, seconds, 1, bonus);

        public static StatusApplication Exposed(float seconds = 8f, float extra = 0.25f)
            => new StatusApplication(StatusId.Exposed, seconds, 1, extra);

        public static StatusApplication Quickened(float seconds = 8f, float bonus = 0.3f)
            => new StatusApplication(StatusId.Quickened, seconds, 1, bonus);

        /// <summary>Magnitude is the share of maximum health below which it dies.</summary>
        public static StatusApplication Withered(float seconds = 8f, float threshold = 0.1f)
            => new StatusApplication(StatusId.Withered, seconds, 1, threshold);

        public static StatusApplication Enlarged(float seconds = 6f, float bonus = 0.3f)
            => new StatusApplication(StatusId.Enlarged, seconds, 1, bonus);

        public static StatusApplication Scenting(float seconds = 10f)
            => new StatusApplication(StatusId.Scenting, seconds, 1, 1f);

        public static StatusApplication Foretold(float seconds = 3f)
            => new StatusApplication(StatusId.Foretold, seconds, 1, 1f);

        public static StatusApplication Phased(float seconds = 0.6f)
            => new StatusApplication(StatusId.Phased, seconds, 1, 1f);

        public static StatusApplication Unleashed(float seconds = 8f)
            => new StatusApplication(StatusId.Unleashed, seconds, 1, 1f);
    }

    // -------------------------------------------------------------------------------- control

    /// <summary>
    /// Does nothing until it wakes: no moving, no attacking, no noticing anything. Direct damage
    /// wakes it, but not the hit that put it to sleep and not the ticks of its other statuses,
    /// and the damage that wakes it alerts it the ordinary way.
    /// </summary>
    public class SleepStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Sleep;
        public override string DisplayName => "Asleep";
        public override string Description => "Does nothing until it wakes. Damage wakes it.";
        public override Color Tint => new Color(0.5f, 0.6f, 0.9f);
        public override int MaxStacks => 1;
        public override bool EndsOnDamage => true;
        public override float EliteDurationScale => 0.5f;
    }

    /// <summary>
    /// Cannot see. An enemy that has not noticed you cannot spot you, though it can still hear
    /// you, and one already fighting keeps aiming at the spot it last saw you. Unlike sleep,
    /// damage does not end it.
    /// </summary>
    public class BlindStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Blind;
        public override string DisplayName => "Blinded";
        public override string Description => "Cannot see, and aims where it last saw its target.";
        public override Color Tint => new Color(0.25f, 0.25f, 0.3f);
        public override int MaxStacks => 1;
    }

    /// <summary>
    /// Snare and stun as one status. The magnitude is the slow, from nothing to everything, and at
    /// full strength the target is held still and cannot act - a stun. Two statuses would have had
    /// to be kept from contradicting each other.
    /// </summary>
    public class SnareStatus : StatusDefinition
    {
        public const float StunMagnitude = 1f;

        public override StatusId Id => StatusId.Snare;
        public override string DisplayName => "Snared";
        public override string Description => "Slowed. At full strength, stunned: cannot move or act.";
        public override Color Tint => new Color(0.55f, 0.45f, 0.3f);
        public override int MaxStacks => 1;

        /// <summary>Control bends elites rather than exempting them: a shorter stun, not none.</summary>
        public override float EliteDurationScale => 0.5f;

        public static bool Stuns(ActiveStatus s) => s != null && s.Magnitude >= StunMagnitude;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, -Mathf.Clamp01(s.Magnitude), s));
        }
    }

    /// <summary>
    /// Runs from whatever caused it, faster than it walks, and cannot attack. Landing it alerts the
    /// enemy and cancels any attack already winding up, so a slam that began a frame earlier does
    /// not still land.
    /// </summary>
    public class FearStatus : StatusDefinition
    {
        /// <summary>How much faster a feared enemy runs. A first guess.</summary>
        public const float SpeedBonus = 0.25f;

        public override StatusId Id => StatusId.Fear;
        public override string DisplayName => "Feared";
        public override string Description => "Flees faster, and cannot attack.";
        public override Color Tint => new Color(0.55f, 0.3f, 0.7f);
        public override int MaxStacks => 1;
        public override float EliteDurationScale => 0.5f;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, SpeedBonus, s));
        }
    }

    /// <summary>
    /// No spells. Enemies have attacks rather than spells, so on an enemy it stops ranged attacks:
    /// bolts, beams, breaths and blasts. Disarm takes the melee half.
    /// </summary>
    public class SilenceStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Silence;
        public override string DisplayName => "Silenced";
        public override string Description => "Cannot cast spells or use ranged attacks.";
        public override Color Tint => new Color(0.6f, 0.6f, 0.75f);
        public override int MaxStacks => 1;
    }

    /// <summary>No guns. On an enemy, which carries none, it stops melee attacks instead.</summary>
    public class DisarmStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Disarm;
        public override string DisplayName => "Disarmed";
        public override string Description => "Cannot shoot or use melee attacks.";
        public override Color Tint => new Color(0.75f, 0.6f, 0.45f);
        public override int MaxStacks => 1;
    }

    /// <summary>
    /// Cannot tell friend from foe: its attacks go out on the neutral team, so they land on its own
    /// kind and the player alike, and it picks targets from both. A direct hit snaps it out of it,
    /// though not the hit that applied it and not a status tick.
    /// </summary>
    public class ConfusionStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Confusion;
        public override string DisplayName => "Confused";
        public override string Description => "Cannot tell friend from foe. A hit snaps it out.";
        public override Color Tint => new Color(0.9f, 0.55f, 0.85f);
        public override int MaxStacks => 1;
        public override bool EndsOnDamage => true;
    }

    // -------------------------------------------------------------------------------- ice

    /// <summary>
    /// Stacking slow, and a countdown to being finished off. One percent per stack means the
    /// stack count reads directly as a percentage, and a target at a hundred is both completely
    /// immobile and one kinetic hit from dead - the old Freeze and its shatter, folded into the
    /// same number rather than being a second status that had to be kept in step with this one.
    /// </summary>
    public class FrostStatus : StatusDefinition
    {
        public const int FullStacks = 100;
        public const float SlowPerStack = 0.01f;

        /// <summary>Fraction of an elite's maximum health a shatter takes, instead of killing it.</summary>
        public const float EliteShatterFraction = 0.25f;

        public override StatusId Id => StatusId.Frost;
        public override string DisplayName => "Frostbitten";
        public override string Description =>
            "Slowed 1% per stack. At 100 stacks, kinetic damage finishes them.";
        public override Color Tint => new Color(0.55f, 0.85f, 1f);
        public override int MaxStacks => FullStacks;

        /// <summary>The slow per stack is fixed, so the stack count is the amount.</summary>
        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleStacks(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            float slow = Mathf.Clamp01(SlowPerStack * s.Stacks);
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, -slow, s));
        }
    }

    // -------------------------------------------------------------------------------- fire

    /// <summary>
    /// A pool of burn that spends itself. Each tick deals whatever is left and then halves it,
    /// so the first tick is the loud one and the tail is a diminishing echo - applying more burn
    /// tops the pool back up rather than extending a flat drip.
    /// </summary>
    public class BurnStatus : StatusDefinition
    {
        /// <summary>Below this the remaining pool is not worth a tick, so the status ends.</summary>
        private const float Spent = 0.5f;

        public override StatusId Id => StatusId.Burn;
        public override string DisplayName => "Burning";
        public override string Description => "Burns for the amount applied, halving every half second.";
        public override Color Tint => new Color(1f, 0.5f, 0.15f);
        public override int MaxStacks => 1;
        public override float TickInterval => 0.5f;

        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        public override void OnTick(StatusController c, ActiveStatus s)
        {
            c.DealTickDamage(s, s.Magnitude, DamageType.Energy);

            s.Magnitude *= 0.5f;
            if (s.Magnitude < Spent) c.Remove(Id);
        }
    }

    // -------------------------------------------------------------------------------- blood

    /// <summary>
    /// Will not stop on its own. Healing is the only answer, which is why it never expires on a
    /// timer - and why the floor ending clears it, so it cannot be carried the length of a run
    /// by a wizard who never finds a heal.
    /// </summary>
    public class BleedStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Bleed;
        public override string DisplayName => "Bleeding";
        public override string Description => "Bleeds until healed. Does not wear off.";
        public override Color Tint => new Color(0.75f, 0.15f, 0.2f);
        public override int MaxStacks => 5;
        public override float TickInterval => 0.5f;

        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        public override void OnTick(StatusController c, ActiveStatus s)
        {
            c.DealTickDamage(s, s.Magnitude * s.Stacks * TickInterval, DamageType.Kinetic);
        }
    }

    // -------------------------------------------------------------------------------- poison

    /// <summary>
    /// Ruins aim rather than dealing damage: the reticle drifts, and every shot goes where the
    /// reticle actually is. Holding still shakes it off twice as fast, so the counter-play is to
    /// stop and steady yourself - which is the last thing a fight wants you to do.
    /// </summary>
    public class PoisonStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Poison;
        public override string DisplayName => "Poisoned";
        public override string Description => "Aim sways. Falls off twice as fast while standing still.";
        public override Color Tint => new Color(0.55f, 0.9f, 0.35f);
        public override int MaxStacks => 6;

        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        /// <summary>Standing still shakes it off twice as fast.</summary>
        public override float DecayScale(StatusController c, ActiveStatus s)
            => c != null && c.Speed < 0.6f ? 2f : 1f;

        /// <summary>Degrees of drift at the current stack count.</summary>
        public static float SwayFor(StatusController c)
        {
            ActiveStatus s = c != null ? c.Find(StatusId.Poison) : null;
            return s == null ? 0f : s.Magnitude * s.Stacks;
        }
    }

    // -------------------------------------------------------------------------------- lightning

    /// <summary>
    /// Amplifies damage and deadens hearing. The second half is why it is worth applying to
    /// something that has not noticed you: a shocked enemy is easier to kill and worse at
    /// working out where the shooting is coming from.
    /// </summary>
    public class ShockStatus : StatusDefinition
    {
        /// <summary>How much of a shocked listener's hearing is lost, per stack.</summary>
        public const float DeafnessPerStack = 0.25f;

        public override StatusId Id => StatusId.Shock;
        public override string DisplayName => "Shocked";
        public override string Description => "Takes increased damage, and hears far less.";
        public override Color Tint => new Color(0.7f, 0.75f, 1f);
        public override int MaxStacks => 3;

        /// <summary>Only the damage amplification grows. Deafness stays per stack.</summary>
        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.DamageTaken, s.Magnitude * s.Stacks, s));
        }

        /// <summary>Fraction of normal hearing left. One when unshocked.</summary>
        public static float HearingScale(StatusController c)
        {
            int stacks = c != null ? c.Stacks(StatusId.Shock) : 0;
            return Mathf.Clamp01(1f - DeafnessPerStack * stacks);
        }
    }

    // -------------------------------------------------------------------------------- curses and buffs

    public class WeakenStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Weaken;
        public override string DisplayName => "Weakened";
        public override string Description => "Deals less damage.";
        public override Color Tint => new Color(0.6f, 0.45f, 0.7f);
        public override int MaxStacks => 3;

        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.DamageDealt, -s.Magnitude * s.Stacks, s));
        }
    }

    public class HasteStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Haste;
        public override string DisplayName => "Hasted";
        public override string Description => "Moves faster.";
        public override Color Tint => new Color(1f, 0.9f, 0.4f);
        public override int MaxStacks => 5;
        public override bool IsDebuff => false;

        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, s.Magnitude * s.Stacks, s));
        }
    }

    public class FortifyStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Fortify;
        public override string DisplayName => "Fortified";
        public override string Description => "Takes reduced damage.";
        public override Color Tint => new Color(0.9f, 0.8f, 0.5f);
        public override int MaxStacks => 3;
        public override bool IsDebuff => false;

        /// <summary>Damage taken is clamped on the sheet, so a huge ward still cannot heal you.</summary>
        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.DamageTaken, -s.Magnitude * s.Stacks, s));
        }
    }

    /// <summary>Consumed by the next hit, which lands for extra damage.</summary>
    public class MarkStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Mark;
        public override string DisplayName => "Marked";
        public override string Description => "The next hit deals bonus damage.";
        public override Color Tint => new Color(1f, 0.35f, 0.45f);
        public override int MaxStacks => 1;

        public override StatusApplication Empower(StatusApplication app, float spellPower)
            => ScaleMagnitude(app, spellPower);
    }

    /// <summary>
    /// The next hit of any kind finishes an ordinary enemy. Short-lived and rare on purpose -
    /// the interesting part is the scramble to land anything at all before it lapses. Elites
    /// take a heavy hit instead, and it never executes the player.
    /// </summary>
    public class DeathmarkStatus : StatusDefinition
    {
        /// <summary>Fraction of maximum health an elite loses instead of dying outright.</summary>
        public const float EliteFraction = 0.35f;

        public override StatusId Id => StatusId.Deathmark;
        public override string DisplayName => "Death-marked";
        public override string Description => "The next hit is fatal. Elites take heavy damage instead.";
        public override Color Tint => new Color(0.95f, 0.1f, 0.35f);
        public override int MaxStacks => 1;
    }

    /// <summary>
    /// Untouchable by anything physical, and undone by everything else. Both a status a spell
    /// can inflict and a state an enemy can simply be born in, so a ghost and a cursed cultist
    /// read by the same rule.
    /// </summary>
    public class EtherealStatus : StatusDefinition
    {
        public const float VulnerabilityMultiplier = 2f;

        public override StatusId Id => StatusId.Ethereal;
        public override string DisplayName => "Ethereal";
        public override string Description => "Immune to kinetic damage. Everything else hits twice as hard.";
        public override Color Tint => new Color(0.7f, 0.9f, 0.95f);
        public override int MaxStacks => 1;
    }

    /// <summary>
    /// A status with a name, a colour and a place in the enum, but no behaviour yet. Applying
    /// one does nothing beyond showing up. It exists so the id resolves and assets can refer to
    /// it; the behaviour arrives with the systems it depends on.
    /// </summary>
    public class PlannedStatus : StatusDefinition
    {
        private readonly StatusId _id;
        private readonly string _name;
        private readonly string _description;
        private readonly Color _tint;

        public PlannedStatus(StatusId id, string name, string description, Color tint)
        {
            _id = id;
            _name = name;
            _description = description;
            _tint = tint;
        }

        public override StatusId Id => _id;
        public override string DisplayName => _name;
        public override string Description => _description;
        public override Color Tint => _tint;
    }
}
