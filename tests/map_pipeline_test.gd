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
## Every check the script makes; fewer at the end means something broke early.
const EXPECTED_CHECKS := 89
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
		map.marker_position(map.markers[0]) == Vector2(8, 16), "markers give their foot position"
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
	# One run of rock along the bottom row, plus a box per solid sprite.
	_check(solids.get_child_count() == 3, "a solid sprite adds one collision box")
	var padded: CollisionShape2D = null
	for child in solids.get_children():
		if child is CollisionShape2D and child.shape.size == Vector2(24, 12):
			padded = child
	_check(padded != null, "a sprite with a hit box collides with its opaque pixels only")
	# Mirrored: the box sits 4 px from the right edge of the 32-px-wide sprite.
	_check(
		padded != null and padded.position == Vector2(100 + 4 + 12, 100 + 20 + 6),
		"and the hit box is mirrored with the sprite"
	)
	var box: CollisionShape2D = null
	for child in solids.get_children():
		if child is CollisionShape2D and child.shape.size == Vector2(32, 16):
			box = child
	_check(box != null, "the box covers the sprite's tiles")
	_check(box != null and box.position == Vector2(36, 12), "and is centred on it")
	_check(builder.player_start == Vector2(8, 16), "the start marker places the hero")
	builder.queue_free()


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
	# Forgetting it removes the folder and the level.
	Levels.register({"id": "map_unit", "name": "Unit Map", "path": store_dir + "/unit/world.json"})
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


func test_region_key() -> void:
	_check(EdgeDecor.region_key("1. Paradise") == "paradise", "region keys drop the number")
	_check(
		EdgeDecor.region_key("Ruined Earth") == "ruined_earth", "and join words with underscores"
	)
	_check(EdgeDecor.region_key(" 7. Violence ") == "violence", "and trim spaces")


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
