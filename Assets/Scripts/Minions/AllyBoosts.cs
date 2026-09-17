namespace Gunspire
{
    /// <summary>
    /// What boons add to everything that fights for the player, read when each ally is summoned: walking
    /// minions, the Bestial companion and familiars. Each value is a percentage bonus, zero at rest. The
    /// companion's own values stack on the general ones. The bound run's copy is the one summoning reads.
    /// </summary>
    public class AllyBoosts
    {
        /// <summary>The boosts of the run being played, or null outside one.</summary>
        public static AllyBoosts Active { get; set; }

        public float Health;
        public float Damage;
        public float MoveSpeed;

        public float CompanionHealth;
        public float UndeadHealth;
        public float CompanionDamage;
        public float CompanionMoveSpeed;

        /// <summary>Puts the bonuses on a freshly summoned minion's sheet.</summary>
        public void ApplyTo(CharacterSheet sheet, bool companion, bool undead = false)
        {
            if (sheet == null) return;

            float health = Health + (companion ? CompanionHealth : 0f) + (undead ? UndeadHealth : 0f);
            float damage = Damage + (companion ? CompanionDamage : 0f);
            float speed = MoveSpeed + (companion ? CompanionMoveSpeed : 0f);

            if (health != 0f) sheet.AddPercent(Attr.MaxHealth, health, this, "Ally boosts");
            if (damage != 0f) sheet.AddPercent(Attr.DamageDealt, damage, this, "Ally boosts");
            if (speed != 0f) sheet.AddPercent(Attr.MoveSpeed, speed, this, "Ally boosts");
        }

        /// <summary>Scales a familiar before it is built. Familiars take the general bonuses only.</summary>
        public void ApplyTo(FamiliarDefinition def)
        {
            if (def == null) return;
            def.Health *= 1f + Health;
            def.DamageMultiplier *= 1f + Damage;
            def.MoveSpeed *= 1f + MoveSpeed;
        }
    }
}
