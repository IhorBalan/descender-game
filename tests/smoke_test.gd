extends Node

## Headless smoke test: boots the main scene, drives the hero with fake
## input for a few seconds of physics ticks and checks that the basics work.
##
##   godot --headless --path . res://tests/smoke_test.tscn
##
## It runs as a scene rather than a --script so the autoloads (Audio,
## Feedback, Levels, SaveManager, Settings) exist before the game scripts
## compile. Exits with code 1 when a check fails, when the scene does not
## load, or when a script error stops the checks from running.

## Every check the script makes; fewer at the end means something broke early.
const EXPECTED_CHECKS := 117
const LAST_TICK := 860

var _ticks := 0
## Set once the level is in the tree, where the checks can reach it.
var _started := false
var _main: Node
var _player: Player
var _start_x := 0.0
var _start_position := Vector2.ZERO
## Just left of the wall of light, on the ground, facing it.
var _barrier_approach := Vector2.ZERO
var _barrier_x := 0.0
var _imp: Walker
var _mark_x := 0.0
var _camera_y := 0.0
var _enemy_count := 0
var _failures := 0
var _checks := 0
## The save written at the end of the run and the level booted from it.
var _save: Dictionary = {}
var _loaded: Node
var _title: Node
## The slot the save/load/transition checks use.
var _slot := -1
var _main_scene: PackedScene

@onready var root: Window = get_tree().root


func _ready() -> void:
	# Flyers pick a random hover phase; a fixed seed keeps every run identical.
	seed(1)
	# The pause checks need the test to keep ticking while the tree is paused.
	process_mode = Node.PROCESS_MODE_ALWAYS
	# This node is the current scene; stepping aside lets the game change
	# scenes (level transitions) without freeing the test.
	get_tree().current_scene = null
	_main_scene = load("res://scenes/main.tscn")
	if _main_scene == null:
		printerr("FAIL main scene did not load")
		get_tree().quit(1)
		return
	_main = _main_scene.instantiate()
	# The root is still adding this scene, so the level has to wait a frame.
	root.add_child.call_deferred(_main)


func _physics_process(_delta: float) -> void:
	if not _started:
		_started = _main.is_inside_tree()
		if not _started:
			return
	_ticks += 1
	match _ticks:
		2:
			_player = _main.player
			_check(_player != null, "hero spawned")
			_check(root.get_node_or_null("Audio") != null, "Audio autoload present")
			_check(root.get_node_or_null("Feedback") != null, "Feedback autoload present")
			_check(Audio.ambient_name == "heaven", "level starts its ambient loop")
			_enemy_count = _main.level.enemy_count()
			_check(_enemy_count >= 6, "enemies spawned (%d)" % _enemy_count)
			_check(_main.level.checkpoints.size() == 1, "checkpoint built")
			_check(_main.level.goal != null, "goal gate built")
			_check(_main.level.pickup_count() == 2, "two ability feathers built")
			_start_x = _player.global_position.x
			_start_position = _player.global_position
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
			_check(Audio.last_played == "jump", "jump plays its sound")
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
		144:
			_check(_player.state == Player.State.ATTACK, "attack still running")
			_check(_player.global_position.x > _mark_x + 3.0, "hero walks while attacking")
			_press("move_right", false)
		160:
			_press("dash", true)
		163:
			_press("dash", false)
			_check(_player.state == Player.State.DASH, "dash starts")
			_check(not _player.dash_ready(), "dash goes on cooldown")
		190:
			_check(_player.dash_ready(), "dash comes back")
			_check(_main.hud.status.dash_readiness >= 1.0, "HUD shows the dash ready")
		200:
			_player.take_damage(2, _player.global_position + Vector2(8, 0))
			_check(Audio.last_played == "hurt", "hurt plays its sound")
			_check(_main.camera.trauma > 0.0, "hurt shakes the camera")
			_check(Engine.time_scale < 1.0, "hurt stops time for a moment")
			_check(_player.sprite.material is ShaderMaterial, "hurt flashes the hero")
		202:
			_check(_player.health == Player.MAX_HEALTH - 2, "damage applied")
			_check(_player.state == Player.State.HURT, "hurt state")
		215:
			_check(is_equal_approx(Engine.time_scale, 1.0), "time runs again after the hit-stop")
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
		468:
			_check(_main.fade.cover.modulate.a > 0.5, "screen fades out before the respawn")
		490:
			_check(_player.state != Player.State.DEAD, "hero respawns")
			_check(_player.health == Player.MAX_HEALTH, "respawn restores health")
			_check(_main.level.enemy_count() == _enemy_count, "enemies respawn")
			_check(_main.fade.cover.modulate.a < 1.0, "screen fades back in after the respawn")
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
		530:
			_press("jump", true)
		532:
			_press("jump", false)
		533:
			_check(
				not _player.is_on_floor() and _player.velocity.y > 0.0,
				"no jump in the air without the wings"
			)
		540:
			# Once the buffered press has expired, drop the hero onto the
			# feather, which sits on a platform.
			_player.global_position = _main.level.pickups[0].global_position
			_player.velocity = Vector2.ZERO
		544:
			_check(_player.has_ability(Player.Ability.WINGS), "feather unlocks the wings")
			_check(_main.hud.status.abilities == [Player.Ability.WINGS], "HUD shows the wings")
			_check(_main.level.pickup_count() == 1, "feather is used up")
			# Back to the start, where there is headroom for two jumps.
			_player.global_position = _start_position
			_player.velocity = Vector2.ZERO
		550:
			_check(_player.is_on_floor(), "hero stands at the start again")
			_press("jump", true)
		553:
			_press("jump", false)
		557:
			_check(not _player.is_on_floor(), "hero is in the air for the second jump")
			_press("jump", true)
		559:
			_check(_player.velocity.y < -200.0, "wings give a second jump in the air")
		560:
			_press("jump", false)
		562:
			_press("jump", true)
		568:
			_check(_player.velocity.y > -150.0, "only one extra jump per airtime")
			_press("jump", false)
		580:
			# Enemy attacks: an imp placed just out of its reach, facing away.
			_player.global_position = _start_position
			_player.velocity = Vector2.ZERO
			_imp = LevelBuilder.WALKER_SCENE.instantiate()
			_imp.character = "imp"
			_imp.direction = 1
			_imp.position = _start_position + Vector2(40.0, 0.0)
			_main.add_child(_imp)
		586:
			_check(_imp.state == Walker.State.PATROL, "imp patrols while the hero is far")
			_check(_player.health == Player.MAX_HEALTH, "hero unhurt before the swing")
			# Step the hero into range.
			_player.global_position.x = _imp.global_position.x - 24.0
		590:
			_check(_imp.state == Walker.State.WINDUP, "imp winds up when the hero is close")
			_check(_imp.direction == -1, "imp turns to face the hero")
		620:
			_check(_player.health < Player.MAX_HEALTH, "imp's swing hurts the hero")
		640:
			# Hitting an enemy: sound, flash and sparks.
			var imp_health: int = _imp.health
			_imp.take_damage(1, _player.global_position)
			_check(_imp.health == imp_health - 1, "imp takes damage")
			_check(Audio.last_played == "hit", "hitting an enemy plays its sound")
			_check(_imp.sprite.material is ShaderMaterial, "hit enemy flashes")
			_check(Feedback.spark_count() >= 1, "hit enemy throws sparks")
			_check(_imp.health_bar.visible, "hit enemy shows its health bar")
			# A heal mote dropped onto the hero, who was hurt by the swing.
			var mote: HealMote = load("res://scenes/heal_mote.tscn").instantiate()
			mote.position = _player.global_position + Vector2(0.0, -12.0)
			_main.add_child(mote)
			_mark_x = _player.health
		646:
			_check(_player.health == int(_mark_x) + 1, "heal mote restores a heart")
			_check(
				_main.get_tree().get_nodes_in_group("heal_mote").is_empty(), "heal mote is used up"
			)
		670:
			_check(Feedback.spark_count() == 0, "sparks clean themselves up")
		672:
			_imp.take_damage(99, _player.global_position)
			_check(_imp._dead, "imp dies")
			_check(Audio.last_played == "enemy_die", "enemy death plays its sound")
			_check(not _imp.health_bar.visible, "health bar hides on death")
		695:
			_check(_imp.sprite.modulate.a < 1.0, "dead imp fades out")
		700:
			# Light barrier: the enemies behind it would knock the hero about,
			# so clear them first, then dash at the wall without the ability.
			for enemy in _main.get_tree().get_nodes_in_group("enemy"):
				enemy.queue_free()
			_barrier_x = _main.level.barrier_rect().get_center().x
			var ground_y: float = _main.level.barrier_rect().end.y
			_barrier_approach = Vector2(_barrier_x - 32.0, ground_y)
			_player.global_position = _barrier_approach
			_player.velocity = Vector2.ZERO
			_player.facing = 1
		704:
			_press("dash", true)
		706:
			_press("dash", false)
			_check(_player.state == Player.State.DASH, "dash at the barrier starts")
		722:
			_check(
				_player.global_position.x < _barrier_x - 8.0, "wall of light stops a normal dash"
			)
			_player.global_position = _main.level.pickups[1].global_position
			_player.velocity = Vector2.ZERO
		726:
			_check(
				_player.has_ability(Player.Ability.LIGHT_DASH),
				"second feather unlocks the light dash"
			)
			_check(_main.level.pickup_count() == 0, "both feathers used up")
			_player.global_position = _barrier_approach
			_player.velocity = Vector2.ZERO
			_player.facing = 1
		734:
			_press("dash", true)
		736:
			_press("dash", false)
			_check(_player.state == Player.State.DASH, "light dash starts")
		756:
			_check(
				_player.global_position.x > _barrier_x + 8.0, "light dash passes through the wall"
			)
			_check(
				_player.collision_mask & LightBarrier.LAYER_BIT != 0,
				"wall of light is solid again after the dash"
			)
		760:
			_press("pause", true)
		762:
			_press("pause", false)
			_check(get_tree().paused, "pause stops the tree")
			_check(_main.pause_menu.is_open(), "pause menu opens")
			_mark_x = _player.global_position.x
			_press("move_right", true)
		766:
			_press("move_right", false)
			_check(
				is_equal_approx(_player.global_position.x, _mark_x),
				"hero stands still while paused"
			)
			_press("pause", true)
		768:
			_press("pause", false)
			_check(not get_tree().paused, "pause again resumes")
			_check(not _main.pause_menu.is_open(), "pause menu closes")
		770:
			# Saving: rest at the checkpoint, write the slot and read it back.
			_check(root.get_node_or_null("SaveManager") != null, "SaveManager autoload present")
			var checkpoint: Checkpoint = _main.level.checkpoints[0]
			_player.global_position = checkpoint.global_position
			_player.velocity = Vector2.ZERO
		774:
			_check(_main._active_checkpoint == 0, "resting records the checkpoint")
			_slot = SaveManager.SLOT_COUNT - 1
			SaveManager.start_new_game(_slot)
			_check(_main.save_game(), "save slot written")
			_save = SaveManager.read(_slot)
			_check(int(_save.get("checkpoint", -1)) == 0, "save holds the checkpoint")
			_check(_save.get("abilities", []).size() == 2, "save holds both abilities")
			_check(str(_save.get("level", "")) == "heaven_1", "save holds the level id")
			_check(SaveManager.latest_slot() == _slot, "latest slot is the new save")
			# Loading: boot a fresh level from the save in place of this one.
			root.remove_child(_main)
			_main.free()
			_check(SaveManager.load_slot(_slot), "save slot loads")
			_loaded = _main_scene.instantiate()
			root.add_child(_loaded)
		778:
			var loaded_player: Player = _loaded.player
			var checkpoint: Checkpoint = _loaded.level.checkpoints[0]
			_check(_loaded.level_id == "heaven_1", "loaded level is the saved one")
			_check(
				loaded_player.global_position.distance_to(checkpoint.global_position) < 1.0,
				"loaded hero stands at the checkpoint"
			)
			_check(checkpoint.active, "loaded checkpoint is lit")
			_check(
				(
					loaded_player.has_ability(Player.Ability.WINGS)
					and loaded_player.has_ability(Player.Ability.LIGHT_DASH)
				),
				"loaded hero keeps both abilities"
			)
			_check(_loaded.level.pickup_count() == 0, "collected feathers stay collected")
			_check(_loaded.hud.status.abilities.size() == 2, "loaded HUD shows both abilities")
			# Level transition: walk into the goal gate.
			var levels := root.get_node_or_null("Levels")
			_check(levels != null, "Levels autoload present")
			_check(levels.next_id("heaven_1") == "heaven_2", "heaven_2 follows heaven_1")
			_check(
				levels.resolve("res://levels/level_01.txt") == "heaven_1", "old path saves resolve"
			)
			loaded_player.global_position = _loaded.level.goal.global_position
			loaded_player.velocity = Vector2.ZERO
		782:
			_check(_loaded.player.frozen, "goal gate freezes the hero")
			_check(_loaded._transitioning, "goal gate starts the transition")
		830:
			# The fade is over and the next level has booted as the current scene.
			_check(_loaded.fade.is_covered(), "screen faded to black")
			_save = SaveManager.read(_slot)
			_check(str(_save.get("level", "")) == "heaven_2", "transition saves the next level")
			_check(int(_save.get("checkpoint", 0)) == -1, "next level starts with no checkpoint")
			var next: Node = get_tree().current_scene
			_check(next != null and next != _loaded, "next level scene booted")
			if next:
				_check(next.level_id == "heaven_2", "next level is heaven_2")
				_check(
					next.player.has_ability(Player.Ability.LIGHT_DASH),
					"abilities carry over to the next level"
				)
				_check(
					next.player.global_position.distance_to(next.level.player_start) < 1.0,
					"hero starts the next level at its start"
				)
				get_tree().current_scene = null
				root.remove_child(next)
				next.free()
			SaveManager.delete(_slot)
			SaveManager.current_slot = -1
			root.remove_child(_loaded)
			_loaded.free()
			_title = load("res://scenes/title.tscn").instantiate()
			root.add_child(_title)
		834:
			_check(_title.get_node("Menu").visible, "title screen shows its menu")
			_check(_title.get_node("Menu/Continue").disabled, "nothing to continue without a save")
		838:
			# Settings: rebinding changes the InputMap and is undone by reset.
			_check(root.get_node_or_null("Settings") != null, "Settings autoload present")
			var before: Array = Settings.action_keys(&"heal")
			Settings.bind_key(&"heal", KEY_G)
			_check(Settings.action_keys(&"heal") == [KEY_G], "key rebinds to G")
			var g := InputEventKey.new()
			g.physical_keycode = KEY_G
			_check(InputMap.action_has_event(&"heal", g), "InputMap takes the new key")
			Settings.reset_keys()
			_check(Settings.action_keys(&"heal") == before, "reset restores the default key")
			# Gamepad: Y (heal) moves to attack, so heal loses it; reset puts it back.
			var pad_before: Array = Settings.action_pad_buttons(&"heal")
			Settings.bind_pad_button(&"attack", JOY_BUTTON_Y)
			_check(
				Settings.action_pad_buttons(&"attack") == [JOY_BUTTON_Y], "pad button rebinds to Y"
			)
			_check(JOY_BUTTON_Y not in Settings.action_pad_buttons(&"heal"), "Y leaves heal")
			var y := InputEventJoypadButton.new()
			y.button_index = JOY_BUTTON_Y
			_check(InputMap.action_has_event(&"attack", y), "InputMap takes the pad button")
			Settings.reset_keys()
			_check(
				Settings.action_pad_buttons(&"heal") == pad_before, "reset restores the pad button"
			)
			_title.get_node("Menu/Settings").pressed.emit()
		840:
			_check(_title.get_node("SettingsMenu").visible, "settings screen opens from the title")
			_check(not _title.get_node("Menu").visible, "title menu hides behind settings")
			var settings_menu: SettingsMenu = _title.get_node("SettingsMenu")
			var attack_button: Button = settings_menu._key_buttons[&"attack"]
			_check(attack_button.text == "J / X", "settings show the attack keys")
			settings_menu.device_button.pressed.emit()
			_check(attack_button.text == "X", "device button switches to gamepad names")
		LAST_TICK:
			_report()
	if _ticks > LAST_TICK:
		# A script error inside the last step kept _report from running.
		printerr("FAIL the test did not finish")
		get_tree().quit(1)


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
	get_tree().quit(1 if _failures > 0 else 0)
