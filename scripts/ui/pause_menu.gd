class_name PauseMenu
extends CanvasLayer

## The pause menu, in two columns: resume, restart at the last checkpoint,
## save to the current slot and quit to the title on the left; the map,
## the promises (quest log), the pack (inventory) and the settings on the
## right. The map covers the whole page, so the heading, the status line
## and the hint step aside while it is open.
## Quit asks to be pressed twice, since whatever happened after the last
## save goes with it. A line under the heading says which slot the game is
## in and how long it has been played. Keeps processing while the tree is
## paused; main.gd opens it and it closes itself.

signal save_requested
signal restart_requested

const QUIT_TEXT := "Quit to Title"
const QUIT_CONFIRM_TEXT := "Quit?  Press again"

const TITLE_SCENE := "res://scenes/title.tscn"

## The hero and the level the menu was opened over, for the map.
var _player: Player
var _level: LevelBuilder

@onready var resume_button: Button = $Panel/Menu/Left/Resume
@onready var restart_button: Button = $Panel/Menu/Left/Restart
@onready var save_button: Button = $Panel/Menu/Left/Save
@onready var map_button: Button = $Panel/Menu/Right/Map
@onready var quest_log_button: Button = $Panel/Menu/Right/Promises
@onready var pack_button: Button = $Panel/Menu/Right/Pack
@onready var settings_button: Button = $Panel/Menu/Right/Settings
@onready var quit_button: Button = $Panel/Menu/Left/Quit
@onready var menu: HBoxContainer = $Panel/Menu
@onready var heading: Label = $Panel/Heading
@onready var hint: Label = $Panel/Hint
@onready var status_label: Label = $Panel/Status
@onready var settings_menu: SettingsMenu = $Panel/SettingsMenu
@onready var quest_log: QuestLog = $Panel/QuestLog
@onready var map_screen: MapScreen = $Panel/MapScreen
@onready var inventory_menu: InventoryMenu = $Panel/InventoryMenu


func _ready() -> void:
	visible = false
	resume_button.pressed.connect(close)
	restart_button.pressed.connect(_on_restart_pressed)
	save_button.pressed.connect(_on_save_pressed)
	map_button.pressed.connect(_on_map_pressed)
	map_screen.closed.connect(_on_map_closed)
	quest_log_button.pressed.connect(_on_quest_log_pressed)
	quest_log.closed.connect(_on_quest_log_closed)
	pack_button.pressed.connect(_on_pack_pressed)
	inventory_menu.closed.connect(_on_pack_closed)
	settings_button.pressed.connect(_on_settings_pressed)
	settings_menu.closed.connect(_on_settings_closed)
	quit_button.pressed.connect(_on_quit_pressed)
	quit_button.focus_exited.connect(_unask_quit)
	Audio.wire_menu(self)


func _unhandled_input(event: InputEvent) -> void:
	if (
		not visible
		or settings_menu.visible
		or quest_log.visible
		or inventory_menu.visible
		or map_screen.visible
	):
		return
	if event.is_action_pressed("pause") or event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()


func is_open() -> bool:
	return visible


## Pauses the tree and shows the menu. Saving is offered only when the game
## was started from a slot. `status` is the line under the heading, `player`
## the hero, for the pack and the map, and `level` the level, for the map
## (the button is greyed out without one).
func open(status := "", player: Player = null, level: LevelBuilder = null) -> void:
	inventory_menu.player = player
	_player = player
	_level = level
	map_button.disabled = level == null or level.world == null
	get_tree().paused = true
	save_button.disabled = SaveManager.current_slot < 0
	save_button.text = "Save Game"
	status_label.text = status
	_unask_quit()
	visible = true
	resume_button.grab_focus()


func close() -> void:
	visible = false
	_unask_quit()
	get_tree().paused = false


func _on_restart_pressed() -> void:
	close()
	restart_requested.emit()


func _on_save_pressed() -> void:
	save_requested.emit()


## Called back by main.gd with where the save landed.
func show_saved(place: String) -> void:
	save_button.text = "Saved at %s" % place


func _on_map_pressed() -> void:
	if _level == null or _level.world == null:
		return
	_show_frame(false)
	var hero := _player.global_position if is_instance_valid(_player) else Vector2.ZERO
	map_screen.open(_level.world, hero, _level.checkpoints)


func _on_map_closed() -> void:
	_show_frame(true)
	map_button.grab_focus()


## The menu with its heading, status line and hint; hidden behind the map.
func _show_frame(shown: bool) -> void:
	menu.visible = shown
	heading.visible = shown
	status_label.visible = shown
	hint.visible = shown


func _on_quest_log_pressed() -> void:
	menu.visible = false
	quest_log.open()


func _on_quest_log_closed() -> void:
	menu.visible = true
	quest_log_button.grab_focus()


func _on_pack_pressed() -> void:
	menu.visible = false
	inventory_menu.open()


func _on_pack_closed() -> void:
	menu.visible = true
	pack_button.grab_focus()


func _on_settings_pressed() -> void:
	menu.visible = false
	settings_menu.open()


func _on_settings_closed() -> void:
	menu.visible = true
	settings_button.grab_focus()


## The first press asks; the second, with the question still showing, quits.
func _on_quit_pressed() -> void:
	if quit_button.text != QUIT_CONFIRM_TEXT:
		quit_button.text = QUIT_CONFIRM_TEXT
		return
	get_tree().paused = false
	get_tree().change_scene_to_file(TITLE_SCENE)


func _unask_quit() -> void:
	quit_button.text = QUIT_TEXT
