extends SceneTree

## Headless smoke test: boots the main scene, drives the hero with fake
## input for a few seconds of physics ticks and checks that the basics work.
##
##   godot --headless --path . --script tests/smoke_test.gd
##
## Exits with code 1 when a check fails.

var _ticks := 0
var _main: Node
var _player: Player
var _start_x := 0.0
var _enemy_count := 0
var _failures := 0
var _checks := 0


func _initialize() -> void:
	var scene: PackedScene = load("res://scenes/main.tscn")
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
		66:
			_press("jump", false)
			_check(not _player.is_on_floor() and _player.velocity.y < 0.0, "hero jumps")
		130:
			_check(_player.is_on_floor(), "hero lands again")
			_press("attack", true)
		134:
			_press("attack", false)
			_check(_player.state == Player.State.ATTACK, "attack starts")
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
			_report()
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
	print("%d checks, %d failed" % [_checks, _failures])
	quit(1 if _failures > 0 else 0)
