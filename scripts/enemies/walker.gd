class_name Walker
extends CharacterBody2D

## A ground enemy that patrols a platform, turning at edges and walls, and
## hurts the hero on contact. The node's origin is at the feet.
## `character` picks the sprite strips and the size/speed profile below.

const GRAVITY := 700.0
const MAX_FALL_SPEED := 260.0
const HURT_TIME := 0.2
const KNOCKBACK := Vector2(90.0, -60.0)

const PROFILES := {
	"imp": {"body": Vector2(18, 24), "speed": 32.0, "health": 3, "walk": "run", "faces_left": true},
	"angel_guard": {"body": Vector2(24, 56), "speed": 18.0, "health": 5, "walk": "walk", "faces_left": true},
}

@export_enum("imp", "angel_guard") var character := "imp"
@export var contact_damage := 1
## Falling below this y (set by the level) removes the enemy, so one knocked
## off its platform does not fall forever.
var death_y := INF

var health := 0
var direction := -1
var speed := 30.0
var _walk_anim := "run"
var _half_width := 0.0
var _faces_left := true
var _hurt_timer := 0.0
var _dead := false

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var body_shape: CollisionShape2D = $CollisionShape2D
@onready var hitbox: Area2D = $Hitbox
@onready var hitbox_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var floor_ray: RayCast2D = $FloorRay


func _ready() -> void:
	add_to_group("enemy")
	if not PROFILES.has(character):
		push_error("Walker: unknown character '%s', using imp" % character)
		character = "imp"
	var profile: Dictionary = PROFILES[character]
	var body: Vector2 = profile["body"]
	health = profile["health"]
	speed = profile["speed"]
	_walk_anim = profile["walk"]
	_faces_left = profile["faces_left"]

	AnimLoader.setup(sprite, character)
	sprite.play(_walk_anim)
	_half_width = body.x / 2.0
	var shape := RectangleShape2D.new()
	shape.size = body
	body_shape.shape = shape
	body_shape.position = Vector2(0.0, -body.y / 2.0)
	var hit_shape := RectangleShape2D.new()
	hit_shape.size = body + Vector2(4.0, 0.0)
	hitbox_shape.shape = hit_shape
	hitbox_shape.position = body_shape.position
	floor_ray.position.y = -2.0
	floor_ray.target_position = Vector2(0.0, 10.0)
	_face(direction)


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL_SPEED)
	if _hurt_timer > 0.0:
		_hurt_timer -= delta
		velocity.x = move_toward(velocity.x, 0.0, 300.0 * delta)
		if _hurt_timer <= 0.0:
			sprite.modulate = Color.WHITE
			sprite.play(_walk_anim)
	else:
		if is_on_floor() and not floor_ray.is_colliding():
			_face(-direction)
		velocity.x = direction * speed
	move_and_slide()
	if is_on_wall() and _hurt_timer <= 0.0:
		_face(-direction)
	if global_position.y > death_y:
		queue_free()
		return
	_hurt_player()


func _face(dir: int) -> void:
	direction = dir
	sprite.flip_h = (dir > 0) == _faces_left
	floor_ray.position.x = (_half_width + 2.0) * dir
	floor_ray.force_raycast_update()


func _hurt_player() -> void:
	if not hitbox.has_overlapping_bodies():
		return
	for body in hitbox.get_overlapping_bodies():
		if body is Player:
			body.take_damage(contact_damage, global_position)


func take_damage(amount: int, source_position: Vector2) -> void:
	if _dead:
		return
	health -= amount
	_hurt_timer = HURT_TIME
	sprite.modulate = Color(1.0, 0.5, 0.5)
	sprite.play("hurt")
	var dir := 1.0 if global_position.x >= source_position.x else -1.0
	velocity = Vector2(KNOCKBACK.x * dir, KNOCKBACK.y)
	if health <= 0:
		_die()


func _die() -> void:
	_dead = true
	set_physics_process(false)
	body_shape.set_deferred("disabled", true)
	hitbox.set_deferred("monitoring", false)
	var tween := create_tween()
	tween.tween_property(sprite, "modulate:a", 0.0, 0.3)
	tween.parallel().tween_property(sprite, "scale", Vector2(1.3, 0.4), 0.3)
	tween.tween_callback(queue_free)
