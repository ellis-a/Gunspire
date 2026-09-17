using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>Scales a hit the player's side deals, once the target is known. Executes never reach it.</summary>
    public interface IOutgoingDamageRule
    {
        /// <summary>A multiplier on the hit; 1 leaves it alone.</summary>
        float OutgoingMultiplier(in DamageInfo hit, Health target);
    }

    /// <summary>Changes a hit on the player after resistances, before the shield.</summary>
    public interface IIncomingDamageRule
    {
        /// <summary>The amount the player takes instead. Zero or less cancels the hit.</summary>
        float ModifyIncoming(in DamageInfo hit, Health player, float amount);
    }

    /// <summary>Gets a chance to keep the player alive when a hit would kill them.</summary>
    public interface ILethalHitRule
    {
        /// <summary>Lower runs first. The first rule that saves the player ends the chain.</summary>
        int LethalPriority { get; }

        /// <summary>
        /// True if the player survives. The hit is then cancelled; the rule sets whatever health the
        /// player is left with.
        /// </summary>
        bool TrySurvive(in DamageInfo hit, Health player, float amount);
    }

    /// <summary>Adjusts a crit roll, knowing who is being hit.</summary>
    public interface ICritRule
    {
        void AdjustCrit(CharacterSheet attacker, IDamageable target, ref float chance, ref bool forced);
    }

    /// <summary>
    /// The rules boons add to combat, read by <see cref="Health"/> and <see cref="Combat.RollCrit(CharacterSheet, IDamageable, out float)"/>.
    /// Static so edit-mode tooling can use them without a run, and cleared when a run starts.
    /// Registering never allocates per hit: each list is walked in place.
    /// </summary>
    public static class CombatRules
    {
        private static readonly List<IOutgoingDamageRule> Outgoing = new List<IOutgoingDamageRule>();
        private static readonly List<IIncomingDamageRule> Incoming = new List<IIncomingDamageRule>();
        private static readonly List<ILethalHitRule> Lethal = new List<ILethalHitRule>();
        private static readonly List<ICritRule> Crits = new List<ICritRule>();

        /// <summary>The player's health, which the incoming and lethal rules apply to. Minions share the team but not these.</summary>
        public static Health PlayerHealth { get; set; }

        public static bool IsPlayer(Health health) => health != null && health == PlayerHealth;

        /// <summary>Adds whichever rule interfaces the object implements.</summary>
        public static void Register(object rule)
        {
            if (rule is IOutgoingDamageRule outgoing && !Outgoing.Contains(outgoing)) Outgoing.Add(outgoing);
            if (rule is IIncomingDamageRule incoming && !Incoming.Contains(incoming)) Incoming.Add(incoming);
            if (rule is ICritRule crit && !Crits.Contains(crit)) Crits.Add(crit);

            if (rule is ILethalHitRule lethal && !Lethal.Contains(lethal))
            {
                // Insertion by priority, stable for equal priorities.
                int at = Lethal.Count;
                while (at > 0 && Lethal[at - 1].LethalPriority > lethal.LethalPriority) at--;
                Lethal.Insert(at, lethal);
            }
        }

        public static void Unregister(object rule)
        {
            if (rule is IOutgoingDamageRule outgoing) Outgoing.Remove(outgoing);
            if (rule is IIncomingDamageRule incoming) Incoming.Remove(incoming);
            if (rule is ILethalHitRule lethal) Lethal.Remove(lethal);
            if (rule is ICritRule crit) Crits.Remove(crit);
        }

        public static void Clear()
        {
            Outgoing.Clear();
            Incoming.Clear();
            Lethal.Clear();
            Crits.Clear();
        }

        public static float OutgoingMultiplier(in DamageInfo hit, Health target)
        {
            float multiplier = 1f;
            for (int i = 0; i < Outgoing.Count; i++)
                multiplier *= Mathf.Max(0f, Outgoing[i].OutgoingMultiplier(in hit, target));
            return multiplier;
        }

        public static float ModifyIncoming(in DamageInfo hit, Health player, float amount)
        {
            for (int i = 0; i < Incoming.Count && amount > 0f; i++)
                amount = Incoming[i].ModifyIncoming(in hit, player, amount);
            return amount;
        }

        public static bool TrySurvive(in DamageInfo hit, Health player, float amount)
        {
            for (int i = 0; i < Lethal.Count; i++)
                if (Lethal[i].TrySurvive(in hit, player, amount)) return true;
            return false;
        }

        public static void AdjustCrit(CharacterSheet attacker, IDamageable target, ref float chance, ref bool forced)
        {
            for (int i = 0; i < Crits.Count; i++)
                Crits[i].AdjustCrit(attacker, target, ref chance, ref forced);
        }
    }
}
