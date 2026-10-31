---
name: sprites-and-animation
description: Sprite import settings (PPU, point filter, slicing, pivots), the SpriteAnimator strips that tools/unity/build_prefabs.py fills from sprites.json, playing animations by name from code, flipping, hit flash, squash and stretch and other tweens by coroutine. Use when adding or changing character art, animations, or sprite feedback.
---

# Sprites and animation

## Import settings

Every pixel texture: `Texture Type: Sprite (2D and UI)`, `Pixels Per Unit`
16 for level art (characters, tiles, UI at 1x) or 64 for painted art
(props, backdrops, drawn at 4x), `Filter Mode: Point`, `Compression:
None`, `Generate Mip Maps` off, `Wrap: Clamp`. `Mesh Type: Full Rect` for
anything with a normal map or that flips (tight meshes clip the normal
edge). `Read/Write` off.

`tools/unity/import_godot_assets.py` writes these into the `.meta` for art
it copies, including the slicing below; a texture dropped in by hand gets
them from the `Descender > Import > Apply pixel art import` menu item.

## Slicing

Strips in `Assets/Art/Sprites/<character>/<anim>.png` are described by
`sprites.json` (one entry per character: frame size, animations with
file, fps, loop). The import script slices them as `Sprite Mode:
Multiple` with sprites named `<file>_<index>`, pivot bottom-centre for
characters (feet on the ground; Godot used `offset` for the same). Props
pivot top-left (Godot `centered = false`). UI pivots centre.

## Strips and the SpriteAnimator

Characters are animated by `Descender.Fx.SpriteAnimator` (on the `Visual`
child, next to the `SpriteRenderer`), not by Animator controllers: its
serialized `strips` are `{ name, fps, loop, frames[] }`, and
`tools/unity/build_prefabs.py` fills them into the prefabs from
`sprites.json`, so a new strip is a `sprites.json` entry plus a run of
that script. Code drives it by name:

```csharp
sprite.Play("run");            // starts it unless it is already playing
sprite.Restart("attack");      // from the first frame, even if playing
sprite.PlayOnce("hurt");       // a one-shot the state machine checks with Finished
sprite.Pose("attack", 0);      // holds one frame (a windup)
if (sprite.Finished) ...       // a non-looping strip reached its end
sprite.SpeedScale = 1.5f;      // hastened loops (a charging boss)
```

`Has(name)` and `Length(name)` ask about strips; a missing name logs once
and keeps the current frame, so a character without a `glide` strip still
plays. It updates on `Time.deltaTime`, so hit-stop slows it with the game.
`SpriteAnimator.CreateChild(transform)` makes an empty one for an object
built in code (tests).

`SpriteAnimationImporter` (`Descender > Import > Sprite animations`) still
builds `.anim` clips and controllers for anything that wants an Animator
(UI, cutscenes); gameplay characters do not use them.

## Facing and flipping

`SpriteRenderer.flipX` for characters whose hitboxes are symmetric;
otherwise scale the `Visual` child by -1 on X so child hitboxes and light
offsets flip too (the hero does this). Never flip the Rigidbody's own
transform.

## Feedback

- **Hit flash**: `Descender.Fx.HitFlash` swaps its renderers'
  `sharedMaterial` to `Assets/Settings/Materials/Sprite Flash.mat` (the
  `Descender/Sprite Flash` shader: the sprite's alpha in flat white) for
  0.1 s of real time, then back; respects the `hitFlash` setting. Call it
  through `Feedback.Flash(component)`. Do not touch `.material` (clones).
- **Squash and stretch** on land/jump: a coroutine scaling the `Visual`
  child from `(1.3, 0.7)` back to `(1, 1)` over 0.12 s with
  `Mathf.SmoothStep`. All tweens are small coroutines in
  `Descender.Fx.Tween` (`Scale`, `Fade`, `Topple`, `Move`, `Wait`); no
  tween package.
- **Sparks, puffs, dust**: `Feedback.Spark/Puff/Dust` emit from three
  ParticleSystems made in code; `Feedback.Shake` and `Feedback.HitStop`
  respect the `shake`, `rumble` and `hitStop` settings.
- **Death**: `EnemyDeath.Play` runs the death strip, topples the sprite,
  waits the corpse time and fades it, then the enemy destroys itself. The
  hero's death strip plays and the object stays for the respawn.

## Pivots and offsets from Godot

Godot `AnimatedSprite2D.offset` and `Sprite2D.offset` become the sprite
pivot in the import (`sprites.json` has `pivot` when it differs from
bottom-centre). Godot `scale = 0.25` on 4x prop art becomes PPU 64. If a
sprite appears a pixel off the ground, fix the pivot in the Sprite
Editor, not with a transform offset.
