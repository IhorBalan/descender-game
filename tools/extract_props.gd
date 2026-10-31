extends SceneTree

## Turns the pieces of a region's set that were painted as tiles into prop
## scenes (see WorldProp): every group of touching cells painted from the
## region's props atlas source on the Decor layer (with the Glow layer's
## cells over them) is one piece; pieces painted from the same tiles in the
## same arrangement are one prop. Each prop gets its PNGs baked from the
## sheets under godot_assets/props/<key>/ (<name>.png, <name>_n.png and, when it
## shines, <name>_glow.png) and a scene under scenes/props/<key>/; the
## region scene then gets an instance per piece under Props/* and loses
## the cells. Names come from godot_assets/props/<key>/names.json ({"<shape
## key>": "lantern", ...}, the shape keys as the first run prints them),
## else the props are numbered.
##
##   godot --headless --path . --script tools/extract_props.gd -- \
##       00_paradise.tscn "Paradise props" [--write]
##
## Without --write it only bakes the PNGs and prints the shape keys, so
## they can be looked at and named; run the import once after
## (`godot --headless --path . --import`), then run it again with --write.

const SCENES_DIR := "res://scenes/world"
const PROPS_SCENES := "res://scenes/props"
const PROPS_ART := "res://godot_assets/props"
const SHEETS_DIR := "res://levels/world"
const DIRECTIONS := [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]


func _initialize() -> void:
	var write := false
	var args: Array[String] = []
	for arg in OS.get_cmdline_user_args():
		if arg == "--write":
			write = true
		else:
			args.append(arg)
	if args.size() < 2:
		printerr("usage: -- <region scene file> <props source name> [--write]")
		quit(1)
		return
	var ok := _extract(SCENES_DIR.path_join(args[0]), args[1], write)
	quit(0 if ok else 1)


## Bakes the props of the region scene at `path` painted from the atlas
## source `source_name`, and with `write` puts them in the scene.
func _extract(path: String, source_name: String, write: bool) -> bool:
	var region := _load_region(path)
	if region == null:
		return false
	var grid := _grid_of(path)
	var sheets := _load_sheets()
	var cells := _collect_cells(region, source_name, grid["columns"])
	if sheets.is_empty() or cells.is_empty():
		region.free()
		return false
	var key := EdgeDecor.region_key(region.region_name)
	var pieces := _pieces(cells)
	var shapes := _bake_all(pieces, key, sheets, grid)
	var ok := not shapes.is_empty()
	if ok and not write:
		print("baked %d props' PNGs; import, name them, run again with --write" % shapes.size())
	elif ok:
		ok = _place(region, path, key, pieces, shapes, grid)
	region.free()
	return ok


## The region scene at `path`, instanced; null, with the error printed.
static func _load_region(path: String) -> WorldRegion:
	var packed := load(path) as PackedScene
	var root := packed.instantiate() if packed else null
	if root is WorldRegion:
		return root
	printerr("%s is not a WorldRegion scene" % path)
	if root:
		root.free()
	return null


## The grid the region is drawn on, from its world scene: the sheet's
## columns, pixels a tile in the sheets (art) and in the level (tile).
static func _grid_of(region_path: String) -> Dictionary:
	var grid := {"columns": 64, "art": 64, "tile": 16}
	var packed := load(region_path.get_base_dir().path_join("world.tscn")) as PackedScene
	var world := packed.instantiate() if packed else null
	if world is WorldScene:
		grid = {"columns": world.columns, "art": world.art_tile, "tile": world.tile}
	if world:
		world.free()
	return grid


## The three sheets as images, by name; empty, with the error printed, when
## one does not read.
static func _load_sheets() -> Dictionary:
	var sheets := {}
	for sheet in [WorldMap.SHEET_DIFFUSE, WorldMap.SHEET_NORMAL, WorldMap.SHEET_GLOW]:
		var image := Image.load_from_file(
			ProjectSettings.globalize_path(SHEETS_DIR.path_join(sheet))
		)
		if image == null:
			printerr("the sheet %s does not read" % sheet)
			return {}
		sheets[sheet] = image
	return sheets


## The Decor cells painted from the source, each cell -> [sheet id, the id
## of the glow cell over it or -1]; empty, with the error printed, when the
## source is not there or nothing is painted from it.
static func _collect_cells(region: WorldRegion, source_name: String, columns: int) -> Dictionary:
	var decor := region.layer("Decor")
	var glow := region.layer("Glow")
	var tile_set := decor.tile_set if decor else null
	var source_id := -1
	for i in tile_set.get_source_count() if tile_set else 0:
		if tile_set.get_source(tile_set.get_source_id(i)).resource_name == source_name:
			source_id = tile_set.get_source_id(i)
	if source_id < 0:
		printerr("%s: no atlas source named '%s'" % [region.region_name, source_name])
		return {}
	var index := TileIndex.of(tile_set, columns)
	var cells := {}
	for cell in decor.get_used_cells():
		if decor.get_cell_source_id(cell) != source_id:
			continue
		var glow_id := -1
		if glow and glow.get_cell_source_id(cell) == source_id:
			glow_id = index.id_of(source_id, glow.get_cell_atlas_coords(cell))
		cells[cell] = [index.id_of(source_id, decor.get_cell_atlas_coords(cell)), glow_id]
	if cells.is_empty():
		printerr("%s: nothing on Decor is painted from '%s'" % [region.region_name, source_name])
	return cells


## The groups of touching cells, each with its origin (its top-left cell),
## its size, its cells as [offset, sheet id, glow id] and a key naming the
## arrangement.
static func _pieces(cells: Dictionary) -> Array[Dictionary]:
	var pieces: Array[Dictionary] = []
	var seen := {}
	var starts := cells.keys()
	starts.sort_custom(
		func(a: Vector2i, b: Vector2i) -> bool: return a.y < b.y or (a.y == b.y and a.x < b.x)
	)
	for start in starts:
		if seen.has(start):
			continue
		var stack: Array[Vector2i] = [start]
		var group: Array[Vector2i] = []
		seen[start] = true
		while not stack.is_empty():
			var cell: Vector2i = stack.pop_back()
			group.append(cell)
			for direction in DIRECTIONS:
				var next: Vector2i = cell + direction
				if cells.has(next) and not seen.has(next):
					seen[next] = true
					stack.append(next)
		var origin := Vector2i(1 << 20, 1 << 20)
		var far := Vector2i(-(1 << 20), -(1 << 20))
		for cell in group:
			origin = Vector2i(mini(origin.x, cell.x), mini(origin.y, cell.y))
			far = Vector2i(maxi(far.x, cell.x), maxi(far.y, cell.y))
		var listed := []
		var parts: Array[String] = []
		for cell in group:
			var offset := cell - origin
			listed.append([offset, cells[cell][0], cells[cell][1]])
			parts.append("%d,%d:%d:%d" % [offset.x, offset.y, cells[cell][0], cells[cell][1]])
		parts.sort()
		var key := ";".join(parts)
		(
			pieces
			. append(
				{
					"origin": origin,
					"size": far - origin + Vector2i.ONE,
					"cells": listed,
					"key": "%08x" % (key.hash() & 0xFFFFFFFF),
				}
			)
		)
	return pieces


## The distinct shapes among the pieces, most placed first, each baked to
## its PNGs and named: shape key -> {cells, size, count, name}. Empty, with
## the error printed, when two shapes were given one name.
static func _bake_all(
	pieces: Array[Dictionary], key: String, sheets: Dictionary, grid: Dictionary
) -> Dictionary:
	var shapes := {}
	for piece in pieces:
		var shape_key: String = piece["key"]
		if not shapes.has(shape_key):
			shapes[shape_key] = {"cells": piece["cells"], "size": piece["size"], "count": 0}
		shapes[shape_key]["count"] += 1
	var names := _names(key)
	var art_dir := PROPS_ART.path_join(key)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(art_dir))
	var ordered := shapes.keys()
	ordered.sort_custom(
		func(a: String, b: String) -> bool: return shapes[a]["count"] > shapes[b]["count"]
	)
	var used := {}
	var number := 0
	for shape_key in ordered:
		number += 1
		var name: String = names.get(shape_key, "prop_%02d" % number)
		if used.has(name):
			printerr("two shapes are named %s" % name)
			return {}
		used[name] = true
		var shape: Dictionary = shapes[shape_key]
		shape["name"] = name
		var shines := _bake(shape, sheets, grid["columns"], grid["art"], art_dir, name)
		var size: Vector2i = shape["size"]
		print(
			(
				"%s  %s  %d x %d cells, %d placed%s"
				% [shape_key, name, size.x, size.y, shape["count"], ", shines" if shines else ""]
			)
		)
	return shapes


## The names given to shapes, from godot_assets/props/<key>/names.json.
static func _names(key: String) -> Dictionary:
	var text := FileAccess.get_file_as_string(PROPS_ART.path_join(key).path_join("names.json"))
	var parsed: Variant = JSON.parse_string(text) if not text.is_empty() else null
	return parsed if parsed is Dictionary else {}


## Writes a shape's PNGs: the diffuse, its normal map and, when any cell
## shines, its glow. True when it shines.
static func _bake(
	shape: Dictionary, sheets: Dictionary, columns: int, art: int, dir: String, name: String
) -> bool:
	var size: Vector2i = shape["size"] * art
	var diffuse := Image.create(size.x, size.y, false, Image.FORMAT_RGBA8)
	var normal := Image.create(size.x, size.y, false, Image.FORMAT_RGBA8)
	var glow := Image.create(size.x, size.y, false, Image.FORMAT_RGBA8)
	var shines := false
	for cell in shape["cells"]:
		var at: Vector2i = Vector2i(cell[0]) * art
		var id: int = cell[1]
		var from := Rect2i((id % columns) * art, (id / columns) * art, art, art)
		diffuse.blit_rect(sheets[WorldMap.SHEET_DIFFUSE], from, at)
		normal.blit_rect(sheets[WorldMap.SHEET_NORMAL], from, at)
		if cell[2] >= 0:
			var glow_id: int = cell[2]
			var glow_from := Rect2i((glow_id % columns) * art, (glow_id / columns) * art, art, art)
			glow.blit_rect(sheets[WorldMap.SHEET_GLOW], glow_from, at)
			shines = true
	var base := ProjectSettings.globalize_path(dir.path_join(name))
	diffuse.save_png(base + ".png")
	normal.save_png(base + "_n.png")
	if shines:
		glow.save_png(base + "_glow.png")
	return shines


## Writes the prop scenes and the region with its pieces as instances of
## them under Props/*, their cells erased from Decor and Glow.
static func _place(
	region: WorldRegion,
	path: String,
	key: String,
	pieces: Array[Dictionary],
	shapes: Dictionary,
	grid: Dictionary
) -> bool:
	var scenes_dir := PROPS_SCENES.path_join(key)
	var prop_scenes := {}
	for shape_key in shapes:
		var name: String = shapes[shape_key]["name"]
		var scene_path := scenes_dir.path_join(name + ".tscn")
		if PropScene.write(key, name, grid["art"], grid["tile"], scene_path).is_empty():
			return false
		prop_scenes[shape_key] = scene_path
	var holder := region.get_node_or_null("Props")
	if holder == null:
		holder = Node2D.new()
		holder.name = "Props"
		region.add_child(holder)
		var tiles := region.get_node_or_null("Tiles")
		if tiles:
			region.move_child(holder, tiles.get_index() + 1)
		holder.owner = region
	var decor := region.layer("Decor")
	var glow := region.layer("Glow")
	for piece in pieces:
		var prop_packed := load(prop_scenes[piece["key"]]) as PackedScene
		var prop := prop_packed.instantiate()
		var base: String = shapes[piece["key"]]["name"].to_pascal_case()
		var prop_name := base
		var n := 2
		while holder.has_node(prop_name):
			prop_name = "%s%d" % [base, n]
			n += 1
		prop.name = prop_name
		prop.position = Vector2(piece["origin"]) * float(grid["tile"])
		holder.add_child(prop)
		prop.owner = region
		for cell in piece["cells"]:
			var at: Vector2i = Vector2i(piece["origin"]) + Vector2i(cell[0])
			decor.erase_cell(at)
			if glow and cell[2] >= 0:
				glow.erase_cell(at)
	# Erased cells leave the layer's data only once it updates, which a
	# headless script never waits a frame for.
	decor.update_internals()
	if glow:
		glow.update_internals()
	var out := PackedScene.new()
	var error := out.pack(region)
	if error == OK:
		error = ResourceSaver.save(out, path)
	if error != OK:
		printerr("%s: could not be written (%s)" % [path, error_string(error)])
		return false
	print(
		(
			"%s: %d pieces as %d props under Props/*, the scenes under %s"
			% [path.get_file(), pieces.size(), shapes.size(), scenes_dir]
		)
	)
	return true
