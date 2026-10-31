using UnityEngine;
using UnityEngine.Audio;

namespace Descender.Audio
{
    /// <summary>
    /// Plays sound effects from a pool of 2D AudioSources (16, round robin) routed
    /// to the Sfx mixer group when one is set. One in Main.unity with the
    /// <see cref="SfxLibrary"/>; the hero, enemies and pickups call the static
    /// <see cref="Play(string, float, float)"/> with the Godot names and a decibel
    /// offset, and nothing happens when there is no Sfx in the scene (tests, early
    /// boot). The sfx volume setting is applied through <see cref="SetVolume"/>.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public const int PoolSize = 16;

        public static Sfx Instance { get; private set; }

        [SerializeField]
        private SfxLibrary library;

        [SerializeField]
        private AudioMixerGroup output;

        private AudioSource[] sources;
        private int next;
        private float volume = 1f;

        public SfxLibrary Library
        {
            get => library;
            set => library = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            sources = new AudioSource[PoolSize];
            for (var i = 0; i < PoolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = output;
                sources[i] = source;
            }
            volume = Save.SettingsData.Current.sfx;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>The sfx volume, 0 to 1, from the settings screen.</summary>
        public static void SetVolume(float value)
        {
            if (Instance != null)
            {
                Instance.volume = Mathf.Clamp01(value);
            }
        }

        /// <summary>Plays a named sound at a decibel offset (0 is the clip's own level, -6 is half).</summary>
        public static void Play(string name, float decibels = 0f, float pitchVariance = 0.05f)
        {
            if (Instance == null || Instance.library == null)
            {
                return;
            }
            var clip = Instance.library.Find(name);
            if (clip != null)
            {
                Play(clip, Mathf.Pow(10f, decibels / 20f), pitchVariance);
            }
        }

        /// <summary>Plays a clip on the next free source (or the oldest) with a little pitch variation.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitchVariance = 0.05f)
        {
            if (Instance == null || clip == null)
            {
                return;
            }
            var self = Instance;
            var source = self.sources[self.next];
            for (var i = 0; i < PoolSize; i++)
            {
                var candidate = self.sources[(self.next + i) % PoolSize];
                if (!candidate.isPlaying)
                {
                    source = candidate;
                    break;
                }
            }
            self.next = (self.next + 1) % PoolSize;
            source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            source.PlayOneShot(clip, Mathf.Clamp01(volume * self.volume));
        }
    }
}
