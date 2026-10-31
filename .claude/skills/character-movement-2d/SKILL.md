---
name: character-movement-2d
description: Player and enemy movement in Unity 2D for this game: Rigidbody2D (dynamic for the hero, kinematic for walkers), FixedUpdate vs Update, input buffering, coyote time, layers and masks, hitboxes as trigger colliders, hit-stop and screen shake. Use when touching PlayerController, enemies, projectiles, hazards, ledges or collision layers.
---

# 2D character movement

Port target for `scripts/player.gd`, `scripts/enemies/*.gd`,
`scripts/world/ledge.gd` and friends. Numbers come from `PlayerTuning`
(ScriptableObject in `Assets/Data/PlayerTuning.asset`) in level pixels,
divided by `PlayerTuning.PixelsPerUnit` (16) at the physics boundary.

## Bodies

- **Hero**: `Rigidbody2D` Dynamic, `gravityScale 0` (gravity is applied by
  the controller so rise, fall and wall-slide gravity can differ), `Freeze
  Rotation Z`, `Interpolate`, `Collision Detection: Continuous`. One
  `BoxCollider2D` for the body (feet slightly inset so a 1 px lip does not
  catch), and a child `Hitbox` with a trigger collider on the `Trigger`
  layer for the sword.
- **Walkers**: `Rigidbody2D` Kinematic with `useFullKinematicContacts`
  on, moved with `MovePosition` in `FixedUpdate`; a `BoxCollider2D` on
  `Enemy`, a child trigger `Hurtbox` for the hero's sword.
- **Flyers and projectiles**: Kinematic, no gravity, moved by setting
  `linearVelocity` or `MovePosition`; projectiles are trigger colliders
  on `Hazard`.
- **Static world**: the Terrain tilemap has `TilemapCollider2D` +
  `CompositeCollider2D` (its Rigidbody2D Static) on the `World` layer.
  Ledges (one-way) use `PlatformEffector2D` on their own `Ledge` layer so
  the hero can drop through by ignoring that layer while `MoveDown` is
  held.

Movement code reads input in `Update` into a small buffer (`jumpPressedAt`,
`attackQueued`) and consumes it in `FixedUpdate`, which runs at the fixed
60 Hz set by `ProjectBootstrap`. Never read `WasPressedThisFrame` in
`FixedUpdate`: at 144 Hz display the press lands in a frame with no fixed
step and is lost.

## Ground and walls

```csharp
private static readonly Collider2D[] Hits = new Collider2D[4];
private ContactFilter2D groundFilter;   // set in Awake: layerMask World | Ledge, useTriggers false

private bool CheckGround()
{
    var box = feet.bounds;                      // a thin box under the collider, 1 px tall
    return Physics2D.OverlapBox(box.center, box.size, 0f, groundFilter, Hits) > 0;
}
```

- Coyote time (`tuning.CoyoteTime`), jump buffer and variable jump height
  (cut `velocity.y` to `tuning.JumpCutVelocity` on release) are timers in
  the controller, counted down in `FixedUpdate` by `Time.fixedDeltaTime`.
- Wall slide and wall jump check a side box the same way, masked to
  `World` only.
- Dash sets velocity directly for `DashTime` and ignores gravity; it
  switches the hero's layer to `PlayerDashing` (no collision with `Enemy`)
  for its duration via `gameObject.layer`, and back after.

## Layers and masks

Set once by `ProjectBootstrap` (`Edit > Project Settings > Physics 2D >
Layer Collision Matrix` after it runs):

| Layer | Collides with |
|---|---|
| World | Player, Enemy, Pickup |
| Ledge | Player (one-way), Enemy |
| Player | World, Ledge, Enemy, Hazard, Pickup, Trigger |
| Enemy | World, Ledge, Player |
| Hazard | Player |
| Pickup | World, Player |
| Trigger | Player |
| LightBarrier | Player (only while the barrier is up) |

Read masks through `LayerMask.GetMask("World", "Ledge")` cached in a
static readonly field, never a magic int. `Physics2D.queriesHitTriggers`
is off; hit detection that wants triggers passes a `ContactFilter2D` with
`useTriggers = true`.

## Hits, damage, invulnerability

- Hitbox and hurtbox are trigger colliders; `OnTriggerEnter2D` on the
  hurtbox side looks for an `IHittable`/`Damager` component with
  `TryGetComponent` and calls `Hit(damage, from)`. No tags.
- The sword hitbox is enabled for the attack's active frames by the
  attack state, not by animation events, so the timing is the same at any
  frame rate.
- Invulnerability after a hit is a timer; while it runs the hurtbox
  collider is disabled and the sprite flashes (`sprites-and-animation`).
- Knockback sets velocity once and lets the controller resume after
  `tuning.HitStunTime`.

## Feel

- Hit-stop: `Time.timeScale = 0.05f` for `tuning.HitStopTime` real seconds
  via a coroutine using `WaitForSecondsRealtime`, respecting the
  `hitStop` setting. Pausing sets `Time.timeScale = 0` and the hit-stop
  coroutine must not undo it (check `Pause.IsPaused` before restoring).
- Screen shake lives on `GameCamera` (see `urp-2d-lighting-and-camera`),
  scaled by the `shake` setting, in whole pixels so the pixel-perfect
  camera does not shimmer.
- Rumble through `Gamepad.current?.SetMotorSpeeds` with a coroutine that
  resets to zero; respect the `rumble` setting and `Gamepad.current`
  being null.

## Death, respawn, checkpoints

The hero's `Die()` disables input and the colliders, plays the death
animation, then `GameFlow.Respawn()` restores the last checkpoint:
position, hearts, flasks, and re-enables. Enemies killed since the
checkpoint stay dead within the region (per-region `KilledIds`) and
reset on region reload; bosses set `BossIds.FlagFor(id)` permanently.

## Things that went wrong in Godot and must not come back

- Moving a body from `Update` (jitter at any non-60 Hz display).
- Stuck on tile seams: keep the composite collider, inset the feet.
- Double damage from overlapping hurtboxes: one hurtbox per enemy, the
  hero's sword hitbox tracks a `HashSet<Collider2D>` of what it hit this
  swing, cleared when the swing starts.
