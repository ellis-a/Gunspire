#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Exercises the debuff rules against real components.
    ///
    /// These are the rules that are easy to get subtly wrong and almost impossible to notice in
    /// play: a burn that never stops halving, an execute that fires on the player, an ethereal
    /// target that is only mostly immune. Each one is a couple of lines of arithmetic in the
    /// damage path, and each one silently changes how the whole game feels.
    /// </summary>
    public static class StatusTools
    {
        [MenuItem("Gunspire/Verify Debuffs")]
        public static void VerifyDebuffs()
        {
            var problems = new List<string>();

            CheckRegistry(problems);
            CheckBurnHalves(problems);
            CheckFrostSlowAndExecute(problems);
            CheckEthereal(problems);
            CheckDeathmark(problems);
            CheckPlayerIsNeverExecuted(problems);
            CheckBleedEndsOnHeal(problems);
            CheckBurnAndFrostCoexist(problems);

            if (problems.Count == 0)
            {
                Debug.Log("Debuffs: burn, frost, poison, shock, bleed, deathmark and ethereal "
                          + "all behave as specified.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Debuffs: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 25; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A living thing on the given team, with the full status and health stack.</summary>
        private static GameObject Subject(Team team, float health, bool elite = false)
        {
            var go = new GameObject("Subject");
            go.AddComponent<CharacterSheet>().SetBaseOverride(Attr.MaxHealth, health);
            go.AddComponent<StatusController>();

            var hp = go.AddComponent<Health>();
            hp.Team = team;
            hp.IsElite = elite;

            // Edit mode never calls Awake, so Health has not worked out its own maximum and
            // would count as already dead - every hit would bounce off a corpse and the whole
            // suite would pass by doing nothing. This is the path enemies are built through.
            hp.ConfigureMaxHealth(health, refill: true);

            return go;
        }

        private static void Hit(GameObject target, float amount, DamageType type)
        {
            DamageInfo info = DamageInfo.Create(amount, type, Team.Player, null);
            target.GetComponent<Health>().TakeDamage(info);
        }

        // ---------------------------------------------------------------- checks

        private static void CheckRegistry(List<string> problems)
        {
            foreach (StatusId id in System.Enum.GetValues(typeof(StatusId)))
                if (StatusLibrary.Get(id) == null)
                    problems.Add(id + " has no definition registered");
        }

        /// <summary>
        /// Burn deals the pool and halves it. The numbers matter: 40 should land 40, then 20,
        /// then 10, and end rather than trickling forever at a vanishing fraction.
        /// </summary>
        private static void CheckBurnHalves(List<string> problems)
        {
            GameObject go = Subject(Team.Enemy, 10000f);
            try
            {
                var status = go.GetComponent<StatusController>();
                var health = go.GetComponent<Health>();

                status.Apply(StatusLibrary.Burn(seconds: 60f, amount: 40f), null, Team.Player);

                ActiveStatus burn = status.Find(StatusId.Burn);
                if (burn == null)
                {
                    problems.Add("burn did not apply at all");
                    return;
                }

                var expected = new[] { 40f, 20f, 10f };
                for (int i = 0; i < expected.Length; i++)
                {
                    float before = health.Current;
                    burn.Def.OnTick(status, burn);
                    float dealt = before - health.Current;

                    if (Mathf.Abs(dealt - expected[i]) > 0.51f)
                        problems.Add("burn tick " + (i + 1) + " dealt " + dealt.ToString("0.#")
                                     + ", expected " + expected[i]);
                }

                // Keep ticking; it has to end rather than halve forever.
                for (int i = 0; i < 40 && status.Has(StatusId.Burn); i++)
                    burn.Def.OnTick(status, burn);

                if (status.Has(StatusId.Burn))
                    problems.Add("burn never burns out - it halved 40 times and is still running");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// One percent per stack, and at a hundred stacks a kinetic hit finishes an ordinary
        /// enemy while energy does not.
        /// </summary>
        private static void CheckFrostSlowAndExecute(List<string> problems)
        {
            GameObject go = Subject(Team.Enemy, 500f);
            try
            {
                var status = go.GetComponent<StatusController>();
                var sheet = go.GetComponent<CharacterSheet>();
                sheet.SetBaseOverride(Attr.MoveSpeed, 10f);

                status.Apply(StatusLibrary.Frost(stacks: 25), null, Team.Player);
                float speed = sheet.Get(Attr.MoveSpeed);
                if (Mathf.Abs(speed - 7.5f) > 0.2f)
                    problems.Add("25 frost stacks gave speed " + speed.ToString("0.0") + ", expected 7.5");

                status.Apply(StatusLibrary.Frost(stacks: FrostStatus.FullStacks), null, Team.Player);
                if (!status.IsFrozen)
                    problems.Add("full frost stacks did not read as frozen");

                // Energy must not execute, however frozen the target is.
                Hit(go, 1f, DamageType.Energy);
                if (!go.GetComponent<Health>().IsAlive)
                    problems.Add("energy damage executed a frozen target - only kinetic should");

                Hit(go, 1f, DamageType.Kinetic);
                if (go.GetComponent<Health>().IsAlive)
                    problems.Add("a kinetic hit at full frost did not finish an ordinary enemy");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Immune to kinetic, doubly hurt by everything else - and the same either way it arrives.</summary>
        private static void CheckEthereal(List<string> problems)
        {
            foreach (bool byNature in new[] { false, true })
            {
                GameObject go = Subject(Team.Enemy, 1000f);
                try
                {
                    var health = go.GetComponent<Health>();
                    string how = byNature ? "innate" : "applied";

                    if (byNature) health.EtherealByNature = true;
                    else go.GetComponent<StatusController>()
                        .Apply(StatusLibrary.Ethereal(), null, Team.Player);

                    if (!health.IsEthereal) problems.Add(how + " ethereal did not take effect");

                    float before = health.Current;
                    Hit(go, 100f, DamageType.Kinetic);
                    if (health.Current < before)
                        problems.Add(how + " ethereal took " + (before - health.Current).ToString("0")
                                     + " kinetic damage, should be immune");

                    before = health.Current;
                    Hit(go, 100f, DamageType.Energy);
                    float dealt = before - health.Current;
                    if (Mathf.Abs(dealt - 200f) > 1f)
                        problems.Add(how + " ethereal took " + dealt.ToString("0")
                                     + " from a 100 energy hit, expected 200");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        /// <summary>Any hit finishes a normal enemy; an elite loses a chunk and lives.</summary>
        private static void CheckDeathmark(List<string> problems)
        {
            GameObject normal = Subject(Team.Enemy, 900f);
            try
            {
                normal.GetComponent<StatusController>()
                    .Apply(StatusLibrary.Deathmark(), null, Team.Player);

                Hit(normal, 1f, DamageType.Energy);
                if (normal.GetComponent<Health>().IsAlive)
                    problems.Add("a death-marked enemy survived a hit");
            }
            finally { Object.DestroyImmediate(normal); }

            GameObject elite = Subject(Team.Enemy, 900f, elite: true);
            try
            {
                var health = elite.GetComponent<Health>();
                var status = elite.GetComponent<StatusController>();
                status.Apply(StatusLibrary.Deathmark(), null, Team.Player);

                Hit(elite, 1f, DamageType.Energy);

                if (!health.IsAlive)
                    problems.Add("a death mark killed an elite outright, which it must not");
                if (status.Has(StatusId.Deathmark))
                    problems.Add("the death mark was not consumed by the hit");

                float lost = health.Max - health.Current;
                float wanted = health.Max * DeathmarkStatus.EliteFraction;
                if (Mathf.Abs(lost - wanted) > 1f)
                    problems.Add("elite lost " + lost.ToString("0") + " to a death mark, expected "
                                 + wanted.ToString("0"));
            }
            finally { Object.DestroyImmediate(elite); }
        }

        /// <summary>
        /// The rule that most needs holding: neither execute may ever fire on the player. An
        /// instant death from a status is indistinguishable from a crash the first time it lands.
        /// </summary>
        private static void CheckPlayerIsNeverExecuted(List<string> problems)
        {
            GameObject go = Subject(Team.Player, 200f);
            try
            {
                var status = go.GetComponent<StatusController>();
                var health = go.GetComponent<Health>();

                status.Apply(StatusLibrary.Frost(stacks: FrostStatus.FullStacks), null, Team.Enemy);
                status.Apply(StatusLibrary.Deathmark(), null, Team.Enemy);

                DamageInfo info = DamageInfo.Create(1f, DamageType.Kinetic, Team.Enemy, null);
                health.TakeDamage(info);

                if (!health.IsAlive)
                    problems.Add("the player was executed by frost or a death mark");
                if (health.Current < health.Max - 10f)
                    problems.Add("the player took " + (health.Max - health.Current).ToString("0")
                                 + " from a 1 damage hit while frozen and marked");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static void CheckBleedEndsOnHeal(List<string> problems)
        {
            GameObject go = Subject(Team.Enemy, 500f);
            try
            {
                var status = go.GetComponent<StatusController>();
                var health = go.GetComponent<Health>();

                status.Apply(StatusLibrary.Bleed(), null, Team.Player);
                if (!status.Has(StatusId.Bleed)) problems.Add("bleed did not apply");

                health.TakeDamage(DamageInfo.Create(50f, DamageType.Kinetic, Team.Player, null));
                health.Heal(1f);

                if (status.Has(StatusId.Bleed))
                    problems.Add("bleeding survived being healed, and nothing else ever stops it");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Burning and frostbitten at the same time, applied in either order, with the frost
        /// stacks intact. These two used to cancel each other out, which quietly made any spell
        /// that applied both deliver only one - and made stacking frost toward the execute
        /// impossible for anyone who also carried fire.
        /// </summary>
        private static void CheckBurnAndFrostCoexist(List<string> problems)
        {
            foreach (bool frostFirst in new[] { true, false })
            {
                GameObject go = Subject(Team.Enemy, 10000f);
                try
                {
                    var status = go.GetComponent<StatusController>();
                    string order = frostFirst ? "frost then burn" : "burn then frost";

                    StatusApplication frost = StatusLibrary.Frost(stacks: 30);
                    StatusApplication burn = StatusLibrary.Burn(amount: 25f);

                    if (frostFirst)
                    {
                        status.Apply(frost, null, Team.Player);
                        status.Apply(burn, null, Team.Player);
                    }
                    else
                    {
                        status.Apply(burn, null, Team.Player);
                        status.Apply(frost, null, Team.Player);
                    }

                    if (!status.Has(StatusId.Burn)) problems.Add(order + ": the burn was removed");
                    if (!status.Has(StatusId.Frost)) problems.Add(order + ": the frost was removed");

                    ActiveStatus chill = status.Find(StatusId.Frost);
                    if (chill != null && chill.Stacks != 30)
                        problems.Add(order + ": frost kept " + chill.Stacks + " stacks, expected 30");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
#endif
