---
name: audio
description: Audio in this project: the Audio Mixer with Master, Music, Sfx and Blips groups mapped to the volume settings, the Sfx pool of AudioSources, music crossfades, looping with loop points, pitch variation, 2D (non-spatial) playback, and import settings for wav and ogg. Use when adding or playing a sound, changing music, or touching the volume settings.
---

# Audio

## Mixer

`Assets/Settings/Descender.mixer` with groups Master > Music, Sfx, Blips.
Exposed parameters `MusicVolume`, `SfxVolume`, `BlipsVolume`,
`MasterVolume`; `SettingsData` volumes (0..1) map through
`20 * Mathf.Log10(Mathf.Max(v, 0.0001f))` dB on `mixer.SetFloat`. Apply
in `Settings.Apply()` on boot and after each change; a mixer `SetFloat`
cannot run from `Awake` of the first frame reliably, so `Audio` applies
in `Start`.

## Sources

- **Sfx**: `Sfx` in `Main.unity` owns a pool of 16 `AudioSource`s
  (`spatialBlend 0`, `playOnAwake off`, output group Sfx).
  `Sfx.Play(AudioClip, volume = 1f, pitchVariance = 0.05f)` takes the
  next free source (or the oldest), sets `pitch = 1 + Random.Range(-v,
  v)`, `PlayOneShot`. `Sfx.PlayBlip` uses the Blips group for UI and
  dialogue ticks.
- **Looping sfx** (torches, water): an `AudioSource` on the prop with
  `loop`, `Sfx` group, started in `OnEnable`, stopped in `OnDisable`;
  volume driven by distance to the hero in a slow coroutine (every 0.1 s),
  not by Unity's 3D rolloff.
- **Music**: two `AudioSource`s on `Music` (`ignoreListenerPause` on, Music
  group). `Music.Play(clip, fade = 1f)` crossfades: new clip on the idle
  source from 0, old one to 0, over `fade` seconds using unscaled time so
  it works while paused. `RegionLook.music` names the clip under
  `Assets/Audio/music/`; the same name does nothing.
- Loop points: Unity has no loop start/end. Tracks with an intro are two
  clips (`<name>_intro`, `<name>_loop`); `Music` schedules the loop with
  `PlayScheduled(AudioSettings.dspTime + intro.length)` on the second
  source for a seamless join.

Clips are referenced through `[SerializeField] AudioClip` on the thing
that plays them, or a `SfxLibrary` ScriptableObject for shared sounds
(hit, jump, pickup). No `Resources.Load` by string.

## Import settings

- Sfx (`.wav`): `Load Type: Decompress On Load`, `Compression: PCM`,
  `Preload Audio Data` on, `Force To Mono` on (they are 2D), sample rate
  preserved.
- Music (`.ogg`): `Load Type: Streaming`, `Compression: Vorbis`, quality
  70, stereo.
- Ambience loops: `Compressed In Memory`, Vorbis.

The import script writes these metas for the files it copies; a new file
dropped in by hand gets them from `Descender > Import > Apply audio
import`.

## Rules

- Nothing plays in `Awake`; the listener may not exist yet. `Start` or
  later.
- No `AudioSource` on prefabs that spawn many times (projectiles); they
  ask `Sfx`.
- `AudioListener` is on the camera only, once.
- Volume changes from the settings screen are audible immediately (mixer
  parameters), without replaying.
