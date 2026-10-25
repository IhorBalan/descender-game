class_name ParallaxSprite
extends Parallax2D

## A free-placed sprite the editor marked for parallax scrolling (the
## export's sprite `parallax`): Godot's Parallax2D with the sprite's layers as
## children at its origin.
##
## Parallax2D puts itself at scroll_offset + (the camera's centre - half the
## viewport in pixels, camera zoom left out) x (1 - scroll_scale). This
## keeps scroll_offset so that the sprite sits at its placed position `at`
## when the camera is centred on the sprite, as the editor shows it,
## whatever the window size; away from there it lags behind (scroll below 1)
## or runs ahead (above 1). Repeating axes tile it across the view with
## enough copies to cover it.
##
## The camera position comes from the drawn view each frame rather than
## from Parallax2D's own camera tracking, which floors it to whole level
## pixels when 2D pixel snapping is on, so slow layers would step unevenly
## against the world.

## The smallest camera zoom the copies of a repeating sprite are counted for.
const MIN_ZOOM := GameCamera.VIEW_ZOOM

## The placed top-left corner and size of the sprite, in level pixels.
var at := Vector2.ZERO
var size := Vector2.ONE
var repeat_x := false
var repeat_y := false

var _viewport := Vector2.ZERO


static func from_spec(parallax: Dictionary, placed: Vector2, art_size: Vector2) -> ParallaxSprite:
	var node := ParallaxSprite.new()
	node.ignore_camera_scroll = true
	# After the camera, which moves the drawn view each frame.
	node.process_priority = 1
	var scroll: Array = parallax.get("scroll", [1.0, 1.0])
	node.scroll_scale = Vector2(float(scroll[0]), float(scroll[1]))
	node.at = placed
	node.size = art_size
	node.repeat_x = bool(parallax.get("repeat_x", false))
	node.repeat_y = bool(parallax.get("repeat_y", false))
	node.fit(
		Vector2(
			ProjectSettings.get_setting("display/window/size/viewport_width"),
			ProjectSettings.get_setting("display/window/size/viewport_height")
		)
	)
	return node


## Whether it is drawn in front of the level (it runs ahead of the camera).
func in_front() -> bool:
	return scroll_scale.x > 1.0 or scroll_scale.y > 1.0


## Lines the sprite up for a viewport of `viewport` pixels; the copies of a
## repeating sprite cover it at any zoom down to the game camera's.
func fit(viewport: Vector2) -> void:
	_viewport = viewport
	scroll_offset = at + (viewport / 2.0 - at - size / 2.0) * (Vector2.ONE - scroll_scale)
	var view := viewport / MIN_ZOOM
	repeat_size = Vector2(size.x if repeat_x else 0.0, size.y if repeat_y else 0.0)
	var copies := 1.0
	if repeat_x:
		copies = maxf(copies, ceilf(view.x / size.x) + 1.0)
	if repeat_y:
		copies = maxf(copies, ceilf(view.y / size.y) + 1.0)
	repeat_times = int(copies)


func _process(_delta: float) -> void:
	var viewport := get_viewport_rect().size
	if viewport != _viewport:
		fit(viewport)
	# Parallax2D's screen offset: the view's centre less half the viewport
	# in pixels (camera zoom left out, as Parallax2D does).
	var centre := get_viewport().get_canvas_transform().affine_inverse() * (viewport / 2.0)
	screen_offset = centre - viewport / 2.0
