---
name: tilemaps-and-rule-tiles
description: How regions are built from Tilemaps in this project: the Grid with Background/Decor/Terrain/Glow Tilemap layers, the tile palette and Rule Tiles (2D Tilemap Extras) that replace Godot terrains, Tilemap Collider 2D with Composite Collider 2D, custom tile data, and how region scenes are laid out. Use when editing a region, adding tiles, or working on world streaming.
---

# Tilemaps and Rule Tiles

The twelve regions are generated, not painted: `tools/unity/
import_godot_regions.py` (run with `python3 -I`) reads each Godot region
scene (`scenes/world/<nn>_<key>.tscn`, four TileMapLayers over one TileSet)
and `scenes/world/world.tscn`, and writes `Assets/Scenes/Region_<key>.unity`
plus the region's data. Re-run it after the Godot scene changes; never
hand-edit a `Region_*.unity`, the next run rewrites it.

## The tiles

The 200k cells are not painted into the scenes (that would be millions of
YAML lines). Each region has a `RegionTileSet` asset
(`Assets/Data/Regions/Tiles/<key>_tiles.asset`: the 256 sprites of its 64 px
atlas `Assets/Art/Tiles/<key>.png`, index = row * 16 + column, the materials)
and a `TextAsset` of cells (`<key>_cells.txt`, format in `RegionTileData`:
base64 int16 triples x, y, tile per layer, plus the `TileKind` of each tile
index, from the Godot TileSet's custom data "kind").

`RegionTiles` on the region's `Tiles` object builds the Grid and the four
Tilemaps in `Awake` (a tile is one unit: 64 px art at 64 PPU):

| Tilemap | sortingOrder | Material | Collider |
|---|---|---|---|
| Background | -300 | URP Sprite-Lit-Default | none |
| Decor | -200 | Sprite-Lit-Default | none |
| Terrain | -100 | Sprite-Lit-Default | TilemapCollider2D + CompositeCollider2D, layer World, for Solid and Hidden tiles |
| Glow | -40 | Sprite-Unlit-Default | none |

Godot cell (x, y), y down from the region's top row, is Unity cell
(x, -y - 1) under the region root. Ledge tiles become one-way strips
(BoxCollider2D runs on a `PlatformEffector2D`, layer Ledge, 4 px tall) and
Hazard tiles trigger runs on layer Hazard with `HazardArea`, as
`scripts/level_builder.gd` made them. In the editor, `Descender > World >
Build region tiles` (or the component's context menu) previews the result;
the built objects carry `HideFlags.DontSave`.

Rule Tiles and the Tile Palette are for new, hand-painted content only; a
region's tiles live in the Godot scene and come through the importer.

## Region layout

```
Region_<key>             (RegionRoot: key, look, topRow, rows; at (0, -topRow) in world units)
  Tiles                  (RegionTiles: the tile set; RegionFringes: tufts, undersides, ivy on exposed edges)
  Backdrop/*             (BackdropLayer: one SpriteRenderer per parallax layer, repeated at runtime)
  Fx/*                   (RegionParticles: a ParticleSystem following the camera)
  Lights/*               (WorldLight: colour, energy, radius in level pixels; makes its Light2D in Awake)
  Rooms/*                (WorldRoom: id, kind, size; a trigger box on layer Trigger clamps the camera)
  Doors/*                (WorldDoor: id, rooms, dir, lock; a lock seals its cells with light)
  Placements/*           (WorldPlacement: type, character, dialogue...; LightBarrier for a wall of light)
  Props/*                (prefab instances of Assets/Prefabs/Props/<key>/*.prefab, WorldProp)
```

Everything under the root is in level coordinates from the region's own
corner (Godot pixels / 16, Y flipped). The whole world is one grid: regions
are horizontal bands stacked top to bottom (`WorldAtlas`,
`Assets/Data/World.asset`), so the hero walks or falls from one into the next.

All renderers sit on the Default sorting layer and sort by `sortingOrder`:
backdrop -1000 and up, tilemaps -300 to -40, fringes -90, props -50, the
hero at 0, particles 60, front haze 500.

## Streaming

Regions are additive scenes, every one in the build's scene list.
`WorldStreamer` (in `Main.unity`, `atlas` set to `Assets/Data/World.asset`)
keeps the hero's band and its neighbours above and below loaded, unloads the
rest, owns the one global Light2D (tinted with the current region's
`RegionLook.ambient`, eased over 2.5 s on a crossing) and shows only the
current region's backdrop. `StartNewGame(hero)`, `ContinueAt(hero,
checkpointIndex)` and `EnterAt(hero, point)` place the hero;
`RegionChanged` fires on a crossing. Checkpoints are numbered as the saves
number them: top to bottom, left to right over the whole world
(`WorldAtlas.checkpoints`).

## Placements

`WorldPlacement` spawns the prefab the `PlacementCatalog`
(`Assets/Data/Placements.asset`, filled by `Descender > Setup > Placement
catalog` from `Assets/Prefabs/{Pickups,Enemies,Npcs}`) gives for its type
and character, and hands itself to the prefab's `IPlacementReceiver`.
`WorldPlacement.IsGone` is the hook the kills tracker sets.
