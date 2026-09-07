using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Every sound in the game, synthesised on demand and cached.
    ///
    /// Same hybrid as the weapon and spell rosters: a built-in exists for everything, and any
    /// AudioClip dropped into a <c>Resources/Audio</c> folder replaces the built-in whose id
    /// matches its filename. So the game is never silent, and a real recording can be swapped
    /// in one sound at a time without touching code.
    ///
    /// Gun sounds are not authored individually. They are derived from the weapon's own stats,
    /// which matters because the twelve weapon assets are already committed - a new field on
    /// WeaponDefinition would deserialise to its default and every gun would sound identical.
    /// Reading damage, rate and school instead means the roster sounds varied with no asset
    /// edits, and a gun tuned in the Inspector changes pitch to match.
    /// </summary>
    public static class SoundLibrary
    {
        /// <summary>Drop overrides in <c>Assets/Resources/Audio/</c>. The filename is the id.</summary>
        public const string ResourceFolder = "Audio";

        public const string ImpactId = "impact";
        public const string HitConfirmId = "hit_confirm";
        public const string DryFireId = "gun_dry";
        public const string SpinUpId = "gun_spin";

        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        private static Dictionary<string, AudioClip> _overrides;

        /// <summary>True once we have tried and failed to synthesise. Stops us retrying every shot.</summary>
        private static bool _synthesisBroken;

        /// <summary>Forget every cached clip. Call after adding files to Resources/Audio.</summary>
        public static void Reload()
        {
            Cache.Clear();
            _overrides = null;
            _synthesisBroken = false;
        }

        // ---------------------------------------------------------------- lookup

        /// <summary>The firing sound for a gun, derived from its stats.</summary>
        public static AudioClip ForWeapon(WeaponDefinition def)
        {
            if (def == null) return Get(DryFireId);
            return Resolve("gun_" + def.Id, () => WeaponRecipe(def));
        }

        /// <summary>The surface impact tick for a school of damage.</summary>
        public static AudioClip Impact(DamageType type)
        {
            string id = type == DamageType.Normal ? ImpactId : ImpactId + "_" + DamageTypes.Name(type).ToLowerInvariant();
            return Resolve(id, () => ImpactRecipe(type));
        }

        /// <summary>
        /// The onset of a beam. One shot at the moment it fires rather than a sustained loop:
        /// beams here run anywhere from 1.3 to 2.4 seconds and the pool plays one-shots, so a
        /// loop would need a source held for the beam's lifetime. The visual carries the tail.
        /// </summary>
        public static AudioClip Beam(DamageType type)
        {
            string id = "beam_" + DamageTypes.Name(type).ToLowerInvariant();
            return Resolve(id, () => BeamRecipe(type));
        }

        /// <summary>One of the named built-ins.</summary>
        public static AudioClip Get(string id)
        {
            return Resolve(id, () => NamedRecipe(id));
        }

        private static AudioClip Resolve(string id, System.Func<SoundRecipe> recipe)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (Cache.TryGetValue(id, out AudioClip cached)) return cached;

            AudioClip clip = FindOverride(id);
            if (clip == null && !_synthesisBroken)
            {
                try
                {
                    clip = Synth.Render(id, recipe(), Synth.SeedFor(id));
                }
                catch (System.Exception e)
                {
                    // No audio device, or a headless build. Silence beats an exception per shot.
                    _synthesisBroken = true;
                    Debug.LogWarning("SoundLibrary could not synthesise audio, running silent: " + e.Message);
                }
            }

            Cache[id] = clip;
            return clip;
        }

        private static AudioClip FindOverride(string id)
        {
            if (_overrides == null)
            {
                _overrides = new Dictionary<string, AudioClip>();

                AudioClip[] loaded = Resources.LoadAll<AudioClip>(ResourceFolder);
                for (int i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] == null) continue;
                    _overrides[loaded[i].name] = loaded[i];
                }
            }

            return _overrides.TryGetValue(id, out AudioClip clip) ? clip : null;
        }

        // ---------------------------------------------------------------- weapon recipes

        /// <summary>
        /// Per-school character. These shift a gun's timbre without changing how heavy it
        /// reads, so a fire uzi and a frost uzi are obviously different weapons and obviously
        /// the same size of weapon.
        /// </summary>
        private struct SchoolVoice
        {
            public float BodyScale;    // multiplies the fundamental
            public float NoiseMix;
            public float Brightness;
            public float DecayScale;   // above 1 is snappier, below 1 rings on
            public float Crack;
        }

        private static SchoolVoice VoiceFor(DamageType type)
        {
            switch (type)
            {
                // Roar rather than crack: noisy, dark, and it hangs around.
                case DamageType.Fire:
                    return new SchoolVoice { BodyScale = 0.85f, NoiseMix = 0.88f, Brightness = 0.42f, DecayScale = 0.75f, Crack = 0.45f };

                // Brittle and immediate. High body, very little tail.
                case DamageType.Frost:
                    return new SchoolVoice { BodyScale = 1.75f, NoiseMix = 0.45f, Brightness = 0.95f, DecayScale = 1.45f, Crack = 0.7f };

                case DamageType.Nature:
                    return new SchoolVoice { BodyScale = 0.95f, NoiseMix = 0.66f, Brightness = 0.34f, DecayScale = 0.95f, Crack = 0.35f };

                // Muffled, as though the sound is being absorbed on its way out.
                case DamageType.Shadow:
                    return new SchoolVoice { BodyScale = 0.60f, NoiseMix = 0.52f, Brightness = 0.16f, DecayScale = 0.65f, Crack = 0.2f };

                // Almost tonal - a struck bell more than a bang.
                case DamageType.Astral:
                    return new SchoolVoice { BodyScale = 2.30f, NoiseMix = 0.28f, Brightness = 0.88f, DecayScale = 0.85f, Crack = 0.4f };

                default:
                    return new SchoolVoice { BodyScale = 1f, NoiseMix = 0.80f, Brightness = 0.55f, DecayScale = 1f, Crack = 0.6f };
            }
        }

        private static SoundRecipe WeaponRecipe(WeaponDefinition def)
        {
            SchoolVoice voice = VoiceFor(def.DamageType);

            // Same per-trigger figure the balance table reports, so what a gun sounds like and
            // what it reads as on the table cannot drift apart.
            float punch = def.Damage * def.RoundsPerTrigger + def.SplashDamage;
            float heft = Mathf.Clamp01(Mathf.InverseLerp(6f, 90f, punch));

            SoundRecipe r = SoundRecipe.Default;
            r.Duration = Mathf.Lerp(0.085f, 0.42f, heft);
            r.BodyHz = Mathf.Lerp(215f, 68f, heft) * voice.BodyScale;
            r.BodyEndHz = r.BodyHz * Mathf.Lerp(0.55f, 0.28f, heft);
            r.NoiseMix = voice.NoiseMix;
            r.Brightness = voice.Brightness;
            r.Decay = Mathf.Lerp(3.6f, 1.9f, heft) * voice.DecayScale;
            r.Crack = voice.Crack;
            r.Drive = Mathf.Lerp(1.4f, 2.1f, heft);
            r.Volume = Mathf.Lerp(0.55f, 0.95f, heft);

            // A launcher lobs rather than cracks, so trade the transient for body.
            if (def.Delivery == DeliveryKind.Projectile)
            {
                r.NoiseMix *= 0.80f;
                r.Crack *= 0.55f;
            }

            // An 900rpm uzi whose shot lasts a third of a second is just mush. Keep each shot
            // comfortably inside its own interval so the rhythm of firing stays audible.
            float interval = def.SecondsBetweenShots;
            r.Duration = Mathf.Max(0.045f, Mathf.Min(r.Duration, interval * 2.2f));

            return r;
        }

        /// <summary>
        /// Why a gun sounds the way it does, for the editor table. Nothing derives behaviour
        /// from this - it exists so "my new gun sounds wrong" is a readable problem.
        /// </summary>
        public static string DescribeWeaponSound(WeaponDefinition def)
        {
            if (def == null) return "no weapon";

            bool overridden = FindOverride("gun_" + def.Id) != null;
            if (overridden) return "override clip gun_" + def.Id;

            SoundRecipe r = WeaponRecipe(def);
            return string.Format("{0,5:0.000}s  {1,6:0}Hz>{2,4:0}Hz  noise {3:0.00}  bright {4:0.00}  decay {5:0.0}  vol {6:0.00}",
                r.Duration, r.BodyHz, r.BodyEndHz, r.NoiseMix, r.Brightness, r.Decay, r.Volume);
        }

        // ---------------------------------------------------------------- other recipes

        private static SoundRecipe ImpactRecipe(DamageType type)
        {
            SchoolVoice voice = VoiceFor(type);

            SoundRecipe r = SoundRecipe.Default;
            r.Duration = 0.075f;
            r.BodyHz = 320f * voice.BodyScale;
            r.BodyEndHz = 90f * voice.BodyScale;
            r.NoiseMix = Mathf.Clamp01(voice.NoiseMix + 0.1f);
            r.Brightness = Mathf.Clamp01(voice.Brightness + 0.15f);
            r.Decay = 4.5f;
            r.Crack = 0.8f;
            r.Drive = 1.3f;

            // Impacts fire far more often than shots, several at once from one explosion.
            // They are a texture, not an event, and mix accordingly.
            r.Volume = 0.32f;
            return r;
        }

        private static SoundRecipe BeamRecipe(DamageType type)
        {
            SchoolVoice voice = VoiceFor(type);

            SoundRecipe r = SoundRecipe.Default;
            r.Duration = 0.40f;

            // A long fall from high to low is what reads as "pew" rather than "beep".
            r.BodyHz = 720f * voice.BodyScale;
            r.BodyEndHz = 240f * voice.BodyScale;

            // Mostly tonal. A beam is a sustained emission, not an explosion.
            r.NoiseMix = Mathf.Clamp01(voice.NoiseMix * 0.35f);
            r.Brightness = voice.Brightness;
            r.Decay = 1.1f;
            r.Crack = 0.25f;
            r.Drive = 1.5f;
            r.Volume = 0.55f;
            return r;
        }

        private static SoundRecipe NamedRecipe(string id)
        {
            switch (id)
            {
                // The hitmarker. Tonal and bright so it cuts through gunfire rather than
                // blending into it - this one has to be heard to do its job.
                case HitConfirmId:
                    return new SoundRecipe
                    {
                        Duration = 0.055f,
                        BodyHz = 1500f,
                        BodyEndHz = 1150f,
                        NoiseMix = 0.10f,
                        Brightness = 0.9f,
                        Decay = 3.2f,
                        Crack = 0.12f,
                        Drive = 1.1f,
                        Volume = 0.30f
                    };

                // Barrels winding up. The only rising sound in the game: BodyEndHz above
                // BodyHz sweeps upward, which is what reads as a motor getting going.
                case SpinUpId:
                    return new SoundRecipe
                    {
                        Duration = 0.6f,
                        BodyHz = 70f,
                        BodyEndHz = 260f,
                        NoiseMix = 0.45f,
                        Brightness = 0.35f,

                        // Below 1 the envelope holds rather than snapping away. It still
                        // decays - the synth has no attack - but slowly enough that the
                        // rising pitch is what the ear follows.
                        Decay = 0.45f,
                        Crack = 0.1f,
                        Drive = 1.3f,
                        Volume = 0.4f
                    };

                // Mechanical, no pitched layer at all: the sound of nothing happening.
                case DryFireId:
                    return new SoundRecipe
                    {
                        Duration = 0.05f,
                        BodyHz = 90f,
                        BodyEndHz = 60f,
                        NoiseMix = 0.95f,
                        Brightness = 0.75f,
                        Decay = 6f,
                        Crack = 0.9f,
                        Drive = 1.2f,
                        Volume = 0.35f
                    };

                default:
                    return SoundRecipe.Default;
            }
        }
    }
}
