class_name RegionFx
extends Node2D

## Drifting particles in the air of a region: petals, motes, embers, ash.
##
## A region of the game's own world keeps its particles in its scene, as
## RegionParticles nodes, which SceneWorld reads into the region's `fx`
## spec (the same shape, with the textures in place of file names). A
## region of a map without a scene opts in with regions/<key>/fx.json next
## to the map's world.json, else godot_assets/regions/<key>/fx.json (key as in
## EdgeDecor.region_key):
##
##   {
##     "art_tile": 64,
##     "particles": [
##       {"texture": "petal.png", "amount": 40, "color": "#ffc8e4", "lifetime": 12,
##        "direction": [-0.4, 1], "speed": [10, 25], "gravity": [0, 3],
##        "scale": [0.6, 1.0], "spin": 2, "sway": 6},
##       {"preset": "embers", "amount": 20}
##     ]
##   }
##
## An entry with a `preset` (see PRESETS: embers, dust, ash, leaves, rain,
## snow, birds) needs no PNG: its sprite is drawn in code and its other
## fields are defaults the entry may override. A region with no fx.json
## but a `particles` list of preset names in the export gets those.
##
## The emitters follow the camera and fill the view plus a margin, so the
## air is never empty when the view moves. Speeds are level pixels a second,
## `scale` is relative to the texture drawn at tile / art_tile, `spin` is
## turns a second at most and `sway` the sideways wobble in level pixels.
## Switching regions fades the old emitters out.

const REGIONS_DIR := "res://godot_assets/regions"
const MARGIN := 48.0
const FADE := 1.5

## The stock particles, drawn at 16 pixels a tile (level pixels).
const PRESETS := {
	"embers":
	{
		"amount": 24,
		"color": "#ffa040",
		"lifetime": 6,
		"direction": [0.15, -1],
		"spread": 30,
		"speed": [8, 22],
		"gravity": [0, -4],
		"scale": [0.6, 1.0],
		"sway": 5,
	},
	"dust":
	{
		"amount": 40,
		"color": "#d8cfb8",
		"lifetime": 14,
		"direction": [0.3, 0.1],
		"spread": 60,
		"speed": [2, 6],
		"gravity": [0, 0],
		"scale": [0.5, 1.0],
		"sway": 3,
	},
	"ash":
	{
		"amount": 30,
		"color": "#9a9a9a",
		"lifetime": 12,
		"direction": [-0.3, 1],
		"spread": 25,
		"speed": [6, 14],
		"gravity": [0, 2],
		"scale": [0.6, 1.0],
		"spin": 0.5,
		"sway": 4,
	},
	"leaves":
	{
		"amount": 18,
		"color": "#b8c860",
		"lifetime": 12,
		"direction": [-0.5, 1],
		"spread": 20,
		"speed": [10, 24],
		"gravity": [0, 4],
		"scale": [0.7, 1.0],
		"spin": 0.8,
		"sway": 8,
	},
	"rain":
	{
		"amount": 120,
		"color": "#b0c4e0",
		"lifetime": 2,
		"direction": [-0.15, 1],
		"spread": 2,
		"speed": [220, 300],
		"gravity": [0, 60],
		"scale": [0.8, 1.0],
	},
	"snow":
	{
		"amount": 60,
		"color": "#f4f8ff",
		"lifetime": 14,
		"direction": [-0.2, 1],
		"spread": 25,
		"speed": [6, 14],
		"gravity": [0, 2],
		"scale": [0.5, 1.0],
		"sway": 5,
	},
	"birds":
	{
		"amount": 4,
		"color": "#2a2630",
		"lifetime": 16,
		"direction": [1, -0.05],
		"spread": 6,
		"speed": [24, 40],
		"gravity": [0, 0],
		"scale": [0.8, 1.2],
	},
}

var _current := ""


## Starts the particles of a region, replacing the previous region's;
## `map_dir` is the folder of the level's world.json, whose own
## regions/<key>/fx.json wins over the shared assets folder.
func show_region(region: Dictionary, tile: int, map_dir := "") -> void:
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
	var spec: Variant = region.get("fx")
	var dir := ""
	if not spec is Dictionary:
		dir = Backdrop.set_dir(key, map_dir, "fx.json")
		var text := FileAccess.get_file_as_string(dir + "/fx.json")
		spec = JSON.parse_string(text) if not text.is_empty() else null
	if not spec is Dictionary:
		spec = region_spec(region)
	var art_tile := int(spec.get("art_tile", tile))
	for entry in spec.get("particles", []):
		if not entry is Dictionary:
			continue
		var emitter := Emitter.new()
		if entry.has("preset"):
			var preset := str(entry["preset"])
			if not PRESETS.has(preset):
				push_warning("RegionFx: no particle preset '%s'" % preset)
				emitter.free()
				continue
			var merged: Dictionary = PRESETS[preset].duplicate()
			merged.merge(entry, true)
			emitter.setup(merged, preset_texture(preset), float(tile) / 16.0)
		else:
			var texture: Texture2D = entry["texture"] if entry.get("texture") is Texture2D else null
			if texture == null and not dir.is_empty():
				texture = Backdrop.load_texture("%s/%s" % [dir, str(entry.get("texture", ""))])
			if texture == null:
				emitter.free()
				continue
			emitter.setup(entry, texture, float(tile) / float(entry.get("art_tile", art_tile)))
		add_child(emitter)


## The spec a region's own `particles` list of preset names stands for.
static func region_spec(region: Dictionary) -> Dictionary:
	var particles := []
	for name in region.get("particles", []):
		if name is String and PRESETS.has(name):
			particles.append({"preset": name})
	return {"art_tile": 16, "particles": particles}


## The sprite of a preset, drawn in code: a soft dot for embers, dust and
## snow, a flake for ash, a leaf, a streak of rain and a bird's wings.
static func preset_texture(preset: String) -> ImageTexture:
	var image: Image
	match preset:
		"rain":
			image = Image.create(1, 7, false, Image.FORMAT_RGBA8)
			for y in 7:
				image.set_pixel(0, y, Color(1, 1, 1, 0.35 + 0.65 * y / 6.0))
		"leaves":
			image = Image.create(5, 3, false, Image.FORMAT_RGBA8)
			for x in 5:
				var half := 1 if x == 0 or x == 4 else 0
				for y in range(half, 3 - half):
					image.set_pixel(x, y, Color.WHITE)
		"ash":
			image = Image.create(3, 3, false, Image.FORMAT_RGBA8)
			for x in 3:
				for y in 3:
					if x == 1 or y == 1:
						image.set_pixel(x, y, Color(1, 1, 1, 0.9))
		"birds":
			image = Image.create(7, 3, false, Image.FORMAT_RGBA8)
			for p in [[0, 0], [1, 1], [2, 2], [3, 2], [4, 2], [5, 1], [6, 0]]:
				image.set_pixel(p[0], p[1], Color.WHITE)
		_:
			var size := 2 if preset == "dust" else 3
			image = Image.create(size, size, false, Image.FORMAT_RGBA8)
			var centre := Vector2((size - 1) / 2.0, (size - 1) / 2.0)
			for x in size:
				for y in size:
					var d := Vector2(x, y).distance_to(centre) / maxf(centre.x, 0.5)
					image.set_pixel(x, y, Color(1, 1, 1, clampf(1.15 - d * 0.8, 0.0, 1.0)))
	return ImageTexture.create_from_image(image)


## One kind of particle, emitted across the view around the camera.
class Emitter:
	extends CPUParticles2D

	func setup(entry: Dictionary, sprite: Texture2D, scale_factor: float) -> void:
		texture = sprite
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
