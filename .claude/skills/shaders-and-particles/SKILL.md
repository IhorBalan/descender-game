---
name: shaders-and-particles
description: Shaders and effects in this project: Shader Graph with the Sprite Lit and Sprite Unlit targets replacing the Godot canvas_item shaders (flash, sky gradient, water, dissolve), material property blocks, the glow layer and bloom, and Particle System for region ambience and hit effects. Use when adding a visual effect, a shader, a particle system or touching the glow look.
---

# Shaders and particles

## Shader Graph

The Godot `canvas_item` shaders under `godot_assets/shaders/` (copied to
`Assets/Shaders/Godot/` as reference) are rewritten as Shader Graphs
under `Assets/Shaders/`, `Universal` target, `Material: Sprite Lit` (takes
light and the `_NormalMap`) or `Sprite Unlit` (glow, sky):

| Godot shader | Graph | Target |
|---|---|---|
| `flash.gdshader` | `SpriteFlash` | Sprite Unlit, outputs a `_FlashColor` where alpha > 0 |
| `sky.gdshader` | `SkyGradient` | Sprite Unlit, lerps `_Top`/`_Bottom` by screen Y |
| `water.gdshader` | `Water` | Sprite Lit, UV scroll + sine, `_Speed`, `_Amplitude` |
| `dissolve.gdshader` | `Dissolve` | Sprite Lit, noise threshold `_Progress` |
| `grade.gdshader` (post) | Volume override `Grade` (tint/contrast/saturation) via Color Adjustments | |

Rules: the fragment reads the sprite texture through the `Main Texture`
node (so Sprite Atlas and the renderer's sprite work); `Alpha Clip` off;
`Blend Mode: Alpha` except glow (`Additive`). Keep graphs small, name
properties with `_` prefix and expose them as `Material` properties.
`Shader.Find` by the graph's name under `Shader Graphs/` only in editor
code; runtime references a `[SerializeField] Material`.

## Per-instance parameters

```csharp
private static readonly int ProgressId = Shader.PropertyToID("_Progress");
private MaterialPropertyBlock block;

private void Awake() => block = new MaterialPropertyBlock();
private void SetProgress(float t)
{
    renderer.GetPropertyBlock(block);
    block.SetFloat(ProgressId, t);
    renderer.SetPropertyBlock(block);
}
```

A property block does not break SRP batching for sprites in the 2D
Renderer; `renderer.material` does (it clones). Colour tint goes through
`SpriteRenderer.color`.

## Glow and bloom

Glowing art is a second sprite (`<name>_glow.png`) on the `Glow` sorting
layer with `Sprite Glow.mat` (Sprite Unlit, additive), not lit. The
global `Volume` has Bloom threshold 1 so only glow pixels above 1 after
the additive blend bloom; keep glow art bright (near white where it
should bloom). The Glow tilemap layer uses the same material.

## Particles

`ParticleSystem` (Shuriken), not VFX Graph (no 2D Renderer lighting, too
heavy). Region ambience (`RegionParticles`: dust, embers, snow, spores)
is a prefab per kind under `Assets/Prefabs/Fx/Ambience/` with:

- `Simulation Space: World`, `Scaling Mode: Hierarchy`, `Max Particles`
  at most 200, `Prewarm` on.
- `Shape: Box` the size of the camera view plus a margin, following the
  camera (`RegionParticles` moves the emitter in `LateUpdate`).
- `Renderer`: `Render Mode: Billboard`, material `Sprite Glow` or
  `Sprite Unlit Particles.mat`, sorting layer Fx, `Sort Mode: None`.
  Pixel art particles are a `Texture Sheet Animation` over a tiny sheet
  at PPU 16 so they stay on the pixel grid.
- Size in units: a 1 px dust mote is 1/16.

Hit effects (`HitSpark`, `Dust`, `DeathBurst`) are pooled prefabs with
`Stop Action: Disable`; `Fx.Spawn(prefab, position, flipX)` pulls from
the pool, plays, and the system disables itself. Never `Instantiate` a
particle prefab per hit.

## Screen effects

Only the Volume: Bloom and Color Adjustments (`Grade`). No custom
`ScriptableRendererFeature` yet; if one is added it lives in
`Assets/Scripts/Runtime/Rendering/` and is added to the 2D Renderer
asset by `ProjectBootstrap`.
