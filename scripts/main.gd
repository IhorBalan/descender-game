extends Node2D

## Boots a level, spawns the hero and wires up the HUD and camera, applies
## the save the title screen chose and writes it back at every checkpoint.
## Reaching the goal gate fades out, saves and boots the next level.
##
## Which level: the save's, else the `--level=<id>` user argument
## (`godot --path . -- --level=world_1`), else the first in Levels. Two
## more user arguments help playtesting: `--checkpoint=<n>` starts the hero
## rested at the level's nth checkpoint and `--abilities=all` unlocks every
## ability (see apply_dev_args).

const PLAYER_SCENE := preload("res://scenes/player.tscn")
## A dialogue entry that sets one of these ends the game (see _end_game).
const ENDING_FLAG_PREFIX := "ending:"
const ENDINGS := {
	"ending:carry": "The end: the Low Gate, from below.",
	"ending:sit": "The end: the floor, counting.",
	"ending:clock": "The end: night in the Garden.",
}
const CONTROLS_HINT := "A/D move   Space jump   J attack   K dash   L cast   H heal   E talk"
const DEATH_FADE_TIME := 0.4
## How far the world's ambient tint is pulled towards white, so the deep
## regions stay readable away from their lights.
const AMBIENT_LIFT := 0.3
const ABILITY_MESSAGES := {
	Player.Ability.WINGS: "Wings: press jump again in the air",
	Player.Ability.LIGHT_DASH: "Light dash: dash further, and through walls of light",
	Player.Ability.WALL_GRIP: "Wall grip: hold towards a wall to cling, jump to spring off",
	Player.Ability.GLIDE: "Glide: press and hold jump while falling to float down",
	Player.Ability.HOLY_LIGHT: "Holy Light: press cast to throw a bolt of light",
	Player.Ability.SMITE: "Smite: down and attack in the air to slam the ground",
}

var player: Player
## The id of the level being played (see Levels).
var level_id := ""
## The level's dialogue, or null when it has no dialogue file.
var dialogue: DialogueData
## Seconds played in this save, carried over from the loaded file.
var play_time := 0.0
## The parallax layers of the region the hero is in (see Backdrop).
var backdrop: Backdrop
## The drifting particles of the region (see RegionFx).
var region_fx: RegionFx
## Index into level.checkpoints of the one the hero last rested at.
var _active_checkpoint := -1
## Set once the goal is reached: the level is fading out, so pausing and
## restarting are over for this scene.
var _transitioning := false
## One of the endings has played: the hero stands still and nothing talks,
## but R still restarts from the last checkpoint.
var _ended := false
## The NPC, relic or trigger whose dialogue is open.
var _talking_to: Interactable
## The region of the world the hero is in (see WorldMap).
var _region: Dictionary = {}
## The rect of the room the hero is in, so the room is only looked up
## again once he leaves it (see MapScreen for what a found room is).
var _room_rect := Rect2()
## A trader's repeat line is playing: the wares open when it closes.
var _shop_pending := false
## The boss being fought, the seals on its room, and whether the fight is
## waiting for the intro dialogue to close.
var _boss: Boss
var _arena_seals: Array[DoorLock] = []
var _fight_pending := false

@onready var sky: TextureRect = $Background/Sky
@onready var level: LevelBuilder = $Level
@onready var camera: GameCamera = $Camera2D
@onready var hud: Hud = $HUD
@onready var fade: ScreenFade = $ScreenFade
@onready var pause_menu: PauseMenu = $PauseMenu
@onready var dialogue_box: DialogueBox = $DialogueBox
@onready var shop_menu: ShopMenu = $ShopMenu
@onready var camp_menu: CheckpointMenu = $CampMenu


func _ready() -> void:
	# A live map sync still writing the world files (see LiveMap) finishes first.
	await LiveMap.wait()
	level_id = _pick_level()
	# Story flags come first: the level leaves out relics the save already has.
	if SaveManager.pending.is_empty():
		Story.reset()
		Inventory.reset()
		Skills.reset()
	else:
		Story.from_list(SaveManager.pending.get("flags", []))
		Inventory.from_dict(SaveManager.pending.get("inventory", {}))
		Skills.from_dict(SaveManager.pending.get("skills", {}))
	level.level_id = level_id
	_paint_sky(Levels.sky(level_id))
	level.load_from_file(Levels.path(level_id))
	# Painted parallax layers sit behind the level, in front of the sky.
	backdrop = Backdrop.new()
	backdrop.name = "Backdrop"
	add_child(backdrop)
	move_child(backdrop, level.get_index())
	region_fx = RegionFx.new()
	region_fx.name = "RegionFx"
	add_child(region_fx)
	if not level.dialogue_path.is_empty():
		dialogue = DialogueData.new(level.dialogue_path)
	for interactable in level.interactables:
		interactable.interacted.connect(_on_interacted)
	dialogue_box.finished.connect(_on_dialogue_finished)

	player = PLAYER_SCENE.instantiate()
	player.position = level.player_start
	player.death_y = level.size_px.y + 64.0
	player.health_changed.connect(hud.set_health)
	player.heal_charges_changed.connect(hud.set_charges)
	player.died.connect(_on_player_died)
	player.respawned.connect(_on_player_respawned)
	add_child(player)

	for checkpoint in level.checkpoints:
		checkpoint.rested.connect(_on_checkpoint_rested)
		checkpoint.camp_requested.connect(_on_camp_requested)
	if level.goal:
		level.goal.reached.connect(_on_goal_reached)
	# Applied before the ability signal is wired, so loaded powers are
	# restored quietly instead of announced again.
	var from_save := not SaveManager.pending.is_empty()
	if from_save:
		_apply_save(SaveManager.pending)
		SaveManager.pending = {}
	# Hearts and healing from the upgrades in the save, in every level.
	player.restore_upgrades()
	# The playtesting arguments only shape a game started without a save.
	if not from_save:
		apply_dev_args(OS.get_cmdline_user_args())
	player.ability_unlocked.connect(_on_ability_unlocked)
	player.upgrade_collected.connect(_on_upgrade_collected)
	player.power_changed.connect(_on_power_changed)
	player.power_granted.connect(_on_power_granted)
	player.purse_changed.connect(hud.set_purse)
	player.loot_collected.connect(_on_loot_collected)
	player.xp_changed.connect(hud.set_progress)
	player.leveled_up.connect(_on_leveled_up)
	hud.set_purse()
	hud.set_progress()
	hud.player = player
	hud.set_abilities(player.abilities)
	# Seals the hero may already pass (a loaded save's abilities) go quietly.
	level.open_locks(player, true)
	for seal in level.locks:
		seal.touched.connect(_on_lock_touched)
	level.spawn_enemies()
	_wire_bosses()
	level.spawn_power_ups()
	pause_menu.save_requested.connect(_on_save_requested)
	pause_menu.restart_requested.connect(restart_from_checkpoint)
	camp_menu.save_requested.connect(_on_camp_save_requested)
	camp_menu.skill_learned.connect(hud.set_progress)

	camera.target = player
	camera.set_limits(level.size_px)
	camera.snap_to_target()

	fade.cover_now()
	fade.fade_in()
	Audio.play_ambient(Levels.ambient(level_id))
	hud.show_message(Levels.display_name(level_id), 2.5)
	_enter_region(level.region_at(player.global_position.y), false)
	if level_id == Levels.first_id:
		hud.queue_message(CONTROLS_HINT, 6.0)


## Recolours the background gradient for the level.
func _paint_sky(colors: Array[Color]) -> void:
	var texture := sky.texture as GradientTexture2D
	if texture == null or colors.size() < 3:
		return
	# The gradient is shared by every instance of the scene, so it is
	# copied before being changed.
	texture = texture.duplicate(true)
	texture.gradient.colors = PackedColorArray(colors)
	sky.texture = texture


## The level the save asks for, else the one named on the command line,
## else the first.
func _pick_level() -> String:
	if SaveManager.pending.has("level"):
		return Levels.resolve(str(SaveManager.pending["level"]))
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--level="):
			var id := arg.trim_prefix("--level=")
			if Levels.has(id):
				return id
			push_warning("Unknown level '%s', starting from the first" % id)
	return Levels.first_id


func _process(delta: float) -> void:
	play_time += delta


## The hero moves in the physics step, so the region and the room he is
## in are read there.
func _physics_process(_delta: float) -> void:
	if is_instance_valid(player):
		var region := level.region_at(player.global_position.y)
		if region != _region:
			_enter_region(region, true)
		_track_room(player.global_position)


## The first time the hero stands in a room of the export it is found:
## its flag goes into the story, so the map shows it and the save keeps it.
func _track_room(at: Vector2) -> void:
	if _room_rect.has_point(at):
		return
	var room_id := level.room_at(at)
	if room_id < 0:
		_room_rect = Rect2()
		return
	_room_rect = level.room_rect(level.world, room_id)
	Story.set_flag(MapScreen.room_flag(room_id))


## Crossing into a region of the world: its ambient tint and sound, and its
## name on the message line when `announce` is set.
func _enter_region(region: Dictionary, announce: bool) -> void:
	_region = region
	if region.is_empty():
		return
	var color := Color.from_string(str(region.get("ambient", "#ffffff")), Color.WHITE)
	level.set_ambient(color.lerp(Color.WHITE, AMBIENT_LIFT))
	if backdrop and level.world:
		backdrop.show_region(region, level.world.tile)
	if region_fx and level.world:
		region_fx.show_region(region, level.world.tile)
	var name := str(region.get("name", ""))
	Audio.play_ambient("heaven" if name == "Paradise" else "earth")
	# Each region has its own loop (see tools/gen_music.py); a fight keeps
	# its own until it is over.
	if _boss == null:
		Audio.play_music(EdgeDecor.region_key(name))
	if announce:
		var subtitle := str(region.get("subtitle", ""))
		hud.queue_message(name if subtitle.is_empty() else "%s: %s" % [name, subtitle], 2.5)


func _unhandled_input(event: InputEvent) -> void:
	if _transitioning:
		return
	if event.is_action_pressed("pause"):
		if shop_menu.is_open() or camp_menu.is_open():
			return
		Audio.play("pause")
		pause_menu.open(pause_status(), player, level)
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("restart"):
		restart_from_checkpoint()


## Restarts from the last checkpoint with the progress so far, so a
## reload never writes a fresh run over the slot.
func restart_from_checkpoint() -> void:
	SaveManager.pending = capture_save()
	get_tree().reload_current_scene()


## The line under the pause menu's heading: the slot and the time played.
func pause_status() -> String:
	var seconds := int(play_time)
	var played := "Played %d:%02d" % [seconds / 60, seconds % 60]
	if SaveManager.current_slot < 0:
		return played
	return (
		"Slot %d      %s      %s"
		% [SaveManager.current_slot + 1, SaveManager.difficulty_name(), played]
	)


# --- Saving -------------------------------------------------------------


## Puts the hero at the saved checkpoint, as if he had just rested there,
## with the saved abilities (their feathers gone from the level).
func _apply_save(data: Dictionary) -> void:
	play_time = float(data.get("play_time", 0.0))
	SaveManager.difficulty = SaveManager.difficulty_of(data)
	var abilities: Array[Player.Ability] = []
	for value in data.get("abilities", []):
		var ability := int(value)
		if ability in Player.Ability.values() and ability not in abilities:
			abilities.append(ability as Player.Ability)
	for ability in abilities:
		player.unlock(ability)
	level.remove_pickups(abilities)
	_rest_at_checkpoint(int(data.get("checkpoint", -1)))


## Puts the hero, rested, at one of the level's checkpoints (by index in
## `level.checkpoints`) and makes it the one he comes back to.
func _rest_at_checkpoint(index: int) -> void:
	if index < 0 or index >= level.checkpoints.size():
		return
	_active_checkpoint = index
	var checkpoint := level.checkpoints[index]
	checkpoint.set_active(true)
	player.global_position = checkpoint.global_position
	player.rest_at(checkpoint.global_position)


## The playtesting arguments after `--` on the command line, for a game
## started without a save: `--checkpoint=<n>` (counted from 1, as the HUD
## names them) and `--abilities=all`. Unknown values warn and do nothing.
func apply_dev_args(args: Array) -> void:
	for arg in args:
		var text := str(arg)
		if text.begins_with("--checkpoint="):
			var number := text.trim_prefix("--checkpoint=")
			var count := level.checkpoints.size()
			if number.is_valid_int() and int(number) >= 1 and int(number) <= count:
				_rest_at_checkpoint(int(number) - 1)
			else:
				push_warning("No checkpoint %s in %s (it has %d)" % [number, level_id, count])
		elif text == "--abilities=all":
			var all: Array[Player.Ability] = []
			for ability: Player.Ability in Player.Ability.values():
				player.unlock(ability)
				all.append(ability)
			level.remove_pickups(all)
			level.open_locks(player, true)
			hud.set_abilities(player.abilities)


## Everything a save needs to put the hero back where he was.
func capture_save() -> Dictionary:
	var abilities: Array[int] = []
	for ability in player.abilities:
		abilities.append(ability)
	return {
		"version": SaveManager.VERSION,
		"level": level_id,
		"checkpoint": _active_checkpoint,
		"abilities": abilities,
		"flags": Story.to_list(),
		"inventory": Inventory.to_dict(),
		"skills": Skills.to_dict(),
		"difficulty": SaveManager.difficulty,
		"play_time": play_time,
		"saved_at": Time.get_unix_time_from_system(),
	}


## Writes the current progress to the slot the game was started from.
## Returns false when there is no slot (the level was run directly).
func save_game() -> bool:
	if SaveManager.current_slot < 0:
		return false
	return SaveManager.write(SaveManager.current_slot, capture_save())


## Saving from the pause menu records the last checkpoint, not the spot the
## hero is standing on; the button says where the save will resume.
func _on_save_requested() -> void:
	if save_game():
		var place := "the start"
		if _active_checkpoint >= 0:
			place = "checkpoint %d" % (_active_checkpoint + 1)
		pause_menu.show_saved(place)


## Dying: the message shows while the hero falls, then the screen goes
## black just before he comes back, so the respawn is never a visible snap.
func _on_player_died() -> void:
	hud.show_message("The angels' system pulls you back...", 1.2)
	await get_tree().create_timer(Player.DEATH_TIME - DEATH_FADE_TIME - 0.05).timeout
	if player.state == Player.State.DEAD:
		fade.fade_out(DEATH_FADE_TIME)


func _on_player_respawned() -> void:
	# The signal fires from the hero's physics tick; rebuild the enemies once
	# the physics step is over.
	_end_fight()
	_respawn_enemies.call_deferred()
	_respawn_power_ups.call_deferred()
	_clear_motes()
	camera.snap_to_target()
	fade.fade_in()


func _respawn_enemies() -> void:
	level.spawn_enemies()
	_wire_bosses()


## Heal motes, power-ups and loot left by enemies go with the enemies when
## the fight resets (a boss's unique loot stays where it fell).
func _clear_motes() -> void:
	for mote in get_tree().get_nodes_in_group("heal_mote"):
		mote.queue_free()
	for power_up in get_tree().get_nodes_in_group("dropped_power"):
		power_up.queue_free()
	for drop in get_tree().get_nodes_in_group("loot"):
		drop.queue_free()


func _on_checkpoint_rested(checkpoint: Checkpoint) -> void:
	_active_checkpoint = level.checkpoints.find(checkpoint)
	var text := "Checkpoint: health and healing restored"
	if save_game():
		text += "   Game saved"
	hud.show_message(text, 2.5)
	if Skills.points_left() > 0:
		hud.queue_message("A skill point waits: press %s to make camp" % _interact_key(), 3.0)


## The hero pressed the talk key at a checkpoint: the camp opens, with the
## pack, the skills and the save (see CheckpointMenu). Not over a dialogue
## or a fight, and never for a dead hero.
func _on_camp_requested(checkpoint: Checkpoint) -> void:
	if (
		dialogue_box.is_open()
		or shop_menu.is_open()
		or camp_menu.is_open()
		or pause_menu.is_open()
		or _transitioning
		or _ended
		or _boss != null
		or player.state == Player.State.DEAD
	):
		return
	# Camp rests him again, so the pack and the skills are managed rested.
	player.rest_at(checkpoint.global_position)
	camp_menu.open(player, level.checkpoints.find(checkpoint), level.checkpoints.size())
	get_viewport().set_input_as_handled()


## Saving from the camp records this checkpoint, where the hero stands.
func _on_camp_save_requested() -> void:
	camp_menu.show_saved(save_game())


## A level made: the HUD says so, and that a point waits for a camp.
func _on_leveled_up(level_reached: int) -> void:
	hud.show_message("Level %d: a skill point to spend at a checkpoint camp" % level_reached, 3.5)


## The key bound to interact, for the messages.
func _interact_key() -> String:
	var settings := get_node_or_null("/root/Settings")
	return "E" if settings == null else str(settings.key_names(&"interact"))


## The power-ups come back with the enemies.
func _respawn_power_ups() -> void:
	level.spawn_power_ups()


## A permanent upgrade reads like a relic: its lines open in the text box
## and the hero stands still until they are read.
func _on_upgrade_collected(kind: Player.UpgradeKind) -> void:
	var entry := Upgrade.description(kind, player)
	if dialogue_box.is_open() or _transitioning:
		hud.show_message(str(entry["lines"][1]["text"]), 3.5)
		return
	player.talking = true
	hud.hide_message()
	dialogue_box.play(entry)


## An item picked up off the ground reads like a relic: a boss's unique
## loot opens the text box with its lines, a consumable only says so.
func _on_loot_collected(item_id: String) -> void:
	var name := Items.display_name(item_id)
	if not Items.is_unique(item_id) or dialogue_box.is_open() or _transitioning:
		hud.show_message("Taken: %s   (in the pack)" % name, 3.0)
		return
	player.talking = true
	hud.hide_message()
	var what := Items.summary(item_id)
	(
		dialogue_box
		. play(
			{
				"id": "loot_%s" % item_id,
				"lines":
				[
					{"who": "", "text": Items.description(item_id)},
					{
						"who": "",
						"text": "%s: %s. Wear it from the pack, in the pause menu." % [name, what]
					},
				],
			}
		)
	)


func _on_power_granted(power: Player.Power) -> void:
	hud.show_message(PowerUp.MESSAGES.get(power, "Power-up"), 3.0)


func _on_power_changed() -> void:
	hud.set_powers(player.powers)


func _on_ability_unlocked(ability: Player.Ability) -> void:
	hud.show_message(ABILITY_MESSAGES.get(ability, "New ability unlocked"), 3.5)
	hud.set_abilities(player.abilities)
	level.open_locks(player)


func _on_lock_touched(seal: DoorLock) -> void:
	if not dialogue_box.is_open():
		hud.show_message(seal.message(), 2.5)


# --- Bosses --------------------------------------------------------------


func _wire_bosses() -> void:
	for boss in level.bosses():
		if not boss.engaged.is_connected(_on_boss_engaged):
			boss.engaged.connect(_on_boss_engaged)
			boss.health_changed.connect(hud.set_boss_health)
			boss.defeated.connect(_on_boss_defeated)


## The hero walked into a boss's room: the doors seal, the bar shows, and
## the intro plays before the boss moves.
func _on_boss_engaged(boss: Boss) -> void:
	if _boss != null or player.state == Player.State.DEAD:
		return
	_boss = boss
	_arena_seals = level.seal_room(level.room_at(boss.global_position))
	hud.show_boss(str(boss.profile["name"]), boss.health, boss.max_health)
	Audio.play("boss_engage")
	Audio.play_music("boss")
	var intro := _boss_entry(boss, "intro")
	if intro.is_empty():
		boss.start_fight()
	else:
		_fight_pending = true
		_play_dialogue(intro, null)


func _on_boss_defeated(boss: Boss) -> void:
	Story.set_flag(Boss.flag_for(boss.boss_id))
	Audio.play("boss_defeat")
	_end_fight()
	level.open_locks(player)
	var defeat := _boss_entry(boss, "defeat")
	if not defeat.is_empty():
		_play_dialogue(defeat, null)
	if save_game():
		hud.queue_message("Game saved", 2.0)


## Drops the seals and the bar, whether the fight was won or the hero died.
func _end_fight() -> void:
	if _boss == null:
		return
	level.unseal(_arena_seals)
	_arena_seals = []
	hud.hide_boss()
	_boss = null
	_fight_pending = false
	Audio.play_music(EdgeDecor.region_key(str(_region.get("name", ""))))


## The boss's intro or defeat lines (`<id>_intro`, `<id>_defeat`), if the
## dialogue file has them and they may play.
func _boss_entry(boss: Boss, part: String) -> Dictionary:
	if dialogue == null:
		return {}
	return dialogue.pick(["%s_%s" % [boss.boss_id, part]])


func _on_goal_reached() -> void:
	player.freeze()
	Audio.play("level_complete")
	var next := Levels.next_id(level_id)
	if next.is_empty():
		hud.show_message("You have reached the end of the prototype.   Press R to restart", -1.0)
		return
	_transitioning = true
	hud.show_message("Level complete", -1.0)
	await fade.fade_out()
	_advance_to(next)


## Boots the next level with the progress so far: same abilities and play
## time, no checkpoint yet. Saves first, so Continue resumes there.
func _advance_to(next_id: String) -> void:
	var data := capture_save()
	data["level"] = next_id
	data["checkpoint"] = -1
	if SaveManager.current_slot >= 0:
		SaveManager.write(SaveManager.current_slot, data)
	SaveManager.pending = data
	get_tree().change_scene_to_file(scene_file_path)


# --- Dialogue ------------------------------------------------------------


## An NPC, relic or trigger asked for its dialogue: the first entry whose
## flags match plays, and the hero stands still until it is read.
func _on_interacted(source: Interactable) -> void:
	if (
		dialogue == null
		or dialogue_box.is_open()
		or _transitioning
		or _ended
		or player.state == Player.State.DEAD
	):
		return
	var entry := dialogue.pick(source.dialogue_ids)
	var trades := source is Npc and not (source as Npc).shop.is_empty()
	if entry.is_empty():
		entry = dialogue.pick(source.rotation_ids())
		# A trader whose unique lines are spent shows the wares after a word.
		_shop_pending = trades
	if entry.is_empty():
		if trades:
			shop_menu.open((source as Npc).shop)
			get_viewport().set_input_as_handled()
		return
	_play_dialogue(entry, source)
	get_viewport().set_input_as_handled()


## Opens the box on an entry; the hero stands still until it is read.
## `source` is the thing that spoke, or null for a boss's lines.
func _play_dialogue(entry: Dictionary, source: Interactable) -> void:
	if dialogue_box.is_open():
		return
	_talking_to = source
	player.talking = true
	if source is Npc:
		source.set_talking(true)
	# The box covers the message line.
	hud.hide_message()
	dialogue_box.play(entry)


func _on_dialogue_finished(entry: Dictionary) -> void:
	var forged := false
	var ending := ""
	for flag in DialogueData.sets(entry):
		var started := Quests.started_by(flag)
		var kept := Quests.finished_by(flag)
		if not started.is_empty() and not Story.has_flag(flag):
			hud.queue_message("A promise: %s" % started["title"], 3.0)
		elif not kept.is_empty() and not Story.has_flag(flag):
			hud.queue_message("Promise kept: %s" % kept["title"], 3.0)
		Story.set_flag(flag)
		forged = forged or flag.begins_with(Upgrade.SWORD_FLAG_PREFIX)
		if flag.begins_with(ENDING_FLAG_PREFIX):
			ending = flag
	Story.set_flag(DialogueData.seen_flag(str(entry.get("id", ""))))
	if forged:
		hud.show_message("Sword damage: %d" % player.attack_damage(), 3.0)
	if not ending.is_empty():
		_end_game(ending)
	if str(entry.get("warp", "")) == "start":
		_warp_to_start()
	if bool(entry.get("save", false)) and save_game():
		hud.queue_message("Game saved", 2.0)
	# Released after this frame's physics step, so the press that closed the
	# box is not also read as a jump.
	player.set_deferred("talking", false)
	var source := _talking_to
	_talking_to = null
	if is_instance_valid(source):
		if source is Npc:
			source.set_talking(false)
			if _shop_pending and not source.shop.is_empty():
				shop_menu.open.call_deferred(source.shop)
		source.on_dialogue_finished(entry)
	_shop_pending = false
	if _fight_pending and is_instance_valid(_boss):
		_fight_pending = false
		_boss.start_fight()
	# A gift (`gives`: an upgrade kind) lands once the hero has been released
	# for this frame, so its description can hold him again.
	var gift := str(entry.get("gives", ""))
	if LevelBuilder.UPGRADE_KINDS.has(gift):
		var kind: Player.UpgradeKind = LevelBuilder.UPGRADE_KINDS[gift]
		var flag := Upgrade.flag_for(kind, "gift", str(entry.get("id", "")))
		if not Story.has_flag(flag):
			player.apply_upgrade.call_deferred(kind, flag)
	# An entry's `then` (or the choice just taken) carries straight on, once
	# the hero has been released for this frame.
	var next_id := DialogueData.next_id(entry)
	if not next_id.is_empty() and dialogue != null:
		var next := dialogue.entry(next_id)
		if not next.is_empty():
			_play_dialogue.call_deferred(next, null)


## Whether one of the endings has played.
func game_over() -> bool:
	return _ended


## The clock turned: back up the pipes to the level's start, with the
## enemies rebuilt, so whatever waits for the `clock_turned` flag stands.
func _warp_to_start() -> void:
	_transitioning = true
	player.freeze()
	await fade.fade_out()
	_end_fight()
	_clear_motes()
	_active_checkpoint = -1
	player.global_position = level.player_start
	player.velocity = Vector2.ZERO
	player.rest_at(level.player_start)
	player.frozen = false
	_respawn_enemies()
	_respawn_power_ups()
	camera.snap_to_target()
	_enter_region(level.region_at(player.global_position.y), true)
	_transitioning = false
	fade.fade_in()


## One of the endings: the hero stands where he is, the line says which
## end this was, and R starts again from the last checkpoint.
func _end_game(flag: String) -> void:
	_ended = true
	player.freeze()
	Audio.play("level_complete")
	hud.show_message("%s   Press R to restart" % ENDINGS.get(flag, "The end."), -1.0)
