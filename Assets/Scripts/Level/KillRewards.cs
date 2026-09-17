using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// What a killed enemy leaves behind: shillings, and sometimes an orb. Every enemy death counts, whoever
    /// made the kill, as with the rest of the run. An enemy that pays no reward (a split copy, a summoned
    /// enemy, a training dummy) leaves nothing.
    /// </summary>
    public static class KillRewards
    {
        public const int CommonMin = 1;
        public const int CommonMax = 3;
        public const int EliteMultiplier = 4;
        public const int BossMin = 25;
        public const int BossMax = 40;

        /// <summary>What a dropped orb restores, before orb potency. Matches the breakables.</summary>
        public const float HealthOrb = 18f;
        public const float ManaOrb = 30f;

        private static RunState _run;

        /// <summary>Pays kills into this run. Only one run is paid at a time; binding another replaces it.</summary>
        public static void Bind(RunState run)
        {
            Health.AnyDied -= OnAnyDied;
            Health.AnyDied += OnAnyDied;
            _run = run;
        }

        /// <summary>Stops paying this run, if it is the one being paid.</summary>
        public static void Unbind(RunState run)
        {
            if (_run != run) return;
            Health.AnyDied -= OnAnyDied;
            _run = null;
        }

        private static void OnAnyDied(Health victim, DamageInfo info)
        {
            if (_run == null || _run.Player == null || !RunState.CountsAsKill(victim)) return;

            EnemyController enemy = victim.GetComponent<EnemyController>();
            if (enemy == null || enemy.PaysNoReward) return;

            Drop(_run, enemy, victim.transform.position + Vector3.up * 0.8f);
        }

        /// <summary>Drops one enemy's reward at a point. Public so tooling can drive it without a death.</summary>
        public static void Drop(RunState run, EnemyController enemy, Vector3 at)
        {
            if (run == null || enemy == null) return;

            bool elite = enemy.Health != null && enemy.Health.IsElite;
            bool boss = enemy.Definition != null && enemy.Definition.Id == EnemyLibrary.BossId;

            int shillings = Roll(run.Rng, enemy.Definition, elite, boss);
            ShillingSource source = boss ? ShillingSource.Boss : elite ? ShillingSource.Elite : ShillingSource.Kill;
            ShillingPickup.Scatter(at, shillings, source);

            float orbChance = run.Sheet != null ? run.Sheet.Get(Attr.OrbDropChance) : 0f;
            if (orbChance > 0f && run.Rng.Value < orbChance)
            {
                if (run.Rng.Value < 0.5f) OrbPickup.SpawnHealth(at, HealthOrb);
                else OrbPickup.SpawnMana(at, ManaOrb);
            }
        }

        /// <summary>A drop in shillings: the enemy's own range, or its rank's default, times four for an elite.</summary>
        public static int Roll(Rng rng, EnemyDefinition def, bool elite, bool boss)
        {
            int min, max;
            if (def != null && def.ShillingsMin >= 0)
            {
                min = def.ShillingsMin;
                max = Mathf.Max(min, def.ShillingsMax);
            }
            else if (boss)
            {
                min = BossMin;
                max = BossMax;
            }
            else
            {
                min = CommonMin;
                max = CommonMax;
            }

            int amount = rng != null ? rng.Range(min, max + 1) : min;
            return elite && !boss ? amount * EliteMultiplier : amount;
        }
    }
}
