@tool
class_name WorldRegion
extends Node2D

## One region (level) of the game's world as a scene: scenes/world/<nn>_<key>.tscn.
##
## A region is a horizontal band of the world grid, `rows` tiles tall and
## the world's width wide, sitting at `position.y` (a multiple of the tile
## size) in the world, so everything in the scene is in level pixels from
## the region's top-left corner. Under it:
##
##   Backdrop/*                               BackdropLayer, the painted parallax set behind
##                                            (and, for a `front` layer, over) the level, back
##                                            to front; drawn in the editor as in the game
##   Tiles/Background, Decor, Terrain, Glow   TileMapLayers painted with the world's TileSet;
##                                            a Terrain tile's custom data "kind" says what it
##                                            does to the hero (see WorldMap.KIND_*)
##   Props/*                                  WorldProp, the pieces of the set (lanterns, pillars,
##                                            ledges, statues) as instances of scenes/props/<key>/
##   Fringes                                  RegionFringes, the tufts, undersides and ivy along
##                                            the terrain's exposed edges, drawn on it in the editor
##   Fx/*                                     RegionParticles, one kind of drifting particle each,
##                                            previewed over the band in the editor
##   Lights/*                                 WorldLight (a PointLight2D)
##   Rooms/*                                  WorldRoom, the rooms of the map page and the fights
##   Doors/*                                  WorldDoor, the openings between rooms and their locks
##   Placements/*                             WorldPlacement: the start, the checkpoints, the
##                                            feathers, NPCs, relics, triggers, enemies and
##                                            bosses, upgrades, power-ups, walls of light
##
## Drag a node to move it; paint the layers with Godot's TileMap tools. The
## editor draws the region's band, and each room, door and placement draws
## itself, so a level reads at a glance. A region without a Backdrop node,
## a Fringes node or Fx nodes falls back to its assets/regions/<key>/
## parallax.json, edges.json or fx.json; tools/scene_art.gd turns those
## files into the nodes.

const LAYER_NAMES: Array[String] = ["Background", "Decor", "Terrain", "Glow"]
const BAND_COLOR := Color(1.0, 1.0, 1.0, 0.25)

## Draw the editor's overlays (the band, the rooms, the doors, the
## placements) outside the editor too: the screenshot tool sets it.
static var show_overlays := false

## The name of the region ("1. Limbo"); its key for art and music is the
## name lowercased without its number (see EdgeDecor.region_key).
@export var region_name := "Region"
@export var subtitle := ""
## How many tiles tall the band is.
@export var rows := 34
## The region's tint, sky, rim light, grade and music (see RegionLook),
## kept at resources/regions/<key>.tres; the defaults when unset.
@export var look: RegionLook


## Whether the overlays are drawn: in the editor, or when a tool asks.
static func overlays_shown() -> bool:
	return Engine.is_editor_hint() or show_overlays


## The region scene `node` is in, or null when it is in none.
static func of(node: Node) -> WorldRegion:
	var parent := node.get_parent() if node else null
	while parent and not parent is WorldRegion:
		parent = parent.get_parent()
	return parent as WorldRegion


func _ready() -> void:
	if WorldRegion.overlays_shown():
		queue_redraw()


func _draw() -> void:
	if not WorldRegion.overlays_shown():
		return
	var world := get_parent() as WorldScene
	var tile := world.tile if world else 16
	var width := world.width if world else 480
	draw_rect(Rect2(Vector2.ZERO, Vector2(width, rows) * tile), BAND_COLOR, false, 2.0)
	draw_string(
		ThemeDB.fallback_font, Vector2(8.0, -8.0), region_name, 0, -1, 24, Color(1, 1, 1, 0.9)
	)


## The tile layer of that name, or null.
func layer(layer_name: String) -> TileMapLayer:
	return get_node_or_null("Tiles/" + layer_name) as TileMapLayer


## The parallax layers of the region's backdrop, back to front.
func backdrop_layers() -> Array[BackdropLayer]:
	var found: Array[BackdropLayer] = []
	for node in _children_of("Backdrop"):
		if node is BackdropLayer:
			found.append(node)
	return found


## The pieces of the set, in tree order.
func props() -> Array[WorldProp]:
	var found: Array[WorldProp] = []
	for node in _children_of("Props"):
		if node is WorldProp:
			found.append(node)
	return found


## The fringes along the terrain's edges, or null for a region without.
func fringes() -> RegionFringes:
	return get_node_or_null("Fringes") as RegionFringes


## The kinds of particle in the region's air, in tree order.
func particles() -> Array[RegionParticles]:
	var found: Array[RegionParticles] = []
	for node in _children_of("Fx"):
		if node is RegionParticles:
			found.append(node)
	return found


func lights() -> Array[WorldLight]:
	var found: Array[WorldLight] = []
	for node in _children_of("Lights"):
		if node is WorldLight:
			found.append(node)
	return found


func rooms() -> Array[WorldRoom]:
	var found: Array[WorldRoom] = []
	for node in _children_of("Rooms"):
		if node is WorldRoom:
			found.append(node)
	return found


func doors() -> Array[WorldDoor]:
	var found: Array[WorldDoor] = []
	for node in _children_of("Doors"):
		if node is WorldDoor:
			found.append(node)
	return found


func placements() -> Array[WorldPlacement]:
	var found: Array[WorldPlacement] = []
	for node in _children_of("Placements"):
		if node is WorldPlacement:
			found.append(node)
	return found


func _children_of(holder: String) -> Array[Node]:
	var node := get_node_or_null(holder)
	return node.get_children() if node else []
