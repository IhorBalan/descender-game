class_name Flyer
extends CharacterBody2D

## A flying enemy that hovers around its post and chases the hero when he
## comes close. Hurts on contact. Origin at the sprite's centre. `character`
## picks the strips and the numbers (PROFILES): the **watcher** is an angel
## eye, found where the angels are (Heaven, Purgatory); the **shade** is a
## forgotten soul that drifts through the circles from Limbo down; the
## **siren** is the winged want of Lust, quicker and keener than either.
##
## With the `dive` behaviour (set by the level from the placement) it keeps
## above the hero instead, and once it is over him it stops, the eye turns
## red (the attack strip), and it drops through him and climbs back up for
## the next pass.

enum State { HOVER, AIM, DIVE, CLIMB }

const ACCEL := 220.0
const HURT_TIME := 0.25
const KNOCKBACK := 120.0
## The eye drops this far as it dies, then lies there this long.
const DEATH_DROP := 12.0
const CORPSE_TIME := 0.6
const DEATH_SPARKS := Color(1.0, 0.45, 0.35)
const DEATH_HIT_STOP := 0.06
## health: hits to kill. hover / chase: speeds about the post and after the
## hero. aggro: how close he has to come. drop_chance: how often a kill
## leaves a heal mote. puff: the colour it dies in. loot: what it leaks and
## carries (see Loot).
const PROFILES := {
	"watcher":
	{
		"health": 2,
		"hover": 25.0,
		"chase": 55.0,
		"aggro": 110.0,
		"drop_chance": 0.5,
		"puff": Color(1.0, 0.95, 0.8),
		"loot": {"light": [4, 6], "bone_chance": 0.04, "flask_chance": 0.03, "xp": 4},
	},
	"shade":
	{
		"health": 2,
		"hover": 18.0,
		"chase": 48.0,
		"aggro": 130.0,
		"drop_chance": 0.3,
		"puff": Color(0.6, 0.66, 0.82),
		"loot": {"light": [3, 6], "bone_chance": 0.05, "flask_chance": 0.03, "xp": 4},
	},
	"siren":
	{
		"health": 3,
		"hover": 30.0,
		"chase": 72.0,
		"aggro": 120.0,
		"drop_chance": 0.4,
		"puff": Color(0.8, 0.4, 0.6),
		"loot": {"light": [5, 8], "bone_chance": 0.06, "flask_chance": 0.04, "xp": 6},
	},
}
## The dive: where it waits over the hero, how long it aims, how fast and
## how long it falls, how long it climbs back and the pause between passes.
const DIVE_PERCH := Vector2(0.0, -64.0)
const AIM_TIME := 0.5
const DIVE_SPEED := 190.0
const DIVE_TIME := 0.45
const CLIMB_TIME := 0.8
const DIVE_COOLDOWN := 1.5
const TINT_AIM := Color(1.0, 0.7, 0.7)
const HEALTH_BAR_SCENE := preload("res://scripts/enemy_health_bar.gd")
const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")

@export_enum("watcher", "shade", "siren") var character := "watcher"
@export var contact_damage := 1

## "dive" or "" (see the top of the file). Set by the level.
var behaviour := ""
var state: State = State.HOVER
var health := 0
var max_health := 0
var home := Vector2.ZERO
var health_bar: EnemyHealthBar
## Where a dropped heal mote goes (set by the level); the enemy's own
## parent when unset, so a dying enemy is not counted among the live ones.
var drop_parent: Node
var _time := 0.0
var _hurt_timer := 0.0
var _state_timer := 0.0
var _dive_cooldown := 0.0
var _dead := false
var _player: Player
var _profile: Dictionary = {}

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var body_shape: CollisionShape2D = $CollisionShape2D
@onready var hitbox: Area2D = $Hitbox


func _ready() -> void:
	add_to_group("enemy")
	if not PROFILES.has(character):
		push_error("Flyer: unknown character '%s', using watcher" % character)
		character = "watcher"
	_profile = PROFILES[character]
	AnimLoader.setup(sprite, character)
	sprite.play("idle")
	max_health = int(_profile["health"])
	health = max_health
	home = global_position
	_time = randf() * TAU
	_player = get_tree().get_first_node_in_group("player") as Player
	health_bar = HEALTH_BAR_SCENE.new()
	health_bar.position = Vector2(0.0, -14.0)
	add_child(health_bar)


func _physics_process(delta: float) -> void:
	_time += delta
	_state_timer -= delta
	_dive_cooldown = maxf(_dive_cooldown - delta, 0.0)
	if _hurt_timer > 0.0:
		_hurt_timer -= delta
		velocity = velocity.move_toward(Vector2.ZERO, ACCEL * delta)
		if _hurt_timer <= 0.0:
			sprite.modulate = Color.WHITE
			sprite.play("idle")
	else:
		match state:
			State.HOVER:
				_process_hover(delta)
			State.AIM:
				velocity = velocity.move_toward(Vector2.ZERO, ACCEL * 2.0 * delta)
				if _state_timer <= 0.0:
					_start_dive()
			State.DIVE:
				if _state_timer <= 0.0 or get_slide_collision_count() > 0:
					_start_climb()
			State.CLIMB:
				var wanted := (home - global_position).limit_length(float(_profile["chase"]))
				velocity = velocity.move_toward(wanted, ACCEL * delta)
				if _state_timer <= 0.0:
					state = State.HOVER
	move_and_slide()
	_hurt_player()


## Drifts about its post; near the hero, it chases him (or, diving, keeps
## above him and drops when it gets there).
func _process_hover(delta: float) -> void:
	var target := home + Vector2(sin(_time * 1.6) * 24.0, sin(_time * 2.7) * 6.0)
	var speed := float(_profile["hover"])
	if _hero_near():
		speed = float(_profile["chase"])
		if behaviour == "dive":
			target = _player.global_position + DIVE_PERCH
			if _dive_cooldown <= 0.0 and global_position.distance_to(target) < 12.0:
				_start_aim()
				return
		else:
			target = _player.global_position + Vector2(0.0, -16.0)
	var wanted := (target - global_position).limit_length(speed)
	velocity = velocity.move_toward(wanted, ACCEL * delta)


func _hero_near() -> bool:
	return (
		is_instance_valid(_player)
		and _player.state != Player.State.DEAD
		and global_position.distance_to(_player.global_position) < float(_profile["aggro"])
	)


## The eye turns red and fixes on the hero: the warning.
func _start_aim() -> void:
	state = State.AIM
	_state_timer = AIM_TIME
	sprite.play("attack")
	sprite.modulate = TINT_AIM


## Straight at where the hero stands now.
func _start_dive() -> void:
	state = State.DIVE
	_state_timer = DIVE_TIME
	sprite.modulate = Color.WHITE
	var at := _player.global_position + Vector2(0.0, -8.0) if is_instance_valid(_player) else home
	var dir := (at - global_position).normalized()
	if dir == Vector2.ZERO:
		dir = Vector2.DOWN
	velocity = dir * DIVE_SPEED
	Audio.play("dash", -8.0)


func _start_climb() -> void:
	state = State.CLIMB
	_state_timer = CLIMB_TIME
	_dive_cooldown = DIVE_COOLDOWN
	sprite.play("idle")


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
	health_bar.show_health(health, max_health)
	Audio.play("hit")
	Feedback.flash(sprite, 0.1)
	Feedback.spark(global_position)
	# A hit knocks it out of its aim.
	if state == State.AIM:
		_start_climb()
	_hurt_timer = HURT_TIME
	sprite.modulate = Color(1.0, 0.5, 0.5)
	sprite.play("hurt")
	var away := (global_position - source_position).normalized()
	if away == Vector2.ZERO:
		away = Vector2.UP
	velocity = away * KNOCKBACK
	if health <= 0:
		_die()


## Death: time stops for a blink; the eye shuts, the ring falls apart and
## the wings fold (the death strip) as it sinks in a puff of light, then it
## fades. Sometimes it leaves a heal mote behind.
func _die() -> void:
	_dead = true
	set_physics_process(false)
	Audio.play("enemy_die")
	Feedback.hit_stop(DEATH_HIT_STOP)
	Feedback.shake(0.3)
	Feedback.spark(global_position, DEATH_SPARKS)
	Feedback.puff(global_position, _profile["puff"])
	body_shape.set_deferred("disabled", true)
	hitbox.set_deferred("monitoring", false)
	health_bar.visible = false
	sprite.modulate = Color.WHITE
	var sink := create_tween()
	sink.tween_property(sprite, "position:y", sprite.position.y + DEATH_DROP, 0.5).set_ease(
		Tween.EASE_IN
	)
	EnemyDeath.play(sprite, 1, CORPSE_TIME, queue_free)
	var parent := drop_parent if drop_parent else get_parent()
	Loot.drop(parent, global_position, _profile["loot"])
	if randf() < float(_profile["drop_chance"]) + Inventory.bonus("mote_chance"):
		var mote: HealMote = MOTE_SCENE.instantiate()
		mote.position = global_position
		parent.add_child.call_deferred(mote)
