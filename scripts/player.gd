class_name Player
extends CharacterBody2D

## The hero: run, jump, melee attack, short dash and limited inner healing.
## The node's origin is at the hero's feet. Tuning values live at the top so
## they are easy to tweak while playtesting.

signal health_changed(current: int, max_value: int)
signal heal_charges_changed(current: int, max_value: int)
signal died
signal respawned

enum State { IDLE, RUN, JUMP, FALL, DASH, ATTACK, HEAL, HURT, DEAD }

const CHARACTER := "hero"

# Movement. The hero is meant to feel weighty: he takes a moment to get up
# to speed, slides a little when he stops and keeps his momentum in the air.
const RUN_SPEED := 100.0
const GROUND_ACCEL := 700.0
const GROUND_TURN := 1000.0
const GROUND_FRICTION := 800.0
const AIR_ACCEL := 450.0
const AIR_TURN := 600.0
const AIR_FRICTION := 200.0
const MAX_FALL_SPEED := 280.0

# Jump, set by its shape; gravity and launch speed are worked out from it.
const JUMP_HEIGHT := 72.0
const JUMP_TIME_TO_APEX := 0.45
const JUMP_TIME_TO_LAND := 0.36
const JUMP_VELOCITY := -2.0 * JUMP_HEIGHT / JUMP_TIME_TO_APEX
const GRAVITY := 2.0 * JUMP_HEIGHT / (JUMP_TIME_TO_APEX * JUMP_TIME_TO_APEX)
const FALL_GRAVITY := 2.0 * JUMP_HEIGHT / (JUMP_TIME_TO_LAND * JUMP_TIME_TO_LAND)
const JUMP_CUT_MULTIPLIER := 0.45
## Near the top of a held jump gravity is eased, so the apex lingers a little.
const APEX_SPEED_THRESHOLD := 30.0
const APEX_GRAVITY_MULTIPLIER := 0.6
const COYOTE_TIME := 0.1
## Jump, dash and attack presses are remembered this long.
const INPUT_BUFFER_TIME := 0.12
## How far the hero is nudged sideways around a ceiling corner, or up onto a
## ledge lip during a dash, instead of being stopped.
const CORNER_CORRECTION := 4

# Dash
const DASH_SPEED := 260.0
const DASH_TIME := 0.14
const DASH_COOLDOWN := 0.4
## Speed kept when the dash ends, so it eases out instead of stopping dead.
const DASH_END_SPEED := 160.0

# Attack
const ATTACK_TIME := 0.28
const ATTACK_ACTIVE_FROM := 0.06
const ATTACK_ACTIVE_UNTIL := 0.2
const ATTACK_DAMAGE := 1
const ATTACK_OFFSET_X := 20.0
## Share of run speed the hero can walk at while swinging on the ground.
const ATTACK_MOVE_MULTIPLIER := 0.6

# Health and healing
const MAX_HEALTH := 5
const MAX_HEAL_CHARGES := 3
const HEAL_AMOUNT := 2
const HEAL_TIME := 0.7
const HURT_TIME := 0.25
const INVULNERABLE_TIME := 0.9
const KNOCKBACK := Vector2(110.0, -110.0)
const DEATH_TIME := 1.2
const SAFE_GROUND_TIME := 0.2

const TINT_DASH := Color(0.7, 0.9, 1.0, 0.85)
const TINT_HEAL := Color(1.0, 0.92, 0.6)

var state: State = State.IDLE
var facing := 1
var health := MAX_HEALTH
var heal_charges := MAX_HEAL_CHARGES
## Where the hero comes back after dying (the last checkpoint rested at).
var respawn_position := Vector2.ZERO
## Where the hero is put back after touching spikes.
var last_safe_position := Vector2.ZERO
## Falling below this y counts as death (set by the level).
var death_y := INF
## Set when the level ends: input is ignored and the hero can no longer be
## hurt, but gravity keeps running so he lands.
var frozen := false

var _coyote_timer := 0.0
var _jump_buffer_timer := 0.0
var _dash_buffer_timer := 0.0
var _attack_buffer_timer := 0.0
## One dash per airtime: spent on dashing, refilled on landing.
var _dash_available := true
var _dash_from_floor := false
var _dash_cooldown_timer := 0.0
var _state_timer := 0.0
var _invulnerable_timer := 0.0
var _safe_ground_timer := 0.0
## True from a jump until its rise ends; lets the jump cut apply once even if
## the button was released during another state.
var _jump_rising := false
var _attack_hit: Array[Node] = []

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var attack_hitbox: Area2D = $AttackHitbox
@onready var attack_shape: CollisionShape2D = $AttackHitbox/CollisionShape2D


func _ready() -> void:
	add_to_group("player")
	AnimLoader.setup(sprite, CHARACTER)
	sprite.play("idle")
	respawn_position = global_position
	last_safe_position = global_position
	attack_shape.disabled = true
	health_changed.emit(health, MAX_HEALTH)
	heal_charges_changed.emit(heal_charges, MAX_HEAL_CHARGES)


func _physics_process(delta: float) -> void:
	_tick_timers(delta)
	# Sampled before the state switch so a press near the end of an attack,
	# dash, heal or hurt is buffered instead of dropped.
	if not frozen:
		if Input.is_action_just_pressed("jump"):
			_jump_buffer_timer = INPUT_BUFFER_TIME
		if Input.is_action_just_pressed("dash"):
			_dash_buffer_timer = INPUT_BUFFER_TIME
		if Input.is_action_just_pressed("attack"):
			_attack_buffer_timer = INPUT_BUFFER_TIME
	match state:
		State.DEAD:
			_process_dead(delta)
		State.DASH:
			_process_dash(delta)
		State.ATTACK:
			_process_attack(delta)
		State.HEAL:
			_process_heal(delta)
		State.HURT:
			_process_hurt(delta)
		_:
			_process_normal(delta)
	_correct_corners(delta)
	move_and_slide()
	if is_on_floor() and state != State.DASH:
		_dash_available = true
	_track_safe_ground(delta)
	if global_position.y > death_y and state != State.DEAD and not frozen:
		_die()
	_update_sprite()


func _tick_timers(delta: float) -> void:
	_coyote_timer = maxf(_coyote_timer - delta, 0.0)
	_jump_buffer_timer = maxf(_jump_buffer_timer - delta, 0.0)
	_dash_buffer_timer = maxf(_dash_buffer_timer - delta, 0.0)
	_attack_buffer_timer = maxf(_attack_buffer_timer - delta, 0.0)
	_dash_cooldown_timer = maxf(_dash_cooldown_timer - delta, 0.0)
	_state_timer = maxf(_state_timer - delta, 0.0)
	_invulnerable_timer = maxf(_invulnerable_timer - delta, 0.0)


# --- States -------------------------------------------------------------


func _process_normal(delta: float) -> void:
	var input_dir := 0.0 if frozen else Input.get_axis("move_left", "move_right")
	if input_dir != 0.0:
		facing = 1 if input_dir > 0.0 else -1
	if is_on_floor():
		_coyote_timer = COYOTE_TIME

	_apply_gravity(delta)
	_apply_horizontal(input_dir, delta)

	if _jump_buffer_timer > 0.0 and _coyote_timer > 0.0:
		velocity.y = JUMP_VELOCITY
		_jump_buffer_timer = 0.0
		_coyote_timer = 0.0
		_jump_rising = true
	elif _jump_rising and velocity.y >= 0.0:
		_jump_rising = false
	elif _jump_rising and not frozen and not Input.is_action_pressed("jump"):
		# Variable jump height: letting go while rising cuts the jump short,
		# whichever state the button was released in.
		velocity.y *= JUMP_CUT_MULTIPLIER
		_jump_rising = false

	if frozen:
		state = State.IDLE if is_on_floor() else State.FALL
		return
	if _dash_buffer_timer > 0.0 and _dash_cooldown_timer <= 0.0 and _dash_available:
		_start_dash()
		return
	if _attack_buffer_timer > 0.0:
		_start_attack()
		return
	if (
		Input.is_action_just_pressed("heal")
		and is_on_floor()
		and heal_charges > 0
		and health < MAX_HEALTH
	):
		_start_heal()
		return

	if not is_on_floor():
		state = State.JUMP if velocity.y < 0.0 else State.FALL
	else:
		state = State.RUN if absf(velocity.x) > 5.0 else State.IDLE


func _start_dash() -> void:
	_dash_from_floor = is_on_floor()
	_set_state(State.DASH, DASH_TIME)
	_dash_buffer_timer = 0.0
	_dash_cooldown_timer = DASH_COOLDOWN
	_dash_available = false
	velocity = Vector2(DASH_SPEED * facing, 0.0)


func _process_dash(_delta: float) -> void:
	velocity = Vector2(DASH_SPEED * facing, 0.0)
	if _state_timer <= 0.0:
		velocity.x = DASH_END_SPEED * facing
		# A dash that ran off a ledge still allows the late jump it would
		# have had when walking off.
		if _dash_from_floor:
			_coyote_timer = COYOTE_TIME
		state = State.IDLE


func _start_attack() -> void:
	_set_state(State.ATTACK, ATTACK_TIME)
	_attack_buffer_timer = 0.0
	_attack_hit.clear()
	attack_hitbox.position.x = ATTACK_OFFSET_X * facing
	sprite.play("attack")


func _process_attack(delta: float) -> void:
	# The hero keeps moving while he swings (slower on the ground) but keeps
	# facing the way the swing started.
	var input_dir := Input.get_axis("move_left", "move_right")
	var speed_multiplier := ATTACK_MOVE_MULTIPLIER if is_on_floor() else 1.0
	_apply_gravity(delta)
	_apply_horizontal(input_dir, delta, speed_multiplier)
	var elapsed := ATTACK_TIME - _state_timer
	var active := elapsed >= ATTACK_ACTIVE_FROM and elapsed <= ATTACK_ACTIVE_UNTIL
	if attack_shape.disabled == active:
		attack_shape.disabled = not active
	if active:
		for body in attack_hitbox.get_overlapping_bodies():
			if body in _attack_hit or not body.has_method("take_damage"):
				continue
			_attack_hit.append(body)
			body.take_damage(ATTACK_DAMAGE, global_position)
	if _state_timer <= 0.0:
		_end_attack()
		state = State.IDLE


func _end_attack() -> void:
	attack_shape.disabled = true


func _start_heal() -> void:
	_set_state(State.HEAL, HEAL_TIME)
	velocity.x = 0.0


func _process_heal(delta: float) -> void:
	_apply_gravity(delta)
	velocity.x = 0.0
	if _state_timer <= 0.0:
		heal_charges -= 1
		health = mini(health + HEAL_AMOUNT, MAX_HEALTH)
		health_changed.emit(health, MAX_HEALTH)
		heal_charges_changed.emit(heal_charges, MAX_HEAL_CHARGES)
		state = State.IDLE


func _process_hurt(delta: float) -> void:
	_apply_gravity(delta)
	velocity.x = move_toward(velocity.x, 0.0, GROUND_FRICTION * 0.5 * delta)
	if _state_timer <= 0.0:
		state = State.IDLE


func _process_dead(delta: float) -> void:
	_apply_gravity(delta)
	velocity.x = move_toward(velocity.x, 0.0, GROUND_FRICTION * delta)
	if _state_timer <= 0.0:
		respawn()


func _set_state(new_state: State, duration: float) -> void:
	_end_attack()
	state = new_state
	_state_timer = duration


# --- Physics helpers ----------------------------------------------------


func _apply_gravity(delta: float) -> void:
	if is_on_floor():
		return
	var gravity := FALL_GRAVITY if velocity.y > 0.0 else GRAVITY
	var airborne := state == State.JUMP or state == State.FALL
	if (
		airborne
		and not frozen
		and absf(velocity.y) < APEX_SPEED_THRESHOLD
		and Input.is_action_pressed("jump")
	):
		gravity *= APEX_GRAVITY_MULTIPLIER
	velocity.y = minf(velocity.y + gravity * delta, MAX_FALL_SPEED)


func _apply_horizontal(input_dir: float, delta: float, speed_multiplier := 1.0) -> void:
	var on_floor := is_on_floor()
	if input_dir != 0.0:
		var turning := signf(velocity.x) == -signf(input_dir)
		var accel: float
		if turning:
			accel = GROUND_TURN if on_floor else AIR_TURN
		else:
			accel = GROUND_ACCEL if on_floor else AIR_ACCEL
		var top_speed := input_dir * RUN_SPEED * speed_multiplier
		velocity.x = move_toward(velocity.x, top_speed, accel * delta)
	else:
		var friction := GROUND_FRICTION if on_floor else AIR_FRICTION
		velocity.x = move_toward(velocity.x, 0.0, friction * delta)


## Nudges the hero around corners he only just clips: sideways past a
## ceiling edge while rising, or up onto a ledge lip while dashing.
func _correct_corners(delta: float) -> void:
	var step := velocity * delta
	if velocity.y < 0.0 and test_move(global_transform, Vector2(0.0, step.y)):
		for offset in range(1, CORNER_CORRECTION + 1):
			for dir: int in [-1, 1]:
				var shift := Vector2(offset * dir, 0.0)
				if (
					not test_move(global_transform, shift)
					and not test_move(global_transform.translated(shift), Vector2(0.0, step.y))
				):
					global_position += shift
					return
	if state == State.DASH and test_move(global_transform, Vector2(step.x, 0.0)):
		for offset in range(1, CORNER_CORRECTION + 1):
			var lift := Vector2(0.0, -offset)
			if (
				not test_move(global_transform, lift)
				and not test_move(global_transform.translated(lift), Vector2(step.x, 0.0))
			):
				global_position += lift
				return


func _track_safe_ground(delta: float) -> void:
	if is_on_floor() and (state == State.IDLE or state == State.RUN):
		_safe_ground_timer += delta
		if _safe_ground_timer >= SAFE_GROUND_TIME:
			last_safe_position = global_position
	else:
		_safe_ground_timer = 0.0


# --- Damage, death, checkpoints ----------------------------------------


## True while nothing can hurt the hero: dead, dashing, blinking after a
## hit, or standing at the gate after finishing the level.
func _can_be_hurt() -> bool:
	return not frozen and state != State.DEAD and state != State.DASH and _invulnerable_timer <= 0.0


## Takes health away and dies at zero. Returns true when the hero survived.
func _lose_health(amount: int) -> bool:
	health = maxi(health - amount, 0)
	health_changed.emit(health, MAX_HEALTH)
	if health == 0:
		_die()
		return false
	return true


## Called by enemies: knockback away from the attacker.
func take_damage(amount: int, source_position: Vector2) -> void:
	if not _can_be_hurt() or not _lose_health(amount):
		return
	var dir := 1.0 if global_position.x >= source_position.x else -1.0
	_set_state(State.HURT, HURT_TIME)
	velocity = Vector2(KNOCKBACK.x * dir, KNOCKBACK.y)
	_invulnerable_timer = INVULNERABLE_TIME


## Called by spikes: put back on the last safe ground.
func take_hazard_damage(amount: int) -> void:
	if not _can_be_hurt() or not _lose_health(amount):
		return
	_set_state(State.HURT, HURT_TIME)
	global_position = last_safe_position
	velocity = Vector2.ZERO
	_invulnerable_timer = INVULNERABLE_TIME


## Ends the level: whatever the hero was doing stops, input is ignored and
## nothing can hurt him any more. Gravity keeps running so he lands. A dead
## hero is left to respawn as usual; the gate is still there afterwards.
func freeze() -> void:
	if state == State.DEAD:
		return
	frozen = true
	_set_state(State.IDLE, 0.0)
	velocity.x = 0.0


## Resting at a checkpoint: full health, healing refilled, respawn point set.
func rest_at(checkpoint_position: Vector2) -> void:
	respawn_position = checkpoint_position
	last_safe_position = checkpoint_position
	health = MAX_HEALTH
	heal_charges = MAX_HEAL_CHARGES
	health_changed.emit(health, MAX_HEALTH)
	heal_charges_changed.emit(heal_charges, MAX_HEAL_CHARGES)


func _die() -> void:
	_set_state(State.DEAD, DEATH_TIME)
	velocity = Vector2(0.0, -90.0)
	died.emit()


## The angels' system resurrects the hero at the last checkpoint.
func respawn() -> void:
	global_position = respawn_position
	last_safe_position = respawn_position
	velocity = Vector2.ZERO
	# Nothing pressed before the death carries over.
	_jump_rising = false
	_jump_buffer_timer = 0.0
	_dash_buffer_timer = 0.0
	_attack_buffer_timer = 0.0
	_coyote_timer = 0.0
	_dash_cooldown_timer = 0.0
	_dash_available = true
	health = MAX_HEALTH
	heal_charges = MAX_HEAL_CHARGES
	health_changed.emit(health, MAX_HEALTH)
	heal_charges_changed.emit(heal_charges, MAX_HEAL_CHARGES)
	_invulnerable_timer = INVULNERABLE_TIME
	state = State.IDLE
	respawned.emit()


# --- Visuals ------------------------------------------------------------


func _update_sprite() -> void:
	sprite.flip_h = facing < 0
	sprite.modulate = Color.WHITE
	sprite.rotation = 0.0
	match state:
		State.IDLE:
			_play("idle")
		State.RUN:
			_play("run")
		State.JUMP:
			_pose("jump", 0)
		State.FALL:
			_pose("jump", 1)
		State.ATTACK:
			pass  # started once in _start_attack
		State.DASH:
			_pose("run", 1)
			sprite.modulate = TINT_DASH
		State.HURT:
			_pose("hurt", 0)
		State.HEAL:
			_play("idle")
			sprite.modulate = TINT_HEAL
		State.DEAD:
			_pose("hurt", 0)
			sprite.rotation = -PI / 2.0 * facing
	# Blink while invulnerable.
	if _invulnerable_timer > 0.0 and state != State.DEAD:
		sprite.visible = fmod(_invulnerable_timer, 0.12) < 0.07
	else:
		sprite.visible = true


## Switches to a looping animation, or restarts it if it was paused by _pose.
## One-shot animations (attack) are started where the state begins, so a
## finished strip is never replayed while the state lasts.
func _play(anim_name: String) -> void:
	if sprite.animation != anim_name:
		sprite.play(anim_name)
	elif not sprite.is_playing() and sprite.sprite_frames.get_animation_loop(anim_name):
		sprite.play(anim_name)


func _pose(anim_name: String, frame: int) -> void:
	if sprite.animation != anim_name:
		sprite.animation = anim_name
	sprite.pause()
	sprite.frame = frame
