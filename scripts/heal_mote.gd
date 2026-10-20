class_name HealMote
extends Area2D

## A mote of light some enemies leave behind. Touching it restores one
## heart. It fades after a while so the level does not fill up with them.

signal collected

const HEAL := 1
const LIFETIME := 8.0
## The mote blinks for its last seconds.
const BLINK_TIME := 2.0
const BOB_HEIGHT := 2.0
const BOB_SPEED := 4.0
const COLOR := Color(1.0, 0.95, 0.6)
const CORE := Color(1.0, 1.0, 0.9)

var _time := 0.0
var _rest_y := 0.0
var _done := false


func _ready() -> void:
	add_to_group("heal_mote")
	_rest_y = position.y
	body_entered.connect(_on_body_entered)
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 6.0
	shape.shape = circle
	add_child(shape)


func _process(delta: float) -> void:
	_time += delta
	# A hero who was at full health when he stepped on the mote and got
	# hurt while standing on it takes it now, without stepping off and on.
	for body in get_overlapping_bodies():
		_on_body_entered(body)
	position.y = _rest_y + sin(_time * BOB_SPEED) * BOB_HEIGHT
	if _time >= LIFETIME:
		queue_free()
		return
	var left := LIFETIME - _time
	visible = left > BLINK_TIME or fmod(left, 0.2) < 0.12
	queue_redraw()


func _draw() -> void:
	var pulse := 2.0 + sin(_time * 6.0) * 0.5
	draw_circle(Vector2.ZERO, pulse + 1.5, Color(COLOR, 0.35))
	draw_circle(Vector2.ZERO, pulse, COLOR)
	draw_circle(Vector2.ZERO, pulse * 0.45, CORE)


func _on_body_entered(body: Node2D) -> void:
	if _done or not body is Player or body.state == Player.State.DEAD:
		return
	if not body.restore_health(HEAL):
		return
	_done = true
	set_deferred("monitoring", false)
	Audio.play("pickup", -4.0)
	Feedback.spark(global_position, COLOR)
	collected.emit()
	queue_free()
