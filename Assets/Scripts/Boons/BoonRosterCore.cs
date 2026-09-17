namespace Gunspire
{
    // The Core family: any build. Numbers are placeholders; Docs/BoonDesign.md is the reference.
    public static partial class BoonLibrary
    {
        private const StatType Dex = StatType.Dexterity;
        private const StatType Pow = StatType.Power;
        private const StatType Ath = StatType.Athletics;
        private const StatType End = StatType.Endurance;
        private const StatType Lck = StatType.Luck;

        private static void AddCore()
        {
            AddStatBoons();

            const BoonFamily F = BoonFamily.Core;

            // ---------------- offers
            Add(F, "Offers", "rewarded", "Rewarded", "You can pick one more boon from each offer, per level.",
                Rarity.Uncommon, 3, Setting(RunSetting.ExtraBoonPicks, 1f), Setting(RunSetting.ExtraOfferChoices, 1f));
            Add(F, "Offers", "fickle_fate", "Fickle Fate", "You can reroll each offer once per level.",
                Rarity.Rare, 2, Setting(RunSetting.OfferRerolls, 1f));

            // ---------------- damage
            Add(F, "Damage", "super_soldier", "Super Soldier", "More gun damage.", Rarity.Common, 5, Percent(Attr.GunDamage, 0.08f));
            Add(F, "Damage", "archmage", "Archmage", "More spell damage.", Rarity.Common, 5, Percent(Attr.SpellPower, 0.08f));
            Add(F, "Damage", BoonIds.ChargeUp, "Charge Up",
                "Double damage on the next floor. The boon is lost afterwards, and can only ever be taken once.",
                Rarity.Rare, 1, Behave(new ChargeUpBehaviour()));
            Add(F, "Damage", "kinetic_enhancer", "Kinetic Enhancer", "More kinetic damage.", Rarity.Common, 3, Enhance(DamageType.Kinetic));
            Add(F, "Damage", "energy_enhancer", "Energy Enhancer", "More energy damage.", Rarity.Common, 3, Enhance(DamageType.Energy));
            Add(F, "Damage", "necrotic_enhancer", "Necrotic Enhancer", "More necrotic damage.", Rarity.Common, 3, Enhance(DamageType.Necrotic));
            Add(F, "Damage", "psychic_enhancer", "Psychic Enhancer", "More psychic damage.", Rarity.Common, 3, Enhance(DamageType.Psychic));
            Add(F, "Damage", "executioner", "Executioner", "More damage to enemies below 30% health.",
                Rarity.Common, 5, Behave(new ExecutionerBehaviour()));
            Add(F, "Damage", "big_game", "Big Game", "More damage to elites and bosses.",
                Rarity.Common, 5, Behave(new BigGameBehaviour()));
            Add(F, "Damage", "brawler", "Brawler", "More damage to enemies within 6 m.",
                Rarity.Common, 5, Behave(new RangeDamageBehaviour { MinDistance = 0f, MaxDistance = 6f }));
            Add(F, "Damage", "longshot", "Longshot", "More damage to enemies beyond 20 m.",
                Rarity.Common, 5, Behave(new RangeDamageBehaviour { MinDistance = 20f, MaxDistance = 9999f }));
            Add(F, "Damage", "momentum", "Momentum", "More damage while moving.", Rarity.Common, 5, Behave(new MomentumBehaviour()));
            Add(F, "Damage", "high_ground", "High Ground", "More damage while in the air.", Rarity.Common, 5, Behave(new HighGroundBehaviour()));
            Add(F, "Damage", "blasphemous_act", "Blasphemous Act",
                "You deal more damage for every enemy that has died on the current floor.",
                Rarity.Rare, 2, Behave(new BlasphemousActBehaviour()));
            Add(F, "Damage", "kill_streak", "Kill Streak",
                "A kill within 3 seconds of another grants a short, stacking damage bonus.",
                Rarity.Common, 3, Behave(new KillStreakBehaviour()));

            // ---------------- mobility
            Add(F, "Mobility", "acrophobia", "Acrophobia", "You can jump again in mid-air.", Rarity.Rare, 1, Flat(Attr.JumpCount, 1f));

            // ---------------- recovery
            Add(F, "Recovery", "treasure_hunter", "Treasure Hunter", "Health and mana orbs restore more.",
                Rarity.Common, 3, Percent(Attr.OrbPotency, 0.15f));
            Add(F, "Recovery", "rest_a_moment", "Rest a Moment", "Heal a small amount when you finish a floor.",
                Rarity.Common, 3, Behave(new RestAMomentBehaviour()));
            Add(F, "Recovery", "vampire_sight", "Vampire Sight", "Killed enemies drop a minor health orb.",
                Rarity.Uncommon, 1, Behave(new MinorOrbBehaviour { Mana = false }));
            Add(F, "Recovery", "demon_sight", "Demon Sight", "Killed enemies drop a minor mana orb.",
                Rarity.Uncommon, 1, Behave(new MinorOrbBehaviour { Mana = true }));
            Add(F, "Recovery", "scavenger", "Scavenger", "Enemies drop health and mana orbs more often.",
                Rarity.Common, 3, Flat(Attr.OrbDropChance, 0.05f));
            Add(F, "Recovery", "bottled_orb", "Bottled Orb",
                "Health orbs collected at full health are stored, one per level, and used automatically when you drop low.",
                Rarity.Rare, 2, Behave(new BottledOrbBehaviour()));
            Add(F, "Recovery", "magnetism", "Magnetism",
                "Orbs fly to you from anywhere on the floor, and each one also restores a little of the other resource.",
                Rarity.Mythic, 1, Behave(new MagnetismBehaviour()));

            // ---------------- shop
            Add(F, "Shop", "haggler", "Haggler", "Shop prices are lower.", Rarity.Common, 3, Setting(RunSetting.ShopPriceScale, 0.08f));
            Add(F, "Shop", "check_the_storage", "Check the Storage", "Shops have one more item to choose from, per level.",
                Rarity.Common, 3, Setting(RunSetting.ShopExtraItems, 1f));
            Add(F, "Shop", "pocket_change", "Pocket Change", "Kills drop more shillings.",
                Rarity.Common, 3, Percent(Attr.ShillingGain, 0.15f));
            Add(F, "Shop", "pickpocket", "Pickpocket", "Melee hits knock shillings loose.",
                Rarity.Common, 3, Behave(new PickpocketBehaviour()));
            Add(F, "Shop", "hoarder", "Hoarder", "Every 100 unspent shillings gives +1 Luck.",
                Rarity.Rare, 1, Behave(new HoarderBehaviour()));

            // ---------------- familiars
            Familiar("wisp", "Wisp", "A wisp that heals you.");
            Familiar("imp", "Imp", "An imp that shoots fireballs at enemies, dealing energy damage.");
            Familiar("crow", "Crow", "A crow that swoops at enemies, dealing kinetic damage.");
            Familiar("bone_moth", "Bone Moth", "A moth that bites enemies for necrotic damage and weakens them.");
            Familiar("seer", "Seer",
                "An eye that zaps the enemy nearest your crosshair for psychic damage and marks it, so your next hit deals bonus damage.");
            Familiar("storm_sprite", "Storm Sprite", "A sprite that arcs lightning between nearby enemies, shocking them.");
            Familiar("rime_watcher", "Rime Watcher", "A watcher that freezes the ground under enemies, slowing them.");
            Familiar("aegis_mote", "Aegis Mote", "A mote that orbits you and blocks an enemy projectile every few seconds.");
            Familiar("magpie", "Magpie", "A magpie that fetches health and mana orbs for you.");
            Familiar("mana_sprite", "Mana Sprite", "A sprite that restores your mana while enemies are near.");
            Familiar("coin_imp", "Coin Imp", "An imp that raises your Luck while it lives.");
            Familiar("homunculus", "Homunculus",
                "A homunculus that enemies attack instead of you. It has 100 health, and if killed it stays dead until the next floor.");
            Familiar("gremlin", "Gremlin", "A gremlin that reloads your holstered gun.");
            Add(F, "Familiars", BoonIds.Familiarity, "Familiarity", "You can have one more familiar.", Rarity.Rare, 1)
                .Needs(new HasTagRequirement { Tag = BoonTags.Familiar });

            // ---------------- minions
            Add(F, "Minions", "master_summoner", "Master Summoner",
                "Your summoned minions have more health and deal more damage.",
                Rarity.Uncommon, 3, new ModifyAlliesEffect { Health = 0.15f, Damage = 0.15f })
                .Needs(new CanSummonRequirement());
            Add(F, "Minions", "beetle_swarm", "Beetle Swarm",
                "A tiny beetle spawns every 5 seconds and attacks enemies in melee. Each dies after 15 seconds.",
                Rarity.Rare, 1, Behave(new BeetleSwarmBehaviour()))
                .Tagged(BoonTags.Summon);
            Add(F, "Minions", "blood_pact", "Blood Pact", "Damage your minions deal heals you for a small share of it.",
                Rarity.Rare, 2, Behave(new BloodPactBehaviour()))
                .Needs(new CanSummonRequirement());

            // ---------------- combat
            Add(F, "Combat", "first_blood", "First Blood", "Damage you deal to an enemy at full health is doubled.",
                Rarity.Rare, 1, Behave(new FirstBloodBehaviour()));
            Add(F, "Combat", "panic_barrier", "Panic Barrier",
                "Taking damage makes you immune to damage for 0.5 seconds per level.",
                Rarity.Rare, 2, Behave(new PanicBarrierBehaviour()));
            Add(F, "Combat", "resilience", "Resilience", "Debuffs on you are 10% less effective per level.",
                Rarity.Common, 5, Percent(Attr.DebuffPotency, -0.1f));
            Add(F, "Combat", "hard_skin", "Hard Skin", "You take a little less damage from every hit.",
                Rarity.Common, 5, Percent(Attr.DamageTaken, -0.04f));
            Add(F, "Combat", "padding", "Padding", "You start each floor with a small shield.",
                Rarity.Common, 3, Behave(new PaddingBehaviour()));
            Add(F, "Combat", "rhythm", "Rhythm", "Kills shorten your spell cooldowns a little.",
                Rarity.Common, 3, Behave(new RhythmBehaviour()));
            Add(F, "Combat", "cool_head", "Cool Head", "Spell cooldowns run faster while you are at full health.",
                Rarity.Common, 3, Behave(new CoolHeadBehaviour()));
            Add(F, "Combat", "cornered", "Cornered", "You take less damage while three or more enemies are near you.",
                Rarity.Common, 3, Behave(new CorneredBehaviour()));
            Add(F, "Combat", "every_reaction", "Every Reaction",
                "Damage you take is dealt back to whoever dealt it, as psychic damage.",
                Rarity.Rare, 1, Behave(new EveryReactionBehaviour()));
            Add(F, "Combat", "cold_blood", "Cold Blood", "At full health, your critical hit chance is doubled.",
                Rarity.Rare, 1, Behave(new ColdBloodBehaviour()));
            Add(F, "Combat", "monarch", "Monarch",
                "Faster cooldowns and mana regeneration. Taking damage loses the effect until the enemy that dealt it dies.",
                Rarity.Rare, 1, Behave(new MonarchBehaviour()));
            Add(F, "Combat", "mana_shield", "Mana Shield",
                "Half of the damage you take drains mana instead. Your maximum health is halved.",
                Rarity.Rare, 1, Behave(new ManaShieldBehaviour()));
            Add(F, "Combat", BoonIds.Cocky, "Cocky",
                "Your maximum health becomes 1 and cannot be raised. You deal triple damage.",
                Rarity.Rare, 1, Behave(new CockyBehaviour()))
                .Needs(NotWith(BoonIds.GlassSoul));
            Add(F, "Combat", BoonIds.Phylactery, "Phylactery",
                "Survive one killing blow per run, returning to half health. Can only ever be taken once.",
                Rarity.Rare, 1, Behave(new PhylacteryBehaviour()));
            Add(F, "Combat", "pinball_wizard", "Pinball Wizard",
                "Your knockback throws enemies further and its impacts deal more damage, and knocked enemies knock back what they hit.",
                Rarity.Uncommon, 3, Percent(Attr.Knockback, 0.25f), Behave(new PinballWizardBehaviour()));
            Add(F, "Combat", BoonIds.GlassSoul, "Glass Soul",
                "Your health is set to 1, but you gain a shield that fully refills after 3 seconds without taking damage.",
                Rarity.Mythic, 1, Behave(new GlassSoulBehaviour()))
                .Needs(NotWith(BoonIds.Cocky));
            Add(F, "Combat", "last_stand", "Last Stand",
                "Below 25% health, cooldowns run three times as fast and you deal 50% more damage.",
                Rarity.Mythic, 1, Behave(new LastStandBehaviour()));
            Add(F, "Combat", "juggernaut", "Juggernaut",
                "You cannot be knocked back, slowed or stunned, but you move 15% slower.",
                Rarity.Mythic, 1, Behave(new JuggernautBehaviour()));
            Add(F, "Combat", "vendetta", "Vendetta",
                "The enemy that last hurt you is marked, and killing it heals you for a quarter of your health.",
                Rarity.Mythic, 1, Behave(new VendettaBehaviour()));

            // ---------------- world
            Add(F, "World", "ghostrealm", "Ghostrealm",
                "Every enemy is Ethereal at all times: immune to kinetic damage, and everything else hits it twice as hard.",
                Rarity.Mythic, 1, Behave(new GhostrealmBehaviour()));
            Add(F, "World", "doomed", "Doomed",
                "At the start of each floor, five random non-elite enemies are death-marked. The mark never wears off.",
                Rarity.Mythic, 1, Behave(new DoomedBehaviour()));
            Add(F, "World", "otherworldly_beauty", "Otherworldly Beauty",
                "The first enemy to see you on each floor is charmed.",
                Rarity.Rare, 1, Behave(new OtherworldlyBeautyBehaviour()));
            Add(F, "World", "courageous", "Courageous",
                "Enemies are stronger, and your Luck is significantly higher.",
                Rarity.Rare, 1, Behave(new CourageousBehaviour()));
        }

        private static ModifyDamageSchoolEffect Enhance(DamageType type) =>
            new ModifyDamageSchoolEffect { School = type, Percent = 0.1f };

        private static void Familiar(string id, string name, string description)
        {
            Add(BoonFamily.Core, "Familiars", id, name, description, Rarity.Uncommon, 1,
                    new GrantFamiliarEffect { FamiliarId = id })
                .Tagged(BoonTags.Familiar)
                .Needs(new TagLimitRequirement
                {
                    Tag = BoonTags.Familiar, Limit = 1, RaisedByBoon = BoonIds.Familiarity, OwnBoonId = id
                });
        }

        /// <summary>The 36 stat boons. All cap at five; any that raises Endurance is a health boon.</summary>
        private static void AddStatBoons()
        {
            StatBoon("nimble", "Nimble", Rarity.Common, 2, Dex);
            StatBoon("arcane", "Arcane", Rarity.Common, 2, Pow);
            StatBoon("swift", "Swift", Rarity.Common, 2, Ath);
            StatBoon("tough", "Tough", Rarity.Common, 2, End);
            StatBoon("lucky", "Lucky", Rarity.Common, 2, Lck);

            StatBoon("agile", "Agile", Rarity.Common, 1, Dex, Ath);
            StatBoon("hardy", "Hardy", Rarity.Common, 1, Ath, End);
            StatBoon("steadfast", "Steadfast", Rarity.Common, 1, End, Pow);
            StatBoon("blessed", "Blessed", Rarity.Common, 1, Pow, Lck);
            StatBoon("gambler", "Gambler", Rarity.Common, 1, Lck, Dex);
            StatBoon("veteran", "Veteran", Rarity.Common, 1, Dex, End);
            StatBoon("spellslinger", "Spellslinger", Rarity.Common, 1, Dex, Pow);
            StatBoon("dynamo", "Dynamo", Rarity.Common, 1, Ath, Pow);
            StatBoon("daring", "Daring", Rarity.Common, 1, Ath, Lck);
            StatBoon("survivor", "Survivor", Rarity.Common, 1, End, Lck);

            StatBoon("commando", "Commando", Rarity.Uncommon, 1, Dex, Ath, End);
            StatBoon("spellblade", "Spellblade", Rarity.Uncommon, 1, Dex, Ath, Pow);
            StatBoon("rogue", "Rogue", Rarity.Uncommon, 1, Dex, Ath, Lck);
            StatBoon("warden", "Warden", Rarity.Uncommon, 1, Dex, End, Pow);
            StatBoon("outlaw", "Outlaw", Rarity.Uncommon, 1, Dex, End, Lck);
            StatBoon("trickster", "Trickster", Rarity.Uncommon, 1, Dex, Pow, Lck);
            StatBoon("champion", "Champion", Rarity.Uncommon, 1, Ath, End, Pow);
            StatBoon("adventurer", "Adventurer", Rarity.Uncommon, 1, Ath, End, Lck);
            StatBoon("wanderer", "Wanderer", Rarity.Uncommon, 1, Ath, Pow, Lck);
            StatBoon("chosen", "Chosen", Rarity.Uncommon, 1, End, Pow, Lck);

            StatBoon("acrobat", "Acrobat", Rarity.Rare, 3, Dex, Ath);
            StatBoon("mercenary", "Mercenary", Rarity.Rare, 3, Dex, End);
            StatBoon("hexslinger", "Hexslinger", Rarity.Rare, 3, Dex, Pow);
            StatBoon("sharpshooter", "Sharpshooter", Rarity.Rare, 3, Dex, Lck);
            StatBoon("titan", "Titan", Rarity.Rare, 3, Ath, End);
            StatBoon("tempest", "Tempest", Rarity.Rare, 3, Ath, Pow);
            StatBoon("daredevil", "Daredevil", Rarity.Rare, 3, Ath, Lck);
            StatBoon("archon", "Archon", Rarity.Rare, 3, End, Pow);
            StatBoon("diehard", "Diehard", Rarity.Rare, 3, End, Lck);
            StatBoon("prophet", "Prophet", Rarity.Rare, 3, Pow, Lck);

            StatBoon("ascendant", "Ascendant", Rarity.Mythic, 5, Dex, Pow, Ath, End, Lck);
        }

        public const string StatsGroup = "Stats";
        public const int StatBoonLevels = 5;

        private static void StatBoon(string id, string name, Rarity rarity, int points, params StatType[] stats)
        {
            var effects = new BoonEffect[stats.Length];
            var parts = new string[stats.Length];
            bool health = false;
            for (int i = 0; i < stats.Length; i++)
            {
                effects[i] = Stat(stats[i], points);
                parts[i] = "+" + points + " " + stats[i];
                health |= stats[i] == End;
            }

            string description = stats.Length == 5 ? "+" + points + " to every stat." : string.Join(", ", parts) + ".";
            Boon boon = Add(BoonFamily.Core, StatsGroup, id, name, description, rarity, StatBoonLevels, effects);
            if (health) HealthBoon(boon);
        }

        /// <summary>A boon that raises maximum health, which Cocky and Glass Soul leave nothing to raise.</summary>
        private static Boon HealthBoon(Boon boon) =>
            boon.Tagged(BoonTags.Health).Needs(NotWith(BoonIds.Cocky), NotWith(BoonIds.GlassSoul));
    }
}
