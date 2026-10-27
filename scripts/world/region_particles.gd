@tool
class_name RegionParticles
extends Node2D

## One kind of particle drifting through the air of a region, petals, motes,
## embers, ash, as a node of the region's scene (`Fx/*`, see WorldRegion)
## with the fields of an fx.json entry as properties (see RegionFx). A
## `preset` (embers, dust, ash, leaves, rain, snow, birds) draws its own
## sprite; with none, `texture` is the sprite, drawn at tile / art_tile of
## its size. The editor runs a preview over the region's band so the
## particles are tuned in the inspector with the result in view.
##
## The game never runs this node: SceneWorld reads the region's `Fx/*`
## into its `fx` spec and RegionFx emits the same particles across the view
## around the camera. A region without Fx nodes falls back to its
## assets/regions/<key>/fx.json; tools/scene_art.gd turns that file into
## the nodes.

## How many more particles the editor's preview emits than the game, since
## it fills the region's band rather than one view.
const PREVIEW_AMOUNT := 6
const PREVIEW_MAX := 400

## A stock sprite and its defaults, or none for `texture`.
@export_enum("none", "embers", "dust", "ash", "leaves", "rain", "snow", "birds")
var preset := "none":
	set(value):
		preset = value
		_refresh()
@export var texture: Texture2D:
	set(value):
		texture = value
		_refresh()
@export_range(1, 500) var amount := 30:
	set(value):
		amount = value
		_refresh()
@export var color := Color.WHITE:
	set(value):
		color = value
		_refresh()
## Seconds a particle lives.
@export var lifetime := 10.0:
	set(value):
		lifetime = maxf(value, 0.1)
		_refresh()
@export var direction := Vector2.DOWN:
	set(value):
		direction = value
		_refresh()
## Degrees either side of the direction.
@export_range(0.0, 180.0) var spread := 25.0:
	set(value):
		spread = value
		_refresh()
## Level pixels a second: the slowest and the fastest.
@export var speed := Vector2(5.0, 15.0):
	set(value):
		speed = value
		_refresh()
## Level pixels a second squared.
@export var gravity := Vector2.ZERO:
	set(value):
		gravity = value
		_refresh()
## The smallest and largest size, relative to the sprite drawn to the grid.
@export var size := Vector2.ONE:
	set(value):
		size = value
		_refresh()
## Turns a second at most.
@export var spin := 0.0:
	set(value):
		spin = value
		_refresh()
## The sideways wobble, in level pixels.
@export var sway := 0.0:
	set(value):
		sway = value
		_refresh()
## Pixels a tile the texture was drawn at: 16 for pixel art, 64 for painted.
@export var art_tile := 64:
	set(value):
		art_tile = maxi(value, 1)
		_refresh()

var _preview: CPUParticles2D


func _ready() -> void:
	_refresh()


## Whether a stock sprite is used rather than `texture`.
func has_preset() -> bool:
	return RegionFx.PRESETS.has(preset)


## The particles as an fx.json entry with the texture itself in place of a
## file name (see RegionFx), what SceneWorld hands the level.
func to_spec() -> Dictionary:
	var spec := {
		"amount": amount,
		"color": "#" + color.to_html(color.a < 1.0),
		"lifetime": lifetime,
		"direction": [direction.x, direction.y],
		"spread": spread,
		"speed": [speed.x, speed.y],
		"gravity": [gravity.x, gravity.y],
		"scale": [size.x, size.y],
		"art_tile": art_tile,
	}
	if has_preset():
		spec["preset"] = preset
	else:
		spec["texture"] = texture
	if spin > 0.0:
		spec["spin"] = spin
	if sway > 0.0:
		spec["sway"] = sway
	return spec


## A node from an fx.json entry (see RegionFx): a preset's fields are its
## defaults, which the entry may override; `sprite` is the texture of an
## entry without a preset and `pixels` the set's art_tile.
static func from_spec(entry: Dictionary, sprite: Texture2D, pixels: int) -> RegionParticles:
	var merged := {}
	if entry.has("preset") and RegionFx.PRESETS.has(str(entry["preset"])):
		merged = RegionFx.PRESETS[str(entry["preset"])].duplicate()
	merged.merge(entry, true)
	var node := RegionParticles.new()
	node.preset = str(merged.get("preset", "none"))
	node.texture = sprite
	node.amount = int(merged.get("amount", 30))
	node.color = Color.from_string(str(merged.get("color", "#ffffff")), Color.WHITE)
	node.lifetime = float(merged.get("lifetime", 10.0))
	node.direction = _vec(merged.get("direction"), Vector2.DOWN)
	node.spread = float(merged.get("spread", 25.0))
	node.speed = _pair(merged.get("speed"), Vector2(5.0, 15.0))
	node.gravity = _vec(merged.get("gravity"), Vector2.ZERO)
	node.size = _pair(merged.get("scale"), Vector2.ONE)
	node.spin = float(merged.get("spin", 0.0))
	node.sway = float(merged.get("sway", 0.0))
	node.art_tile = int(merged.get("art_tile", pixels))
	return node


static func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Array and value.size() >= 2:
		return Vector2(float(value[0]), float(value[1]))
	return fallback


## [min, max], or [one] for both.
static func _pair(value: Variant, fallback: Vector2) -> Vector2:
	if value is Array and value.size() >= 2:
		return Vector2(float(value[0]), float(value[1]))
	if value is Array and value.size() == 1:
		return Vector2(float(value[0]), float(value[0]))
	return fallback


## The editor's preview: an emitter like the game's over the region's band.
func _refresh() -> void:
	if not Engine.is_editor_hint() or not is_inside_tree():
		return
	if _preview:
		_preview.queue_free()
		_preview = null
	var region := WorldRegion.of(self)
	if region == null:
		return
	var world := region.get_parent() as WorldScene
	var tile := world.tile if world else 16
	var band := Vector2(world.width if world else 480, region.rows) * tile
	var spec := to_spec()
	var sprite := texture
	var scale_factor := float(tile) / float(art_tile)
	if has_preset():
		sprite = RegionFx.preset_texture(preset)
		scale_factor = float(tile) / 16.0
	if sprite == null:
		return
	spec["amount"] = mini(amount * PREVIEW_AMOUNT, PREVIEW_MAX)
	var emitter := RegionFx.Emitter.new()
	emitter.setup(spec, sprite, scale_factor)
	emitter.position = band / 2.0 - position
	emitter.emission_rect_extents = band / 2.0
	emitter.emitting = true
	add_child(emitter)
	_preview = emitter
