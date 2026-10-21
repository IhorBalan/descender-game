class_name DialogueBox
extends CanvasLayer

## The text box at the bottom of the screen: a speaker name and one box of
## text at a time, typed out. The typing rests a moment on punctuation and
## ticks with a soft blip, pitched by who is speaking; the name takes the
## speaker's colour, and a stage note (no speaker) is grey and silent.
## Interact (or jump) finishes the typing, then turns the page; the last
## page closes the box. A line with `choices` shows them once it is typed
## out: left and right pick, interact confirms, and the picked choice's
## `then` goes out with the entry. main.gd opens the box with an entry from
## DialogueData and freezes the hero while it is open.

signal finished(entry: Dictionary)

const CHARS_PER_SECOND := 45.0
const BLINK_PERIOD := 0.8
const CHOICE_GAP := "      "
## How long the typing rests after a sentence, and after a comma.
const PAUSE_SENTENCE := 0.2
const PAUSE_COMMA := 0.08
const SENTENCE_ENDS := ".!?"
const COMMA_ENDS := ",;:"
## One blip every this many characters typed.
const BLIP_EVERY := 3
const BLIP_VOLUME_DB := -14.0
const NAME_COLOR := Color(1.0, 0.95, 0.7)
const TEXT_COLOR := Color(0.95, 0.95, 1.0)
## A line nobody speaks: a stage note in parentheses, or a description.
const NOTE_COLOR := Color(0.7, 0.7, 0.78)
## Who speaks in which colour; anyone else gets NAME_COLOR. The Voice is
## the Choir's light, Tobiah and the demons the colour of the deep
## circles, the Gardener the sun that never sets.
const SPEAKER_COLORS := {
	"Ansel": Color(1.0, 0.95, 0.7),
	"Voice": Color(0.75, 0.85, 1.0),
	"The Voice": Color(0.75, 0.85, 1.0),
	"Tobiah": Color(0.95, 0.6, 0.55),
	"Gorrow": Color(0.95, 0.55, 0.45),
	"Vael": Color(0.85, 0.65, 0.95),
	"The Penitent": Color(0.8, 0.8, 0.85),
	"Warden": Color(0.8, 0.9, 1.0),
	"The Gardener": Color(1.0, 0.85, 0.5),
	"Hanne": Color(1.0, 0.8, 0.75),
}
## How the blip is pitched for each speaker; anyone else is 1.0, and a
## line with no speaker types in silence.
const SPEAKER_PITCH := {
	"Ansel": 1.0,
	"Voice": 0.65,
	"The Voice": 0.65,
	"Tobiah": 0.85,
	"Gorrow": 0.7,
	"Vael": 1.2,
	"Hanne": 1.35,
	"Old Pell": 0.8,
	"Corporal Dace": 0.9,
	"Sister Ava": 1.1,
	"Mereth": 1.05,
	"Ithuriel": 0.95,
	"The Ferryman": 0.75,
	"The Gardener": 0.6,
	"Warden": 0.8,
	"The Penitent": 0.9,
}

var _entry: Dictionary = {}
var _index := 0
var _shown := 0.0
var _blink := 0.0
var _choices: Array = []
var _choice := 0
## Seconds the typing still rests after the last punctuation mark.
var _pause := 0.0
var _since_blip := 0
## The blip pitch for the line being shown; 0 types in silence.
var _blip_pitch := 1.0

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
	tick(delta)


## The typing's clock: `delta` seconds of typing, resting and blinking.
## Stepped by _process; the smoke test steps it by hand.
func tick(delta: float) -> void:
	if not is_open():
		return
	var total := text_label.text.length()
	if _shown < total:
		if _pause > 0.0:
			_pause -= delta
		else:
			_type(delta, total)
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


## Types the next characters: a blip every few of them, and a rest after
## the mark that ends a sentence or a clause (an ellipsis rests once, at
## its end, and the end of the box does not rest at all).
func _type(delta: float, total: int) -> void:
	var before := int(_shown)
	_shown = minf(_shown + CHARS_PER_SECOND * delta, total)
	var after := int(_shown)
	if after == before:
		return
	text_label.visible_characters = after
	var text := text_label.text
	var last := text[after - 1]
	if after < total:
		if last in SENTENCE_ENDS and text[after] != last:
			_pause = PAUSE_SENTENCE
		elif last in COMMA_ENDS:
			_pause = PAUSE_COMMA
	_since_blip += after - before
	if _since_blip >= BLIP_EVERY and _blip_pitch > 0.0:
		_since_blip = 0
		Audio.play("talk", BLIP_VOLUME_DB, _blip_pitch)


## Whether the typing is resting on a punctuation mark.
func is_pausing() -> bool:
	return is_open() and _pause > 0.0 and _shown < text_label.text.length()


## The colour a speaker's name is shown in.
static func speaker_color(who: String) -> Color:
	return SPEAKER_COLORS.get(who, NAME_COLOR)


## The blip pitch for a speaker: 0 for a line nobody speaks (a stage note
## or an upgrade's description), which types in silence.
static func speaker_pitch(who: String) -> float:
	if who.is_empty():
		return 0.0
	return float(SPEAKER_PITCH.get(who, 1.0))


## Finishes the typing, or turns to the next page, or closes the box.
func advance() -> void:
	if not is_open():
		return
	if _shown < text_label.text.length():
		_shown = text_label.text.length()
		_pause = 0.0
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
	var who: String = line["who"]
	var text: String = line["text"]
	name_label.text = who
	name_label.visible = not who.is_empty()
	name_label.add_theme_color_override("font_color", speaker_color(who))
	text_label.text = text
	text_label.add_theme_color_override(
		"font_color", TEXT_COLOR if not who.is_empty() else NOTE_COLOR
	)
	text_label.visible_characters = 0
	_shown = 0.0
	_pause = 0.0
	_since_blip = 0
	_blip_pitch = speaker_pitch(who)
	_blink = 0.0
	more_label.visible = false
	_choices = DialogueData.choices(_entry, _index)
	_choice = 0
	choice_label.visible = false
	choice_label.text = _choice_text()
