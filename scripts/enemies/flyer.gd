class_name Flyer
extends CharacterBody2D

## A Watcher: an angel eye that hovers around its post and chases the hero
## when he comes close. Hurts on contact. Origin at the sprite's centre.

const CHARACTER := "watcher"
const HOVER_SPEED := 25.0
const CHASE_SPEED := 55.0
const ACCEL := 220.0
const AGGRO_RANGE := 110.0
const HURT_TIME := 0.25
const KNOCKBACK := 120.0

@export var max_health := 2
@export var contact_damage := 1

var health := 0
var home := Vector2.ZERO
var _time := 0.0
var _hurt_timer := 0.0
var _dead := false
var _player: Player

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var body_shape: CollisionShape2D = $CollisionShape2D
@onready var hitbox: Area2D = $Hitbox


func _ready() -> void:
	add_to_group("enemy")
	AnimLoader.setup(sprite, CHARACTER)
	sprite.play("idle")
	health = max_health
	home = global_position
	_time = randf() * TAU
	_player = get_tree().get_first_node_in_group("player") as Player


func _physics_process(delta: float) -> void:
	_time += delta
	if _hurt_timer > 0.0:
		_hurt_timer -= delta
		velocity = velocity.move_toward(Vector2.ZERO, ACCEL * delta)
		if _hurt_timer <= 0.0:
			sprite.modulate = Color.WHITE
			sprite.play("idle")
	else:
		var target := home + Vector2(sin(_time * 1.6) * 24.0, sin(_time * 2.7) * 6.0)
		var speed := HOVER_SPEED
		if (
			is_instance_valid(_player)
			and _player.state != Player.State.DEAD
			and global_position.distance_to(_player.global_position) < AGGRO_RANGE
		):
			target = _player.global_position + Vector2(0.0, -16.0)
			speed = CHASE_SPEED
		var wanted := (target - global_position).limit_length(speed)
		velocity = velocity.move_toward(wanted, ACCEL * delta)
	move_and_slide()
	_hurt_player()


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
	var away := (global_position - source_position).normalized()
	if away == Vector2.ZERO:
		away = Vector2.UP
	velocity = away * KNOCKBACK
	if health <= 0:
		_die()


func _die() -> void:
	_dead = true
	set_physics_process(false)
	body_shape.set_deferred("disabled", true)
	hitbox.set_deferred("monitoring", false)
	var tween := create_tween()
	tween.tween_property(sprite, "modulate:a", 0.0, 0.3)
	tween.parallel().tween_property(sprite, "scale", Vector2(1.4, 1.4), 0.3)
	tween.tween_callback(queue_free)
