class_name DialogueBox
extends CanvasLayer

## The text box at the bottom of the screen: a speaker name and one box of
## text at a time, typed out. Interact (or jump) finishes the typing, then
## turns the page; the last page closes the box. main.gd opens it with an
## entry from DialogueData and freezes the hero while it is open.

signal finished(entry: Dictionary)

const CHARS_PER_SECOND := 45.0
const BLINK_PERIOD := 0.8

var _entry: Dictionary = {}
var _index := 0
var _shown := 0.0
var _blink := 0.0

@onready var panel: Control = $Panel
@onready var name_label: Label = $Panel/Name
@onready var text_label: Label = $Panel/Text
@onready var more_label: Label = $Panel/More


func _ready() -> void:
	panel.visible = false


func is_open() -> bool:
	return panel.visible


## The id of the entry being shown, or "" when the box is closed.
func current_id() -> String:
	return str(_entry.get("id", ""))


func play(entry: Dictionary) -> void:
	if DialogueData.line_count(entry) == 0:
		return
	_entry = entry
	_index = 0
	panel.visible = true
	_show_line()


func _process(delta: float) -> void:
	if not is_open():
		return
	var total := text_label.text.length()
	if _shown < total:
		_shown = minf(_shown + CHARS_PER_SECOND * delta, total)
		text_label.visible_characters = int(_shown)
	_blink += delta
	more_label.visible = _shown >= total and fmod(_blink, BLINK_PERIOD) < BLINK_PERIOD * 0.6


func _unhandled_input(event: InputEvent) -> void:
	if not is_open():
		return
	if event.is_action_pressed("interact") or event.is_action_pressed("jump"):
		advance()
		get_viewport().set_input_as_handled()


## Finishes the typing, or turns to the next page, or closes the box.
func advance() -> void:
	if not is_open():
		return
	if _shown < text_label.text.length():
		_shown = text_label.text.length()
		text_label.visible_characters = -1
		return
	_index += 1
	if _index >= DialogueData.line_count(_entry):
		close()
	else:
		_show_line()


func close() -> void:
	if not is_open():
		return
	panel.visible = false
	var entry := _entry
	_entry = {}
	finished.emit(entry)


func _show_line() -> void:
	var line := DialogueData.line(_entry, _index)
	name_label.text = line["who"]
	name_label.visible = not line["who"].is_empty()
	text_label.text = line["text"]
	text_label.visible_characters = 0
	_shown = 0.0
	_blink = 0.0
	more_label.visible = false
