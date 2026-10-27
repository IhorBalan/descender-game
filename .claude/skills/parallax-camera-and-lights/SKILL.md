---
name: parallax-camera-and-lights
description: The view: GameCamera (Camera2D with custom follow and shake), Backdrop and ParallaxSprite parallax layers, CanvasLayer overlays, PointLight2D with CanvasTexture normal maps, canvas transforms and the 320x180 integer-scaled viewport. Use when the camera, backgrounds, lighting, screen-space overlays or resolution are involved.
---

# Camera, parallax, layers and lights

## Resolution model

`project.godot`: viewport 320x180, window override 1280x720, stretch mode
`canvas_items`, aspect `keep`, scale mode `integer`, texture filter
nearest. `GameCamera.VIEW_ZOOM = 0.5` zooms out so the view shows 640x360
level pixels while the window keeps drawing at its own resolution, so
pixel art stays whole at every whole-number window scale
(`Settings.window_size_for(scale)`, 2 to 6). 2D pixel snapping is **off**
on purpose: snapping rounds every node to a level pixel, which makes layers
that scroll at their own speed hop against the world. Do not turn it on,
and do not round camera or parallax positions.

Convert between spaces with `get_viewport().get_canvas_transform()`
(world to screen) and `get_canvas_transform().affine_inverse()`;
`get_viewport_rect().size` is the base 320x180, `get_visible_rect()` on the
camera is the world rect in view. UI goes on a `CanvasLayer` so it ignores
the camera entirely.

## GameCamera (`scripts/game_camera.gd`)

A `Camera2D` with built-in smoothing off and its own follow in
`_physics_process`: a horizontal dead zone (`DEADZONE_X`), look-ahead that
only flips after `LOOK_SWITCH_DISTANCE`, vertical follow only on landing,
dropping or climbing past `UP_WINDOW`, a hold-down peek, level `limit_*`
from the world bounds, and trauma shake (`add_trauma`, offset grows with
trauma squared, never moves the follow position). `main.gd` sets `target`
because the hero is spawned at runtime; the camera adds itself to the
`game_camera` group so `Feedback.shake` finds it.

- Tune feel through the constants; do not switch on `position_smoothing`
  (it fights the custom follow and physics interpolation).
- After teleporting the hero, move the camera too and call
  `reset_physics_interpolation()` on both.
- Shake uses `offset`, so anything that reads `global_position` of the
  camera for logic should use the follow position.

## Backdrop (`scripts/backdrop.gd`)

Painted parallax per region from `assets/regions/<key>/parallax.json`
(`art_tile`, `layers[]` with `file`, `scroll [x, y]`, `y`, `cover`,
`front`, `drift`, `sway`, `blend` mix/add/screen, `lit`, `repeat_y`,
`depth`, `tint`). Layers are drawn back to front, scaled by
`tile / art_tile`, repeat sideways, and cross-fade with tweens when the
region changes. Add a layer by editing the JSON and dropping the PNG next
to it; the unit test checks every named PNG exists. `front: true` layers
draw over the level (haze), `lit: true` lets the level lights hit the
layer (fog), everything else ignores lights.

`ParallaxSprite` (`extends Parallax2D`) is for a single placed sprite
with `scroll_scale`: it keeps `scroll_offset` so the sprite sits where
the editor put it when the camera is centred on it, sets
`ignore_camera_scroll = true`, reads the camera from the canvas transform
each frame (not Parallax2D's own tracking, which floors to pixels), runs at
`process_priority = 1` after the camera, and turns physics interpolation
off because it moves in `_process`. Follow that pattern for anything new
that scrolls with its own factor; prefer `Parallax2D` over the old
`ParallaxBackground`.

## CanvasLayers

Overlays live on `CanvasLayer`s so they ignore the camera: `PostFx`
(layer 1, placed before the HUD so it draws under it), the HUD, menus
(`PauseMenu` layer 10, `process_mode = PROCESS_MODE_ALWAYS`, see
`ui-and-input`), `ScreenFade` and `DialogueBox` on layer 5. (`PostFx` is layer 1, the pause menu 10.) Higher `layer` draws later. A
`CanvasLayer` with `follow_viewport_enabled` would scale with the camera;
leave it off for UI. World-space effects (sparks, lights, parallax)
stay in the world, not on a layer.

## Lights and normal maps

- The TileSet atlases are `CanvasTexture`s with a diffuse and a normal map
  (`levels/world/tiles/<region>_n.png`), so terrain shades under
  `PointLight2D`s. New tile art needs a matching `_n.png`.
- `WorldLight` (`@tool`, `extends PointLight2D`) is the editor marker in a
  region's `Lights` holder; `LevelBuilder._build_lights` builds the runtime
  light with a 128-pixel disc texture, `texture_scale` giving the radius
  (`radius() = texture_scale * 64`), `energy`, `color`, and an optional
  `flicker`.
- Lights are cheap in 2D compared to 3D but each light re-renders the
  items it touches: keep `range_layer_min/max` and `range_item_cull_mask`
  tight, do not give hundreds of tiles their own light, and turn
  `shadow_enabled` on only where the look needs it (each shadowed light
  draws occluders again). The Mobile renderer caps lights per item; a
  region of torches should share a few lights, not one per torch tile.
- Glow tiles are a separate `Glow` `TileMapLayer` drawn above with
  emissive art, not extra lights.
- `CanvasModulate` or the `PostFx` grade sets the region's darkness; the
  descent darkens through `WorldScene`'s settings and `RegionLook`, so a
  new region sets its grade there rather than with a dark overlay.

## Draw order

Within a canvas: `z_index` (and `z_as_relative`), then tree order, then
`y_sort_enabled` where set. The world uses tree order (layers back to
front, then placements, hero, effects); overlays use `CanvasLayer.layer`.
Use `z_index` only locally (a hitbox debug draw, a projectile over its
enemy), never to lift something above UI.
