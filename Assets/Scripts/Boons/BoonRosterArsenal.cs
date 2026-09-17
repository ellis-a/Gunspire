namespace Gunspire
{
    // The Arsenal family: guns. A class boon is offered only while a gun of that class is carried.
    public static partial class BoonLibrary
    {
        private static void AddArsenal()
        {
            const BoonFamily F = BoonFamily.Arsenal;
            const string Classes = "Classes";

            // ---------------- class Commons
            Add(F, Classes, "quickdraw", "Quickdraw", "Swapping to a handgun is instant, and its first shot deals more damage.",
                    Rarity.Common, 3,
                    ClassPercent(WeaponClass.Handgun, Attr.DrawSpeed, 10f),
                    Behave(new ClassRoundBehaviour
                    {
                        Class = WeaponClass.Handgun, When = ClassRoundBehaviour.Condition.FirstAfterDraw, PerLevel = 0.15f
                    }))
                .Needs(Carries(WeaponClass.Handgun));
            Add(F, Classes, "sidearm", "Sidearm", "Handguns reload faster.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Handgun, Attr.ReloadSpeed, 0.1f))
                .Needs(Carries(WeaponClass.Handgun));
            Add(F, Classes, "spray_and_pray", "Spray and Pray", "SMGs lose less accuracy while moving.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.SMG, Attr.MovingSpread, -0.12f))
                .Needs(Carries(WeaponClass.SMG));
            Add(F, Classes, "extended_drum", "Extended Drum", "SMG magazines hold more rounds.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.SMG, Attr.MagazineSize, 0.15f))
                .Needs(Carries(WeaponClass.SMG));
            Add(F, Classes, "tight_choke", "Tight Choke", "Shotguns have less spread.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Shotgun, Attr.Spread, -0.1f))
                .Needs(Carries(WeaponClass.Shotgun));
            Add(F, Classes, "point_blank", "Point Blank", "Shotgun pellets deal more damage within 5 m.",
                    Rarity.Common, 3,
                    Behave(new ClassRoundBehaviour
                    {
                        Class = WeaponClass.Shotgun, When = ClassRoundBehaviour.Condition.WithinRange, Threshold = 5f,
                        PerLevel = 0.1f
                    }))
                .Needs(Carries(WeaponClass.Shotgun));
            Add(F, Classes, "marksman", "Marksman", "Rifles have less recoil.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Rifle, Attr.Recoil, -0.12f))
                .Needs(Carries(WeaponClass.Rifle));
            Add(F, Classes, "closing_round", "Closing Round", "The last round of each burst deals more damage.",
                    Rarity.Common, 3,
                    Behave(new ClassRoundBehaviour
                    {
                        Class = WeaponClass.Rifle, When = ClassRoundBehaviour.Condition.BurstEnd, PerLevel = 0.2f
                    }))
                .Needs(Carries(WeaponClass.Rifle));
            Add(F, Classes, "steady_breath", "Steady Breath", "Aiming down the scope zooms further and steadies faster.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Sniper, Attr.ScopeZoom, 0.1f))
                .Needs(Carries(WeaponClass.Sniper));
            Add(F, Classes, "overpenetration", "Overpenetration", "Sniper rounds pierce one more enemy.",
                    Rarity.Common, 3, ClassFlat(WeaponClass.Sniper, Attr.Pierce, 1f))
                .Needs(Carries(WeaponClass.Sniper));
            Add(F, Classes, "warm_barrel", "Warm Barrel", "Heavy guns spin up faster.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Heavy, Attr.SpinUpRate, 0.15f))
                .Needs(Carries(WeaponClass.Heavy));
            Add(F, Classes, "belt_fed", "Belt Fed", "Heavy guns reload faster.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Heavy, Attr.ReloadSpeed, 0.12f))
                .Needs(Carries(WeaponClass.Heavy));
            Add(F, Classes, "bigger_boom", "Bigger Boom", "Launcher explosions have a larger radius.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Launcher, Attr.SplashRadius, 0.1f))
                .Needs(Carries(WeaponClass.Launcher));
            Add(F, Classes, "hot_load", "Hot Load", "Launcher projectiles fly faster.",
                    Rarity.Common, 3, ClassPercent(WeaponClass.Launcher, Attr.ProjectileSpeed, 0.12f))
                .Needs(Carries(WeaponClass.Launcher));

            // ---------------- class Rares
            Add(F, Classes, "dead_mans_hand", "Dead Man's Hand",
                    "Each handgun hit in a row deals more damage; a miss resets it.",
                    Rarity.Rare, 1, Behave(new DeadMansHandBehaviour()))
                .Needs(Carries(WeaponClass.Handgun));
            Add(F, Classes, "tail_end", "Tail End", "The last quarter of an SMG magazine deals double damage.",
                    Rarity.Rare, 1,
                    Behave(new ClassRoundBehaviour
                    {
                        Class = WeaponClass.SMG, When = ClassRoundBehaviour.Condition.MagazineTail, Threshold = 0.25f,
                        PerLevel = 0f, Multiplier = 2f
                    }))
                .Needs(Carries(WeaponClass.SMG));
            Add(F, Classes, "flechette", "Flechette", "Shotgun pellets pierce one enemy.",
                    Rarity.Rare, 1, ClassFlat(WeaponClass.Shotgun, Attr.Pierce, 1f))
                .Needs(Carries(WeaponClass.Shotgun));
            Add(F, Classes, "pinpoint", "Pinpoint", "Every third rifle hit on the same enemy is a critical hit.",
                    Rarity.Rare, 1, Behave(new PinpointBehaviour()))
                .Needs(Carries(WeaponClass.Rifle));
            Add(F, Classes, "quickscope", "Quickscope",
                    "A sniper shot within 0.3 seconds of aiming down the scope deals double damage.",
                    Rarity.Rare, 1, Behave(new QuickscopeBehaviour()))
                .Needs(Carries(WeaponClass.Sniper));
            Add(F, Classes, "planted", "Planted", "While you stand still, heavy guns have no spread and never spin down.",
                    Rarity.Rare, 1, Behave(new PlantedBehaviour()))
                .Needs(Carries(WeaponClass.Heavy));
            Add(F, Classes, "rocket_jump", "Rocket Jump", "Your own launcher explosions launch you, without hurting you.",
                    Rarity.Rare, 1, Behave(new RocketJumpBehaviour()))
                .Needs(Carries(WeaponClass.Launcher));

            // ---------------- enchantments
            Enchantment("burn", "Burn", "Rounds apply burn.", StatusId.Burn, 0.25f);
            Enchantment("bleed", "Bleed", "Rounds apply bleed.", StatusId.Bleed, 0.1f);
            Enchantment("poison", "Poison", "Rounds apply poison.", StatusId.Poison, 0.1f);
            Enchantment("torment", "Torment", "Rounds apply torment.", StatusId.Torment, 0.15f);
            Enchantment("shock", "Shock", "The first round after a reload applies shock.", StatusId.Shock, 0.5f);
            Enchantment("frost", "Frost", "Rounds apply frost.", StatusId.Frost, 0.5f);
            Enchantment("weaken", "Weaken", "Rounds apply weaken, so enemies deal less damage.", StatusId.Weaken, 0.3f);
            Enchantment("hex", "Hex", "Rounds apply hex.", StatusId.Hex, 0.5f);
            Enchantment("volatile", "Volatile", "Rounds apply volatile.", StatusId.Volatile, 1f);
            Enchantment("gilded", "Gilded", "Rounds apply gilded.", StatusId.Gilded, 0.02f);
            Enchantment("dread", "Dread", "Rounds apply dread.", StatusId.Dread, 1f);

            // ---------------- handling
            Add(F, "Handling", "bigger_bullets", "Bigger Bullets", "Critical hits deal more damage.",
                Rarity.Common, 3, Percent(Attr.CritDamage, 0.1f));
            Add(F, "Handling", "mag_dimension", "Mag Dimension", "Magazines hold 50% more rounds, rounded down.",
                Rarity.Uncommon, 2, Percent(Attr.MagazineSize, 0.5f));
            Add(F, "Handling", "lock_and_load", "Lock and Load", "Kills put a round back in the magazine.",
                Rarity.Uncommon, 1, Behave(new LockAndLoadBehaviour()));
            Add(F, "Handling", "quick_hands", "Quick Hands", "Swapping guns is faster.",
                Rarity.Common, 3, Percent(Attr.DrawSpeed, 0.2f));
            Add(F, "Handling", "fresh_mag", "Fresh Mag", "The first round after a reload deals more damage.",
                Rarity.Common, 3,
                Behave(new ClassRoundBehaviour { When = ClassRoundBehaviour.Condition.FirstAfterReload, PerLevel = 0.2f }));
            Add(F, "Handling", "gunmage", "Gunmage",
                "While you have mana, rounds cost mana instead of ammo, so you never need to reload.",
                Rarity.Mythic, 1, Behave(new GunmageBehaviour()));
            Add(F, "Handling", "third_hand", "Third Hand", "You can carry a third gun.",
                Rarity.Mythic, 1, Behave(new ThirdHandBehaviour()));
            Add(F, "Handling", "bifurcator", "Bifurcator", "Each shot also fires a second round with much worse accuracy.",
                Rarity.Rare, 1, Behave(new BifurcatorBehaviour()));
            Add(F, "Handling", "magnetised_ammo", "Magnetised Ammo", "Projectile rounds curve towards enemies.",
                    Rarity.Rare, 1, Behave(new MagnetisedAmmoBehaviour()))
                .Needs(new CarriesProjectileGunRequirement());
            Add(F, "Handling", "mirror_barrel", "Mirror Barrel",
                "Your holstered gun fires alongside the one in your hands, at half damage.",
                Rarity.Mythic, 1, Behave(new MirrorBarrelBehaviour()));

            // ---------------- headshots and on-hit
            Add(F, "Headshots", "birthday_party", "Birthday Party",
                "Headshots explode, dealing heavy damage in an area. The target does not have to die.",
                Rarity.Mythic, 1, Behave(new BirthdayPartyBehaviour()));
            Add(F, "On-hit", "chain_static", "Chain Static",
                "A gun hit sends a bolt of lightning to an enemy near the one you hit, dealing energy damage.",
                Rarity.Uncommon, 3, Behave(new ChainStaticBehaviour()));
            Add(F, "On-hit", "concussive", "Concussive", "Gun hits add a little knockback.",
                Rarity.Common, 3, Behave(new ConcussiveBehaviour()));
        }

        public const string EnchantmentGroup = "Enchantments";

        private static void Enchantment(string id, string name, string description, StatusId status, float multiplier)
        {
            Add(BoonFamily.Arsenal, EnchantmentGroup, id + "_enchantment", name + " Enchantment", description,
                Rarity.Uncommon, 3, Behave(new EnchantmentBehaviour { Status = status, Multiplier = multiplier }));
        }
    }
}
