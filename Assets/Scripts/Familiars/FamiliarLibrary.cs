using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The familiar roster. Same hybrid as every other library: built-ins in code, any
    /// <see cref="FamiliarAsset"/> under a Resources folder merged over the top by id.
    ///
    /// The four built-ins deliberately cover four different jobs, because "familiar" is a
    /// slot rather than a role: one shoots, one heals, one is a walking stat buff, and one
    /// casts an area control spell.
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
        public static List<FamiliarDefinition> BuiltIn()
        {
            return new List<FamiliarDefinition> { Wisp(), Mender(), Imp(), Watcher() };
        }

        // ---------------------------------------------------------------- archetypes

        /// <summary>The plain one: it shoots things. Damage numbers are modest by design -
        /// a familiar is a steady trickle, not a second gun.</summary>
        private static FamiliarDefinition Wisp()
        {
            return new FamiliarDefinition
            {
                Id = "wisp",
                DisplayName = "Arcane Wisp",
                Description = "A mote of bound starlight. Picks a target and keeps at it.",
                Health = 55f, MoveSpeed = 8f,
                BodyColor = Palette.Arcane, EyeColor = new Color(1f, 0.95f, 0.8f),
                EngageRange = 20f,
                Attacks =
                {
                    new AttackDefinition
                    {
                        Name = "Starbolt", DamageType = DamageType.Energy, Tint = Palette.Arcane,
                        MinRange = 0f, MaxRange = 22f, Cooldown = 1.4f, InitialDelay = 0.4f,
                        Sequence =
                        {
                            new AimAtTargetEffect(),
                            new SpawnProjectileEffect
                            {
                                Damage = 11f, Speed = 34f, Radius = 0.16f,
                                Lifetime = 4f, SpreadDegrees = 1f
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Heals its owner rather than attacking.
        ///
        /// It still needs an enemy in range to act, because an ability only fires when the
        /// familiar has a target. That is deliberate rather than incidental: a familiar that
        /// healed out of combat would top you up for free between every room and delete
        /// attrition from the run entirely. The description says so, since a healer that
        /// appears to do nothing in an empty room otherwise reads as broken.
        /// </summary>
        private static FamiliarDefinition Mender()
        {
            return new FamiliarDefinition
            {
                Id = "mender",
                DisplayName = "Mender",
                Description = "A patient little thing that stitches you back together, slowly. " +
                              "Only works while there is something to fight.",
                Health = 70f, MoveSpeed = 8.5f,
                BodyColor = new Color(0.55f, 0.95f, 0.65f), EyeColor = new Color(0.9f, 1f, 0.85f),
                FollowDistance = 2.2f,
                EngageRange = 24f,
                Attacks =
                {
                    new AttackDefinition
                    {
                        Name = "Mend", DamageType = DamageType.Necrotic, Tint = Palette.Poison,
                        MinRange = 0f, MaxRange = 999f, Cooldown = 6f, InitialDelay = 3f,
                        RequiresLineOfSight = false,
                        Sequence =
                        {
                            new HealOwnerEffect { Amount = 14f },
                            new VfxOnOwnerEffect { Diameter = 1.4f, Lifetime = 0.4f }
                        }
                    }
                }
            };
        }

        /// <summary>The walking buff. No attacks at all - it is worth carrying for the aura.</summary>
        private static FamiliarDefinition Imp()
        {
            return new FamiliarDefinition
            {
                Id = "imp",
                DisplayName = "Fel Imp",
                Description = "Sharpens everything you do, and never stops complaining about it.",
                Health = 45f, MoveSpeed = 8f,
                BodyColor = new Color(0.85f, 0.35f, 0.30f), EyeColor = new Color(1f, 0.75f, 0.35f),
                FollowDistance = 2.4f,
                Auras =
                {
                    new FamiliarAura { Attribute = Attr.DamageDealt, Mode = ModifierMode.Percent, Amount = 0.12f },
                    new FamiliarAura { Attribute = Attr.AttackSpeed, Mode = ModifierMode.Percent, Amount = 0.08f }
                },
                Attacks =
                {
                    // Weak on purpose: the aura is the reason to take it, and giving it no
                    // attack at all makes it feel inert next to the others.
                    new AttackDefinition
                    {
                        Name = "Cinder Nip", DamageType = DamageType.Energy, Tint = Palette.Fire,
                        MinRange = 0f, MaxRange = 14f, Cooldown = 2.2f, InitialDelay = 1f,
                        Sequence =
                        {
                            new AimAtTargetEffect(),
                            new SpawnProjectileEffect
                            {
                                Damage = 6f, Speed = 28f, Radius = 0.14f, Lifetime = 3f
                            }
                        }
                    }
                }
            };
        }

        /// <summary>The caster: drops a chilling zone rather than shooting, so it controls space.</summary>
        private static FamiliarDefinition Watcher()
        {
            return new FamiliarDefinition
            {
                Id = "watcher",
                DisplayName = "Rime Watcher",
                Description = "Freezes the ground under whatever you are looking at.",
                Health = 50f, MoveSpeed = 7.5f,
                BodyColor = Palette.Ice, EyeColor = new Color(0.85f, 0.98f, 1f),
                EngageRange = 18f,
                Attacks =
                {
                    new AttackDefinition
                    {
                        Name = "Rime Pool", DamageType = DamageType.Kinetic, Tint = Palette.Ice,
                        MinRange = 0f, MaxRange = 20f, Cooldown = 7f, InitialDelay = 2f,
                        Sequence =
                        {
                            new TargetGroundPointEffect { LeadDistance = 0f },
                            new StatusPayloadEffect
                            {
                                Status = StatusId.Frost, Duration = 2.5f, Stacks = 1, Magnitude = 0.11f
                            },
                            new LingeringZoneEffect
                            {
                                Radius = 3.4f, Duration = 4f, DamagePerTick = 3f, TickInterval = 0.5f
                            }
                        }
                    }
                }
            };
        }
    }
}
