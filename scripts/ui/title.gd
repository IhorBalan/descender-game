extends Control

## The title screen: new game, continue the latest save, load a slot, maps,
## settings, credits, quit.
##
## New Game and Load both open the slot picker. For a new game an occupied
## slot asks to be pressed twice before it is overwritten, and then the
## difficulty picker chooses how hard the run is (kept in its save); the picker's
## Delete button turns it into a delete picker, where a slot asks the same
## before it is cleared. Maps lists the maps the editor published (see
## MapStore) and the exports in the project; choosing one downloads it if
## needed and makes it what New Game starts on. The hint along the bottom
## says where Continue would resume, while that button is focused, and
## otherwise shows the keys or the pad buttons, whichever the player last
## used. The version in the corner is the project's `application/config/version`.

enum SlotMode { NEW_GAME, LOAD, DELETE }

const GAME_SCENE := "res://scenes/main.tscn"
const DEFAULT_HINT := "{navigate} move   {ui_accept} choose   {ui_cancel} go back"
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
## The slot a new game will start in, while the difficulty is being picked.
var _new_game_slot := -1
## The slot pressed once, waiting for the second press that overwrites or
## deletes it.
var _confirm_slot := -1
var _starting := false

@onready var menu: VBoxContainer = $Menu
@onready var new_game_button: Button = $Menu/NewGame
@onready var continue_button: Button = $Menu/Continue
@onready var load_button: Button = $Menu/Load
@onready var maps_button: Button = $Menu/Maps
@onready var settings_button: Button = $Menu/Settings
@onready var credits_button: Button = $Menu/Credits
@onready var quit_button: Button = $Menu/Quit
@onready var settings_menu: SettingsMenu = $SettingsMenu
@onready var slots: VBoxContainer = $Slots
@onready var slots_heading: Label = $Slots/Heading
@onready var slot_buttons: Array[Button] = [$Slots/Slot1, $Slots/Slot2, $Slots/Slot3]
@onready var delete_button: Button = $Slots/Delete
@onready var back_button: Button = $Slots/Back
@onready var difficulty: VBoxContainer = $Difficulty
@onready
var difficulty_buttons: Array[Button] = [$Difficulty/Easy, $Difficulty/Normal, $Difficulty/Hard]
@onready var difficulty_back_button: Button = $Difficulty/Back
@onready var credits: Control = $Credits
@onready var credits_text: Label = $Credits/Text
@onready var credits_back_button: Button = $Credits/Back
@onready var hint: PromptLabel = $Hint
@onready var version_label: Label = $Version
@onready var maps: VBoxContainer = $Maps
@onready var maps_list: VBoxContainer = $Maps/List
@onready var maps_status: Label = $Maps/Status
@onready var maps_back_button: Button = $Maps/Back
@onready var live_map_status: Label = $LiveMapStatus


func _ready() -> void:
	new_game_button.pressed.connect(_show_slots.bind(SlotMode.NEW_GAME))
	continue_button.pressed.connect(_on_continue_pressed)
	load_button.pressed.connect(_show_slots.bind(SlotMode.LOAD))
	maps_button.pressed.connect(_show_maps)
	settings_button.pressed.connect(_show_settings)
	settings_menu.closed.connect(_show_menu)
	credits_button.pressed.connect(_show_credits)
	credits_back_button.pressed.connect(_show_menu)
	quit_button.pressed.connect(get_tree().quit)
	delete_button.pressed.connect(_on_delete_pressed)
	back_button.pressed.connect(_show_menu)
	maps_back_button.pressed.connect(_show_menu)
	for slot in slot_buttons.size():
		slot_buttons[slot].pressed.connect(_on_slot_pressed.bind(slot))
	for which in difficulty_buttons.size():
		var button := difficulty_buttons[which]
		button.text = (
			"%s   %s" % [SaveManager.DIFFICULTY_NAMES[which], SaveManager.DIFFICULTY_NOTES[which]]
		)
		button.pressed.connect(_on_difficulty_pressed.bind(which))
	difficulty_back_button.pressed.connect(_show_slots.bind(SlotMode.NEW_GAME))
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
	if not event.is_action_pressed("ui_cancel"):
		return
	if difficulty.visible:
		_show_slots(SlotMode.NEW_GAME)
	elif slots.visible or credits.visible or maps.visible:
		_show_menu()
	else:
		return
	Audio.play("menu_back")
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
	difficulty.visible = false
	credits.visible = false
	maps.visible = false
	menu.visible = true
	# New Game says which map it starts (see the Maps menu).
	var chosen := MapStore.chosen_name()
	new_game_button.text = "New Game" if chosen.is_empty() else "New Game: %s" % chosen
	var any_save := SaveManager.latest_slot() >= 0
	continue_button.disabled = not any_save
	load_button.disabled = not any_save
	new_game_button.grab_focus()


## The hint says where Continue would resume while it is focused.
func _on_menu_focus(button: Button) -> void:
	if button == continue_button and not continue_button.disabled:
		hint.template = continue_summary()
	else:
		hint.template = DEFAULT_HINT


## One line on the save Continue resumes: its slot and the slot's summary.
func continue_summary() -> String:
	var slot := SaveManager.latest_slot()
	if slot < 0:
		return DEFAULT_HINT
	return "Resumes slot %d:   %s" % [slot + 1, SaveManager.summary(slot)]


func _show_slots(mode: SlotMode) -> void:
	_slot_mode = mode
	_confirm_slot = -1
	var chosen := MapStore.chosen_name()
	match mode:
		SlotMode.NEW_GAME:
			slots_heading.text = (
				"New game: choose a slot"
				if chosen.is_empty()
				else "New game on %s: choose a slot" % chosen
			)
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
	difficulty.visible = false
	slots.visible = true
	(first if first else back_button).grab_focus()


func _show_difficulty(slot: int) -> void:
	_new_game_slot = slot
	slots.visible = false
	difficulty.visible = true
	difficulty_buttons[SaveManager.Difficulty.NORMAL].grab_focus()


func _on_difficulty_pressed(which: int) -> void:
	if _new_game_slot < 0:
		return
	SaveManager.start_new_game(_new_game_slot, which as SaveManager.Difficulty)
	_start_game()


func _on_slot_pressed(slot: int) -> void:
	match _slot_mode:
		SlotMode.NEW_GAME:
			if SaveManager.has_save(slot) and not _confirm(slot, "Overwrite?"):
				return
			_show_difficulty(slot)
			return
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


## The Maps menu: the exports in the project (the descent and anything
## registered), then the maps the editor published, downloaded ones first.
func _show_maps() -> void:
	menu.visible = false
	maps.visible = true
	for child in maps_list.get_children():
		maps_list.remove_child(child)
		child.queue_free()
	maps_status.text = "Looking for published maps…"
	for level in Levels.LEVELS:
		_add_map_button("%s   (in the game)" % level["name"], _start_level.bind(""))
	maps_back_button.grab_focus()
	var listed := await MapStore.list()
	if not maps.visible:
		return
	for entry in listed:
		var map_id := str(entry.get("mapId", ""))
		if entry.get("unpublished", false):
			_add_map_button(
				"%s   (downloaded, no longer published)" % str(entry.get("name", map_id)),
				_start_level.bind(MapStore.level_id(map_id))
			)
		else:
			var note := (
				"downloaded"
				if entry.get("cached", false)
				else ("update available" if entry.has("path") else "download")
			)
			_add_map_button(
				"%s   (%s)" % [str(entry.get("name", map_id)), note], _fetch_and_start.bind(map_id)
			)
	maps_status.text = (
		MapStore.status
		if not MapStore.status.is_empty()
		else (
			"%d published" % listed.size()
			if listed.size()
			else "Nothing published yet: use Publish for game in the editor"
		)
	)
	if maps_list.get_child_count() > 0:
		maps_list.get_child(0).grab_focus()


func _add_map_button(text: String, on_pressed: Callable) -> void:
	var button := Button.new()
	button.text = text
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.pressed.connect(on_pressed)
	maps_list.add_child(button)
	Audio.wire_menu(button)


func _fetch_and_start(map_id: String) -> void:
	for child in maps_list.get_children():
		child.disabled = true
	var level: String = await MapStore.fetch(map_id)
	maps_status.text = MapStore.status
	for child in maps_list.get_children():
		child.disabled = false
	if level.is_empty() or not maps.visible:
		return
	_start_level(level)


func _process(_delta: float) -> void:
	# A download reports its progress while the Maps menu is up.
	if maps.visible and MapStore.running:
		maps_status.text = MapStore.status


## A chosen map (empty: the descent) is what New Game starts on from now
## on; it goes straight to the slot picker.
func _start_level(level_id: String) -> void:
	MapStore.choose(level_id)
	_show_slots(SlotMode.NEW_GAME)


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
