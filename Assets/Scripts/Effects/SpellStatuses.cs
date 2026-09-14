using UnityEngine;

namespace Gunspire
{
    // The statuses the school spells are built from. Every number is a first guess.

    /// <summary>
    /// A plagued enemy that dies rises as a short-lived zombie on your side and passes the plague to the
    /// enemies around it. Passing it on applies no fear: only Apocalypse's cone fears, since fear alerts,
    /// and a plague that feared would chain alerts across the floor.
    /// </summary>
    public class PlagueStatus : StatusDefinition
    {
        public const string ZombieId = "plague_zombie";
        public const float SpreadRadius = 5f;

        public override StatusId Id => StatusId.Plague;
        public override string DisplayName => "Plagued";
        public override string Description => "Rises as your zombie on death, and passes the plague on.";
        public override Color Tint => new Color(0.5f, 0.65f, 0.25f);
        public override int MaxStacks => 1;

        public override void OnRemoved(StatusController c, ActiveStatus s)
        {
            // Every status is cleared on death, so a plague removed from something dead is the plague killing it.
            Health health = c.Health;
            if (health == null || health.IsAlive || health.Team != Team.Enemy) return;

            Vector3 at = c.transform.position;
            MinionSummoner.Spawn(ZombieId, at);

            Collider[] near = Physics.OverlapSphere(at, SpreadRadius, Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < near.Length; i++)
            {
                StatusController other = near[i].GetComponentInParent<StatusController>();
                if (other == null || other == c || other.Health == null || !other.Health.IsAlive) continue;
                if (other.Has(StatusId.Plague)) continue;

                other.Apply(new StatusApplication(StatusId.Plague, s.Duration, 1, s.Magnitude), s.Source, s.SourceTeam);
            }
        }
    }

    /// <summary>
    /// Psychic damage over time. The plan named a general damage-over-time status that takes a type;
    /// Intrusive Thoughts is its only customer and wants psychic, so it is psychic until another needs more.
    /// </summary>
    public class TormentStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Torment;
        public override string DisplayName => "Tormented";
        public override string Description => "Takes psychic damage over time.";
        public override Color Tint => new Color(0.8f, 0.35f, 0.6f);
        public override int MaxStacks => 3;
        public override float TickInterval => 0.5f;

        public override StatusApplication Empower(StatusApplication app, float spellPower) => ScaleMagnitude(app, spellPower);

        public override void OnTick(StatusController c, ActiveStatus s)
            => c.DealTickDamage(s, s.Magnitude * s.Stacks * TickInterval, DamageType.Psychic);
    }

    public class EmpoweredStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Empowered;
        public override string DisplayName => "Empowered";
        public override string Description => "Deals more damage.";
        public override Color Tint => new Color(1f, 0.55f, 0.3f);
        public override int MaxStacks => 3;
        public override bool IsDebuff => false;

        public override StatusApplication Empower(StatusApplication app, float spellPower) => ScaleMagnitude(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
            => c.AddModifier(s, StatModifier.Percent(Attr.DamageDealt, s.Magnitude * s.Stacks, s));
    }

    public class ExposedStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Exposed;
        public override string DisplayName => "Exposed";
        public override string Description => "Takes more damage.";
        public override Color Tint => new Color(0.75f, 0.2f, 0.3f);
        public override int MaxStacks => 3;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
            => c.AddModifier(s, StatModifier.Percent(Attr.DamageTaken, s.Magnitude * s.Stacks, s));
    }

    public class QuickenedStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Quickened;
        public override string DisplayName => "Quickened";
        public override string Description => "Attacks and reloads faster.";
        public override Color Tint => new Color(0.95f, 0.85f, 0.45f);
        public override int MaxStacks => 1;
        public override bool IsDebuff => false;

        public override StatusApplication Empower(StatusApplication app, float spellPower) => ScaleMagnitude(app, spellPower);

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.AttackSpeed, s.Magnitude, s));
            c.AddModifier(s, StatModifier.Percent(Attr.ReloadSpeed, s.Magnitude, s));
        }
    }

    /// <summary>
    /// Wither's mark. Below its share of maximum health the enemy dies outright - and unlike deathmark and
    /// full frost, elites included. That is a deliberate exception to the elite rule in <see cref="Health"/>.
    /// Weaken is applied alongside it, but the kill keys off this status alone, so Gravewalk's and
    /// Gravebite's weaken never inherit it. It never fires on the player.
    /// </summary>
    public class WitheredStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Withered;
        public override string DisplayName => "Withered";
        public override string Description => "Dies outright below a share of its health, elites included.";
        public override Color Tint => new Color(0.4f, 0.45f, 0.3f);
        public override int MaxStacks => 1;
        public override float TickInterval => 0.1f;

        public override void OnApplied(StatusController c, ActiveStatus s) => Check(c, s);
        public override void OnTick(StatusController c, ActiveStatus s) => Check(c, s);

        private static void Check(StatusController c, ActiveStatus s)
        {
            Health health = c.Health;
            if (health == null || !health.IsAlive || health.Team == Team.Player || health.Fraction > s.Magnitude) return;

            DamageInfo cause = DamageInfo.Create(0f, DamageType.Execute, s.SourceTeam, s.Source);
            cause.Origin = DamageOrigin.Spell;
            health.Execute(cause, Health.FinishesElites);
        }
    }

    /// <summary>
    /// Grown large: hits harder. Anything that is not the player also looks larger, its visuals scaled
    /// rather than its body, so nothing is pushed into a wall by growing. The player is the camera, so
    /// there is nothing of theirs to see grow.
    /// </summary>
    public class EnlargedStatus : StatusDefinition
    {
        public const float VisualScale = 1.3f;

        public override StatusId Id => StatusId.Enlarged;
        public override string DisplayName => "Enlarged";
        public override string Description => "Larger, and deals more damage.";
        public override Color Tint => new Color(0.7f, 0.85f, 0.45f);
        public override int MaxStacks => 1;
        public override bool IsDebuff => false;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
            => c.AddModifier(s, StatModifier.Percent(Attr.DamageDealt, s.Magnitude, s));

        public override void OnApplied(StatusController c, ActiveStatus s) => ScaleVisuals(c, VisualScale);
        public override void OnRemoved(StatusController c, ActiveStatus s) => ScaleVisuals(c, 1f / VisualScale);

        private static void ScaleVisuals(StatusController c, float factor)
        {
            if (c.GetComponent<PlayerRig>() != null) return;

            Transform root = c.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.GetComponent<Renderer>() == null) continue;
                child.localScale *= factor;
                child.localPosition *= factor;
            }
        }
    }

    /// <summary>Blood Scent. The HUD marks every enemy through walls while the player carries it.</summary>
    public class ScentingStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Scenting;
        public override string DisplayName => "Blood Scent";
        public override string Description => "Senses every enemy through walls.";
        public override Color Tint => new Color(0.85f, 0.25f, 0.25f);
        public override int MaxStacks => 1;
        public override bool IsDebuff => false;
    }

    /// <summary>Foretell. <see cref="Health"/> turns the next direct hit into a miss and removes this.</summary>
    public class ForetoldStatus : StatusDefinition
    {
        public override StatusId Id => StatusId.Foretold;
        public override string DisplayName => "Foretold";
        public override string Description => "The next direct hit misses.";
        public override Color Tint => new Color(0.95f, 0.95f, 0.7f);
        public override int MaxStacks => 1;
        public override bool IsDebuff => false;
    }

    /// <summary>
    /// Flicker: out of reality for a moment. Immune, unseen and untargetable while it lasts. Flicker's
    /// slow and self-disarm are their own statuses applied alongside.
    /// </summary>
    public class PhasedStatus : StatusDefinition
    {
        private static readonly object ConcealKey = new object();

        public override StatusId Id => StatusId.Phased;
        public override string DisplayName => "Phased";
        public override string Description => "Out of reality: cannot be hurt or seen.";
        public override Color Tint => new Color(0.6f, 0.5f, 1f);
        public override int MaxStacks => 1;
        public override bool IsDebuff => false;
        public override float TickInterval => 0.05f;

        public override void OnApplied(StatusController c, ActiveStatus s)
        {
            KeepImmune(c);
            PlayerConcealment concealment = c.GetComponent<PlayerConcealment>();
            if (concealment != null) concealment.Hide(ConcealKey, fromSight: true, untargetable: true);
        }

        public override void OnTick(StatusController c, ActiveStatus s) => KeepImmune(c);

        public override void OnRemoved(StatusController c, ActiveStatus s)
        {
            PlayerConcealment concealment = c.GetComponent<PlayerConcealment>();
            if (concealment != null) concealment.Release(ConcealKey);
        }

        private static void KeepImmune(StatusController c)
        {
            if (c.Health != null) c.Health.InvulnerabilityTimer = Mathf.Max(c.Health.InvulnerabilityTimer, 0.1f);
        }
    }

    /// <summary>
    /// Superid. The gun fires itself at the enemy nearest the reticle and never misses, never reloads and
    /// cannot be swapped, while the player moves faster and takes less damage. Melee still works. With no
    /// enemy in sight it holds fire, the default for that open question.
    /// </summary>
    public class UnleashedStatus : StatusDefinition
    {
        public const float SpeedBonus = 0.25f;
        public const float DamageReduction = 0.25f;

        private static readonly object SwapLock = new object();

        public override StatusId Id => StatusId.Unleashed;
        public override string DisplayName => "Unleashed";
        public override string Description => "The gun fires itself and never misses. Faster, and harder to hurt.";
        public override Color Tint => new Color(0.95f, 0.4f, 0.8f);
        public override int MaxStacks => 1;
        public override bool IsDebuff => false;

        public override void BuildModifiers(StatusController c, ActiveStatus s)
        {
            c.AddModifier(s, StatModifier.Percent(Attr.MoveSpeed, SpeedBonus, s));
            c.AddModifier(s, StatModifier.Percent(Attr.DamageTaken, -DamageReduction, s));
        }

        public override void OnApplied(StatusController c, ActiveStatus s)
        {
            PlayerRig rig = c.GetComponent<PlayerRig>();
            if (rig == null) return;

            if (rig.AutoFire != null) rig.AutoFire.Begin();
            if (rig.Weapon != null) rig.Weapon.InfiniteAmmo = true;
            if (rig.Holster != null) rig.Holster.LockSwap(SwapLock);
        }

        public override void OnRemoved(StatusController c, ActiveStatus s)
        {
            PlayerRig rig = c.GetComponent<PlayerRig>();
            if (rig == null) return;

            if (rig.AutoFire != null) rig.AutoFire.End();
            if (rig.Weapon != null) rig.Weapon.InfiniteAmmo = false;
            if (rig.Holster != null) rig.Holster.UnlockSwap(SwapLock);
        }
    }
}
