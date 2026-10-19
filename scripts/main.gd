extends Node2D

## Boots the level, spawns the hero and wires up the HUD and camera.

const LEVEL_PATH := "res://levels/level_01.txt"
const PLAYER_SCENE := preload("res://scenes/player.tscn")
const CONTROLS_HINT := "A/D move   Space jump   J attack   K dash   H heal"

@onready var level: LevelBuilder = $Level
@onready var camera: GameCamera = $Camera2D
@onready var hud: Hud = $HUD

var player: Player
var level_complete := false


func _ready() -> void:
	level.load_from_file(LEVEL_PATH)

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
	level.spawn_enemies()

	camera.target = player
	camera.set_limits(level.size_px)
	camera.global_position = player.global_position
	camera.reset_smoothing()

	hud.show_message(CONTROLS_HINT, 6.0)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("restart"):
		get_tree().reload_current_scene()


func _on_player_died() -> void:
	hud.show_message("The angels' system pulls you back...", 1.2)


func _on_player_respawned() -> void:
	# The signal fires from the hero's physics tick; rebuild the enemies once
	# the physics step is over.
	level.spawn_enemies.call_deferred()
	camera.global_position = player.global_position
	camera.reset_smoothing()


func _on_checkpoint_rested(_checkpoint: Checkpoint) -> void:
	hud.show_message("Checkpoint: health and healing restored", 2.5)


func _on_goal_reached() -> void:
	level_complete = true
	player.frozen = true
	hud.show_message("Level complete!   Press R to restart", -1.0)
