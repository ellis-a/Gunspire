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

        public override StatusId Id => StatusId.Frost;
        public override string DisplayName => "Frostbitten";
        public override string Description =>
            "Slowed 1% per stack. At 100 stacks, kinetic damage finishes them.";
        public override Color Tint => new Color(0.55f, 0.85f, 1f);
        public override int MaxStacks => FullStacks;

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
}
