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
const EXPECTED_CHECKS := 315
const LAST_TICK := 1270
## A spot on the start room's floor with clear air above it, for the
## jumps and the air dashes (the start itself has ledges overhead).
const AIR_START := Vector2(1064.0, 240.0)

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
var _mereth: Npc
var _token: LoreItem
## The world booted again at the end, for the hub region, and the
## blacksmith in it.
var _hub: Node
var _pell: Npc
## The tint of Paradise, which crossing into Ruined Earth changes.
var _paradise_tint := Color.WHITE
## The world booted once more for the seals and the boss fight.
var _world: Node
var _wings_seal: DoorLock
var _boss: Boss
var _boss_start := Vector2.ZERO

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
			_check(Audio.ambient_name == "heaven", "Paradise starts the heaven ambient loop")
			# The world: built from the map tool's export with TileMapLayers,
			# collision from the terrain kinds, the markers, the lights and
			# the objects of the placements sidecar.
			var level: LevelBuilder = _main.level
			_check(level.world != null, "world export read")
			_check(level.get_node("Tiles").get_child_count() == 4, "four tile layers built")
			var terrain := level.get_node_or_null("Tiles/Terrain")
			_check(terrain is TileMapLayer, "terrain is a TileMapLayer")
			_check(
				terrain != null and terrain.get_used_cells().size() == 120990,
				"every terrain cell is set"
			)
			_check(level.get_node("Solids").get_child_count() > 0, "solid runs have collision")
			_check(level.get_node("Ledges").get_child_count() > 0, "one-way ledges built")
			_check(level.get_node("Hazards").get_child_count() > 0, "hazards built")
			_check(level.get_node("Lights").get_child_count() == 853, "every light placed")
			_check(
				level.region_at(_player.global_position.y).get("name") == "Paradise",
				"the hero starts in Paradise"
			)
			_check(
				_player.global_position.is_equal_approx(Vector2(1192.0, 240.0)),
				"the hero stands on the start marker"
			)
			_check(
				level.ambient != null and level.ambient.color != Color.WHITE,
				"the region's ambient tint is applied"
			)
			_enemy_count = level.enemy_count()
			_check(
				_enemy_count >= 6 + 11 + 130,
				"enemies below the hub and placeholder bosses spawned (%d)" % _enemy_count
			)
			var brutes := 0
			var ferryman: Npc = null
			for enemy in level.get_node("Enemies").get_children():
				if enemy is Walker and enemy.character == "brute":
					brutes += 1
			for node in level.interactables:
				if node is Npc and node.character == "ferryman":
					ferryman = node
			_check(brutes >= 20, "brutes stand in the deep circles (%d)" % brutes)
			_check(ferryman != null, "the Ferryman waits in Purgatory")
			_check(
				ferryman != null and ferryman.sprite.sprite_frames.has_animation("talk"),
				"the Ferryman has his strips"
			)
			_check(level.checkpoints.size() == 31, "every save marker is a checkpoint")
			_check(level.goal == null, "the world has no goal gate yet")
			_check(level.pickup_count() == 6, "feathers for all six abilities built")
			_check(level.upgrade_count() == 3, "three permanent upgrades built")
			_check(level.power_up_count() == 3, "three power-ups built")
			_start_x = _player.global_position.x
			_start_position = _player.global_position
			# The opening dialogue would freeze the hero at the start; it is
			# tested on its own below, so mark it as seen for the run.
			Story.set_flag("opening_seen")
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
			_player.global_position = AIR_START + Vector2(0.0, -160.0)
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
			_check(_main.level.pickup_count() == 5, "feather is used up")
			# Back to the start room, where there is headroom for two jumps.
			_player.global_position = AIR_START
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
			# Barks: a fallen imp shouts as it winds up, half the time.
			_imp.barks = "fallen"
			for i in 24:
				if not _imp.bark_showing():
					_imp._maybe_bark()
			_check(_imp.bark_showing(), "a fallen imp barks on the wind-up")
			_check(_imp._bark_label.text in Walker.BARKS["fallen"], "the bark is one of its lines")
			var brute: Walker = LevelBuilder.WALKER_SCENE.instantiate()
			brute.character = "brute"
			brute.position = _start_position + Vector2(-200.0, 0.0)
			_main.add_child(brute)
			_check(brute.health == Walker.PROFILES["brute"]["health"], "the brute has its profile")
			_check(brute.sprite.sprite_frames.has_animation("walk"), "the brute has its strips")
			brute.queue_free()
			_imp.take_damage(99, _player.global_position)
			_check(_imp._dead, "imp dies")
			_check(not _imp.bark_showing(), "a dead imp stops shouting")
			_check(Audio.last_played == "enemy_die", "enemy death plays its sound")
			_check(not _imp.health_bar.visible, "health bar hides on death")
		674:
			# Upgrades: the Kept Light on the ledge adds a heart and reads
			# like a relic.
			_player.global_position = _main.level.upgrades[0].global_position
			_player.velocity = Vector2.ZERO
		678:
			_check(_player.max_health == Player.MAX_HEALTH + 1, "Kept Light adds a heart")
			_check(_player.health == _player.max_health, "Kept Light fills the hearts")
			_check(Story.has_flag("upgrade:heart:world_1:0"), "taken upgrade is flagged")
			_check(_main.level.upgrade_count() == 2, "taken upgrade is gone")
			_check(
				_main.dialogue_box.current_id() == "upgrade_heart", "upgrade shows its description"
			)
			_check(_player.talking, "hero stands still to read it")
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
		680:
			_check(not _main.dialogue_box.is_open(), "upgrade box closes")
			# Power-ups: the Lit Blade, then the Halo.
			_player.global_position = _power_up_position(Player.Power.BLADE)
			_player.velocity = Vector2.ZERO
		684:
			_check(_player.has_power(Player.Power.BLADE), "Lit Blade is picked up")
			_check(
				_player.attack_damage() == Player.ATTACK_DAMAGE + 1, "Lit Blade raises the damage"
			)
			_check(_main.level.power_up_count() == 2, "taken power-up is gone")
			_check(_main.hud.status.powers.has(Player.Power.BLADE), "HUD shows the power-up")
			_player.global_position = _power_up_position(Player.Power.HALO)
			_player.velocity = Vector2.ZERO
		688:
			_check(_player.has_power(Player.Power.HALO), "Halo is picked up")
			_mark_x = _player.health
			_player.take_damage(1, _player.global_position + Vector2(8, 0))
			_check(_player.health == int(_mark_x), "Halo turns the hit aside")
			_check(not _player.has_power(Player.Power.HALO), "Halo is spent by the hit")
			_check(_player.state != Player.State.HURT, "a turned hit has no knockback")
		690:
			_player.grant_power(Player.Power.ZEAL)
			_check(
				_player.dash_cooldown() < Player.DASH_COOLDOWN, "Zeal shortens the dash cooldown"
			)
			_check(_player.run_speed() > Player.RUN_SPEED, "Zeal makes the hero faster")
			_player.powers[Player.Power.ZEAL] = 0.01
		692:
			_check(not _player.has_power(Player.Power.ZEAL), "power-ups run out")
			_check(_player.has_power(Player.Power.BLADE), "the others keep running")
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
			_check(_main.level.pickup_count() == 4, "both feathers used up")
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
			# Dialogue: the story objects from the level's sidecar.
			_check(InputMap.has_action("interact"), "interact action exists")
			_check(_main.level.interactables.size() == 38, "thirty-eight story objects built")
			var messenger: Npc = null
			var vaels := 0
			for node in _main.level.interactables:
				if node is Npc and node.npc_name == "Messenger":
					messenger = node
				elif node is Npc and node.npc_name == "Vael the Bargainer":
					vaels += 1
			_check(messenger != null, "Tobiah's messenger stands in Heresy")
			_check(
				messenger != null and messenger.character == "angel_guard",
				"the messenger is a sent-down angel"
			)
			_check(vaels == 2, "Vael waits in Greed and again in Violence")
			_check(_main.level.npc_count() == 12, "twelve NPCs built")
			_check(_main.level.item_count() == 16, "sixteen relics built")
			_check(not _main.dialogue_box.is_open(), "dialogue box starts closed")
			for node in _main.level.interactables:
				if node is Npc and node.npc_name == "Mereth":
					_mereth = node
				elif node is LoreItem and node.item_id == "token":
					_token = node
			_check(_mereth != null and _token != null, "Mereth and the token are placed")
			_player.global_position = _mereth.global_position + Vector2(-20.0, 0.0)
			_player.velocity = Vector2.ZERO
		764:
			_check(_mereth.is_player_near(), "NPC notices the hero standing close")
			_press("interact", true)
		766:
			_press("interact", false)
			_check(_main.dialogue_box.is_open(), "interact opens the dialogue box")
			_check(_main.dialogue_box.current_id() == "mereth_intro", "first talk plays the intro")
			_check(_main.dialogue_box.name_label.text == "Mereth", "box shows the speaker")
			_check(_player.talking, "hero is talking")
			_mark_x = _player.global_position.x
			_press("move_right", true)
		772:
			_press("move_right", false)
			_check(
				is_equal_approx(_player.global_position.x, _mark_x),
				"hero stands still while talking"
			)
			_check(_main.dialogue_box.text_label.visible_characters >= 0, "text is being typed")
			_press("jump", true)
		774:
			_press("jump", false)
			_check(
				_main.dialogue_box.text_label.visible_characters == -1,
				"a press finishes the typing"
			)
			_check(_player.is_on_floor(), "the press did not make the hero jump")
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
		776:
			_check(not _main.dialogue_box.is_open(), "box closes after the last page")
			_check(Story.has_flag("met_mereth"), "finishing the talk sets its flag")
			_check(not _player.talking, "hero is released")
			_press("interact", true)
		778:
			_press("interact", false)
			_check(
				_main.dialogue_box.current_id() == "mereth_again", "second talk plays later lines"
			)
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
			# A relic: examining it shows its description, then it is gone.
			_player.global_position = _token.global_position
			_player.velocity = Vector2.ZERO
		782:
			_press("interact", true)
		784:
			_press("interact", false)
			_check(_main.dialogue_box.current_id() == "item_token", "relic shows its description")
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
		786:
			_check(_main.level.item_count() == 15, "examined relic is gone")
			_check(Story.has_flag("item:token"), "examined relic is flagged")
			# A trigger: walking in plays its dialogue once.
			for node in _main.level.interactables:
				if (
					is_instance_valid(node)
					and node is StoryTrigger
					and node.dialogue_ids == ["gate"]
				):
					_player.global_position = node.global_position
			_player.velocity = Vector2.ZERO
		790:
			_check(_main.dialogue_box.current_id() == "gate", "trigger plays on touch")
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
		792:
			_check(Story.has_flag("reached_gate"), "trigger sets its flag")
			_player.global_position = _start_position
			_player.velocity = Vector2.ZERO
		796:
			_check(not _main.dialogue_box.is_open(), "seen trigger stays quiet")
			_press("pause", true)
		798:
			_press("pause", false)
			_check(get_tree().paused, "pause stops the tree")
			_check(_main.pause_menu.is_open(), "pause menu opens")
			_mark_x = _player.global_position.x
			_press("move_right", true)
		802:
			_press("move_right", false)
			_check(
				is_equal_approx(_player.global_position.x, _mark_x),
				"hero stands still while paused"
			)
			_press("pause", true)
		804:
			_press("pause", false)
			_check(not get_tree().paused, "pause again resumes")
			_check(not _main.pause_menu.is_open(), "pause menu closes")
		806:
			# Saving: rest at the checkpoint, write the slot and read it back.
			_check(root.get_node_or_null("SaveManager") != null, "SaveManager autoload present")
			var checkpoint: Checkpoint = _main.level.checkpoints[0]
			_player.global_position = checkpoint.global_position
			_player.velocity = Vector2.ZERO
		810:
			_check(_main._active_checkpoint == 0, "resting records the checkpoint")
			_slot = SaveManager.SLOT_COUNT - 1
			SaveManager.start_new_game(_slot)
			_check(_main.save_game(), "save slot written")
			_save = SaveManager.read(_slot)
			_check(int(_save.get("checkpoint", -1)) == 0, "save holds the checkpoint")
			_check(_save.get("abilities", []).size() == 2, "save holds both abilities")
			_check(str(_save.get("level", "")) == "world_1", "save holds the level id")
			_check("item:token" in _save.get("flags", []), "save holds the story flags")
			_check(SaveManager.latest_slot() == _slot, "latest slot is the new save")
			# Loading: boot a fresh level from the save in place of this one.
			# Freed at the end of the frame rather than at once: the world has
			# hundreds of bodies and areas the physics step still refers to.
			root.remove_child(_main)
			_main.queue_free()
			Story.reset()
			_check(SaveManager.load_slot(_slot), "save slot loads")
			_loaded = _main_scene.instantiate()
			root.add_child(_loaded)
		814:
			var loaded_player: Player = _loaded.player
			var checkpoint: Checkpoint = _loaded.level.checkpoints[0]
			_check(_loaded.level_id == "world_1", "loaded level is the saved one")
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
			_check(_loaded.level.pickup_count() == 4, "collected feathers stay collected")
			_check(Story.has_flag("met_mereth"), "loaded save restores the story flags")
			_check(_loaded.level.item_count() == 15, "examined relic stays gone")
			_check(_loaded.hud.status.abilities.size() == 2, "loaded HUD shows both abilities")
			_check(_loaded.level.upgrade_count() == 2, "taken upgrade stays taken")
			_check(loaded_player.max_health == Player.MAX_HEALTH + 1, "loaded hero keeps his heart")
			_check(loaded_player.powers.is_empty(), "power-ups are not saved")
			# The registry: one level, which old saves and unknown ids fall
			# back to, and nothing after it yet.
			var levels := root.get_node_or_null("Levels")
			_check(levels != null, "Levels autoload present")
			_check(levels.first_id == "world_1", "the world is the first level")
			_check(levels.next_id("world_1") == "", "nothing follows the world yet")
			_check(levels.resolve("heaven_1") == "world_1", "old level ids fall back to the world")
			_check(
				levels.resolve("res://levels/level_01.txt") == "world_1", "old path saves fall back"
			)
		866:
			SaveManager.delete(_slot)
			SaveManager.current_slot = -1
			root.remove_child(_loaded)
			_loaded.queue_free()
			_title = load("res://scenes/title.tscn").instantiate()
			root.add_child(_title)
		870:
			_check(_title.get_node("Menu").visible, "title screen shows its menu")
			_check(_title.get_node("Menu/Continue").disabled, "nothing to continue without a save")
		874:
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
			_check(
				&"interact" in Settings.REBINDABLE_ACTIONS, "interact is rebindable in the settings"
			)
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
		876:
			_check(_title.get_node("SettingsMenu").visible, "settings screen opens from the title")
			_check(not _title.get_node("Menu").visible, "title menu hides behind settings")
			var settings_menu: SettingsMenu = _title.get_node("SettingsMenu")
			var attack_button: Button = settings_menu._key_buttons[&"attack"]
			_check(attack_button.text == "J / X", "settings show the attack keys")
			settings_menu.device_button.pressed.emit()
			_check(attack_button.text == "X", "device button switches to gamepad names")
		880:
			# The hub region: crossing into Ruined Earth changes the ambient
			# tint and sound, and the survivors stand in it with their unique
			# lines first and then the repeat lines in turn.
			root.remove_child(_title)
			_title.free()
			SaveManager.pending = {"level": "world_1", "flags": []}
			_hub = _main_scene.instantiate()
			root.add_child(_hub)
		884:
			_check(_hub.level_id == "world_1", "world booted from a save")
			_check(
				_hub.sky.texture.gradient.colors[0] == Levels.WORLD_SKY[0], "world sky is painted"
			)
			_paradise_tint = _hub.level.ambient.color
			for node in _hub.level.interactables:
				if node is Npc and node.npc_name == "Old Pell":
					_pell = node
			_check(_pell != null, "Pell is placed")
			_check(_pell.sprite.sprite_frames.has_animation("talk"), "Pell has a talk animation")
			_check(_pell.rotate_ids.size() == 3, "Pell has three repeat lines")
			_check(
				_hub.level.region_at(_pell.global_position.y).get("name") == "Ruined Earth",
				"Pell stands in Ruined Earth"
			)
			_hub.player.global_position = _pell.global_position + Vector2(-20.0, 0.0)
			_hub.player.velocity = Vector2.ZERO
		886:
			_check(
				Audio.ambient_name == "earth", "crossing into Ruined Earth plays its ambient loop"
			)
			_check(
				_hub.level.ambient.color != _paradise_tint,
				"crossing into a region changes the tint"
			)
		888:
			_press("interact", true)
		890:
			_press("interact", false)
			_check(_hub.dialogue_box.current_id() == "pell_intro", "first talk is Pell's intro")
			_check(_pell.sprite.animation == "talk", "NPC plays its talk animation")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
		892:
			_check(_pell.sprite.animation == "idle", "NPC goes back to idle")
			_check(Story.has_flag("seen:pell_intro"), "a finished entry is marked seen")
			_press("interact", true)
		894:
			_press("interact", false)
			_check(
				_hub.dialogue_box.current_id() == "pell_sky", "second talk is the next unique line"
			)
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
		895:
			_press("interact", true)
		896:
			_press("interact", false)
			_check(
				_hub.dialogue_box.current_id() == "pell_forge_first",
				"Pell forges his own iron on the third talk"
			)
			_check(_hub.player.attack_damage() == 1, "the sword starts at one damage")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
			_check(Story.has_flag("sword:1"), "the forging sets its flag")
			_check(_hub.player.attack_damage() == 2, "the forging hardens the sword")
			_check(_hub.hud.message.text == "Sword damage: 2", "the HUD says so")
		897:
			_press("interact", true)
		898:
			_press("interact", false)
			_check(
				_hub.dialogue_box.current_id() == "pell_forge", "unique lines used up, first repeat"
			)
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
		899:
			_press("interact", true)
		900:
			_press("interact", false)
			_check(_hub.dialogue_box.current_id() == "pell_light", "second repeat line")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
		901:
			_press("interact", true)
		902:
			_press("interact", false)
			_check(_hub.dialogue_box.current_id() == "pell_boy", "third repeat line")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
		903:
			_press("interact", true)
		904:
			_press("interact", false)
			_check(_hub.dialogue_box.current_id() == "pell_forge", "repeat lines wrap around")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
			Story.set_flag("item:hook")
		905:
			_press("interact", true)
		906:
			_press("interact", false)
			_check(
				_hub.dialogue_box.current_id() == "pell_hook",
				"a newly unlocked line beats the repeats"
			)
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
			# Church iron from the deep: Pell hammers it in.
			_check(Upgrade.iron_carried() == 0, "no iron carried yet")
			Story.set_flag(Upgrade.flag_for(Player.UpgradeKind.IRON, "world_1", 0))
			_check(Upgrade.iron_carried() == 1, "picked-up iron is carried")
		907:
			_press("interact", true)
		908:
			_press("interact", false)
			_check(_hub.dialogue_box.current_id() == "pell_iron", "Pell takes the church iron")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
			_check(_hub.player.attack_damage() == 3, "the second forging hardens it again")
			_check(Upgrade.iron_carried() == 0, "the forging spends the iron")
			_hub.player.restore_upgrades()
			_check(_hub.player.attack_damage() == 3, "a loaded hero keeps the forged sword")
			# Vael's trade, once the hero knows the name: a memory for the clock key.
			var vael: Npc = null
			for node in _hub.level.interactables:
				if node is Npc and node.npc_name == "Vael the Bargainer" and vael == null:
					vael = node
			_check(vael != null, "Vael is placed")
			_check(vael.rotate_ids.size() == 3, "Vael has three repeat lines")
			Story.set_flag("met_vael")
			Story.set_flag(DialogueData.seen_flag("vael_intro"))
			Story.set_flag("reveal")
			_hub.player.global_position = vael.global_position + Vector2(-20.0, 0.0)
			_hub.player.velocity = Vector2.ZERO
		910:
			_press("interact", true)
		912:
			_press("interact", false)
			_check(_hub.dialogue_box.current_id() == "vael_trade", "Vael offers the trade")
			while _hub.dialogue_box.is_open():
				_hub.dialogue_box.advance()
			_check(Story.has_flag("sold_winter"), "the trade takes the memory")
			_check(Story.has_flag("item:clock_key"), "and gives the clock key")
			# The playtesting arguments: start at a checkpoint with everything.
			var third: Checkpoint = _hub.level.checkpoints[2]
			_hub.apply_dev_args(["--checkpoint=3", "--abilities=all"])
			_check(
				_hub.player.global_position == third.global_position,
				"--checkpoint=3 puts the hero at the third checkpoint"
			)
			_check(third.active, "and makes it the one he comes back to")
			_check(
				_hub.player.abilities.size() == Player.Ability.size(),
				"--abilities=all unlocks every ability"
			)
			_check(_hub.level.pickups.is_empty(), "and takes the feathers out of the world")
			_check(_hub.level.locks.size() < 19, "and opens the ability seals")
			root.remove_child(_hub)
			_hub.queue_free()
			Audio.play_ambient("")
			# Seals and the boss: a fresh world, nothing seen.
			SaveManager.pending = {"level": "world_1", "flags": []}
			_world = _main_scene.instantiate()
			root.add_child(_world)
		918:
			_check(InputMap.has_action("move_down"), "move_down action exists")
			_check(&"move_down" in Settings.REBINDABLE_ACTIONS, "move down is rebindable")
			var level: LevelBuilder = _world.level
			_check(
				level.locks.size() == 19, "a seal on every locked door (%d)" % level.locks.size()
			)
			for seal in level.locks:
				if seal.door_id == 415:
					_wings_seal = seal
			_check(_wings_seal != null, "the door under the Warden is sealed")
			_check(
				_wings_seal != null and _wings_seal.kind == DoorLock.Kind.ABILITY,
				"that seal is an ability lock"
			)
			_check(
				_wings_seal != null and _wings_seal.ability == Player.Ability.WINGS,
				"it wants the wings"
			)
			_check(
				_wings_seal != null and _wings_seal.cells == _shaft_cells(279, 50),
				"the seal fills the shaft under the ledge"
			)
			var boss_seal: DoorLock = null
			for seal in level.locks:
				if seal.door_id == 421:
					boss_seal = seal
			_check(
				boss_seal != null and boss_seal.kind == DoorLock.Kind.BOSS,
				"the door under the Golden Vault is a boss lock"
			)
			_check(
				boss_seal != null and boss_seal.boss_flag == "boss:golden_vault",
				"boss lock reads the boss flag"
			)
			_check(
				boss_seal != null and "Golden Vault" in boss_seal.message(),
				"boss lock names its boss"
			)
			_check(level.bosses().size() == 11, "every boss room holds a boss")
			for boss in level.bosses():
				if boss.boss_id == "seraph_warden":
					_boss = boss
			_check(_boss != null, "the Seraph Warden is a boss")
			_check(Boss.has_profile("the_fury"), "Gorrow has a boss profile")
			_check(
				str(_world.dialogue.entry("the_fury_defeat").get("sets", "")) == "reveal",
				"beating Gorrow sets the reveal"
			)
			var gorrow := 0
			for node in level.interactables:
				if node is Npc and node.npc_name == "Gorrow":
					gorrow += 1
			_check(gorrow == 1, "Gorrow waits in Lust")
			_check(_boss != null and _boss.state == Boss.State.DORMANT, "boss waits in its room")
			_check(
				_boss != null and _boss.arena.has_point(_boss.global_position),
				"boss arena is its room"
			)
			_check(not _world.hud.boss_bar.visible, "boss bar starts hidden")
			# Into the Warden's room, well away from him.
			_world.player.global_position = Vector2(4600.0, 784.0)
			_world.player.velocity = Vector2.ZERO
			_world.player.facing = 1
		922:
			_check(_world._boss == _boss, "walking into the room starts the fight")
			_check(
				_world.dialogue_box.current_id() == "seraph_warden_intro", "the boss intro plays"
			)
			var sealed_doors: Array[int] = []
			for seal in _world._arena_seals:
				sealed_doors.append(seal.door_id)
			sealed_doors.sort()
			# The door down is still under its own seal; the other two get one.
			_check(
				sealed_doors == [218, 219],
				"the room's doors are sealed for the fight (%s)" % str(sealed_doors)
			)
			_check(_world.hud.boss_bar.visible, "boss bar shows")
			_check(_world.hud.boss_name_label.text == "Seraph Warden", "boss bar names the boss")
			_check(_boss.state == Boss.State.DORMANT, "boss waits for the intro")
			_boss_start = _boss.global_position
			while _world.dialogue_box.is_open():
				_world.dialogue_box.advance()
		924:
			_check(_boss.state != Boss.State.DORMANT, "boss wakes after the intro")
		990:
			_check(
				_boss.global_position.distance_to(_boss_start) > 20.0,
				(
					"boss comes at the hero (moved %.0f)"
					% _boss.global_position.distance_to(_boss_start)
				)
			)
			var hits := _boss.max_health - int(ceil(_boss.max_health * Boss.PHASE_TWO_AT))
			_boss.take_damage(hits, _world.player.global_position)
			_check(_boss.health == _boss.max_health - hits, "boss takes damage")
			_check(_boss.in_phase_two(), "half health starts phase two")
			_check(
				_world.hud._boss_fill.size.x <= (Hud.BOSS_BAR_WIDTH - 2.0) / 2.0,
				"boss bar drains with the hits"
			)
		994:
			_boss.take_damage(99, _world.player.global_position)
			_check(_boss._dead, "boss dies")
			_check(Story.has_flag("boss:seraph_warden"), "dead boss sets its flag")
			_check(_world._boss == null, "the fight is over")
			_check(_world._arena_seals.is_empty(), "the room's seals break")
			_check(not _world.hud.boss_bar.visible, "boss bar hides")
			_check(
				_world.dialogue_box.current_id() == "seraph_warden_defeat", "the defeat lines play"
			)
			while _world.dialogue_box.is_open():
				_world.dialogue_box.advance()
		1000:
			_check(Story.has_flag("warden_fallen"), "the defeat lines set their flag")
			_check(_world.level.get_node("Seals").get_child_count() == 19, "no arena seal is left")
			# On the ledge over the sealed shaft, pressing down.
			_world.player.global_position = Vector2(280.0 * 16.0 + 8.0, 49.0 * 16.0)
			_world.player.velocity = Vector2.ZERO
			_press("move_down", true)
		1006:
			_check(_world.player.is_on_floor(), "hero stands on the ledge over the shaft")
			_check(_world.player.global_position.y <= 785.0, "down alone does not drop him")
			_press("jump", true)
		1010:
			_press("jump", false)
			_check(_world.player.global_position.y > 786.0, "down and jump drops through the ledge")
		1030:
			_press("move_down", false)
			_check(
				absf(_world.player.global_position.y - 800.0) < 1.5,
				"the seal of light stops the hero (y %.1f)" % _world.player.global_position.y
			)
			_check(
				_world.hud.message.visible and "Wings" in _world.hud.message.text,
				"touching the seal says what it wants"
			)
			_world.player.unlock(Player.Ability.WINGS)
			_check(_world.level.locks.size() == 18, "learning the ability breaks its seal")
		1032:
			_check(not is_instance_valid(_wings_seal), "the broken seal is gone")
		1056:
			_check(_world.player.global_position.y > 812.0, "hero falls through the opened door")
			# A world booted from a save with the Warden dead has no Warden.
			var flags := Story.to_list()
			flags.append("boss:the_fury")
			root.remove_child(_world)
			_world.queue_free()
			SaveManager.pending = {"level": "world_1", "flags": flags}
			_world = _main_scene.instantiate()
			root.add_child(_world)
		1060:
			_check(_world.level.bosses().size() == 9, "dead bosses stay dead in the save")
			_check(
				_world.level.enemy_count() == _enemy_count - 2,
				"the dead bosses leave no placeholder"
			)
			var gorrow := 0
			for node in _world.level.interactables:
				if node is Npc and node.npc_name == "Gorrow":
					gorrow += 1
			_check(gorrow == 0, "dead Gorrow no longer waits in Lust")
			# Wall Grip: in the air beside the wall at the Warden's room's edge.
			_world.player.unlock(Player.Ability.WALL_GRIP)
			_check(
				Player.Ability.WALL_GRIP in _world.hud.status.abilities, "HUD shows the wall grip"
			)
			_world.player.global_position = Vector2(327.0 * 16.0 + 8.0, 44.0 * 16.0)
			_world.player.velocity = Vector2.ZERO
			_press("move_right", true)
		1070:
			_check(_world.player.is_gripping_wall(), "pushing into a wall grips it")
			_check(
				_world.player.velocity.y <= Player.WALL_SLIDE_SPEED + 1.0,
				"wall grip slows the slide (%.0f)" % _world.player.velocity.y
			)
			_check(not _world.player.is_on_floor(), "hero is still off the ground")
			_press("jump", true)
		1073:
			_press("jump", false)
			_check(
				_world.player.velocity.x < 0.0 and _world.player.velocity.y < 0.0,
				"wall jump springs away from the wall"
			)
			_check(not _world.player.is_gripping_wall(), "the jump lets go of the wall")
		1078:
			# Still holding towards the wall: the spring is not steered back yet.
			_check(
				_world.player.velocity.x < 0.0,
				"holding towards the wall does not pull back at once"
			)
			_press("move_right", false)
		1090:
			# Glide: dropped from high up with no wings, pressing jump on the way down.
			_world.player.unlock(Player.Ability.GLIDE)
			_world.player.global_position = AIR_START + Vector2(0.0, -160.0)
			_world.player.velocity = Vector2.ZERO
		1096:
			_check(_world.player.velocity.y > 0.0, "hero is falling")
			_press("jump", true)
		1116:
			_check(_world.player.is_gliding(), "a press while falling opens the wings")
			_check(
				_world.player.velocity.y <= Player.GLIDE_FALL_SPEED + 1.0,
				"glide slows the fall (%.0f)" % _world.player.velocity.y
			)
			_press("jump", false)
		1126:
			_check(not _world.player.is_gliding(), "letting go closes the wings")
			_check(
				(
					_world.player.velocity.y > Player.GLIDE_FALL_SPEED + 5.0
					or _world.player.is_on_floor()
				),
				"the fall picks up again"
			)
		1140:
			# Holy Light: a bolt at an imp standing in front of the hero.
			_check(InputMap.has_action("cast"), "cast action exists")
			_check(&"cast" in Settings.REBINDABLE_ACTIONS, "cast is rebindable")
			_world.player.unlock(Player.Ability.HOLY_LIGHT)
			_world.player.global_position = _start_position
			_world.player.velocity = Vector2.ZERO
			_world.player.facing = 1
			_imp = LevelBuilder.WALKER_SCENE.instantiate()
			_imp.character = "imp"
			_imp.direction = 1
			_imp.position = _start_position + Vector2(70.0, 0.0)
			_world.add_child(_imp)
			_press("cast", true)
		1143:
			_press("cast", false)
			_check(_world.player.state == Player.State.CAST, "cast starts")
			_check(
				_world.get_tree().get_nodes_in_group("light_bolt").size() == 1,
				"cast throws a bolt of light"
			)
			_mark_x = _imp.health
		1170:
			_check(_imp.health == int(_mark_x) - 1, "the bolt hurts the imp")
			_check(
				_world.get_tree().get_nodes_in_group("light_bolt").is_empty(),
				"the bolt breaks on the hit"
			)
			_imp.queue_free()
			# Smite: down and attack from the air, onto an imp.
			_world.player.unlock(Player.Ability.SMITE)
			_imp = LevelBuilder.WALKER_SCENE.instantiate()
			_imp.character = "imp"
			_imp.direction = 1
			_imp.position = AIR_START + Vector2(30.0, 0.0)
			_world.add_child(_imp)
			_world.player.global_position = AIR_START + Vector2(0.0, -100.0)
			_world.player.velocity = Vector2.ZERO
			_press("move_down", true)
		1174:
			_mark_x = _imp.health
			_press("attack", true)
		1176:
			_press("attack", false)
			_check(_world.player.state == Player.State.SLAM, "down and attack in the air slams")
			_check(
				_world.player.velocity.y >= Player.SLAM_SPEED - 1.0, "the slam falls at full speed"
			)
		1179:
			_check(_world.player.state == Player.State.SLAM, "the slam is still falling")
			_check(
				_world.player.collision_mask & Player.LEDGE_LAYER_BIT == 0,
				"the slam passes through ledges all the way down"
			)
		1210:
			_press("move_down", false)
			_check(_world.player.is_on_floor(), "the slam lands")
			_check(_world.player.state != Player.State.SLAM, "the slam is over")
			_check(
				_imp.health <= int(_mark_x) - Player.SLAM_DAMAGE,
				"the landing hurts the imp beside the hero"
			)
			_check(
				_world.player.collision_mask & Player.LEDGE_LAYER_BIT != 0,
				"ledges are solid again after the slam"
			)
			_check(_world.player.attack_shape.disabled, "the slam's hitbox is off")
			_imp.queue_free()
			# The last camp: a resting demon beside the hero never swings.
			_imp = LevelBuilder.WALKER_SCENE.instantiate()
			_imp.character = "imp"
			_imp.passive = true
			_imp.direction = 1
			_imp.position = _start_position + Vector2(16.0, 0.0)
			_world.add_child(_imp)
			_world.player.global_position = _start_position
			_world.player.velocity = Vector2.ZERO
			_mark_x = _world.player.health
		1220:
			_check(_imp.state == Walker.State.PATROL, "a resting demon never winds up")
			_check(_world.player.health == int(_mark_x), "and does no contact damage")
			_imp.queue_free()
			# The brothers: the last choice in the dialogue, and the ending it leads to.
			var defeat: Dictionary = _world.dialogue.entry("final_boss_the_betrayer_defeat")
			_check(not defeat.is_empty(), "the Lowest has his defeat lines")
			_world.dialogue_box.play(defeat)
			_world.player.talking = true
			while _world.dialogue_box.is_open() and not _world.dialogue_box.has_choice():
				_world.dialogue_box.advance()
			_check(_world.dialogue_box.has_choice(), "the last line offers a choice")
			_world.dialogue_box.advance()
			_press("move_right", true)
		1222:
			_press("move_right", false)
			_check(_world.dialogue_box.selected_choice() == 1, "right moves the cursor to Sit down")
			_world.dialogue_box.advance()
		1224:
			_check(
				_world.dialogue_box.current_id() == "ending_sit", "the choice leads to its ending"
			)
			while _world.dialogue_box.is_open():
				_world.dialogue_box.advance()
			_check(Story.has_flag("ending:sit"), "the ending sets its flag")
			_check(_world.player.frozen, "the hero stands still at the end")
			_check("Press R" in _world.hud.message.text, "the end line shows")
			_check(_world.game_over(), "the game is over")
			_check(not _world._transitioning, "and R is still read for the restart")
			# The clock turned: back up the pipes, where the Gardener waits.
			_check(_gardeners() == 0, "the Gardener is not in the world before the clock")
			_world.dialogue_box.play(_world.dialogue.entry("garden"))
			while _world.dialogue_box.is_open():
				_world.dialogue_box.advance()
			_check(Story.has_flag("clock_turned"), "turning the clock sets its flag")
		1262:
			_check(
				_world.player.global_position.distance_to(_world.level.player_start) < 8.0,
				"the hero is back at the start"
			)
			_check(_gardeners() == 1, "the Gardener stands in Paradise")
			_check(
				DialogueData.next_id(_world.dialogue.entry("gardener_defeat")) == "ending_night",
				"the Gardener's defeat leads to the night"
			)
			root.remove_child(_world)
			_world.queue_free()
			Audio.play_ambient("")
		LAST_TICK:
			_report()
	if _ticks > LAST_TICK:
		# A script error inside the last step kept _report from running.
		printerr("FAIL the test did not finish")
		get_tree().quit(1)


## The four cells of a shaft seal, from its left column.
func _shaft_cells(left: int, row: int) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for i in 4:
		cells.append(Vector2i(left + i, row))
	return cells


## Where the power-up of that kind lies in the level.
func _power_up_position(power: Player.Power) -> Vector2:
	for power_up in _main.level.get_node("PowerUps").get_children():
		if power_up.power == power:
			return power_up.global_position
	return Vector2.ZERO


## How many Gardeners stand in the world right now.
func _gardeners() -> int:
	var count := 0
	for boss in _world.level.bosses():
		if boss.boss_id == "gardener":
			count += 1
	return count


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
