extends Node

## Button prompts for whichever device the player last touched.
##
## Registered as the Prompts autoload. Every key or mouse press makes the
## keyboard the current device; every gamepad button or a stick pushed
## past half way makes that gamepad current, in its own layout: the
## PlayStation one when its name says so, the Xbox one otherwise (which
## also covers Switch and generic pads, whose buttons sit the same way).
## The settings can pin the layout. `device_changed` fires on a switch, so
## a hint can redraw itself; `PromptLabel` does that on its own.
##
## A hint is written as a template: the words with `{action}` tokens in
## them, one per InputMap action, which `render` turns into BBCode for a
## RichTextLabel: a key name on the keyboard, a small icon on a pad (see
## tools/gen_prompts.py). `{navigate}` stands for the arrow keys or the
## d-pad and stick in a menu, `{move}` for the move keys in the game. A
## token the InputMap does not know is left as it is, so a typo shows.
##
##   "{navigate} move   {ui_accept} choose   {ui_cancel} back"
##
## `icon` and `button_icon` hand out the textures for any other UI that
## wants to draw a prompt itself.
##
## It also gives the menus their gamepad buttons: Godot's own ui_accept
## and ui_cancel know only keys, so A (Cross) is added to accept and B
## (Circle) to cancel. The d-pad and the left stick already move focus.

signal device_changed(device: Device)

enum Device { KEYBOARD, XBOX, PLAYSTATION }
## The settings' choice of gamepad layout: follow the pad, or pin one.
enum Layout { AUTO, XBOX, PLAYSTATION }

const ICON_DIR := "res://godot_assets/ui/prompts"
## Key names are written in this colour, so they stand out from the hint.
const KEY_COLOR := "#ffe2b8"
## A stick has to be pushed this far before it counts as the player
## picking the pad up; a resting stick drifts a little.
const STICK_THRESHOLD := 0.5
const LAYOUT_NAMES: Array[String] = ["Auto", "Xbox", "PlayStation"]
## Words in a pad's name that mark it as a PlayStation one.
const PLAYSTATION_MARKS: Array[String] = [
	"playstation",
	"dualshock",
	"dualsense",
	"sony",
	"ps2",
	"ps3",
	"ps4",
	"ps5",
	"wireless controller"
]

## Icon file and short name per button, in the Xbox layout.
const XBOX_BUTTONS := {
	JOY_BUTTON_A: ["xbox_a", "A"],
	JOY_BUTTON_B: ["xbox_b", "B"],
	JOY_BUTTON_X: ["xbox_x", "X"],
	JOY_BUTTON_Y: ["xbox_y", "Y"],
	JOY_BUTTON_BACK: ["xbox_back", "View"],
	JOY_BUTTON_GUIDE: ["xbox_start", "Guide"],
	JOY_BUTTON_START: ["xbox_start", "Menu"],
	JOY_BUTTON_LEFT_STICK: ["xbox_ls", "LS"],
	JOY_BUTTON_RIGHT_STICK: ["xbox_rs", "RS"],
	JOY_BUTTON_LEFT_SHOULDER: ["xbox_lb", "LB"],
	JOY_BUTTON_RIGHT_SHOULDER: ["xbox_rb", "RB"],
	JOY_BUTTON_DPAD_UP: ["dpad_up", "D-Up"],
	JOY_BUTTON_DPAD_DOWN: ["dpad_down", "D-Down"],
	JOY_BUTTON_DPAD_LEFT: ["dpad_left", "D-Left"],
	JOY_BUTTON_DPAD_RIGHT: ["dpad_right", "D-Right"],
}
## The same in the PlayStation layout.
const PLAYSTATION_BUTTONS := {
	JOY_BUTTON_A: ["ps_cross", "Cross"],
	JOY_BUTTON_B: ["ps_circle", "Circle"],
	JOY_BUTTON_X: ["ps_square", "Square"],
	JOY_BUTTON_Y: ["ps_triangle", "Triangle"],
	JOY_BUTTON_BACK: ["ps_share", "Share"],
	JOY_BUTTON_GUIDE: ["ps_options", "PS"],
	JOY_BUTTON_START: ["ps_options", "Options"],
	JOY_BUTTON_LEFT_STICK: ["ps_l3", "L3"],
	JOY_BUTTON_RIGHT_STICK: ["ps_r3", "R3"],
	JOY_BUTTON_LEFT_SHOULDER: ["ps_l1", "L1"],
	JOY_BUTTON_RIGHT_SHOULDER: ["ps_r1", "R1"],
	JOY_BUTTON_DPAD_UP: ["dpad_up", "D-Up"],
	JOY_BUTTON_DPAD_DOWN: ["dpad_down", "D-Down"],
	JOY_BUTTON_DPAD_LEFT: ["dpad_left", "D-Left"],
	JOY_BUTTON_DPAD_RIGHT: ["dpad_right", "D-Right"],
}
## The triggers, by axis, and the sticks, by axis and sign.
const XBOX_AXES := {
	JOY_AXIS_TRIGGER_LEFT: ["xbox_lt", "LT"],
	JOY_AXIS_TRIGGER_RIGHT: ["xbox_rt", "RT"],
}
const PLAYSTATION_AXES := {
	JOY_AXIS_TRIGGER_LEFT: ["ps_l2", "L2"],
	JOY_AXIS_TRIGGER_RIGHT: ["ps_r2", "R2"],
}
## The sticks, by "axis:sign".
const STICK_AXES := {
	"0:-1": ["stick_left", "Stick left"],
	"0:1": ["stick_right", "Stick right"],
	"1:-1": ["stick_up", "Stick up"],
	"1:1": ["stick_down", "Stick down"],
	"2:-1": ["stick_left", "Right stick left"],
	"2:1": ["stick_right", "Right stick right"],
	"3:-1": ["stick_up", "Right stick up"],
	"3:1": ["stick_down", "Right stick down"],
}
## Key names shortened to fit a hint.
const KEY_SHORT_NAMES := {
	"Escape": "Esc",
	"Backspace": "Bksp",
	"Delete": "Del",
	"Insert": "Ins",
	"Page Up": "PgUp",
	"Page Down": "PgDn",
}

## The device the last input came from.
var device := Device.KEYBOARD
## The layout the settings ask for; AUTO follows the pad's name. Set it
## with `set_layout`, which redraws the hints.
var layout := Layout.AUTO

## The layout each connected pad was recognised as, by device id, and
## the pad the last input came from.
var _pad_layouts: Dictionary = {}
var _last_pad := 0
var _icons: Dictionary = {}


func _ready() -> void:
	# Menus pause the tree; the prompts keep following the player's hands.
	process_mode = Node.PROCESS_MODE_ALWAYS
	Input.joy_connection_changed.connect(_on_joy_connection_changed)
	_add_pad_button(&"ui_accept", JOY_BUTTON_A)
	_add_pad_button(&"ui_cancel", JOY_BUTTON_B)


func _input(event: InputEvent) -> void:
	if event is InputEventKey or event is InputEventMouseButton:
		_set_device(Device.KEYBOARD)
	elif event is InputEventJoypadButton:
		_last_pad = event.device
		_set_device(_pad_device(event.device))
	elif event is InputEventJoypadMotion and absf(event.axis_value) >= STICK_THRESHOLD:
		_last_pad = event.device
		_set_device(_pad_device(event.device))


## Whether the last input came from a gamepad.
func is_gamepad() -> bool:
	return device != Device.KEYBOARD


## Pins the gamepad layout (or lets it follow the pad again) and redraws
## the hints if the current device is a pad.
func set_layout(which: Layout) -> void:
	if which == layout:
		return
	layout = which
	_pad_layouts.clear()
	if is_gamepad():
		device = _pad_device(_last_pad)
		device_changed.emit(device)


## The name of the current device: "Keyboard", "Xbox" or "PlayStation".
func device_name(which: Device = device) -> String:
	match which:
		Device.XBOX:
			return "Xbox"
		Device.PLAYSTATION:
			return "PlayStation"
	return "Keyboard"


## Turns a template into BBCode for the current device; see the top of
## the file. Text outside the tokens is kept out of BBCode's way.
func render(template: String, which: Device = device) -> String:
	var out := ""
	var at := 0
	while at < template.length():
		var open := template.find("{", at)
		if open < 0:
			out += _escape(template.substr(at))
			break
		var close := template.find("}", open)
		if close < 0:
			out += _escape(template.substr(at))
			break
		out += _escape(template.substr(at, open - at))
		out += _token(template.substr(open + 1, close - open - 1), which)
		at = close + 1
	return out


## The BBCode for one action on the current device: a key name, or an
## icon per gamepad binding (the first two at most, so a hint stays short).
func prompt(action: StringName, which: Device = device) -> String:
	if which == Device.KEYBOARD:
		var code := _key_for(action)
		if code == KEY_NONE:
			return _key_text("none")
		return _key_text(key_name(code))
	var names := _pad_icon_names(action, which)
	if names.is_empty():
		return _key_text("none")
	var out := ""
	for i in mini(names.size(), 2):
		out += _img(names[i])
	return out


## The icon for an action's first gamepad binding, in the current layout
## (or `which`); null for the keyboard or an unbound action.
func icon(action: StringName, which: Device = device) -> Texture2D:
	if which == Device.KEYBOARD:
		return null
	var names := _pad_icon_names(action, which)
	return _icon(names[0]) if not names.is_empty() else null


## The icon for a gamepad button in a layout.
func button_icon(button: int, which: Device = device) -> Texture2D:
	var entry: Array = _buttons(which).get(button, [])
	return _icon(entry[0]) if not entry.is_empty() else null


## The short name of a gamepad button in a layout: "A" or "Cross".
func button_name(button: int, which: Device = device) -> String:
	var entry: Array = _buttons(which).get(button, [])
	return entry[1] if not entry.is_empty() else "Button %d" % button


## The short name of a key, from its physical keycode.
func key_name(code: int) -> String:
	var event := InputEventKey.new()
	event.physical_keycode = code as Key
	var full := event.as_text_physical_keycode()
	return KEY_SHORT_NAMES.get(full, full)


func _token(token: String, which: Device) -> String:
	match token:
		"navigate":
			if which == Device.KEYBOARD:
				return _key_text("Arrows")
			return _img("dpad") + _img("stick")
		"move":
			if which == Device.KEYBOARD:
				var left := _key_for(&"move_left")
				var right := _key_for(&"move_right")
				return _key_text("%s/%s" % [key_name(left), key_name(right)])
			return _img("stick") + _img("dpad")
	var action := StringName(token)
	if InputMap.has_action(action):
		return prompt(action, which)
	return _escape("{%s}" % token)


## The key shown for an action: its first key, except that Space stands
## for the UI's accept when it is bound, since the hints always said so.
func _key_for(action: StringName) -> int:
	var first := KEY_NONE
	for event in InputMap.action_get_events(action):
		if not event is InputEventKey:
			continue
		var code: int = event.physical_keycode
		if code == KEY_NONE:
			code = event.keycode
		if action == &"ui_accept" and code == KEY_SPACE:
			return code
		if first == KEY_NONE:
			first = code
	return first


## The icon names for an action's gamepad bindings, in binding order.
func _pad_icon_names(action: StringName, which: Device) -> Array[String]:
	var names: Array[String] = []
	var buttons := _buttons(which)
	var axes := _axes(which)
	for event in InputMap.action_get_events(action):
		var entry: Array = []
		if event is InputEventJoypadButton:
			entry = buttons.get(event.button_index, [])
		elif event is InputEventJoypadMotion:
			var sign := -1 if event.axis_value < 0.0 else 1
			entry = axes.get(event.axis, STICK_AXES.get("%d:%d" % [event.axis, sign], []))
		if not entry.is_empty() and entry[0] not in names:
			names.append(entry[0])
	return names


func _buttons(which: Device) -> Dictionary:
	return PLAYSTATION_BUTTONS if which == Device.PLAYSTATION else XBOX_BUTTONS


func _axes(which: Device) -> Dictionary:
	return PLAYSTATION_AXES if which == Device.PLAYSTATION else XBOX_AXES


func _icon(icon_name: String) -> Texture2D:
	if not _icons.has(icon_name):
		_icons[icon_name] = load("%s/%s.png" % [ICON_DIR, icon_name])
	return _icons[icon_name]


func _img(icon_name: String) -> String:
	return "[img]%s/%s.png[/img]" % [ICON_DIR, icon_name]


func _key_text(text: String) -> String:
	return "[color=%s]%s[/color]" % [KEY_COLOR, _escape(text)]


func _escape(text: String) -> String:
	return text.replace("[", "[lb]")


func _set_device(which: Device) -> void:
	if which == device:
		return
	device = which
	device_changed.emit(device)


## The layout of a connected pad: the pinned one, else from its name.
func _pad_device(pad: int) -> Device:
	if layout == Layout.XBOX:
		return Device.XBOX
	if layout == Layout.PLAYSTATION:
		return Device.PLAYSTATION
	if not _pad_layouts.has(pad):
		var joy_name := Input.get_joy_name(pad).to_lower()
		var found := Device.XBOX
		for mark in PLAYSTATION_MARKS:
			if joy_name.contains(mark):
				found = Device.PLAYSTATION
				break
		_pad_layouts[pad] = found
	return _pad_layouts[pad]


## Binds a gamepad button to a built-in action, once.
func _add_pad_button(action: StringName, button: JoyButton) -> void:
	var event := InputEventJoypadButton.new()
	event.button_index = button
	if not InputMap.action_has_event(action, event):
		InputMap.action_add_event(action, event)


func _on_joy_connection_changed(pad: int, _connected: bool) -> void:
	_pad_layouts.erase(pad)
