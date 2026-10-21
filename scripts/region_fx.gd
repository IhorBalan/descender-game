class_name RegionFx
extends Node2D

## Drifting particles in the air of a region: petals, motes, embers, ash.
##
## A region opts in with assets/regions/<key>/fx.json (key as in
## EdgeDecor.region_key):
##
##   {
##     "art_tile": 64,
##     "particles": [
##       {"texture": "petal.png", "amount": 40, "color": "#ffc8e4", "lifetime": 12,
##        "direction": [-0.4, 1], "speed": [10, 25], "gravity": [0, 3],
##        "scale": [0.6, 1.0], "spin": 2, "sway": 6}
##     ]
##   }
##
## The emitters follow the camera and fill the view plus a margin, so the
## air is never empty when the view moves. Speeds are level pixels a second,
## `scale` is relative to the texture drawn at tile / art_tile, `spin` is
## turns a second at most and `sway` the sideways wobble in level pixels.
## Switching regions fades the old emitters out.

const REGIONS_DIR := "res://assets/regions"
const MARGIN := 48.0
const FADE := 1.5

var _current := ""


## Starts the particles of a region, replacing the previous region's.
func show_region(region: Dictionary, tile: int) -> void:
	var key := EdgeDecor.region_key(str(region.get("name", "")))
	if key == _current:
		return
	_current = key
	for child in get_children():
		var emitter := child as CPUParticles2D
		if emitter:
			emitter.emitting = false
		var tween := create_tween()
		tween.tween_property(child, "modulate:a", 0.0, FADE)
		tween.tween_callback(child.queue_free)
	var dir := "%s/%s" % [REGIONS_DIR, key]
	var text := FileAccess.get_file_as_string(dir + "/fx.json")
	var spec: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if not spec is Dictionary:
		return
	var scale_factor := float(tile) / float(spec.get("art_tile", tile))
	for entry in spec.get("particles", []):
		if not entry is Dictionary:
			continue
		var emitter := Emitter.new()
		emitter.setup(entry, "%s/%s" % [dir, str(entry.get("texture", ""))], scale_factor)
		add_child(emitter)


## One kind of particle, emitted across the view around the camera.
class Emitter:
	extends CPUParticles2D

	func setup(entry: Dictionary, texture_path: String, scale_factor: float) -> void:
		texture = load(texture_path) as Texture2D
		texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		z_index = 5
		amount = int(entry.get("amount", 30))
		lifetime = float(entry.get("lifetime", 10.0))
		preprocess = lifetime
		local_coords = false
		# Started in _ready, once the emitter sits on the camera, so the
		# preprocessed particles fill the view rather than the level's corner.
		emitting = false
		emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
		var dir: Array = entry.get("direction", [0.0, 1.0])
		direction = Vector2(float(dir[0]), float(dir[1])) if dir.size() >= 2 else Vector2.DOWN
		spread = float(entry.get("spread", 25.0))
		var speed: Array = entry.get("speed", [5.0, 15.0])
		initial_velocity_min = float(speed[0])
		initial_velocity_max = float(speed[1]) if speed.size() >= 2 else float(speed[0])
		var g: Array = entry.get("gravity", [0.0, 0.0])
		gravity = Vector2(float(g[0]), float(g[1])) if g.size() >= 2 else Vector2.ZERO
		var size: Array = entry.get("scale", [1.0, 1.0])
		scale_amount_min = float(size[0]) * scale_factor
		scale_amount_max = (float(size[1]) if size.size() >= 2 else float(size[0])) * scale_factor
		angular_velocity_max = float(entry.get("spin", 0.0)) * 360.0
		angular_velocity_min = -angular_velocity_max
		angle_max = 360.0
		var sway := float(entry.get("sway", 0.0))
		if sway > 0.0:
			# A gentle sideways wobble: the orbit is a cheap stand-in for wind.
			orbit_velocity_min = -0.05
			orbit_velocity_max = 0.05
			radial_accel_min = -sway
			radial_accel_max = sway
		color = Color.from_string(str(entry.get("color", "#ffffff")), Color.WHITE)
		# Fade in and out over the life, so particles never pop.
		var ramp := Gradient.new()
		ramp.set_color(0, Color(1, 1, 1, 0))
		ramp.add_point(0.15, Color(1, 1, 1, 1))
		ramp.add_point(0.85, Color(1, 1, 1, 1))
		ramp.set_color(ramp.get_point_count() - 1, Color(1, 1, 1, 0))
		color_ramp = ramp

	func _ready() -> void:
		_follow_camera()
		restart()
		emitting = true

	func _process(_delta: float) -> void:
		_follow_camera()

	func _follow_camera() -> void:
		var camera := get_viewport().get_camera_2d()
		if camera == null:
			return
		var view := get_viewport_rect().size / camera.zoom
		global_position = camera.get_screen_center_position()
		emission_rect_extents = view / 2.0 + Vector2(MARGIN, MARGIN)
