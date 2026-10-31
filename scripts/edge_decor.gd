class_name EdgeDecor
extends Node2D

## Pieces of art along the exposed edges of the solid terrain: tufts on the
## tops, hanging growth under the undersides, moss down the sides. They are
## what lets a grid of tiles read as a painted shape, since they overhang
## the cells they belong to.
##
## A region of the game's own world keeps its set in its scene, as a
## RegionFringes node, which SceneWorld reads into the region's `edges` spec
## (the same fields, with the textures in place of file names). A region of
## a map without a scene opts in with godot_assets/regions/<key>/edges.json,
## where key is the region name lowercased without its number
## ("1. Limbo" -> "limbo"):
##
##   {
##     "top": ["tuft_a.png", "tuft_b.png"],   anchored by their bottom centre on the cell's top edge
##     "bottom": ["vine_a.png"],              anchored by their top centre on the cell's bottom edge
##     "left": ["moss_l.png"],                anchored by their right centre on the cell's left edge
##     "right": ["moss_r.png"],               anchored by their left centre on the cell's right edge
##     "chance": 1.0,                         share of exposed cells that get a piece (per side with
##                                            "top_chance" and so on)
##     "art_tile": 64                         pixels per tile the PNGs are drawn at (16 when unset)
##   }
##
## The PNGs are drawn scaled onto the 16-unit grid by tile / art_tile, so a
## set serves maps of any art scale. Pieces are drawn in chunks of
## CHUNK x CHUNK cells, one canvas item each, so only the chunks in view
## cost anything; the variant of a cell is fixed by its position. The level
## streams them with its tiles: prepare once, then build_cells per chunk.

const REGIONS_DIR := "res://godot_assets/regions"
const CHUNK := 32
const SIDES: Array[String] = ["top", "bottom", "left", "right"]

## Region key -> {side -> Array[Texture2D], side_chance -> float}, or an
## empty dictionary for a region without edge art.
var _sets := {}

## Which cells of the map are solid (1) or not (0), row by row, from prepare.
var _solid := PackedByteArray()
var _width := 0
var _height := 0


## Builds the edge pieces for the whole map at once.
func build(map: WorldMap) -> void:
	prepare(map)
	build_cells(map, Rect2i(0, 0, map.width, map.height))


## Reads the map's solid cells and clears the pieces built so far, ready for
## build_cells to build the pieces of any part of the grid.
func prepare(map: WorldMap) -> void:
	for child in get_children():
		child.queue_free()
	_width = map.width
	_height = map.height
	var terrain: PackedInt32Array = map.cells(WorldMap.LAYER_TERRAIN)
	_solid = PackedByteArray()
	_solid.resize(terrain.size())
	for i in terrain.size():
		_solid[i] = (
			1 if terrain[i] >= 0 and int(map.kinds.get(terrain[i], 0)) == WorldMap.KIND_SOLID else 0
		)


## Builds the pieces of the solid cells within `cells` (a chunk, see
## LevelBuilder.stream_around) and returns the chunk nodes made, so the
## caller can free them again. The pieces of a cell overhang it, so a
## chunk's pieces may reach a little outside its cells.
func build_cells(map: WorldMap, cells: Rect2i) -> Array[Node]:
	var made: Array[Node] = []
	if _solid.is_empty() or not cells.has_area():
		return made
	var tile := map.tile
	# Chunk key -> the pieces in it.
	var chunks := {}
	for region in map.regions:
		var edge_set := _set_for(region)
		if edge_set.is_empty():
			continue
		var y0 := int(floor(float(region.get("y0", 0)) / tile))
		var y1 := int(ceil(float(region.get("y1", 0)) / tile))
		var band := cells.intersection(Rect2i(0, y0, _width, y1 - y0))
		for piece in pieces_in(_solid, _width, _height, band, edge_set, tile):
			var at: Vector2 = piece[1]
			var key := Vector2i(int(at.x) / (CHUNK * tile), int(at.y) / (CHUNK * tile))
			if not chunks.has(key):
				chunks[key] = []
			chunks[key].append(piece)
	for key in chunks:
		var chunk := EdgeChunk.new()
		chunk.pieces = chunks[key]
		chunk.position = Vector2(key) * CHUNK * tile
		add_child(chunk)
		made.append(chunk)
	return made


## The pieces along the exposed sides of the solid cells within `cells`, as
## [texture, top-left in level pixels, scale] each: `solid` is the grid,
## row by row, `edge_set` a region's set (see _set_for) and `tile` the
## level's tile size. Which piece a cell gets, if any, is fixed by its
## position, so the editor and the game agree.
static func pieces_in(
	solid: PackedByteArray, width: int, height: int, cells: Rect2i, edge_set: Dictionary, tile: int
) -> Array:
	var pieces: Array = []
	if not cells.has_area():
		return pieces
	var scale_factor := float(tile) / float(edge_set.get("art_tile", tile))
	for y in range(maxi(cells.position.y, 0), mini(cells.end.y, height)):
		for x in range(maxi(cells.position.x, 0), mini(cells.end.x, width)):
			if solid[y * width + x] == 0:
				continue
			for side in SIDES:
				var options: Array = edge_set.get(side, [])
				if options.is_empty() or not _exposed(solid, width, height, x, y, side):
					continue
				var roll := _hash(x, y, side.unicode_at(0))
				if roll % 1000 >= int(float(edge_set.get(side + "_chance", 1.0)) * 1000.0):
					continue
				var texture: Texture2D = options[(roll / 1000) % options.size()]
				pieces.append(
					[
						texture,
						_anchor(x, y, side, tile, texture.get_size() * scale_factor),
						scale_factor
					]
				)
	return pieces


## Whether the solid cell (x, y) has open air on the given side.
static func _exposed(
	solid: PackedByteArray, width: int, height: int, x: int, y: int, side: String
) -> bool:
	var nx := x
	var ny := y
	match side:
		"top":
			ny -= 1
		"bottom":
			ny += 1
		"left":
			nx -= 1
		"right":
			nx += 1
	if nx < 0 or ny < 0 or nx >= width or ny >= height:
		return false
	return solid[ny * width + nx] == 0


## Where the piece's top-left corner goes, in level pixels, for a piece of
## `size` (already scaled) on the given side of cell (x, y).
static func _anchor(x: int, y: int, side: String, tile: int, size: Vector2) -> Vector2:
	var left := float(x * tile)
	var top := float(y * tile)
	var half := tile / 2.0
	match side:
		"top":
			return Vector2(left + half - size.x / 2.0, top - size.y)
		"bottom":
			return Vector2(left + half - size.x / 2.0, top + tile)
		"left":
			return Vector2(left - size.x, top + half - size.y / 2.0)
		_:
			return Vector2(left + tile, top + half - size.y / 2.0)


static func _hash(x: int, y: int, salt: int) -> int:
	var h := (x * 374761393 + y * 668265263 + salt * 2246822519) & 0x7fffffff
	h = ((h ^ (h >> 13)) * 1274126177) & 0x7fffffff
	return h ^ (h >> 16)


## The edge set of a region: its scene's (the `edges` spec), else its
## edges.json loaded once; empty when the region has none.
func _set_for(region: Dictionary) -> Dictionary:
	var own: Variant = region.get("edges")
	if own is Dictionary:
		return own
	var key := region_key(str(region.get("name", "")))
	if _sets.has(key):
		return _sets[key]
	var edge_set := {}
	var dir := "%s/%s" % [REGIONS_DIR, key]
	var text := FileAccess.get_file_as_string(dir + "/edges.json")
	var spec: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if spec is Dictionary:
		edge_set["art_tile"] = int(spec.get("art_tile", 16))
		var chance := float(spec.get("chance", 1.0))
		for side in SIDES:
			var textures: Array = []
			for file in spec.get(side, []):
				var texture := load("%s/%s" % [dir, str(file)]) as Texture2D
				if texture:
					textures.append(texture)
			if not textures.is_empty():
				edge_set[side] = textures
			edge_set[side + "_chance"] = float(spec.get(side + "_chance", chance))
	_sets[key] = edge_set
	return edge_set


## The folder name of a region: lowercased, without the circle's number.
static func region_key(region_name: String) -> String:
	var key := region_name.to_lower().strip_edges()
	var dot := key.find(". ")
	if dot > 0 and key.substr(0, dot).is_valid_int():
		key = key.substr(dot + 2)
	return key.replace(" ", "_")


## One chunk of pieces, drawn in a single canvas item.
class EdgeChunk:
	extends Node2D
	## [texture, top-left in level pixels, scale] per piece.
	var pieces: Array = []

	func _ready() -> void:
		texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST

	func _draw() -> void:
		for piece in pieces:
			var texture: Texture2D = piece[0]
			var at: Vector2 = piece[1] - position
			draw_texture_rect(texture, Rect2(at, texture.get_size() * float(piece[2])), false)
