using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Heals whoever summoned the caster. Distinct from HealSelfEffect: a familiar healing
    /// itself is not what a healer familiar is for.
    /// </summary>
    [System.Serializable]
    public class HealOwnerEffect : AbilityEffect
    {
        public float Amount = 12f;

        /// <summary>Scales with the caster's power, so a familiar boon level improves it.</summary>
        public bool ScaleWithPower = true;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig player = PlayerRig.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive) return false;

            float amount = ScaleWithPower ? Amount * ctx.Power : Amount;
            return player.Health.Heal(amount) > 0f;
        }

        public override string Describe() => "heals its owner for " + Amount.ToString("0");
    }

    /// <summary>A flash on the owner, so a heal from off-screen still reads as coming from something.</summary>
    [System.Serializable]
    public class VfxOnOwnerEffect : AbilityEffect
    {
        public float Diameter = 1.4f;
        public float Lifetime = 0.4f;

        public override bool Execute(AbilityContext ctx)
        {
            PlayerRig player = PlayerRig.Instance;
            if (player == null) return true;

            var tint = new Color(ctx.Tint.r, ctx.Tint.g, ctx.Tint.b, 0.45f);
            GameObject flash = Build.Sphere(null, "FamiliarVfx",
                player.transform.position + Vector3.up * 1f, Diameter,
                MaterialLibrary.Transparent(tint), collider: false);

            FadeAndDie.Attach(flash, Lifetime, tint, Vector3.one * 2f);
            return true;
        }

        public override string Describe() => null;
    }

    /// <summary>
    /// Buffs every familiar the player has out. This is the spell half of the familiar
    /// system - Fel Empowerment is built from it.
    ///
    /// The health cost is per attack rather than per second, so empowering while nothing is
    /// in range costs nothing and empowering into a fight is the actual decision.
    /// </summary>
    [System.Serializable]
    public class EmpowerFamiliarsEffect : AbilityEffect
    {
        public float DamageMultiplier = 1.8f;
        public float Duration = 10f;
        public float HealthCostPerAttack = 4f;

        public override bool Execute(AbilityContext ctx)
        {
            // Nothing to empower is a failed cast, which refunds the mana rather than
            // quietly spending it on nobody.
            if (FamiliarController.All.Count == 0)
            {
                if (GameDirector.Instance != null)
                    GameDirector.Instance.Notify("No familiar to empower", 1.4f);
                return false;
            }

            FamiliarController.EmpowerAll(DamageMultiplier, Duration * ctx.LevelScale, HealthCostPerAttack);
            return true;
        }

        public override string Describe() =>
            "familiars deal " + ((DamageMultiplier - 1f) * 100f).ToString("0") + "% more for "
            + Duration.ToString("0") + "s, costing " + HealthCostPerAttack.ToString("0") + " health per attack";
    }

    /// <summary>
    /// Grants a familiar for the run. Taking the boon again raises its level, which makes the
    /// familiar you already have tougher and hit harder rather than giving you a second one -
    /// stacking copies would multiply upkeep-free damage rather than deepening a choice.
    /// </summary>
    [System.Serializable]
    public class GrantFamiliarEffect : BoonEffect
    {
        public string FamiliarId = "wisp";

        /// <summary>Per level past the first.</summary>
        public float DamagePerLevel = 0.35f;
        public float HealthPerLevel = 0.30f;

        public override void Apply(RunState run, int level)
        {
            if (run == null) return;

            run.GrantFamiliar(FamiliarId, level);

            // Re-summon so a level-up takes effect now rather than at the next room.
            if (run.Player != null) FamiliarSummoner.Resummon(run);
        }

        public override string Describe() => "summons a " + FamiliarId;
    }
}
