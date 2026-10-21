class_name Boss
extends CharacterBody2D

## A boss: a big enemy that waits in its room until the hero walks in, then
## fights in two phases until it is dead. main.gd runs the fight around it:
## the intro dialogue, the seals on the room's doors, the health bar at the
## bottom of the screen, the defeat dialogue and the story flag
## (`boss:<id>`) the locked doors read.
##
## The boss walks at the hero and swings when he is close (a wind-up, a
## strike, a recovery, like a Walker's but wider), and charges across the
## room when he keeps his distance. Each body has a move of its own on top
## (`move` in the profile): the brutes slam the ground and send a tall
## shockwave along the floor, the angels leap and throw three bolts of
## light down in a fan, and the imp kings call two imps to them when the
## fight turns. From half health it moves and winds up faster and leaps at
## the hero after every charge; the turn is marked by a roar, a flash and
## a shake. Hits do not stagger it. `boss_id` picks the profile; the
## sprite is one of the character strips scaled up. The node's origin is
## at the feet.

signal engaged(boss: Boss)
signal health_changed(current: int, max_value: int)
signal defeated(boss: Boss)

enum State {
	DORMANT,
	APPROACH,
	WINDUP,
	STRIKE,
	RECOVER,
	CHARGE_WINDUP,
	CHARGE,
	LEAP,
	SLAM_WINDUP,
	SLAM_RECOVER,
	VOLLEY,
	DEAD,
}

const GRAVITY := 700.0
const MAX_FALL_SPEED := 300.0
const HURT_TIME := 0.15
## How long the body lies there before it fades.
const CORPSE_TIME := 1.5
const DEATH_SPARKS := Color(1.0, 0.85, 0.5)
const DEATH_PUFF := Color(1.0, 0.9, 0.6)
const TINT_WINDUP := Color(1.0, 0.75, 0.55)
const TINT_CHARGE := Color(1.0, 0.6, 0.5)
const TINT_HURT := Color(1.0, 0.5, 0.5)
## Phase two starts at this share of the health.
const PHASE_TWO_AT := 0.5
## How much quicker the wind-ups and walking are in phase two.
const PHASE_TWO_HASTE := 0.7
## The hero has to be roughly at the same height to be swung at.
const ATTACK_HEIGHT_TOLERANCE := 32.0
## Further than this and the boss charges instead of walking.
const CHARGE_RANGE := 90.0
const CHARGE_TIME_MAX := 1.4
const LEAP_VELOCITY_Y := -260.0
const LEAP_TIME := 0.9
## The slam: the hero between the swing's reach and this far away.
const SLAM_RANGE := 130.0
const SLAM_WINDUP := 0.7
const SLAM_RECOVER := 0.8
const SLAM_COOLDOWN := 3.0
const TINT_SLAM := Color(1.0, 0.6, 0.45)
## The volley: a leap from this far away; the bolts go at the apex.
const VOLLEY_MIN := 56.0
const VOLLEY_MAX := 220.0
const VOLLEY_VELOCITY_Y := -230.0
const VOLLEY_TIME := 1.2
const VOLLEY_COOLDOWN := 3.5
const VOLLEY_SPREAD := 22.0
const VOLLEY_COLOR := Color(1.0, 0.95, 0.7)
const VOLLEY_CORE := Color(1.0, 1.0, 0.9)
## The summons: how many imps come when the fight turns, and from how far.
const SUMMON_COUNT := 2
const SUMMON_SPACING := 48.0
const MOTE_SCENE := preload("res://scenes/heal_mote.tscn")
const SHOT_SCRIPT := preload("res://scripts/enemies/enemy_shot.gd")
const WALKER_SCENE := preload("res://scenes/enemies/walker.tscn")
## The move each body has: see the top of the file.
const MOVES := {"brute": "slam", "angel_guard": "volley", "imp": "summon"}

## name: shown on the health bar. character: the sprite strips, and walk:
## the name of their walking loop. scale: how much bigger than the strips.
## tint: the sprite's colour. body: collision size (at the scale). health:
## hits to kill. speed: walking speed.
## faces_left: whether the strips face left. attack_range / reach / windup /
## strike / recover / cooldown / attack_damage: the swing, as a Walker's.
## charge_speed / charge_windup / charge_cooldown: the rush. contact_damage:
## hearts for touching it. motes: heal motes dropped on death.
const PROFILES := {
	# Paradise: the Warden of the Low Gate.
	"seraph_warden":
	{
		"name": "Seraph Warden",
		"character": "angel_guard",
		"walk": "walk",
		"scale": 1.5,
		"tint": Color(1.0, 0.92, 0.7),
		"body": Vector2(32, 80),
		"health": 24,
		"speed": 34.0,
		"faces_left": true,
		"attack_range": 60.0,
		"reach": Vector2(52, 60),
		"windup": 0.55,
		"strike": 0.2,
		"recover": 0.6,
		"cooldown": 0.9,
		"attack_damage": 2,
		"charge_speed": 230.0,
		"charge_windup": 0.6,
		"charge_cooldown": 2.5,
		"contact_damage": 1,
		"motes": 2,
	},
	# Purgatory: an angel that chose to fall and never finished.
	"the_penitent":
	{
		"name": "The Penitent",
		"character": "angel_guard",
		"walk": "walk",
		"scale": 1.25,
		"tint": Color(0.75, 0.75, 0.85),
		"body": Vector2(28, 68),
		"health": 20,
		"speed": 40.0,
		"faces_left": true,
		"attack_range": 50.0,
		"reach": Vector2(44, 50),
		"windup": 0.45,
		"strike": 0.2,
		"recover": 0.5,
		"cooldown": 0.8,
		"attack_damage": 1,
		"charge_speed": 250.0,
		"charge_windup": 0.5,
		"charge_cooldown": 2.0,
		"contact_damage": 1,
		"motes": 2,
	},
	# Limbo: the first of the fallen to stand up, grown huge on forgetting.
	"limbo_lord":
	{
		"name": "Limbo Lord",
		"character": "imp",
		"walk": "run",
		"scale": 2.5,
		"tint": Color(0.7, 0.75, 1.0),
		"body": Vector2(40, 56),
		"health": 26,
		"speed": 48.0,
		"faces_left": true,
		"attack_range": 48.0,
		"reach": Vector2(46, 44),
		"windup": 0.35,
		"strike": 0.15,
		"recover": 0.45,
		"cooldown": 0.7,
		"attack_damage": 1,
		"charge_speed": 260.0,
		"charge_windup": 0.45,
		"charge_cooldown": 1.8,
		"contact_damage": 1,
		"motes": 2,
	},
	# Lust: the keeper of the Palace of Velvet.
	"palace_of_velvet":
	{
		"name": "Keeper of the Palace",
		"character": "imp",
		"walk": "run",
		"scale": 2.0,
		"tint": Color(1.0, 0.6, 0.85),
		"body": Vector2(34, 46),
		"health": 28,
		"speed": 60.0,
		"faces_left": true,
		"attack_range": 40.0,
		"reach": Vector2(40, 40),
		"windup": 0.3,
		"strike": 0.15,
		"recover": 0.4,
		"cooldown": 0.6,
		"attack_damage": 1,
		"charge_speed": 280.0,
		"charge_windup": 0.4,
		"charge_cooldown": 1.5,
		"contact_damage": 1,
		"motes": 2,
	},
	# Gluttony: the brute that ate the circle.
	"the_bloated_one":
	{
		"name": "The Bloated One",
		"character": "brute",
		"walk": "walk",
		"scale": 1.5,
		"tint": Color(0.75, 1.0, 0.7),
		"body": Vector2(52, 90),
		"health": 36,
		"speed": 22.0,
		"faces_left": true,
		"attack_range": 70.0,
		"reach": Vector2(68, 64),
		"windup": 0.8,
		"strike": 0.3,
		"recover": 1.0,
		"cooldown": 1.4,
		"attack_damage": 2,
		"charge_speed": 200.0,
		"charge_windup": 0.8,
		"charge_cooldown": 3.0,
		"contact_damage": 2,
		"motes": 3,
	},
	# Greed: the thing that guards the Golden Vault.
	"golden_vault":
	{
		"name": "Warden of the Vault",
		"character": "brute",
		"walk": "walk",
		"scale": 1.3,
		"tint": Color(1.0, 0.88, 0.5),
		"body": Vector2(46, 78),
		"health": 34,
		"speed": 30.0,
		"faces_left": true,
		"attack_range": 62.0,
		"reach": Vector2(58, 56),
		"windup": 0.6,
		"strike": 0.25,
		"recover": 0.8,
		"cooldown": 1.1,
		"attack_damage": 2,
		"charge_speed": 240.0,
		"charge_windup": 0.6,
		"charge_cooldown": 2.4,
		"contact_damage": 1,
		"motes": 3,
	},
	# Wrath: Gorrow, the demon captain who served under the Lowest.
	"the_fury":
	{
		"name": "Gorrow, the Fury",
		"character": "brute",
		"walk": "walk",
		"scale": 1.2,
		"tint": Color(1.0, 0.55, 0.5),
		"body": Vector2(42, 72),
		"health": 40,
		"speed": 44.0,
		"faces_left": true,
		"attack_range": 58.0,
		"reach": Vector2(54, 54),
		"windup": 0.5,
		"strike": 0.25,
		"recover": 0.6,
		"cooldown": 0.8,
		"attack_damage": 2,
		"charge_speed": 270.0,
		"charge_windup": 0.45,
		"charge_cooldown": 1.6,
		"contact_damage": 1,
		"motes": 3,
	},
	# Heresy: a bishop of the Choir, sent down for what he taught.
	"cursed_bishop":
	{
		"name": "Cursed Bishop",
		"character": "angel_guard",
		"walk": "walk",
		"scale": 1.4,
		"tint": Color(0.7, 0.5, 0.45),
		"body": Vector2(30, 74),
		"health": 38,
		"speed": 36.0,
		"faces_left": true,
		"attack_range": 56.0,
		"reach": Vector2(50, 56),
		"windup": 0.45,
		"strike": 0.2,
		"recover": 0.55,
		"cooldown": 0.8,
		"attack_damage": 2,
		"charge_speed": 260.0,
		"charge_windup": 0.5,
		"charge_cooldown": 2.0,
		"contact_damage": 1,
		"motes": 3,
	},
	# Violence: the champion of the Red Arena.
	"the_red_arena":
	{
		"name": "Champion of the Red Arena",
		"character": "brute",
		"walk": "walk",
		"scale": 1.4,
		"tint": Color(1.0, 0.45, 0.4),
		"body": Vector2(48, 84),
		"health": 44,
		"speed": 40.0,
		"faces_left": true,
		"attack_range": 64.0,
		"reach": Vector2(62, 60),
		"windup": 0.5,
		"strike": 0.25,
		"recover": 0.6,
		"cooldown": 0.8,
		"attack_damage": 2,
		"charge_speed": 290.0,
		"charge_windup": 0.4,
		"charge_cooldown": 1.5,
		"contact_damage": 2,
		"motes": 3,
	},
	# Fraud: the king who never was.
	"the_false_king":
	{
		"name": "The False King",
		"character": "imp",
		"walk": "run",
		"scale": 2.8,
		"tint": Color(1.0, 0.85, 0.45),
		"body": Vector2(44, 62),
		"health": 42,
		"speed": 70.0,
		"faces_left": true,
		"attack_range": 50.0,
		"reach": Vector2(50, 48),
		"windup": 0.3,
		"strike": 0.15,
		"recover": 0.35,
		"cooldown": 0.5,
		"attack_damage": 2,
		"charge_speed": 300.0,
		"charge_windup": 0.35,
		"charge_cooldown": 1.3,
		"contact_damage": 1,
		"motes": 3,
	},
	# Treachery: the Lowest. Tobiah, at the bottom, in his brother's shape.
	# His lines (the brothers, both endings) are a later beat.
	"final_boss_the_betrayer":
	{
		"name": "The Lowest",
		"character": "hero",
		"walk": "run",
		"scale": 1.3,
		"tint": Color(0.55, 0.5, 0.65),
		"body": Vector2(20, 42),
		"health": 50,
		"speed": 90.0,
		"faces_left": false,
		"attack_range": 36.0,
		"reach": Vector2(36, 34),
		"windup": 0.25,
		"strike": 0.15,
		"recover": 0.3,
		"cooldown": 0.45,
		"attack_damage": 2,
		"charge_speed": 300.0,
		"charge_windup": 0.3,
		"charge_cooldown": 1.2,
		"contact_damage": 1,
		"motes": 0,
	},
	# The Garden, the day before: the first angel, who made the Basin.
	# Placed by the sidecar once the clock has turned.
	"gardener":
	{
		"name": "The Gardener",
		"character": "angel_guard",
		"walk": "walk",
		"scale": 2.2,
		"tint": Color(1.0, 1.0, 0.95),
		"body": Vector2(40, 70),
		"health": 60,
		"speed": 75.0,
		"faces_left": true,
		"attack_range": 56.0,
		"reach": Vector2(56, 52),
		"windup": 0.35,
		"strike": 0.15,
		"recover": 0.4,
		"cooldown": 0.6,
		"attack_damage": 2,
		"charge_speed": 320.0,
		"charge_windup": 0.35,
		"charge_cooldown": 1.1,
		"contact_damage": 1,
		"motes": 0,
	},
}

## Which profile; see PROFILES. Set by the level before the boss is added.
var boss_id := "seraph_warden"
## The room the boss guards, in level pixels: the hero walking into it
## starts the fight.
var arena := Rect2()
## Where dropped heal motes go (set by the level).
var drop_parent: Node

var state: State = State.DORMANT
var health := 0
var max_health := 1
var direction := -1
var profile: Dictionary = {}
var _walk_anim := "walk"
var _faces_left := true
var _half_width := 0.0
var _state_timer := 0.0
var _hurt_timer := 0.0
var _attack_cooldown := 0.0
var _charge_cooldown := 0.0
var _move_cooldown := 0.0
var _leap_after_charge := false
## Whether the volley's bolts have gone on this leap.
var _volley_thrown := false
var _phase_two := false
var _dead := false
var _player: Player

@onready var sprite: AnimatedSprite2D = $Sprite
@onready var body_shape: CollisionShape2D = $CollisionShape2D
@onready var hitbox: Area2D = $Hitbox
@onready var hitbox_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var attack_hitbox: Area2D = $AttackHitbox
@onready var attack_shape: CollisionShape2D = $AttackHitbox/CollisionShape2D


## The story flag a dead boss sets.
static func flag_for(id: String) -> String:
	return "boss:%s" % id


## The profile id for a boss marker's label ("Seraph Warden" ->
## "seraph_warden"), whether or not a profile exists for it.
static func id_for(label: String) -> String:
	var id := label.to_lower().strip_edges()
	for ch in [":", ",", "'", "."]:
		id = id.replace(ch, "")
	return id.replace(" ", "_")


static func has_profile(id: String) -> bool:
	return PROFILES.has(id)


func _ready() -> void:
	add_to_group("enemy")
	add_to_group("boss")
	if not PROFILES.has(boss_id):
		push_error("Boss: no profile called '%s'" % boss_id)
		boss_id = PROFILES.keys()[0]
	profile = PROFILES[boss_id]
	max_health = int(profile["health"])
	health = max_health
	_faces_left = bool(profile["faces_left"])
	_walk_anim = str(profile.get("walk", "walk"))
	var body: Vector2 = profile["body"]
	_half_width = body.x / 2.0
	AnimLoader.setup(sprite, str(profile["character"]))
	sprite.scale = Vector2.ONE * float(profile["scale"])
	sprite.position *= float(profile["scale"])
	sprite.modulate = profile["tint"]
	sprite.play("idle")
	var shape := RectangleShape2D.new()
	shape.size = body
	body_shape.shape = shape
	body_shape.position = Vector2(0.0, -body.y / 2.0)
	var hit_shape := RectangleShape2D.new()
	hit_shape.size = body + Vector2(4.0, 0.0)
	hitbox_shape.shape = hit_shape
	hitbox_shape.position = body_shape.position
	var reach: Vector2 = profile["reach"]
	var attack_rect := RectangleShape2D.new()
	attack_rect.size = reach
	attack_shape.shape = attack_rect
	attack_shape.disabled = true
	_face(direction)


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL_SPEED)
	_attack_cooldown = maxf(_attack_cooldown - delta, 0.0)
	_charge_cooldown = maxf(_charge_cooldown - delta, 0.0)
	_move_cooldown = maxf(_move_cooldown - delta, 0.0)
	_state_timer -= delta
	if _hurt_timer > 0.0:
		_hurt_timer -= delta
		if _hurt_timer <= 0.0:
			_tint(Color.WHITE)
	match state:
		State.DORMANT:
			velocity.x = 0.0
			if _hero_in_arena():
				engaged.emit(self)
		State.APPROACH:
			_process_approach()
		State.WINDUP, State.STRIKE, State.RECOVER:
			_process_swing()
		State.CHARGE_WINDUP:
			velocity.x = 0.0
			if _state_timer <= 0.0:
				_start_charge()
		State.CHARGE:
			_process_charge()
		State.LEAP:
			if is_on_floor() and _state_timer < LEAP_TIME - 0.1:
				velocity.x = 0.0
				_set_state(State.APPROACH, 0.0)
			elif _state_timer <= 0.0:
				_set_state(State.APPROACH, 0.0)
		State.SLAM_WINDUP:
			velocity.x = 0.0
			if _state_timer <= 0.0:
				_slam()
		State.SLAM_RECOVER:
			velocity.x = 0.0
			if _state_timer <= 0.0:
				_set_state(State.APPROACH, 0.0)
				sprite.play(_walk_anim)
		State.VOLLEY:
			_process_volley()
	move_and_slide()
	if state != State.DORMANT and state != State.DEAD:
		_hurt_player()


## Called by main.gd once the intro is over: the boss wakes up and comes.
func start_fight() -> void:
	if state != State.DORMANT or _dead:
		return
	_set_state(State.APPROACH, 0.0)
	sprite.play(_walk_anim)


func in_phase_two() -> bool:
	return health <= int(ceil(max_health * PHASE_TWO_AT))


func is_fighting() -> bool:
	return state != State.DORMANT and state != State.DEAD


## Phase two shortens the wind-ups and quickens the walk.
func _haste() -> float:
	return PHASE_TWO_HASTE if in_phase_two() else 1.0


func _hero_in_arena() -> bool:
	if not _find_player():
		return false
	return arena.has_point(_player.global_position)


func _find_player() -> bool:
	if not is_instance_valid(_player):
		_player = get_tree().get_first_node_in_group("player") as Player
	return _player != null and _player.state != Player.State.DEAD


func _process_approach() -> void:
	if not _find_player():
		velocity.x = 0.0
		return
	var offset := _player.global_position - global_position
	_face(1 if offset.x >= 0.0 else -1)
	if absf(offset.x) <= float(profile["attack_range"]):
		velocity.x = 0.0
		if _attack_cooldown <= 0.0 and absf(offset.y) <= ATTACK_HEIGHT_TOLERANCE:
			_start_windup()
		return
	if _move_cooldown <= 0.0 and _move_wanted(offset):
		_start_move()
		return
	if absf(offset.x) >= CHARGE_RANGE and _charge_cooldown <= 0.0:
		_start_charge_windup()
		return
	velocity.x = direction * float(profile["speed"]) / _haste()


## The body's own move: see MOVES.
func move() -> String:
	return str(MOVES.get(str(profile["character"]), ""))


## Whether the hero stands where the body's move wants him.
func _move_wanted(offset: Vector2) -> bool:
	var dx := absf(offset.x)
	match move():
		"slam":
			return dx <= SLAM_RANGE and absf(offset.y) <= ATTACK_HEIGHT_TOLERANCE
		"volley":
			return dx >= VOLLEY_MIN and dx <= VOLLEY_MAX and offset.y > -64.0
	return false


func _start_move() -> void:
	match move():
		"slam":
			# The arms go up (the strip's raised frame) and stay there.
			_set_state(State.SLAM_WINDUP, SLAM_WINDUP * _haste())
			sprite.animation = "attack"
			sprite.pause()
			sprite.frame = 1
			_tint(TINT_SLAM)
		"volley":
			_set_state(State.VOLLEY, VOLLEY_TIME)
			_volley_thrown = false
			velocity = Vector2(direction * 40.0, VOLLEY_VELOCITY_Y)
			sprite.play("idle")
			_tint(TINT_WINDUP)
			Audio.play("jump", -4.0)


## The ground shakes and a tall wave runs along the floor the way it faces.
func _slam() -> void:
	sprite.play("attack")
	_tint(Color.WHITE)
	var shot: EnemyShot = SHOT_SCRIPT.new()
	shot.kind = EnemyShot.Kind.WAVE
	shot.big = true
	shot.damage = int(profile["attack_damage"])
	shot.direction = Vector2(direction, 0.0)
	shot.position = global_position + Vector2(direction * (_half_width + 8.0), 0.0)
	_spawn_parent().add_child(shot)
	Feedback.shake(0.5)
	Audio.play("land")
	for i in 4:
		Feedback.spark(
			global_position + Vector2(direction * (_half_width + i * 10.0), -2.0),
			SHOT_SCRIPT.WAVE_DUST
		)
	_move_cooldown = SLAM_COOLDOWN * _haste()
	_set_state(State.SLAM_RECOVER, SLAM_RECOVER * _haste())


## In the air: at the top of the leap three bolts of light go down at the
## hero in a fan; landing ends it.
func _process_volley() -> void:
	if not _volley_thrown and velocity.y >= 0.0:
		_volley_thrown = true
		_throw_volley()
	if (is_on_floor() and _state_timer < VOLLEY_TIME - 0.1) or _state_timer <= 0.0:
		velocity.x = 0.0
		_tint(Color.WHITE)
		_move_cooldown = VOLLEY_COOLDOWN * _haste()
		_set_state(State.APPROACH, 0.0)
		sprite.play(_walk_anim)


func _throw_volley() -> void:
	sprite.play("attack")
	_tint(Color.WHITE)
	var from := global_position + Vector2(direction * 8.0, body_shape.position.y)
	var at := (
		_player.global_position + Vector2(0.0, -12.0) if _find_player() else from + Vector2.DOWN
	)
	var aim := (at - from).normalized()
	if aim == Vector2.ZERO:
		aim = Vector2.DOWN
	for i in 3:
		var shot: EnemyShot = SHOT_SCRIPT.new()
		shot.kind = EnemyShot.Kind.BOLT
		shot.color = VOLLEY_COLOR
		shot.core = VOLLEY_CORE
		shot.direction = aim.rotated(deg_to_rad((i - 1) * VOLLEY_SPREAD))
		shot.position = from
		_spawn_parent().add_child(shot)
	Audio.play("swing", -2.0)
	Feedback.spark(from, VOLLEY_COLOR)


## The imp kings' move: two imps come running when the fight turns.
func _summon() -> void:
	if _dead or not is_inside_tree():
		return
	for i in SUMMON_COUNT:
		var imp: Walker = WALKER_SCENE.instantiate()
		imp.character = "imp"
		imp.behaviour = "chase"
		imp.death_y = global_position.y + 2000.0
		imp.drop_parent = drop_parent
		imp.position = global_position + Vector2((i * 2 - 1) * SUMMON_SPACING, -8.0)
		get_parent().add_child(imp)
		Feedback.puff(imp.position + Vector2(0.0, -12.0), TINT_CHARGE)


## Where shots go: the level's objects, or the boss's own parent.
func _spawn_parent() -> Node:
	return drop_parent if drop_parent else get_parent()


func _start_windup() -> void:
	_set_state(State.WINDUP, float(profile["windup"]) * _haste())
	sprite.animation = "attack"
	sprite.pause()
	sprite.frame = 0
	_tint(TINT_WINDUP)


func _process_swing() -> void:
	velocity.x = 0.0
	if state == State.STRIKE:
		for body in attack_hitbox.get_overlapping_bodies():
			if body is Player:
				body.take_damage(int(profile["attack_damage"]), global_position)
	if _state_timer > 0.0:
		return
	match state:
		State.WINDUP:
			_set_state(State.STRIKE, float(profile["strike"]))
			_tint(Color.WHITE)
			sprite.play("attack")
			attack_shape.disabled = false
		State.STRIKE:
			_set_state(State.RECOVER, float(profile["recover"]) * _haste())
			attack_shape.disabled = true
			sprite.play("idle")
		State.RECOVER:
			_attack_cooldown = float(profile["cooldown"]) * _haste()
			_set_state(State.APPROACH, 0.0)
			sprite.play(_walk_anim)


func _start_charge_windup() -> void:
	_set_state(State.CHARGE_WINDUP, float(profile["charge_windup"]) * _haste())
	sprite.play("idle")
	_tint(TINT_CHARGE)


func _start_charge() -> void:
	if _find_player():
		_face(1 if _player.global_position.x >= global_position.x else -1)
	_set_state(State.CHARGE, CHARGE_TIME_MAX)
	_tint(Color.WHITE)
	sprite.play(_walk_anim)
	sprite.speed_scale = 2.0
	_leap_after_charge = in_phase_two()
	Audio.play("dash", -4.0)


## The rush: straight ahead at full speed until a wall or the time runs
## out, hurting the hero on contact.
func _process_charge() -> void:
	velocity.x = direction * float(profile["charge_speed"])
	if is_on_wall() or _state_timer <= 0.0:
		sprite.speed_scale = 1.0
		_charge_cooldown = float(profile["charge_cooldown"])
		if _leap_after_charge and _find_player():
			_start_leap()
		else:
			velocity.x = 0.0
			_set_state(State.APPROACH, 0.0)
			Feedback.shake(0.25)


## Phase two: a jump that lands by the hero.
func _start_leap() -> void:
	_leap_after_charge = false
	var offset := _player.global_position - global_position
	_face(1 if offset.x >= 0.0 else -1)
	var air_time := -2.0 * LEAP_VELOCITY_Y / GRAVITY
	velocity = Vector2(clampf(offset.x / air_time, -220.0, 220.0), LEAP_VELOCITY_Y)
	_set_state(State.LEAP, LEAP_TIME)
	sprite.play("idle")
	Audio.play("jump", -4.0)


func _set_state(new_state: State, duration: float) -> void:
	attack_shape.disabled = true
	state = new_state
	_state_timer = duration


func _face(dir: int) -> void:
	direction = dir
	sprite.flip_h = (dir > 0) == _faces_left
	var reach: Vector2 = profile["reach"]
	attack_shape.position = Vector2((_half_width + reach.x / 2.0) * dir, -reach.y / 2.0)


func _tint(color: Color) -> void:
	sprite.modulate = (profile["tint"] as Color) * color


func _hurt_player() -> void:
	for body in hitbox.get_overlapping_bodies():
		if body is Player:
			body.take_damage(int(profile["contact_damage"]), global_position)


## Hits flash and count, but never stagger the boss.
func take_damage(amount: int, _source_position: Vector2) -> void:
	if _dead or state == State.DORMANT:
		return
	health = maxi(health - amount, 0)
	health_changed.emit(health, max_health)
	Audio.play("hit")
	Feedback.flash(sprite, 0.1)
	Feedback.spark(global_position + Vector2(0.0, body_shape.position.y))
	_hurt_timer = HURT_TIME
	_tint(TINT_HURT)
	if health <= 0:
		_die()
	elif not _phase_two and in_phase_two():
		_enter_phase_two()


## The turn of the fight: a roar, a flash, a shake and a blink of stopped
## time, the body swelling for a moment; the imp kings call their imps.
func _enter_phase_two() -> void:
	_phase_two = true
	Audio.play("roar")
	Feedback.flash(sprite, 0.35)
	Feedback.shake(0.5)
	Feedback.hit_stop(0.1)
	var pulse := create_tween()
	var base := Vector2.ONE * float(profile["scale"])
	pulse.tween_property(sprite, "scale", base * 1.15, 0.12).set_ease(Tween.EASE_OUT)
	pulse.tween_property(sprite, "scale", base, 0.25).set_ease(Tween.EASE_IN)
	if move() == "summon":
		# Deferred: the hit may come from a bolt's overlap signal, and the
		# physics server refuses new bodies while it flushes that.
		_summon.call_deferred()


## Death: the body plays its death strip (or topples, for a sprite without
## one), lies there and fades, with a burst of sparks, a puff of light and
## a long shake, and leaves heal motes for the hero to recover with.
func _die() -> void:
	_dead = true
	_set_state(State.DEAD, 0.0)
	set_physics_process(false)
	Audio.play("enemy_die")
	Audio.play("level_complete", -6.0)
	Feedback.shake(0.9)
	Feedback.hit_stop(0.15)
	var centre := global_position + Vector2(0.0, body_shape.position.y)
	for i in 4:
		Feedback.spark(
			centre + Vector2(randf_range(-12.0, 12.0), randf_range(-20.0, 20.0)), DEATH_SPARKS
		)
	body_shape.set_deferred("disabled", true)
	hitbox.set_deferred("monitoring", false)
	attack_hitbox.set_deferred("monitoring", false)
	sprite.speed_scale = 1.0
	_tint(Color.WHITE)
	Feedback.puff(centre, DEATH_PUFF)
	# The body falls away from the hero, who is on the side it faces.
	EnemyDeath.play(sprite, -direction, CORPSE_TIME, queue_free)
	for i in int(profile["motes"]):
		var mote: HealMote = MOTE_SCENE.instantiate()
		mote.position = centre + Vector2((i - 0.5) * 20.0, 0.0)
		var parent := drop_parent if drop_parent else get_parent()
		parent.add_child.call_deferred(mote)
	defeated.emit(self)
