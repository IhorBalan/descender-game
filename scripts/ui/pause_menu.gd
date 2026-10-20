class_name PauseMenu
extends CanvasLayer

## The pause menu: resume, save to the current slot, or quit to the title.
## Keeps processing while the tree is paused; main.gd opens and closes it.

signal save_requested

const TITLE_SCENE := "res://scenes/title.tscn"

@onready var resume_button: Button = $Panel/Menu/Resume
@onready var save_button: Button = $Panel/Menu/Save
@onready var quit_button: Button = $Panel/Menu/Quit


func _ready() -> void:
	visible = false
	resume_button.pressed.connect(close)
	save_button.pressed.connect(_on_save_pressed)
	quit_button.pressed.connect(_on_quit_pressed)


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("pause") or event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()


func is_open() -> bool:
	return visible


## Pauses the tree and shows the menu. Saving is offered only when the game
## was started from a slot.
func open() -> void:
	get_tree().paused = true
	save_button.disabled = SaveManager.current_slot < 0
	save_button.text = "Save Game"
	visible = true
	resume_button.grab_focus()


func close() -> void:
	visible = false
	get_tree().paused = false


func _on_save_pressed() -> void:
	save_requested.emit()


## Called back by main.gd with where the save landed.
func show_saved(place: String) -> void:
	save_button.text = "Saved at %s" % place


func _on_quit_pressed() -> void:
	get_tree().paused = false
	get_tree().change_scene_to_file(TITLE_SCENE)
