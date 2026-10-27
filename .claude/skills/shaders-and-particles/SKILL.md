---
name: shaders-and-particles
description: Visual effects: canvas_item shaders in assets/shaders, ShaderMaterial parameters and per-instance copies, screen-reading post effects (PostFx), CPUParticles2D bursts and the RegionFx ambient particle presets. Use when writing a shader, tinting or flashing sprites, adding particles, or changing the post stack.
---

# Shaders and particles

## canvas_item shaders

Shaders live in `assets/shaders/*.gdshader` and are `preload`ed into
constants (`const FLASH_SHADER := preload(".../hit_flash.gdshader")`).
Existing ones: `hit_flash` (whiten by a `flash` uniform), `rim` (edge
light), `sky` (gradient backdrop), `post` (tint, contrast, saturation,
vignette over the screen) and `post_tint` (the same without reading the
screen).

```glsl
shader_type canvas_item;

uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform sampler2D screen : hint_screen_texture, filter_nearest;  // only when needed

void fragment() {
	COLOR.rgb = mix(COLOR.rgb, vec3(1.0), flash);
}
```

- `COLOR` is the sampled texture times modulate in `fragment()`; `UV`,
  `TEXTURE`, `SCREEN_UV`, `TIME` are available. `vertex()` moves
  `VERTEX` for sway. `light()` runs per `PointLight2D` for custom shading;
  the default handles normal maps (`CanvasTexture`), so most sprites
  need no `light()`.
- Hints matter: `source_color` for colours (sRGB), `hint_range` for
  sliders, `filter_nearest` on textures in a pixel-art game,
  `hint_screen_texture` to read the screen.
- Keep the shader free of branches on uniforms where a second cheaper
  shader can do (that is why `post_tint` exists). Texture reads are the
  cost; a shader reading the screen copies the whole frame first, which
  at a 4K window costs more than a 120 Hz frame (`PostFx` falls back to
  `post_tint` when contrast and saturation are 1).
- Mobile renderer, OpenGL3 fallback: avoid `textureLod` tricks, derivatives
  and anything not in GLES3. Test with `--rendering-driver opengl3`.

## ShaderMaterial in code

```gdscript
var material := ShaderMaterial.new()
material.shader = FLASH_SHADER
sprite.material = material
material.set_shader_parameter("flash", 1.0)
create_tween().tween_property(material, "shader_parameter/flash", 0.0, 0.1)
```

A material set on a scene in the editor is **shared** by every instance;
give an instance its own copy (`material.duplicate()` or build it in
code as `Feedback.flash` does) before animating a uniform, or every imp
flashes together. Set `resource_local_to_scene = true` on a material in a
`.tscn` to have Godot copy it per instance. A `CanvasGroup` applies a
material to its children as one image (for an outline around a
multi-sprite enemy). `use_parent_material = true` lets child sprites share.

Plain tints need no shader: `modulate` (and `self_modulate`) multiply the
colour (`TINT_WINDOW`, `TINT_HURT` on enemies), `CanvasItem.material` with
`CanvasItemMaterial.blend_mode = BLEND_MODE_ADD` makes glows.

## Post effects (`PostFx`)

A `CanvasLayer` (layer 1) with a full-rect `ColorRect` carrying the grade
material, placed before the HUD so it draws under it. `set_grade(tint,
contrast, saturation)` is eased by `Main` between regions (`RegionLook`).
A new full-screen effect goes on this layer as another rect with its own
shader, cheapest first; it must ignore the mouse
(`mouse_filter = MOUSE_FILTER_IGNORE`).

## Particles

- `CPUParticles2D` everywhere (GPU particles are not worth the mobile and
  GLES3 caveats at this scale). Bursts (`HitSpark`, `DeathPuff`,
  `Feedback.dust`) are scenes with `one_shot = true`, `explosiveness`
  near 1, `emitting = true` in `_ready` and `queue_free` after
  `lifetime + 0.05` via `get_tree().create_timer`. Colour comes from the
  caller (`Feedback.spark(position, color)`).
- Ambient particles are `RegionFx`: per region `assets/regions/<key>/fx.json`
  lists entries with a `texture` or a `preset` (`embers`, `dust`, `ash`,
  `leaves`, `rain`, `snow`, `birds`) plus `amount`, `color`, `lifetime`,
  `direction`, `spread`, `speed [min, max]`, `gravity`, `scale`, `spin`,
  `sway`. Emitters follow the camera with a `MARGIN` so the air is never
  empty, and fade over `FADE` seconds when the region changes. Add a
  preset to `PRESETS` rather than a new node type.
- Tuning knobs that matter: `amount` (cost is per particle per frame on
  the CPU), `lifetime`, `emission_shape` (`EMISSION_SHAPE_RECTANGLE` with
  `emission_rect_extents` for area fills), `direction` + `spread`,
  `initial_velocity_min/max`, `gravity`, `scale_amount_curve`,
  `color_ramp` for fade-outs, `local_coords = false` so trails stay put
  when the emitter moves.
- Hundreds of particles per emitter are fine; dozens of emitters are not.
  Pool bursts through `Feedback` rather than spawning a scene per hit
  from many scripts, so the smoke test can count them (`spark_count`).

## Debugging

The editor's shader errors show in the script panel; at runtime a broken
shader draws magenta. Use `--verbose` headless runs to catch shader
compile errors in tests, and keep every shader used by a scene the
smoke test visits, so CI compiles it.
