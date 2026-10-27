---
name: tilemap-and-terrains
description: The world grid: TileMapLayer, the shared TileSet with its atlas sources, terrains (peering bits), custom data "kind", chunk streaming in LevelBuilder, and the WorldRegion/WorldPlacement editor scenes. Use when editing levels, tiles, terrain rules, the tileset, or anything that reads the grid.
---

# TileMaps, terrains and the world

## The pieces

- **TileSet**: `levels/world/world_tiles.tres`, one `TileSetAtlasSource`
  per region (named after it; Paradise also has "Paradise props"), 16 columns
  wide (`TileIndex.ATLAS_COLUMNS`). Each source's texture is a
  `CanvasTexture` with `levels/world/tiles/<region>.png` as diffuse and
  `<region>_n.png` as normal map, so `PointLight2D`s shade the tiles. Terrain
  set 0 (mode *match sides*) and custom data layer 0 `kind` (String:
  `solid`, `ledge`, `hazard`, decor kinds...) are shared by all sources.
- **Region scenes**: `scenes/world/NN_<region>.tscn`, each a `WorldRegion`
  (`@tool`) holding `Tiles/Background`, `Decor`, `Terrain`, `Glow`
  (`TileMapLayer`s, in draw order), and `Lights`, `Rooms`, `Doors`,
  `Placements` holders. `world.tscn` (`WorldScene`) instances all twelve and
  stores grid width, tile sizes, the TileSet, abilities and the dialogue
  file. A region is a band `rows` tall at its `position.y`; its coordinates
  are level pixels from its top-left.
- **LevelBuilder** (`scripts/level_builder.gd`) reads the scenes (or a JSON
  map in tests) into a `WorldMap`, then builds runtime layers, collision
  and placements. It is the only code that turns cells into bodies.
- Helpers: `TileIndex` (sheet rows, atlas coords), `TerrainRules`
  (`TOP = 1, LEFT = 2, RIGHT = 4, BOTTOM = 8` side masks to peering bits),
  `tools/check_terrains.gd` to verify the terrain set.

## TileMapLayer API you will use

```gdscript
layer.set_cell(coords, source_id, atlas_coords, alternative)
layer.erase_cell(coords)
layer.get_cell_source_id(coords)        # -1 when empty
layer.get_cell_atlas_coords(coords)
layer.get_cell_tile_data(coords)        # TileData or null
layer.get_used_cells()                   # Array[Vector2i]
layer.get_used_cells_by_id(source, atlas_coords)
layer.local_to_map(local_pos)  /  layer.map_to_local(coords)  # centre of the cell
layer.set_cells_terrain_connect(cells, terrain_set, terrain)  # paints with peering bits
```

`map_to_local` returns the **centre** of the cell; feet-origin placements
want `map_to_local(c) + Vector2(0, tile / 2)`. Read custom data with
`tile_data.get_custom_data("kind")`. Reading cells in a loop is cheap; the
rendering cost is in how many *quadrants* change, so batch `set_cell` calls
and avoid toggling cells every frame.

Each `TileMapLayer` is one node and one draw batch per quadrant; four
layers per region is the budget. Do not add a layer for a one-off effect,
use a `Sprite2D` or a `PointLight2D`.

## Terrains

Terrain painting uses *match sides* peering bits, so a tile's bits say
which neighbours it joins on the top, left, right and bottom.
`TerrainRules` encodes which atlas tile has which side mask, and
`set_cells_terrain_connect` picks tiles from those bits. When adding a
region's tiles: fill the peering bits in the TileSet editor for the 16
side combinations, set `kind` on the solid ones, then run
`tools/check_terrains.gd` headless to catch missing combinations. A tile
with the wrong bits shows up as a seam; the fix is in the TileSet, not in
code.

## Custom data drives collision

Collision is **not** TileSet physics. `LevelBuilder` walks the `Terrain`
layer, reads `kind` and builds:

- `solid`: horizontal runs merged into one `StaticBody2D` rectangle per run
  (layer `world`), so a 64-wide floor is one shape.
- `ledge`: runs on the `ledge` layer with `one_way_collision`.
- `hazard`: `Hazard` areas over the run.

A new tile that should block, hurt or carry the hero gets a `kind`, and
if the kind is new, a branch in the builder. Decor and glow tiles have no
kind.

## Streaming

The grid is 880,000 cells; it is built in chunks of `CHUNK_TILES = 64`
(1024 level pixels a side). `stream_around(points)` builds chunks within
`STREAM_IN = Vector2(960, 540)` of the hero and camera and frees those
beyond `STREAM_OUT = Vector2(1600, 960)`. `Main` calls it every physics
tick before the hero moves and again after any teleport. Rules that
follow:

- Anything with state (placements, seals, secret walls, lights, pickups)
  is placed once for the whole world, not per chunk; only cells and the
  collision they imply stream.
- Code that needs ground under a point checks `is_loaded(cell)` or calls
  `stream_around([point])` first.
- Tests and tools set `streaming = false` or call `load_all()`.
- Do not keep references to chunk nodes; ask the builder
  (`loaded_chunks()`) each time.

## Editing a level

1. Open the region scene (or `world.tscn` for context). Paint on
   `Tiles/Terrain` with the region's atlas source and terrain; decor on
   `Decor`/`Background`; emissive tiles on `Glow`.
2. Add a `WorldPlacement` under `Placements` and set `type` (`start`,
   `save`, `ability`, `npc`, `item`, `trigger`, `enemy`, `upgrade`,
   `power`, `barrier`) plus the fields that type uses (its doc comment
   lists them). Upgrades are numbered in scene order per region, so keep
   order to keep saves valid.
3. Lights go under `Lights` (`WorldLight`), rooms and doors under theirs;
   `WorldRegion._draw` shows overlays in the editor.
4. Run the map pipeline test and the smoke test (see `testing-headless`);
   the unit test checks placement names resolve to sprites, dialogue and
   profiles.

## TileSet changes

Keep sources 16 columns wide and named after the region; `TileIndex`
derives sheet rows from that. Regenerate atlases with the tools under
`tools/` rather than hand-editing PNGs, keep `_n.png` normal maps in step,
and never reorder sources (cells store source ids). After changing the
TileSet run `godot --headless --path . --import` and the tests.
