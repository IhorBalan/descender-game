extends SceneTree

## Puts a region's painted parallax set into its scene: reads
## assets/regions/<key>/parallax.json (see Backdrop) and writes a Backdrop
## node of BackdropLayer children, back to front, into the region's scene
## under scenes/world/, so the editor shows the set the way the game draws
## it and each layer can be picked, moved and tuned there. A region that
## already has a Backdrop node is left alone unless `--replace` is given.
##
##   godot --headless --path . --script tools/scene_backdrops.gd -- \
##       [--replace] [00_paradise.tscn ...]
##
## With no scene names, every region scene is done. Run the import first
## (`godot --headless --path . --import`) so the PNGs are resources.

const SCENES_DIR := "res://scenes/world"
const REGIONS_DIR := "res://assets/regions"


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


## Writes the region scene at `path` with its backdrop as nodes; false when
## something went wrong (a region without a set is skipped, not an error).
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
	var existing := region.get_node_or_null("Backdrop")
	if existing and not replace:
		print("%s: has a Backdrop already, left alone (--replace redoes it)" % path.get_file())
		region.free()
		return true
	var dir := "%s/%s" % [REGIONS_DIR, key]
	var text := FileAccess.get_file_as_string(dir + "/parallax.json")
	var spec: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if not spec is Dictionary:
		print("%s: no parallax.json for '%s', skipped" % [path.get_file(), key])
		region.free()
		return true
	if existing:
		region.remove_child(existing)
		existing.free()
	var holder := Node2D.new()
	holder.name = "Backdrop"
	region.add_child(holder)
	region.move_child(holder, 0)
	holder.owner = region
	var art_tile := int(spec.get("art_tile", 16))
	var count := 0
	for entry in spec.get("layers", []):
		if not entry is Dictionary:
			continue
		var file := str(entry.get("file", ""))
		var texture := load(dir.path_join(file)) as Texture2D
		if texture == null:
			printerr("%s: %s/%s is not an imported texture" % [path.get_file(), key, file])
			region.free()
			return false
		var layer := BackdropLayer.from_spec(entry, texture, art_tile)
		layer.name = _layer_name(file, holder)
		holder.add_child(layer)
		layer.owner = region
		count += 1
	var out := PackedScene.new()
	var error := out.pack(region)
	if error == OK:
		error = ResourceSaver.save(out, path)
	region.free()
	if error != OK:
		printerr("%s: could not be written (%s)" % [path, error_string(error)])
		return false
	print("%s: Backdrop with %d layers from %s/parallax.json" % [path.get_file(), count, key])
	return true


## A node name from the PNG's name (clouds_far.png -> CloudsFar), numbered
## when the set uses the same PNG twice.
static func _layer_name(file: String, holder: Node) -> String:
	var base := file.get_basename().to_pascal_case()
	if base.is_empty():
		base = "Layer"
	var name := base
	var n := 2
	while holder.has_node(name):
		name = "%s%d" % [base, n]
		n += 1
	return name
