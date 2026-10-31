---
name: urp-2d-lighting-and-camera
description: The URP 2D Renderer in this project (Light2D types, blend styles, normal maps as the _NormalMap secondary texture, Sprite-Lit materials, the global ambient per region, bloom through a Volume), the Pixel Perfect Camera at 320x180, camera follow and limits, screen shake, and parallax backdrops. Use when touching lights, materials, the camera, regions' look, or anything that renders.
---

# URP 2D lighting and camera

`Assets/Settings/Descender URP.asset` (UniversalRenderPipelineAsset) with
`Descender 2D Renderer.asset` (Renderer2DData), created and assigned to
Graphics and every Quality level by `Descender > Setup > Render pipeline
and settings`. HDR off, MSAA off (pixel art). Everything that renders uses
URP 2D shaders; a Built-in shader shows magenta.

## Lights

The Godot regions have 853 `PointLight2D`s with a shared normal-mapped
look. In URP:

- `Light2D` component, type **Point** for lanterns and glows (radius in
  units: Godot `texture_scale` x texture half-size / 16), **Freeform** for
  shafts and doorways, **Global** one per region for the ambient
  (`RegionLook.ambient` colour and `light` intensity).
- `Target Sorting Layers`: Background, Decor, Terrain, Props, Characters.
  The Glow layer is **not** lit (it is additive emissive art).
- `Blend Style 0` (Multiply) is the only one used; keep the renderer at
  one blend style so the whole lighting pass is one texture.
- `Normal Map Quality: Accurate`, `Normal Map Distance` 3 for point lights
  that should pick up the tile and prop normals; `Disabled` on lights that
  are pure colour washes (cheaper).
- Shadows off (the Godot game had none); `Volumetric` off.
- Light intensity 1 at colour `(1,1,1)` is "unlit art shows as painted".
  Ambient under 1 darkens; the per-light `Falloff` 0.5 matches the Godot
  gradient textures.

Light count is fine in URP (lights batch per layer), but every light with
normal maps re-reads the normal target. Keep normal-mapped lights to the
ones near the hero's path; use `WorldLight` (a tiny component that
disables its `Light2D` when further than 30 units from the camera).

## Materials and normal maps

- Sprites use `Universal Render Pipeline/2D/Sprite-Lit-Default`
  (`Assets/Settings/Materials/Sprite Lit.mat`); glow layers use
  `Sprite-Unlit-Default` with additive blending (`Sprite Glow.mat`).
- Normal maps are **secondary textures** named `_NormalMap` on the sprite,
  set in the Sprite Editor (Secondary Textures) or, for imported art, by
  the `.meta` the import script writes. The lit shader reads them; there
  is no per-material normal slot. A `_n.png` must import as `Texture Type:
  Normal map`, sRGB off, point filter, no compression.
- Tilemaps: the `TilemapRenderer` material is `Sprite Lit`; the tile sheet
  sprites carry their `_NormalMap` secondary texture too.
- One material per look, shared; per-instance colour goes through
  `SpriteRenderer.color`, never `renderer.material.color` (that clones
  the material and breaks batching). Hit flash uses a
  `MaterialPropertyBlock` or swaps to `Sprite Flash.mat` for a few frames.

## Camera

`Main.unity` has one `Camera` (Orthographic, `Background: Solid Color`,
`Culling Mask` everything, `Post Processing` on) with:

- `Pixel Perfect Camera` (URP): `Assets Pixels Per Unit 16`, `Reference
  Resolution 320 x 180`, `Crop Frame: None`, `Grid Snapping: Pixel
  Snapping`, `Upscale Render Texture` on, `Stretch Fill` off. The game
  scales by integers to the window; the `scale` setting picks the window
  size (`SettingsData.WindowSizeFor`).
- `GameCamera` (ours): follows `Player.transform` with a look-ahead in
  the facing direction, a dead zone, `SmoothDamp` on X only, hard limits
  from the current `WorldRoom` bounds, and `Shake(pixels, seconds)` that
  offsets in whole pixels and decays. Runs in `LateUpdate` after the
  interpolated body has moved. Camera position is snapped to the pixel
  grid in `LateUpdate` (`Mathf.Round(x * 16) / 16`).
- `Volume` (global) with **Bloom** (threshold 1, intensity 0.6, scatter
  0.7) for the glow layer; nothing else. `Universal Additional Camera Data`
  Anti-aliasing none.

## Backdrops and parallax

Godot `Parallax2D` layers become `BackdropLayer` components: each child
sprite layer has a `parallaxFactor` (0 = fixed to camera, 1 = moves with
world) and a `repeatWidth`; in `LateUpdate` it offsets by `(camera -
origin) * (1 - factor)` and wraps by `repeatWidth`. Sky is a two-colour
gradient drawn by `SkyGradient` (a quad with the `Sky` Shader Graph,
`RegionLook.skyTop`/`skyBottom`), fixed to the camera, sorting layer
Background order -100. Fringes (`RegionFringes`) are sprites at the room
edges that fade with distance; particles see `shaders-and-particles`.

## Sorting

Sorting layers (Project Settings > Tags and Layers), back to front:
Background, Decor, Terrain, Props, Characters, Glow, Fx, UI. Within a
layer `sortingOrder` orders; the hero is Characters 10, enemies 0,
projectiles 20. Transparency Sort Mode is `Custom Axis (0,1,0)` so a
lower Y draws in front within the same order.

## Region look

`RegionLook` (`Assets/Data/Regions/<name>.asset`) is applied by
`RegionLookApplier` when a region loads: global light colour and
intensity, sky colours, `cave` (hides the sky), tint/contrast/saturation
go to the `Grade` Volume override, `music` to `Music.Play`.

## Checklist for a new lit sprite

1. Import with `Sprite Mode`, PPU 16 or 64, point filter, no compression.
2. Add the `_n.png` as `_NormalMap` secondary texture.
3. Material `Sprite Lit`; sorting layer set; `sortingOrder` chosen.
4. If it glows, a child sprite on Glow with `Sprite Glow`.
