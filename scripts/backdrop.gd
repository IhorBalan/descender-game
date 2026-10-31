class_name Backdrop
extends Node2D

## Painted parallax layers behind the level, one set per region, each a
## BackdropLayer (see it for what a layer does).
##
## A region of the game's own world keeps its set in its scene, under
## `Backdrop/*`, which SceneWorld reads into the region's `parallax` spec.
## A region of a map without a scene opts in with regions/<key>/parallax.json
## next to the map's world.json (the editor exports a region's parallax set
## that way), else godot_assets/regions/<key>/parallax.json (key as in
## EdgeDecor.region_key): the pixels per tile its PNGs are drawn at and the
## layers from back to front:
##
##   {
##     "art_tile": 64,
##     "layers": [
##       {"file": "far.png", "scroll": [0.2, 0.9], "y": -40, "tint": "#ffffff",
##        "depth": 0.5},
##       {"file": "fog.png", "scroll": [0.5, 1.0], "y": 0, "drift": [6, 0],
##        "sway": [8, 9], "blend": "add", "lit": true, "repeat_y": true}
##     ]
##   }
##
## `y`, in level pixels, puts the layer's anchor relative to the region's
## top edge; the other fields are BackdropLayer's. Switching regions
## cross-fades the sets.

const REGIONS_DIR := "res://godot_assets/regions"
const FADE := 2.5
const SCREEN_ALPHA := BackdropLayer.SCREEN_ALPHA

var _current := ""


## Draws the layers for a region, replacing the previous region's; `tile`
## is the level's tile size in pixels and `map_dir` the folder of its
## world.json, whose regions/<key>/ set wins over godot_assets/regions/<key>/.
## A region with a `parallax` spec of its own (from its scene) uses that.
func show_region(region: Dictionary, tile: int, map_dir := "") -> void:
	var key := EdgeDecor.region_key(str(region.get("name", "")))
	var spec: Variant = region.get("parallax")
	var dir := ""
	if spec is Dictionary:
		dir = "scene:" + key
	else:
		dir = set_dir(key, map_dir, "parallax.json")
	if dir == _current:
		return
	_current = dir
	for child in get_children():
		var tween := create_tween()
		tween.tween_property(child, "modulate:a", 0.0, FADE)
		tween.tween_callback(child.queue_free)
	if not spec is Dictionary:
		var text := FileAccess.get_file_as_string(dir + "/parallax.json")
		spec = JSON.parse_string(text) if not text.is_empty() else null
	if not spec is Dictionary:
		return
	var art_tile := int(spec.get("art_tile", tile))
	var top := float(region.get("y0", 0))
	var sky := str(region.get("sky_bottom", ""))
	for entry in spec.get("layers", []):
		if not entry is Dictionary:
			continue
		var texture: Texture2D = entry.get("texture") as Texture2D
		if texture == null and entry.has("file"):
			texture = load_texture("%s/%s" % [dir, str(entry.get("file", ""))], art_tile > 16)
		if texture == null:
			continue
		var layer := BackdropLayer.from_spec(entry, texture, art_tile)
		layer.tile = tile
		layer.sky = sky
		layer.position.y += top
		add_child(layer)
		layer.modulate.a = 0.0
		create_tween().tween_property(layer, "modulate:a", layer.effective_tint().a, FADE)


## The folder a region's `file` is read from: the map's own
## regions/<key>/ when it has the file, else the shared assets folder.
static func set_dir(key: String, map_dir: String, file: String) -> String:
	if not map_dir.is_empty():
		var own := map_dir.path_join("regions").path_join(key)
		if FileAccess.file_exists(own.path_join(file)):
			return own
	return "%s/%s" % [REGIONS_DIR, key]


## A layer's `depth`: its own, else half of the camera travel it does not
## follow (nothing for a cover or a front layer).
static func layer_depth(entry: Dictionary) -> float:
	var layer := BackdropLayer.from_spec(entry, null, 16)
	var depth := layer.effective_depth()
	layer.free()
	return depth


## A layer's PNG: Godot's imported copy for a resource in an exported build,
## else the file itself (a map's folder under user://, or a sheet the live
## map sync rewrote while the editor was not looking), with mipmaps for
## painted art.
static func load_texture(path: String, painted := false) -> Texture2D:
	if path.begins_with("res://") and not OS.has_feature("editor"):
		return load(path) as Texture2D
	if not FileAccess.file_exists(path):
		return null
	var image := Image.load_from_file(ProjectSettings.globalize_path(path))
	if image == null:
		return null
	if painted and not image.has_mipmaps():
		image.generate_mipmaps()
	return ImageTexture.create_from_image(image)
