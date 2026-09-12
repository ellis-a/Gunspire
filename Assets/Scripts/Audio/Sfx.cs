using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Plays one-shot sounds through a fixed pool of AudioSources.
    ///
    /// A pool rather than PlayClipAtPoint because that allocates and destroys a GameObject per
    /// call, and an automatic weapon at 900rpm asks for fifteen a second before anything else
    /// in the room has made a noise.
    /// </summary>
    public static class Sfx
    {
        /// <summary>Global scale on everything played here. Hook an options slider to this.</summary>
        public static float MasterVolume = 1f;

        /// <summary>
        /// Scales everything the player hears. Shock drives this down, which is the other half of
        /// what that status does - being deafened matters in a game where enemies are located by
        /// the noise they make.
        /// </summary>
        public static float Muffle = 1f;

        /// <summary>Concurrent positional sounds. Past this the oldest voice is stolen.</summary>
        private const int VoiceCount = 16;

        /// <summary>Full volume within this radius, silent beyond MaxDistance.</summary>
        private const float MinDistance = 4f;
        private const float MaxDistance = 60f;

        /// <summary>
        /// Unspatialised voices. More than one because pitch is a property of the source, not
        /// of the one-shot: re-pitching a single source while an earlier shot is still ringing
        /// bends that shot too, which an automatic weapon turns into an audible warble.
        /// </summary>
        private const int FlatVoiceCount = 4;

        private static GameObject _root;
        private static AudioSource[] _voices;
        private static AudioSource[] _flatVoices;
        private static int _next;
        private static int _nextFlat;

        /// <summary>How many copies of one clip may start in a single frame, and how they fade.</summary>
        private struct Burst
        {
            public int Frame;
            public int Count;
        }

        // Keyed on the clip itself. Object.GetInstanceID is deprecated in Unity 6, and the
        // reference is a perfectly good key since SoundLibrary caches one clip per id.
        private static readonly Dictionary<AudioClip, Burst> Bursts = new Dictionary<AudioClip, Burst>();

        // ---------------------------------------------------------------- api

        /// <summary>
        /// Plays without spatialisation, for sounds belonging to the player themselves. The
        /// player's own gun must not pan as they turn - it is attached to their hands.
        /// </summary>
        public static void PlayFlat(AudioClip clip, float volume = 1f, float pitchVariance = 0.06f)
        {
            if (clip == null || !EnsurePool()) return;

            AudioSource voice = _flatVoices[_nextFlat];
            _nextFlat = (_nextFlat + 1) % _flatVoices.Length;

            voice.pitch = Pitch(pitchVariance);
            voice.PlayOneShot(clip, Mathf.Clamp01(volume) * MasterVolume * Muffle);
        }

        /// <summary>
        /// Plays at a world position. <paramref name="maxPerFrame"/> caps how many copies of
        /// the same clip may start together: one explosion hitting eight enemies would
        /// otherwise fire eight identical impacts on the same frame, which sums to a click
        /// rather than to eight impacts.
        /// </summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f,
            float pitchVariance = 0.08f, int maxPerFrame = 3)
        {
            if (clip == null || !EnsurePool()) return;

            float crowdFade = Crowding(clip, maxPerFrame);
            if (crowdFade <= 0f) return;

            AudioSource voice = NextVoice();
            voice.transform.position = position;
            voice.pitch = Pitch(pitchVariance);
            voice.PlayOneShot(clip, Mathf.Clamp01(volume) * crowdFade * MasterVolume * Muffle);
        }

        /// <summary>Drops the pool. The next play rebuilds it.</summary>
        public static void Reset()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _voices = null;
            _flatVoices = null;
            _next = 0;
            _nextFlat = 0;
            Bursts.Clear();
        }

        // ---------------------------------------------------------------- internals

        private static float Pitch(float variance)
        {
            // Identical playback of an identical clip is what makes repeated fire sound
            // synthetic. A few percent of wobble is enough to break that up.
            return variance <= 0f ? 1f : 1f + Random.Range(-variance, variance);
        }

        /// <summary>
        /// Returns the volume scale for this copy of the clip, or 0 to drop it. Stacked copies
        /// duck so a crowd reads as louder without also reading as distorted.
        /// </summary>
        private static float Crowding(AudioClip clip, int maxPerFrame)
        {
            if (maxPerFrame <= 0) return 1f;

            int frame = Time.frameCount;

            Bursts.TryGetValue(clip, out Burst burst);
            if (burst.Frame != frame)
            {
                burst.Frame = frame;
                burst.Count = 0;
            }

            if (burst.Count >= maxPerFrame) return 0f;

            burst.Count++;
            Bursts[clip] = burst;

            return 1f / burst.Count;
        }

        private static AudioSource NextVoice()
        {
            // Prefer a voice that has finished. Only steal once they are all busy.
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource candidate = _voices[(_next + i) % _voices.Length];
                if (!candidate.isPlaying)
                {
                    _next = (_next + i + 1) % _voices.Length;
                    return candidate;
                }
            }

            AudioSource stolen = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            return stolen;
        }

        private static bool EnsurePool()
        {
            // The null check is not just first-run: entering play mode or reloading a scene
            // can take the pool with it while these statics survive.
            if (_root != null && _voices != null && _flatVoices != null) return true;

            _root = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideInHierarchy;

            _flatVoices = new AudioSource[FlatVoiceCount];
            for (int i = 0; i < FlatVoiceCount; i++) _flatVoices[i] = CreateSource("Flat" + i, spatial: false);

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) _voices[i] = CreateSource("Voice" + i, spatial: true);

            _next = 0;
            _nextFlat = 0;
            Bursts.Clear();
            return true;
        }

        private static AudioSource CreateSource(string name, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;

            if (spatial)
            {
                // Linear rather than logarithmic: the arena is a known size, and logarithmic
                // rolloff makes anything past a few metres almost inaudible.
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = MinDistance;
                source.maxDistance = MaxDistance;
            }

            return source;
        }
    }
}
