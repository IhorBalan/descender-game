class_name DialogueBox
extends CanvasLayer

## The text box at the bottom of the screen: a speaker name and one box of
## text at a time, typed out. Interact (or jump) finishes the typing, then
## turns the page; the last page closes the box. A line with `choices`
## shows them once it is typed out: left and right pick, interact confirms,
## and the picked choice's `then` goes out with the entry. main.gd opens
## the box with an entry from DialogueData and freezes the hero while it
## is open.

signal finished(entry: Dictionary)

const CHARS_PER_SECOND := 45.0
const BLINK_PERIOD := 0.8
const CHOICE_GAP := "      "

var _entry: Dictionary = {}
var _index := 0
var _shown := 0.0
var _blink := 0.0
var _choices: Array = []
var _choice := 0

@onready var panel: Control = $Panel
@onready var name_label: Label = $Panel/Name
@onready var text_label: Label = $Panel/Text
@onready var more_label: Label = $Panel/More
@onready var choice_label: Label = $Panel/Choice


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
	var typed := _shown >= total
	if has_choice() and typed:
		choice_label.visible = true
		more_label.visible = false
		return
	more_label.visible = typed and fmod(_blink, BLINK_PERIOD) < BLINK_PERIOD * 0.6


func _unhandled_input(event: InputEvent) -> void:
	if not is_open():
		return
	if event.is_action_pressed("interact") or event.is_action_pressed("jump"):
		advance()
		get_viewport().set_input_as_handled()
	elif _choosing() and event.is_action_pressed("move_right"):
		_select(_choice + 1)
		get_viewport().set_input_as_handled()
	elif _choosing() and event.is_action_pressed("move_left"):
		_select(_choice - 1)
		get_viewport().set_input_as_handled()


## Finishes the typing, or turns to the next page, or closes the box.
func advance() -> void:
	if not is_open():
		return
	if _shown < text_label.text.length():
		_shown = text_label.text.length()
		text_label.visible_characters = -1
		return
	if has_choice():
		choose(_choice)
		return
	_index += 1
	if _index >= DialogueData.line_count(_entry):
		close()
	else:
		_show_line()


## Whether the line being shown ends in a choice.
func has_choice() -> bool:
	return not _choices.is_empty()


## The choice the cursor is on.
func selected_choice() -> int:
	return _choice


## Takes a choice: the entry goes out with the choice's `then` and index.
func choose(index: int) -> void:
	if not has_choice() or index < 0 or index >= _choices.size():
		return
	var picked: Dictionary = _choices[index] if _choices[index] is Dictionary else {}
	_entry["then"] = str(picked.get("then", ""))
	_entry["chosen"] = index
	close()


## Whether the choices are up: the line has them and is typed out.
func _choosing() -> bool:
	return has_choice() and _shown >= text_label.text.length()


func _select(index: int) -> void:
	_choice = clampi(index, 0, _choices.size() - 1)
	choice_label.text = _choice_text()


func _choice_text() -> String:
	var parts: Array[String] = []
	for i in _choices.size():
		var text := str(_choices[i].get("text", "")) if _choices[i] is Dictionary else ""
		parts.append(("> " if i == _choice else "  ") + text)
	return CHOICE_GAP.join(parts)


func close() -> void:
	if not is_open():
		return
	panel.visible = false
	choice_label.visible = false
	_choices = []
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
	_choices = DialogueData.choices(_entry, _index)
	_choice = 0
	choice_label.visible = false
	choice_label.text = _choice_text()
