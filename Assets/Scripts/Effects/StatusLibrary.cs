using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
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
            Register(new ChillStatus());
            Register(new FreezeStatus());
            Register(new BurnStatus());
            Register(new BlightStatus());
            Register(new ShockStatus());
            Register(new WeakenStatus());
            Register(new HasteStatus());
            Register(new FortifyStatus());
            Register(new MarkStatus());
        }

        private static void Register(StatusDefinition def) => Map[def.Id] = def;

        public static StatusDefinition Get(StatusId id) => Map.TryGetValue(id, out var d) ? d : null;

        // Convenience constructors for the payloads weapons and spells hand out.
        public static StatusApplication Chill(float seconds = 3f, int stacks = 1, float slowPerStack = 0.11f)
            => new StatusApplication(StatusId.Chill, seconds, stacks, slowPerStack);

        public static StatusApplication Burn(float seconds = 4f, int stacks = 1, float dpsPerStack = 5f)
            => new StatusApplication(StatusId.Burn, seconds, stacks, dpsPerStack);

        public static StatusApplication Blight(float seconds = 6f, int stacks = 1, float dpsPerStack = 3f)
            => new StatusApplication(StatusId.Blight, seconds, stacks, dpsPerStack);

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
    }

    // -------------------------------------------------------------------------------- ice

    /// <summary>Slows movement and attack speed. Five stacks freeze the target solid.</summary>
    public class ChillStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Chill;
        public override string DisplayName => "Chilled";
        public override string Description => "Slowed. At maximum stacks the target freezes.";
        public override Color Tint => new Color(0.55f, 0.85f, 1f);
        public override int MaxStacks => 5;
        public override bool IsDebuff => true;
        public override StatusId[] Cleanses => new[] { StatusId.Burn };

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            float slow = Mathf.Clamp01(s.Magnitude * s.Stacks);
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, -slow, s));
            c.AddModifier(s, StatModifier.Percent(Attr.AttackSpeed, -slow * 0.5f, s));
        }
    }

    /// <summary>Hard crowd control. Frozen targets cannot move or act and shatter for bonus damage.</summary>
    public class FreezeStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Freeze;
        public override string DisplayName => "Frozen";
        public override string Description => "Cannot act. The next hit shatters for bonus damage.";
        public override Color Tint => new Color(0.75f, 0.95f, 1f);
        public override int MaxStacks => 1;
        public override StatusId[] Cleanses => new[] { StatusId.Chill };

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, -0.95f, s));
        }
    }

    // -------------------------------------------------------------------------------- fire

    /// <summary>Damage over time that scales with stacks. Melts ice on contact.</summary>
    public class BurnStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Burn;
        public override string DisplayName => "Burning";
        public override string Description => "Takes fire damage over time.";
        public override Color Tint => new Color(1f, 0.5f, 0.15f);
        public override int MaxStacks => 10;
        public override float TickInterval => 0.4f;
        public override StatusId[] Cleanses => new[] { StatusId.Chill, StatusId.Freeze };

        public override void OnTick(StatusController c, ActiveStatus s)
        {
            float dmg = s.Magnitude * s.Stacks * TickInterval;
            c.DealTickDamage(s, dmg, DamageType.Fire);
        }
    }

    // -------------------------------------------------------------------------------- poison

    /// <summary>Damage over time that also drinks any healing the target receives.</summary>
    public class BlightStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Blight;
        public override string DisplayName => "Blighted";
        public override string Description => "Poison damage over time. Absorbs incoming healing.";
        public override Color Tint => new Color(0.55f, 0.9f, 0.35f);
        public override int MaxStacks => 10;
        public override float TickInterval => 0.5f;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            // 15% of healing eaten per stack; seven stacks swallow it entirely.
            c.AddModifier(s, StatModifier.Percent(Attr.HealingReceived, -0.15f * s.Stacks, s));
        }

        public override void OnTick(StatusController c, ActiveStatus s)
        {
            float dmg = s.Magnitude * s.Stacks * TickInterval;
            c.DealTickDamage(s, dmg, DamageType.Poison);
        }
    }

    // -------------------------------------------------------------------------------- lightning

    public class ShockStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Shock;
        public override string DisplayName => "Shocked";
        public override string Description => "Takes increased damage from every source.";
        public override Color Tint => new Color(0.7f, 0.75f, 1f);
        public override int MaxStacks => 3;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.DamageTaken, s.Magnitude * s.Stacks, s));
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
}
