extends Node

## Player settings: master volume, fullscreen and keyboard bindings.
##
## Registered as the Settings autoload. Values live in user://settings.cfg
## and are applied to the audio bus, the window and the InputMap when the
## game starts and whenever the settings screen changes them. Only keyboard
## keys are rebound; gamepad buttons stay as the project defines them.

const PATH := "user://settings.cfg"
## The actions the settings screen lets the player rebind, in menu order.
## Every keyboard action is here, so a key moved to one action is taken
## away from whichever other action had it.
const REBINDABLE_ACTIONS: Array[StringName] = [
	&"move_left", &"move_right", &"jump", &"attack", &"dash", &"heal", &"pause", &"restart"
]
const ACTION_LABELS := {
	&"move_left": "Move left",
	&"move_right": "Move right",
	&"jump": "Jump",
	&"attack": "Attack",
	&"dash": "Dash",
	&"heal": "Heal",
	&"pause": "Pause",
	&"restart": "Restart",
}

var volume := 1.0
var fullscreen := false
## Saved keys per action: physical keycodes. Actions not listed keep the
## project's defaults.
var keys: Dictionary = {}

## The project's own keyboard bindings, kept so a key can be reset.
var _default_keys: Dictionary = {}


func _ready() -> void:
	for action in REBINDABLE_ACTIONS:
		_default_keys[action] = _keyboard_keys(action)
	load_settings()
	apply()


func load_settings() -> void:
	var config := ConfigFile.new()
	if config.load(PATH) != OK:
		return
	volume = clampf(float(config.get_value("audio", "volume", 1.0)), 0.0, 1.0)
	fullscreen = bool(config.get_value("display", "fullscreen", false))
	keys = {}
	for action in REBINDABLE_ACTIONS:
		if config.has_section_key("keys", action):
			var saved: Array = config.get_value("keys", action, [])
			var codes: Array[int] = []
			for code in saved:
				codes.append(int(code))
			keys[action] = codes


func save_settings() -> void:
	var config := ConfigFile.new()
	config.set_value("audio", "volume", volume)
	config.set_value("display", "fullscreen", fullscreen)
	for action in keys:
		config.set_value("keys", action, keys[action])
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


func reset_keys() -> void:
	keys = {}
	apply()


func key_names(action: StringName) -> String:
	var names: PackedStringArray = []
	for code in action_keys(action):
		var event := InputEventKey.new()
		event.physical_keycode = code
		names.append(event.as_text_physical_keycode())
	return " / ".join(names) if names.size() > 0 else "none"


func _keyboard_keys(action: StringName) -> Array[int]:
	var codes: Array[int] = []
	for event in InputMap.action_get_events(action):
		if event is InputEventKey:
			codes.append(event.physical_keycode)
	return codes


func _set_keyboard_keys(action: StringName, codes: Array[int]) -> void:
	for event in InputMap.action_get_events(action):
		if event is InputEventKey:
			InputMap.action_erase_event(action, event)
	for code in codes:
		var event := InputEventKey.new()
		event.physical_keycode = code
		InputMap.action_add_event(action, event)
