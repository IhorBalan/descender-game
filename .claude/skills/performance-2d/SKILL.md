---
name: performance-2d
description: Keeping the Unity game fast on modest hardware: the Profiler and Frame Debugger, draw calls and SRP batching for sprites and tilemaps, Light2D cost, physics cost (colliders, queries), GC allocations in hot paths, object pooling, additive scene streaming, and what not to optimise. Use when something is slow, when adding many objects, or when a reviewer asks about performance.
---

# 2D performance

Targets: 60 fps at 1080p on an integrated GPU, no GC spike while playing,
region load under 0.5 s. Measure before changing anything.

## Tools

- `Window > Analysis > Profiler`, CPU and Rendering modules, in a build
  (`Development Build` + `Autoconnect Profiler`) for real numbers; the
  editor adds overhead to everything.
- `Window > Analysis > Frame Debugger` shows draw calls and why a batch
  broke (different material, sorting layer interleaving, property block
  with a texture change).
- `Profiler > Memory > GC Alloc` column, or `ProfilerRecorder` on
  `GC.Alloc` in a PlayMode test that asserts zero allocation over 60
  frames of idle play (`AllocationTests`).

## Rendering

- The 2D Renderer batches sprites that share material and texture within
  a sorting layer; interleaving two materials by sorting order breaks the
  batch. Keep all props on `Sprite Lit` with a shared texture per region
  where possible (Sprite Atlas per region: `Assets/Art/Atlases/<region>.
  spriteatlasv2`, `Include in Build`, point filter, no compression).
- Tilemaps render per chunk per material; four layers is four batches
  plus lights. Fine.
- Each `Light2D` with normal maps on costs a pass over the normal
  target. `WorldLight` disables lights beyond 30 units; keep the shared
  normal-map lights below ~40 live. Global light is one.
- Bloom is the only post effect; `Volume` blending is cheap.
- `Pixel Perfect Camera` renders at 320 x 180 then upscales, so fill rate
  is never the problem; overdraw of large transparent backdrops is fine.

## Physics

- Composite collider on the Terrain tilemap (one body), not per-tile
  colliders.
- Kinematic enemies move with `MovePosition`; only the hero is dynamic.
- Queries use `ContactFilter2D` + non-alloc overloads with a cached
  array. One ground check and two wall checks per hero step, not one per
  frame.
- `Physics2D.simulationMode: FixedUpdate` (default) at 60 Hz; never raise
  the fixed rate. `Rigidbody2D.interpolation: Interpolate` on the hero
  and enemies so 144 Hz displays look smooth.
- Sleeping: enemies far from the hero `enabled = false` their behaviour
  and body (`WorldPlacement` handles distance), not `Destroy`.

## Allocation

Zero per frame in play. Watch for: LINQ, `string` building for the HUD
(use cached strings and `TMP_Text.SetText(format, value)`),
`GetComponentsInChildren` without a list, `foreach` over interfaces,
closures in `Update`, `new Vector3[]`, boxing in `Debug.Log` (never log
in play). Pools: `Fx`, `AfterimagePool`, projectiles, pickups
(`UnityEngine.Pool.ObjectPool<T>`).

## Streaming

Regions load additively with `LoadSceneAsync`, `allowSceneActivation`
true; neighbours preload when the hero is within a room of a door; the
far region unloads with `UnloadSceneAsync` after the door transition.
Addressables are not used. Audio for the next region is loaded with the
scene (clip references on the `RegionRoot`).

## Not worth it

- Merging tilemap layers (the batches are already few).
- Replacing `Animator` with a hand sprite swapper (the animator cost is
  negligible at this count; the generated clips are the workflow).
- IL2CPP for performance (Mono is fine here; IL2CPP is a platform
  decision).
- Caching `transform` (already cached by Unity).
- Micro-optimising C# that runs once per region load.
