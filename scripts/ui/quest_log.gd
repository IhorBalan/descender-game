class_name QuestLog
extends Control

## The promises page of the pause menu: every side quest the hero has taken
## on, its steps ticked as the story flags come in, the kept ones last.

signal closed

const OPEN_COLOR := Color(1.0, 0.95, 0.7)
const DONE_COLOR := Color(0.55, 0.52, 0.7)
const STEP_COLOR := Color(0.9, 0.9, 1.0)

@onready var list: VBoxContainer = $List
@onready var empty_label: Label = $Empty
@onready var back_button: Button = $Back


func _ready() -> void:
	visible = false
	back_button.pressed.connect(close)


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("pause"):
		close()
		get_viewport().set_input_as_handled()


func open() -> void:
	refresh()
	visible = true
	back_button.grab_focus()


func close() -> void:
	visible = false
	closed.emit()


## Rebuilds the list from the story flags.
func refresh() -> void:
	for child in list.get_children():
		list.remove_child(child)
		child.queue_free()
	var quests := Quests.active()
	empty_label.visible = quests.is_empty()
	for quest in quests:
		var done := Quests.is_done(quest)
		var title := Label.new()
		title.text = "%s%s" % [quest["title"], "  (kept)" if done else ""]
		title.add_theme_color_override("font_color", DONE_COLOR if done else OPEN_COLOR)
		list.add_child(title)
		for step in Quests.steps(quest):
			var line := Label.new()
			line.text = "   %s %s" % ["+" if step["done"] else "-", step["text"]]
			line.add_theme_color_override(
				"font_color", DONE_COLOR if step["done"] or done else STEP_COLOR
			)
			list.add_child(line)
		var gap := Control.new()
		gap.custom_minimum_size = Vector2(0, 4)
		list.add_child(gap)


## The titles shown, for the test.
func titles() -> Array[String]:
	var result: Array[String] = []
	for child in list.get_children():
		if child is Label and not child.text.begins_with("   "):
			result.append(child.text)
	return result
