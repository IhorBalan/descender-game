@tool
class_name BackdropLayer
extends Node2D

## One painted parallax layer of a region's backdrop: a texture repeated
## sideways across the level that follows the camera by `scroll` of its
## movement (1 moves with the world, 0 stays on screen), anchored so it
## sits at the node's own position when the camera is centred there. Drag
## the node to move the layer; its `y` is read relative to the region's top.
##
## A region's scene keeps its layers under `Backdrop/*`, back to front, so
## the editor shows the set the way the game draws it, scrolling as the
## view pans (see WorldRegion). The game reads them into the region's
## `parallax` spec (SceneWorld) and Backdrop builds the same nodes from it,
## or from a region's parallax.json for a map without a scene. A `cover`
## layer is stretched over the whole view instead (a sky), and a `front`
## layer draws over the level (haze, wisps). `drift` moves the layer by
## itself, in level pixels a second, and `sway` rocks it sideways by an
## amplitude over a period in seconds. `blend` is mix, add or screen (the
## same additive blend at a lower alpha; Godot has no screen mode in 2D).
## `lit` lets the level's lights fall on the layer. `repeat_y` tiles it up
## and down too. `depth` pulls the layer towards the region's sky colour,
## the haze of distance; -1 takes half of what it does not follow the
## camera by. The PNG is drawn at tile / `art_tile` of its size, so one set
## serves maps of any art scale; painted art (`art_tile` above 16) is
## filtered with mipmaps and pixel art stays nearest.

## How much a screen blend's alpha is cut, so it adds softly.
const SCREEN_ALPHA := 0.6
## The pixel size of a tile in a scene, where the world's tile is not known.
const SCENE_TILE := 16

@export var texture: Texture2D:
	set(value):
		texture = value
		queue_redraw()
## The share of the camera's movement the layer follows, per axis.
@export var scroll := Vector2.ONE:
	set(value):
		scroll = value
		queue_redraw()
## Stretched over the whole view (a sky) instead of repeated.
@export var cover := false:
	set(value):
		cover = value
		queue_redraw()
## Drawn in front of the level rather than behind it.
@export var front := false:
	set(value):
		front = value
		_apply_order()
## Level pixels a second the layer moves by itself.
@export var drift := Vector2.ZERO
## Sideways sway: [amplitude in level pixels, period in seconds].
@export var sway := Vector2.ZERO
## Tiled up and down too, for a region taller than its art.
@export var repeat_y := false:
	set(value):
		repeat_y = value
		queue_redraw()
@export_enum("mix", "add", "screen") var blend := "mix":
	set(value):
		blend = value
		_apply_material()
## Whether the level's lights fall on the layer.
@export var lit := false:
	set(value):
		lit = value
		_apply_material()
## How far the layer is pulled towards the sky colour; -1 for the default.
@export_range(-1.0, 1.0) var depth := -1.0:
	set(value):
		depth = value
		_apply_tint()
## The layer's own colour, before the sky's haze.
@export var tint := Color.WHITE:
	set(value):
		tint = value
		_apply_tint()
## Pixels a tile the PNG was drawn at: 16 for pixel art, 64 for painted.
@export var art_tile := 64:
	set(value):
		art_tile = maxi(value, 1)
		_apply_filter()
		queue_redraw()

## Level pixels a tile of the level the layer is drawn in.
var tile := SCENE_TILE:
	set(value):
		tile = maxi(value, 1)
		queue_redraw()
## The sky colour the layer's haze is pulled towards ("" for none).
var sky := "":
	set(value):
		sky = value
		_apply_tint()

var _camera_at := Vector2.ZERO
var _view := Vector2.ZERO
var _time := 0.0


## The looks the properties set (order, material, filter, colour) are applied
## once the layer is in the tree, so a layer built for a scene file carries
## only its own fields.
func _ready() -> void:
	# After the camera, which moves the drawn view each frame (see _process).
	process_priority = 1
	_apply_order()
	_apply_material()
	_apply_filter()
	_apply_tint()
	if Engine.is_editor_hint():
		var region := _region()
		if region:
			sky = str(region.look.get("sky_bottom", ""))


## What the PNG is scaled by to land on the level's grid.
func scale_factor() -> float:
	return float(tile) / float(art_tile)


## Whether the art is painted (filtered) rather than pixel art (nearest).
func painted() -> bool:
	return art_tile > 16


## The layer's depth as drawn: its own, else half of the camera travel it
## does not follow (nothing for a cover or a front layer).
func effective_depth() -> float:
	if depth >= 0.0:
		return clampf(depth, 0.0, 1.0)
	if cover or front:
		return 0.0
	return clampf(1.0 - scroll.x, 0.0, 1.0) * 0.5


## The colour the layer is drawn in: its tint pulled towards the sky by its
## depth, a screen blend at a lower alpha.
func effective_tint() -> Color:
	var colour := tint
	if Color.html_is_valid(sky):
		colour = tint.lerp(Color.html(sky), effective_depth())
		colour.a = tint.a
	if blend == "screen":
		colour.a *= SCREEN_ALPHA
	return colour


## Whether the layer moves by itself, so it is redrawn every frame.
func animated() -> bool:
	return drift != Vector2.ZERO or sway.x != 0.0


## Where the layer's own motion has taken it after `time` seconds.
func motion(time: float) -> Vector2:
	var moved := drift * time
	if sway.x != 0.0 and sway.y > 0.0:
		moved.x += sin(time * TAU / sway.y) * sway.x
	return moved


## The layer as a parallax.json entry (with the texture itself under
## `texture`, and its `y` relative to the region's top), what SceneWorld
## hands Backdrop.
func to_spec() -> Dictionary:
	var spec := {
		"texture": texture,
		"scroll": [scroll.x, scroll.y],
		"y": position.y,
		"art_tile": art_tile,
	}
	if cover:
		spec["cover"] = true
	if front:
		spec["front"] = true
	if drift != Vector2.ZERO:
		spec["drift"] = [drift.x, drift.y]
	if sway != Vector2.ZERO:
		spec["sway"] = [sway.x, sway.y]
	if repeat_y:
		spec["repeat_y"] = true
	if blend != "mix":
		spec["blend"] = blend
	if lit:
		spec["lit"] = true
	if depth >= 0.0:
		spec["depth"] = depth
	if tint != Color.WHITE:
		spec["tint"] = "#" + tint.to_html(tint.a < 1.0)
	return spec


## A layer from a parallax.json entry (see the class notes for the fields),
## drawing `texture` at `art_tile` pixels a tile; `y` is relative to the
## region's top. The node is placed at (0, y).
static func from_spec(entry: Dictionary, layer_texture: Texture2D, pixels: int) -> BackdropLayer:
	var layer := BackdropLayer.new()
	layer.texture = layer_texture
	layer.art_tile = int(entry.get("art_tile", pixels))
	layer.scroll = _vec(entry.get("scroll"), Vector2.ONE)
	layer.position = Vector2(0.0, float(entry.get("y", 0)))
	layer.cover = bool(entry.get("cover", false))
	layer.front = bool(entry.get("front", false))
	layer.drift = _vec(entry.get("drift"), Vector2.ZERO)
	layer.sway = _vec(entry.get("sway"), Vector2.ZERO)
	layer.repeat_y = bool(entry.get("repeat_y", false))
	layer.blend = str(entry.get("blend", "mix"))
	layer.lit = bool(entry.get("lit", false))
	layer.depth = clampf(float(entry["depth"]), 0.0, 1.0) if entry.has("depth") else -1.0
	layer.tint = Color.from_string(str(entry.get("tint", "#ffffff")), Color.WHITE)
	return layer


static func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Array and value.size() >= 2:
		return Vector2(float(value[0]), float(value[1]))
	return fallback


func _apply_order() -> void:
	if not is_inside_tree():
		return
	z_index = 4 if front else -10


func _apply_material() -> void:
	if not is_inside_tree():
		return
	light_mask = 1 if lit else 0
	var additive := blend == "add" or blend == "screen"
	if additive or not lit:
		var item := CanvasItemMaterial.new()
		if additive:
			item.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		if not lit:
			item.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
		material = item
	else:
		material = null
	_apply_tint()


func _apply_filter() -> void:
	if not is_inside_tree():
		return
	texture_filter = (
		CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		if painted()
		else CanvasItem.TEXTURE_FILTER_NEAREST
	)


## The drawn colour. In the game the alpha is the fade's (see Backdrop), so
## only the colour changes once the layer is in the tree.
func _apply_tint() -> void:
	if not is_inside_tree():
		return
	var colour := effective_tint()
	var alpha := modulate.a if is_node_ready() and not Engine.is_editor_hint() else colour.a
	modulate = Color(colour.r, colour.g, colour.b, alpha)


## The region scene the layer is in, when it is in one.
func _region() -> WorldRegion:
	var node := get_parent()
	while node and not node is WorldRegion:
		node = node.get_parent()
	return node as WorldRegion


func _process(delta: float) -> void:
	_time += delta
	var viewport := get_viewport()
	if viewport == null:
		return
	if not Engine.is_editor_hint() and viewport.get_camera_2d() == null:
		return
	# Where the view is drawn this frame: with physics interpolation the
	# camera's own position only changes on physics ticks. In the editor it
	# is the view the scene is panned to.
	var to_world := viewport.get_canvas_transform().affine_inverse()
	var size := viewport.get_visible_rect().size
	var at := to_local(to_world * (size / 2.0))
	_view = to_world.basis_xform(size).abs()
	if at != _camera_at or animated():
		_camera_at = at
		queue_redraw()


func _draw() -> void:
	if texture == null:
		return
	if cover:
		draw_texture_rect(texture, Rect2(_camera_at - _view / 2.0, _view), false)
		return
	var size := texture.get_size() * scale_factor()
	if size.x <= 0.0 or size.y <= 0.0:
		return
	# The layer drifts by the share of the camera's travel it does not
	# follow, measured from its anchor (the node's position), plus its own
	# motion.
	var origin := _camera_at * (Vector2.ONE - scroll) + motion(_time)
	var left := _camera_at.x - _view.x / 2.0
	var right := _camera_at.x + _view.x / 2.0
	var first := floori((left - origin.x) / size.x)
	var last := ceili((right - origin.x) / size.x)
	var rows := [0]
	if repeat_y:
		var above := _camera_at.y - _view.y / 2.0
		var below := _camera_at.y + _view.y / 2.0
		rows = range(floori((above - origin.y) / size.y), ceili((below - origin.y) / size.y) + 1)
	for j in rows:
		for k in range(first, last + 1):
			var at := Vector2(origin.x + k * size.x, origin.y + j * size.y)
			draw_texture_rect(texture, Rect2(at, size), false)
