extends Control

## The title screen: new game, continue the latest save, load a slot,
## settings, credits, quit.
##
## New Game and Load both open the slot picker. For a new game an occupied
## slot asks to be pressed twice before it is overwritten; the picker's
## Delete button turns it into a delete picker, where a slot asks the same
## before it is cleared. The hint along the bottom says where Continue
## would resume, while that button is focused. The version in the corner
## is the project's `application/config/version`.

enum SlotMode { NEW_GAME, LOAD, DELETE }

const GAME_SCENE := "res://scenes/main.tscn"
const DEFAULT_HINT := "Arrows or stick to move   Space or A to choose   Esc or B to go back"
## The credits, one line each; an empty string is a blank line.
const CREDITS: Array[String] = [
	"A game by Ihor Balan",
	"",
	"Design, code, placeholder art and sound",
	"made with Claude Code",
	"",
	"Map editor by Vadim Tarasenko",
	"",
	"Made with the Godot Engine",
]

var _slot_mode := SlotMode.NEW_GAME
## The slot pressed once, waiting for the second press that overwrites or
## deletes it.
var _confirm_slot := -1
var _starting := false

@onready var menu: VBoxContainer = $Menu
@onready var new_game_button: Button = $Menu/NewGame
@onready var continue_button: Button = $Menu/Continue
@onready var load_button: Button = $Menu/Load
@onready var settings_button: Button = $Menu/Settings
@onready var credits_button: Button = $Menu/Credits
@onready var quit_button: Button = $Menu/Quit
@onready var settings_menu: SettingsMenu = $SettingsMenu
@onready var slots: VBoxContainer = $Slots
@onready var slots_heading: Label = $Slots/Heading
@onready var slot_buttons: Array[Button] = [$Slots/Slot1, $Slots/Slot2, $Slots/Slot3]
@onready var delete_button: Button = $Slots/Delete
@onready var back_button: Button = $Slots/Back
@onready var credits: Control = $Credits
@onready var credits_text: Label = $Credits/Text
@onready var credits_back_button: Button = $Credits/Back
@onready var hint: Label = $Hint
@onready var version_label: Label = $Version
@onready var live_map_status: Label = $LiveMapStatus


func _ready() -> void:
	new_game_button.pressed.connect(_show_slots.bind(SlotMode.NEW_GAME))
	continue_button.pressed.connect(_on_continue_pressed)
	load_button.pressed.connect(_show_slots.bind(SlotMode.LOAD))
	settings_button.pressed.connect(_show_settings)
	settings_menu.closed.connect(_show_menu)
	credits_button.pressed.connect(_show_credits)
	credits_back_button.pressed.connect(_show_menu)
	quit_button.pressed.connect(get_tree().quit)
	delete_button.pressed.connect(_on_delete_pressed)
	back_button.pressed.connect(_show_menu)
	for slot in slot_buttons.size():
		slot_buttons[slot].pressed.connect(_on_slot_pressed.bind(slot))
	for button in menu.get_children():
		button.focus_entered.connect(_on_menu_focus.bind(button))
	credits_text.text = "\n".join(CREDITS)
	var version := str(ProjectSettings.get_setting("application/config/version", ""))
	version_label.text = "v%s" % version if not version.is_empty() else ""
	Audio.wire_menu(self)
	Audio.stop_ambient()
	Audio.play_music("title")
	_show_menu()
	# The live map sync (see LiveMap) reports along the bottom while it runs.
	if LiveMap.enabled:
		live_map_status.visible = true
		_show_live_map_status(LiveMap.ok, LiveMap.status)
		LiveMap.finished.connect(_show_live_map_status)


func _unhandled_input(event: InputEvent) -> void:
	if (slots.visible or credits.visible) and event.is_action_pressed("ui_cancel"):
		Audio.play("menu_back")
		_show_menu()
		get_viewport().set_input_as_handled()


func _show_settings() -> void:
	menu.visible = false
	settings_menu.open()


func _show_credits() -> void:
	menu.visible = false
	credits.visible = true
	credits_back_button.grab_focus()


func _show_menu() -> void:
	settings_menu.visible = false
	slots.visible = false
	credits.visible = false
	menu.visible = true
	var any_save := SaveManager.latest_slot() >= 0
	continue_button.disabled = not any_save
	load_button.disabled = not any_save
	new_game_button.grab_focus()


## The hint says where Continue would resume while it is focused.
func _on_menu_focus(button: Button) -> void:
	if button == continue_button and not continue_button.disabled:
		hint.text = continue_summary()
	else:
		hint.text = DEFAULT_HINT


## One line on the save Continue resumes: its slot and the slot's summary.
func continue_summary() -> String:
	var slot := SaveManager.latest_slot()
	if slot < 0:
		return DEFAULT_HINT
	return "Resumes slot %d:   %s" % [slot + 1, SaveManager.summary(slot)]


func _show_slots(mode: SlotMode) -> void:
	_slot_mode = mode
	_confirm_slot = -1
	match mode:
		SlotMode.NEW_GAME:
			slots_heading.text = "New game: choose a slot"
		SlotMode.LOAD:
			slots_heading.text = "Load game"
		SlotMode.DELETE:
			slots_heading.text = "Delete a save: choose a slot"
	# Deleting is offered from the load picker, and the delete picker goes
	# back to loading.
	delete_button.visible = mode != SlotMode.NEW_GAME
	delete_button.text = "Load instead" if mode == SlotMode.DELETE else "Delete a save"
	var first: Button = null
	for slot in slot_buttons.size():
		var button := slot_buttons[slot]
		button.text = "%d   %s" % [slot + 1, SaveManager.summary(slot)]
		button.disabled = mode != SlotMode.NEW_GAME and not SaveManager.has_save(slot)
		if first == null and not button.disabled:
			first = button
	menu.visible = false
	slots.visible = true
	(first if first else back_button).grab_focus()


func _on_slot_pressed(slot: int) -> void:
	match _slot_mode:
		SlotMode.NEW_GAME:
			if SaveManager.has_save(slot) and not _confirm(slot, "Overwrite?"):
				return
			SaveManager.start_new_game(slot)
		SlotMode.LOAD:
			if not SaveManager.load_slot(slot):
				return
		SlotMode.DELETE:
			if _confirm(slot, "Delete?"):
				SaveManager.delete(slot)
				_show_slots(SlotMode.DELETE if SaveManager.latest_slot() >= 0 else SlotMode.LOAD)
			return
	_start_game()


## A slot's first press asks the question; the second press answers it.
func _confirm(slot: int, question: String) -> bool:
	if _confirm_slot == slot:
		return true
	_confirm_slot = slot
	slot_buttons[slot].text = "%d   %s  Press again" % [slot + 1, question]
	return false


func _on_delete_pressed() -> void:
	_show_slots(SlotMode.LOAD if _slot_mode == SlotMode.DELETE else SlotMode.DELETE)


func _on_continue_pressed() -> void:
	var slot := SaveManager.latest_slot()
	if slot >= 0 and SaveManager.load_slot(slot):
		_start_game()


func _start_game() -> void:
	if _starting:
		return
	_starting = true
	# The world files may still be on their way from the editor.
	if LiveMap.running:
		live_map_status.text = "Waiting for the live map…"
		await LiveMap.wait()
	get_tree().change_scene_to_file(GAME_SCENE)


func _show_live_map_status(ok: bool, message: String) -> void:
	live_map_status.text = message
	live_map_status.add_theme_color_override(
		"font_color", Color(0.56, 0.88, 0.54) if ok or LiveMap.running else Color(1.0, 0.55, 0.45)
	)
