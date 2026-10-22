class_name LightDrop
extends Area2D

## Currency lying where an enemy died: a cluster of light (what a killed
## thing leaks) or a tally bone. It bobs where it fell and flies to the
## hero once he comes near; touching it puts the amount in the purse. A
## drop goes with the heal motes when the enemies come back (the "loot"
## group), so light left lying is lost to a death.

signal collected(currency: String, amount: int)

const BOB_HEIGHT := 1.5
const BOB_SPEED := 3.5
## How close the hero has to be for the drop to come to him, and how fast
## it goes.
const PULL_RANGE := 56.0
const PULL_SPEED := 220.0
const LIGHT_COLOR := Color(1.0, 0.95, 0.65)
const LIGHT_CORE := Color(1.0, 1.0, 0.9)
const BONE_COLOR := Color(0.95, 0.92, 0.85)
const BONE_SHADE := Color(0.6, 0.55, 0.5)

var currency := "light"
var amount := 1

var _time := randf() * TAU
var _rest := Vector2.ZERO
var _pulled := false
var _done := false


func _ready() -> void:
	add_to_group("loot")
	collision_layer = 0
	collision_mask = 2
	_rest = position
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 7.0
	shape.shape = circle
	add_child(shape)
	body_entered.connect(_on_body_entered)


func _physics_process(delta: float) -> void:
	_time += delta
	var player := get_tree().get_first_node_in_group("player") as Player
	if player != null and player.state != Player.State.DEAD:
		var target := player.global_position + Vector2(0.0, -14.0)
		if _pulled or global_position.distance_to(target) <= PULL_RANGE:
			_pulled = true
			global_position = global_position.move_toward(target, PULL_SPEED * delta)
			# A drop that has flown onto the hero counts, even if the overlap
			# signal already fired while he was out of reach of it.
			if global_position.distance_to(target) < 4.0:
				_on_body_entered(player)
	if not _pulled:
		position.y = _rest.y + sin(_time * BOB_SPEED) * BOB_HEIGHT
	queue_redraw()


func _draw() -> void:
	if currency == "bones":
		draw_rect(Rect2(-4, 0, 9, 2), BONE_SHADE)
		draw_rect(Rect2(-4, -1, 9, 2), BONE_COLOR)
		draw_rect(Rect2(-5, -2, 2, 4), BONE_COLOR)
		draw_rect(Rect2(4, -2, 2, 4), BONE_COLOR)
		return
	# A few motes for a few light, more for more.
	var motes := clampi(1 + amount / 4, 1, 4)
	for i in motes:
		var angle := _time * 1.5 + i * TAU / motes
		var at := Vector2(cos(angle), sin(angle) * 0.5) * (3.0 if motes > 1 else 0.0)
		var pulse := 1.5 + sin(_time * 6.0 + i) * 0.4
		draw_circle(at, pulse + 1.0, Color(LIGHT_COLOR, 0.3))
		draw_circle(at, pulse, LIGHT_COLOR)
		draw_circle(at, pulse * 0.4, LIGHT_CORE)


func _on_body_entered(body: Node2D) -> void:
	if _done or not body is Player or body.state == Player.State.DEAD:
		return
	_done = true
	set_deferred("monitoring", false)
	body.collect_currency(currency, amount)
	Audio.play("pickup", -8.0 if currency == "light" else -2.0)
	Feedback.spark(global_position, LIGHT_COLOR if currency == "light" else BONE_COLOR)
	collected.emit(currency, amount)
	queue_free()
