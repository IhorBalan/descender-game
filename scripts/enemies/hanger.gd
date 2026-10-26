class_name Hanger
extends CharacterBody2D

## The hanged thing: a frozen traitor on a rope under the ceilings of
## Treachery. It hangs where the placement puts its feet, swaying, with the
## rope drawn up to whatever ceiling is above it. When the hero walks
## beneath it the rope creaks (it twitches, the eyes open), then it lets go
## and drops on him, hard. It lies where it lands for a moment, when it can
## be hit, and the rope hauls it back up for the next pass. Killed, it
## falls for good. Origin at the feet; the rope's knot is at the top of
## the head. The scene sets the sprite's frames (assets/sprites/hanged/).

enum State { HANG, CREAK, DROP, REST, CLIMB }

const CHARACTER := "hanged"
const GRAVITY := 700.0
const MAX_FALL_SPEED := 300.0
const HURT_TIME := 0.2
const KNOCKBACK := Vector2(60.0, -30.0)
## How far to the side the hero may be for it to let go, and how far below.
const DROP_RANGE_X := 18.0
const DROP_RANGE_Y := 220.0
const CREAK_TIME := 0.35
const REST_TIME := 1.4
const CLIMB_TIME := 1.2
const DROP_COOLDOWN := 1.0
const CORPSE_TIME := 1.0
## The knot in the strip's frame, from the feet (see tools/enemy_sprites/hanged.py).
const KNOT := Vector2(0.0, -58.0)
const DROP_DAMAGE := 2
const CONTACT_DAMAGE := 1
const DEATH_SPARKS := Color(0.75, 0.85, 1.0)
const DEATH_PUFF := Color(0.8, 0.88, 1.0)
const DEATH_HIT_STOP := 0.06
const TINT_CREAK := Color(1.0, 0.8, 0.8)
const TINT_HURT := Color(1.0, 0.5, 0.5)
const DROP_CHANCE := 0.5
## What it leaks and carried (see Loot): a traitor's light, and a bone now
## and then.
const LOOT := {"light": [7, 10], "bone_chance": 0.12, "flask_chance": 0.04, "xp": 8}
const HEALTH_BAR_SCENE := preload("res://scripts/enemy_health_bar.gd")
const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")

@export var max_health := 3

var state: State = State.HANG
var health := 0
## Where the feet hang; the rope goes from the knot above it to the ceiling.
var home := Vector2.ZERO
var anchor := Vector2.ZERO
var health_bar: EnemyHealthBar
## Where a dropped heal mote goes (set by the level).
var drop_parent: Node
## Falling below this y (set by the level) removes it.
var death_y := INF
var _time := 0.0
var _hurt_timer := 0.0
var _state_timer := 0.0
var _cooldown := 0.0
var _dead := false
var _player: Player
var _climb_tween: Tween

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var rope: Line2D = $Rope
@onready var body_shape: CollisionShape2D = $CollisionShape2D
@onready var hitbox: Area2D = $Hitbox
@onready var ceiling_ray: RayCast2D = $CeilingRay


func _ready() -> void:
	add_to_group("enemy")
	sprite.play("idle")
	health = max_health
	home = global_position
	_time = randf() * TAU
	health_bar = HEALTH_BAR_SCENE.new()
	health_bar.position = Vector2(0.0, -70.0)
	add_child(health_bar)
	# The rope runs to the ceiling above the knot, or a few tiles up when
	# nothing is there (a hanger placed in the open).
	ceiling_ray.force_raycast_update()
	var top := (
		ceiling_ray.get_collision_point()
		if ceiling_ray.is_colliding()
		else (home + KNOT + Vector2(0.0, -48.0))
	)
	anchor = top
	_draw_rope()


func _physics_process(delta: float) -> void:
	_time += delta
	_state_timer -= delta
	_cooldown = maxf(_cooldown - delta, 0.0)
	if _dead:
		_fall(delta)
		move_and_slide()
		_draw_rope()
		return
	if _hurt_timer > 0.0:
		_hurt_timer -= delta
		if _hurt_timer <= 0.0:
			sprite.modulate = Color.WHITE
			_show_state()
	match state:
		State.HANG:
			velocity = Vector2.ZERO
			# A small sway on the rope.
			sprite.position.x = sin(_time * 1.3) * 1.5
			if _cooldown <= 0.0 and _hero_beneath():
				_start_creak()
		State.CREAK:
			velocity = Vector2.ZERO
			if _state_timer <= 0.0:
				_let_go()
		State.DROP:
			_fall(delta)
		State.REST:
			velocity.x = move_toward(velocity.x, 0.0, 200.0 * delta)
			_fall(delta)
			if _state_timer <= 0.0:
				_start_climb()
		State.CLIMB:
			velocity = Vector2.ZERO
			if _state_timer <= 0.0:
				_hang_again()
	move_and_slide()
	if state == State.DROP and is_on_floor():
		_land()
	if global_position.y > death_y:
		queue_free()
		return
	_draw_rope()
	_hurt_player()


func _fall(delta: float) -> void:
	if not is_on_floor():
		velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL_SPEED)


## True when the hero is alive, right under it and not too far down.
func _hero_beneath() -> bool:
	if not _find_player():
		return false
	var offset := _player.global_position - global_position
	return absf(offset.x) <= DROP_RANGE_X and offset.y > 0.0 and offset.y <= DROP_RANGE_Y


func _find_player() -> bool:
	if not is_instance_valid(_player):
		_player = get_tree().get_first_node_in_group("player") as Player
	return _player != null and _player.state != Player.State.DEAD


## The warning: the rope creaks, the eyes open, it twitches.
func _start_creak() -> void:
	state = State.CREAK
	_state_timer = CREAK_TIME
	sprite.position.x = 0.0
	sprite.animation = "attack"
	sprite.pause()
	sprite.frame = 0
	sprite.modulate = TINT_CREAK
	Audio.play("creak", -4.0)


func _let_go() -> void:
	state = State.DROP
	sprite.modulate = Color.WHITE
	sprite.play("attack")
	velocity = Vector2.ZERO


## It hits the floor, and lies there.
func _land() -> void:
	state = State.REST
	_state_timer = REST_TIME
	velocity = Vector2.ZERO
	Feedback.shake(0.3)
	Feedback.puff(global_position, DEATH_PUFF)
	Audio.play("land", -2.0)
	sprite.animation = "death"
	sprite.pause()
	sprite.frame = 2


## The rope hauls it back up to where it hung.
func _start_climb() -> void:
	state = State.CLIMB
	_state_timer = CLIMB_TIME
	sprite.play("hurt")
	sprite.pause()
	sprite.frame = 1
	sprite.modulate = Color.WHITE
	_climb_tween = create_tween()
	_climb_tween.tween_property(self, "global_position", home, CLIMB_TIME).set_ease(
		Tween.EASE_IN_OUT
	)


func _hang_again() -> void:
	state = State.HANG
	_cooldown = DROP_COOLDOWN
	global_position = home
	velocity = Vector2.ZERO
	sprite.play("idle")


## The frame the state holds, once a flinch is over: the sway on the rope,
## the heap it lies in, the slump as it is hauled up, or the fall.
func _show_state() -> void:
	match state:
		State.REST:
			sprite.animation = "death"
			sprite.pause()
			sprite.frame = 2
		State.CLIMB:
			sprite.animation = "hurt"
			sprite.pause()
			sprite.frame = 1
		State.DROP, State.CREAK:
			sprite.play("attack")
		_:
			sprite.play("idle")


func _draw_rope() -> void:
	rope.points = PackedVector2Array([to_local(anchor), KNOT + sprite.position])


func _hurt_player() -> void:
	if not hitbox.has_overlapping_bodies():
		return
	for body in hitbox.get_overlapping_bodies():
		if body is Player:
			var damage := DROP_DAMAGE if state == State.DROP else CONTACT_DAMAGE
			body.take_damage(damage, global_position)


func take_damage(amount: int, source_position: Vector2) -> void:
	if _dead:
		return
	health -= amount
	health_bar.show_health(health, max_health)
	Audio.play("hit")
	Feedback.flash(sprite, 0.1)
	Feedback.spark(global_position + Vector2(0.0, -22.0))
	if health <= 0:
		_die()
		return
	# A hit while it hangs or creaks sets it swinging but does not drop it;
	# one while it rests pushes it along the floor.
	if state == State.REST:
		var dir := 1.0 if global_position.x >= source_position.x else -1.0
		velocity = Vector2(KNOCKBACK.x * dir, KNOCKBACK.y)
	if state == State.CREAK:
		_let_go()
		return
	_hurt_timer = HURT_TIME
	sprite.modulate = TINT_HURT
	sprite.play("hurt")


## Death: the rope breaks, time stops for a blink, and it falls for good,
## folding into a heap (the death strip) where it lands, then fades.
func _die() -> void:
	_dead = true
	if _climb_tween and _climb_tween.is_valid():
		_climb_tween.kill()
	Audio.play("enemy_die")
	Feedback.hit_stop(DEATH_HIT_STOP)
	Feedback.shake(0.3)
	Feedback.spark(global_position + Vector2(0.0, -22.0), DEATH_SPARKS)
	Feedback.puff(global_position + Vector2(0.0, -16.0), DEATH_PUFF)
	hitbox.set_deferred("monitoring", false)
	health_bar.visible = false
	sprite.modulate = Color.WHITE
	sprite.position.x = 0.0
	rope.visible = false
	EnemyDeath.play(sprite, 1, CORPSE_TIME, queue_free)
	var parent := drop_parent if drop_parent else get_parent()
	Loot.drop(parent, global_position + Vector2(0.0, -22.0), LOOT)
	if randf() < DROP_CHANCE:
		var mote: HealMote = MOTE_SCENE.instantiate()
		mote.position = global_position + Vector2(0.0, -22.0)
		parent.add_child.call_deferred(mote)
