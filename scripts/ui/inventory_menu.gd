class_name InventoryMenu
extends Control

## The pack page of the pause menu: the purse, what is worn in each slot,
## then one button per item. Space on a piece of equipment wears it (or
## takes it off, when it is worn); on a consumable, uses it. The kept
## things of the story (the scarf, the clock pieces) and the church iron
## not yet forged are listed after, since they are flags rather than
## items. The line under the list describes the picked item.

signal closed

const WORN_MARK := "* "
const ITEM_COLOR := Color(0.95, 0.95, 1.0)
const WORN_COLOR := Color(1.0, 0.95, 0.7)
const KEPT_COLOR := Color(0.7, 0.7, 0.78)
const NOTE_COLOR := Color(0.7, 0.7, 0.78)
const WARN_COLOR := Color(1.0, 0.7, 0.6)
const DONE_COLOR := Color(0.75, 1.0, 0.8)

## The hero, for wearing and using; set by the pause menu when it opens.
var player: Player

@onready var purse_label: Label = $Purse
@onready var worn_label: Label = $Worn
@onready var list: VBoxContainer = $Scroll/List
@onready var empty_label: Label = $Empty
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
	if list.get_child_count() > 0 and list.get_child(0) is Button:
		list.get_child(0).grab_focus()
	else:
		back_button.grab_focus()


func close() -> void:
	visible = false
	closed.emit()


## Rebuilds the page from the pack and the story flags.
func refresh() -> void:
	purse_label.text = ShopMenu.purse_text()
	var worn: PackedStringArray = []
	for slot: int in Items.Slot.values():
		var id := Inventory.equipped_id(slot)
		worn.append("%s: %s" % [Items.SLOT_NAMES[slot], Items.display_name(id) if id else "-"])
	worn_label.text = "   ".join(worn)
	var focused := -1
	for i in list.get_child_count():
		if list.get_child(i).has_focus():
			focused = i
	for child in list.get_children():
		list.remove_child(child)
		child.queue_free()
	var ids := Inventory.sorted_ids()
	for id in ids:
		var button := Button.new()
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		var count := Inventory.count(id)
		var is_worn := Inventory.is_equipped(id)
		var text := "%s%s" % [WORN_MARK if is_worn else "  ", Items.display_name(id)]
		if count > 1:
			text += " x%d" % count
		var what := Items.summary(id)
		if not what.is_empty():
			text = "%s   %s" % [text.rpad(26), what]
		button.text = text
		button.add_theme_color_override("font_color", WORN_COLOR if is_worn else ITEM_COLOR)
		button.pressed.connect(_on_item_pressed.bind(id))
		button.focus_entered.connect(_on_item_focused.bind(id))
		list.add_child(button)
	var kept := kept_things()
	if not kept.is_empty():
		_add_line("Kept things:", KEPT_COLOR)
		for name in kept:
			_add_line("   %s" % name, KEPT_COLOR)
	empty_label.visible = ids.is_empty() and kept.is_empty()
	Audio.wire_menu(list)
	if visible:
		_focus_near(focused)


## Puts the focus back on the row that had it, or the nearest one left,
## so the arrows keep working after a row goes.
func _focus_near(index: int) -> void:
	var buttons: Array[Button] = []
	for child in list.get_children():
		if child is Button:
			buttons.append(child)
	if buttons.is_empty():
		back_button.grab_focus()
	else:
		buttons[clampi(index, 0, buttons.size() - 1)].grab_focus()


## The things the story has handed the hero, by their flags, and the
## church iron he carries for Pell.
static func kept_things() -> Array[String]:
	var result: Array[String] = []
	for flag in Items.KEPT_FLAGS:
		if Story.has_flag(flag):
			result.append(str(Items.KEPT_FLAGS[flag]))
	var iron := Upgrade.iron_carried()
	if iron > 0:
		result.append("Church iron x%d" % iron)
	return result


func _add_line(text: String, color: Color) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_color_override("font_color", color)
	list.add_child(label)


func _on_item_focused(id: String) -> void:
	_show_note(Items.description(id), NOTE_COLOR)


func _on_item_pressed(id: String) -> void:
	if player == null:
		return
	if Items.is_equipment(id):
		var worn := not Inventory.is_equipped(id)
		player.toggle_equipment(id)
		refresh()
		_show_note("%s: %s" % ["Worn" if worn else "Taken off", Items.display_name(id)], DONE_COLOR)
		return
	var reason := player.use_item(id)
	if reason.is_empty():
		refresh()
		_show_note("Used: %s" % Items.display_name(id), DONE_COLOR)
	else:
		Audio.play("menu_back")
		_show_note(reason, WARN_COLOR)


func _show_note(text: String, color: Color) -> void:
	description.text = text
	description.add_theme_color_override("font_color", color)


## The item lines shown, for the test.
func lines() -> Array[String]:
	var result: Array[String] = []
	for child in list.get_children():
		if child is Button:
			result.append(child.text)
	return result
