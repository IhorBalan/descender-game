class_name GameCamera
extends Camera2D

## Follows the hero inside the level bounds. The hero is spawned at runtime,
## so main.gd assigns `target`. Built-in smoothing is off: this script does
## its own follow in the physics step so it can read the hero's state.
##
## - Horizontal: the hero walks freely inside a window around the camera
##   (DEADZONE_X each side) before it moves, and the view sits LOOK_AHEAD
##   ahead of him. The look-ahead only swaps sides once he has walked
##   LOOK_SWITCH_DISTANCE back the other way, so short turns don't swing it.
## - Vertical: jumps don't move the camera. It follows when he lands at a new
##   height, drops below the last ground, or climbs past UP_WINDOW, and leads
##   downward on fast falls.
## - Peek: standing still on the ground and holding down pans the view
##   PEEK_DISTANCE below after a short delay, so a drop can be looked at
##   before it is taken; letting go brings the view back.
## - The final position is rounded to whole pixels so tiles don't shimmer.
## - Shake: hits add trauma, which decays; the view is offset by a random
##   amount that grows with trauma squared, so small hits barely register and
##   big ones kick. The offset never moves the follow position itself.

## How far the view is zoomed out: 0.5 shows 640 x 360 level pixels, twice
## what the 320 x 180 base view would, so the hero takes half the height he
## otherwise would. The window still draws at its own resolution, so pixel
## art stays whole at any whole-number window scale.
const VIEW_ZOOM := 0.5

# Horizontal
const DEADZONE_X := 24.0
const LOOK_AHEAD := 40.0
const LOOK_SWITCH_DISTANCE := 32.0
const LOOK_AHEAD_SPEED := 90.0
const SMOOTH_X := 6.0

# Vertical
## The camera aims this far above the hero's feet (his chest).
const FOCUS_HEIGHT := 16.0
## How far above the last ground the hero can go before the camera follows.
## A bit more than a full jump (72 px) so normal jumps never move it.
const UP_WINDOW := 80.0
const FALL_LEAD := 40.0
const FALL_LEAD_SPEED := 200.0
const SMOOTH_Y := 4.0
const SMOOTH_Y_FALLING := 10.0

# Peek
const PEEK_DISTANCE := 48.0
const PEEK_DELAY := 0.25
const PEEK_SPEED := 160.0

# Shake
const MAX_SHAKE := 4.0
const TRAUMA_DECAY := 2.2

var target: Node2D
## 0 to 1; see add_trauma.
var trauma := 0.0

var _level_size := Vector2.ZERO
## Unrounded camera position; global_position is this rounded.
var _smoothed := Vector2.ZERO
## Horizontal centre of the deadzone window.
var _window_x := 0.0
## Height the camera rests at: the hero's focus point on the last ground.
var _ground_y := 0.0
var _look_dir := 1
var _look_offset := LOOK_AHEAD
## Furthest x the hero reached in the look direction; walking back
## LOOK_SWITCH_DISTANCE from it swaps the look-ahead.
var _look_extreme_x := 0.0
var _fall_offset := 0.0
var _peek_timer := 0.0
var _peek_offset := 0.0


func _ready() -> void:
	zoom = Vector2.ONE * VIEW_ZOOM
	add_to_group("game_camera")


func _physics_process(delta: float) -> void:
	_update_shake(delta)
	if not is_instance_valid(target):
		return
	var focus := _focus_point()
	_update_horizontal(focus.x, delta)
	_update_vertical(focus.y, delta)
	_update_peek(delta)
	var goal := _clamp_to_level(
		Vector2(_window_x + _look_offset, _ground_y + _fall_offset + _peek_offset)
	)
	var smooth_y := SMOOTH_Y_FALLING if _fall_offset > 0.0 else SMOOTH_Y
	_smoothed.x = lerpf(_smoothed.x, goal.x, 1.0 - exp(-SMOOTH_X * delta))
	_smoothed.y = lerpf(_smoothed.y, goal.y, 1.0 - exp(-smooth_y * delta))
	global_position = _smoothed.round()


func set_limits(level_size: Vector2) -> void:
	_level_size = level_size
	limit_left = 0
	limit_top = 0
	limit_right = int(level_size.x)
	limit_bottom = int(level_size.y)


## Jumps straight to the target with no smoothing, e.g. at spawn or respawn.
func snap_to_target() -> void:
	if not is_instance_valid(target):
		return
	var focus := _focus_point()
	_window_x = focus.x
	_ground_y = focus.y
	_look_dir = _target_facing()
	_look_offset = LOOK_AHEAD * _look_dir
	_look_extreme_x = focus.x
	_fall_offset = 0.0
	_peek_timer = 0.0
	_peek_offset = 0.0
	_smoothed = _clamp_to_level(Vector2(_window_x + _look_offset, _ground_y))
	global_position = _smoothed.round()
	reset_smoothing()


## Adds shake; amounts add up and are capped at 1.
func add_trauma(amount: float) -> void:
	trauma = minf(trauma + amount, 1.0)


func _update_shake(delta: float) -> void:
	if trauma <= 0.0:
		offset = Vector2.ZERO
		return
	trauma = maxf(trauma - TRAUMA_DECAY * delta, 0.0)
	var strength := trauma * trauma * MAX_SHAKE
	offset = Vector2(randf_range(-strength, strength), randf_range(-strength, strength)).round()


func _focus_point() -> Vector2:
	return target.global_position - Vector2(0.0, FOCUS_HEIGHT)


func _update_horizontal(x: float, delta: float) -> void:
	_window_x = clampf(_window_x, x - DEADZONE_X, x + DEADZONE_X)
	if _look_dir > 0:
		_look_extreme_x = maxf(_look_extreme_x, x)
		if x < _look_extreme_x - LOOK_SWITCH_DISTANCE:
			_look_dir = -1
			_look_extreme_x = x
	else:
		_look_extreme_x = minf(_look_extreme_x, x)
		if x > _look_extreme_x + LOOK_SWITCH_DISTANCE:
			_look_dir = 1
			_look_extreme_x = x
	_look_offset = move_toward(_look_offset, LOOK_AHEAD * _look_dir, LOOK_AHEAD_SPEED * delta)


func _update_vertical(y: float, delta: float) -> void:
	var body := target as CharacterBody2D
	var on_floor := body != null and body.is_on_floor()
	if on_floor or y > _ground_y:
		_ground_y = y
	elif y < _ground_y - UP_WINDOW:
		_ground_y = y + UP_WINDOW
	var falling_fast := body != null and not on_floor and body.velocity.y > FALL_LEAD_SPEED
	var lead := FALL_LEAD if falling_fast else 0.0
	_fall_offset = move_toward(_fall_offset, lead, FALL_LEAD * 4.0 * delta)


## Holding down while standing still on the ground, with nothing holding
## the hero's input, peeks below after PEEK_DELAY.
func _update_peek(delta: float) -> void:
	var peeking := _wants_peek()
	_peek_timer = _peek_timer + delta if peeking else 0.0
	var lead := PEEK_DISTANCE if peeking and _peek_timer >= PEEK_DELAY else 0.0
	_peek_offset = move_toward(_peek_offset, lead, PEEK_SPEED * delta)


func _wants_peek() -> bool:
	var player := target as Player
	if player == null or not player.is_on_floor():
		return false
	if player.frozen or player.talking or player.state != Player.State.IDLE:
		return false
	return Input.is_action_pressed("move_down") and not Input.is_action_pressed("jump")


## How far below the hero the view is peeking, 0 when it is not.
func peek_offset() -> float:
	return _peek_offset


func _target_facing() -> int:
	var facing = target.get("facing")
	return -1 if facing is int and facing < 0 else 1


func _clamp_to_level(point: Vector2) -> Vector2:
	if _level_size == Vector2.ZERO:
		return point
	var half := get_viewport_rect().size / zoom / 2.0
	return Vector2(
		clampf(point.x, half.x, maxf(_level_size.x - half.x, half.x)),
		clampf(point.y, half.y, maxf(_level_size.y - half.y, half.y))
	)
