using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The familiar roster. Same hybrid as every other library: built-ins in code, any
    /// <see cref="FamiliarAsset"/> under a Resources folder merged over the top by id.
    /// </summary>
    public static class FamiliarLibrary
    {
        private static List<FamiliarDefinition> _all;

        public static IReadOnlyList<FamiliarDefinition> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static void Reload() => _all = null;

        /// <summary>Fresh copy, safe to mutate per summon.</summary>
        public static FamiliarDefinition Get(string id)
        {
            FamiliarDefinition found = Peek(id);
            return found != null ? found.Clone() : null;
        }

        public static FamiliarDefinition Peek(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        private static void Build()
        {
            _all = BuiltIn();

            FamiliarAsset[] authored = Resources.LoadAll<FamiliarAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                FamiliarDefinition def = authored[i] != null ? authored[i].Familiar : null;
                if (def == null) continue;

                if (string.IsNullOrEmpty(def.Id))
                {
                    Debug.LogWarning("Familiar asset \"" + authored[i].name + "\" has no Id and was ignored.");
                    continue;
                }

                int existing = _all.FindIndex(f => f.Id == def.Id);
                if (existing >= 0) _all[existing] = def;
                else _all.Add(def);
            }
        }

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<FamiliarDefinition> BuiltIn() => new List<FamiliarDefinition>
        {
            Wisp(), Imp(), Crow(), BoneMoth(), Seer(), StormSprite(), RimeWatcher(),
            AegisMote(), Magpie(), ManaSprite(), CoinImp(), Homunculus(), Gremlin()
        };

        // ---------------------------------------------------------------- the roster
        //
        // One familiar at a time, so each is meant to feel like a real choice. Every number is a first guess.

        /// <summary>
        /// The healer. Its heal only fires with an enemy in range, which is deliberate: a healer working
        /// between fights would erase attrition from the run.
        /// </summary>
        private static FamiliarDefinition Wisp() => new FamiliarDefinition
        {
            Id = "wisp",
            DisplayName = "Wisp",
            Description = "A mote of light that heals you while there is something to fight.",
            Health = 60f, MoveSpeed = 8.5f,
            BodyColor = new Color(0.7f, 1f, 0.8f), EyeColor = new Color(1f, 1f, 0.9f),
            FollowDistance = 2.2f, EngageRange = 24f,
            Attacks =
            {
                new AttackDefinition
                {
                    Name = "Mend", DamageType = DamageType.Energy, Tint = new Color(0.7f, 1f, 0.8f),
                    MinRange = 0f, MaxRange = 999f, Cooldown = 4f, InitialDelay = 2f, RequiresLineOfSight = false,
                    Sequence =
                    {
                        new HealOwnerEffect { Amount = 12f },
                        new VfxOnOwnerEffect { Diameter = 1.4f, Lifetime = 0.4f }
                    }
                }
            }
        };

        private static FamiliarDefinition Imp() => new FamiliarDefinition
        {
            Id = "imp",
            DisplayName = "Imp",
            Description = "Throws fireballs at whatever you are fighting.",
            Health = 55f, MoveSpeed = 8f,
            BodyColor = Palette.Fire, EyeColor = new Color(1f, 0.85f, 0.4f),
            EngageRange = 20f,
            Attacks = { Bolt("Fireball", DamageType.Energy, Palette.Fire, 16f, 1.3f, 30f, 0.2f, splash: 1.8f) }
        };

        private static FamiliarDefinition Crow() => new FamiliarDefinition
        {
            Id = "crow",
            DisplayName = "Crow",
            Description = "Dives at enemies, tearing at them.",
            Health = 60f, MoveSpeed = 10f,
            BodyColor = new Color(0.18f, 0.18f, 0.22f), EyeColor = new Color(0.9f, 0.9f, 1f),
            EngageRange = 20f,
            Attacks = { Bolt("Swoop", DamageType.Kinetic, new Color(0.5f, 0.5f, 0.6f), 13f, 0.8f, 60f, 0.25f) }
        };

        private static FamiliarDefinition BoneMoth() => new FamiliarDefinition
        {
            Id = "bone_moth",
            DisplayName = "Bone Moth",
            Description = "Bites enemies with necrotic dust that weakens them.",
            Health = 50f, MoveSpeed = 9f,
            BodyColor = new Color(0.85f, 0.82f, 0.7f), EyeColor = new Color(0.6f, 1f, 0.6f),
            EngageRange = 18f,
            Attacks =
            {
                Bolt("Grave Dust", DamageType.Necrotic, new Color(0.6f, 0.8f, 0.5f), 10f, 1.1f, 32f, 0.18f,
                    payload: StatusLibrary.Weaken(4f))
            }
        };

        private static FamiliarDefinition Seer() => new FamiliarDefinition
        {
            Id = "seer",
            DisplayName = "Seer",
            Description = "Zaps the enemy nearest your crosshair and marks it, so your next hit on it lands harder.",
            Health = 45f, MoveSpeed = 8f,
            BodyColor = new Color(0.8f, 0.5f, 1f), EyeColor = new Color(1f, 0.95f, 0.6f),
            EngageRange = 30f,
            Perk = FamiliarPerk.CrosshairTarget,
            Attacks =
            {
                Bolt("Glimpse", DamageType.Psychic, new Color(0.8f, 0.5f, 1f), 9f, 2f, 90f, 0.12f,
                    payload: StatusLibrary.Mark())
            }
        };

        private static FamiliarDefinition StormSprite() => new FamiliarDefinition
        {
            Id = "storm_sprite",
            DisplayName = "Storm Sprite",
            Description = "Arcs lightning between nearby enemies, shocking them.",
            Health = 50f, MoveSpeed = 9f,
            BodyColor = Palette.Lightning, EyeColor = new Color(1f, 1f, 0.8f),
            EngageRange = 18f,
            Attacks =
            {
                new AttackDefinition
                {
                    Name = "Arc", DamageType = DamageType.Energy, Tint = Palette.Lightning,
                    MinRange = 0f, MaxRange = 18f, Cooldown = 2.5f, InitialDelay = 1f,
                    Sequence =
                    {
                        new AimAtTargetEffect(),
                        new StatusPayloadEffect
                        {
                            Status = StatusId.Shock, Duration = 4f, Stacks = ShockStatus.SpellStacks,
                            Magnitude = ShockStatus.DamagePerStack
                        },
                        new ChainEffect { Damage = 10f, FirstRange = 20f, JumpRange = 8f, BaseJumps = 3, JumpsPerLevel = 0f }
                    }
                }
            }
        };

        /// <summary>Drops a freezing patch rather than shooting, so it controls space.</summary>
        private static FamiliarDefinition RimeWatcher() => new FamiliarDefinition
        {
            Id = "rime_watcher",
            DisplayName = "Rime Watcher",
            Description = "Freezes the ground under enemies, slowing them.",
            Health = 55f, MoveSpeed = 7.5f,
            BodyColor = Palette.Ice, EyeColor = new Color(0.85f, 0.98f, 1f),
            EngageRange = 18f,
            Attacks =
            {
                new AttackDefinition
                {
                    Name = "Rime Pool", DamageType = DamageType.Kinetic, Tint = Palette.Ice,
                    MinRange = 0f, MaxRange = 20f, Cooldown = 6f, InitialDelay = 1.5f,
                    Sequence =
                    {
                        new TargetGroundPointEffect { LeadDistance = 0f },
                        new StatusPayloadEffect
                        {
                            Status = StatusId.Frost, Duration = 3f, Stacks = 12, Magnitude = FrostStatus.SlowPerStack
                        },
                        new LingeringZoneEffect { Radius = 3.4f, Duration = 5f, DamagePerTick = 3f, TickInterval = 0.5f }
                    }
                }
            }
        };

        private static FamiliarDefinition AegisMote() => new FamiliarDefinition
        {
            Id = "aegis_mote",
            DisplayName = "Aegis Mote",
            Description = "Orbits you and blocks an enemy projectile every few seconds.",
            Health = 70f, MoveSpeed = 12f,
            BodyColor = new Color(0.9f, 0.85f, 0.5f), EyeColor = Color.white,
            FollowDistance = 1.4f, HoverHeight = 1.3f,
            Perk = FamiliarPerk.BlockProjectiles, PerkInterval = 3f, PerkRange = 4f
        };

        private static FamiliarDefinition Magpie() => new FamiliarDefinition
        {
            Id = "magpie",
            DisplayName = "Magpie",
            Description = "Fetches health and mana orbs for you.",
            Health = 50f, MoveSpeed = 10f,
            BodyColor = new Color(0.15f, 0.2f, 0.35f), EyeColor = new Color(0.9f, 0.95f, 1f),
            Perk = FamiliarPerk.FetchOrbs, PerkRange = 25f
        };

        private static FamiliarDefinition ManaSprite() => new FamiliarDefinition
        {
            Id = "mana_sprite",
            DisplayName = "Mana Sprite",
            Description = "Restores your mana while enemies are near.",
            Health = 50f, MoveSpeed = 8.5f,
            BodyColor = new Color(0.45f, 0.6f, 1f), EyeColor = new Color(0.85f, 0.9f, 1f),
            Perk = FamiliarPerk.ManaNearEnemies, PerkAmount = 4f, PerkRange = 15f
        };

        private static FamiliarDefinition CoinImp() => new FamiliarDefinition
        {
            Id = "coin_imp",
            DisplayName = "Coin Imp",
            Description = "Raises your Luck while it lives.",
            Health = 45f, MoveSpeed = 8f,
            BodyColor = new Color(1f, 0.82f, 0.3f), EyeColor = new Color(1f, 0.6f, 0.2f),
            Perk = FamiliarPerk.LuckAura, PerkAmount = 4f
        };

        /// <summary>The only familiar enemies go after. It floats low, where their attacks can reach it.</summary>
        private static FamiliarDefinition Homunculus() => new FamiliarDefinition
        {
            Id = "homunculus",
            DisplayName = "Homunculus",
            Description = "Enemies attack it instead of you. If it dies, it stays dead until the next floor.",
            Health = 100f, MoveSpeed = 7f, Radius = 0.45f,
            BodyColor = new Color(0.75f, 0.45f, 0.4f), EyeColor = new Color(1f, 0.9f, 0.5f),
            BodyDiameter = 0.8f, HoverHeight = 0.8f, FollowDistance = 3f,
            Perk = FamiliarPerk.Decoy
        };

        private static FamiliarDefinition Gremlin() => new FamiliarDefinition
        {
            Id = "gremlin",
            DisplayName = "Gremlin",
            Description = "Reloads the guns you are not holding.",
            Health = 50f, MoveSpeed = 9f,
            BodyColor = new Color(0.4f, 0.6f, 0.3f), EyeColor = new Color(1f, 0.8f, 0.3f),
            Perk = FamiliarPerk.ReloadHolstered, PerkInterval = 2f, PerkAmount = 0.25f
        };

        /// <summary>A projectile attack at the familiar's target, optionally carrying a status or a small splash.</summary>
        private static AttackDefinition Bolt(string name, DamageType type, Color tint, float damage, float cooldown,
            float speed, float radius, float splash = 0f, StatusApplication? payload = null)
        {
            var attack = new AttackDefinition
            {
                Name = name, DamageType = type, Tint = tint,
                MinRange = 0f, MaxRange = 22f, Cooldown = cooldown, InitialDelay = 0.6f
            };

            attack.Sequence.Add(new AimAtTargetEffect());
            if (payload.HasValue)
            {
                StatusApplication p = payload.Value;
                attack.Sequence.Add(new StatusPayloadEffect { Status = p.Id, Duration = p.Duration, Stacks = p.Stacks, Magnitude = p.Magnitude });
            }

            attack.Sequence.Add(new SpawnProjectileEffect
            {
                Damage = damage, Speed = speed, Radius = radius, Lifetime = 3f, SpreadDegrees = 1f,
                SplashRadius = splash, SplashDamage = splash > 0f ? damage * 0.5f : 0f
            });
            return attack;
        }
    }
}
