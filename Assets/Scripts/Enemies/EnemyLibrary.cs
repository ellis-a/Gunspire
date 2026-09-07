using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The enemy roster. Built-ins in code so a fresh clone runs with nothing authored, and any
    /// <see cref="EnemyAsset"/> under a Resources folder merged in by id - a matching id
    /// replaces the built-in, a new id joins the roster and starts appearing in combat rooms if
    /// it says so.
    ///
    /// Damage numbers here are what an attack is worth on floor one. Floor scaling is applied
    /// through the ability context at spawn, not baked into these values, so an authored enemy
    /// does not have to know the formula.
    /// </summary>
    public static class EnemyLibrary
    {
        public const string BossId = "tower_warden";

        private static List<EnemyDefinition> _all;

        public static IReadOnlyList<EnemyDefinition> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        /// <summary>Drops the cached roster so authored assets are picked up again.</summary>
        public static void Reload() => _all = null;

        /// <summary>Fresh copy, safe to mutate per spawn.</summary>
        public static EnemyDefinition Get(string id)
        {
            EnemyDefinition found = Peek(id);
            return found != null ? found.Clone() : null;
        }

        /// <summary>The shared instance. Do not mutate.</summary>
        public static EnemyDefinition Peek(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>What ordinary combat rooms may roll.</summary>
        public static List<EnemyDefinition> StandardRoster()
        {
            var pool = new List<EnemyDefinition>();
            for (int i = 0; i < All.Count; i++)
                if (All[i].InStandardRoster) pool.Add(All[i]);
            return pool;
        }

        private static void Build()
        {
            _all = BuiltIn();

            EnemyAsset[] authored = Resources.LoadAll<EnemyAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                EnemyDefinition def = authored[i] != null ? authored[i].Enemy : null;
                if (def == null) continue;

                if (string.IsNullOrEmpty(def.Id))
                {
                    Debug.LogWarning("Enemy asset \"" + authored[i].name + "\" has no Id and was ignored.");
                    continue;
                }

                int existing = _all.FindIndex(e => e.Id == def.Id);
                if (existing >= 0) _all[existing] = def;
                else _all.Add(def);
            }
        }

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<EnemyDefinition> BuiltIn()
        {
            return new List<EnemyDefinition>
            {
                Cultist(), Hound(), Warden(), Sentinel(), Frostcaller(), Gazer(), TowerWarden()
            };
        }

        // ---------------------------------------------------------------- archetypes

        private static EnemyDefinition Cultist()
        {
            return new EnemyDefinition
            {
                Id = "cultist",
                DisplayName = "Cultist",
                Health = 55f, MoveSpeed = 4.2f, Radius = 0.42f, BodyHeight = 1.8f, BodyWidth = 0.8f,
                PreferredRange = 13f, MinComfortRange = 8f,
                BodyColor = Palette.EnemyRanged, EyeColor = new Color(1f, 0.5f, 1f),
                Resists = DamageType.Astral, WeakTo = DamageType.Nature,
                Attacks =
                {
                    new EnemyAttackDefinition
                    {
                        Name = "Arcane Volley", DamageType = DamageType.Astral, Tint = Palette.Arcane,
                        MinRange = 0f, MaxRange = 26f, Cooldown = 2.6f, Priority = 0,
                        Sequence =
                        {
                            new TelegraphFlashEffect { Duration = 0.55f, Radius = 0.35f, Height = 1.35f },
                            new WaitEffect { Seconds = 0.55f },
                            new RepeatEffect
                            {
                                Times = 3, Interval = 0.16f,
                                Body =
                                {
                                    new AimAtTargetEffect(),
                                    new SpawnProjectileEffect
                                    {
                                        Damage = 9f, Speed = 20f, Radius = 0.24f,
                                        Lifetime = 6f, SpreadDegrees = 1.5f
                                    }
                                }
                            },
                            new WaitEffect { Seconds = 0.45f }
                        }
                    }
                }
            };
        }

        private static EnemyDefinition Hound()
        {
            return new EnemyDefinition
            {
                Id = "hound",
                DisplayName = "Hound",
                Health = 46f, MoveSpeed = 6.9f, Radius = 0.4f, BodyHeight = 1.2f, BodyWidth = 0.75f,
                PreferredRange = 1.6f, MinComfortRange = 0f, StrafeInterval = 2.6f,
                BodyColor = Palette.EnemyMelee, EyeColor = new Color(1f, 0.4f, 0.2f),
                Resists = DamageType.Normal, WeakTo = DamageType.Frost,
                Attacks =
                {
                    new EnemyAttackDefinition
                    {
                        Name = "Lunge", DamageType = DamageType.Normal, Tint = Palette.EnemyMelee,
                        MinRange = 0f, MaxRange = 4.6f, Cooldown = 1.9f, Priority = 0,
                        Sequence =
                        {
                            new TelegraphFlashEffect { Duration = 0.55f, Radius = 0.55f, Height = 1.1f },
                            new WaitEffect { Seconds = 0.55f },
                            // Direction locks here, so strafing during the wind-up beats it.
                            new AimAtTargetEffect { Flatten = true },
                            new ImpulseSelfEffect { Speed = 16f },
                            new RepeatEffect
                            {
                                Times = 6, Interval = 0.04f,
                                Body =
                                {
                                    // The swing follows the hound, but not the player.
                                    new OriginFromCasterEffect(),
                                    new SelectConeEffect { Range = 3.4f, HalfAngle = 60f, RequireLineOfSight = false },
                                    new DealDamageEffect { Amount = 17f, Knockback = 6f, OnlyOncePerCast = true }
                                }
                            },
                            new WaitEffect { Seconds = 0.6f }
                        }
                    }
                }
            };
        }

        private static EnemyDefinition Warden()
        {
            return new EnemyDefinition
            {
                Id = "warden",
                DisplayName = "Warden",
                Health = 95f, MoveSpeed = 3.1f, Radius = 0.5f, BodyHeight = 2.2f, BodyWidth = 0.95f,
                PreferredRange = 18f, MinComfortRange = 12f,
                BodyColor = Palette.EnemyBeam, EyeColor = new Color(0.5f, 0.9f, 1f),
                Resists = DamageType.Astral, WeakTo = DamageType.Shadow,
                Attacks =
                {
                    new EnemyAttackDefinition
                    {
                        Name = "Sweeping Beam", DamageType = DamageType.Astral, Tint = Palette.Lightning,
                        MinRange = 6f, MaxRange = 38f, Cooldown = 5.5f, Priority = 0,
                        Sequence =
                        {
                            new AimAtTargetEffect { Flatten = true },
                            new TelegraphLineEffect { Length = 40f, Width = 0.33f, Duration = 1.1f },
                            new WaitEffect { Seconds = 1.1f },
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Shock, Duration = 3f, Stacks = 1, Magnitude = 0.12f
                            },
                            new BeamEffect
                            {
                                Duration = 1.4f, DamagePerTick = 5.5f, SweepDegreesPerSecond = 26f
                            },
                            new WaitEffect { Seconds = 0.9f }
                        }
                    }
                }
            };
        }

        private static EnemyDefinition Sentinel()
        {
            return new EnemyDefinition
            {
                Id = "sentinel",
                DisplayName = "Sentinel",
                Health = 84f, MoveSpeed = 3.6f, Radius = 0.55f, BodyHeight = 2.0f, BodyWidth = 1.05f,
                PreferredRange = 11f, MinComfortRange = 6f,
                BodyColor = Palette.EnemyCaster, EyeColor = new Color(1f, 0.8f, 0.3f),
                Resists = DamageType.Fire, WeakTo = DamageType.Frost,
                Attacks =
                {
                    new EnemyAttackDefinition
                    {
                        Name = "Cinder Slam", DamageType = DamageType.Fire, Tint = Palette.Fire,
                        MinRange = 0f, MaxRange = 24f, Cooldown = 4.6f, Priority = 0,
                        Sequence =
                        {
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Burn, Duration = 4f, Stacks = 2, Magnitude = 4f
                            },
                            new RepeatEffect
                            {
                                Times = 3, Interval = 0.45f,
                                Body =
                                {
                                    // Each circle lands where the player was, so standing still is fatal.
                                    new TargetGroundPointEffect { LeadDistance = 3f },
                                    new TelegraphCircleEffect { Radius = 3.5f, Duration = 0.9f, ScaleWithLevel = false },
                                    new DelayedBlastEffect
                                    {
                                        Delay = 0.9f, Radius = 3.5f, Damage = 22f,
                                        Knockback = 7f, ScaleRadiusWithLevel = false
                                    }
                                }
                            },
                            new WaitEffect { Seconds = 1.6f }
                        }
                    }
                }
            };
        }

        private static EnemyDefinition Frostcaller()
        {
            var body = new Color(0.35f, 0.55f, 0.75f);

            return new EnemyDefinition
            {
                Id = "frostcaller",
                DisplayName = "Frostcaller",
                Health = 68f, MoveSpeed = 4.6f, Radius = 0.45f, BodyHeight = 1.9f, BodyWidth = 0.85f,
                PreferredRange = 12f, MinComfortRange = 7f,
                BodyColor = body, EyeColor = Palette.Ice,
                Resists = DamageType.Frost, WeakTo = DamageType.Fire,
                Attacks =
                {
                    new EnemyAttackDefinition
                    {
                        Name = "Ice Shards", DamageType = DamageType.Frost, Tint = Palette.Ice,
                        MinRange = 5f, MaxRange = 28f, Cooldown = 3.2f, Priority = 0,
                        Sequence =
                        {
                            new TelegraphFlashEffect { Duration = 0.5f, Radius = 0.35f, Height = 1.35f },
                            new WaitEffect { Seconds = 0.5f },
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Chill, Duration = 3f, Stacks = 1, Magnitude = 0.11f
                            },
                            new RepeatEffect
                            {
                                Times = 4, Interval = 0.14f,
                                Body =
                                {
                                    new AimAtTargetEffect(),
                                    new SpawnProjectileEffect
                                    {
                                        Damage = 8f, Speed = 24f, Radius = 0.22f,
                                        Lifetime = 6f, SpreadDegrees = 2f
                                    }
                                }
                            },
                            new WaitEffect { Seconds = 0.45f }
                        }
                    },

                    new EnemyAttackDefinition
                    {
                        Name = "Frost Breath", DamageType = DamageType.Frost, Tint = Palette.Ice,
                        MinRange = 0f, MaxRange = 10f, Cooldown = 6.5f, Priority = 1,
                        Sequence =
                        {
                            // Aim first so the warning cone shows exactly where the breath will go.
                            new AimAtTargetEffect { Flatten = true },
                            new TelegraphConeEffect { Range = 11f, HalfAngle = 32f, Duration = 0.85f },
                            new WaitEffect { Seconds = 0.85f },
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Chill, Duration = 4f, Stacks = 2, Magnitude = 0.11f
                            },
                            new SelectConeEffect { Range = 11f, HalfAngle = 32f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 16f },
                            new VfxConeEffect { Range = 11f, HalfAngle = 32f, Alpha = 0.4f, Lifetime = 0.3f },
                            new WaitEffect { Seconds = 0.8f }
                        }
                    }
                }
            };
        }

        private static EnemyDefinition Gazer()
        {
            return new EnemyDefinition
            {
                Id = "gazer",
                DisplayName = "Gazer",

                // Fragile. It is the only thing in the roster the melee bash cannot reach, so
                // its answer to being shot has to be dying quickly rather than soaking.
                Health = 48f, MoveSpeed = 4.4f, Radius = 0.45f, BodyHeight = 1.1f, BodyWidth = 1.15f,
                PreferredRange = 15f, MinComfortRange = 9f, StrafeInterval = 2.2f,
                TurnSpeed = 5f,               // slow to track, so circling it beats standing still
                Flying = true, HoverHeight = 3.6f,
                EyeHeight = 0.55f,            // it looks out of its middle; there is no head
                Shape = BodyShape.Eyeball,
                BodyColor = Palette.EnemyFlyer, EyeColor = new Color(1f, 0.35f, 0.30f),
                Resists = DamageType.Shadow, WeakTo = DamageType.Astral,
                Attacks =
                {
                    // The cheap attack: dodgeable bolts that punish standing in the open.
                    new EnemyAttackDefinition
                    {
                        Name = "Laser Bolts", DamageType = DamageType.Shadow,
                        Tint = new Color(1f, 0.4f, 0.35f),
                        MinRange = 0f, MaxRange = 30f, Cooldown = 2.9f, Priority = 0,
                        Sequence =
                        {
                            new TelegraphFlashEffect { Duration = 0.5f, Radius = 0.4f, Height = 0.6f },
                            new WaitEffect { Seconds = 0.5f },
                            new RepeatEffect
                            {
                                Times = 3, Interval = 0.18f,
                                Body =
                                {
                                    new AimAtTargetEffect(),
                                    new SpawnProjectileEffect
                                    {
                                        Damage = 8f, Speed = 26f, Radius = 0.18f,
                                        Lifetime = 5f, SpreadDegrees = 1.8f
                                    }
                                }
                            },
                            new WaitEffect { Seconds = 0.5f }
                        }
                    },

                    // The signature. Telegraphed, then a slow sweep - break line of sight or move.
                    new EnemyAttackDefinition
                    {
                        Name = "Searing Gaze", DamageType = DamageType.Shadow,
                        Tint = new Color(1f, 0.3f, 0.25f),
                        MinRange = 5f, MaxRange = 34f, Cooldown = 7.5f, Priority = 1,
                        Sequence =
                        {
                            // Deliberately not flattened: the line has to show the real angle.
                            new AimAtTargetEffect(),
                            new TelegraphLineEffect { Length = 36f, Width = 0.3f, Duration = 1.15f },
                            new WaitEffect { Seconds = 1.15f },
                            new BeamEffect
                            {
                                Duration = 1.4f, Width = 0.45f, DamagePerTick = 5f,
                                SweepDegreesPerSecond = 18f,

                                // Firing down from hover height, the default 0.25 would send the
                                // beam over the player's head from inside its preferred range.
                                MaxPitch = 0.95f
                            },
                            new WaitEffect { Seconds = 1.1f }
                        }
                    }
                }
            };
        }

        private static EnemyDefinition TowerWarden()
        {
            return new EnemyDefinition
            {
                Id = BossId,
                DisplayName = "Tower Warden",
                InStandardRoster = false,
                Health = 620f, MoveSpeed = 4.0f, Radius = 0.9f, BodyHeight = 3.2f, BodyWidth = 1.6f,
                PreferredRange = 14f, MinComfortRange = 8f, StrafeInterval = 2.2f,
                BodyColor = Palette.EnemyBoss, EyeColor = new Color(1f, 0.4f, 0.6f),
                Crowned = true,

                // Shrugs off every school a little, and is soft to none of them.
                ResistsEverything = true, BroadResistance = 0.18f,
                Attacks =
                {
                    new EnemyAttackDefinition
                    {
                        Name = "Arcane Fan", DamageType = DamageType.Astral, Tint = Palette.Arcane,
                        MinRange = 0f, MaxRange = 34f, Cooldown = 3.4f, Priority = 0,
                        Sequence =
                        {
                            new TelegraphFlashEffect { Duration = 0.6f, Radius = 0.6f, Height = 2.4f },
                            new WaitEffect { Seconds = 0.6f },
                            new AimAtTargetEffect(),
                            new SpawnProjectileEffect
                            {
                                Damage = 12f, Speed = 22f, Radius = 0.3f,
                                Lifetime = 6f, Count = 7, ArcSpreadDegrees = 40f
                            },
                            new WaitEffect { Seconds = 0.5f }
                        }
                    },

                    new EnemyAttackDefinition
                    {
                        Name = "Warden Beam", DamageType = DamageType.Astral, Tint = Palette.Lightning,
                        MinRange = 5f, MaxRange = 40f, Cooldown = 7.5f, Priority = 1,
                        Sequence =
                        {
                            new AimAtTargetEffect { Flatten = true },
                            new TelegraphLineEffect { Length = 40f, Width = 0.5f, Duration = 1.1f },
                            new WaitEffect { Seconds = 1.1f },
                            new BeamEffect
                            {
                                Duration = 2.4f, Width = 0.8f,
                                DamagePerTick = 7f, SweepDegreesPerSecond = 30f
                            },
                            new WaitEffect { Seconds = 0.9f }
                        }
                    },

                    new EnemyAttackDefinition
                    {
                        Name = "Cinder Rain", DamageType = DamageType.Fire, Tint = Palette.Fire,
                        MinRange = 0f, MaxRange = 28f, Cooldown = 6.5f, Priority = 1,
                        Sequence =
                        {
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Burn, Duration = 5f, Stacks = 2, Magnitude = 5f
                            },
                            new RepeatEffect
                            {
                                Times = 5, Interval = 0.4f,
                                Body =
                                {
                                    new TargetGroundPointEffect { LeadDistance = 4f },
                                    new TelegraphCircleEffect { Radius = 4.2f, Duration = 0.9f, ScaleWithLevel = false },
                                    new DelayedBlastEffect
                                    {
                                        Delay = 0.9f, Radius = 4.2f, Damage = 26f,
                                        Knockback = 8f, ScaleRadiusWithLevel = false
                                    }
                                }
                            },
                            new WaitEffect { Seconds = 1.4f }
                        }
                    },

                    new EnemyAttackDefinition
                    {
                        Name = "Withering Breath", DamageType = DamageType.Nature, Tint = Palette.Poison,
                        MinRange = 0f, MaxRange = 13f, Cooldown = 8f, Priority = 2,
                        Sequence =
                        {
                            new AimAtTargetEffect { Flatten = true },
                            new TelegraphConeEffect { Range = 14f, HalfAngle = 40f, Duration = 0.85f },
                            new WaitEffect { Seconds = 0.85f },
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Blight, Duration = 8f, Stacks = 3, Magnitude = 4f
                            },
                            new SelectConeEffect { Range = 14f, HalfAngle = 40f, RequireLineOfSight = false },
                            new DealDamageEffect { Amount = 30f },
                            new VfxConeEffect { Range = 14f, HalfAngle = 40f, Alpha = 0.4f, Lifetime = 0.3f },
                            new WaitEffect { Seconds = 0.8f }
                        }
                    }
                }
            };
        }
    }
}
