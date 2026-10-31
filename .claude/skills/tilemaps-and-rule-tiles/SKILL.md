---
name: tilemaps-and-rule-tiles
description: How regions are built from Tilemaps in this project: the Grid with Background/Decor/Terrain/Glow Tilemap layers, the tile palette and Rule Tiles (2D Tilemap Extras) that replace Godot terrains, Tilemap Collider 2D with Composite Collider 2D, custom tile data, and how region scenes are laid out. Use when editing a region, adding tiles, or working on world streaming.
---

# Tilemaps and Rule Tiles

Each Godot region scene (`levels/world/region_*.tscn`, four TileMapLayers
over one TileSet) becomes `Assets/Scenes/Regions/<name>.unity` with one
`Grid` (cell size 1 x 1 unit, a 16 px tile) and these child Tilemaps, each
a `Tilemap` + `TilemapRenderer` on the matching sorting layer:

| Tilemap | Sorting layer | Material | Collider |
|---|---|---|---|
| Background | Background | Sprite Lit | none |
| Decor | Decor | Sprite Lit | none |
| Terrain | Terrain | Sprite Lit | TilemapCollider2D + CompositeCollider2D, layer World |
| Glow | Glow | Sprite Glow | none |

`TilemapRenderer.Mode: Chunk` for all four (the default; `Individual` only
if a layer must sort against sprites by Y, which none do). `Sort Order:
Top Left`.

## Tiles

Tile art is `Assets/Art/Tiles/Sheets/*.png` (the Godot atlases), sliced to
16 x 16 by the import script, PPU 16, point filter, with the `_n.png`
attached as `_NormalMap`. Tile assets live in `Assets/Art/Tiles/Tiles/`:

- Plain `Tile` for one-off decor.
- **Rule Tile** (`Create > 2D > Tiles > Rule Tile`) for each Godot terrain
  (stone, brick, moss, ice...). Rules go from most to least specific:
  corners and single-tile islands first, then edges, then the inner fill;
  use the `Any`/`This`/`Not This` neighbour marks; `Output: Random` for the
  fill so large walls do not repeat.
- **Animated Tile** for lava and water surfaces (frames in order, speed
  as in Godot's animation frames_per_second).
- **Rule Override Tile** when a region has a recoloured copy of a terrain:
  override the sprites, keep the rules.

Custom data on tiles (hazard, one-way, slippery) is a `DescenderTile`
subclass of `RuleTile` with `[SerializeField]` fields; gameplay reads it
with `tilemap.GetTile<DescenderTile>(cell)`. Do not encode it in tile
names.

## Colliders

The Terrain tilemap's `TilemapCollider2D` has `Use Composite` on and a
`CompositeCollider2D` with `Geometry Type: Polygons`, `Generation Type:
Synchronous`, on a `Rigidbody2D` Static. Tiles that should not collide
set `Collider Type: None` in the sprite's tile asset. Ledges (one-way) are
their own `Ledge` tilemap with a `PlatformEffector2D` (`Use One Way`,
`Surface Arc 170`), layer Ledge. Hazards (spikes) are trigger colliders
on `Hazard`, placed as prefabs, not tiles.

## Painting

`Window > 2D > Tile Palette`; palettes under `Assets/Art/Tiles/Palettes/`,
one per sheet. Pick the active Tilemap in the palette's dropdown before
painting; a tile painted on the wrong layer is the usual bug. `Grid
Selection` + `Edit > Delete` clears a block.

## Region layout

```
Region_Hollow            (RegionRoot: RegionLook asset, bounds, music)
  Grid                   (the four Tilemaps + Ledge)
  Lights                 (WorldLight children, Light2D Point)
  Props                  (WorldProp prefab instances; see editor-tooling)
  Rooms                  (WorldRoom: BoxCollider2D trigger bounds for the camera)
  Doors                  (WorldDoor: trigger, target region + door id)
  Placements             (WorldPlacement: enemy/pickup spawns by id)
  Backdrop               (BackdropLayer, RegionFringes, RegionParticles)
```

`WorldRoom` bounds are what the camera clamps to; every walkable area is
inside exactly one room. Doors link `region:door` ids; `WorldDoor`
validation (EditMode test `WorldLinksTests`) checks every target exists.

## Streaming

Regions are additive scenes. `WorldStreamer` in `Main.unity` loads the
hero's region and its door neighbours, unloads the rest, and positions
the hero at the door's spawn point. The Godot chunk culling is replaced by
Unity's own renderer culling plus `WorldLight` distance disabling; do not
hide tilemap chunks by hand.

## Converting a Godot region

`tools/unity/import_godot_regions.py` (to be written in Phase 3) reads the
`.tscn` `tile_map_data` and writes a `.unity` with the four tilemaps and
the light/prop/room/door nodes. Until it exists, regions are painted by
hand from the Godot scene as the reference, starting with `hollow`.
