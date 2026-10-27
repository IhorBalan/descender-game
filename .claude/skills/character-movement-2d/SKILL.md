---
name: character-movement-2d
description: Moving bodies in 2D here: CharacterBody2D state machines, jump tuning, physics layers and masks, Area2D hitboxes and hazards, RayCast2D probes, and physics interpolation. Use when touching the player, an enemy, a projectile, a pickup or anything with collision.
---

# 2D movement and collision

## The physics layers (`project.godot`)

| Bit | Name | Who is on it |
|---|---|---|
| 1 | `world` | Terrain collision built by `LevelBuilder`, secret walls |
| 2 | `player` | `Player` body (`collision_layer = 2`) |
| 4 | `enemy` | Enemy bodies (`collision_layer = 4`) |
| 8 | `light_barrier` | Barriers the light dash passes through |
| 16 | `ledge` | One-way platforms (`Player.LEDGE_LAYER_BIT`) |

Bodies collide with `world + light_barrier + ledge` (`collision_mask = 25`).
Hitboxes are `Area2D`s with `collision_layer = 0` and a mask of the one
layer they look for: the enemy's `Hitbox` masks the player (2), the
player's attack masks enemies (4). `Interactable` and `Hazard` are areas
with `monitorable = false` when nothing needs to see them. Always set
layer *and* mask explicitly in the `.tscn`; a default of 1/1 collides with
the world and is the usual cause of "my hitbox hits terrain".

Every scene's origin is at the **feet**, so placements, sprites
(`AnimLoader.offset`) and rays agree.

## CharacterBody2D the way the player does it

- All movement runs in `_physics_process(delta)` and ends in
  `move_and_slide()`. Input is read there too (`Input.is_action_just_pressed`)
  with a buffer (`INPUT_BUFFER_TIME`) so a press a few ticks early still
  counts. Never move a body in `_process`.
- `velocity` is the state; gravity is added each tick and capped at
  `MAX_FALL_SPEED`. `up_direction` is up by default; `floor_snap_length = 4`
  keeps him glued over small lips; `is_on_floor()` is only valid *after*
  `move_and_slide()`.
- A `match state:` machine (`State.IDLE, RUN, JUMP, FALL, DASH, ATTACK, ...`)
  with one `_process_<state>` per state. State changes set the animation and
  the sounds in one place.
- Jump tuning derives from feel constants, not raw numbers:
  `JUMP_VELOCITY := -2.0 * JUMP_HEIGHT / JUMP_TIME_TO_APEX`,
  `GRAVITY := 2.0 * JUMP_HEIGHT / (JUMP_TIME_TO_APEX ** 2)`, a heavier
  `FALL_GRAVITY`, a jump cut multiplier on release, lighter gravity near the
  apex, coyote time, and a landing with `_was_on_floor` tracking. Reuse these
  constants for anything that jumps.
- Acceleration is separate for ground, air, turning and friction
  (`GROUND_ACCEL`, `GROUND_TURN`, `AIR_ACCEL`...). Use `move_toward` on
  `velocity.x`.
- Corner correction nudges the body sideways by up to `CORNER_CORRECTION`
  pixels with `test_move` before accepting a ceiling or lip collision.
- Dropping through a ledge: temporarily clear the ledge bit from
  `collision_mask` for `DROP_TIME`, then restore it.
- Walls: `is_on_wall()` and `get_wall_normal()` after the move, with a
  short `WALL_JUMP_LOCK` on horizontal input after a wall jump.

Enemies (`Walker`, `Flyer`) are also `CharacterBody2D` with their own
`GRAVITY`, a `FloorRay` `RayCast2D` ahead of the feet (mask `world + ledge`)
to turn at edges, and `WINDUP / STRIKE / RECOVER` states driven by timers.
Their numbers come from a `WalkerProfile` resource, not constants, so a new
body is a `.tres`, not a script.

## Area2D rules

- Connect `body_entered`/`body_exited` in `_ready`; use `is` to filter
  (`if body is Player`).
- A hazard that must keep hurting while the hero stands in it polls
  `get_overlapping_bodies()` in `_physics_process`, but only while something
  is inside (`set_physics_process(false)` until `body_entered`) so hundreds of
  spikes cost nothing (`scripts/hazard.gd`).
- An attack hitbox is a child `Area2D` whose `CollisionShape2D` is
  `disabled` until the active frames (`ATTACK_ACTIVE_FROM / UNTIL`), then
  disabled again. Toggle `shape.disabled` or `set_deferred("disabled", ...)`
  from within a physics callback.
- Changing shapes or monitoring from inside a signal callback must be
  deferred (`set_deferred`, `call_deferred`), since the physics server is
  flushing.
- Area detection needs a tick: a shape enabled this frame reports overlaps
  next frame. Tests account for that.

## Shapes and one-way platforms

- `RectangleShape2D` for bodies, `CapsuleShape2D` only where a round bottom
  helps; keep collision simpler than the sprite.
- Ledges are `StaticBody2D`s on layer 16 with `one_way_collision = true`
  on the shape, built in `LevelBuilder` from the `kind == "ledge"` tiles.
- Solid runs of tiles are merged into one shape per row by the builder
  instead of per-tile collision, so `TileSet` physics layers are not used
  for the world. Keep it that way: new terrain kinds go through the builder.

## Physics interpolation (on project-wide)

`physics/common/physics_interpolation = true`, so rendering interpolates
between physics ticks and the camera moves smoothly.

- Anything **teleported** (respawn, fast travel, warp) calls
  `reset_physics_interpolation()` right after setting its position
  (`main.gd` does this for the player), or it slides across the screen.
- Anything moved in `_process` or by a tween (floating pickups, the
  `ParallaxSprite`, `HealMote`) sets
  `physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF` on the
  moved node, or it lags a tick behind.
- Read positions for rendering (sprites following a body) in `_process`,
  and for logic in `_physics_process`. Never set `global_position` from
  `_process` on an interpolated node.
- Keep the physics tick at 60; use delta, not frame counts, in logic. The
  smoke test counts ticks because it runs headless at a fixed rate.

## Spawning and projectiles

- Spawn with a preloaded `PackedScene`, set `global_position` and
  properties, then `add_child`. `EnemyShot` is an `Area2D` moved by its own
  velocity in `_physics_process` and freed on hit or after a lifetime.
- Give every spawned thing a way to die: a lifetime timer, leaving the
  loaded chunks (`LevelBuilder.is_loaded(cell)`), or the owner freeing it.

## Debugging collision

Run with **Debug → Visible Collision Shapes**, or in headless tests check
`is_on_floor()` after a few ticks. `Player.LEDGE_LAYER_BIT` and the layer
names above are the first things to compare when a body falls through or
sticks.
