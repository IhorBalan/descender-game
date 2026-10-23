class_name WorldMap
extends RefCounted

## A world exported by the Lit Pixel map tool (levels/world/world.json).
##
## The export is one big tile grid in four layers (Background, Decor,
## Terrain, Glow) drawn from three tile sheets next to the JSON: the diffuse
## sheet, its normal map and the emissive glow sheet. Terrain tiles have a
## kind that says what they do to the hero; the other layers are only
## drawn. Around the grid the export describes regions (horizontal bands
## with an ambient colour), rooms, doors, markers (start, saves, bosses,
## abilities and so on) and point lights. LevelBuilder turns the grid into
## TileMapLayers and collision and places the markers; the rest is kept
## here for the game code.

## Terrain kinds, from the export's tile_kinds table.
const KIND_NONE := 0
const KIND_SOLID := 1
const KIND_LEDGE := 2
const KIND_HAZARD := 3
## Collision without a tile of its own, under solid decorations.
const KIND_HIDDEN := 4

const LAYER_BACKGROUND := "Background"
const LAYER_DECOR := "Decor"
const LAYER_TERRAIN := "Terrain"
const LAYER_GLOW := "Glow"

const FORMAT := "lit-pixel-world-godot"
const SHEET_DIFFUSE := "world_tiles.png"
const SHEET_NORMAL := "world_tiles_n.png"
const SHEET_GLOW := "world_tiles_e.png"

## The file the map was read from.
var path := ""
## Size in tiles, and the tile size in level pixels (the world unit).
var width := 0
var height := 0
var tile := 16
## Pixels per tile in the sheets: the art scale (16 for pixel art, 64 for
## painted tiles). The layers are drawn scaled by tile / art_tile.
var art_tile := 16
## How many tiles wide the sheets are.
var columns := 64
## Layer name -> one tile id per cell, row by row, -1 for empty.
var layers: Dictionary = {}
## The order the layers are drawn in, back to front.
var layer_names: PackedStringArray = []
## Tile id -> kind, for the terrain tiles that do something.
var kinds: Dictionary = {}
## Every tile id any layer uses.
var used_tiles: PackedInt32Array = []
## The horizontal bands of the world, top to bottom: name, subtitle,
## ambient (a colour) and the y range in pixels (y0, y1).
var regions: Array[Dictionary] = []
var rooms: Array[Dictionary] = []
var doors: Array[Dictionary] = []
## The ability names the doors' locks and the ability markers refer to,
## in the tool's order.
var abilities: PackedStringArray = []
## Points of interest: type, label, the cell (x, y) and the pixel position
## of the bottom of that cell (px, py), where a thing standing there has
## its feet.
var markers: Array[Dictionary] = []
## Point lights: x, y, z (height), radius, color, energy, flicker.
var lights: Array[Dictionary] = []
## Free-placed decorations: id, name, x, y (pixels of the top-left corner),
## columns, rows (tiles), tiles (atlas ids row by row), flip, solid, glow,
## and hit, the box of the art's opaque pixels ([x, y, w, h] from the
## top-left, unmirrored) that a solid one collides with.
var sprites: Array[Dictionary] = []
## How much the world darkens from its top to its bottom (0 to 1, see
## RegionLook.descent_factor): the export's "descent": {"dark": ...}.
var descent_dark := 0.0


## Reads the export at `file_path`; null, with an error pushed, when the
## file is missing or not a world export.
static func read(file_path: String) -> WorldMap:
	if not FileAccess.file_exists(file_path):
		push_error("WorldMap: no file at %s" % file_path)
		return null
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(file_path))
	if not parsed is Dictionary or str(parsed.get("format", "")) != FORMAT:
		push_error("WorldMap: %s is not a %s export" % [file_path, FORMAT])
		return null
	var map := WorldMap.new()
	map.path = file_path
	map._parse(parsed)
	if map.width <= 0 or map.height <= 0 or map.layers.is_empty():
		push_error("WorldMap: %s has no tiles" % file_path)
		return null
	return map


func _parse(data: Dictionary) -> void:
	width = int(data.get("width", 0))
	height = int(data.get("height", 0))
	tile = int(data.get("tile", 16))
	art_tile = int(data.get("art_tile", tile))
	columns = int(data.get("columns", 64))
	var descent: Variant = data.get("descent", {})
	if descent is Dictionary:
		descent_dark = clampf(float(descent.get("dark", 0.0)), 0.0, 1.0)
	for layer in data.get("layers", []):
		if not layer is Dictionary:
			continue
		var name := str(layer.get("name", ""))
		var cells := PackedInt32Array(layer.get("tiles", []))
		if name.is_empty() or cells.size() != width * height:
			push_warning(
				"WorldMap: layer '%s' skipped, it is not %d cells" % [name, width * height]
			)
			continue
		layers[name] = cells
		layer_names.append(name)
	for key in data.get("tile_kinds", {}):
		kinds[int(key)] = int(data["tile_kinds"][key])
	used_tiles = PackedInt32Array(data.get("used_tiles", []))
	for region in data.get("regions", []):
		if region is Dictionary:
			regions.append(region)
	for room in data.get("rooms", []):
		if room is Dictionary:
			rooms.append(room)
	for door in data.get("doors", []):
		if door is Dictionary:
			doors.append(door)
	abilities = PackedStringArray(data.get("abilities", []))
	for marker in data.get("markers", []):
		if marker is Dictionary:
			markers.append(marker)
	for light in data.get("lights", []):
		if light is Dictionary:
			lights.append(light)
	for sprite in data.get("sprites", []):
		if sprite is Dictionary:
			sprites.append(sprite)


## The world's size in pixels.
func size_px() -> Vector2:
	return Vector2(width * tile, height * tile)


## The folder the tile sheets live in.
func dir() -> String:
	return path.get_base_dir() + "/"


## What a tile layer is scaled by to put art_tile pixels in a tile-wide cell.
func art_scale() -> float:
	return float(tile) / float(art_tile)


## How the sheets are sampled: nearest for pixel art and for painted art that
## lands pixel for pixel; linear for art drawn finer than the screen shows
## it (128-pixel tiles), which nearest would shimmer. The atlas sources pad
## their tiles, so linear does not bleed between them.
func texture_filter() -> CanvasItem.TextureFilter:
	return CanvasItem.TEXTURE_FILTER_LINEAR if art_tile > 64 else CanvasItem.TEXTURE_FILTER_NEAREST


## One of the tile sheets next to the export, or null when it is missing.
## Run from the project, the sheet is read from the PNG itself rather than
## from Godot's imported copy: the live map sync (see LiveMap) rewrites the
## sheets while the game is starting, and only the editor re-imports them.
## Exported builds carry imported textures only, so they load those.
func _sheet(sheet_path: String) -> Texture2D:
	if OS.has_feature("editor") or not sheet_path.begins_with("res://"):
		if not FileAccess.file_exists(sheet_path):
			return null
		# The OS path: given res://, Godot warns at every start that the
		# file will not be there in an export, which _sheet already handles.
		var image := Image.load_from_file(ProjectSettings.globalize_path(sheet_path))
		return ImageTexture.create_from_image(image) if image else null
	return load(sheet_path) if ResourceLoader.exists(sheet_path) else null


## The cells of a layer, or an empty array for a layer the export lacks.
func cells(layer_name: String) -> PackedInt32Array:
	return layers.get(layer_name, PackedInt32Array())


## The tile id of a terrain cell, -1 when empty or outside the grid.
func terrain_at(x: int, y: int) -> int:
	if x < 0 or y < 0 or x >= width or y >= height:
		return -1
	var terrain: PackedInt32Array = layers.get(LAYER_TERRAIN, PackedInt32Array())
	return terrain[y * width + x] if terrain.size() > 0 else -1


## What the terrain cell does: one of the KIND constants.
func kind_at(x: int, y: int) -> int:
	var id := terrain_at(x, y)
	return int(kinds.get(id, KIND_NONE)) if id >= 0 else KIND_NONE


## Where a tile id sits in the sheets.
func atlas_coords(id: int) -> Vector2i:
	return Vector2i(id % columns, id / columns)


## The region the pixel row `y` falls in; an empty dictionary when the
## export has no regions.
func region_at(y: float) -> Dictionary:
	for region in regions:
		if y >= float(region.get("y0", 0)) and y < float(region.get("y1", 0)):
			return region
	if regions.is_empty():
		return {}
	return regions[0] if y < float(regions[0].get("y0", 0)) else regions[regions.size() - 1]


## The markers of one type, in export order.
func markers_of(type: String) -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	for marker in markers:
		if str(marker.get("type", "")) == type:
			found.append(marker)
	return found


## The pixel position of a marker: the bottom centre of its cell.
func marker_position(marker: Dictionary) -> Vector2:
	if marker.has("px") and marker.has("py"):
		return Vector2(float(marker["px"]), float(marker["py"]))
	return Vector2(
		int(marker.get("x", 0)) * tile + tile / 2.0, (int(marker.get("y", 0)) + 1) * tile
	)


## A TileSet with the diffuse sheet (lit by its normal map) as source 0 and
## the glow sheet as source 1, with a tile for every id the layers use.
## Collision is not part of it: LevelBuilder merges runs of cells into
## shapes instead, which is far fewer bodies than one polygon per tile.
func build_tile_set() -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_size = Vector2i(art_tile, art_tile)
	var lit := CanvasTexture.new()
	lit.diffuse_texture = _sheet(dir() + SHEET_DIFFUSE)
	lit.normal_texture = _sheet(dir() + SHEET_NORMAL)
	lit.texture_filter = texture_filter()
	var diffuse := TileSetAtlasSource.new()
	diffuse.texture = lit
	diffuse.texture_region_size = Vector2i(art_tile, art_tile)
	tile_set.add_source(diffuse, 0)
	var glow := TileSetAtlasSource.new()
	var glow_texture := _sheet(dir() + SHEET_GLOW)
	glow.texture = glow_texture if glow_texture else lit
	glow.texture_region_size = Vector2i(art_tile, art_tile)
	tile_set.add_source(glow, 1)
	for id in _all_tile_ids():
		var coords := atlas_coords(id)
		if diffuse.has_room_for_tile(coords, Vector2i.ONE, 1, Vector2i.ZERO, 1):
			diffuse.create_tile(coords)
		if glow.has_room_for_tile(coords, Vector2i.ONE, 1, Vector2i.ZERO, 1):
			glow.create_tile(coords)
	return tile_set


## The ids the layers and sprites use: used_tiles when the export lists
## them (plus the sprites' tiles, in case an export left those out), else a
## scan of every layer and sprite.
func _all_tile_ids() -> PackedInt32Array:
	var seen := {}
	if used_tiles.size() > 0:
		for id in used_tiles:
			seen[id] = true
	else:
		for name in layer_names:
			for id in layers[name]:
				if id >= 0:
					seen[id] = true
	for sprite in sprites:
		for id in sprite.get("tiles", []):
			if int(id) >= 0:
				seen[int(id)] = true
	var ids := PackedInt32Array(seen.keys())
	ids.sort()
	return ids
