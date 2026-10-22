class_name ShopMenu
extends CanvasLayer

## A trader's wares: the stall's name and greeting, the hero's purse and
## one button per ware with its price. Space buys (equipment once, then
## the line reads "sold"; consumables as often as the purse allows), the
## line under the list says what the picked ware does or why it could not
## be bought, and Esc leaves. The tree is paused while it is open, like
## the pause menu; main.gd opens it when a trading NPC's talk ends.

signal closed

const SOLD_COLOR := Color(0.55, 0.52, 0.7)
const WARE_COLOR := Color(0.95, 0.95, 1.0)
const NOTE_COLOR := Color(0.7, 0.7, 0.78)
const WARN_COLOR := Color(1.0, 0.7, 0.6)
const BOUGHT_COLOR := Color(0.75, 1.0, 0.8)

## The Shops id open right now.
var shop_id := ""

@onready var heading: Label = $Panel/Heading
@onready var greeting: Label = $Panel/Greeting
@onready var purse_label: Label = $Panel/Purse
@onready var list: VBoxContainer = $Panel/Scroll/List
@onready var description: Label = $Panel/Description
@onready var back_button: Button = $Panel/Back


func _ready() -> void:
	visible = false
	back_button.pressed.connect(close)
	back_button.focus_entered.connect(_show_note.bind("", NOTE_COLOR))
	Audio.wire_menu(self)


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("pause"):
		close()
		get_viewport().set_input_as_handled()


func is_open() -> bool:
	return visible


## Pauses the tree and shows a trader's wares.
func open(id: String) -> void:
	if not Shops.has(id):
		push_warning("ShopMenu: no shop called '%s'" % id)
		return
	shop_id = id
	var shop := Shops.get_shop(id)
	heading.text = str(shop.get("name", "Trader")).to_upper()
	greeting.text = str(shop.get("greeting", ""))
	get_tree().paused = true
	visible = true
	_show_note("", NOTE_COLOR)
	refresh()


func close() -> void:
	visible = false
	get_tree().paused = false
	closed.emit()


## Rebuilds the wares from the stall and the purse.
func refresh() -> void:
	purse_label.text = purse_text()
	var focused := -1
	for i in list.get_child_count():
		if list.get_child(i).has_focus():
			focused = i
	for child in list.get_children():
		list.remove_child(child)
		child.queue_free()
	var stock: Array = Shops.get_shop(shop_id).get("stock", [])
	for ware: Dictionary in stock:
		var item_id := str(ware.get("item", ""))
		var button := Button.new()
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		var sold := Shops.sold_out(shop_id, item_id)
		var price := "sold" if sold else Inventory.price_text(Shops.price_of(ware))
		button.text = "%s   %s" % [Items.display_name(item_id).rpad(22), price]
		button.add_theme_color_override("font_color", SOLD_COLOR if sold else WARE_COLOR)
		button.pressed.connect(_on_ware_pressed.bind(ware))
		button.focus_entered.connect(_on_ware_focused.bind(item_id))
		list.add_child(button)
	Audio.wire_menu(list)
	if focused >= 0 and focused < list.get_child_count():
		list.get_child(focused).grab_focus()
	elif list.get_child_count() > 0:
		list.get_child(0).grab_focus()
	else:
		back_button.grab_focus()


## The purse as one line: "12 light   3 tally bones".
static func purse_text() -> String:
	var parts: PackedStringArray = []
	for currency in Inventory.CURRENCIES:
		parts.append(Inventory.price_text({currency: Inventory.amount(currency)}))
	return "   ".join(parts)


func _on_ware_focused(item_id: String) -> void:
	var text := Items.description(item_id)
	var what := Items.summary(item_id)
	if not what.is_empty():
		text += "  (%s)" % what
	_show_note(text, NOTE_COLOR)


func _on_ware_pressed(ware: Dictionary) -> void:
	var item_id := str(ware.get("item", ""))
	var reason := Shops.buy(shop_id, ware)
	if reason.is_empty():
		Audio.play("pickup")
		refresh()
		_show_note("Bought: %s. It is in the pack." % Items.display_name(item_id), BOUGHT_COLOR)
	else:
		Audio.play("menu_back")
		_show_note(reason, WARN_COLOR)


func _show_note(text: String, color: Color) -> void:
	description.text = text
	description.add_theme_color_override("font_color", color)


## The ware lines shown, for the test.
func lines() -> Array[String]:
	var result: Array[String] = []
	for child in list.get_children():
		if child is Button:
			result.append(child.text)
	return result
