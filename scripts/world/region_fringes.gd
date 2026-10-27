@tool
class_name RegionFringes
extends Node2D

## The pieces of art along the exposed edges of a region's solid terrain,
## tufts on the tops, growth under the undersides, ivy down the sides, as a
## node of the region's scene (`Fringes`, see WorldRegion) with the fields
## of an edges.json as properties: the PNGs per side and the share of
## exposed cells that get one. The editor draws them on `Tiles/Terrain` the
## way the game does (EdgeDecor picks the same piece for the same cell) and
## redraws as the terrain is painted, so the fringes are tuned in the
## inspector with the result in view.
##
## The game never draws this node: SceneWorld reads it into the region's
## `edges` spec and EdgeDecor builds the pieces from that, in chunks, as the
## level streams. A region without a Fringes node falls back to its
## assets/regions/<key>/edges.json; tools/scene_art.gd turns that file
## into the node.

## Pieces standing on the top edges, anchored by their bottom centre.
@export var top: Array[Texture2D] = []:
	set(value):
		top = value
		queue_redraw()
## Pieces hanging from the undersides, anchored by their top centre.
@export var bottom: Array[Texture2D] = []:
	set(value):
		bottom = value
		queue_redraw()
## Pieces on the left sides, anchored by their right centre.
@export var left: Array[Texture2D] = []:
	set(value):
		left = value
		queue_redraw()
## Pieces on the right sides, anchored by their left centre.
@export var right: Array[Texture2D] = []:
	set(value):
		right = value
		queue_redraw()
## The share of exposed cells on each side that get a piece.
@export_range(0.0, 1.0) var top_chance := 1.0:
	set(value):
		top_chance = value
		queue_redraw()
@export_range(0.0, 1.0) var bottom_chance := 1.0:
	set(value):
		bottom_chance = value
		queue_redraw()
@export_range(0.0, 1.0) var left_chance := 1.0:
	set(value):
		left_chance = value
		queue_redraw()
@export_range(0.0, 1.0) var right_chance := 1.0:
	set(value):
		right_chance = value
		queue_redraw()
## Pixels a tile the PNGs were drawn at: 16 for pixel art, 64 for painted.
@export var art_tile := 64:
	set(value):
		art_tile = maxi(value, 1)
		queue_redraw()


func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	if not Engine.is_editor_hint():
		return
	var terrain := _terrain()
	if terrain and not terrain.changed.is_connected(queue_redraw):
		terrain.changed.connect(queue_redraw)
	queue_redraw()


## The fringes as an edges.json spec with the textures themselves in place
## of file names (see EdgeDecor), what SceneWorld hands the level.
func to_spec() -> Dictionary:
	var spec := {"art_tile": art_tile}
	var sides := {"top": top, "bottom": bottom, "left": left, "right": right}
	var chances := {
		"top": top_chance, "bottom": bottom_chance, "left": left_chance, "right": right_chance
	}
	for side in EdgeDecor.SIDES:
		var textures: Array = []
		for texture in sides[side]:
			if texture:
				textures.append(texture)
		if not textures.is_empty():
			spec[side] = textures
		spec[side + "_chance"] = chances[side]
	return spec


## A node from an edges.json spec whose PNGs are in `dir` (a resource
## folder), or whose sides already hold textures (a to_spec result).
static func from_spec(spec: Dictionary, dir := "") -> RegionFringes:
	var fringes := RegionFringes.new()
	fringes.art_tile = int(spec.get("art_tile", 16))
	var chance := float(spec.get("chance", 1.0))
	var chances := {}
	for side in EdgeDecor.SIDES:
		var textures: Array[Texture2D] = []
		for entry in spec.get(side, []):
			var texture: Texture2D = null
			if entry is Texture2D:
				texture = entry
			elif entry is String and not dir.is_empty():
				texture = load("%s/%s" % [dir, entry]) as Texture2D
			if texture:
				textures.append(texture)
		fringes.set(side, textures)
		chances[side] = float(spec.get(side + "_chance", chance))
	fringes.top_chance = chances["top"]
	fringes.bottom_chance = chances["bottom"]
	fringes.left_chance = chances["left"]
	fringes.right_chance = chances["right"]
	return fringes


## The pieces the terrain gets, as EdgeDecor lays them out: [texture,
## top-left in the region's pixels, scale] each.
func pieces() -> Array:
	var terrain := _terrain()
	var region := WorldRegion.of(self)
	if terrain == null or terrain.tile_set == null or region == null:
		return []
	var world := region.get_parent() as WorldScene
	var tile := world.tile if world else 16
	var width := world.width if world else 480
	var height := region.rows
	var solid := PackedByteArray()
	solid.resize(width * height)
	for cell in terrain.get_used_cells():
		if cell.x < 0 or cell.y < 0 or cell.x >= width or cell.y >= height:
			continue
		var data := terrain.get_cell_tile_data(cell)
		if data and int(data.get_custom_data("kind")) == WorldMap.KIND_SOLID:
			solid[cell.y * width + cell.x] = 1
	return EdgeDecor.pieces_in(solid, width, height, Rect2i(0, 0, width, height), to_spec(), tile)


func _draw() -> void:
	for piece in pieces():
		var texture: Texture2D = piece[0]
		var at: Vector2 = piece[1] - position
		draw_texture_rect(texture, Rect2(at, texture.get_size() * float(piece[2])), false)


func _terrain() -> TileMapLayer:
	var region := WorldRegion.of(self)
	return region.layer("Terrain") if region else null
