extends SceneTree

## Puts a region's art data into its scene as nodes: reads
## godot_assets/regions/<key>/parallax.json, edges.json and fx.json (see Backdrop,
## EdgeDecor and RegionFx) and writes into the region's scene under
## scenes/world/ a Backdrop node of BackdropLayer children (back to front),
## a Fringes node (RegionFringes) and an Fx node of RegionParticles
## children, so the editor shows the region the way the game draws it and
## each part can be picked, moved and tuned there. A region whose look is
## not a resource file yet gets one, resources/regions/<key>.tres (see
## RegionLook), from the look it has. A part the scene already has is
## left alone unless `--replace` is given.
##
##   godot --headless --path . --script tools/scene_art.gd -- \
##       [--replace] [00_paradise.tscn ...]
##
## With no scene names, every region scene is done. Run the import first
## (`godot --headless --path . --import`) so the PNGs are resources.

const SCENES_DIR := "res://scenes/world"
const REGIONS_DIR := "res://godot_assets/regions"


func _initialize() -> void:
	var replace := false
	var only: Array[String] = []
	for arg in OS.get_cmdline_user_args():
		if arg == "--replace":
			replace = true
		else:
			only.append(arg)
	var names: Array[String] = []
	for file in DirAccess.get_files_at(SCENES_DIR):
		if file.ends_with(".tscn") and file != "world.tscn" and (only.is_empty() or file in only):
			names.append(file)
	names.sort()
	var failed := false
	for file in names:
		if not _convert(SCENES_DIR.path_join(file), replace):
			failed = true
	quit(1 if failed else 0)


## Writes the region scene at `path` with its art as nodes; false when
## something went wrong (a region without a file is skipped, not an error).
func _convert(path: String, replace: bool) -> bool:
	var packed := load(path) as PackedScene
	var root := packed.instantiate() if packed else null
	if not root is WorldRegion:
		printerr("%s is not a WorldRegion scene" % path)
		if root:
			root.free()
		return false
	var region := root as WorldRegion
	var key := EdgeDecor.region_key(region.region_name)
	var dir := "%s/%s" % [REGIONS_DIR, key]
	var done: Array[String] = []
	var ok := (
		_look(region, key, done)
		and _backdrop(region, dir, replace, done)
		and _fringes(region, dir, replace, done)
		and _fx(region, dir, replace, done)
	)
	var error := OK
	if ok and not done.is_empty():
		var out := PackedScene.new()
		error = out.pack(region)
		if error == OK:
			error = ResourceSaver.save(out, path)
		if error != OK:
			printerr("%s: could not be written (%s)" % [path, error_string(error)])
	if ok and error == OK:
		var what := ", ".join(done) if not done.is_empty() else "nothing to do"
		print("%s (%s): %s" % [path.get_file(), key, what])
	region.free()
	return ok and error == OK


## Writes the region's look as resources/regions/<key>.tres when it is not
## a resource file yet (an inline resource, or none).
static func _look(region: WorldRegion, key: String, done: Array[String]) -> bool:
	var look := region.look if region.look else RegionLook.new()
	if look.resource_path.begins_with(RegionLook.DIR):
		return true
	var written := RegionLook.write(key, look)
	if written == null:
		printerr("%s: %s/%s.tres could not be written" % [region.region_name, RegionLook.DIR, key])
		return false
	region.look = written
	done.append("look at %s.tres" % key)
	return true


## The JSON at `file` in `dir`, or null for a region without it.
static func _spec(dir: String, file: String) -> Variant:
	var text := FileAccess.get_file_as_string(dir.path_join(file))
	return JSON.parse_string(text) if not text.is_empty() else null


## Whether the part named `holder` is to be written: not when the scene has
## it and --replace was not given (the old node is removed when it is).
static func _wanted(region: WorldRegion, holder: String, replace: bool) -> bool:
	var existing := region.get_node_or_null(holder)
	if existing == null:
		return true
	if not replace:
		return false
	region.remove_child(existing)
	existing.free()
	return true


## Puts `node`, owned by the scene, under the region after the last of the
## nodes named in `after` that the region has (else first).
static func _insert(region: WorldRegion, node: Node, after: Array[String]) -> void:
	region.add_child(node)
	node.owner = region
	var index := 0
	for name in after:
		var sibling := region.get_node_or_null(name)
		if sibling:
			index = maxi(index, sibling.get_index() + 1)
	region.move_child(node, index)


## A node name from a PNG's name (clouds_far.png -> CloudsFar), numbered
## when a holder uses the same name twice.
static func _node_name(file: String, holder: Node, fallback: String) -> String:
	var base := file.get_basename().get_file().to_pascal_case()
	if base.is_empty():
		base = fallback
	var name := base
	var n := 2
	while holder.has_node(name):
		name = "%s%d" % [base, n]
		n += 1
	return name


func _backdrop(region: WorldRegion, dir: String, replace: bool, done: Array[String]) -> bool:
	var spec: Variant = _spec(dir, "parallax.json")
	if not spec is Dictionary or not _wanted(region, "Backdrop", replace):
		return true
	var holder := Node2D.new()
	holder.name = "Backdrop"
	_insert(region, holder, [])
	var art_tile := int(spec.get("art_tile", 16))
	var count := 0
	for entry in spec.get("layers", []):
		if not entry is Dictionary:
			continue
		var file := str(entry.get("file", ""))
		var texture := load(dir.path_join(file)) as Texture2D
		if texture == null:
			printerr(
				"%s: %s is not an imported texture" % [region.region_name, dir.path_join(file)]
			)
			return false
		var layer := BackdropLayer.from_spec(entry, texture, art_tile)
		layer.name = _node_name(file, holder, "Layer")
		holder.add_child(layer)
		layer.owner = region
		count += 1
	done.append("Backdrop with %d layers" % count)
	return true


func _fringes(region: WorldRegion, dir: String, replace: bool, done: Array[String]) -> bool:
	var spec: Variant = _spec(dir, "edges.json")
	if not spec is Dictionary or not _wanted(region, "Fringes", replace):
		return true
	var fringes := RegionFringes.from_spec(spec, dir)
	fringes.name = "Fringes"
	var count := 0
	for side in EdgeDecor.SIDES:
		var listed: Array = spec.get(side, [])
		var loaded: Array = fringes.get(side)
		if loaded.size() != listed.size():
			printerr(
				"%s: a PNG of %s/edges.json is not an imported texture" % [region.region_name, dir]
			)
			fringes.free()
			return false
		count += loaded.size()
	_insert(region, fringes, ["Tiles", "Props"])
	done.append("Fringes with %d pieces" % count)
	return true


func _fx(region: WorldRegion, dir: String, replace: bool, done: Array[String]) -> bool:
	var spec: Variant = _spec(dir, "fx.json")
	if not spec is Dictionary or not _wanted(region, "Fx", replace):
		return true
	var holder := Node2D.new()
	holder.name = "Fx"
	_insert(region, holder, ["Tiles", "Props", "Fringes"])
	var art_tile := int(spec.get("art_tile", 16))
	var count := 0
	for entry in spec.get("particles", []):
		if not entry is Dictionary:
			continue
		var texture: Texture2D = null
		var file := str(entry.get("texture", ""))
		if not entry.has("preset"):
			texture = load(dir.path_join(file)) as Texture2D
			if texture == null:
				printerr(
					"%s: %s is not an imported texture" % [region.region_name, dir.path_join(file)]
				)
				return false
		var node := RegionParticles.from_spec(entry, texture, art_tile)
		node.name = _node_name(
			file if not entry.has("preset") else str(entry["preset"]), holder, "Particles"
		)
		holder.add_child(node)
		node.owner = region
		count += 1
	done.append("Fx with %d kinds of particle" % count)
	return true
