---
name: character-movement-2d
description: Player and enemy movement in Unity 2D for this game: Rigidbody2D (dynamic, gravityScale 0, velocity set in FixedUpdate), input buffering, coyote time, layers and masks, hits by OverlapBox queries rather than hitbox children, hit-stop and screen shake. Use when touching PlayerController, enemies, projectiles, hazards, ledges or collision layers.
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
  `BoxCollider2D` for the body (14x32 px, origin at the feet), friction 0
  (`PhysicsMaterial2D`). No hitbox children: the sword, the slam and the
  bolt find enemies with `Physics2D.OverlapBox` and a cached
  `ContactFilter2D` (`Layers.Any(Layers.EnemyMask)`), calling `IHittable.Hit`.
- **Walkers, flyers, bosses, the hanged**: also Dynamic with
  `gravityScale 0`, their own gravity in level pixels, velocity set in
  `FixedUpdate` (`EnemyPhysics.Prepare`). One `BoxCollider2D` on `Enemy`.
  They hurt the hero with `EnemyPhysics.HurtHero` (an OverlapBox of the
  body or the swing's reach over `Player`), never by contact callbacks.
- **Shots** (`EnemyShot`, `LightBolt`): kinematic trigger boxes moved by
  `MovePosition`, checking `OverlapBox` each step; they break on
  `Layers.SolidMask`.
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
- Dash sets velocity directly for `DashTime` and ignores gravity. The
  hero and the enemies never collide physically (as the Godot masks had
  it), so no layer switch is needed; the light dash passes walls of light
  with `Physics2D.IgnoreLayerCollision(Player, LightBarrier)` for its
  duration, and ledge drops and slams do the same with `Ledge`.

## Layers and masks

Set by `Descender.Combat.Layers.ApplyCollisionMatrix()`, which
`ProjectBootstrap` runs into the project settings and the hero's `Awake`
runs again at play time (idempotent), so the matrix is right even in a
test scene:

| Layer | Collides with |
|---|---|
| World | Default, World, Player, Enemy |
| Ledge | Player (one-way), Enemy |
| Player | World, Ledge, LightBarrier, Hazard, Pickup, Trigger |
| Enemy | World, Ledge |
| Hazard, Pickup, Trigger | Player |
| LightBarrier | Player (ignored during a light dash) |

The hero and the enemies pass through each other; hits are queries.
Read layers and masks through `Descender.Combat.Layers` (`Layers.Enemy`,
`Layers.GroundMask`, `Layers.Solid(mask)`, `Layers.Any(mask)`), never a
magic int. `Physics2D.queriesHitTriggers`
is off; hit detection that wants triggers passes a `ContactFilter2D` with
`useTriggers = true`.

## Hits, damage, invulnerability

- A hit is an `OverlapBox` over the other side's layer; the hitter looks
  for `IHittable` on `attachedRigidbody` with `TryGetComponent` and calls
  `Hit(damage, from)`, which returns false when the target cannot be hurt
  now. No tags, no hitbox children.
- The sword's box is queried during the attack's active frames by the
  attack state, not by animation events, so the timing is the same at any
  frame rate; a `HashSet<Collider2D>` per swing stops double hits.
- Invulnerability after a hit is a timer (`PlayerController.CanBeHurt`);
  while it runs `Hurt` returns false and the sprite blinks
  (`sprites-and-animation`).
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

The hero's `Kill()` drops input, plays the death strip and raises `Died`;
with `autoRespawn` on, `Respawn()` follows after `tuning.deathTime` at the
last checkpoint (`Checkpoint` calls `RestAt` on touch and raises the
static `Checkpoint.AnyRested` for the game flow). Enemies killed since
the checkpoint are in `Enemies.Kills` by placement id, cleared on
respawn (`Kills.Cleared`); bosses set `BossIds.FlagFor(id)` permanently.

## Things that went wrong in Godot and must not come back

- Moving a body from `Update` (jitter at any non-60 Hz display).
- Stuck on tile seams: keep the composite collider, inset the feet.
- Double damage from overlapping boxes: one body box per enemy, the
  hero's sword tracks a `HashSet<Collider2D>` of what it hit this swing,
  cleared when the swing starts.
