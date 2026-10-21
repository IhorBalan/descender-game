class_name LightBolt
extends Area2D

## A bolt of Holy Light: flies straight from the hero's hand, hurts the
## first enemy it meets and breaks on the world. Drawn as a short streak
## with a bright head.

const SPEED := 230.0
const LIFETIME := 0.55
const DAMAGE := 1
const COLOR := Color(1.0, 0.95, 0.7)
const HEAD := Color(1.0, 1.0, 0.9)

var direction := 1
var _time := 0.0


func _ready() -> void:
	add_to_group("light_bolt")
	collision_layer = 0
	# Enemies and the world.
	collision_mask = 4 | 1
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(10, 6)
	shape.shape = rect
	add_child(shape)
	body_entered.connect(_on_body_entered)


func _physics_process(delta: float) -> void:
	position.x += direction * SPEED * delta
	_time += delta
	queue_redraw()
	if _time >= LIFETIME:
		_fade()


func _draw() -> void:
	var tail := -direction * 10.0
	draw_line(Vector2(tail, 0.0), Vector2(0.0, 0.0), COLOR, 2.0)
	draw_rect(Rect2(Vector2(-2.0, -2.0), Vector2(4.0, 4.0)), HEAD)


func _on_body_entered(body: Node2D) -> void:
	if body is Player:
		return
	if body.has_method("take_damage"):
		body.take_damage(DAMAGE, global_position - Vector2(direction * 20.0, 0.0))
	Feedback.spark(global_position, COLOR)
	_fade()


## Gone at once: no second hit, no lingering streak.
func _fade() -> void:
	set_deferred("monitoring", false)
	set_physics_process(false)
	queue_free()
