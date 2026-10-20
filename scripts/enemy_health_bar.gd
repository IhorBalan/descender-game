class_name EnemyHealthBar
extends Node2D

## A small bar over an enemy's head that appears when it is hit and hides
## again a moment later, so the screen is not full of bars.

const WIDTH := 16.0
const HEIGHT := 3.0
const SHOW_TIME := 1.5
const BACK := Color(0.0, 0.0, 0.0, 0.6)
const FILL := Color(0.96, 0.3, 0.3)
const FILL_LOW := Color(1.0, 0.75, 0.3)

var current := 1
var maximum := 1
var _timer := 0.0


func _ready() -> void:
	visible = false
	z_index = 5


func _process(delta: float) -> void:
	if _timer > 0.0:
		_timer -= delta
		if _timer <= 0.0:
			visible = false


## Shows the bar at this health for a while.
func show_health(value: int, max_value: int) -> void:
	current = clampi(value, 0, max_value)
	maximum = maxi(max_value, 1)
	_timer = SHOW_TIME
	visible = true
	queue_redraw()


func _draw() -> void:
	var left := -WIDTH / 2.0
	draw_rect(Rect2(left - 1.0, -1.0, WIDTH + 2.0, HEIGHT + 2.0), BACK)
	var fill := WIDTH * float(current) / float(maximum)
	var color := FILL if current * 2 > maximum else FILL_LOW
	draw_rect(Rect2(left, 0.0, roundf(fill), HEIGHT), color)
