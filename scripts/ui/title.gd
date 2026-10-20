extends Control

## The title screen: new game, continue the latest save, load a slot,
## settings, quit.
##
## New Game and Load both open the slot picker. For a new game an occupied
## slot asks to be pressed twice before it is overwritten.

enum SlotMode { NEW_GAME, LOAD }

const GAME_SCENE := "res://scenes/main.tscn"

var _slot_mode := SlotMode.NEW_GAME
var _overwrite_slot := -1

@onready var menu: VBoxContainer = $Menu
@onready var new_game_button: Button = $Menu/NewGame
@onready var continue_button: Button = $Menu/Continue
@onready var load_button: Button = $Menu/Load
@onready var settings_button: Button = $Menu/Settings
@onready var quit_button: Button = $Menu/Quit
@onready var settings_menu: SettingsMenu = $SettingsMenu
@onready var slots: VBoxContainer = $Slots
@onready var slots_heading: Label = $Slots/Heading
@onready var slot_buttons: Array[Button] = [$Slots/Slot1, $Slots/Slot2, $Slots/Slot3]
@onready var back_button: Button = $Slots/Back


func _ready() -> void:
	new_game_button.pressed.connect(_show_slots.bind(SlotMode.NEW_GAME))
	continue_button.pressed.connect(_on_continue_pressed)
	load_button.pressed.connect(_show_slots.bind(SlotMode.LOAD))
	settings_button.pressed.connect(_show_settings)
	settings_menu.closed.connect(_show_menu)
	quit_button.pressed.connect(get_tree().quit)
	back_button.pressed.connect(_show_menu)
	for slot in slot_buttons.size():
		slot_buttons[slot].pressed.connect(_on_slot_pressed.bind(slot))
	Audio.wire_menu(self)
	Audio.stop_ambient()
	_show_menu()


func _unhandled_input(event: InputEvent) -> void:
	if slots.visible and event.is_action_pressed("ui_cancel"):
		_show_menu()
		get_viewport().set_input_as_handled()


func _show_settings() -> void:
	menu.visible = false
	settings_menu.open()


func _show_menu() -> void:
	settings_menu.visible = false
	slots.visible = false
	menu.visible = true
	var any_save := SaveManager.latest_slot() >= 0
	continue_button.disabled = not any_save
	load_button.disabled = not any_save
	new_game_button.grab_focus()


func _show_slots(mode: SlotMode) -> void:
	_slot_mode = mode
	_overwrite_slot = -1
	slots_heading.text = "New game: choose a slot" if mode == SlotMode.NEW_GAME else "Load game"
	var first: Button = null
	for slot in slot_buttons.size():
		var button := slot_buttons[slot]
		button.text = "%d   %s" % [slot + 1, SaveManager.summary(slot)]
		button.disabled = mode == SlotMode.LOAD and not SaveManager.has_save(slot)
		if first == null and not button.disabled:
			first = button
	menu.visible = false
	slots.visible = true
	(first if first else back_button).grab_focus()


func _on_slot_pressed(slot: int) -> void:
	match _slot_mode:
		SlotMode.NEW_GAME:
			if SaveManager.has_save(slot) and _overwrite_slot != slot:
				_overwrite_slot = slot
				slot_buttons[slot].text = "%d   Overwrite?  Press again" % (slot + 1)
				return
			SaveManager.start_new_game(slot)
		SlotMode.LOAD:
			if not SaveManager.load_slot(slot):
				return
	_start_game()


func _on_continue_pressed() -> void:
	var slot := SaveManager.latest_slot()
	if slot >= 0 and SaveManager.load_slot(slot):
		_start_game()


func _start_game() -> void:
	get_tree().change_scene_to_file(GAME_SCENE)
