@tool
class_name WorldScene
extends Node2D

## The game's own world as Godot scenes: scenes/world/world.tscn, whose
## children are the regions (WorldRegion), one scene each under
## scenes/world/, stacked top to bottom. Open a region's scene to edit that
## level on its own, or this one to see them all. The game reads the scenes
## into a WorldMap at boot (see SceneWorld), the same thing a map editor
## export gives it, so everything downstream (LevelBuilder, the map page,
## the saves) is the same for both. tools/migrate_world.gd made the scenes
## from the last export; after that the scenes are the world.
##
## What lives here is what is not any one region's: the grid's width, the
## tile size and the art scale, the TileSet the regions paint with (its
## tiles carry the terrain kind as custom data "kind"), the abilities in
## the order the doors' locks and the ability markers refer to, the
## dialogue file and how dark the world gets towards its bottom.

## Shown on the title line when the world starts.
@export var world_name := "World 1"
## The grid's width in tiles; the height is the regions' rows together.
@export var width := 480
## Level pixels a tile, and pixels a tile in the sheets (the art scale).
@export var tile := 16
@export var art_tile := 64
## How many tiles wide the tile sheets are: tile id = row * columns + column.
@export var columns := 64
## The TileSet the regions' layers paint with (levels/world/world_tiles.tres).
@export var tile_set: TileSet
## The abilities in the map tool's order (see Player.Ability).
@export var abilities: PackedStringArray = []
## The dialogue file (see DialogueData), or "" for a world without one.
@export_file("*.json") var dialogue := ""
## How much the world darkens from its top to its bottom, 0 to 1 (see
## RegionLook.descent_factor).
@export_range(0.0, 1.0) var descent_dark := 0.0


## The regions, top to bottom.
func regions() -> Array[WorldRegion]:
	var found: Array[WorldRegion] = []
	for child in get_children():
		if child is WorldRegion:
			found.append(child)
	found.sort_custom(
		func(a: WorldRegion, b: WorldRegion) -> bool: return a.position.y < b.position.y
	)
	return found
