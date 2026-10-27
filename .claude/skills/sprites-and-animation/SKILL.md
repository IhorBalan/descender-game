---
name: sprites-and-animation
description: Character art and animation: the sprites.json strip spec, SpriteFrames resources cut by tools/sprite_frames.py, AnimatedSprite2D with feet origins, when to use AnimationPlayer or tweens instead, and hit feedback (flash, hit-stop, shake, sparks). Use when adding or changing a character's frames, playing animations, tweening a property or adding game feel.
---

# Sprites and animation

## Frames pipeline

1. Strips live at `assets/sprites/<name>/<name>_<anim>.png`, listed in
   `assets/sprites/sprites.json`:
   ```json
   "hero": {"frame_size": [64, 48], "origin": [32, 44], "anchor": "feet",
            "animations": {"run": {"file": "hero_run.png", "frames": 8, "fps": 12, "loop": true}}}
   ```
2. `python3 tools/sprite_frames.py` cuts every strip into
   `assets/sprites/<name>/<name>.tres` (a `SpriteFrames`) and stores the
   origin as the resource's `origin` metadata.
3. `AnimLoader` (static): `frames(character)` loads the resource (missing
   one reports and returns empty frames), `origin`, `offset` turns the
   origin into the `AnimatedSprite2D.offset` that puts the feet on the
   node's position, `setup(sprite, character)` does all of it.
4. The unit test (`test_sprites`, `test_enemies`) checks every strip is
   listed, cut to its frame count, and that every animation a script plays
   exists. Add the entry to `sprites.json` **first**, then run the tool.

Enemy scenes set `sprite_frames` and `offset` in the `.tscn` (inheriting
`walker.tscn`); the hero, NPCs and bosses, whose look is picked in code,
call `AnimLoader.setup`.

## AnimatedSprite2D in code

- `sprite.play("run")` is idempotent for the same animation; set
  `sprite.flip_h` from facing. Switch animations when the **state**
  changes, in one place (`_enter_state`), not from scattered calls.
- Non-looping animations: connect `animation_finished` once in `_ready`
  and branch on `sprite.animation`; or read `sprite.frame` /
  `frame_changed` for frame-exact events. The hero's attack instead times
  its active window with constants (`ATTACK_ACTIVE_FROM / UNTIL`) against
  the strip's fps, which is documented next to them; keep those two in step
  when re-cutting a strip.
- `speed_scale` for slow motion on one sprite; `Engine.time_scale` for
  everyone (that is what hit-stop uses).
- Texture filter is nearest project-wide; do not set per-sprite filters.
- Sprites that bob in `_process` or via tween set
  `physics_interpolation_mode = PHYSICS_INTERPOLATION_MODE_OFF`.

## When to use AnimationPlayer or AnimationTree

The repo animates characters with `AnimatedSprite2D` and state machines in
code; nothing uses `AnimationPlayer` yet. Reach for one when a scene needs
several properties keyed together on a timeline: a cutscene, a boss intro
that moves the camera and plays sounds, a UI reveal, a door opening. From
the docs:

- Add an `AnimationPlayer` as a child of the scene root; its tracks
  address nodes by path relative to it, so keep it next to what it moves.
- Track types: property tracks (position, modulate, `frame`), **method
  call tracks** for sounds and signals (`Audio.play` at a keyframe),
  audio tracks (play an `AudioStream` on an `AudioStreamPlayer` node),
  animation playback tracks to drive a child `AnimationPlayer`, and
  Bezier tracks for eased curves.
- `animation.loop_mode`, `play("name")`, `play_backwards`, `queue`,
  `animation_finished` signal, `seek(0, true)` to reset, `speed_scale`.
  Set `callback_mode_process = ANIMATION_CALLBACK_MODE_PROCESS_PHYSICS`
  when the animation moves bodies.
- `AnimationTree` with an `AnimationNodeStateMachine` is the editor-driven
  alternative to a `match state:` machine, with blend times between
  states; use it only for a character whose animation graph outgrows code.
- Store the animations in an `AnimationLibrary` on the player; copying a
  player between scenes shares the library resource, so `duplicate()` it
  when one scene must diverge.
- Cut-out (skeletal) animation: `Skeleton2D` + `Bone2D` + `Polygon2D`
  with an `AnimationPlayer` on the bones; the hero art is strips, so this
  only applies to new large characters.

## Tweens

`create_tween()` on the node that owns the effect (the tween dies with the
node). Chain `tween_property(obj, "modulate:a", 0.0, 0.3)`,
`tween_interval`, `tween_callback(queue_free)`; `set_parallel(true)` to run
tracks together; `set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)`
for curves; `set_process_mode(Tween.TWEEN_PROCESS_PHYSICS)` when moving
bodies. Keep the `Tween` in a member (`_fade`) and `kill()` it before
starting a replacement, as `Audio` and `ScreenFade` do. A tween-based
function returns its `finished` signal (`ScreenFade.fade_out() -> Signal`)
so callers can `await` it. Shader uniforms tween through
`"shader_parameter/flash"`. For a pause-proof tween set
`set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)`.

Use a tween for one-off property motion (fades, pickups bobbing, bars
filling), `AnimationPlayer` for authored timelines, and the state machine
for gameplay frames.

## Game feel (`Feedback` autoload)

`Feedback.hit_stop(seconds)` (scales `Engine.time_scale` to
`HIT_STOP_SCALE`, real-time stamped so it works while paused),
`Feedback.shake(amount)` (camera trauma plus gamepad rumble, both scaled by
`Settings`), `Feedback.flash(sprite, duration)` (gives the sprite a
`ShaderMaterial` with `hit_flash.gdshader` and tweens `flash` 1 to 0),
`Feedback.spark(position, color)`, `puff`, `dust`. A hit is usually all
three lines: flash the victim, spark at the contact point, a small shake;
a kill adds hit-stop and a puff. The smoke test counts sparks and dust
(`spark_count`, `dusts_raised`), so new effects should go through these
helpers to be testable.

`HitSpark` / `DeathPuff` are `CPUParticles2D` scenes with `one_shot`,
set `emitting = true` in `_ready` and free themselves after
`lifetime + 0.05`; copy that for a new burst.
