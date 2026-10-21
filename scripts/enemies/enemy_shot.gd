class_name EnemyShot
extends Area2D

## Something an enemy throws at the hero. Two kinds:
##
## - BOLT: a gob of hellfire that flies straight in `direction`, hurts the
##   hero once and breaks on the world (and on walls of angel light).
## - WAVE: a shockwave that runs along the floor from a brute's slam. It is
##   low enough to jump over, dies at the end of its ledge or at a wall,
##   and hurts the hero once.
##
## Drawn by hand like the hero's Holy Light bolt. Origin: the bolt's centre;
## the wave's feet.

enum Kind { BOLT, WAVE }

const BOLT_SPEED := 150.0
const BOLT_LIFETIME := 1.3
const BOLT_SIZE := Vector2(8, 6)
const BOLT_COLOR := Color(1.0, 0.5, 0.2)
const BOLT_CORE := Color(1.0, 0.9, 0.6)
const WAVE_SPEED := 120.0
const WAVE_LIFETIME := 1.1
## How tall the wave's hitbox is: a standing hero is hit, a jumping one is not.
const WAVE_SIZE := Vector2(12, 10)
const WAVE_COLOR := Color(0.75, 0.5, 0.35)
const WAVE_DUST := Color(0.9, 0.75, 0.6)

var kind := Kind.BOLT
var direction := Vector2.RIGHT
var damage := 1
var _time := 0.0
var _floor_ray: RayCast2D


func _ready() -> void:
	add_to_group("enemy_shot")
	collision_layer = 0
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	shape.shape = rect
	add_child(shape)
	body_entered.connect(_on_body_entered)
	if kind == Kind.BOLT:
		# The hero, the world and the walls of light.
		collision_mask = 2 | 1 | 8
		rect.size = BOLT_SIZE
	else:
		collision_mask = 2 | 1
		rect.size = WAVE_SIZE
		# A pixel above the floor, so the floor itself is never "entered".
		shape.position = Vector2(0.0, -WAVE_SIZE.y / 2.0 - 1.0)
		_floor_ray = RayCast2D.new()
		_floor_ray.position = Vector2(direction.x * WAVE_SIZE.x / 2.0, -2.0)
		_floor_ray.target_position = Vector2(0.0, 8.0)
		# The world and the one-way ledges.
		_floor_ray.collision_mask = 1 | 16
		add_child(_floor_ray)


func _physics_process(delta: float) -> void:
	_time += delta
	if kind == Kind.BOLT:
		position += direction * BOLT_SPEED * delta
		if _time >= BOLT_LIFETIME:
			_fade()
	else:
		position.x += direction.x * WAVE_SPEED * delta
		# The wave stops at the end of its ledge and when it has run out.
		# (Forced: a ray added this frame has not cast yet on its own.)
		_floor_ray.force_raycast_update()
		if _time >= WAVE_LIFETIME or not _floor_ray.is_colliding():
			_fade()
	queue_redraw()


func _draw() -> void:
	if kind == Kind.BOLT:
		var tail := -direction * 8.0
		draw_line(tail, Vector2.ZERO, BOLT_COLOR, 3.0)
		draw_rect(Rect2(Vector2(-2.0, -2.0), Vector2(4.0, 4.0)), BOLT_CORE)
		var flicker := 1.0 if int(_time * 30.0) % 2 == 0 else -1.0
		draw_rect(Rect2(Vector2(-1.0, -4.0 * flicker), Vector2(2.0, 2.0)), BOLT_COLOR)
	else:
		# A low hump of rubble with dust flying off its back.
		var points := PackedVector2Array(
			[Vector2(-7.0, 0.0), Vector2(-3.0, -8.0), Vector2(3.0, -9.0), Vector2(7.0, 0.0)]
		)
		draw_colored_polygon(points, WAVE_COLOR)
		var behind := -direction.x
		for i in 3:
			var dust_x := behind * (6.0 + i * 4.0) + sin(_time * 25.0 + i) * 1.5
			draw_rect(Rect2(Vector2(dust_x, -6.0 - i * 2.0), Vector2(2.0, 2.0)), WAVE_DUST)


func _on_body_entered(body: Node2D) -> void:
	if body is Player:
		# Deferred: the hero's hurt state toggles shapes, which the physics
		# server refuses while it is still flushing this overlap.
		body.take_damage.call_deferred(damage, global_position)
		Feedback.spark(global_position, BOLT_COLOR if kind == Kind.BOLT else WAVE_DUST)
		_fade()
	elif kind == Kind.BOLT:
		Feedback.spark(global_position, BOLT_COLOR)
		_fade()
	elif body is not CharacterBody2D:
		# The wave has hit a wall.
		_fade()


## Gone at once: no second hit.
func _fade() -> void:
	set_deferred("monitoring", false)
	set_physics_process(false)
	queue_free()
