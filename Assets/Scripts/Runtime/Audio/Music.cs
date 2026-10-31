using System.Collections;
using UnityEngine;

namespace Descender.Audio
{
    /// <summary>
    /// The music: one loop at a time, crossfaded from the last, named by the clip
    /// (`title`, `boss`, or a region key such as `paradise`). Lives in Main.unity
    /// and keeps playing while the game is paused, ducked. Port of the music half
    /// of scripts/audio.gd; the effects half is <c>Sfx</c>. Volume comes from
    /// <see cref="Save.SettingsData"/> (master x music) through <see cref="ApplyVolume"/>.
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        public static Music Instance { get; private set; }

        /// <summary>The music's own level under the master, -8 dB in the Godot game.</summary>
        public const float Level = 0.4f;

        /// <summary>How far the music drops while the game is paused, -12 dB.</summary>
        public const float DuckFactor = 0.25f;
        public const float CrossfadeTime = 1.5f;
        public const float DuckTime = 0.3f;

        [SerializeField]
        private AudioClip[] tracks;

        private readonly AudioSource[] players = new AudioSource[2];
        private int live;
        private Coroutine fade;
        private float duck = 1f;
        private float duckVelocity;
        private bool ducked;

        /// <summary>The loop playing or fading in; empty when none.</summary>
        public string Playing { get; private set; } = "";

        /// <summary>Set while the game is paused: the music keeps going, quieter.</summary>
        public bool Ducked
        {
            get => ducked;
            set => ducked = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            for (var i = 0; i < players.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.ignoreListenerPause = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                players[i] = source;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            var wanted = ducked ? DuckFactor : 1f;
            if (!Mathf.Approximately(duck, wanted))
            {
                duck = Mathf.SmoothDamp(
                    duck,
                    wanted,
                    ref duckVelocity,
                    DuckTime,
                    float.PositiveInfinity,
                    Time.unscaledDeltaTime
                );
                if (fade == null)
                {
                    players[live].volume = Volume();
                }
            }
        }

        /// <summary>The loop by name, crossfaded from the current one; the same name again does nothing.</summary>
        public void Play(string name)
        {
            if (name == Playing)
            {
                return;
            }
            var clip = Find(name);
            if (clip == null)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    Debug.LogWarning($"Music: no track named '{name}'.");
                }
                Stop();
                return;
            }
            Playing = name;
            var next = 1 - live;
            players[next].clip = clip;
            players[next].volume = 0f;
            players[next].Play();
            StartFade(next);
        }

        public void Stop()
        {
            Playing = "";
            StartFade(-1);
        }

        /// <summary>Takes the settings' volumes; call after any volume change.</summary>
        public void ApplyVolume()
        {
            if (fade == null)
            {
                players[live].volume = Playing.Length > 0 ? Volume() : 0f;
            }
        }

        private float Volume()
        {
            var settings = Save.SettingsData.Current;
            return settings.music * Level * duck;
        }

        private AudioClip Find(string name)
        {
            if (tracks == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            foreach (var track in tracks)
            {
                if (track != null && track.name == name)
                {
                    return track;
                }
            }
            return null;
        }

        private void StartFade(int next)
        {
            if (fade != null)
            {
                StopCoroutine(fade);
            }
            fade = StartCoroutine(Crossfade(live, next));
            if (next >= 0)
            {
                live = next;
            }
        }

        private IEnumerator Crossfade(int from, int to)
        {
            var fromStart = players[from].volume;
            var elapsed = 0f;
            while (elapsed < CrossfadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / CrossfadeTime);
                players[from].volume = Mathf.Lerp(fromStart, 0f, t);
                if (to >= 0)
                {
                    players[to].volume = Mathf.Lerp(0f, Volume(), t);
                }
                yield return null;
            }
            players[from].Stop();
            players[from].volume = 0f;
            if (to >= 0)
            {
                players[to].volume = Volume();
            }
            fade = null;
        }
    }
}
