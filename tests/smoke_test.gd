extends SceneTree

## Headless smoke test: boots the main scene, drives the hero with fake
## input for a few seconds of physics ticks and checks that the basics work.
##
##   godot --headless --path . --script tests/smoke_test.gd
##
## Exits with code 1 when a check fails, when the scene does not load, or
## when a script error stops the checks from running.

## Every check the script makes; fewer at the end means something broke early.
const EXPECTED_CHECKS := 25
const LAST_TICK := 540

var _ticks := 0
var _main: Node
var _player: Player
var _start_x := 0.0
var _mark_x := 0.0
var _camera_y := 0.0
var _enemy_count := 0
var _failures := 0
var _checks := 0


func _initialize() -> void:
	# Flyers pick a random hover phase; a fixed seed keeps every run identical.
	seed(1)
	var scene: PackedScene = load("res://scenes/main.tscn")
	if scene == null:
		printerr("FAIL main scene did not load")
		quit(1)
		return
	_main = scene.instantiate()
	root.add_child(_main)


func _physics_process(_delta: float) -> bool:
	_ticks += 1
	match _ticks:
		2:
			_player = _main.player
			_check(_player != null, "hero spawned")
			_enemy_count = _main.level.enemy_count()
			_check(_enemy_count >= 6, "enemies spawned (%d)" % _enemy_count)
			_check(_main.level.checkpoints.size() == 1, "checkpoint built")
			_check(_main.level.goal != null, "goal gate built")
			_start_x = _player.global_position.x
			_press("move_right", true)
		60:
			_check(_player.global_position.x > _start_x + 40.0, "hero runs right")
			_check(_player.is_on_floor(), "hero stands on the floor")
			_press("move_right", false)
			_press("jump", true)
			_camera_y = _main.camera.global_position.y
		66:
			_press("jump", false)
			_check(not _player.is_on_floor() and _player.velocity.y < 0.0, "hero jumps")
		80:
			var camera_moved := absf(_main.camera.global_position.y - _camera_y)
			_check(camera_moved < 2.0, "camera ignores a jump (moved %.1f)" % camera_moved)
		130:
			_check(_player.is_on_floor(), "hero lands again")
			_press("attack", true)
			_press("move_right", true)
			_mark_x = _player.global_position.x
		134:
			_press("attack", false)
			_check(_player.state == Player.State.ATTACK, "attack starts")
		148:
			_check(_player.state == Player.State.ATTACK, "attack still running")
			_check(_player.global_position.x > _mark_x + 3.0, "hero walks while attacking")
			_press("move_right", false)
		160:
			_press("dash", true)
		163:
			_press("dash", false)
			_check(_player.state == Player.State.DASH, "dash starts")
		200:
			_player.take_damage(2, _player.global_position + Vector2(8, 0))
		202:
			_check(_player.health == Player.MAX_HEALTH - 2, "damage applied")
			_check(_player.state == Player.State.HURT, "hurt state")
		260:
			_press("heal", true)
		263:
			_press("heal", false)
			_check(_player.state == Player.State.HEAL, "heal starts")
		330:
			_check(_player.health == Player.MAX_HEALTH, "heal restores health")
			_check(_player.heal_charges == Player.MAX_HEAL_CHARGES - 1, "heal uses a charge")
			_player.take_hazard_damage(1)
		332:
			_check(_player.health == Player.MAX_HEALTH - 1, "spikes hurt")
		400:
			_player.take_damage(99, _player.global_position)
		402:
			_check(_player.state == Player.State.DEAD, "hero dies")
		490:
			_check(_player.state != Player.State.DEAD, "hero respawns")
			_check(_player.health == Player.MAX_HEALTH, "respawn restores health")
			_check(_main.level.enemy_count() == _enemy_count, "enemies respawn")
			# Drop the hero from high up so he has time for two dashes.
			_player.global_position.y -= 160.0
			_player.velocity = Vector2.ZERO
		500:
			_press("dash", true)
		502:
			_press("dash", false)
			_check(_player.state == Player.State.DASH, "air dash starts")
		526:
			_press("dash", true)
		529:
			_press("dash", false)
			_check(
				not _player.is_on_floor() and _player.state != Player.State.DASH,
				"only one dash per airtime"
			)
		LAST_TICK:
			_report()
			return true
	if _ticks > LAST_TICK:
		# A script error inside the last step kept _report from running.
		printerr("FAIL the test did not finish")
		quit(1)
		return true
	return false


func _press(action: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	Input.parse_input_event(event)


func _check(ok: bool, label: String) -> void:
	_checks += 1
	if ok:
		print("  ok   ", label)
	else:
		_failures += 1
		printerr("  FAIL ", label)


func _report() -> void:
	if _checks != EXPECTED_CHECKS:
		_failures += 1
		printerr("  FAIL expected %d checks, ran %d" % [EXPECTED_CHECKS, _checks])
	print("%d checks, %d failed" % [_checks, _failures])
	quit(1 if _failures > 0 else 0)
