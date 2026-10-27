---
name: audio
description: Sound in this project: the Audio autoload (SFX player pool, music crossfade, ambient loop, ducking), the Master/Music/SFX buses, looping WAV and Ogg streams, generated placeholder sounds, and how tests observe sounds. Use when playing a sound, adding music or ambience, or changing volume handling.
---

# Audio

## The Audio autoload (`scripts/audio.gd`)

```gdscript
Audio.play("jump")                 # an effect by file name, from assets/audio/sfx/
Audio.play("land", -6.0)           # volume_db, then pitch (default 1.0)
Audio.play_music("wrath")          # crossfades to assets/audio/music/<key>.ogg|wav
Audio.stop_music()
Audio.play_ambient("cave")         # one long loop under the music
Audio.wire_menu(root)              # hover/press sounds on every button under root
```

- Effects play from a pool of `POOL_SIZE = 8` `AudioStreamPlayer`s on the
  `SFX` bus, round-robin, with a random pitch spread of `PITCH_SPREAD`
  so repeats do not sound stamped. A sound that must not be cut off by the
  ninth effect gets its own `AudioStreamPlayer` in its scene instead.
- Music: two players on the `Music` bus so one loop fades out under the
  next (`CROSSFADE_TIME = 1.5`), tracked in `_fade`; the same key again is
  a no-op (`music_name`). `MUSIC_DB`, `AMBIENT_DB`, `SILENT_DB` are the
  levels. Pausing the tree ducks the music by `DUCK_DB` over `DUCK_TIME`
  (the autoload is `PROCESS_MODE_ALWAYS` and watches `get_tree().paused`).
- Streams are cached in `_sounds` and set to loop in code (`_loop`):
  `AudioStreamWAV.loop_mode = LOOP_FORWARD` with `loop_end` at the sample
  count, `AudioStreamOggVorbis.loop = true`. Do not rely on import
  settings for looping.
- `last_played` and `frame_played` record what played, so the smoke test
  can `_check(Audio.last_played == "jump", ...)`. Keep updating them in
  any new play path.

## Buses (`default_bus_layout.tres`)

`Master` > `Music`, `SFX`. `Settings` stores linear volumes and applies
them with `AudioServer.set_bus_volume_db(idx, linear_to_db(v))` through
`_set_bus_volume(bus_name, linear)`; mute at 0 rather than `-80 dB`
(`SILENT_DB` is -40 for fades). A new category of sound (voice, UI) gets a
new bus in the layout, a slider in `SettingsMenu`, and a key in
`settings.cfg`, in that order. Bus effects (reverb for a cave, a low-pass
when paused) belong on the bus, added in the editor's Audio panel, and
toggled with `AudioServer.set_bus_effect_enabled`.

## Positional sound

The game is small enough that effects are non-positional
(`AudioStreamPlayer`). If something needs panning or distance falloff use
`AudioStreamPlayer2D` with `max_distance` and `attenuation`, as a child of
the emitter, on the `SFX` bus; the listener is the camera (`Camera2D`
acts as the listener in 2D). Keep it rare: a screen 640 pixels wide does
not need stereo spread for most hits.

## Files and generators

Every sound is synthesised: `tools/gen_sounds.py` writes
`assets/audio/sfx/<name>.wav` from a recipe per name, `tools/gen_music.py`
writes one loop per region key (`paradise` ... `treachery`, plus `title`
and `boss`) as Ogg (ffmpeg) or WAV. Effects are played by name, so a real
recording only has to keep the file name. The unit test (`test_sounds`)
scans the scripts for `Audio.play("...")` and fails on a name with no
file, so add the recipe (or file) before the call.

Import settings: WAV effects are imported as-is (short, uncompressed);
music is Ogg Vorbis. Leave `force/8_bit`, `force/mono` and compression off
for effects; set loop points in code as above.

## Timing

- Sync to physics or timers, not to audio playback position, except for
  rhythm features; `get_playback_position() + AudioServer.get_time_since_last_mix()`
  minus output latency is the accurate clock when you need it.
- Start sounds from the state change that causes them (one `Audio.play`
  per state entry), not from animation frames, so headless tests hear
  them at the same tick.
- A sound for a thing that is freed the same frame should go through
  `Audio.play` (the pool outlives the emitter), not a player on the node.
