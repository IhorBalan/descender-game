class_name CheckpointMenu
extends CanvasLayer

## The camp at a checkpoint: pressing the talk key at one the hero has
## rested at opens this, over the paused game. The pack (wear and use
## what he carries), the skills (spend the points his levels gave) and
## fast travel on the left; save and leave on the right. A line under the
## heading says which checkpoint this is and the level, and a line above
## the hint describes the picked button. Cancel leaves. main.gd opens it
## and it closes itself.
##
## Fast travel opens the map page on the checkpoints the hero has rested
## at; picking one emits fast_travel_requested and main.gd carries him
## there (see Main.travel_to_checkpoint). main.gd offers it with
## set_fast_travel when it may be taken from here.

signal closed
signal save_requested
## A skill point was spent (the HUD line follows).
signal skill_learned
## A rested checkpoint was picked to travel to (by index in the level).
signal fast_travel_requested(index: int)

const SAVE_TEXT := "Save Game"
const NOTE_COLOR := Color(0.7, 0.7, 0.78)
const WARN_COLOR := Color(1.0, 0.7, 0.6)
const TRAVEL_CLOSED_NOTE := "No other camp he has rested at answers from here."
const NOTES := {
	"pack": "What he carries: wear a blade, a coat or a charm, or drink a flask.",
	"skills": "Where his levels go: a heart, an edge, quicker feet.",
	"travel": "The angels' system can carry him to another camp he has rested at.",
	"save": "Writes the game to its slot, resuming at this camp.",
	"leave": "Back to the descent.",
}

## The checkpoint the camp was opened at, by index in the level.
var checkpoint_index := -1
var _player: Player
var _level: LevelBuilder
## The checkpoints on offer to travel to (rested at, by index).
var _rested: Array[int] = []

@onready var heading: Label = $Panel/Heading
@onready var divider: TextureRect = $Panel/Divider
@onready var status_label: Label = $Panel/Status
@onready var menu: HBoxContainer = $Panel/Menu
@onready var pack_button: Button = $Panel/Menu/Left/Pack
@onready var skills_button: Button = $Panel/Menu/Left/Skills
@onready var travel_button: Button = $Panel/Menu/Left/Travel
@onready var save_button: Button = $Panel/Menu/Right/Save
@onready var leave_button: Button = $Panel/Menu/Right/Leave
@onready var description: Label = $Panel/Description
@onready var hint: PromptLabel = $Panel/Hint
@onready var inventory_menu: InventoryMenu = $Panel/InventoryMenu
@onready var skill_menu: SkillMenu = $Panel/SkillMenu
@onready var map_screen: MapScreen = $Panel/MapScreen


func _ready() -> void:
	visible = false
	pack_button.pressed.connect(_on_pack_pressed)
	inventory_menu.closed.connect(_on_page_closed.bind(pack_button))
	skills_button.pressed.connect(_on_skills_pressed)
	skill_menu.closed.connect(_on_page_closed.bind(skills_button))
	skill_menu.learned.connect(_on_skill_learned)
	travel_button.pressed.connect(_on_travel_pressed)
	map_screen.closed.connect(_on_page_closed.bind(travel_button))
	map_screen.travel_requested.connect(fast_travel_requested.emit)
	save_button.pressed.connect(save_requested.emit)
	leave_button.pressed.connect(close)
	for key: String in NOTES:
		var button: Button = get("%s_button" % key)
		button.focus_entered.connect(_show_note.bind(str(NOTES[key]), NOTE_COLOR))
	Audio.wire_menu(self)


func _unhandled_input(event: InputEvent) -> void:
	if not visible or inventory_menu.visible or skill_menu.visible or map_screen.visible:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("pause"):
		close()
		get_viewport().set_input_as_handled()


func is_open() -> bool:
	return visible


## Pauses the tree and opens the camp at a checkpoint (`index` in the
## level's list, of `count`). Saving is offered only when the game was
## started from a slot.
func open(player: Player, index: int, count: int) -> void:
	_player = player
	checkpoint_index = index
	inventory_menu.player = player
	skill_menu.player = player
	heading.text = "CAMP" if index < 0 else "CAMP %d" % (index + 1)
	status_label.text = status_text(index, count)
	save_button.disabled = SaveManager.current_slot < 0
	save_button.text = SAVE_TEXT
	set_fast_travel(null, [])
	_show_frame(true)
	get_tree().paused = true
	visible = true
	# A point waiting is what the camp is for; the skills take the focus.
	if Skills.points_left() > 0:
		skills_button.grab_focus()
	else:
		pack_button.grab_focus()


func close() -> void:
	visible = false
	inventory_menu.visible = false
	skill_menu.visible = false
	map_screen.visible = false
	get_tree().paused = false
	closed.emit()


## The line under the heading: which checkpoint of how many, the level and
## the points waiting.
static func status_text(index: int, count: int) -> String:
	var parts: PackedStringArray = []
	if index >= 0 and count > 0:
		parts.append("Checkpoint %d of %d" % [index + 1, count])
	parts.append("Level %d" % Skills.level())
	var points := Skills.points_left()
	if points > 0:
		parts.append("%d skill point%s to spend" % [points, "" if points == 1 else "s"])
	return "      ".join(parts)


## Offers fast travel from this camp: `level` for the map and the
## checkpoints rested at. It needs another one to go to, else the button
## stays grey.
func set_fast_travel(level: LevelBuilder, rested: Array[int]) -> void:
	_level = level
	_rested = rested.duplicate()
	travel_button.disabled = not can_travel()


## Whether there is somewhere to go: a map, and a rested camp besides this.
func can_travel() -> bool:
	if _level == null or _level.world == null or checkpoint_index not in _rested:
		return false
	return _rested.size() > 1


## Called back by main.gd with whether the save landed.
func show_saved(saved: bool) -> void:
	if saved:
		save_button.text = "Saved at camp %d" % (checkpoint_index + 1)
	else:
		_show_note("There is no slot to save to.", WARN_COLOR)


func _on_pack_pressed() -> void:
	_show_frame(false)
	inventory_menu.open()


func _on_skills_pressed() -> void:
	_show_frame(false)
	skill_menu.open()


func _on_skill_learned(_id: String) -> void:
	status_label.text = status_text(checkpoint_index, -1)
	skill_learned.emit()


func _on_travel_pressed() -> void:
	if not can_travel():
		_show_note(TRAVEL_CLOSED_NOTE, WARN_COLOR)
		return
	_show_frame(false)
	var hero := _player.global_position if is_instance_valid(_player) else Vector2.ZERO
	map_screen.open(_level.world, hero, _level.checkpoints, _rested, checkpoint_index)


func _on_page_closed(button: Button) -> void:
	status_label.text = status_text(checkpoint_index, -1)
	_show_frame(true)
	button.grab_focus()


## The menu with its heading, status line and hint; hidden behind a page.
func _show_frame(shown: bool) -> void:
	menu.visible = shown
	heading.visible = shown
	divider.visible = shown
	status_label.visible = shown
	description.visible = shown
	hint.visible = shown


func _show_note(text: String, color: Color) -> void:
	description.text = text
	description.add_theme_color_override("font_color", color)
