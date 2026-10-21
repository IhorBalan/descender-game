class_name Walker
extends CharacterBody2D

## A ground enemy that patrols a platform, turning at edges and walls, and
## hurts the hero on contact. When the hero stands close in front of it, it
## stops and swings: a visible wind-up, a short strike with a hitbox in
## front, then a recovery it cannot act in. A hit during the wind-up or the
## strike cancels the attack, so stepping in first is rewarded.
##
## `behaviour` (set by the level from the placement) adds one habit on top:
##
## - chase: walks at the hero once he is near and about level, stopping at
##   the end of its ledge, and goes back to patrolling when he leaves.
## - ambush: stands still, as if idle, until the hero is close, then
##   springs with a quick swing; a mark over its head is the only warning.
##   Awake, it chases.
## - pounce: crouches and leaps at a hero a few tiles away, claws out.
## - hurl: keeps its distance and throws a bolt of hellfire when the hero
##   is in sight, backing away when he comes close.
## - slam: strikes the ground when the hero is at mid range, sending a
##   shockwave along the floor that has to be jumped.
##
## The node's origin is at the feet. `character` picks the sprite strips and
## the size/speed/attack profile below.

enum State { PATROL, CHASE, LURK, WINDUP, STRIKE, LEAP, RECOVER }
## What a wind-up is for.
enum Attack { SWING, POUNCE, HURL, SLAM }

const GRAVITY := 700.0
const MAX_FALL_SPEED := 260.0
const HURT_TIME := 0.2
const KNOCKBACK := Vector2(110.0, -70.0)
## The hero has to be roughly at the same height to be attacked.
const ATTACK_HEIGHT_TOLERANCE := 24.0
const TINT_WINDUP := Color(1.0, 0.75, 0.55)
const TINT_HURT := Color(1.0, 0.5, 0.5)
## How long the body lies there before it fades.
const CORPSE_TIME := 1.0
const DEATH_SPARKS := Color(1.0, 0.45, 0.35)
const DEATH_HIT_STOP := 0.06
const HEALTH_BAR_SCENE := preload("res://scripts/enemy_health_bar.gd")
const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")
const SHOT_SCRIPT := preload("res://scripts/enemies/enemy_shot.gd")

# Chasing
## How close (and how level) the hero has to be for a chaser to come, and
## how much further he can get before it gives up.
const CHASE_RANGE := 120.0
const CHASE_HEIGHT_TOLERANCE := 40.0
const CHASE_GIVE_UP := 1.4
const CHASE_SPEED := 1.8

# Ambush
const AMBUSH_RANGE := 40.0
## The wind-up of the ambush swing, as a share of the usual one.
const AMBUSH_HASTE := 0.5
const MARK_TIME := 0.6
const MARK_COLOR := Color(1.0, 0.35, 0.3)

# Pounce
const POUNCE_MIN := 36.0
const POUNCE_MAX := 96.0
const POUNCE_WINDUP := 0.4
const POUNCE_VELOCITY := Vector2(150.0, -190.0)
const POUNCE_TIME_MAX := 1.0
const POUNCE_RECOVER := 0.4
const POUNCE_COOLDOWN := 1.6
const CROUCH_SCALE := Vector2(1.2, 0.8)

# Hurl
const HURL_RANGE := 150.0
const HURL_HEIGHT_TOLERANCE := 48.0
## Closer than this, a hurler backs away instead of throwing.
const HURL_RETREAT := 48.0
const HURL_WINDUP := 0.5
const HURL_RECOVER := 0.5
const HURL_COOLDOWN := 1.8
const HURL_HAND := Vector2(10.0, -14.0)

# Slam
const SLAM_MAX := 110.0
const SLAM_WINDUP := 0.7
const SLAM_RECOVER := 0.9
const SLAM_COOLDOWN := 2.5
const TINT_SLAM := Color(1.0, 0.6, 0.45)

## body: collision size. speed: patrol speed. health: hits to kill.
## walk: the looping animation name. faces_left: whether the strips face left.
## attack_range: how close (horizontally) the hero must be to trigger a swing.
## reach: the strike hitbox size, placed in front of the body.
## windup / strike / recover: the three phases of a swing, in seconds.
## cooldown: pause before the next swing can start. attack_damage: hearts.
## drop_chance: how often a kill leaves a heal mote behind. puff: the colour
## of the dust (or light) it dies in. armour: hits never cancel its attack
## or push it back while it attacks (and push it half as far otherwise).
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
		"puff": Color(0.55, 0.15, 0.15),
		"armour": false,
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
		"puff": Color(1.0, 0.95, 0.75),
		"armour": false,
	},
	"brute":
	{
		"body": Vector2(36, 62),
		"speed": 14.0,
		"health": 8,
		"walk": "walk",
		"faces_left": true,
		"attack_range": 52.0,
		"reach": Vector2(46, 48),
		"windup": 0.75,
		"strike": 0.25,
		"recover": 0.9,
		"cooldown": 1.5,
		"attack_damage": 2,
		"drop_chance": 1.0,
		"puff": Color(0.45, 0.12, 0.12),
		"armour": true,
	},
}
## The habits a placement may ask for (see the top of the file).
const BEHAVIOURS: Array[String] = ["chase", "ambush", "pounce", "hurl", "slam"]
## Lines an enemy may shout as it winds up, by bark set (see `barks`).
const BARKS := {
	"fallen":
	[
		"Up! He said up!",
		"The Lowest talks to nobody.",
		"Half-angel! He said you'd come!",
		"He says a name. To nobody.",
	],
	"brute": ["Hrr.", "Down. Stay down."],
}
const BARK_CHANCE := 0.5
const BARK_TIME := 1.3
const BARK_COLOR := Color(1.0, 0.85, 0.8)

@export_enum("imp", "angel_guard", "brute") var character := "imp"
@export var contact_damage := 1
## One of BEHAVIOURS, or "" for plain patrolling. Set by the level from the
## placement.
var behaviour := ""
## A key of BARKS: the enemy shouts one of those lines when it winds up,
## half the time. Set by the level from the placement; "" is silent.
var barks := ""
## A resting demon of the last camp: patrols, never swings and does no
## contact damage. Still dies like the others.
var passive := false
## Falling below this y (set by the level) removes the enemy, so one knocked
## off its platform does not fall forever.
var death_y := INF

var state: State = State.PATROL
var health := 0
var direction := -1
var speed := 30.0
var health_bar: EnemyHealthBar
## Where a dropped heal mote (and a thrown shot) goes (set by the level);
## the enemy's own parent when unset, so a dying enemy is not counted
## among the live ones.
var drop_parent: Node
var _profile: Dictionary = {}
var _walk_anim := "run"
var _half_width := 0.0
var _faces_left := true
var _hurt_timer := 0.0
var _state_timer := 0.0
var _attack_cooldown := 0.0
## The pounce, hurl and slam have a cooldown of their own, so a hurler
## can still swing between throws.
var _special_cooldown := 0.0
var _attack: Attack = Attack.SWING
## An ambusher that has sprung once chases from then on.
var _awake := false
var _dead := false
var _player: Player
var _mark_label: Label
var _mark_timer := 0.0
var _crouch_tween: Tween

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
	if not behaviour.is_empty() and behaviour not in BEHAVIOURS:
		push_warning("Walker: no behaviour called '%s'" % behaviour)
		behaviour = ""
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
	if behaviour == "ambush" and not passive:
		state = State.LURK
		sprite.play("idle")


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL_SPEED)
	_attack_cooldown = maxf(_attack_cooldown - delta, 0.0)
	_special_cooldown = maxf(_special_cooldown - delta, 0.0)
	if _mark_timer > 0.0:
		_mark_timer -= delta
		if _mark_timer <= 0.0 and _mark_label:
			_mark_label.visible = false
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
			State.CHASE:
				_process_chase()
			State.LURK:
				_process_lurk()
			State.LEAP:
				_process_leap()
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
	if _try_attacks():
		return
	if _chasing() and _hero_within(CHASE_RANGE, CHASE_HEIGHT_TOLERANCE):
		state = State.CHASE
		return
	if behaviour == "hurl" and _hero_within(HURL_RETREAT, HURL_HEIGHT_TOLERANCE):
		# Too close to throw: back off, but not over the edge.
		_face(-1 if _player.global_position.x >= global_position.x else 1)
		velocity.x = direction * speed if _ledge_ahead() else 0.0
		return
	if is_on_floor() and not floor_ray.is_colliding():
		_face(-direction)
	velocity.x = direction * speed


## Walks at the hero, stopping at the end of the ledge, until he is gone.
func _process_chase() -> void:
	if _try_attacks():
		return
	if not _hero_within(CHASE_RANGE * CHASE_GIVE_UP, CHASE_HEIGHT_TOLERANCE):
		state = State.PATROL
		return
	_face(1 if _player.global_position.x >= global_position.x else -1)
	velocity.x = direction * speed * CHASE_SPEED if _ledge_ahead() else 0.0


## Waits as if idle; the hero coming close springs the swing.
func _process_lurk() -> void:
	velocity.x = 0.0
	if _attack_cooldown > 0.0 or not _hero_within(AMBUSH_RANGE, ATTACK_HEIGHT_TOLERANCE):
		return
	_awake = true
	_show_mark("!", MARK_COLOR, MARK_TIME)
	_start_windup(Attack.SWING, float(_profile["windup"]) * AMBUSH_HASTE)


## Starts whichever attack the hero's distance calls for. True if one did.
func _try_attacks() -> bool:
	if passive or not _find_player():
		return false
	if _attack_cooldown <= 0.0 and _hero_in_reach():
		_start_windup(Attack.SWING, float(_profile["windup"]))
		return true
	if _special_cooldown > 0.0 or not _special_wanted():
		return false
	match behaviour:
		"pounce":
			_start_windup(Attack.POUNCE, POUNCE_WINDUP)
		"hurl":
			_start_windup(Attack.HURL, HURL_WINDUP)
		"slam":
			_start_windup(Attack.SLAM, SLAM_WINDUP)
	return true


## Whether the hero stands where the behaviour's own attack wants him.
func _special_wanted() -> bool:
	var offset := _player.global_position - global_position
	var dx := absf(offset.x)
	match behaviour:
		"pounce":
			# A hero a few tiles away, level or a little above.
			return dx >= POUNCE_MIN and dx <= POUNCE_MAX and offset.y > -48.0 and offset.y <= 8.0
		"hurl":
			return (
				dx > HURL_RETREAT and dx <= HURL_RANGE and absf(offset.y) <= HURL_HEIGHT_TOLERANCE
			)
		"slam":
			var too_close := dx <= float(_profile["attack_range"])
			return not too_close and dx <= SLAM_MAX and absf(offset.y) <= ATTACK_HEIGHT_TOLERANCE
	return false


func _chasing() -> bool:
	return behaviour == "chase" or (behaviour == "ambush" and _awake)


## True when the hero is alive and in this box around the enemy.
func _hero_within(range_x: float, range_y: float) -> bool:
	if passive or not _find_player():
		return false
	var offset := _player.global_position - global_position
	return absf(offset.x) <= range_x and absf(offset.y) <= range_y


## True when the hero is alive, close in front or behind, and about level.
func _hero_in_reach() -> bool:
	return _hero_within(float(_profile["attack_range"]), ATTACK_HEIGHT_TOLERANCE)


func _find_player() -> bool:
	if not is_instance_valid(_player):
		_player = get_tree().get_first_node_in_group("player") as Player
	return _player != null and _player.state != Player.State.DEAD


## Whether there is floor in front of the feet to walk on.
func _ledge_ahead() -> bool:
	return not is_on_floor() or floor_ray.is_colliding()


func _start_windup(attack: Attack, duration: float) -> void:
	# Turn to the hero and plant the feet; the attack telegraphs itself.
	_face(1 if _player.global_position.x >= global_position.x else -1)
	velocity.x = 0.0
	_attack = attack
	state = State.WINDUP
	_state_timer = duration
	sprite.animation = "attack"
	sprite.pause()
	sprite.frame = 0
	match attack:
		Attack.SWING:
			sprite.modulate = TINT_WINDUP
			_maybe_bark()
		Attack.POUNCE:
			# A crouch: the body squashes down before the spring.
			sprite.modulate = TINT_WINDUP
			_crouch_tween = create_tween()
			_crouch_tween.tween_property(sprite, "scale", CROUCH_SCALE, duration).set_ease(
				Tween.EASE_OUT
			)
		Attack.HURL:
			sprite.modulate = TINT_WINDUP
			Feedback.spark(_hand_position(), SHOT_SCRIPT.BOLT_COLOR)
		Attack.SLAM:
			# The arms go up (the strip's raised frame) and stay there.
			sprite.frame = 1
			sprite.modulate = TINT_SLAM
			_maybe_bark()


## A line over the head while the swing winds up, for enemies that talk.
func _maybe_bark() -> void:
	if not BARKS.has(barks) or randf() >= BARK_CHANCE:
		return
	var lines: Array = BARKS[barks]
	_show_mark(str(lines[randi() % lines.size()]), BARK_COLOR, BARK_TIME)


## Text over the head for a while: a bark, or the ambush's mark.
func _show_mark(text: String, color: Color, duration: float) -> void:
	if _mark_label == null:
		_mark_label = Label.new()
		_mark_label.add_theme_font_size_override("font_size", 8)
		_mark_label.add_theme_color_override("font_shadow_color", Color(0.0, 0.0, 0.0, 0.8))
		_mark_label.add_theme_constant_override("shadow_offset_x", 1)
		_mark_label.add_theme_constant_override("shadow_offset_y", 1)
		_mark_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_mark_label.size = Vector2(120, 10)
		_mark_label.position = Vector2(-60.0, body_shape.position.y * 2.0 - 14.0)
		add_child(_mark_label)
	_mark_label.add_theme_color_override("font_color", color)
	_mark_label.text = text
	_mark_label.visible = true
	_mark_timer = duration


func bark_showing() -> bool:
	return _mark_label != null and _mark_label.visible


## The text over the head right now, or "".
func mark_text() -> String:
	return _mark_label.text if bark_showing() else ""


func _process_attack(delta: float) -> void:
	velocity.x = 0.0
	_state_timer -= delta
	if state == State.STRIKE:
		_strike_player()
	if _state_timer > 0.0:
		return
	match state:
		State.WINDUP:
			_release()
		State.STRIKE:
			state = State.RECOVER
			_state_timer = _profile["recover"]
			attack_shape.disabled = true
			sprite.play("idle")
		State.RECOVER:
			_end_attack()


## The wind-up is over: the attack happens.
func _release() -> void:
	sprite.modulate = Color.WHITE
	sprite.play("attack")
	match _attack:
		Attack.SWING:
			state = State.STRIKE
			_state_timer = _profile["strike"]
			attack_shape.disabled = false
		Attack.POUNCE:
			_unsquash()
			state = State.LEAP
			_state_timer = POUNCE_TIME_MAX
			velocity = Vector2(POUNCE_VELOCITY.x * direction, POUNCE_VELOCITY.y)
			attack_shape.disabled = false
			Audio.play("jump", -6.0)
		Attack.HURL:
			_throw()
			state = State.RECOVER
			_state_timer = HURL_RECOVER
		Attack.SLAM:
			_slam()
			state = State.RECOVER
			_state_timer = SLAM_RECOVER


## Airborne with the claws out; landing ends it.
func _process_leap() -> void:
	_state_timer -= get_physics_process_delta_time()
	_strike_player()
	if (is_on_floor() and _state_timer < POUNCE_TIME_MAX - 0.1) or _state_timer <= 0.0:
		velocity.x = 0.0
		attack_shape.disabled = true
		state = State.RECOVER
		_state_timer = POUNCE_RECOVER
		sprite.play("idle")


func _strike_player() -> void:
	for body in attack_hitbox.get_overlapping_bodies():
		if body is Player:
			body.take_damage(_profile["attack_damage"], global_position)


func _throw() -> void:
	var shot: EnemyShot = SHOT_SCRIPT.new()
	shot.kind = EnemyShot.Kind.BOLT
	shot.position = _hand_position()
	var offset := _player.global_position + Vector2(0.0, -12.0) - shot.position
	shot.direction = offset.normalized() if offset != Vector2.ZERO else Vector2(direction, 0.0)
	_spawn_parent().add_child(shot)
	Audio.play("swing", -4.0)


## The ground shakes and a wave runs along the floor the way it faces.
func _slam() -> void:
	var shot: EnemyShot = SHOT_SCRIPT.new()
	shot.kind = EnemyShot.Kind.WAVE
	shot.direction = Vector2(direction, 0.0)
	shot.position = global_position + Vector2(direction * (_half_width + 6.0), 0.0)
	_spawn_parent().add_child(shot)
	Feedback.shake(0.35)
	Audio.play("land", -2.0)
	for i in 3:
		Feedback.spark(
			global_position + Vector2(direction * (_half_width + i * 8.0), -2.0),
			SHOT_SCRIPT.WAVE_DUST
		)


func _hand_position() -> Vector2:
	return global_position + Vector2(HURL_HAND.x * direction, HURL_HAND.y)


func _spawn_parent() -> Node:
	return drop_parent if drop_parent else get_parent()


func _unsquash() -> void:
	if _crouch_tween and _crouch_tween.is_valid():
		_crouch_tween.kill()
	sprite.scale = Vector2.ONE


## Back to patrolling (or lurking), with the cooldowns running.
func _end_attack() -> void:
	state = State.LURK if behaviour == "ambush" and not _awake else State.PATROL
	attack_shape.disabled = true
	_unsquash()
	match _attack:
		Attack.SWING:
			_attack_cooldown = _profile["cooldown"]
		Attack.POUNCE:
			_special_cooldown = POUNCE_COOLDOWN
		Attack.HURL:
			_special_cooldown = HURL_COOLDOWN
		Attack.SLAM:
			_special_cooldown = SLAM_COOLDOWN
	sprite.modulate = Color.WHITE
	sprite.play("idle" if state == State.LURK else _walk_anim)


func _face(dir: int) -> void:
	direction = dir
	sprite.flip_h = (dir > 0) == _faces_left
	floor_ray.position.x = (_half_width + 2.0) * dir
	floor_ray.force_raycast_update()
	var reach: Vector2 = _profile["reach"]
	attack_shape.position = Vector2((_half_width + reach.x / 2.0) * dir, -reach.y / 2.0)


func _hurt_player() -> void:
	if passive or not hitbox.has_overlapping_bodies():
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
	# A hit wakes an ambusher.
	_awake = true
	if health <= 0:
		_die()
		return
	var attacking := state != State.PATROL and state != State.CHASE and state != State.LURK
	if attacking and bool(_profile["armour"]):
		# Armour: the blow lands, the attack goes on.
		return
	# Getting hit mid-attack cancels it (and starts the cooldown).
	if attacking:
		_end_attack()
	_hurt_timer = HURT_TIME
	sprite.modulate = TINT_HURT
	sprite.play("hurt")
	var dir := 1.0 if global_position.x >= source_position.x else -1.0
	var push := 0.5 if bool(_profile["armour"]) else 1.0
	velocity = Vector2(KNOCKBACK.x * dir * push, KNOCKBACK.y * push)


## Death: time stops for a blink, the body plays its death strip (and
## lies there a moment before it fades) in a puff of dust, with a burst of
## sparks, and sometimes leaves a heal mote behind.
func _die() -> void:
	_dead = true
	set_physics_process(false)
	Audio.play("enemy_die")
	Feedback.hit_stop(DEATH_HIT_STOP)
	var body: Vector2 = _profile["body"]
	# A heavier body shakes the ground more.
	Feedback.shake(0.25 + body.y / 250.0)
	var centre := global_position + Vector2(0.0, body_shape.position.y)
	Feedback.spark(centre, DEATH_SPARKS)
	Feedback.puff(global_position + Vector2(0.0, -body.y / 4.0), _profile["puff"])
	body_shape.set_deferred("disabled", true)
	hitbox.set_deferred("monitoring", false)
	attack_hitbox.set_deferred("monitoring", false)
	health_bar.visible = false
	if _mark_label:
		_mark_label.visible = false
	_unsquash()
	sprite.modulate = Color.WHITE
	# The body falls away from the hero, who is on the side it faces.
	EnemyDeath.play(sprite, -direction, CORPSE_TIME, queue_free)
	_maybe_drop_mote()


func _maybe_drop_mote() -> void:
	if randf() >= float(_profile["drop_chance"]):
		return
	var mote: HealMote = MOTE_SCENE.instantiate()
	mote.position = global_position + Vector2(0.0, body_shape.position.y)
	var parent := drop_parent if drop_parent else get_parent()
	parent.add_child.call_deferred(mote)
