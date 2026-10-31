class_name Player
extends CharacterBody2D

## The hero: run, jump, melee attack, short dash and limited inner healing,
## plus the angel abilities he unlocks along the way (see Ability).
## The node's origin is at the hero's feet. Tuning values live at the top so
## they are easy to tweak while playtesting.

signal health_changed(current: int, max_value: int)
signal heal_charges_changed(current: int, max_value: int)
signal ability_unlocked(ability: Ability)
signal upgrade_collected(kind: UpgradeKind)
signal power_changed
## A power-up was taken (placed or dropped by an enemy).
signal power_granted(power: Power)
## The purse changed: light or tally bones picked up or spent.
signal purse_changed
## An item was picked up off the ground (see LootDrop).
signal loot_collected(item_id: String)
## Experience came in (see Skills); the HUD follows it.
signal xp_changed
## The experience made a level: `level` is the new one.
signal leveled_up(level: int)
signal died
signal respawned

enum State { IDLE, RUN, JUMP, FALL, DASH, ATTACK, HEAL, HURT, DEAD, CAST, SLAM }
## The half-finished angel powers, in the map tool's order (the export's
## ability list indexes this enum). Each is unlocked by a pickup in the
## world and stays unlocked for the rest of the run, including after death.
enum Ability { WINGS, LIGHT_DASH, WALL_GRIP, HOLY_LIGHT, GLIDE, SMITE }
## Permanent upgrades picked up in the world (see Upgrade). Each one is a
## story flag, so it stays collected in the save.
enum UpgradeKind { HEART, VIAL, IRON }
## Timed power-ups picked up in a room (see PowerUp). Lost on death and at
## the end of the level.
enum Power { HALO, BLADE, ZEAL }

const CHARACTER := "hero"
## The physics layer the one-way ledges sit on (see LevelBuilder).
const LEDGE_LAYER_BIT := 16

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
## Pressing down and jump on a one-way ledge drops through it: the ledges
## are left out of the collision mask for this long.
const DROP_TIME := 0.25
## Wings: one extra jump in the air, a little lower than the first.
const DOUBLE_JUMP_HEIGHT := 56.0
const DOUBLE_JUMP_VELOCITY := -sqrt(2.0 * GRAVITY * DOUBLE_JUMP_HEIGHT)
const WING_FLASH_TIME := 0.15
# Wall Grip: pressing into a wall in the air holds the hero to it, sliding
# down slowly; jump then springs him away from it.
const WALL_SLIDE_SPEED := 40.0
const WALL_JUMP_VELOCITY := Vector2(150.0, -2.0 * JUMP_HEIGHT * 0.9 / JUMP_TIME_TO_APEX)
## Steering is locked away from the wall this long after a wall jump, so
## holding towards the wall does not climb it by rapid jumps.
const WALL_JUMP_LOCK := 0.16
# Glide: pressing jump again while falling (with the wings' jump spent)
# and holding it slows the fall to this.
const GLIDE_FALL_SPEED := 45.0
const GLIDE_GRAVITY_MULTIPLIER := 0.35
## Jump, dash and attack presses are remembered this long.
const INPUT_BUFFER_TIME := 0.12
## How far the hero is nudged sideways around a ceiling corner, or up onto a
## ledge lip during a dash or a walk, instead of being stopped. Walking it is
## a step up: a lip this low (painted ground is never perfectly flat) is
## climbed, and floor_snap_length (4) puts him back on the ground after it.
const CORNER_CORRECTION := 4

# Dash
const DASH_SPEED := 260.0
const DASH_TIME := 0.14
## Light dash: the same speed for longer, and it passes through light
## barriers (see LightBarrier).
const LIGHT_DASH_TIME := 0.22
const DASH_COOLDOWN := 0.4
## Zeal: the dash comes back sooner and the hero runs faster while it lasts.
const ZEAL_COOLDOWN_MULTIPLIER := 0.375
const ZEAL_SPEED_MULTIPLIER := 1.25
## Speed kept when the dash ends, so it eases out instead of stopping dead.
const DASH_END_SPEED := 160.0

# Holy Light: a bolt from the hand (see LightBolt), on the cast action.
const CAST_TIME := 0.22
const CAST_COOLDOWN := 0.45
const CAST_OFFSET := Vector2(14.0, -18.0)
# Smite: down and attack in the air slams the hero to the ground, through
# ledges, and the landing hurts everything around his feet.
const SLAM_SPEED := 330.0
const SLAM_DAMAGE := 2
const SLAM_REACH := Vector2(84.0, 28.0)
const SLAM_HIT_TIME := 0.12
## The hero cannot act for this long after the landing.
const SLAM_RECOVER_TIME := 0.18
# Attack
const ATTACK_TIME := 0.28
## The swing frames of the attack strip (6 frames at 21 fps): the sword
## passes through the arc from frame 2 to frame 4.
const ATTACK_ACTIVE_FROM := 0.1
const ATTACK_ACTIVE_UNTIL := 0.24
const ATTACK_DAMAGE := 1
## Lit Blade: extra damage while the power-up lasts.
const BLADE_BONUS_DAMAGE := 1
const ATTACK_OFFSET_X := 20.0
const ATTACK_OFFSET_Y := -18.0
const ATTACK_SHAPE_SIZE := Vector2(30.0, 28.0)
## Share of run speed the hero can walk at while swinging on the ground.
const ATTACK_MOVE_MULTIPLIER := 0.6

# Health and healing. These are what a new game starts with; Kept Light
# and Vial pickups raise max_health and max_heal_charges from there.
const MAX_HEALTH := 5
const MAX_HEAL_CHARGES := 3
## How long a timed power-up lasts.
const POWER_TIME := 20.0
const HEAL_AMOUNT := 2
const HEAL_TIME := 0.7
const HURT_TIME := 0.25
const INVULNERABLE_TIME := 0.9
const KNOCKBACK := Vector2(110.0, -110.0)
const DEATH_TIME := 1.2
## Falling at least this fast makes a landing sound and raises dust.
const LAND_SOUND_SPEED := 120.0
# Animation. The strips are in godot_assets/sprites/hero (tools/hero_placeholder):
# every frame's body fills the collision box, so the poses below only pick
# which frame shows for the movement under way.
## Falling at least this fast shows the landing squash, for this long.
const LAND_ANIM_SPEED := 60.0
const LAND_ANIM_TIME := 0.14
## The wing beat of the second jump.
const DOUBLE_JUMP_ANIM_TIME := 0.2
## Running at least this fast against the stick shows the skid frame.
const SKID_SPEED := 40.0
## The run strip plays faster or slower with the hero's speed, within these.
const RUN_ANIM_SPEED_RANGE := Vector2(0.6, 1.6)
## The launch frame of the jump lasts while the hero rises faster than this.
const JUMP_LAUNCH_SPEED := 200.0
const SAFE_GROUND_TIME := 0.2

const TINT_DASH := Color(0.7, 0.9, 1.0, 0.85)
const TINT_LIGHT_DASH := Color(1.0, 0.95, 0.7, 0.85)
const TINT_HEAL := Color(1.0, 0.92, 0.6)
const TINT_WINGS := Color(1.0, 1.0, 0.8)
const TINT_GLIDE := Color(0.9, 0.95, 1.0)
const TINT_CAST := Color(1.0, 0.97, 0.75)
const TINT_SLAM := Color(1.0, 0.8, 0.6)
const LIGHT_BOLT_SCENE := preload("res://scripts/light_bolt.gd")
const TINT_BLADE := Color(1.0, 0.85, 0.5)

var state: State = State.IDLE
var facing := 1
var max_health := MAX_HEALTH
var max_heal_charges := MAX_HEAL_CHARGES
var health := MAX_HEALTH
var heal_charges := MAX_HEAL_CHARGES
var abilities: Array[Ability] = []
## Active power-ups: Power -> seconds left (INF for the Halo, which lasts
## until it is spent).
var powers: Dictionary = {}
## Where the hero comes back after dying (the last checkpoint rested at).
var respawn_position := Vector2.ZERO
## Where the hero is put back after touching spikes.
var last_safe_position := Vector2.ZERO
## Falling below this y counts as death (set by the level).
var death_y := INF
## Set when the level ends: input is ignored and the hero can no longer be
## hurt, but gravity keeps running so he lands.
var frozen := false
## Set by main.gd while a dialogue box is open: input is ignored and nothing
## can hurt the hero, but he is released again when the box closes.
var talking := false

var _coyote_timer := 0.0
var _jump_buffer_timer := 0.0
var _dash_buffer_timer := 0.0
var _attack_buffer_timer := 0.0
## One dash per airtime: spent on dashing, refilled on landing.
var _dash_available := true
var _dash_from_floor := false
## The wings' extra jump: spent in the air, refilled on landing.
var _air_jump_available := false
var _wing_flash_timer := 0.0
var _dash_cooldown_timer := 0.0
## Counts down while the hero is dropping through a ledge.
var _drop_timer := 0.0
## True while the hero holds a wall (see Wall Grip).
var _on_wall_grip := false
## Which way the wall is while gripping, for the jump away from it.
var _wall_dir := 0
var _wall_jump_lock_timer := 0.0
var _cast_buffer_timer := 0.0
var _cast_cooldown_timer := 0.0
## True once the slam has landed and the recovery runs.
var _slam_landed := false
var _slam_hit: Array[Node] = []
## True while the hero floats on the wings (see Glide).
var _gliding := false
## True while light barriers are ignored: during a light dash, and after it
## until the hero is clear of the barrier he ended inside.
var _phasing := false
var _state_timer := 0.0
var _invulnerable_timer := 0.0
var _safe_ground_timer := 0.0
## True from a jump until its rise ends; lets the jump cut apply once even if
## the button was released during another state.
var _jump_rising := false
var _attack_hit: Array[Node] = []
## Last tick's floor contact and fall speed, for the landing sound.
var _was_on_floor := false
var _last_fall_speed := 0.0
## Counts down while the landing squash shows.
var _land_timer := 0.0
## Counts down while the wings beat for the second jump.
var _double_jump_timer := 0.0
## The stick this tick, for the skid frame when it points against the run.
var _move_input := 0.0
## The one-shot strip started for the current state, so it plays once.
var _one_shot := ""

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
	health_changed.emit(health, max_health)
	heal_charges_changed.emit(heal_charges, max_heal_charges)


func _physics_process(delta: float) -> void:
	_tick_timers(delta)
	# Sampled before the state switch so a press near the end of an attack,
	# dash, heal or hurt is buffered instead of dropped.
	if not _input_blocked():
		if Input.is_action_just_pressed("jump"):
			_jump_buffer_timer = INPUT_BUFFER_TIME
		if Input.is_action_just_pressed("dash"):
			_dash_buffer_timer = INPUT_BUFFER_TIME
		if Input.is_action_just_pressed("attack"):
			_attack_buffer_timer = INPUT_BUFFER_TIME
		if Input.is_action_just_pressed("cast"):
			_cast_buffer_timer = INPUT_BUFFER_TIME
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
		State.CAST:
			_process_cast(delta)
		State.SLAM:
			_process_slam(delta)
		_:
			_process_normal(delta)
	_correct_corners(delta)
	_update_phasing()
	_update_dropping()
	_last_fall_speed = velocity.y
	move_and_slide()
	if is_on_floor() and state != State.DASH:
		_dash_available = true
		_air_jump_available = true
	if is_on_floor() and not _was_on_floor and _last_fall_speed >= LAND_SOUND_SPEED:
		if state != State.DEAD:
			Audio.play("land", -6.0)
			Feedback.dust(global_position)
	if is_on_floor() and not _was_on_floor and _last_fall_speed >= LAND_ANIM_SPEED:
		_land_timer = LAND_ANIM_TIME
		_one_shot = ""
	_was_on_floor = is_on_floor()
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
	_wing_flash_timer = maxf(_wing_flash_timer - delta, 0.0)
	_drop_timer = maxf(_drop_timer - delta, 0.0)
	_wall_jump_lock_timer = maxf(_wall_jump_lock_timer - delta, 0.0)
	_cast_buffer_timer = maxf(_cast_buffer_timer - delta, 0.0)
	_cast_cooldown_timer = maxf(_cast_cooldown_timer - delta, 0.0)
	_land_timer = maxf(_land_timer - delta, 0.0)
	_double_jump_timer = maxf(_double_jump_timer - delta, 0.0)
	_tick_powers(delta)


# --- States -------------------------------------------------------------


func _process_normal(delta: float) -> void:
	var input_dir := 0.0 if _input_blocked() else Input.get_axis("move_left", "move_right")
	if _wall_jump_lock_timer > 0.0 and int(signf(input_dir)) == _wall_dir:
		# Just sprang off a wall: steering back towards it is ignored for a
		# moment, so the wall cannot be climbed by rapid jumps.
		input_dir = 0.0
	if input_dir != 0.0:
		facing = 1 if input_dir > 0.0 else -1
	_move_input = input_dir
	if is_on_floor():
		_coyote_timer = COYOTE_TIME
	_update_wall_grip(input_dir)
	_update_glide()

	_apply_gravity(delta)
	_apply_horizontal(input_dir, delta)

	if _jump_buffer_timer > 0.0 and is_on_floor() and _wants_drop():
		_start_drop()
	elif _jump_buffer_timer > 0.0 and _coyote_timer > 0.0:
		velocity.y = JUMP_VELOCITY
		_jump_buffer_timer = 0.0
		_coyote_timer = 0.0
		_jump_rising = true
		Audio.play("jump")
	elif _jump_buffer_timer > 0.0 and _on_wall_grip:
		# Wall Grip: spring away from the wall; the air jump and dash come back.
		velocity = Vector2(WALL_JUMP_VELOCITY.x * -_wall_dir, WALL_JUMP_VELOCITY.y)
		facing = -_wall_dir
		_wall_jump_lock_timer = WALL_JUMP_LOCK
		_on_wall_grip = false
		_jump_buffer_timer = 0.0
		_jump_rising = true
		_air_jump_available = true
		_dash_available = true
		Audio.play("jump")
	elif _jump_buffer_timer > 0.0 and _air_jump_available and has_ability(Ability.WINGS):
		# Wings: a second jump from mid-air, with the same variable height.
		velocity.y = DOUBLE_JUMP_VELOCITY
		_jump_buffer_timer = 0.0
		_air_jump_available = false
		_jump_rising = true
		_wing_flash_timer = WING_FLASH_TIME
		_double_jump_timer = DOUBLE_JUMP_ANIM_TIME
		_one_shot = ""
		Audio.play("double_jump")
	elif _jump_buffer_timer > 0.0 and velocity.y > 0.0 and has_ability(Ability.GLIDE):
		# Glide: the press opens the wings; holding it keeps them open.
		_gliding = true
		_jump_buffer_timer = 0.0
	elif _jump_rising and velocity.y >= 0.0:
		_jump_rising = false
	elif _jump_rising and not _input_blocked() and not Input.is_action_pressed("jump"):
		# Variable jump height: letting go while rising cuts the jump short,
		# whichever state the button was released in.
		velocity.y *= JUMP_CUT_MULTIPLIER
		_jump_rising = false

	if _input_blocked():
		state = State.IDLE if is_on_floor() else State.FALL
		return
	if _dash_buffer_timer > 0.0 and _dash_cooldown_timer <= 0.0 and _dash_available:
		_start_dash()
		return
	if (
		_attack_buffer_timer > 0.0
		and not is_on_floor()
		and Input.is_action_pressed("move_down")
		and has_ability(Ability.SMITE)
	):
		_start_slam()
		return
	if _attack_buffer_timer > 0.0:
		_start_attack()
		return
	if _cast_buffer_timer > 0.0 and _cast_cooldown_timer <= 0.0 and has_ability(Ability.HOLY_LIGHT):
		_start_cast()
		return
	if (
		Input.is_action_just_pressed("heal")
		and is_on_floor()
		and heal_charges > 0
		and health < max_health
	):
		_start_heal()
		return

	if not is_on_floor():
		state = State.JUMP if velocity.y < 0.0 else State.FALL
	else:
		state = State.RUN if absf(velocity.x) > 5.0 else State.IDLE


func _start_dash() -> void:
	_dash_from_floor = is_on_floor()
	var light := has_ability(Ability.LIGHT_DASH)
	_set_state(State.DASH, LIGHT_DASH_TIME if light else DASH_TIME)
	if light:
		_phasing = true
	_dash_buffer_timer = 0.0
	_dash_cooldown_timer = dash_cooldown()
	_dash_available = false
	velocity = Vector2(DASH_SPEED * facing, 0.0)
	Audio.play("dash")


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
	_set_state(State.ATTACK, attack_time())
	_attack_buffer_timer = 0.0
	_attack_hit.clear()
	_restore_attack_shape()
	# A longer blade widens the hitbox away from the body.
	var rect := attack_shape.shape as RectangleShape2D
	rect.size.x = ATTACK_SHAPE_SIZE.x * _stat_factor("reach")
	var extra := (rect.size.x - ATTACK_SHAPE_SIZE.x) / 2.0
	attack_hitbox.position.x = (ATTACK_OFFSET_X + extra) * facing
	_play_once("attack")
	# A slow or quick blade plays the strip to match.
	sprite.speed_scale = ATTACK_TIME / attack_time()
	Audio.play("swing")


func _process_attack(delta: float) -> void:
	# The hero keeps moving while he swings (slower on the ground) but keeps
	# facing the way the swing started.
	var input_dir := Input.get_axis("move_left", "move_right")
	var speed_multiplier := ATTACK_MOVE_MULTIPLIER if is_on_floor() else 1.0
	# Walking off a ledge mid-swing keeps the late jump a walk would have.
	if is_on_floor():
		_coyote_timer = COYOTE_TIME
	_apply_gravity(delta)
	_apply_horizontal(input_dir, delta, speed_multiplier)
	var scale := attack_time() / ATTACK_TIME
	var elapsed := attack_time() - _state_timer
	var active := elapsed >= ATTACK_ACTIVE_FROM * scale and elapsed <= ATTACK_ACTIVE_UNTIL * scale
	if attack_shape.disabled == active:
		attack_shape.disabled = not active
	if active:
		for body in attack_hitbox.get_overlapping_bodies():
			if body in _attack_hit or not body.has_method("take_damage"):
				continue
			_attack_hit.append(body)
			body.take_damage(attack_damage(), global_position)
			# A landed hit stops time for a moment so the swing has weight.
			Feedback.hit_stop(0.05)
	if _state_timer <= 0.0:
		_end_attack()
		state = State.IDLE


func _end_attack() -> void:
	attack_shape.disabled = true
	sprite.speed_scale = 1.0


## The slam borrows the attack hitbox and widens it; the swing puts it back.
func _restore_attack_shape() -> void:
	var rect := attack_shape.shape as RectangleShape2D
	rect.size = ATTACK_SHAPE_SIZE
	attack_hitbox.position.y = ATTACK_OFFSET_Y


# --- Holy Light and Smite -----------------------------------------------


func _start_cast() -> void:
	_set_state(State.CAST, CAST_TIME)
	_cast_buffer_timer = 0.0
	_cast_cooldown_timer = CAST_COOLDOWN
	var bolt := LIGHT_BOLT_SCENE.new()
	bolt.direction = facing
	bolt.position = global_position + Vector2(CAST_OFFSET.x * facing, CAST_OFFSET.y)
	get_parent().add_child(bolt)
	Audio.play("swing", -3.0)
	Feedback.spark(bolt.position, LightBolt.COLOR)


## The hero keeps his momentum while the bolt leaves his hand.
func _process_cast(delta: float) -> void:
	if is_on_floor():
		_coyote_timer = COYOTE_TIME
	_apply_gravity(delta)
	_apply_horizontal(0.0, delta)
	if _state_timer <= 0.0:
		state = State.IDLE


func _start_slam() -> void:
	_set_state(State.SLAM, 0.0)
	_attack_buffer_timer = 0.0
	_slam_landed = false
	_slam_hit.clear()
	velocity = Vector2(0.0, SLAM_SPEED)
	# Straight down through any ledge in the way.
	collision_mask &= ~LEDGE_LAYER_BIT
	_drop_timer = 0.0
	Audio.play("dash", -2.0)


## Falls at full speed until the ground, then the shock: everything around
## the feet is hurt once, the screen shakes, and a short recovery follows.
func _process_slam(delta: float) -> void:
	if not _slam_landed:
		velocity = Vector2(0.0, SLAM_SPEED)
		if is_on_floor():
			_land_slam()
		return
	_apply_gravity(delta)
	velocity.x = 0.0
	var elapsed := SLAM_RECOVER_TIME - _state_timer
	if elapsed <= SLAM_HIT_TIME:
		for body in attack_hitbox.get_overlapping_bodies():
			if body in _slam_hit or not body.has_method("take_damage"):
				continue
			_slam_hit.append(body)
			body.take_damage(SLAM_DAMAGE, global_position)
			Feedback.hit_stop(0.05)
	elif not attack_shape.disabled:
		attack_shape.disabled = true
	if _state_timer <= 0.0:
		_end_attack()
		state = State.IDLE


func _land_slam() -> void:
	_slam_landed = true
	_state_timer = SLAM_RECOVER_TIME
	collision_mask |= LEDGE_LAYER_BIT
	attack_hitbox.position = Vector2(0.0, -SLAM_REACH.y / 2.0)
	var rect := attack_shape.shape as RectangleShape2D
	rect.size = SLAM_REACH
	attack_shape.disabled = false
	# The rock over a secret passage breaks under the blow.
	get_tree().call_group("secret_wall", "smite", global_position, SLAM_REACH)
	Audio.play("land")
	Feedback.shake(0.6)
	Feedback.hit_stop(0.06)
	for i in 3:
		Feedback.spark(global_position + Vector2((i - 1) * 18.0, -4.0), TINT_SLAM)


func _start_heal() -> void:
	_set_state(State.HEAL, HEAL_TIME)
	velocity.x = 0.0


func _process_heal(delta: float) -> void:
	_apply_gravity(delta)
	velocity.x = 0.0
	if _state_timer <= 0.0:
		heal_charges -= 1
		health = mini(health + heal_amount(), max_health)
		health_changed.emit(health, max_health)
		heal_charges_changed.emit(heal_charges, max_heal_charges)
		Audio.play("heal")
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
	_on_wall_grip = false
	_gliding = false
	state = new_state
	_state_timer = duration
	_land_timer = 0.0
	_one_shot = ""


# --- Physics helpers ----------------------------------------------------


func _apply_gravity(delta: float) -> void:
	if is_on_floor():
		return
	if _on_wall_grip:
		velocity.y = minf(velocity.y + GRAVITY * delta, WALL_SLIDE_SPEED)
		return
	var gravity := FALL_GRAVITY if velocity.y > 0.0 else GRAVITY
	if _gliding:
		velocity.y = minf(velocity.y + gravity * GLIDE_GRAVITY_MULTIPLIER * delta, GLIDE_FALL_SPEED)
		return
	var airborne := state == State.JUMP or state == State.FALL
	if (
		airborne
		and not _input_blocked()
		and absf(velocity.y) < APEX_SPEED_THRESHOLD
		and Input.is_action_pressed("jump")
	):
		gravity *= APEX_GRAVITY_MULTIPLIER
	velocity.y = minf(velocity.y + gravity * delta, MAX_FALL_SPEED)


## Input is ignored at the end of the level and while a dialogue is open.
func _input_blocked() -> bool:
	return frozen or talking


func _apply_horizontal(input_dir: float, delta: float, speed_multiplier := 1.0) -> void:
	var on_floor := is_on_floor()
	if input_dir != 0.0:
		var turning := signf(velocity.x) == -signf(input_dir)
		var accel: float
		if turning:
			accel = GROUND_TURN if on_floor else AIR_TURN
		else:
			accel = GROUND_ACCEL if on_floor else AIR_ACCEL
		var top_speed := input_dir * run_speed() * speed_multiplier
		velocity.x = move_toward(velocity.x, top_speed, accel * delta)
	else:
		var friction := GROUND_FRICTION if on_floor else AIR_FRICTION
		velocity.x = move_toward(velocity.x, 0.0, friction * delta)


## Wall Grip: holding towards a wall while in the air and falling grips
## it. Letting go of the direction, or landing, releases it.
func _update_wall_grip(input_dir: float) -> void:
	if not has_ability(Ability.WALL_GRIP) or is_on_floor() or _input_blocked():
		_on_wall_grip = false
		return
	var pushing := int(signf(input_dir))
	if pushing == 0 or velocity.y < 0.0 and not _on_wall_grip:
		_on_wall_grip = false
		return
	var wall := test_move(global_transform, Vector2(pushing, 0.0))
	if wall and _wall_is_solid(pushing):
		if not _on_wall_grip:
			velocity.y = minf(velocity.y, WALL_SLIDE_SPEED)
			_air_jump_available = true
			_dash_available = true
		_on_wall_grip = true
		_wall_dir = pushing
		_jump_rising = false
	else:
		_on_wall_grip = false


## Only the world's solids can be gripped, not a ledge's thin strip or a
## wall of light.
func _wall_is_solid(dir: int) -> bool:
	var query := PhysicsRayQueryParameters2D.create(
		global_position + Vector2(0.0, -16.0), global_position + Vector2(dir * 10.0, -16.0), 1
	)
	return not get_world_2d().direct_space_state.intersect_ray(query).is_empty()


## Glide lasts while the button is held and the hero falls.
func _update_glide() -> void:
	if not _gliding:
		return
	if is_on_floor() or _on_wall_grip or _input_blocked() or not Input.is_action_pressed("jump"):
		_gliding = false


func is_gripping_wall() -> bool:
	return _on_wall_grip


func is_gliding() -> bool:
	return _gliding


## Down and jump on a one-way ledge: true when the floor the hero stands
## on is a ledge rather than solid ground, so the same press on the ground
## is still a jump.
func _wants_drop() -> bool:
	if _input_blocked() or not Input.is_action_pressed("move_down"):
		return false
	return _on_ledge()


## Whether the floor under the hero's feet is a one-way ledge: a short ray
## down from the feet that only sees the ledge layer.
func _on_ledge() -> bool:
	var query := PhysicsRayQueryParameters2D.create(
		global_position + Vector2(0.0, -2.0), global_position + Vector2(0.0, 4.0), LEDGE_LAYER_BIT
	)
	query.hit_from_inside = true
	return not get_world_2d().direct_space_state.intersect_ray(query).is_empty()


func _start_drop() -> void:
	_jump_buffer_timer = 0.0
	_coyote_timer = 0.0
	_drop_timer = DROP_TIME
	collision_mask &= ~LEDGE_LAYER_BIT
	# Out of the ledge's thin strip at once, so the snap cannot catch it.
	velocity.y = maxf(velocity.y, 40.0)


## The ledges come back into the mask once the drop is over, and once a
## slam has landed (or was cut short by a hit, which ends the state).
func _update_dropping() -> void:
	if _drop_timer > 0.0 or (state == State.SLAM and not _slam_landed):
		return
	if collision_mask & LEDGE_LAYER_BIT == 0:
		collision_mask |= LEDGE_LAYER_BIT


## Light barriers are left out of the collision mask while light dashing,
## and stay out until the hero is no longer inside one, so a dash that ends
## in a barrier does not leave him stuck.
func _update_phasing() -> void:
	var dashing := state == State.DASH and has_ability(Ability.LIGHT_DASH)
	if _phasing and not dashing:
		collision_mask |= LightBarrier.LAYER_BIT
		if test_move(global_transform, Vector2.ZERO):
			# Still inside the barrier: keep passing through this tick.
			collision_mask &= ~LightBarrier.LAYER_BIT
		else:
			_phasing = false
	elif dashing:
		collision_mask &= ~LightBarrier.LAYER_BIT


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
		_lift_over(step.x)
		return
	if is_on_floor() and velocity.y >= 0.0 and step.x != 0.0:
		# Only a wall-like edge is stepped over; a slope is walked up as it is.
		var hit := KinematicCollision2D.new()
		if (
			test_move(global_transform, Vector2(step.x, 0.0), hit)
			and absf(hit.get_normal().x) > 0.9
		):
			_lift_over(step.x)


## Lifts the hero by the least of up to CORNER_CORRECTION pixels that lets
## him move `dx` sideways; leaves him be when no lift does (a real wall).
func _lift_over(dx: float) -> void:
	for offset in range(1, CORNER_CORRECTION + 1):
		var lift := Vector2(0.0, -offset)
		if (
			not test_move(global_transform, lift)
			and not test_move(global_transform.translated(lift), Vector2(dx, 0.0))
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


# --- Abilities ----------------------------------------------------------


func has_ability(ability: Ability) -> bool:
	return ability in abilities


## How much of the dash cooldown has passed, 0 to 1. Stays at 0 while the
## one dash per airtime is spent, until the hero lands.
func dash_readiness() -> float:
	if not _dash_available and not is_on_floor():
		return 0.0
	return 1.0 - _dash_cooldown_timer / dash_cooldown()


func dash_ready() -> bool:
	return _dash_available and _dash_cooldown_timer <= 0.0


## Unlocks an angel power for the rest of the run. Unlocking one the hero
## already has does nothing.
func unlock(ability: Ability) -> void:
	if has_ability(ability):
		return
	abilities.append(ability)
	ability_unlocked.emit(ability)


# --- Upgrades and power-ups ---------------------------------------------


## A permanent pickup: raises the maximum and fills it, and records the
## pickup as a story flag so it stays collected in the save. Church iron
## raises nothing by itself; Old Pell forges it (see Upgrade.sword_bonus).
func apply_upgrade(kind: UpgradeKind, flag: String) -> void:
	Story.set_flag(flag)
	_raise_maximum(kind)
	match kind:
		UpgradeKind.HEART:
			health = max_health
		UpgradeKind.VIAL:
			heal_charges = max_heal_charges
	health_changed.emit(health, max_health)
	heal_charges_changed.emit(heal_charges, max_heal_charges)
	upgrade_collected.emit(kind)


## Sets the maximums from the upgrade flags in the save, for a loaded or
## continued hero, and fills them (he arrives rested).
func restore_upgrades() -> void:
	_set_maximums()
	health = max_health
	heal_charges = max_heal_charges
	health_changed.emit(health, max_health)
	heal_charges_changed.emit(heal_charges, max_heal_charges)


## The maximums from scratch: the base, the difficulty, the upgrade flags
## in the save, the equipment worn and the skills learned.
func _set_maximums() -> void:
	max_health = MAX_HEALTH + SaveManager.bonus_hearts()
	max_heal_charges = MAX_HEAL_CHARGES + SaveManager.bonus_charges()
	for flag in Story.to_list():
		for kind: UpgradeKind in UpgradeKind.values():
			if flag.begins_with(Upgrade.flag_prefix(kind)):
				_raise_maximum(kind)
	max_health = maxi(max_health + int(_stat_bonus("hearts")), 1)
	max_heal_charges = maxi(max_heal_charges + int(_stat_bonus("charges")), 0)


func _raise_maximum(kind: UpgradeKind) -> void:
	match kind:
		UpgradeKind.HEART:
			max_health += 1
		UpgradeKind.VIAL:
			max_heal_charges += 1


## A timed power-up. Picking up one the hero already has restarts its time.
func grant_power(power: Power) -> void:
	powers[power] = INF if power == Power.HALO else POWER_TIME
	power_changed.emit()
	power_granted.emit(power)


func has_power(power: Power) -> bool:
	return powers.has(power)


## Seconds left on a timed power-up, 0 when the hero does not have it.
func power_time_left(power: Power) -> float:
	return float(powers.get(power, 0.0))


## A stat the equipment worn and the skills learned add to (see
## Inventory.bonus and Skills.bonus).
func _stat_bonus(stat: String) -> float:
	return Inventory.bonus(stat) + Skills.bonus(stat)


## A stat the equipment worn and the skills learned multiply (see
## Inventory.factor and Skills.factor).
func _stat_factor(stat: String) -> float:
	return Inventory.factor(stat) * Skills.factor(stat)


## The sword: its base, Pell's forge, the blade worn (see Inventory), the
## Edge skill and the Lit Blade.
func attack_damage() -> int:
	return (
		ATTACK_DAMAGE
		+ Upgrade.sword_bonus()
		+ int(_stat_bonus("damage"))
		+ (BLADE_BONUS_DAMAGE if has_power(Power.BLADE) else 0)
	)


## How long a swing takes: a heavy blade swings slower, a knife quicker.
func attack_time() -> float:
	return ATTACK_TIME * _stat_factor("attack_time")


## Hearts a healing charge gives back.
func heal_amount() -> int:
	return HEAL_AMOUNT + int(_stat_bonus("heal"))


## Hearts the worn armour and the Iron Skin skill take off a hit; a hit
## never drops below one.
func guard() -> int:
	return int(_stat_bonus("guard"))


func dash_cooldown() -> float:
	return (
		DASH_COOLDOWN
		* (ZEAL_COOLDOWN_MULTIPLIER if has_power(Power.ZEAL) else 1.0)
		* _stat_factor("dash_cooldown")
	)


func run_speed() -> float:
	return (
		RUN_SPEED
		* (ZEAL_SPEED_MULTIPLIER if has_power(Power.ZEAL) else 1.0)
		* _stat_factor("speed")
	)


## A hit on the hero after his armour: the difficulty's blow less the guard,
## never below one heart.
func guarded_damage(amount: int) -> int:
	return maxi(SaveManager.scaled_damage(amount) - guard(), 1)


# --- The pack: currency, loot, consumables, equipment -------------------


## Light or tally bones picked up (see LightDrop).
func collect_currency(currency: String, amount: int) -> void:
	Inventory.add_currency(currency, amount)
	purse_changed.emit()


## An item picked up off the ground (see LootDrop). A boss's unique loot
## sets its flag so it is never dropped again.
func collect_loot(item_id: String) -> void:
	Inventory.add(item_id)
	if Items.is_unique(item_id):
		Story.set_flag(Items.loot_flag(item_id))
	loot_collected.emit(item_id)


## Uses a consumable from the pack. Returns "" when it was used, else why
## it was not (and the item stays).
func use_item(item_id: String) -> String:
	if not Inventory.has(item_id) or not Items.is_consumable(item_id):
		return "Nothing to use"
	match item_id:
		"flask":
			if heal_charges >= max_heal_charges:
				return "The healing charges are full"
			heal_charges += 1
			heal_charges_changed.emit(heal_charges, max_heal_charges)
			Audio.play("heal")
		"halo_shard":
			grant_power(Power.HALO)
		"blade_oil":
			grant_power(Power.BLADE)
		"zeal_draught":
			grant_power(Power.ZEAL)
		_:
			return "Nothing to use"
	Inventory.remove(item_id)
	return ""


## Experience from a kill (see Loot). Enough of it is a level, and every
## level is a skill point to spend at a checkpoint camp (see Skills).
func gain_xp(amount: int) -> void:
	var levels := Skills.add_xp(amount)
	if levels <= 0 and amount <= 0:
		return
	xp_changed.emit()
	if levels > 0:
		Audio.play("checkpoint", -2.0)
		Feedback.spark(global_position + Vector2(0.0, -20.0), Color(1.0, 0.95, 0.7))
		leveled_up.emit(Skills.level())


## Wears a piece from the pack, or takes it off if it is worn. The
## maximums follow the armour; what is worn is kept with the save.
func toggle_equipment(item_id: String) -> void:
	if Inventory.is_equipped(item_id):
		Inventory.unequip(Items.slot_of(item_id))
	elif not Inventory.equip(item_id):
		return
	refresh_equipment()


## Sets the maximums again after the equipment changed, without filling
## them: a heart gained is empty, a heart lost goes with its blood.
func refresh_equipment() -> void:
	_set_maximums()
	health = mini(health, max_health)
	heal_charges = mini(heal_charges, max_heal_charges)
	health_changed.emit(health, max_health)
	heal_charges_changed.emit(heal_charges, max_heal_charges)


## A Warden's Halo worn gives a Halo at every rest (and respawn).
func _rest_halo() -> void:
	if Inventory.bonus("halo_at_rest") > 0.0 and not has_power(Power.HALO):
		powers[Power.HALO] = INF
		power_changed.emit()


func _tick_powers(delta: float) -> void:
	var expired: Array[Power] = []
	for power: Power in powers:
		if powers[power] != INF:
			powers[power] -= delta
			if powers[power] <= 0.0:
				expired.append(power)
	for power in expired:
		powers.erase(power)
	if not expired.is_empty():
		power_changed.emit()


## Turns a hit aside with the Halo, if the hero has one. Returns true when
## the hit was absorbed.
func _spend_halo() -> bool:
	if not has_power(Power.HALO):
		return false
	powers.erase(Power.HALO)
	power_changed.emit()
	_invulnerable_timer = INVULNERABLE_TIME
	Audio.play("checkpoint", -4.0)
	Feedback.spark(global_position + Vector2(0.0, -20.0), Color(1.0, 0.85, 0.4))
	Feedback.flash(sprite, 0.12)
	return true


func _clear_powers() -> void:
	if powers.is_empty():
		return
	powers.clear()
	power_changed.emit()


# --- Damage, death, checkpoints ----------------------------------------


## True while nothing can hurt the hero: dead, dashing, blinking after a
## hit, talking, or standing at the gate after finishing the level.
func _can_be_hurt() -> bool:
	return (
		not _input_blocked()
		and state != State.DEAD
		and state != State.DASH
		and _invulnerable_timer <= 0.0
	)


## Takes health away and dies at zero. Returns true when the hero survived.
func _lose_health(amount: int) -> bool:
	health = maxi(health - amount, 0)
	health_changed.emit(health, max_health)
	if health == 0:
		_die()
		return false
	return true


## Called by enemies: knockback away from the attacker.
func take_damage(amount: int, source_position: Vector2) -> void:
	if not _can_be_hurt() or _spend_halo():
		return
	if not _lose_health(guarded_damage(amount)):
		return
	var dir := 1.0 if global_position.x >= source_position.x else -1.0
	_set_state(State.HURT, HURT_TIME)
	velocity = Vector2(KNOCKBACK.x * dir, KNOCKBACK.y)
	_invulnerable_timer = INVULNERABLE_TIME
	_hurt_feedback()


## Called by spikes: put back on the last safe ground.
func take_hazard_damage(amount: int) -> void:
	if not _can_be_hurt():
		return
	# The Halo spares the heart, but the hero still has to leave the spikes.
	if _spend_halo():
		global_position = last_safe_position
		reset_physics_interpolation()
		velocity = Vector2.ZERO
		return
	if not _lose_health(guarded_damage(amount)):
		return
	_hurt_feedback()
	_set_state(State.HURT, HURT_TIME)
	global_position = last_safe_position
	reset_physics_interpolation()
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


## Heals from a pickup. Returns false when there is nothing to heal, so the
## pickup can stay where it is.
func restore_health(amount: int) -> bool:
	if state == State.DEAD or health >= max_health:
		return false
	health = mini(health + amount, max_health)
	health_changed.emit(health, max_health)
	return true


## Resting at a checkpoint: full health, healing refilled, respawn point set.
func rest_at(checkpoint_position: Vector2) -> void:
	respawn_position = checkpoint_position
	last_safe_position = checkpoint_position
	health = max_health
	heal_charges = max_heal_charges
	health_changed.emit(health, max_health)
	heal_charges_changed.emit(heal_charges, max_heal_charges)
	_rest_halo()


## Taking a hit: a sound, a white flash, a shake and a short freeze.
func _hurt_feedback() -> void:
	Audio.play("hurt")
	Feedback.flash(sprite, 0.12)
	Feedback.shake(0.45)
	Feedback.hit_stop(0.08)


func _die() -> void:
	_set_state(State.DEAD, DEATH_TIME)
	velocity = Vector2(0.0, -90.0)
	_clear_powers()
	Audio.play("death")
	Feedback.shake(0.6)
	died.emit()


## The angels' system resurrects the hero at the last checkpoint.
func respawn() -> void:
	global_position = respawn_position
	reset_physics_interpolation()
	last_safe_position = respawn_position
	velocity = Vector2.ZERO
	# Nothing pressed before the death carries over.
	_jump_rising = false
	_jump_buffer_timer = 0.0
	_dash_buffer_timer = 0.0
	_attack_buffer_timer = 0.0
	_cast_buffer_timer = 0.0
	_coyote_timer = 0.0
	_dash_cooldown_timer = 0.0
	_cast_cooldown_timer = 0.0
	collision_mask |= LEDGE_LAYER_BIT
	_drop_timer = 0.0
	_on_wall_grip = false
	_gliding = false
	_wall_jump_lock_timer = 0.0
	_dash_available = true
	_air_jump_available = true
	health = max_health
	heal_charges = max_heal_charges
	health_changed.emit(health, max_health)
	heal_charges_changed.emit(heal_charges, max_heal_charges)
	_invulnerable_timer = INVULNERABLE_TIME
	state = State.IDLE
	_rest_halo()
	respawned.emit()


# --- Visuals ------------------------------------------------------------


func _update_sprite() -> void:
	sprite.flip_h = facing < 0
	sprite.modulate = Color.WHITE
	if state != State.ATTACK:
		sprite.speed_scale = 1.0
	match state:
		State.IDLE, State.RUN:
			_update_ground_sprite()
		State.JUMP:
			if _double_jump_timer > 0.0:
				_play_once("double_jump")
				sprite.modulate = TINT_WINGS
			elif velocity.y < -JUMP_LAUNCH_SPEED:
				_pose("jump", 0)
			elif velocity.y < -APEX_SPEED_THRESHOLD:
				_pose("jump", 1)
			else:
				_pose("jump", 2)
		State.FALL:
			if _on_wall_grip:
				_play("wall")
				sprite.flip_h = _wall_dir < 0
			elif _gliding:
				_play("glide")
				sprite.modulate = TINT_GLIDE
			elif _double_jump_timer > 0.0:
				_play_once("double_jump")
				sprite.modulate = TINT_WINGS
			elif velocity.y < APEX_SPEED_THRESHOLD:
				# Just past the top of the arc, before the fall proper.
				_pose("jump", 2)
			else:
				_play("fall")
		State.ATTACK:
			# Started once in _start_attack; the Lit Blade tints the swing.
			if has_power(Power.BLADE):
				sprite.modulate = TINT_BLADE
		State.DASH:
			_play("dash")
			sprite.modulate = TINT_LIGHT_DASH if has_ability(Ability.LIGHT_DASH) else TINT_DASH
		State.HURT:
			_play_once("hurt")
		State.HEAL:
			_play("heal")
			sprite.modulate = TINT_HEAL
		State.CAST:
			_play_once("cast")
			sprite.modulate = TINT_CAST
		State.SLAM:
			_pose("slam", 1 if _slam_landed else 0)
			sprite.modulate = TINT_SLAM
		State.DEAD:
			_play_once("death")
	# Blink while invulnerable.
	if _invulnerable_timer > 0.0 and state != State.DEAD:
		sprite.visible = fmod(_invulnerable_timer, 0.12) < 0.07
	else:
		sprite.visible = true


## On the ground: the landing squash first, then the skid when the stick
## points against the run, else the run (paced by the speed) or the idle.
func _update_ground_sprite() -> void:
	if _land_timer > 0.0:
		_play_once("land")
		return
	var skidding := (
		state == State.RUN
		and _move_input != 0.0
		and signf(_move_input) != signf(velocity.x)
		and absf(velocity.x) >= SKID_SPEED
	)
	if skidding:
		_pose("skid", 0)
		# The feet point the way he slides; he looks back where he is going.
		sprite.flip_h = velocity.x < 0.0
	elif state == State.RUN:
		_play("run")
		sprite.speed_scale = clampf(
			absf(velocity.x) / RUN_SPEED, RUN_ANIM_SPEED_RANGE.x, RUN_ANIM_SPEED_RANGE.y
		)
	else:
		_play("idle")


## Switches to a looping animation, or restarts it if it was paused by _pose.
func _play(anim_name: String) -> void:
	if sprite.animation != anim_name:
		sprite.play(anim_name)
	elif not sprite.is_playing() and sprite.sprite_frames.get_animation_loop(anim_name):
		sprite.play(anim_name)


## Starts a one-shot strip from its first frame, once per state (or per
## landing or wing beat, which clear the token), so a finished strip is
## never replayed while the state lasts and stays on its last frame.
func _play_once(anim_name: String) -> void:
	if _one_shot == anim_name:
		return
	_one_shot = anim_name
	sprite.play(anim_name)


func _pose(anim_name: String, frame: int) -> void:
	if sprite.animation != anim_name:
		sprite.animation = anim_name
	sprite.pause()
	sprite.frame = frame
