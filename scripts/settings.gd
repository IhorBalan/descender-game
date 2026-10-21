extends Node

## Player settings: master volume, fullscreen, keyboard and gamepad bindings.
##
## Registered as the Settings autoload. Values live in user://settings.cfg
## and are applied to the audio bus, the window and the InputMap when the
## game starts and whenever the settings screen changes them. Keyboard keys
## and gamepad buttons are rebound separately; the stick axes stay as the
## project defines them.

const PATH := "user://settings.cfg"
## The actions the settings screen lets the player rebind, in menu order.
## Every keyboard action is here, so a key moved to one action is taken
## away from whichever other action had it.
const REBINDABLE_ACTIONS: Array[StringName] = [
	&"move_left",
	&"move_right",
	&"move_down",
	&"jump",
	&"attack",
	&"dash",
	&"cast",
	&"heal",
	&"interact",
	&"pause",
	&"restart",
]
## Short names for the gamepad buttons, in Xbox layout.
const PAD_BUTTON_NAMES := {
	JOY_BUTTON_A: "A",
	JOY_BUTTON_B: "B",
	JOY_BUTTON_X: "X",
	JOY_BUTTON_Y: "Y",
	JOY_BUTTON_BACK: "Back",
	JOY_BUTTON_GUIDE: "Guide",
	JOY_BUTTON_START: "Start",
	JOY_BUTTON_LEFT_STICK: "LS",
	JOY_BUTTON_RIGHT_STICK: "RS",
	JOY_BUTTON_LEFT_SHOULDER: "LB",
	JOY_BUTTON_RIGHT_SHOULDER: "RB",
	JOY_BUTTON_DPAD_UP: "D-Up",
	JOY_BUTTON_DPAD_DOWN: "D-Down",
	JOY_BUTTON_DPAD_LEFT: "D-Left",
	JOY_BUTTON_DPAD_RIGHT: "D-Right",
}
const ACTION_LABELS := {
	&"move_left": "Move left",
	&"move_right": "Move right",
	&"move_down": "Move down",
	&"jump": "Jump",
	&"attack": "Attack",
	&"dash": "Dash",
	&"cast": "Holy Light",
	&"heal": "Heal",
	&"interact": "Talk",
	&"pause": "Pause",
	&"restart": "Restart",
}

var volume := 1.0
var fullscreen := false
## Saved keys per action: physical keycodes. Actions not listed keep the
## project's defaults.
var keys: Dictionary = {}
## Saved gamepad buttons per action: JoyButton indices. Same rule.
var pad_buttons: Dictionary = {}

## The project's own bindings, kept so they can be reset.
var _default_keys: Dictionary = {}
var _default_pad_buttons: Dictionary = {}


func _ready() -> void:
	for action in REBINDABLE_ACTIONS:
		_default_keys[action] = _keyboard_keys(action)
		_default_pad_buttons[action] = _joypad_buttons(action)
	load_settings()
	apply()


func load_settings() -> void:
	var config := ConfigFile.new()
	if config.load(PATH) != OK:
		return
	volume = clampf(float(config.get_value("audio", "volume", 1.0)), 0.0, 1.0)
	fullscreen = bool(config.get_value("display", "fullscreen", false))
	keys = _load_bindings(config, "keys")
	pad_buttons = _load_bindings(config, "pad")


func _load_bindings(config: ConfigFile, section: String) -> Dictionary:
	var out := {}
	for action in REBINDABLE_ACTIONS:
		var saved: Variant = config.get_value(section, action, null)
		if typeof(saved) != TYPE_ARRAY:
			continue
		var codes: Array[int] = []
		for code in saved:
			codes.append(int(code))
		out[action] = codes
	return out


func save_settings() -> void:
	var config := ConfigFile.new()
	config.set_value("audio", "volume", volume)
	config.set_value("display", "fullscreen", fullscreen)
	for action in keys:
		config.set_value("keys", action, keys[action])
	for action in pad_buttons:
		config.set_value("pad", action, pad_buttons[action])
	if config.save(PATH) != OK:
		push_warning("Could not save settings to %s" % PATH)


## Pushes every setting to the engine.
func apply() -> void:
	var bus := AudioServer.get_bus_index("Master")
	AudioServer.set_bus_volume_db(bus, linear_to_db(maxf(volume, 0.0001)))
	AudioServer.set_bus_mute(bus, volume <= 0.0)
	var mode := (
		DisplayServer.WINDOW_MODE_FULLSCREEN if fullscreen else DisplayServer.WINDOW_MODE_WINDOWED
	)
	if DisplayServer.window_get_mode() != mode:
		DisplayServer.window_set_mode(mode)
	for action in REBINDABLE_ACTIONS:
		_set_keyboard_keys(action, keys.get(action, _default_keys[action]))
		_set_joypad_buttons(action, pad_buttons.get(action, _default_pad_buttons[action]))


## The physical keycodes bound to an action right now.
func action_keys(action: StringName) -> Array[int]:
	return keys.get(action, _default_keys.get(action, []))


## Makes `keycode` the action's only key. Another action already using the
## key loses it, so a key never does two things.
func bind_key(action: StringName, keycode: int) -> void:
	for other in REBINDABLE_ACTIONS:
		if other != action and keycode in action_keys(other):
			var remaining: Array[int] = action_keys(other).duplicate()
			remaining.erase(keycode)
			keys[other] = remaining
	var codes: Array[int] = [keycode]
	keys[action] = codes
	apply()


## The gamepad buttons bound to an action right now.
func action_pad_buttons(action: StringName) -> Array[int]:
	return pad_buttons.get(action, _default_pad_buttons.get(action, []))


## Makes `button` the action's only gamepad button, taking it away from any
## other action that had it.
func bind_pad_button(action: StringName, button: int) -> void:
	for other in REBINDABLE_ACTIONS:
		if other != action and button in action_pad_buttons(other):
			var remaining: Array[int] = action_pad_buttons(other).duplicate()
			remaining.erase(button)
			pad_buttons[other] = remaining
	var buttons: Array[int] = [button]
	pad_buttons[action] = buttons
	apply()


## Puts both the keys and the gamepad buttons back to the project's own.
func reset_keys() -> void:
	keys = {}
	pad_buttons = {}
	apply()


func key_names(action: StringName) -> String:
	var names: PackedStringArray = []
	for code in action_keys(action):
		var event := InputEventKey.new()
		event.physical_keycode = code
		names.append(event.as_text_physical_keycode())
	return " / ".join(names) if names.size() > 0 else "none"


func pad_button_names(action: StringName) -> String:
	var names: PackedStringArray = []
	for button in action_pad_buttons(action):
		names.append(PAD_BUTTON_NAMES.get(button, "Button %d" % button))
	return " / ".join(names) if names.size() > 0 else "none"


func _keyboard_keys(action: StringName) -> Array[int]:
	var codes: Array[int] = []
	for event in InputMap.action_get_events(action):
		if event is InputEventKey:
			codes.append(event.physical_keycode)
	return codes


func _joypad_buttons(action: StringName) -> Array[int]:
	var buttons: Array[int] = []
	for event in InputMap.action_get_events(action):
		if event is InputEventJoypadButton:
			buttons.append(event.button_index)
	return buttons


func _set_joypad_buttons(action: StringName, buttons: Array[int]) -> void:
	for event in InputMap.action_get_events(action):
		if event is InputEventJoypadButton:
			InputMap.action_erase_event(action, event)
	for button in buttons:
		var event := InputEventJoypadButton.new()
		event.button_index = button as JoyButton
		InputMap.action_add_event(action, event)


func _set_keyboard_keys(action: StringName, codes: Array[int]) -> void:
	for event in InputMap.action_get_events(action):
		if event is InputEventKey:
			InputMap.action_erase_event(action, event)
	for code in codes:
		var event := InputEventKey.new()
		event.physical_keycode = code
		InputMap.action_add_event(action, event)
