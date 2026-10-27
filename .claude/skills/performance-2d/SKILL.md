---
name: performance-2d
description: Keeping the game fast on the Mobile renderer and the OpenGL3 fallback: profiling with the Godot profiler and Time.get_ticks_usec, node and draw-call budgets, TileMap and particle costs, physics cost, screen-reading shaders, streaming, and the project's existing tricks. Use when something is slow, when adding many nodes or lights, or when choosing between implementations.
---

# 2D performance

## Measure first

- Editor: Debugger → Profiler, **Start** (or Autostart), play, read
  *Frame Time* vs *Physics Time* vs *Idle Time*; switch to *Self* to find
  the function that is really slow, not the one waiting on it. Frame time
  includes rendering, so a spike with cheap scripts is particles,
  lights or a shader.
- Monitors tab: `Objects/Nodes`, `2D Draw Calls`, `2D Items`, `Physics
  2D Active Objects`, `Collision Pairs`.
- Code timing:
  ```gdscript
  var t := Time.get_ticks_usec()
  stream_around(points)
  print("stream_around: %d us" % (Time.get_ticks_usec() - t))
  ```
  Average over many calls; the first run is cold cache.
- Test with V-Sync off (`Settings.vsync`) to see headroom, and at a big
  window size: a fill-rate problem (post shader, overlapping transparent
  layers) shows as FPS dropping with window size.
- Test on the OpenGL3 fallback too (`--rendering-driver opengl3`); the
  Mobile (Vulkan) path hides driver costs the fallback does not.

## What already costs little, and why

- **Streaming** (`LevelBuilder.stream_around`): 3 of 64 chunks live
  instead of 880,000 cells; collision is merged runs, not per tile. Keep
  new per-cell work inside the chunk build, not per tick.
- **Hazards** only poll while a body is inside
  (`set_physics_process(false)` by default).
- **Parallax** layers are a few sprites per region, repeated by drawing,
  not by nodes.
- **PostFx** avoids reading the screen unless contrast or saturation
  changes (a screen read copies the frame).
- **Audio** uses a fixed pool of 8 players.
- **Particles** are CPU, one emitter per preset, following the camera.

## Budgets and rules of thumb

- Nodes: thousands are fine, tens of thousands are not; `_process` and
  `_physics_process` propagate through every node that has them. Park
  nodes with `set_process(false)` / `remove_child` rather than hiding;
  free with `queue_free` what will not come back.
- Draw calls: 2D batches consecutive items that share texture and
  material. Atlases (one per region) keep tiles in one batch; a unique
  `ShaderMaterial` or texture per sprite breaks the batch. Share
  materials; give a sprite its own copy only while it flashes.
- `TileMapLayer`: cost is per quadrant updated. Painting cells every tick
  rebuilds quadrants; animate with shaders or sprites instead. Four layers
  per region is the budget.
- Lights: each `PointLight2D` re-draws the items in its range; shadows
  draw occluders again. Dozens per screen is too many; use the `Glow`
  layer and emissive art for most "lights".
- Transparency and overdraw: full-screen transparent rects (fog layers,
  scrims) cost fill rate on every pixel; keep `front` layers few and
  small, and do not stack several screen-reading shaders.
- Physics: keep 60 Hz ticks; simple rectangle shapes; disable monitoring
  on areas nobody queries (`monitorable = false`); remove bodies that are
  off-screen (streaming does this for terrain; enemies far from the hero
  can `set_physics_process(false)`). Physics interpolation is cheap and
  already on.
- GDScript: typed code runs faster; avoid building arrays or dictionaries
  per tick (`get_overlapping_bodies()` allocates), cache `get_node` in
  `@onready`, use `StringName` for group and bus names, prefer signals to
  polling, and move per-frame math out of nested loops.
- Loading: `preload` constants load with the script; a big `load()` in
  `_ready` stalls the frame, so background-load with
  `ResourceLoader.load_threaded_request` for big things.

## Don't

- Don't optimise without a profile; a 9x speed-up of a 9 ms function in a
  59 ms frame buys little.
- Don't lower the physics tick to save CPU; it adds input lag and jitter.
- Don't switch to GPU particles or servers for a few hundred particles.
- Don't call `RenderingServer` or `PhysicsServer2D` getters every frame;
  they stall the server.
- Don't turn on 2D pixel snapping for smoothness (see
  `parallax-camera-and-lights`), or `position_smoothing` on the camera.
