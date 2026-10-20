class_name Walker
extends CharacterBody2D

## A ground enemy that patrols a platform, turning at edges and walls, and
## hurts the hero on contact. When the hero stands close in front of it, it
## stops and swings: a visible wind-up, a short strike with a hitbox in
## front, then a recovery it cannot act in. A hit during the wind-up or the
## strike cancels the attack, so stepping in first is rewarded.
## The node's origin is at the feet. `character` picks the sprite strips and
## the size/speed/attack profile below.

enum State { PATROL, WINDUP, STRIKE, RECOVER }

const GRAVITY := 700.0
const MAX_FALL_SPEED := 260.0
const HURT_TIME := 0.2
const KNOCKBACK := Vector2(90.0, -60.0)
## The hero has to be roughly at the same height to be attacked.
const ATTACK_HEIGHT_TOLERANCE := 24.0
const TINT_WINDUP := Color(1.0, 0.75, 0.55)
const TINT_HURT := Color(1.0, 0.5, 0.5)
const DEATH_TIME := 0.5
const DEATH_SPARKS := Color(1.0, 0.45, 0.35)
const HEALTH_BAR_SCENE := preload("res://scripts/enemy_health_bar.gd")
const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")

## body: collision size. speed: patrol speed. health: hits to kill.
## walk: the looping animation name. faces_left: whether the strips face left.
## attack_range: how close (horizontally) the hero must be to trigger a swing.
## reach: the strike hitbox size, placed in front of the body.
## windup / strike / recover: the three phases of a swing, in seconds.
## cooldown: pause before the next swing can start. attack_damage: hearts.
## drop_chance: how often a kill leaves a heal mote behind.
const PROFILES := {
	"imp":
	{
		"body": Vector2(18, 24),
		"speed": 32.0,
		"health": 3,
		"walk": "run",
		"faces_left": true,
		"attack_range": 30.0,
		"reach": Vector2(22, 20),
		"windup": 0.35,
		"strike": 0.15,
		"recover": 0.45,
		"cooldown": 0.8,
		"attack_damage": 1,
		"drop_chance": 0.35,
	},
	"angel_guard":
	{
		"body": Vector2(24, 56),
		"speed": 18.0,
		"health": 5,
		"walk": "walk",
		"faces_left": true,
		"attack_range": 44.0,
		"reach": Vector2(36, 40),
		"windup": 0.6,
		"strike": 0.2,
		"recover": 0.7,
		"cooldown": 1.2,
		"attack_damage": 2,
		"drop_chance": 1.0,
	},
}

@export_enum("imp", "angel_guard") var character := "imp"
@export var contact_damage := 1
## Falling below this y (set by the level) removes the enemy, so one knocked
## off its platform does not fall forever.
var death_y := INF

var state: State = State.PATROL
var health := 0
var direction := -1
var speed := 30.0
var health_bar: EnemyHealthBar
var _profile: Dictionary = {}
var _walk_anim := "run"
var _half_width := 0.0
var _faces_left := true
var _hurt_timer := 0.0
var _state_timer := 0.0
var _attack_cooldown := 0.0
var _dead := false
var _player: Player

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var body_shape: CollisionShape2D = $CollisionShape2D
@onready var hitbox: Area2D = $Hitbox
@onready var hitbox_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var attack_hitbox: Area2D = $AttackHitbox
@onready var attack_shape: CollisionShape2D = $AttackHitbox/CollisionShape2D
@onready var floor_ray: RayCast2D = $FloorRay


func _ready() -> void:
	add_to_group("enemy")
	if not PROFILES.has(character):
		push_error("Walker: unknown character '%s', using imp" % character)
		character = "imp"
	_profile = PROFILES[character]
	var body: Vector2 = _profile["body"]
	health = _profile["health"]
	speed = _profile["speed"]
	_walk_anim = _profile["walk"]
	_faces_left = _profile["faces_left"]

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
	var reach: Vector2 = _profile["reach"]
	var attack_rect := RectangleShape2D.new()
	attack_rect.size = reach
	attack_shape.shape = attack_rect
	attack_shape.disabled = true
	floor_ray.position.y = -2.0
	floor_ray.target_position = Vector2(0.0, 10.0)
	health_bar = HEALTH_BAR_SCENE.new()
	health_bar.position = Vector2(0.0, -body.y - 6.0)
	add_child(health_bar)
	_face(direction)


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL_SPEED)
	_attack_cooldown = maxf(_attack_cooldown - delta, 0.0)
	if _hurt_timer > 0.0:
		_hurt_timer -= delta
		velocity.x = move_toward(velocity.x, 0.0, 300.0 * delta)
		if _hurt_timer <= 0.0:
			sprite.modulate = Color.WHITE
			sprite.play(_walk_anim)
	else:
		match state:
			State.PATROL:
				_process_patrol()
			_:
				_process_attack(delta)
	move_and_slide()
	if is_on_wall() and _hurt_timer <= 0.0 and state == State.PATROL:
		_face(-direction)
	if global_position.y > death_y:
		queue_free()
		return
	_hurt_player()


func _process_patrol() -> void:
	if _hero_in_reach() and _attack_cooldown <= 0.0:
		_start_windup()
		return
	if is_on_floor() and not floor_ray.is_colliding():
		_face(-direction)
	velocity.x = direction * speed


## True when the hero is alive, close in front or behind, and about level.
func _hero_in_reach() -> bool:
	if not is_instance_valid(_player):
		_player = get_tree().get_first_node_in_group("player") as Player
		if _player == null:
			return false
	if _player.state == Player.State.DEAD:
		return false
	var offset := _player.global_position - global_position
	return (
		absf(offset.x) <= float(_profile["attack_range"])
		and absf(offset.y) <= ATTACK_HEIGHT_TOLERANCE
	)


func _start_windup() -> void:
	# Turn to the hero and plant the feet; the swing telegraphs itself.
	_face(1 if _player.global_position.x >= global_position.x else -1)
	velocity.x = 0.0
	state = State.WINDUP
	_state_timer = _profile["windup"]
	sprite.animation = "attack"
	sprite.pause()
	sprite.frame = 0
	sprite.modulate = TINT_WINDUP


func _process_attack(delta: float) -> void:
	velocity.x = 0.0
	_state_timer -= delta
	if state == State.STRIKE:
		for body in attack_hitbox.get_overlapping_bodies():
			if body is Player:
				body.take_damage(_profile["attack_damage"], global_position)
	if _state_timer > 0.0:
		return
	match state:
		State.WINDUP:
			state = State.STRIKE
			_state_timer = _profile["strike"]
			sprite.modulate = Color.WHITE
			sprite.play("attack")
			attack_shape.disabled = false
		State.STRIKE:
			state = State.RECOVER
			_state_timer = _profile["recover"]
			attack_shape.disabled = true
			sprite.play("idle")
		State.RECOVER:
			_end_attack()


## Back to patrolling, with the cooldown running.
func _end_attack() -> void:
	state = State.PATROL
	attack_shape.disabled = true
	_attack_cooldown = _profile["cooldown"]
	sprite.modulate = Color.WHITE
	sprite.play(_walk_anim)


func _face(dir: int) -> void:
	direction = dir
	sprite.flip_h = (dir > 0) == _faces_left
	floor_ray.position.x = (_half_width + 2.0) * dir
	floor_ray.force_raycast_update()
	var reach: Vector2 = _profile["reach"]
	attack_shape.position = Vector2((_half_width + reach.x / 2.0) * dir, -reach.y / 2.0)


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
	health_bar.show_health(health, _profile["health"])
	Audio.play("hit")
	Feedback.flash(sprite, 0.1)
	Feedback.spark(global_position + Vector2(0.0, -body_shape.shape.get_rect().size.y / 2.0))
	if state != State.PATROL:
		# Getting hit mid-swing cancels it (and starts the cooldown).
		_end_attack()
	_hurt_timer = HURT_TIME
	sprite.modulate = TINT_HURT
	sprite.play("hurt")
	var dir := 1.0 if global_position.x >= source_position.x else -1.0
	velocity = Vector2(KNOCKBACK.x * dir, KNOCKBACK.y)
	if health <= 0:
		_die()


## Death: the body is thrown back and topples over while it fades, with a
## burst of sparks, and sometimes leaves a heal mote behind.
func _die() -> void:
	_dead = true
	set_physics_process(false)
	Audio.play("enemy_die")
	Feedback.shake(0.3)
	Feedback.spark(global_position + Vector2(0.0, body_shape.position.y), DEATH_SPARKS)
	body_shape.set_deferred("disabled", true)
	hitbox.set_deferred("monitoring", false)
	attack_hitbox.set_deferred("monitoring", false)
	health_bar.visible = false
	sprite.animation = "hurt"
	sprite.pause()
	sprite.frame = 0
	sprite.modulate = Color.WHITE
	# Topple away from the hero, who is on the side the body faces.
	var fall_dir := -direction
	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(sprite, "rotation", fall_dir * PI / 2.0, DEATH_TIME).set_ease(
		Tween.EASE_OUT
	)
	tween.tween_property(sprite, "position:x", fall_dir * 6.0, DEATH_TIME).set_ease(Tween.EASE_OUT)
	tween.tween_property(sprite, "modulate:a", 0.0, DEATH_TIME * 0.5).set_delay(DEATH_TIME * 0.5)
	tween.chain().tween_callback(queue_free)
	_maybe_drop_mote()


func _maybe_drop_mote() -> void:
	if randf() >= float(_profile["drop_chance"]):
		return
	var mote: HealMote = MOTE_SCENE.instantiate()
	mote.position = global_position + Vector2(0.0, body_shape.position.y)
	get_parent().add_child.call_deferred(mote)
