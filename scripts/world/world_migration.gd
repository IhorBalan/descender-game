class_name WorldMigration
extends RefCounted

## Turns a map editor export (world.json, the placements.json next to it)
## into the world's scenes: a TileSet resource of the three sheets with the
## terrain kind on each tile, a WorldRegion scene per region with the tile
## layers painted and the lights, rooms, doors and placements as nodes
## (the export's markers among the placements, see
## WorldMap.placement_of_marker), and a WorldScene that holds them all (see
## SceneWorld for the way back). tools/migrate_world.gd runs it; the map pipeline tests round-trip
## an export through it.

const LAYER_NAMES := WorldRegion.LAYER_NAMES
const TILE_SET_FILE := "world_tiles.tres"
## The lights' disc texture, in the project; written next to the TileSet
## when a world is migrated somewhere else (the tests).
const LIGHT_DISC := "res://assets/sprites/light_disc.png"
const LIGHT_DISC_FILE := "light_disc.png"
const WORLD_SCENE_FILE := "world.tscn"
## The folder next to the TileSet its per-region atlases are written to.
const TILES_DIR := "tiles"
## The sheets next to an export, by what they hold.
const SHEET_FILES := {
	"diffuse": WorldMap.SHEET_DIFFUSE, "normal": WorldMap.SHEET_NORMAL, "glow": WorldMap.SHEET_GLOW
}
## The sidecar next to an export (see LevelBuilder.WORLD_PLACEMENTS).
const PLACEMENTS_FILE := "placements.json"


## Writes the TileSet to `tile_set_path`, the region scenes and the world
## scene to `scenes_dir`, from the export at `path` (a world.json) or from
## the world's own scenes (a world.tscn, rebuilt with the current TileSet
## layout; the sheets are read from next to its TileSet). Returns the path
## of the world scene, or "" with an error pushed.
static func migrate(path: String, scenes_dir: String, tile_set_path: String) -> String:
	var map: WorldMap
	var dialogue := ""
	var sheets_dir := ""
	if path.ends_with(".tscn"):
		map = SceneWorld.read(path)
		if map == null:
			return ""
		dialogue = map.dialogue_path
		var old := map.tile_set.resource_path if map.tile_set else tile_set_path
		sheets_dir = old.get_base_dir() + "/"
	else:
		map = WorldMap.read(path)
		if map == null:
			return ""
		sheets_dir = map.dir()
		var sidecar := map.dir() + PLACEMENTS_FILE
		if FileAccess.file_exists(sidecar):
			var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(sidecar))
			if parsed is Dictionary:
				dialogue = str(parsed.get("dialogue", ""))
				if parsed.get("objects", []) is Array:
					map.objects.append_array(parsed["objects"])
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(scenes_dir))
	DirAccess.make_dir_recursive_absolute(
		ProjectSettings.globalize_path(tile_set_path.get_base_dir())
	)
	var tile_set := build_tile_set(
		map, load_sheets(sheets_dir), tile_set_path.get_base_dir().path_join(TILES_DIR)
	)
	if ResourceSaver.save(tile_set, tile_set_path) != OK:
		push_error("WorldMigration: could not write %s" % tile_set_path)
		return ""
	tile_set = ResourceLoader.load(tile_set_path, "", ResourceLoader.CACHE_MODE_REPLACE)
	var light_texture := _light_texture(tile_set_path.get_base_dir())
	var world := WorldScene.new()
	world.name = "World"
	world.world_name = map.name
	world.width = map.width
	world.tile = map.tile
	world.art_tile = map.art_tile
	world.columns = map.columns
	world.tile_set = tile_set
	world.abilities = map.abilities
	world.dialogue = dialogue
	world.descent_dark = map.descent_dark
	var index := TileIndex.of(tile_set, map.columns)
	for i in map.regions.size():
		var region := _build_region(map, i, tile_set, index, light_texture)
		var file := "%s/%02d_%s.tscn" % [scenes_dir, i, EdgeDecor.region_key(region.region_name)]
		if not _save_scene(region, file):
			return ""
		region.free()
		var packed: PackedScene = ResourceLoader.load(file, "", ResourceLoader.CACHE_MODE_REPLACE)
		var instance: WorldRegion = packed.instantiate()
		world.add_child(instance)
		instance.owner = world
	var world_path := scenes_dir.path_join(WORLD_SCENE_FILE)
	var saved := _save_scene(world, world_path)
	world.free()
	return world_path if saved else ""


## The three sheets in `dir` (with a trailing slash) as images: "diffuse",
## "normal" and "glow", null where a sheet is missing.
static func load_sheets(dir: String) -> Dictionary:
	var sheets := {}
	for key in SHEET_FILES:
		var file: String = dir + SHEET_FILES[key]
		var image: Image = null
		if FileAccess.file_exists(file):
			image = Image.load_from_file(ProjectSettings.globalize_path(file))
		sheets[key] = image
	return sheets


## The world's TileSet, cut up for the editor: one atlas source per region
## holding the rows of the sheet only that region paints with (a second
## source, "<region> props", for a region's further rows, as Paradise's
## big pieces), reflowed TileIndex.ATLAS_COLUMNS wide, the region's glow
## tiles under its diffuse ones; rows two regions share go in "Shared",
## rows none paints with in "Spare". A source's atlas is written to
## `crops_dir` as <key>.png (diffuse over glow) and <key>_n.png (its
## normal map), cut from `sheets` (see load_sheets); crops Godot has
## imported are referenced, others embedded. Every tile that has pixels,
## a terrain kind or a use is created, with its kind as custom data
## "kind"; each source records its sheet rows for TileIndex. Terrain set
## 0 matches sides and has a terrain per region, coloured like it; each
## region's solid tiles carry the peering bits TerrainRules reads off the
## map, so the terrain tools paint the edges the map does.
static func build_tile_set(map: WorldMap, sheets: Dictionary, crops_dir: String) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_size = Vector2i(map.art_tile, map.art_tile)
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(0, "kind")
	tile_set.set_custom_data_layer_type(0, TYPE_INT)
	# One "match sides" terrain set, a terrain per region, so painting ground
	# picks the edge pieces the map uses (see TerrainRules).
	var rules := TerrainRules.infer(map)
	tile_set.add_terrain_set()
	tile_set.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_SIDES)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(crops_dir))
	var index := TileIndex.new()
	index.columns = map.columns
	var ids := _tile_ids(map)
	var glow_ids := {}
	for id in map.cells(WorldMap.LAYER_GLOW):
		if id >= 0:
			glow_ids[id] = true
	var source_id := 0
	for group in _row_groups(map, ids, sheets):
		var rows: PackedInt32Array = group["rows"]
		index.add_source(source_id, rows)
		var width := index.atlas_width()
		var art := map.art_tile
		var glow_row: int = int(ceil(float(rows.size() * map.columns) / width))
		var diffuse := Image.create(width * art, glow_row * 2 * art, false, Image.FORMAT_RGBA8)
		var normal := Image.create(width * art, glow_row * 2 * art, false, Image.FORMAT_RGBA8)
		var source := TileSetAtlasSource.new()
		source.resource_name = group["name"]
		source.set_meta(TileIndex.SHEET_ROWS, rows)
		source.texture_region_size = Vector2i(art, art)
		var key: String = group["key"]
		var terrain := -1
		if group.has("region"):
			terrain = tile_set.get_terrains_count(0)
			tile_set.add_terrain(0)
			tile_set.set_terrain_name(0, terrain, group["name"])
			tile_set.set_terrain_color(0, terrain, group["ambient"])
		var created := []
		for order in rows.size():
			for column in map.columns:
				var id: int = rows[order] * map.columns + column
				var from := Rect2i(column * art, rows[order] * art, art, art)
				var at: Vector2i = index.locate(id)[1]
				var lit := _cut(sheets.get("diffuse"), from)
				var has_pixels := lit != null and not lit.is_invisible()
				if has_pixels:
					diffuse.blit_rect(lit, Rect2i(Vector2i.ZERO, from.size), at * art)
					var bumps := _cut(sheets.get("normal"), from)
					if bumps:
						normal.blit_rect(bumps, Rect2i(Vector2i.ZERO, from.size), at * art)
				if has_pixels or ids.has(id):
					created.append([at, int(map.kinds.get(id, 0)), id])
				var shine := _cut(sheets.get("glow"), from)
				var shines := _lit(shine)
				if shines or glow_ids.has(id):
					var glow_at: Vector2i = index.locate(id, true)[1]
					if shines:
						diffuse.blit_rect(shine, Rect2i(Vector2i.ZERO, from.size), glow_at * art)
					created.append([glow_at, -1, -1])
		var texture := CanvasTexture.new()
		texture.diffuse_texture = _crop_texture(diffuse, crops_dir.path_join(key + ".png"))
		texture.normal_texture = _crop_texture(normal, crops_dir.path_join(key + "_n.png"))
		texture.texture_filter = map.texture_filter()
		source.texture = texture
		tile_set.add_source(source, source_id)
		for entry in created:
			if source.has_room_for_tile(entry[0], Vector2i.ONE, 1, Vector2i.ZERO, 1):
				source.create_tile(entry[0])
				if entry[1] >= 0:
					var data := source.get_tile_data(entry[0], 0)
					data.set_custom_data("kind", entry[1])
					if terrain >= 0 and rules.has(entry[2]):
						data.terrain_set = 0
						data.terrain = terrain
						for bit in TerrainRules.SIDES:
							if rules[entry[2]] & bit:
								data.set_terrain_peering_bit(TerrainRules.SIDES[bit], terrain)
		source_id += 1
	return tile_set


## The ids the map gives a meaning: used, painted or with a kind.
static func _tile_ids(map: WorldMap) -> Dictionary:
	var ids := {}
	for id in map.used_tiles:
		ids[id] = true
	for layer_name in map.layer_names:
		for id in map.layers[layer_name]:
			if id >= 0:
				ids[id] = true
	for id in map.kinds:
		ids[id] = true
	return ids


## The sources to make, in order: {"name", "key", "rows"} each. A sheet row
## belongs to the one region that paints with it; a region's first run of
## rows is its own source and any further rows its "props" source.
static func _row_groups(map: WorldMap, ids: Dictionary, sheets: Dictionary) -> Array:
	var columns := map.columns
	var painters := {}
	for i in map.regions.size():
		var spec: Dictionary = map.regions[i]
		var top := int(floor(float(spec.get("y0", 0)) / map.tile))
		var bottom := mini(int(ceil(float(spec.get("y1", 0)) / map.tile)), map.height)
		for layer_name in map.layer_names:
			var cells := map.cells(layer_name)
			for y in range(top, bottom):
				for x in map.width:
					var id := cells[y * map.width + x]
					if id >= 0:
						var row := id / columns
						if not painters.has(row):
							painters[row] = {}
						painters[row][i] = true
	var row_count := 0
	for id in ids:
		row_count = maxi(row_count, id / columns + 1)
	var diffuse: Image = sheets.get("diffuse")
	if diffuse != null:
		row_count = maxi(row_count, diffuse.get_height() / map.art_tile)
	var groups := []
	var taken := {}
	for i in map.regions.size():
		var name := str(map.regions[i].get("name", "Region %d" % i))
		var own := PackedInt32Array()
		for row in range(row_count):
			if painters.get(row, {}).keys() == [i]:
				own.append(row)
		if own.is_empty():
			continue
		var first := PackedInt32Array([own[0]])
		var rest := PackedInt32Array()
		for j in range(1, own.size()):
			if rest.is_empty() and own[j] == own[j - 1] + 1:
				first.append(own[j])
			else:
				rest.append(own[j])
		var key := EdgeDecor.region_key(name)
		var ambient := Color.from_string(str(map.regions[i].get("ambient", "#ffffff")), Color.WHITE)
		groups.append({"name": name, "key": key, "rows": first, "region": i, "ambient": ambient})
		if not rest.is_empty():
			groups.append({"name": "%s props" % name, "key": key + "_props", "rows": rest})
		for row in own:
			taken[row] = true
	var shared := PackedInt32Array()
	var spare := PackedInt32Array()
	for row in range(row_count):
		if taken.has(row):
			continue
		if painters.has(row):
			shared.append(row)
		elif _row_matters(row, columns, ids, diffuse, map.art_tile):
			spare.append(row)
	if not shared.is_empty():
		groups.append({"name": "Shared", "key": "shared", "rows": shared})
	if not spare.is_empty():
		groups.append({"name": "Spare", "key": "spare", "rows": spare})
	return groups


## Whether a row nobody paints with has anything in it.
static func _row_matters(row: int, columns: int, ids: Dictionary, diffuse: Image, art: int) -> bool:
	for column in columns:
		if ids.has(row * columns + column):
			return true
	if diffuse == null:
		return false
	var band := Rect2i(0, row * art, diffuse.get_width(), art)
	return not diffuse.get_region(band).is_invisible()


## Whether a glow tile has any light in it: the glow sheet is opaque black
## where a tile does not glow, which adds nothing in the game but would
## clutter the palette.
static func _lit(image: Image) -> bool:
	if image == null or image.is_invisible():
		return false
	var rgb := image.duplicate() as Image
	rgb.premultiply_alpha()
	rgb.convert(Image.FORMAT_RGB8)
	var data := rgb.get_data()
	return data.count(0) < data.size()


## A tile's pixels out of a sheet, or null when the sheet lacks them.
static func _cut(sheet: Image, from: Rect2i) -> Image:
	if sheet == null:
		return null
	if from.end.x > sheet.get_width() or from.end.y > sheet.get_height():
		return null
	return sheet.get_region(from)


## Writes a crop and returns it as a texture: by reference when Godot has
## imported it already, else its pixels.
static func _crop_texture(image: Image, path: String) -> Texture2D:
	image.save_png(ProjectSettings.globalize_path(path))
	return _texture(path)


## An imported texture by reference when Godot has it, else the PNG's
## pixels as an ImageTexture; null when there is no file.
static func _texture(path: String) -> Texture2D:
	if path.begins_with("res://") and ResourceLoader.exists(path):
		return load(path)
	if not FileAccess.file_exists(path):
		return null
	var image := Image.load_from_file(ProjectSettings.globalize_path(path))
	return ImageTexture.create_from_image(image) if image else null


## The lights' disc: the project's, else one written next to the TileSet.
static func _light_texture(dir: String) -> Texture2D:
	if ResourceLoader.exists(LIGHT_DISC):
		return load(LIGHT_DISC)
	var path := dir.path_join(LIGHT_DISC_FILE)
	if not FileAccess.file_exists(path):
		var size := WorldLight.TEXTURE_SIZE
		var image := Image.create(size, size, false, Image.FORMAT_RGBA8)
		var centre := Vector2.ONE * size / 2.0
		for y in size:
			for x in size:
				var distance := Vector2(x + 0.5, y + 0.5).distance_to(centre) / centre.x
				image.set_pixel(x, y, Color(1.0, 1.0, 1.0, clampf(1.0 - distance, 0.0, 1.0)))
		image.save_png(ProjectSettings.globalize_path(path))
	return _texture(path)


static func _save_scene(root: Node, path: String) -> bool:
	var packed := PackedScene.new()
	if packed.pack(root) != OK or ResourceSaver.save(packed, path) != OK:
		push_error("WorldMigration: could not write %s" % path)
		return false
	return true


## One region's scene: the band's rows of every layer, and what stands in
## the band, in level pixels from its top-left corner.
static func _build_region(
	map: WorldMap, index: int, tile_set: TileSet, tiles_index: TileIndex, light_texture: Texture2D
) -> WorldRegion:
	var spec := map.regions[index]
	var tile := map.tile
	var top := int(floor(float(spec.get("y0", 0)) / tile))
	var bottom := int(ceil(float(spec.get("y1", 0)) / tile))
	var offset := Vector2(0.0, top * tile)
	var region := WorldRegion.new()
	region.name = _node_name(str(spec.get("name", "Region")), "Region")
	region.region_name = str(spec.get("name", ""))
	region.subtitle = str(spec.get("subtitle", ""))
	region.position = offset
	region.rows = bottom - top
	region.look = RegionLook.write(
		EdgeDecor.region_key(region.region_name), RegionLook.from_spec(spec)
	)
	var tiles := Node2D.new()
	tiles.name = "Tiles"
	region.add_child(tiles)
	tiles.owner = region
	for layer_name in LAYER_NAMES:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		layer.tile_set = tile_set
		layer.texture_filter = map.texture_filter()
		layer.scale = Vector2.ONE * map.art_scale()
		layer.collision_enabled = false
		var glow := layer_name == WorldMap.LAYER_GLOW
		if glow:
			var material := CanvasItemMaterial.new()
			material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
			material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
			layer.material = material
		var cells := map.cells(layer_name)
		if cells.size() > 0:
			for y in range(top, mini(bottom, map.height)):
				for x in map.width:
					var id := cells[y * map.width + x]
					if id >= 0:
						var at := tiles_index.locate(id, glow)
						layer.set_cell(Vector2i(x, y - top), at[0], at[1])
		tiles.add_child(layer)
		layer.owner = region
	var holders := {}
	for holder_name in ["Lights", "Rooms", "Doors", "Placements"]:
		var holder := Node2D.new()
		holder.name = holder_name
		region.add_child(holder)
		holder.owner = region
		holders[holder_name] = holder
	var count := 0
	for light_spec in map.lights:
		var y := float(light_spec.get("y", 0))
		if y < offset.y or y >= bottom * tile:
			continue
		count += 1
		var light := WorldLight.new()
		light.name = "Light%d" % count
		light.texture = light_texture
		light.position = Vector2(float(light_spec.get("x", 0)), y) - offset
		light.height = float(light_spec.get("z", 0))
		light.color = Color.from_string(str(light_spec.get("color", "#ffffff")), Color.WHITE)
		light.energy = float(light_spec.get("energy", 1.0))
		light.texture_scale = float(light_spec.get("radius", 64)) * 2.0 / WorldLight.TEXTURE_SIZE
		light.shadow_enabled = false
		light.flicker = bool(light_spec.get("flicker", false))
		_add(holders["Lights"], light, region)
	for room_spec in map.rooms:
		if int(room_spec.get("region", -1)) != index:
			continue
		var rect: Array = room_spec.get("rect", [0, 0, 0, 0])
		var room := WorldRoom.new()
		room.id = int(room_spec.get("id", 0))
		room.name = "Room%d" % room.id
		room.kind = str(room_spec.get("kind", "room"))
		room.area = int(room_spec.get("area", 0))
		room.depth = int(room_spec.get("depth", 0))
		room.room_name = str(room_spec.get("name", "")) if room_spec.get("name") != null else ""
		room.ability = str(room_spec.get("ability", "")) if room_spec.get("ability") != null else ""
		room.position = Vector2(float(rect[0]), float(rect[1])) - offset
		room.size = Vector2(float(rect[2]), float(rect[3]))
		_add(holders["Rooms"], room, region)
	for door_spec in map.doors:
		var cell := Vector2i(int(door_spec.get("x", 0)), int(door_spec.get("y", 0)))
		if cell.y < top or cell.y >= bottom:
			continue
		var door := WorldDoor.new()
		door.id = int(door_spec.get("id", 0))
		door.name = "Door%d" % door.id
		door.room_a = int(door_spec.get("a", 0))
		door.room_b = int(door_spec.get("b", 0))
		door.dir = str(door_spec.get("dir", "h"))
		var lock: Variant = door_spec.get("lock")
		if lock is Dictionary:
			door.lock_kind = str(lock.get("kind", ""))
			door.lock_ability = int(lock.get("ability", 0))
			door.lock_region = int(lock.get("region", 0))
		door.secret = bool(door_spec.get("secret", false))
		door.position = (Vector2(cell) + Vector2(0.5, 0.5)) * tile - offset
		_add(holders["Doors"], door, region)
	count = 0
	for object_spec in map.objects:
		if not object_spec is Dictionary:
			continue
		var cell := Vector2i(int(object_spec.get("x", 0)), int(object_spec.get("y", 0)))
		if cell.y < top or cell.y >= bottom:
			continue
		count += 1
		var placement := WorldPlacement.new()
		placement.from_spec(object_spec)
		placement.name = "%s%d" % [placement.type.capitalize(), count]
		placement.position = SceneWorld.feet_of(cell, tile) - offset
		_add(holders["Placements"], placement, region)
	return region


static func _add(holder: Node, node: Node, root: Node) -> void:
	holder.add_child(node)
	node.owner = root


## A node name from a region's name, without its number: "RuinedEarth".
static func _node_name(text: String, fallback: String) -> String:
	var name := ""
	for word in EdgeDecor.region_key(text).split("_"):
		var clean := ""
		for ch in word:
			if ch.is_valid_identifier():
				clean += ch
		name += clean.capitalize()
	return name if not name.is_empty() else fallback
