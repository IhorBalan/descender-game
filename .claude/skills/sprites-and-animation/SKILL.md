---
name: sprites-and-animation
description: Sprite import settings (PPU, point filter, slicing, pivots), the SpriteFramesImporter that builds Animation Clips and Animators from sprites.json, playing animations by name from code, flipping, hit flash, squash and stretch and other tweens by coroutine. Use when adding or changing character art, animations, or sprite feedback.
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

## Clips and Animators

`Descender > Import > Sprite animations` (`SpriteAnimationImporter`)
builds, per character, `Animations/<character>_<anim>.anim` clips (sprite
keys at the spec's fps, `Loop Time` from `loop`) and one
`<character>.controller` with a state per clip, `idle` as default and no
transitions. Code drives it directly:

```csharp
[SerializeField] private Animator animator;
private static readonly int RunHash = Animator.StringToHash("run");

private void Play(string state) => animator.Play(state, 0, 0f);   // restarts
private void PlayIfNew(int hash)
{
    if (!animator.GetCurrentAnimatorStateInfo(0).shortNameHash.Equals(hash))
        animator.Play(hash);
}
```

Hash the state names in `static readonly int` fields. Animator `Update
Mode: Normal`, `Culling: Always Animate` on the hero (so off-screen
timing still matches). Finishing: `animator.GetCurrentAnimatorStateInfo(0)
.normalizedTime >= 1f` for a non-looping clip, or use an `AnimationEvent`
only for cosmetic hooks (footstep dust), never gameplay timing.

Re-run the importer after editing `sprites.json` or a strip; it keeps
existing clips' extra curves and the controller's extra states.

## Facing and flipping

`SpriteRenderer.flipX` for characters whose hitboxes are symmetric;
otherwise scale the `Visual` child by -1 on X so child hitboxes and light
offsets flip too (the hero does this). Never flip the Rigidbody's own
transform.

## Feedback

- **Hit flash**: swap the renderer's `sharedMaterial` to `Sprite Flash.mat`
  (unlit white) for `tuning.HitFlashFrames` fixed steps, then back;
  respects the `hitFlash` setting. Do not touch `.material` (clones).
- **Squash and stretch** on land/jump: a coroutine scaling the `Visual`
  child from `(1.3, 0.7)` back to `(1, 1)` over 0.12 s with
  `Mathf.SmoothStep`. All tweens are small coroutines in
  `Descender.Fx.Tween` (`Scale`, `Fade`, `Shake`, `Punch`); no tween
  package.
- **Afterimages** for the dash: `AfterimagePool` spawns pooled
  `SpriteRenderer`s with the current sprite, fading over 0.2 s.
- **Death**: the death clip plays, then `Fade` of the renderer; the
  object is disabled, not destroyed, until respawn.

## Pivots and offsets from Godot

Godot `AnimatedSprite2D.offset` and `Sprite2D.offset` become the sprite
pivot in the import (`sprites.json` has `pivot` when it differs from
bottom-centre). Godot `scale = 0.25` on 4x prop art becomes PPU 64. If a
sprite appears a pixel off the ground, fix the pivot in the Sprite
Editor, not with a transform offset.
