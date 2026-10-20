extends Node2D

## Boots a level, spawns the hero and wires up the HUD and camera, applies
## the save the title screen chose and writes it back at every checkpoint.
## Reaching the goal gate fades out, saves and boots the next level.
##
## Which level: the save's, else the `--level=<id>` user argument
## (`godot --path . -- --level=heaven_2`), else the first in Levels.

const PLAYER_SCENE := preload("res://scenes/player.tscn")
const CONTROLS_HINT := "A/D move   Space jump   J attack   K dash   H heal"
const DEATH_FADE_TIME := 0.4
const ABILITY_MESSAGES := {
	Player.Ability.WINGS: "Wings: press jump again in the air",
	Player.Ability.LIGHT_DASH: "Light dash: dash further, and through walls of light",
}

var player: Player
## The id of the level being played (see Levels).
var level_id := ""
## Seconds played in this save, carried over from the loaded file.
var play_time := 0.0
## Index into level.checkpoints of the one the hero last rested at.
var _active_checkpoint := -1
## Set once the goal is reached: the level is fading out, so pausing and
## restarting are over for this scene.
var _transitioning := false

@onready var level: LevelBuilder = $Level
@onready var camera: GameCamera = $Camera2D
@onready var hud: Hud = $HUD
@onready var fade: ScreenFade = $ScreenFade
@onready var pause_menu: PauseMenu = $PauseMenu


func _ready() -> void:
	level_id = _pick_level()
	level.load_from_file(Levels.path(level_id))

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
	if level.goal:
		level.goal.reached.connect(_on_goal_reached)
	# Applied before the ability signal is wired, so loaded powers are
	# restored quietly instead of announced again.
	if not SaveManager.pending.is_empty():
		_apply_save(SaveManager.pending)
		SaveManager.pending = {}
	player.ability_unlocked.connect(_on_ability_unlocked)
	level.spawn_enemies()
	pause_menu.save_requested.connect(_on_save_requested)

	camera.target = player
	camera.set_limits(level.size_px)
	camera.snap_to_target()

	fade.cover_now()
	fade.fade_in()
	Audio.play_ambient(Levels.ambient(level_id))
	hud.show_message(Levels.display_name(level_id), 2.5)
	if level_id == Levels.first_id:
		hud.queue_message(CONTROLS_HINT, 6.0)


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


func _unhandled_input(event: InputEvent) -> void:
	if _transitioning:
		return
	if event.is_action_pressed("pause"):
		pause_menu.open()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("restart"):
		# Restart from the last checkpoint with the progress so far, so a
		# reload never writes a fresh run over the slot.
		SaveManager.pending = capture_save()
		get_tree().reload_current_scene()


# --- Saving -------------------------------------------------------------


## Puts the hero at the saved checkpoint, as if he had just rested there,
## with the saved abilities (their feathers gone from the level).
func _apply_save(data: Dictionary) -> void:
	play_time = float(data.get("play_time", 0.0))
	var abilities: Array[Player.Ability] = []
	for value in data.get("abilities", []):
		var ability := int(value)
		if ability in Player.Ability.values() and ability not in abilities:
			abilities.append(ability as Player.Ability)
	for ability in abilities:
		player.unlock(ability)
	level.remove_pickups(abilities)
	var index := int(data.get("checkpoint", -1))
	if index >= 0 and index < level.checkpoints.size():
		_active_checkpoint = index
		var checkpoint := level.checkpoints[index]
		checkpoint.set_active(true)
		player.global_position = checkpoint.global_position
		player.rest_at(checkpoint.global_position)


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
	level.spawn_enemies.call_deferred()
	_clear_motes()
	camera.snap_to_target()
	fade.fade_in()


## Heal motes left by enemies go with the enemies when the fight resets.
func _clear_motes() -> void:
	for mote in get_tree().get_nodes_in_group("heal_mote"):
		mote.queue_free()


func _on_checkpoint_rested(checkpoint: Checkpoint) -> void:
	_active_checkpoint = level.checkpoints.find(checkpoint)
	var text := "Checkpoint: health and healing restored"
	if save_game():
		text += "   Game saved"
	hud.show_message(text, 2.5)


func _on_ability_unlocked(ability: Player.Ability) -> void:
	hud.show_message(ABILITY_MESSAGES.get(ability, "New ability unlocked"), 3.5)


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
