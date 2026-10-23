class_name SkillMenu
extends Control

## The skills page of the checkpoint camp: the level, the experience
## towards the next and the points to spend, then one button per skill
## with its rank. Space on a skill the hero may learn puts a point into
## it (see Skills.raise); the line under the list describes the picked
## skill, or says why it cannot be raised. Spending asks for a hero, so
## the maximums follow (see learn); without one the page only shows.

signal closed
## A point went into a skill.
signal learned(id: String)

const RANK_MARKS := "IIIII"
const SKILL_COLOR := Color(0.95, 0.95, 1.0)
const OPEN_COLOR := Color(1.0, 0.95, 0.7)
const LOCKED_COLOR := Color(0.55, 0.52, 0.7)
const DONE_COLOR := Color(0.75, 1.0, 0.8)
const NOTE_COLOR := Color(0.7, 0.7, 0.78)
const WARN_COLOR := Color(1.0, 0.7, 0.6)

## The hero, for learning; set by the camp when it opens.
var player: Player

@onready var status_label: Label = $Status
@onready var points_label: Label = $Points
@onready var list: VBoxContainer = $Scroll/List
@onready var description: Label = $Description
@onready var back_button: Button = $Back


func _ready() -> void:
	visible = false
	back_button.pressed.connect(close)
	back_button.focus_entered.connect(_show_note.bind("", NOTE_COLOR))


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("pause"):
		close()
		get_viewport().set_input_as_handled()


func open() -> void:
	refresh()
	visible = true
	if list.get_child_count() > 0:
		list.get_child(0).grab_focus()
	else:
		back_button.grab_focus()


func close() -> void:
	visible = false
	closed.emit()


## The level line: "Level 4   130 / 160 experience" (at the cap, that it
## is the cap).
static func status_text() -> String:
	var progress := Skills.level_progress()
	if progress[1] <= 0:
		return "Level %d   the last" % Skills.level()
	return "Level %d   %d / %d experience" % [Skills.level(), progress[0], progress[1]]


## The points line: how many wait to be spent.
static func points_text() -> String:
	var points := Skills.points_left()
	if points <= 0:
		return "No skill point to spend: the next level gives one"
	return "%d skill point%s to spend" % [points, "" if points == 1 else "s"]


## Rebuilds the page from the skills.
func refresh() -> void:
	status_label.text = status_text()
	points_label.text = points_text()
	var focused := -1
	for i in list.get_child_count():
		if list.get_child(i).has_focus():
			focused = i
	for child in list.get_children():
		list.remove_child(child)
		child.queue_free()
	for id in Skills.ids():
		var button := Button.new()
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		var rank := Skills.rank(id)
		var marks := "%s%s" % [RANK_MARKS.substr(0, rank), "-".repeat(Skills.max_rank(id) - rank)]
		var text := "%s %s" % [Skills.display_name(id).rpad(14), marks.rpad(4)]
		var open := Skills.blocked_reason(id).is_empty()
		if Skills.level() < Skills.level_needed(id):
			text += "   level %d" % Skills.level_needed(id)
		else:
			text += "   %s" % Skills.summary(id)
		button.text = text
		var color := SKILL_COLOR
		if open:
			color = OPEN_COLOR
		elif Skills.level() < Skills.level_needed(id) or rank >= Skills.max_rank(id):
			color = LOCKED_COLOR
		button.add_theme_color_override("font_color", color)
		button.pressed.connect(_on_skill_pressed.bind(id))
		button.focus_entered.connect(_on_skill_focused.bind(id))
		list.add_child(button)
	Audio.wire_menu(list)
	if visible and focused >= 0 and focused < list.get_child_count():
		list.get_child(focused).grab_focus()


func _on_skill_focused(id: String) -> void:
	_show_note(Skills.description(id), NOTE_COLOR)


func _on_skill_pressed(id: String) -> void:
	var reason := "Skills are learned at a camp" if player == null else learn(player, id)
	if reason.is_empty():
		Audio.play("pickup")
		refresh()
		_show_note(
			"%s, rank %d of %d" % [Skills.display_name(id), Skills.rank(id), Skills.max_rank(id)],
			DONE_COLOR
		)
		learned.emit(id)
	else:
		Audio.play("menu_back")
		_show_note(reason, WARN_COLOR)


## Puts a point into a skill for the hero. The maximums follow and are
## filled, since he is resting at a camp. Returns "" when the point was
## spent, else why it was not (see Skills.raise).
static func learn(hero: Player, id: String) -> String:
	var reason := Skills.raise(id)
	if reason.is_empty():
		hero.refresh_equipment()
		hero.rest_at(hero.respawn_position)
	return reason


func _show_note(text: String, color: Color) -> void:
	description.text = text
	description.add_theme_color_override("font_color", color)


## The skill lines shown, for the test.
func lines() -> Array[String]:
	var result: Array[String] = []
	for child in list.get_children():
		if child is Button:
			result.append(child.text)
	return result
