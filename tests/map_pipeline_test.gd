extends Node

## Headless tests of the pieces behind the map pipeline: the level
## registry, the export reader, the Firestore reader's parsing, the map
## store's cache, the level builder's free-placed sprites and the backdrop
## lookup. Each test builds what it needs under user://map_pipeline_test/
## and cleans up after.
##
##   godot --headless --path . res://tests/map_pipeline_test.tscn
##
## It runs as a scene so the autoloads exist (Levels, MapStore), next to
## unit_test.tscn and the smoke test in CI. Every method named test_* runs
## once, in order; `_check` counts. Exits with code 1 when a check fails or
## an error stops the checks from running.

## Loaded by path, so a fresh checkout works before the class cache knows it.
const FirestoreRest := preload("res://scripts/firestore_rest.gd")
const LookBlend := preload("res://scripts/look_blend.gd")
## Every check the script makes; fewer at the end means something broke early.
const EXPECTED_CHECKS := 260
const DIR := "user://map_pipeline_test"

var _checks := 0
var _failures := 0


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DIR))
	for method in get_method_list():
		var name := str(method["name"])
		if name.begins_with("test_"):
			call(name)
	_cleanup()
	_report()


# ---------- Levels ----------


func test_levels() -> void:
	_check(Levels.first_id == "world_1", "the descent starts at world_1")
	_check(Levels.next_id("world_1") == "", "nothing follows the last level of the descent")
	_check(not Levels.has("map_nope"), "an unregistered level is unknown")
	_check(
		Levels.resolve("made up") == Levels.first_id, "an unknown id resolves to the first level"
	)

	Levels.register({"id": "map_a", "name": "Map A", "path": "user://maps/a/world.json"})
	_check(Levels.has("map_a"), "a registered level is known")
	_check(Levels.display_name("map_a") == "Map A", "a registered level keeps its name")
	_check(Levels.path("map_a") == "user://maps/a/world.json", "a registered level keeps its path")
	_check(Levels.ambient("map_a") == "heaven", "a registered level defaults to the heaven ambient")
	_check(Levels.sky("map_a") == Levels.WORLD_SKY, "a registered level defaults to the world sky")
	_check(Levels.next_id("map_a") == "", "nothing follows a registered level")
	_check(
		Levels.resolve("user://maps/a/world.json") == "map_a", "a registered level resolves by path"
	)
	Levels.register({"id": "map_a", "name": "Map A2", "path": "user://maps/a/world.json"})
	var copies := 0
	for level in Levels.extra():
		if level["id"] == "map_a":
			copies += 1
	_check(copies == 1, "registering the same id again replaces it")
	_check(Levels.display_name("map_a") == "Map A2", "the replacement wins")
	Levels.unregister("map_a")
	_check(not Levels.has("map_a"), "an unregistered level is forgotten")
	Levels.register({"id": "", "path": "x"})
	_check(not Levels.has(""), "registering without an id is ignored")

	var path := DIR + "/path_level/world.json"
	FirestoreRest.write_file(path, "{}".to_utf8_buffer())
	_check(Levels.register_path(path) == path, "a world.json path registers under its own path")
	_check(Levels.display_name(path) == "Path Level", "a path level is named after its folder")
	_check(Levels.resolve(path) == path, "a path resolves to itself once registered")
	_check(
		Levels.register_path(DIR + "/missing/world.json") == "", "a missing path does not register"
	)
	_check(
		Levels.register_path(DIR + "/path_level/notes.txt") == "", "only world json files register"
	)
	Levels.unregister(path)


# ---------- FirestoreRest ----------


func test_firestore_unwrap() -> void:
	var typed := {
		"mapValue":
		{
			"fields":
			{
				"name": {"stringValue": "World"},
				"count": {"integerValue": "42"},
				"ratio": {"doubleValue": 0.5},
				"flag": {"booleanValue": true},
				"nothing": {"nullValue": null},
				"list": {"arrayValue": {"values": [{"stringValue": "a"}, {"integerValue": "2"}]}},
				"nested": {"mapValue": {"fields": {"x": {"integerValue": "1"}}}},
			}
		}
	}
	var plain: Dictionary = FirestoreRest.unwrap(typed)
	_check(plain["name"] == "World", "unwrap reads strings")
	_check(plain["count"] == 42 and plain["count"] is int, "unwrap reads integers as ints")
	_check(is_equal_approx(plain["ratio"], 0.5), "unwrap reads doubles")
	_check(plain["flag"] == true, "unwrap reads booleans")
	_check(plain["nothing"] == null, "unwrap reads nulls")
	_check(plain["list"] == ["a", 2], "unwrap reads arrays")
	_check(plain["nested"] == {"x": 1}, "unwrap reads nested maps")
	_check(FirestoreRest.unwrap("junk") == null, "unwrap of a non-value is null")
	_check(
		FirestoreRest.base_url("p").begins_with("https://firestore.googleapis.com/v1/projects/p/"),
		"the documents URL names the project"
	)


func test_write_file() -> void:
	var path := DIR + "/deep/er/file.bin"
	_check(
		FirestoreRest.write_file(path, PackedByteArray([1, 2, 3])), "write_file makes the folders"
	)
	_check(FileAccess.get_file_as_bytes(path) == PackedByteArray([1, 2, 3]), "and writes the bytes")
	_check(not FileAccess.file_exists(path + ".tmp"), "the temporary file is renamed away")
	_check(FirestoreRest.write_file(path, PackedByteArray([9])), "write_file replaces a file")
	_check(FileAccess.get_file_as_bytes(path) == PackedByteArray([9]), "with the new bytes")


# ---------- WorldMap ----------


## A 3 x 2 export: rock along the bottom row, one glowing cell, a sprite.
func _write_export(dir: String, art_tile: int) -> String:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(dir))
	var image := Image.create(64 * art_tile, 2 * art_tile, false, Image.FORMAT_RGBA8)
	image.fill(Color(0.5, 0.5, 0.5, 1.0))
	for sheet in ["world_tiles.png", "world_tiles_n.png", "world_tiles_e.png"]:
		image.save_png(ProjectSettings.globalize_path(dir.path_join(sheet)))
	var data := {
		"format": "lit-pixel-world-godot",
		"export_version": 3,
		"width": 3,
		"height": 2,
		"tile": 16,
		"art_tile": art_tile,
		"columns": 64,
		"layers":
		[
			{"name": "Background", "tiles": [-1, -1, -1, -1, -1, -1]},
			{"name": "Decor", "tiles": [-1, 5, -1, -1, -1, -1]},
			{"name": "Terrain", "tiles": [-1, -1, -1, 1, 1, 1]},
			{"name": "Glow", "tiles": [-1, 5, -1, -1, -1, -1]},
		],
		"used_tiles": [1, 5],
		"tile_kinds": {"1": 1},
		"regions":
		[{"name": "1. Test Land", "subtitle": "", "ambient": "#ffffff", "y0": 0, "y1": 32}],
		"rooms": [],
		"doors": [],
		"abilities": [],
		"markers":
		[{"id": 1, "type": "start", "x": 0, "y": 0, "label": "Start", "px": 8, "py": 16}],
		"lights": [],
		"sprites":
		[
			{
				"id": 9,
				"name": "ledge",
				"x": 20,
				"y": 4,
				"columns": 2,
				"rows": 1,
				"flip": true,
				"solid": true,
				"glow": true,
				"tiles": [70, 71],
			},
			{"id": 10, "name": "broken", "x": 0, "y": 0, "columns": 2, "rows": 2, "tiles": [70]},
			{
				"id": 11,
				"name": "padded",
				"x": 100,
				"y": 100,
				"columns": 2,
				"rows": 2,
				"flip": true,
				"solid": true,
				"tiles": [70, 71, 70, 71],
				"hit": [4, 20, 24, 12],
			},
		],
	}
	var path := dir.path_join("world.json")
	FirestoreRest.write_file(path, JSON.stringify(data).to_utf8_buffer())
	return path


func test_world_map() -> void:
	var path := _write_export(DIR + "/export16", 16)
	var map := WorldMap.read(path)
	_check(map != null, "a small export reads")
	if map == null:
		return
	_check(map.width == 3 and map.height == 2, "the size is read")
	_check(
		map.layer_names == PackedStringArray(["Background", "Decor", "Terrain", "Glow"]),
		"the layers are read"
	)
	_check(map.kind_at(0, 1) == WorldMap.KIND_SOLID, "terrain kinds are read")
	_check(map.kind_at(0, 0) == WorldMap.KIND_NONE, "air has no kind")
	_check(map.sprites.size() == 3, "sprites are read")
	_check(map.atlas_coords(70) == Vector2i(6, 1), "atlas ids become columns and rows")
	_check(is_equal_approx(map.art_scale(), 1.0), "16-px art is drawn at scale 1")
	_check(
		map.texture_filter() == CanvasItem.TEXTURE_FILTER_NEAREST, "16-px art is sampled nearest"
	)
	_check(map.region_at(5.0).get("name") == "1. Test Land", "regions are found by y")
	_check(
		map.objects == [{"type": "start", "x": 0, "y": 0}],
		"the start marker reads as a start placement"
	)
	var tile_set := map.build_tile_set()
	var source := tile_set.get_source(0) as TileSetAtlasSource
	_check(
		source != null and source.has_tile(Vector2i(6, 1)), "the tile set has the sprites' tiles"
	)
	_check(source.has_tile(Vector2i(1, 0)), "and the layers' tiles")

	var big := WorldMap.read(_write_export(DIR + "/export128", 128))
	_check(big != null and is_equal_approx(big.art_scale(), 0.125), "128-px art is drawn at 1/8")
	_check(
		big != null and big.texture_filter() == CanvasItem.TEXTURE_FILTER_LINEAR,
		"128-px art is sampled linear"
	)
	_check(WorldMap.read(DIR + "/nowhere.json") == null, "a missing export reads as null")
	FirestoreRest.write_file(DIR + "/bad.json", '{"format":"other"}'.to_utf8_buffer())
	_check(WorldMap.read(DIR + "/bad.json") == null, "another format reads as null")


# ---------- LevelBuilder ----------


func test_level_builder_sprites() -> void:
	var builder := LevelBuilder.new()
	add_child(builder)
	# The whole export at once: nothing stands in it to stream around.
	builder.streaming = false
	builder.load_from_file(DIR + "/export16/world.json")
	var tiles := builder.get_node("Tiles")
	var names: Array[String] = []
	for child in tiles.get_children():
		names.append(child.name)
	_check(
		names.slice(0, 4) == ["Background", "Decor", "Terrain", "Glow"],
		"the tile layers come first"
	)
	_check(names.has("Sprite9"), "a sprite becomes a layer of its own")
	_check(names.has("Sprite9Glow"), "a glowing sprite also gets a glow layer")
	_check(not names.has("Sprite10"), "a sprite whose tiles do not fill its size is skipped")
	var sprite := tiles.get_node_or_null("Sprite9") as TileMapLayer
	if sprite:
		_check(sprite.position == Vector2(20, 4), "the sprite sits at its pixel position")
		_check(sprite.get_used_cells().size() == 2, "the sprite has its tiles")
		# Mirrored: the first tile goes to the right column, flipped.
		_check(
			sprite.get_cell_atlas_coords(Vector2i(1, 0)) == Vector2i(6, 1),
			"a mirrored sprite swaps columns"
		)
		_check(
			sprite.get_cell_alternative_tile(Vector2i(0, 0)) == TileSetAtlasSource.TRANSFORM_FLIP_H,
			"and flips each tile"
		)
	var solids := builder.get_node("Solids")
	# One run of rock along the bottom row, plus a polygon per solid sprite.
	_check(solids.get_child_count() == 3, "a solid sprite adds one collision polygon")
	var outlines: Array[Rect2] = []
	for child in solids.get_children():
		if child is CollisionPolygon2D:
			var bounds := Rect2(child.polygon[0], Vector2.ZERO)
			for point in child.polygon:
				bounds = bounds.expand(point)
			outlines.append(Rect2(child.position + bounds.position, bounds.size))
	_check(outlines.size() == 2, "the sprites collide with polygons traced from their art")
	# The test sheet is opaque everywhere, so each outline is the sprite's whole box, give or
	# take the smoothing (LevelBuilder.SPRITE_SMOOTH_PX).
	_check(_has_box(outlines, Rect2(20, 4, 32, 16)), "the ledge's outline covers its two tiles")
	_check(
		_has_box(outlines, Rect2(100, 100, 32, 32)),
		"the padded sprite's outline covers its four tiles"
	)
	_check(builder.player_start == Vector2(8, 16), "the start placement puts the hero down")
	builder.queue_free()


# ---------- SceneWorld and WorldMigration ----------


## An export with a bit of everything that the scenes have to carry.
func _write_rich_export(dir: String) -> String:
	var path := _write_export(dir, 16)
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	data["name"] = "Round Trip"
	data["width"] = 4
	data["height"] = 4
	data["layers"] = [
		{
			"name": "Background",
			"tiles": [-1, -1, -1, -1, -1, -1, -1, -1, 66, 66, 66, 66, -1, -1, -1, -1]
		},
		{"name": "Decor", "tiles": [-1, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1]},
		{"name": "Terrain", "tiles": [-1, -1, -1, -1, 1, 1, 7, 7, -1, -1, -1, -1, 65, 65, 65, 65]},
		{"name": "Glow", "tiles": [-1, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1]},
	]
	# The top region paints with the sheet's first row, the low one with its second.
	data["used_tiles"] = [1, 5, 7, 65, 66]
	data["tile_kinds"] = {"1": 1, "7": 2, "65": 1}
	# The glow sheet is black but for tile 5, as the editor exports it.
	var glow := Image.create(64 * 16, 2 * 16, false, Image.FORMAT_RGBA8)
	glow.fill(Color.BLACK)
	glow.fill_rect(Rect2i(5 * 16, 0, 16, 16), Color(1.0, 0.8, 0.2, 1.0))
	glow.save_png(ProjectSettings.globalize_path(dir.path_join("world_tiles_e.png")))
	data["regions"] = [
		{"name": "Top", "subtitle": "Up", "ambient": "#ffffff", "y0": 0, "y1": 32, "cave": true},
		{"name": "1. Low", "subtitle": "", "ambient": "#102030", "y0": 32, "y1": 64},
	]
	data["rooms"] = [
		{
			"id": 1,
			"region": 0,
			"cx": 0,
			"cy": 0,
			"cw": 1,
			"ch": 1,
			"kind": "boss",
			"area": 0,
			"depth": 0,
			"name": "Hall",
			"rect": [0, 0, 480, 272]
		},
		{
			"id": 2,
			"region": 1,
			"cx": 0,
			"cy": 0,
			"cw": 1,
			"ch": 1,
			"kind": "room",
			"area": 1,
			"depth": 1,
			"rect": [0, 32, 480, 272]
		},
	]
	data["doors"] = [
		{"id": 5, "a": 1, "b": 2, "dir": "v", "x": 2, "y": 1, "lock": null},
		{
			"id": 6,
			"a": 1,
			"b": 2,
			"dir": "h",
			"x": 1,
			"y": 2,
			"lock": {"kind": "ability", "ability": 2},
			"secret": true
		},
	]
	data["abilities"] = ["Wings (double jump)", "Halo Dash", "Wall Grip"]
	data["markers"] = [
		{"id": 1, "type": "start", "x": 0, "y": 0, "label": "Start", "room": 1, "px": 8, "py": 16},
		{"id": 2, "type": "save", "x": 1, "y": 2, "label": "Save", "room": 2, "px": 24, "py": 48},
	]
	data["lights"] = [
		{
			"x": 20,
			"y": 10,
			"z": 18,
			"radius": 64,
			"color": "#ffb35c",
			"energy": 0.7,
			"flicker": false
		},
		{
			"x": 40,
			"y": 40,
			"z": 8,
			"radius": 32,
			"color": "#4080ff",
			"energy": 1.25,
			"flicker": true
		},
	]
	data["sprites"] = []
	FirestoreRest.write_file(path, JSON.stringify(data).to_utf8_buffer())
	var sidecar := {
		"dialogue": "res://dialogue/world_1.json",
		"objects":
		[
			{"type": "trigger", "x": 0, "y": 0, "dialogue": ["opening"]},
			{
				"type": "npc",
				"x": 1,
				"y": 0,
				"name": "Pell",
				"character": "pell",
				"faces_left": true,
				"shop": "pell",
				"tint": [1.0, 0.5, 0.25],
				"dialogue": ["pell_intro"],
				"rotate": ["pell_again"]
			},
			{
				"type": "enemy",
				"x": 2,
				"y": 0,
				"character": "imp",
				"behaviour": "chase",
				"barks": "imp"
			},
			{
				"type": "enemy",
				"x": 3,
				"y": 2,
				"character": "brute",
				"passive": true,
				"requires": "x"
			},
			{"type": "item", "x": 0, "y": 2, "item": "token", "dialogue": ["item_token"]},
			{"type": "upgrade", "x": 1, "y": 2, "upgrade": "heart"},
			{"type": "power", "x": 2, "y": 2, "power": "halo", "unless": "gone"},
			{"type": "barrier", "x": 3, "y": 0, "height": 2},
		]
	}
	FirestoreRest.write_file(
		dir.path_join("placements.json"), JSON.stringify(sidecar).to_utf8_buffer()
	)
	return path


## Numbers as floats and dictionaries with their keys sorted, so a JSON
## reading and a scene reading of the same world compare equal.
func _norm(value: Variant) -> Variant:
	if value is Dictionary:
		var keys: Array = value.keys()
		keys.sort()
		var out := {}
		for key in keys:
			out[key] = _norm(value[key])
		return out
	if value is Array or value is PackedInt32Array or value is PackedStringArray:
		var out := []
		for item in value:
			out.append(_norm(item))
		return out
	if value is int or value is float:
		# Node properties are single precision: 0.7 comes back as 0.699999988.
		return snappedf(float(value), 0.001)
	return value


func _same(a: Variant, b: Variant) -> bool:
	return JSON.stringify(_norm(a)) == JSON.stringify(_norm(b))


func test_scene_world_round_trip() -> void:
	var json_path := _write_rich_export(DIR + "/round_trip")
	var scenes_dir := DIR + "/round_trip/scenes"
	var world_path := WorldMigration.migrate(
		json_path, scenes_dir, DIR + "/round_trip/scenes/world_tiles.tres"
	)
	_check(world_path == scenes_dir + "/world.tscn", "the migration writes the world scene")
	_check(FileAccess.file_exists(scenes_dir + "/00_top.tscn"), "and a scene per region")
	_check(FileAccess.file_exists(scenes_dir + "/01_low.tscn"), "named after its key")
	var a := WorldMap.read(json_path)
	var b := SceneWorld.read(world_path)
	_check(b != null, "the world scene reads back")
	if b == null:
		return
	_check(b.width == a.width and b.height == a.height, "the grid keeps its size")
	_check(b.tile == a.tile and b.art_tile == a.art_tile, "and its tile sizes")
	for layer_name in a.layer_names:
		_check(b.layers[layer_name] == a.layers[layer_name], "layer %s round-trips" % layer_name)
	_check(_same(b.kinds, a.kinds), "the terrain kinds ride on the TileSet")
	_check(b.used_tiles == a.used_tiles, "the used tiles are found again")
	_check(_same(b.regions, a.regions), "the regions round-trip, their look included")
	_check(_same(b.rooms, a.rooms), "the rooms round-trip")
	_check(_same(b.doors, a.doors), "the doors round-trip, locks and secrets included")
	_check(_same(b.lights, a.lights), "the lights round-trip")
	_check(b.abilities == a.abilities, "the abilities round-trip")
	_check(b.dialogue_path == "res://dialogue/world_1.json", "the dialogue file is kept")
	var sidecar: Dictionary = JSON.parse_string(
		FileAccess.get_file_as_string(DIR + "/round_trip/placements.json")
	)
	# The scenes keep the placements region by region, in their order within
	# it: what the export's markers stand for, then the sidecar's objects.
	var expected: Array = []
	for region in 2:
		for spec in a.objects + sidecar["objects"]:
			if int(spec["y"]) / 2 == region:
				expected.append(spec)
	_check(
		a.objects.size() == 2 and _same(b.objects, expected),
		"the placements round-trip, region by region, the markers among them"
	)
	_check(b.tile_set != null and b.tile_set.has_source(0), "the world brings its TileSet")
	# One atlas source per region, holding the sheet rows only it paints with.
	var top := b.tile_set.get_source(0) as TileSetAtlasSource
	var low := b.tile_set.get_source(1) as TileSetAtlasSource
	_check(b.tile_set.get_source_count() == 2, "a source per region")
	_check(top.resource_name == "Top" and low.resource_name == "1. Low", "named after it")
	_check(
		(
			top.get_meta(TileIndex.SHEET_ROWS) == PackedInt32Array([0])
			and low.get_meta(TileIndex.SHEET_ROWS) == PackedInt32Array([1])
		),
		"each recording the sheet rows it holds"
	)
	var index := b.tile_index()
	_check(index.locate(65) == [1, Vector2i(1, 0)], "a tile is drawn from its region's source")
	_check(index.locate(5, true) == [0, Vector2i(5, 4)], "its glow under the diffuse tiles")
	_check(index.id_of(1, Vector2i(2, 0)) == 66, "and the source's coords name the id again")
	_check(
		top.has_tile(Vector2i(5, 4)) and not top.has_tile(Vector2i(2, 4)),
		"glow tiles exist only where they glow or are painted"
	)
	_check(
		int(low.get_tile_data(Vector2i(1, 0), 0).get_custom_data("kind")) == WorldMap.KIND_SOLID,
		"the kind rides on the region's tile"
	)
	_check(
		(
			FileAccess.file_exists(scenes_dir + "/tiles/low.png")
			and FileAccess.file_exists(scenes_dir + "/tiles/top_n.png")
		),
		"the atlases are written next to the TileSet"
	)
	# A terrain per region, its solid tiles facing the way the map paints them.
	_check(
		(
			b.tile_set.get_terrain_sets_count() == 1
			and b.tile_set.get_terrain_set_mode(0) == TileSet.TERRAIN_MODE_MATCH_SIDES
		),
		"one terrain set, matching sides"
	)
	_check(
		(
			b.tile_set.get_terrains_count(0) == 2
			and b.tile_set.get_terrain_name(0, 0) == "Top"
			and b.tile_set.get_terrain_name(0, 1) == "1. Low"
		),
		"a terrain per region"
	)
	var ground := low.get_tile_data(Vector2i(1, 0), 0)
	_check(
		ground.terrain_set == 0 and ground.terrain == 1, "a solid tile is in its region's terrain"
	)
	_check(
		(
			TerrainRules.mask_of(ground, 1)
			== TerrainRules.LEFT | TerrainRules.RIGHT | TerrainRules.BOTTOM
		),
		"facing the way the map paints it: ground to both sides and below, open above"
	)
	_check(top.get_tile_data(Vector2i(7, 0), 0).terrain == -1, "a ledge is not ground")
	var builder := LevelBuilder.new()
	add_child(builder)
	builder.streaming = false
	builder.load_from_file(world_path)
	_check(builder.world != null, "a level builds from the world scene")
	_check(builder.player_start == Vector2(8, 16), "the start placement puts the hero down")
	_check(builder.checkpoints.size() == 1, "the save placement is a checkpoint")
	_check(builder.npc_count() == 1 and builder.item_count() == 1, "the placements stand")
	_check(builder.get_node("Ledges").get_child_count() == 1, "a ledge run has one-way collision")
	_check(builder.get_node("Lights").get_child_count() == 2, "both lights are placed")
	_check(builder.dialogue_path == "res://dialogue/world_1.json", "and the dialogue is named")
	builder.queue_free()


# ---------- TerrainRules ----------


func test_terrain_rules() -> void:
	# A 3 x 3 block of ground with its top middle cell missing.
	var map := WorldMap.new()
	map.width = 3
	map.height = 3
	map.columns = 64
	map.kinds = {1: WorldMap.KIND_SOLID, 65: WorldMap.KIND_SOLID, 7: WorldMap.KIND_LEDGE}
	map.layer_names = PackedStringArray([WorldMap.LAYER_TERRAIN])
	map.layers[WorldMap.LAYER_TERRAIN] = PackedInt32Array([1, -1, 1, 1, 1, 1, 1, 1, 1])
	var solid := PackedByteArray([1, 0, 1, 1, 1, 1, 1, 1, 1])
	_check(
		(
			TerrainRules.mask_at(solid, 3, 3, 1, 1)
			== TerrainRules.LEFT | TerrainRules.RIGHT | TerrainRules.BOTTOM
		),
		"a mask names the solid sides, the world's edge counting as solid"
	)
	_check(TerrainRules.mask_at(solid, 3, 3, 0, 0) == 11, "a corner cell has the edge on two sides")
	var rules := TerrainRules.infer(map)
	_check(rules.get(1) == 15, "a tile's rule is its most common surroundings")
	_check(rules.get(65) == 15, "a solid tile never painted takes its column's rule")
	_check(not rules.has(7), "a ledge has no rule")
	_check(
		is_equal_approx(TerrainRules.fit(map, rules), 5.0 / 8.0),
		"fit is the share of cells that follow their rule"
	)


# ---------- TileIndex ----------


func test_tile_index() -> void:
	var plain := TileIndex.of(null, 64)
	_check(plain.plain, "no TileSet is the sheet's own grid")
	_check(plain.locate(70) == [0, Vector2i(6, 1)], "where source 0 holds the diffuse sheet")
	_check(plain.locate(70, true) == [1, Vector2i(6, 1)], "and source 1 the glow sheet")
	_check(
		plain.id_of(1, Vector2i(6, 1)) == 70 and plain.id_of(2, Vector2i.ZERO) == -1, "both ways"
	)
	var cut := TileIndex.new()
	cut.columns = 64
	cut.add_source(3, PackedInt32Array([10, 11]))
	_check(not cut.plain, "a source with sheet rows is a cut-up TileSet")
	_check(cut.locate(10 * 64 + 5) == [3, Vector2i(5, 0)], "a row's tiles reflow 16 wide")
	_check(cut.locate(11 * 64 + 20) == [3, Vector2i(4, 5)], "the next row after it")
	_check(cut.locate(11 * 64 + 20, true) == [3, Vector2i(4, 13)], "the glow 8 rows further down")
	_check(cut.locate(12 * 64) == [-1, Vector2i(-1, -1)], "a row no source holds is nowhere")
	_check(cut.id_of(3, Vector2i(4, 5)) == 11 * 64 + 20, "coords name the id")
	_check(cut.id_of(3, Vector2i(4, 13)) == 11 * 64 + 20, "glow coords the same id")
	_check(cut.is_glow(3, Vector2i(4, 13)) and not cut.is_glow(3, Vector2i(4, 5)), "told apart")
	_check(cut.id_of(3, Vector2i(0, 20)) == -1 and cut.id_of(4, Vector2i.ZERO) == -1, "or -1")


# ---------- FirestoreRest.prune_files ----------


func _has_box(outlines: Array[Rect2], box: Rect2) -> bool:
	var slack := LevelBuilder.SPRITE_SMOOTH_PX
	for r in outlines:
		if r.position.distance_to(box.position) <= slack and r.end.distance_to(box.end) <= slack:
			return true
	return false


func test_drawn_shapes() -> void:
	var shape := [[[0, 4], [16, 2], [16, 16], [0, 16]], [[1, 1], [2, 2]], "junk"]
	var plain := LevelBuilder.drawn_shapes(shape, 32.0, false)
	_check(plain.size() == 1, "a drawn shape becomes a polygon; one with two points is left out")
	_check(plain[0][1] == Vector2(16, 2), "and keeps its points")
	var mirrored := LevelBuilder.drawn_shapes(shape, 32.0, true)
	_check(mirrored[0][0] == Vector2(32, 4), "a mirrored sprite mirrors its shape")
	_check(LevelBuilder.drawn_shapes(null, 32.0, false).is_empty(), "no shape is no polygon")
	# Built into a level, the drawn shape replaces the traced outline.
	var builder := LevelBuilder.new()
	add_child(builder)
	var map := WorldMap.new()
	map.width = 8
	map.height = 4
	map.sprites = [
		{
			"id": 1,
			"x": 10,
			"y": 20,
			"columns": 2,
			"rows": 1,
			"solid": true,
			"tiles": [-1, -1],
			"shape": [[[0, 4], [32, 6], [32, 16], [0, 16]]],
		}
	]
	builder._solids = StaticBody2D.new()
	builder.add_child(builder._solids)
	builder._add_sprite_collision(
		map.sprites[0], Vector2(10, 20), 2, 1, false, PackedInt32Array([-1, -1]), map, null
	)
	var polys := builder._solids.get_children()
	_check(polys.size() == 1, "a sprite with a drawn shape gets that one polygon")
	_check(
		polys[0] is CollisionPolygon2D and polys[0].polygon[1] == Vector2(32, 6),
		"made of the drawn points, not a trace"
	)
	# A copy resized in the editor (scale) collides at that size, from its top-left corner.
	builder._add_sprite_collision(
		map.sprites[0], Vector2(10, 20), 2, 1, false, PackedInt32Array([-1, -1]), map, null, 2.0
	)
	var scaled: Node = builder._solids.get_child(builder._solids.get_child_count() - 1)
	_check(scaled.polygon[1] == Vector2(64, 12), "a resized sprite scales its shape")
	_check(scaled.position == Vector2(10, 20), "around its top-left corner")
	builder.queue_free()


func test_parallax_sprites() -> void:
	var builder := LevelBuilder.new()
	add_child(builder)
	builder._make_containers()
	var map := WorldMap.new()
	map.width = 8
	map.height = 4
	var shape := [[[0, 0], [32, 0], [32, 16], [0, 16]]]
	map.sprites = [
		{
			"id": 1,
			"x": 400,
			"y": 64,
			"columns": 2,
			"rows": 1,
			"solid": true,
			"shape": shape,
			"tiles": [-1, -1],
			"parallax": {"scroll": [0.2, 1.0], "repeat_x": true},
		},
		{
			"id": 2,
			"x": 0,
			"y": 0,
			"columns": 2,
			"rows": 1,
			"scale": 2.0,
			"tiles": [-1, -1],
			"parallax": {"scroll": [1.2, 1.0]},
		},
		{
			"id": 3,
			"x": 8,
			"y": 8,
			"columns": 2,
			"rows": 1,
			"solid": true,
			"shape": shape,
			"tiles": [-1, -1]
		},
	]
	builder._build_sprites(map, TileSet.new())
	var back := builder.get_node("ParallaxBack/Sprite1Parallax") as ParallaxSprite
	var front := builder.get_node_or_null("ParallaxFront/Sprite2Parallax") as ParallaxSprite
	_check(
		back != null and back.scroll_scale == Vector2(0.2, 1.0),
		"a parallax sprite lagging the camera goes behind the level"
	)
	_check(front != null, "one running ahead goes in front")
	_check(builder.get_node("ParallaxFront").z_index > 0, "drawn over the hero")
	_check(
		builder.has_node("Tiles/Sprite3") and not builder.has_node("Tiles/Sprite1"),
		"plain sprites stay with the tiles"
	)
	if back:
		_check(
			back.get_node("Sprite1").position == Vector2.ZERO,
			"its layer sits at the ParallaxSprite's origin"
		)
		_check(
			back.at == Vector2(400, 64) and back.size == Vector2(32, 16),
			"which knows where it was placed"
		)
		_check(back.repeat_size == Vector2(32, 0), "repeat_x tiles it sideways only")
		_check(back.repeat_times >= 2, "with copies enough to cover the view")
		_check(
			back.physics_interpolation_mode == Node.PHYSICS_INTERPOLATION_MODE_OFF,
			"it moves every frame, so physics interpolation leaves it alone"
		)
		# Centred on the sprite, Parallax2D's offset puts it where it was placed.
		var viewport := Vector2(320, 180)
		back.fit(viewport)
		var centre := back.at + back.size / 2.0
		var placed := (
			back.scroll_offset + (centre - viewport / 2.0) * (Vector2.ONE - back.scroll_scale)
		)
		_check(placed.is_equal_approx(back.at), "centred on it, it sits where the editor shows it")
	if front:
		_check(
			front.size == Vector2(64, 32) and front.repeat_size == Vector2.ZERO,
			"scaled, without repeats"
		)
	_check(builder._solids.get_child_count() == 1, "only the plain solid sprite collides")
	builder.queue_free()


func test_smooth_outline() -> void:
	# A ledge whose top is a staircase of half-pixel steps, as the tracing
	# makes of a painted top that is nearly flat.
	var stairs := PackedVector2Array([Vector2(0, 10)])
	for i in 8:
		stairs.append(Vector2(i * 12 + 12, 10 - i * 0.5))
		stairs.append(Vector2(i * 12 + 12, 10 - (i + 1) * 0.5))
	stairs.append_array([Vector2(100, 6), Vector2(100, 40), Vector2(0, 40)])
	var smooth := LevelBuilder.smooth_outline(stairs, 1.5)
	var vertical := 0
	for i in smooth.size():
		var a := smooth[i]
		var b := smooth[(i + 1) % smooth.size()]
		if absf(a.x - b.x) < 0.01 and absf(a.y - b.y) < 4.0:
			vertical += 1
	_check(vertical == 0, "half-pixel steps merge into a slope (no short vertical edges left)")
	_check(smooth.size() < stairs.size(), "the outline has fewer points")
	var furthest := 0.0
	for p in stairs:
		furthest = maxf(furthest, _distance_to_outline(p, smooth))
	_check(furthest <= 1.5 + 0.001, "no traced point is further than the tolerance from it")
	var box := PackedVector2Array([Vector2(0, 0), Vector2(30, 0), Vector2(30, 20), Vector2(0, 20)])
	_check(LevelBuilder.smooth_outline(box, 1.5) == box, "a box keeps its corners")
	var tall := PackedVector2Array(
		[
			Vector2(0, 20),
			Vector2(10, 20),
			Vector2(10, 0),
			Vector2(20, 0),
			Vector2(20, 30),
			Vector2(0, 30)
		]
	)
	_check(
		LevelBuilder.smooth_outline(tall, 1.5).size() == 6,
		"a real step taller than the tolerance stays"
	)


func _distance_to_outline(p: Vector2, outline: PackedVector2Array) -> float:
	var best := INF
	for i in outline.size():
		var on := Geometry2D.get_closest_point_to_segment(
			p, outline[i], outline[(i + 1) % outline.size()]
		)
		best = minf(best, p.distance_to(on))
	return best


func test_prune_files() -> void:
	var dir := DIR + "/prune"
	for name in [
		"world.json",
		"map.json",
		"regions/r1/sky.png",
		"regions/r1/sky.png.import",
		"regions/r1/parallax.json",
		"regions/r2/parallax.json",
		"world_tiles.png",
		"world_tiles.png.import",
	]:
		FirestoreRest.write_file(dir + "/" + name, "x".to_utf8_buffer())
	var files := [{"name": "world.json"}, {"name": "regions/r2/parallax.json"}, "junk"]
	var removed := FirestoreRest.prune_files(dir, files, ["map.json"])
	removed.sort()
	_check(
		(
			removed
			== [
				"regions/r1/parallax.json",
				"regions/r1/sky.png",
				"regions/r1/sky.png.import",
				"world_tiles.png",
				"world_tiles.png.import"
			]
		),
		"files the publication no longer lists go, their .import sidecars with them"
	)
	_check(FileAccess.file_exists(dir + "/world.json"), "a listed file stays")
	_check(FileAccess.file_exists(dir + "/map.json"), "a kept file stays")
	_check(
		FileAccess.file_exists(dir + "/regions/r2/parallax.json"),
		"a listed file in a subfolder stays"
	)
	_check(not DirAccess.dir_exists_absolute(dir + "/regions/r1"), "an emptied folder goes")
	_check(DirAccess.dir_exists_absolute(dir + "/regions"), "a folder with a listed file stays")
	_check(
		FirestoreRest.prune_files(DIR + "/prune/none", files).is_empty(),
		"a missing folder is nothing to prune"
	)


# ---------- MapStore ----------


func test_map_store() -> void:
	_check(MapStore.level_id("abc") == "map_abc", "a published map runs as map_<id>")
	_check(MapStore.is_safe_id("mvk5yxk4zvw5bw"), "a library id is a safe map id")
	_check(MapStore.is_safe_id("map_1-2"), "dashes and underscores are safe")
	for bad in ["", "..", "../x", "a/b", "a\\b", "a b", ".hidden"]:
		_check(not MapStore.is_safe_id(bad), "'%s' is not a safe map id" % bad)
	_check(not MapStore.running, "the store is idle without --map")
	# The cache is read from user://maps; a stamp without a world.json does not count.
	var store_dir := MapStore.MAPS_DIR
	var before := MapStore.cached().size()
	var stamp := {
		"mapId": "unit", "name": "Unit Map", "revision": "r1", "publishedAt": 1, "artTile": 16
	}
	FirestoreRest.write_file(store_dir + "/unit/map.json", JSON.stringify(stamp).to_utf8_buffer())
	_check(MapStore.cached().size() == before, "a stamp without a world.json is not a cached map")
	FirestoreRest.write_file(store_dir + "/unit/world.json", "{}".to_utf8_buffer())
	var cached := MapStore.cached()
	_check(cached.size() == before + 1, "a stamp with a world.json is a cached map")
	var mine := {}
	for entry in cached:
		if entry.get("mapId") == "unit":
			mine = entry
	_check(mine.get("name") == "Unit Map", "the cached entry carries the stamp")
	_check(mine.get("cached") == true, "and is marked cached")
	_check(mine.get("path") == store_dir + "/unit/world.json", "and points at its world.json")
	MapStore.register_cached("unit")
	_check(
		Levels.path("map_unit") == store_dir + "/unit/world.json",
		"a downloaded folder registers its level from the stamp"
	)
	# Forgetting it removes the folder and the level.
	var removed := false
	var dir := ProjectSettings.globalize_path(store_dir + "/unit")
	if DirAccess.dir_exists_absolute(dir):
		# OS.move_to_trash needs a desktop; clear the folder by hand here.
		for file in DirAccess.get_files_at(dir):
			DirAccess.remove_absolute(dir.path_join(file))
		removed = DirAccess.remove_absolute(dir) == OK
	Levels.unregister("map_unit")
	_check(removed and not Levels.has("map_unit"), "a forgotten map leaves no folder or level")
	_check(MapStore.cached().size() == before, "and is no longer cached")


# ---------- Backdrop ----------


func test_backdrop_lookup() -> void:
	# A region's set next to the map wins over the shared assets folder.
	var map_dir := DIR + "/with_backdrop"
	var region_dir := map_dir + "/regions/test_land"
	var image := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	image.fill(Color.RED)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(region_dir))
	image.save_png(ProjectSettings.globalize_path(region_dir + "/sky.png"))
	FirestoreRest.write_file(
		region_dir + "/parallax.json",
		(
			JSON
			. stringify({"art_tile": 16, "layers": [{"file": "sky.png", "cover": true}]})
			. to_utf8_buffer()
		)
	)
	var backdrop := Backdrop.new()
	add_child(backdrop)
	var region := {"name": "1. Test Land", "y0": 0, "y1": 32}
	backdrop.show_region(region, 16, map_dir)
	_check(backdrop.get_child_count() == 1, "a region's own parallax set is drawn")
	backdrop.show_region(region, 16, map_dir)
	_check(backdrop.get_child_count() == 1, "showing the same region again draws nothing new")
	backdrop.show_region({"name": "Nowhere", "y0": 0, "y1": 32}, 16, map_dir)
	_check(backdrop.get_child_count() == 1, "a region without a set only fades the old one out")
	backdrop.queue_free()


func test_backdrop_layers() -> void:
	var map_dir := DIR + "/with_layers"
	var region_dir := map_dir + "/regions/layered"
	var image := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	image.fill(Color.WHITE)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(region_dir))
	image.save_png(ProjectSettings.globalize_path(region_dir + "/far.png"))
	image.save_png(ProjectSettings.globalize_path(region_dir + "/fog.png"))
	var spec := {
		"art_tile": 64,
		"layers":
		[
			{"file": "far.png", "scroll": [0.2, 0.1]},
			{
				"file": "fog.png",
				"scroll": [0.6, 1.0],
				"drift": [6, 0],
				"sway": [8, 9],
				"blend": "add",
				"lit": true,
				"repeat_y": true,
				"depth": 0.0,
				"tint": "#ff8800",
			},
			{"file": "fog.png", "cover": true, "blend": "screen"},
			{"file": "missing.png"},
		]
	}
	FirestoreRest.write_file(region_dir + "/parallax.json", JSON.stringify(spec).to_utf8_buffer())
	var backdrop := Backdrop.new()
	add_child(backdrop)
	var region := {"name": "Layered", "y0": 0, "y1": 64, "sky_bottom": "#0000ff"}
	backdrop.show_region(region, 16, map_dir)
	_check(backdrop.get_child_count() == 3, "a layer without its PNG is skipped")
	var far: BackdropLayer = backdrop.get_child(0)
	var fog: BackdropLayer = backdrop.get_child(1)
	var cover: BackdropLayer = backdrop.get_child(2)
	_check(
		far.depth < 0.0 and is_equal_approx(far.effective_depth(), 0.4),
		"a far layer's depth defaults to half of what it does not follow"
	)
	_check(
		far.effective_tint().is_equal_approx(Color.WHITE.lerp(Color.BLUE, 0.4)),
		"and is pulled towards the sky colour"
	)
	_check(
		far.drift == Vector2.ZERO and far.sway == Vector2.ZERO and not far.animated(),
		"a plain layer stands still"
	)
	var far_material := far.material as CanvasItemMaterial
	_check(
		(
			far.light_mask == 0
			and far_material != null
			and far_material.light_mode == CanvasItemMaterial.LIGHT_MODE_UNSHADED
		),
		"and the lights pass it by"
	)
	_check(
		far.texture_filter == CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS,
		"painted art is filtered with mipmaps"
	)
	_check(
		fog.drift == Vector2(6, 0) and fog.sway == Vector2(8, 9) and fog.animated(),
		"drift and sway are read"
	)
	_check(
		fog.motion(0.0) == Vector2.ZERO and is_equal_approx(fog.motion(4.5).x, 27.0),
		"a layer drifts at its speed and sways back through the middle"
	)
	var fog_material := fog.material as CanvasItemMaterial
	_check(
		(
			fog.light_mask == 1
			and fog_material.blend_mode == CanvasItemMaterial.BLEND_MODE_ADD
			and fog_material.light_mode == CanvasItemMaterial.LIGHT_MODE_NORMAL
		),
		"a lit additive layer keeps the lights"
	)
	_check(
		fog.repeat_y and fog.depth == 0.0 and fog.tint == Color("#ff8800"),
		"repeat_y and an explicit depth are read"
	)
	_check(
		(
			cover.effective_depth() == 0.0
			and is_equal_approx(cover.effective_tint().a, Backdrop.SCREEN_ALPHA)
		),
		"a cover layer has no depth; a screen blend is additive at a lower alpha"
	)
	_check(
		Backdrop.layer_depth({"front": true, "scroll": [0.2, 1]}) == 0.0,
		"a front layer has no depth"
	)
	backdrop.queue_free()


func test_props() -> void:
	var prop_path := "res://scenes/props/paradise/hanging_lantern.tscn"
	var prop: WorldProp = load(prop_path).instantiate()
	prop.position = Vector2(32, 48)
	var spec := prop.to_spec(Vector2(0, 800))
	_check(
		(
			spec["scene"] == prop_path
			and spec["x"] == 32.0
			and spec["y"] == 848.0
			and not spec.has("scale")
		),
		"a prop's spec is its scene and its place in the world"
	)
	prop.scale = Vector2(-1, 1)
	_check(prop.to_spec(Vector2.ZERO).get("scale") == [-1.0, 1.0], "a flipped prop says so")
	_check(
		prop.lit_sprites().size() == 1 and prop.lit_sprites()[0].name == "Art",
		"the art is lit, the glow is not"
	)
	prop.free()
	var map := WorldMap.new()
	map.width = 4
	map.height = 4
	map.layer_names = PackedStringArray(["Terrain"])
	map.layers["Terrain"] = PackedInt32Array(
		[-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1]
	)
	map.props = [
		{"scene": prop_path, "x": 10, "y": 20, "scale": [-1, 1]},
		{"scene": "res://scenes/props/paradise/nowhere.tscn", "x": 0, "y": 0},
	]
	var builder := LevelBuilder.new()
	add_child(builder)
	builder.streaming = false
	builder.build(map)
	var holder := builder.get_node_or_null("Tiles/Props")
	_check(
		holder != null and holder.get_child_count() == 1,
		"a prop is instanced, a missing scene skipped"
	)
	if holder and holder.get_child_count() == 1:
		var placed: WorldProp = holder.get_child(0)
		_check(
			placed.position == Vector2(10, 20) and placed.scale == Vector2(-1, 1),
			"where and how it was placed"
		)
		_check(
			(
				placed.get_node("Art").material == builder.rim
				and placed.get_node("Glow").material != builder.rim
			),
			"its art wears the rim light, its glow keeps its own blend"
		)
	builder.queue_free()
	var world := SceneWorld.read("res://scenes/world/world.tscn")
	var in_paradise := 0
	for placed in world.props if world else []:
		if (
			float(placed["y"]) < 816.0
			and str(placed["scene"]).begins_with("res://scenes/props/paradise/")
		):
			in_paradise += 1
	_check(in_paradise == 115, "Paradise's scene places its 115 props (%d)" % in_paradise)


func test_backdrop_from_scene() -> void:
	var image := Image.create(8, 4, false, Image.FORMAT_RGBA8)
	image.fill(Color.WHITE)
	var texture := ImageTexture.create_from_image(image)
	var entry := {
		"texture": texture,
		"scroll": [0.3, 0.8],
		"y": 24,
		"art_tile": 64,
		"drift": [2, 0],
		"blend": "add",
		"lit": true,
		"depth": 0.25,
		"tint": "#80ff00",
	}
	var layer := BackdropLayer.from_spec(entry, texture, 16)
	var back := layer.to_spec()
	layer.free()
	var same := true
	for key in entry:
		if key == "texture":
			same = same and back.get(key) == texture
		elif entry[key] is Array:
			same = (
				same
				and Vector2(back[key][0], back[key][1]) == Vector2(entry[key][0], entry[key][1])
			)
		else:
			same = same and is_equal_approx(float(back.get(key, -1)), float(entry[key]))
	_check(
		same and not back.has("cover") and not back.has("repeat_y"), "a layer's spec round-trips"
	)
	var region := {
		"name": "Scened",
		"y0": 200,
		"y1": 300,
		"sky_bottom": "#0000ff",
		"parallax":
		{
			"art_tile": 64,
			"layers":
			[
				{"texture": texture, "scroll": [0.3, 0.8], "y": 24},
				{"texture": texture, "cover": true},
				{"file": "nowhere.png"},
			]
		},
	}
	var backdrop := Backdrop.new()
	add_child(backdrop)
	backdrop.show_region(region, 16, "res://nowhere")
	_check(backdrop.get_child_count() == 2, "a region's scene layers are drawn, not its JSON")
	var far: BackdropLayer = backdrop.get_child(0)
	_check(
		far.position.y == 224.0 and far.texture == texture,
		"a layer sits at its y from the region's top"
	)
	_check(
		far.tile == 16 and is_equal_approx(far.scale_factor(), 0.25),
		"painted art is scaled to the grid"
	)
	_check(
		far.sky == "#0000ff" and far.effective_tint() != Color.WHITE, "the sky's haze is applied"
	)
	backdrop.show_region(region, 16, "res://nowhere")
	_check(backdrop.get_child_count() == 2, "showing the same scene region again draws nothing new")
	backdrop.queue_free()
	var world := SceneWorld.read("res://scenes/world/world.tscn")
	var paradise := world.region_at(0.0) if world else {}
	var layers: Array = paradise.get("parallax", {}).get("layers", [])
	_check(
		layers.size() == 7 and layers[0].get("cover", false) and layers[6].get("front", false),
		"Paradise's scene carries its backdrop: 7 layers, the sky first and the wisps in front"
	)


# ---------- Fringes and Fx from a scene ----------


func test_fringes_and_fx_from_scene() -> void:
	var image := Image.create(8, 4, false, Image.FORMAT_RGBA8)
	image.fill(Color.WHITE)
	var texture := ImageTexture.create_from_image(image)
	var fringes := RegionFringes.from_spec(
		{
			"art_tile": 64,
			"top": [texture, texture],
			"left": [texture],
			"chance": 0.5,
			"top_chance": 0.9
		}
	)
	var spec := fringes.to_spec()
	fringes.free()
	_check(
		(
			spec["art_tile"] == 64
			and spec["top"].size() == 2
			and spec["left"] == [texture]
			and not spec.has("bottom")
			and is_equal_approx(spec["top_chance"], 0.9)
			and is_equal_approx(spec["left_chance"], 0.5)
		),
		"a fringes node's spec round-trips, a side without its own chance taking the shared one"
	)
	var solid := PackedByteArray([0, 0, 0, 0, 1, 0, 0, 0, 0])
	var pieces := EdgeDecor.pieces_in(solid, 3, 3, Rect2i(0, 0, 3, 3), spec, 16)
	var tops := pieces.filter(func(piece: Array) -> bool: return piece[1].y < 16.0)
	_check(
		pieces.size() == 2 and tops.size() == 1 and tops[0][1] == Vector2(23.0, 15.0),
		"a lone solid cell gets a tuft on top and ivy on its left, the tuft standing on the cell"
	)
	var snow := RegionParticles.from_spec({"preset": "snow", "amount": 5}, null, 64)
	var entry := snow.to_spec()
	snow.free()
	_check(
		(
			entry["preset"] == "snow"
			and entry["amount"] == 5
			and entry["color"] == "#f4f8ff"
			and is_equal_approx(entry["sway"], 5.0)
			and not entry.has("texture")
		),
		"a preset particle node takes the preset's defaults and keeps its own amount"
	)
	var fx := RegionFx.new()
	add_child(fx)
	var region := {
		"name": "Scened",
		"particles": ["embers"],
		"fx":
		{
			"art_tile": 64,
			"particles": [{"texture": texture, "amount": 7}, {"preset": "ash"}, {"preset": "nope"}]
		},
	}
	fx.show_region(region, 16, "res://nowhere")
	var emitters := fx.get_children()
	_check(
		(
			emitters.size() == 2
			and emitters[0].amount == 7
			and is_equal_approx(emitters[0].scale_amount_max, 0.25)
			and emitters[0].texture == texture
		),
		"a region's scene particles are emitted, not its presets list, the art scaled to the grid"
	)
	fx.queue_free()
	var world := SceneWorld.read("res://scenes/world/world.tscn")
	var paradise := world.region_at(0.0) if world else {}
	var edges: Dictionary = paradise.get("edges", {})
	_check(
		(
			edges.get("top", []).size() == 4
			and edges.get("bottom", []).size() == 3
			and is_equal_approx(float(edges.get("top_chance", 0.0)), 0.9)
			and edges.get("top", [null])[0] is Texture2D
		),
		"Paradise's scene carries its fringes: four tufts, three undersides, with their chances"
	)
	var particles: Array = paradise.get("fx", {}).get("particles", [])
	_check(
		particles.size() == 2 and particles[0].get("amount") == 36 and particles[1].has("texture"),
		"Paradise's scene carries its particles: the petals and the motes"
	)


# ---------- RegionFx ----------


func test_region_fx() -> void:
	_check(
		RegionFx.PRESETS.has("embers") and RegionFx.PRESETS.size() == 7,
		"seven stock particle presets"
	)
	var spec := RegionFx.region_spec({"particles": ["embers", "nothing", "snow"]})
	_check(
		spec["particles"] == [{"preset": "embers"}, {"preset": "snow"}],
		"a region's particles list names presets, unknown ones dropped"
	)
	_check(
		(
			RegionFx.preset_texture("rain").get_size() == Vector2(1, 7)
			and RegionFx.preset_texture("birds").get_width() == 7
		),
		"presets draw their own sprites"
	)
	var fx := RegionFx.new()
	add_child(fx)
	fx.show_region({"name": "Nowhere Special", "particles": ["embers", "dust"]}, 16)
	_check(fx.get_child_count() == 2, "a region with no fx.json gets its presets")
	var map_dir := DIR + "/with_fx"
	var region_dir := map_dir + "/regions/foggy"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(region_dir))
	var own := {"art_tile": 16, "particles": [{"preset": "snow", "amount": 7}, {"preset": "none"}]}
	FirestoreRest.write_file(region_dir + "/fx.json", JSON.stringify(own).to_utf8_buffer())
	fx.show_region({"name": "Foggy", "particles": ["embers"]}, 16, map_dir)
	var emitters := fx.get_children().filter(func(c: Node) -> bool: return c.emitting)
	_check(
		emitters.size() == 1 and emitters[0].amount == 7,
		"the map's own fx.json wins over the region's list and tunes the preset"
	)
	# Freed now, not at exit: the particles' meshes would be reported as leaked.
	remove_child(fx)
	fx.free()


# ---------- RegionLook ----------


func test_region_look_resource() -> void:
	var spec := {
		"ambient": "#102030",
		"sky_top": "#405060",
		"sky_bottom": "#708090",
		"cave": true,
		"light": "#ffffff",
		"grade": {"tint": "#ffe0c0", "saturation": 0.5},
		"music": "hymn",
	}
	var look := RegionLook.from_spec(spec)
	_check(
		(
			look.own_sky
			and look.cave
			and look.light == Color.WHITE
			and look.ambient == Color("#102030")
		),
		"a look takes an exported region's fields"
	)
	_check(look.to_spec() == spec, "a look's spec round-trips, with only the fields that are set")
	var plain := RegionLook.new().to_spec()
	_check(
		plain.keys() == ["ambient"] and plain["ambient"] == "#c2cce6",
		"a default look is only its ambient"
	)
	_check(
		(
			LookBlend.music_of({"name": "1. Limbo"}) == "limbo"
			and LookBlend.music_of({"name": "1. Limbo", "music": "hymn"}) == "hymn"
		),
		"a region's music is its own loop, else its key"
	)
	var world := SceneWorld.read("res://scenes/world/world.tscn")
	var paradise := world.region_at(0.0) if world else {}
	var saved := load("res://resources/regions/paradise.tres") as RegionLook
	_check(
		saved != null and paradise.get("ambient") == "#" + saved.ambient.to_html(false),
		"Paradise's ambient comes from resources/regions/paradise.tres"
	)


# ---------- LookBlend ----------


func test_region_look() -> void:
	var fallback: Array[Color] = [Color.RED, Color.GREEN, Color.BLUE]
	var sky := LookBlend.sky_of({}, fallback)
	_check(
		sky[0] == Color.RED and sky[1] == Color.BLUE,
		"a region without sky colours takes the level's top and bottom"
	)
	sky = LookBlend.sky_of({"sky_top": "#102030", "sky_bottom": "nonsense"}, fallback)
	_check(
		sky[0] == Color("#102030") and sky[1] == Color.BLUE,
		"its own colours win, bad ones fall back"
	)
	_check(
		(
			LookBlend.rim_of({"sky_top": "#102030"}, fallback) == Color("#102030")
			and LookBlend.rim_of({"light": "#ffffff"}, fallback) == Color.WHITE
		),
		"the rim is the region's light, else its sky top"
	)
	_check(
		LookBlend.ambient_of({"ambient": "#808080"}, fallback) == Color("#808080"),
		"an explicit ambient is used as it is"
	)
	var derived := LookBlend.ambient_of({"sky_top": "#ffffff", "sky_bottom": "#ffffff"}, fallback)
	_check(derived.is_equal_approx(Color(0.6, 0.6, 0.6)), "a derived ambient sits below the sky")
	var grade := LookBlend.grade_of({})
	_check(
		grade["tint"] == Color.WHITE and grade["contrast"] == 1.0 and grade["saturation"] == 1.0,
		"no grade leaves the view alone"
	)
	grade = LookBlend.grade_of({"grade": {"tint": "#ffe0c0", "contrast": 9, "saturation": 0.5}})
	_check(
		(
			grade["tint"] == Color("#ffe0c0")
			and grade["contrast"] == 3.0
			and grade["saturation"] == 0.5
		),
		"a grade is read and clamped"
	)
	_check(
		(
			LookBlend.descent_factor(0.0, 50, 100) == 1.0
			and is_equal_approx(LookBlend.descent_factor(0.6, 50, 100), 0.7)
			and is_equal_approx(LookBlend.descent_factor(0.6, 500, 100), 0.4)
			and LookBlend.descent_factor(0.6, -5, 100) == 1.0
		),
		"the descent darkens evenly to the bottom and no further"
	)
	var look := LookBlend.new()
	look.reset({"sky_top": "#000000", "sky_bottom": "#000000", "y0": 0, "y1": 100}, fallback)
	var next := {
		"sky_top": "#ffffff",
		"sky_bottom": "#ffffff",
		"cave": true,
		"y0": 100,
		"y1": 300,
		"grade": {"contrast": 2},
	}
	look.retarget(next, fallback)
	look.step(LookBlend.FADE / 2.0)
	_check(
		(
			is_equal_approx(look.top.r, 0.5)
			and is_equal_approx(look.cave, 0.5)
			and look.band.is_equal_approx(Vector2(50, 200))
			and is_equal_approx(look.contrast, 1.5)
		),
		"halfway through a fade the look is halfway"
	)
	_check(
		look.step(LookBlend.FADE) and look.top == Color.WHITE and not look.step(0.1),
		"the fade ends on the new look"
	)


func test_post_fx_path() -> void:
	var post := PostFx.new()
	add_child(post)
	post.set_grade(Color(1.0, 0.9, 0.8), 1.0, 1.0)
	_check(not post.reads_screen(), "a tint-only grade draws without reading the screen back")
	post.set_grade(Color.WHITE, 1.2, 1.0)
	_check(post.reads_screen(), "contrast needs the screen")
	post.set_grade(Color.WHITE, 1.0, 0.8)
	_check(post.reads_screen(), "and so does saturation")
	post.set_grade(Color.WHITE, 1.0, 1.0)
	_check(not post.reads_screen(), "back to the cheap path once the grade is plain again")
	post.queue_free()


func test_region_key() -> void:
	_check(EdgeDecor.region_key("1. Paradise") == "paradise", "region keys drop the number")
	_check(
		EdgeDecor.region_key("Ruined Earth") == "ruined_earth", "and join words with underscores"
	)
	_check(EdgeDecor.region_key(" 7. Violence ") == "violence", "and trim spaces")


# ---------- UI components ----------


func test_ui_component_rules() -> void:
	var fill := {
		"kind": "fill",
		"x": 10,
		"y": 2,
		"w": 100,
		"h": 8,
		"dir": "right",
		"bind": {"value": "hp", "max": "top", "visible": "", "color": ""},
	}
	var node := UiComponent.new()
	node.spec = {
		"w": 200,
		"h": 20,
		"props":
		[
			{"name": "hp", "type": "number", "default": 3},
			{"name": "top", "type": "number", "default": 4},
			{"name": "label", "type": "text", "default": "HP"},
			{"name": "show", "type": "bool", "default": false},
		],
		"parts": [fill],
	}
	node.values = {"hp": 3.0, "top": 4.0, "label": "HP", "show": false}
	_check(is_equal_approx(node.fill_ratio(fill), 0.75), "a fill's ratio is its value over its max")
	node.values["top"] = 0.0
	_check(node.fill_ratio(fill) == 0.0, "a zero max is an empty bar")
	_check(
		UiComponent.fill_rect(fill, 0.5) == Rect2(10, 2, 50, 8), "a right fill grows from the left"
	)
	fill["dir"] = "up"
	_check(
		UiComponent.fill_rect(fill, 0.25) == Rect2(10, 8, 100, 2),
		"an up fill grows from the bottom"
	)
	node.values["hp"] = 3.456
	_check(
		node.format_text("{label} {hp}/{missing}") == "HP 3.46/", "text fills in props and rounds"
	)
	node.values["hp"] = 5.0
	_check(node.format_text("{hp}") == "5", "whole numbers show no decimals")
	_check(not node.is_visible_part({"bind": {"visible": "show"}}), "a false bool hides a part")
	node.values["show"] = true
	_check(node.is_visible_part({"bind": {"visible": "show"}}), "a true bool shows it")
	var nine := UiComponent.pieces("nine", 30, 30, Rect2(0, 0, 100, 40), [10, 10, 10, 10])
	_check(nine.size() == 9, "nine-slicing makes nine pieces")
	_check(nine[0][1] == Rect2(0, 0, 10, 10), "the corners keep their size")
	_check(nine[4][1] == Rect2(10, 10, 80, 20), "the middle stretches")
	var tiles := UiComponent.pieces("tile", 16, 16, Rect2(0, 0, 40, 16), [])
	_check(tiles.size() == 3 and tiles[2][1] == Rect2(32, 0, 8, 16), "tiling cuts the last tile")
	var cut := UiComponent.clip_piece(
		Rect2(0, 0, 200, 20), Rect2(0, 0, 100, 10), Rect2(0, 0, 50, 10)
	)
	_check(
		cut[0] == Rect2(0, 0, 100, 20) and cut[1] == Rect2(0, 0, 50, 10),
		"clipping cuts the source by the same share"
	)
	_check(
		(
			UiComponent
			. clip_piece(Rect2(0, 0, 1, 1), Rect2(0, 0, 10, 10), Rect2(20, 0, 5, 5))
			. is_empty()
		),
		"a piece outside the clip is dropped"
	)
	node.free()


func test_component_store() -> void:
	_check(
		ComponentStore.key("Health Bar") == ComponentStore.key("health_bar"),
		"component names match loosely"
	)
	_check("HealthBar" in ComponentStore.names(), "the shipped HealthBar is available")
	var bar := ComponentStore.create("health bar", 110.0)
	_check(bar != null, "a component is created by name")
	if bar == null:
		return
	_check(bar.has_prop("health") and bar.has_prop("maxHealth"), "it has the health props")
	_check(is_equal_approx(bar.size.x, 110.0), "it is drawn at the asked width")
	_check(is_equal_approx(bar.size.y, 110.0 * 90.0 / 420.0), "its height keeps its shape")
	bar.set_prop("health", 2.0)
	bar.set_prop("maxHealth", 4.0)
	var fill: Dictionary = {}
	for part: Dictionary in bar.spec["parts"]:
		if part["kind"] == "fill":
			fill = part
	_check(is_equal_approx(bar.fill_ratio(fill), 0.5), "the game's health drives its fill")
	_check(not bar.set_prop("nope", 1), "unknown props are ignored")
	_check(ComponentStore.create("no such component") == null, "an unknown component is null")
	bar.free()


# ---------- harness ----------


func _check(ok: bool, label: String) -> void:
	_checks += 1
	if ok:
		print("  ok   ", label)
	else:
		_failures += 1
		printerr("  FAIL ", label)


func _cleanup() -> void:
	_remove_dir(ProjectSettings.globalize_path(DIR))


func _remove_dir(path: String) -> void:
	var dir := DirAccess.open(path)
	if dir == null:
		return
	for file in dir.get_files():
		DirAccess.remove_absolute(path.path_join(file))
	for sub in dir.get_directories():
		_remove_dir(path.path_join(sub))
	DirAccess.remove_absolute(path)


func _report() -> void:
	if _checks != EXPECTED_CHECKS:
		_failures += 1
		printerr("  FAIL expected %d checks, ran %d" % [EXPECTED_CHECKS, _checks])
	print("%d checks, %d failed" % [_checks, _failures])
	get_tree().quit(1 if _failures > 0 else 0)
