using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Parameters for one synthesised sound. A gunshot is a pitched thump that falls in
    /// frequency, a band of noise for the crack, and an envelope that decides whether it reads
    /// as a snap or a boom.
    ///
    /// These are deliberately perceptual rather than physical: <see cref="SoundLibrary"/>
    /// derives them from weapon stats, so they need to interpolate sensibly between a pistol
    /// and a rocket launcher.
    /// </summary>
    public struct SoundRecipe
    {
        /// <summary>Total length in seconds. Also the decay time - nothing sustains.</summary>
        public float Duration;

        /// <summary>Starting fundamental of the pitched layer. Low reads heavy.</summary>
        public float BodyHz;

        /// <summary>Where the fundamental falls to by the end. The drop is most of the "punch".</summary>
        public float BodyEndHz;

        /// <summary>0 is a pure tone, 1 is pure noise. Real gunfire sits high; magic sits lower.</summary>
        public float NoiseMix;

        /// <summary>Lowpass on the noise layer. 0 is muffled, 1 is unfiltered hiss.</summary>
        public float Brightness;

        /// <summary>Envelope curve. 1 is a linear fade, 4 is a sharp snap.</summary>
        public float Decay;

        /// <summary>Extra transient in the first few milliseconds - the click of the action.</summary>
        public float Crack;

        /// <summary>Soft-clip drive. Above 1 it starts to sound loud rather than just be loud.</summary>
        public float Drive;

        /// <summary>Peak amplitude before drive, 0..1.</summary>
        public float Volume;

        public static SoundRecipe Default => new SoundRecipe
        {
            Duration = 0.18f,
            BodyHz = 140f,
            BodyEndHz = 50f,
            NoiseMix = 0.75f,
            Brightness = 0.55f,
            Decay = 3f,
            Crack = 0.5f,
            Drive = 1.6f,
            Volume = 0.8f
        };
    }

    /// <summary>
    /// Renders a <see cref="SoundRecipe"/> into an AudioClip. Everything else in this project
    /// builds its meshes and materials at runtime rather than shipping files; this is the same
    /// idea applied to audio, so the game makes noise without a single .wav in the repo.
    ///
    /// Output is deterministic for a given seed: the same gun sounds the same every launch,
    /// with per-shot variation coming from pitching the source at playback instead.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;

        /// <summary>Longest clip we will render. A stuck parameter should waste a moment, not a gigabyte.</summary>
        private const float MaxDuration = 2f;

        public static AudioClip Render(string name, SoundRecipe r, int seed)
        {
            float duration = Mathf.Clamp(r.Duration, 0.01f, MaxDuration);
            int sampleCount = Mathf.Max(16, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[sampleCount];

            uint rng = (uint)seed;
            if (rng == 0u) rng = 0x9E3779B9u;   // xorshift is a fixed point at zero

            float bodyPhase = 0f;
            float lowpass = 0f;
            float brightness = Mathf.Clamp01(r.Brightness);

            // One-pole coefficient. Squared so the low end of the dial has usable resolution -
            // most of the interesting difference between "muffled" and "open" lives down there.
            float lpCoeff = Mathf.Lerp(0.02f, 1f, brightness * brightness);

            float decay = Mathf.Max(0.2f, r.Decay);
            float drive = Mathf.Max(0.01f, r.Drive);
            float volume = Mathf.Clamp01(r.Volume);

            for (int i = 0; i < sampleCount; i++)
            {
                float n = (float)i / sampleCount;          // 0..1 through the sound
                float env = Mathf.Pow(1f - n, decay);      // always reaches zero, so no end click

                float bodyHz = Mathf.Lerp(r.BodyHz, r.BodyEndHz, n);
                bodyPhase += 2f * Mathf.PI * bodyHz / SampleRate;
                if (bodyPhase > 2f * Mathf.PI) bodyPhase -= 2f * Mathf.PI;
                float body = Mathf.Sin(bodyPhase);

                float white = NextFloat(ref rng);
                lowpass += lpCoeff * (white - lowpass);

                float sample = Mathf.Lerp(body, lowpass, Mathf.Clamp01(r.NoiseMix));

                // The transient is what the ear reads as "impact". It has to be much shorter
                // than the body or it just sounds like the clip starts too loud.
                if (r.Crack > 0f)
                    sample += r.Crack * white * Mathf.Exp(-n * 220f);

                data[i] = SoftClip(sample * env * drive) * volume;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// tanh-ish saturation. Rounds the peaks off instead of squaring them, which is the
        /// difference between a shot sounding powerful and sounding like a broken speaker.
        /// </summary>
        private static float SoftClip(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            return x * (27f + x * x) / (27f + 9f * x * x);
        }

        /// <summary>xorshift32 in -1..1. Deterministic, and far cheaper than UnityEngine.Random per sample.</summary>
        private static float NextFloat(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state / (float)uint.MaxValue) * 2f - 1f;
        }

        /// <summary>Stable hash for seeding. string.GetHashCode is randomised per process in .NET Core.</summary>
        public static int SeedFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;

            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < id.Length; i++) hash = (hash ^ id[i]) * 16777619;
                return hash;
            }
        }
    }
}
