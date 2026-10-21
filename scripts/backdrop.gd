class_name Backdrop
extends Node2D

## Painted parallax layers behind the level, one set per region.
##
## A region opts in with assets/regions/<key>/parallax.json (key as in
## EdgeDecor.region_key): the pixels per tile its PNGs are drawn at and the
## layers from back to front:
##
##   {
##     "art_tile": 64,
##     "layers": [
##       {"file": "far.png", "scroll": [0.2, 0.9], "y": -40, "tint": "#ffffff"},
##       {"file": "fog.png", "scroll": [0.5, 1.0], "y": 0}
##     ]
##   }
##
## Each layer repeats sideways across the level. `scroll` is how much of the
## camera's movement the layer follows (1 moves with the world, 0 stays on
## screen); `y`, in level pixels, puts the layer's top edge relative to the
## region's top edge. A layer with "cover": true is stretched over the whole
## view instead (a sky), and "front": true draws it in front of the level
## (haze, foreground wisps). The PNGs are drawn scaled onto the 16-unit grid by
## tile / art_tile, so a set serves maps of any art scale. Switching regions
## fades the old set out over the new one.

const REGIONS_DIR := "res://assets/regions"
const FADE := 1.2

var _current := ""


## Draws the layers for a region, replacing the previous region's; `tile`
## is the level's tile size in pixels.
func show_region(region: Dictionary, tile: int) -> void:
	var key := EdgeDecor.region_key(str(region.get("name", "")))
	if key == _current:
		return
	_current = key
	for child in get_children():
		var tween := create_tween()
		tween.tween_property(child, "modulate:a", 0.0, FADE)
		tween.tween_callback(child.queue_free)
	var dir := "%s/%s" % [REGIONS_DIR, key]
	var text := FileAccess.get_file_as_string(dir + "/parallax.json")
	var spec: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if not spec is Dictionary:
		return
	var scale_factor := float(tile) / float(spec.get("art_tile", tile))
	var top := float(region.get("y0", 0))
	for entry in spec.get("layers", []):
		if not entry is Dictionary:
			continue
		var texture := load("%s/%s" % [dir, str(entry.get("file", ""))]) as Texture2D
		if texture == null:
			continue
		var layer := Layer.new()
		layer.texture = texture
		layer.scale_factor = scale_factor
		var scroll: Array = entry.get("scroll", [1.0, 1.0])
		layer.scroll = Vector2(float(scroll[0]), float(scroll[1])) if scroll.size() >= 2 else Vector2.ONE
		layer.anchor = Vector2(0.0, top + float(entry.get("y", 0)))
		layer.cover = bool(entry.get("cover", false))
		if bool(entry.get("front", false)):
			layer.z_index = 4
		layer.modulate = Color.from_string(str(entry.get("tint", "#ffffff")), Color.WHITE)
		layer.modulate.a = 0.0
		add_child(layer)
		create_tween().tween_property(layer, "modulate:a", 1.0, FADE)


## One parallax layer: a texture repeated sideways that follows the camera
## by `scroll`, anchored so it sits at `anchor` when the camera is there.
class Layer:
	extends Node2D
	var texture: Texture2D
	var scale_factor := 1.0
	var scroll := Vector2.ONE
	var anchor := Vector2.ZERO
	var cover := false
	var _camera_at := Vector2.ZERO
	var _view := Vector2.ZERO

	func _ready() -> void:
		texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		z_index = -10

	func _process(_delta: float) -> void:
		var camera := get_viewport().get_camera_2d()
		if camera == null:
			return
		var at := camera.get_screen_center_position()
		_view = get_viewport_rect().size / camera.zoom
		if at != _camera_at:
			_camera_at = at
			queue_redraw()

	func _draw() -> void:
		if texture == null:
			return
		if cover:
			draw_texture_rect(texture, Rect2(_camera_at - _view / 2.0, _view), false)
			return
		var size := texture.get_size() * scale_factor
		# The layer drifts by the share of the camera's travel it does not
		# follow, measured from its anchor.
		var origin := anchor + (_camera_at - anchor) * (Vector2.ONE - scroll)
		var left := _camera_at.x - _view.x / 2.0
		var right := _camera_at.x + _view.x / 2.0
		var first := floori((left - origin.x) / size.x)
		var last := ceili((right - origin.x) / size.x)
		for k in range(first, last + 1):
			draw_texture_rect(texture, Rect2(Vector2(origin.x + k * size.x, origin.y), size), false)
