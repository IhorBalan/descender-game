class_name PauseMenu
extends CanvasLayer

## The pause menu: resume, restart at the last checkpoint, save to the
## current slot, settings, or quit to the title. Quit asks to be pressed
## twice, since whatever happened after the last save goes with it. A
## line under the heading says which slot the game is in and how long it
## has been played. Keeps processing while the tree is paused; main.gd
## opens it and it closes itself.

signal save_requested
signal restart_requested

const QUIT_TEXT := "Quit to Title"
const QUIT_CONFIRM_TEXT := "Quit?  Press again"

const TITLE_SCENE := "res://scenes/title.tscn"

@onready var resume_button: Button = $Panel/Menu/Resume
@onready var restart_button: Button = $Panel/Menu/Restart
@onready var save_button: Button = $Panel/Menu/Save
@onready var settings_button: Button = $Panel/Menu/Settings
@onready var quit_button: Button = $Panel/Menu/Quit
@onready var menu: VBoxContainer = $Panel/Menu
@onready var status_label: Label = $Panel/Status
@onready var settings_menu: SettingsMenu = $Panel/SettingsMenu


func _ready() -> void:
	visible = false
	resume_button.pressed.connect(close)
	restart_button.pressed.connect(_on_restart_pressed)
	save_button.pressed.connect(_on_save_pressed)
	settings_button.pressed.connect(_on_settings_pressed)
	settings_menu.closed.connect(_on_settings_closed)
	quit_button.pressed.connect(_on_quit_pressed)
	quit_button.focus_exited.connect(_unask_quit)
	Audio.wire_menu(self)


func _unhandled_input(event: InputEvent) -> void:
	if not visible or settings_menu.visible:
		return
	if event.is_action_pressed("pause") or event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()


func is_open() -> bool:
	return visible


## Pauses the tree and shows the menu. Saving is offered only when the game
## was started from a slot. `status` is the line under the heading.
func open(status := "") -> void:
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
