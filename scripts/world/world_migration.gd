class_name WorldMigration
extends RefCounted

## Turns a map editor export (world.json, the placements.json next to it)
## into the world's scenes: a TileSet resource of the three sheets with the
## terrain kind on each tile, a WorldRegion scene per region with the tile
## layers painted and the lights, rooms, doors, markers and placements as
## nodes, and a WorldScene that holds them all (see SceneWorld for the way
## back). tools/migrate_world.gd runs it; the map pipeline tests round-trip
## an export through it.

const LAYER_NAMES := WorldRegion.LAYER_NAMES
const TILE_SET_FILE := "world_tiles.tres"
## The lights' disc texture, in the project; written next to the TileSet
## when a world is migrated somewhere else (the tests).
const LIGHT_DISC := "res://assets/sprites/light_disc.png"
const LIGHT_DISC_FILE := "light_disc.png"
const WORLD_SCENE_FILE := "world.tscn"
## The sidecar next to an export (see LevelBuilder.WORLD_PLACEMENTS).
const PLACEMENTS_FILE := "placements.json"


## Writes the TileSet to `tile_set_path`, the region scenes and the world
## scene to `scenes_dir`, from the export at `json_path`. Returns the path
## of the world scene, or "" with an error pushed.
static func migrate(json_path: String, scenes_dir: String, tile_set_path: String) -> String:
	var map := WorldMap.read(json_path)
	if map == null:
		return ""
	var objects: Array = []
	var sidecar := map.dir() + PLACEMENTS_FILE
	var dialogue := ""
	if FileAccess.file_exists(sidecar):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(sidecar))
		if parsed is Dictionary:
			dialogue = str(parsed.get("dialogue", ""))
			if parsed.get("objects", []) is Array:
				objects = parsed["objects"]
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(scenes_dir))
	DirAccess.make_dir_recursive_absolute(
		ProjectSettings.globalize_path(tile_set_path.get_base_dir())
	)
	var tile_set := build_tile_set(map)
	if ResourceSaver.save(tile_set, tile_set_path) != OK:
		push_error("WorldMigration: could not write %s" % tile_set_path)
		return ""
	tile_set = load(tile_set_path)
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
	for i in map.regions.size():
		var region := _build_region(map, i, objects, tile_set, light_texture)
		var file := "%s/%02d_%s.tscn" % [scenes_dir, i, EdgeDecor.region_key(region.region_name)]
		if not _save_scene(region, file):
			return ""
		region.free()
		var instance: WorldRegion = load(file).instantiate()
		world.add_child(instance)
		instance.owner = world
	var world_path := scenes_dir.path_join(WORLD_SCENE_FILE)
	var saved := _save_scene(world, world_path)
	world.free()
	return world_path if saved else ""


## The TileSet of the export's sheets: the diffuse sheet lit by its normal
## map as source 0, the glow sheet as source 1, a tile for every id the
## layers use with its terrain kind as custom data "kind". Sheets under
## res:// that Godot has imported are referenced, so the resource stays
## small; others are embedded.
static func build_tile_set(map: WorldMap) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_size = Vector2i(map.art_tile, map.art_tile)
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(0, "kind")
	tile_set.set_custom_data_layer_type(0, TYPE_INT)
	var lit := CanvasTexture.new()
	lit.diffuse_texture = _texture(map.dir() + WorldMap.SHEET_DIFFUSE)
	lit.normal_texture = _texture(map.dir() + WorldMap.SHEET_NORMAL)
	lit.texture_filter = map.texture_filter()
	var diffuse := TileSetAtlasSource.new()
	diffuse.texture = lit
	diffuse.texture_region_size = Vector2i(map.art_tile, map.art_tile)
	tile_set.add_source(diffuse, 0)
	var glow := TileSetAtlasSource.new()
	var glow_texture := _texture(map.dir() + WorldMap.SHEET_GLOW)
	glow.texture = glow_texture if glow_texture else lit
	glow.texture_region_size = Vector2i(map.art_tile, map.art_tile)
	tile_set.add_source(glow, 1)
	var ids := {}
	for id in map.used_tiles:
		ids[id] = true
	for layer_name in map.layer_names:
		for id in map.layers[layer_name]:
			if id >= 0:
				ids[id] = true
	for id in map.kinds:
		ids[id] = true
	for id in ids:
		var coords := map.atlas_coords(id)
		if diffuse.has_room_for_tile(coords, Vector2i.ONE, 1, Vector2i.ZERO, 1):
			diffuse.create_tile(coords)
			diffuse.get_tile_data(coords, 0).set_custom_data("kind", int(map.kinds.get(id, 0)))
		if glow.has_room_for_tile(coords, Vector2i.ONE, 1, Vector2i.ZERO, 1):
			glow.create_tile(coords)
	return tile_set


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
	map: WorldMap, index: int, objects: Array, tile_set: TileSet, light_texture: Texture2D
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
	region.ambient = Color.from_string(str(spec.get("ambient", "#ffffff")), Color.WHITE)
	region.position = offset
	region.rows = bottom - top
	for key in spec:
		if key not in ["name", "subtitle", "ambient", "y0", "y1"]:
			region.look[key] = spec[key]
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
		var source := 0
		if layer_name == WorldMap.LAYER_GLOW:
			source = 1
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
						layer.set_cell(Vector2i(x, y - top), source, map.atlas_coords(id))
		tiles.add_child(layer)
		layer.owner = region
	var holders := {}
	for holder_name in ["Lights", "Rooms", "Doors", "Markers", "Placements"]:
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
	for marker_spec in map.markers:
		var cell := Vector2i(int(marker_spec.get("x", 0)), int(marker_spec.get("y", 0)))
		if cell.y < top or cell.y >= bottom:
			continue
		var marker := WorldMarker.new()
		marker.id = int(marker_spec.get("id", 0))
		marker.type = str(marker_spec.get("type", "save"))
		marker.name = "%s%d" % [marker.type.capitalize(), marker.id]
		marker.label = str(marker_spec.get("label", ""))
		marker.room = int(marker_spec.get("room", -1))
		marker.position = map.marker_position(marker_spec) - offset
		_add(holders["Markers"], marker, region)
	count = 0
	for object_spec in objects:
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
