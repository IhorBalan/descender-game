class_name Backdrop
extends Node2D

## Painted parallax layers behind the level, one set per region.
##
## A region opts in with regions/<key>/parallax.json next to the map's
## world.json (the editor exports a region's parallax set that way), else
## assets/regions/<key>/parallax.json (key as in EdgeDecor.region_key): the
## pixels per tile its PNGs are drawn at and the layers from back to front:
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
## Each layer repeats sideways across the level. `scroll` is how much of the
## camera's movement the layer follows (1 moves with the world, 0 stays on
## screen); `y`, in level pixels, puts the layer's top edge relative to the
## region's top edge. A layer with "cover": true is stretched over the whole
## view instead (a sky), and "front": true draws it in front of the level
## (haze, foreground wisps). `drift` moves the layer by itself, in level
## pixels a second, and `sway` rocks it sideways by an amplitude over a
## period in seconds. `blend` is "mix" (the default), "add" or "screen"
## (the same additive blend at a lower alpha: Godot has no screen mode in
## 2D). `lit` lets the level's lights fall on the layer, for fog a torch
## should brighten; other layers ignore them. `repeat_y` tiles the layer
## up and down too, for a region taller than its art. `depth` pulls the
## layer towards the region's sky colour (its `sky_bottom`, else the tint),
## the haze of distance; a layer without one gets half of what it does not
## follow the camera by, so far layers fade and near ones stay crisp.
## The PNGs are drawn scaled onto the 16-unit grid by tile / art_tile, so
## a set serves maps of any art scale; painted sets are filtered with
## mipmaps, pixel art stays nearest. Switching regions cross-fades the sets.

const REGIONS_DIR := "res://assets/regions"
const FADE := 2.5
const SCREEN_ALPHA := 0.6

var _current := ""


## Draws the layers for a region, replacing the previous region's; `tile`
## is the level's tile size in pixels and `map_dir` the folder of its
## world.json, whose regions/<key>/ set wins over assets/regions/<key>/.
func show_region(region: Dictionary, tile: int, map_dir := "") -> void:
	var key := EdgeDecor.region_key(str(region.get("name", "")))
	var dir := set_dir(key, map_dir, "parallax.json")
	if dir == _current:
		return
	_current = dir
	for child in get_children():
		var tween := create_tween()
		tween.tween_property(child, "modulate:a", 0.0, FADE)
		tween.tween_callback(child.queue_free)
	var text := FileAccess.get_file_as_string(dir + "/parallax.json")
	var spec: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if not spec is Dictionary:
		return
	var art_tile := int(spec.get("art_tile", tile))
	var scale_factor := float(tile) / float(art_tile)
	var painted := art_tile > 16
	var top := float(region.get("y0", 0))
	var sky := str(region.get("sky_bottom", ""))
	for entry in spec.get("layers", []):
		if not entry is Dictionary:
			continue
		var texture := load_texture("%s/%s" % [dir, str(entry.get("file", ""))], painted)
		if texture == null:
			continue
		var layer := Layer.new()
		layer.texture = texture
		layer.scale_factor = scale_factor
		layer.painted = painted
		layer.scroll = _vec(entry.get("scroll"), Vector2.ONE)
		layer.anchor = Vector2(0.0, top + float(entry.get("y", 0)))
		layer.cover = bool(entry.get("cover", false))
		layer.front = bool(entry.get("front", false))
		layer.drift = _vec(entry.get("drift"), Vector2.ZERO)
		layer.sway = _vec(entry.get("sway"), Vector2.ZERO)
		layer.repeat_y = bool(entry.get("repeat_y", false))
		layer.blend = str(entry.get("blend", "mix"))
		layer.lit = bool(entry.get("lit", false))
		layer.depth = layer_depth(entry)
		var tint := Color.from_string(str(entry.get("tint", "#ffffff")), Color.WHITE)
		layer.tint = tint.lerp(Color.from_string(sky, tint), layer.depth)
		layer.tint.a = tint.a
		if layer.blend == "screen":
			layer.tint.a *= SCREEN_ALPHA
		layer.modulate = layer.tint
		layer.modulate.a = 0.0
		add_child(layer)
		create_tween().tween_property(layer, "modulate:a", layer.tint.a, FADE)


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
	if entry.has("depth"):
		return clampf(float(entry["depth"]), 0.0, 1.0)
	if bool(entry.get("cover", false)) or bool(entry.get("front", false)):
		return 0.0
	var scroll := _vec(entry.get("scroll"), Vector2.ONE)
	return clampf(1.0 - scroll.x, 0.0, 1.0) * 0.5


static func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Array and value.size() >= 2:
		return Vector2(float(value[0]), float(value[1]))
	return fallback


## One parallax layer: a texture repeated sideways that follows the camera
## by `scroll`, anchored so it sits at `anchor` when the camera is there.
class Layer:
	extends Node2D
	var texture: Texture2D
	var scale_factor := 1.0
	var painted := false
	var scroll := Vector2.ONE
	var anchor := Vector2.ZERO
	var cover := false
	var front := false
	var drift := Vector2.ZERO
	var sway := Vector2.ZERO
	var repeat_y := false
	var blend := "mix"
	var lit := false
	var depth := 0.0
	var tint := Color.WHITE
	var _camera_at := Vector2.ZERO
	var _view := Vector2.ZERO
	var _time := 0.0

	func _ready() -> void:
		texture_filter = (
			CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
			if painted
			else CanvasItem.TEXTURE_FILTER_NEAREST
		)
		z_index = 4 if front else -10
		light_mask = 1 if lit else 0
		var additive := blend == "add" or blend == "screen"
		if additive or not lit:
			var item := CanvasItemMaterial.new()
			if additive:
				item.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
			if not lit:
				item.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
			material = item

	## Whether the layer moves by itself, so it is redrawn every frame.
	func animated() -> bool:
		return drift != Vector2.ZERO or sway.x != 0.0

	## Where the layer's own motion has taken it after `time` seconds.
	func motion(time: float) -> Vector2:
		var moved := drift * time
		if sway.x != 0.0 and sway.y > 0.0:
			moved.x += sin(time * TAU / sway.y) * sway.x
		return moved

	func _process(delta: float) -> void:
		_time += delta
		var camera := get_viewport().get_camera_2d()
		if camera == null:
			return
		var at := camera.get_screen_center_position()
		_view = get_viewport_rect().size / camera.zoom
		if at != _camera_at or animated():
			_camera_at = at
			queue_redraw()

	func _draw() -> void:
		if texture == null:
			return
		if cover:
			draw_texture_rect(texture, Rect2(_camera_at - _view / 2.0, _view), false)
			return
		var size := texture.get_size() * scale_factor
		if size.x <= 0.0 or size.y <= 0.0:
			return
		# The layer drifts by the share of the camera's travel it does not
		# follow, measured from its anchor, plus its own motion.
		var origin := anchor + (_camera_at - anchor) * (Vector2.ONE - scroll) + motion(_time)
		var left := _camera_at.x - _view.x / 2.0
		var right := _camera_at.x + _view.x / 2.0
		var first := floori((left - origin.x) / size.x)
		var last := ceili((right - origin.x) / size.x)
		var rows := [0]
		if repeat_y:
			var above := _camera_at.y - _view.y / 2.0
			var below := _camera_at.y + _view.y / 2.0
			rows = range(
				floori((above - origin.y) / size.y), ceili((below - origin.y) / size.y) + 1
			)
		for j in rows:
			for k in range(first, last + 1):
				var at := Vector2(origin.x + k * size.x, origin.y + j * size.y)
				draw_texture_rect(texture, Rect2(at, size), false)


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
