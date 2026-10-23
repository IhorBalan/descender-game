extends Node

## Player settings: volumes, window, screen shake, game feel and
## accessibility, keyboard and gamepad bindings, the gamepad's prompts.
##
## Registered as the Settings autoload. Values live in user://settings.cfg
## and are applied to the audio buses, the window and the InputMap when the
## game starts and whenever the settings screen changes them. Keyboard keys
## and gamepad buttons are rebound separately; the stick axes stay as the
## project defines them.

const PATH := "user://settings.cfg"
## The game renders at this size; the window is a whole multiple of it.
const BASE_SIZE := Vector2i(320, 180)
const MIN_WINDOW_SCALE := 2
const MAX_WINDOW_SCALE := 6
const DEFAULT_WINDOW_SCALE := 4
## Screen shake amounts the settings screen cycles through: off, half, full.
const SHAKE_LEVELS: Array[float] = [0.0, 0.5, 1.0]
## Dialogue typing speeds, in characters per second, and their names in
## the settings screen. The last is instant: the whole page at once.
const TEXT_SPEED_NAMES: Array[String] = ["Slow", "Normal", "Fast", "Instant"]
const TEXT_SPEEDS: Array[float] = [25.0, 45.0, 90.0, 0.0]
const DEFAULT_TEXT_SPEED := 1
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

## Master, music (the ambient loops) and sound effect volumes, 0 to 1.
var volume := 1.0
var music_volume := 1.0
var sfx_volume := 1.0
var fullscreen := false
## Window size as a multiple of BASE_SIZE, used when not fullscreen.
var window_scale := DEFAULT_WINDOW_SCALE
var vsync := true
## Multiplier for camera shake, 0 to 1; see Feedback.shake.
var screen_shake := 1.0
## Index into TEXT_SPEEDS: how fast dialogue types.
var text_speed := DEFAULT_TEXT_SPEED
## Whether a hit flashes the sprite white; off for players flashing bothers.
var hit_flash := true
## Whether a hit freezes the game for a blink; see Feedback.hit_stop.
var hit_stop := true
## Whether hits rumble a connected gamepad.
var rumble := true
## Whether dialogue ticks with a blip as it types.
var voice_blips := true
## Which gamepad's prompts the hints show: a Prompts.Layout, where AUTO
## follows the pad the player picks up.
var prompt_layout := Prompts.Layout.AUTO
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
	volume = _volume_from(config, "volume")
	music_volume = _volume_from(config, "music")
	sfx_volume = _volume_from(config, "sfx")
	fullscreen = bool(config.get_value("display", "fullscreen", false))
	window_scale = clampi(
		int(config.get_value("display", "scale", DEFAULT_WINDOW_SCALE)),
		MIN_WINDOW_SCALE,
		MAX_WINDOW_SCALE
	)
	vsync = bool(config.get_value("display", "vsync", true))
	screen_shake = clampf(float(config.get_value("display", "shake", 1.0)), 0.0, 1.0)
	text_speed = clampi(
		int(config.get_value("game", "text_speed", DEFAULT_TEXT_SPEED)), 0, TEXT_SPEEDS.size() - 1
	)
	hit_flash = bool(config.get_value("game", "hit_flash", true))
	hit_stop = bool(config.get_value("game", "hit_stop", true))
	rumble = bool(config.get_value("game", "rumble", true))
	voice_blips = bool(config.get_value("audio", "blips", true))
	prompt_layout = (
		clampi(
			int(config.get_value("game", "prompts", Prompts.Layout.AUTO)),
			0,
			Prompts.LAYOUT_NAMES.size() - 1
		)
		as Prompts.Layout
	)
	keys = _load_bindings(config, "keys")
	pad_buttons = _load_bindings(config, "pad")


func _volume_from(config: ConfigFile, key: String) -> float:
	return clampf(float(config.get_value("audio", key, 1.0)), 0.0, 1.0)


func _load_bindings(config: ConfigFile, section: String) -> Dictionary:
	var out := {}
	for action in REBINDABLE_ACTIONS:
		if not config.has_section_key(section, action):
			continue
		var saved: Variant = config.get_value(section, action)
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
	config.set_value("audio", "music", music_volume)
	config.set_value("audio", "sfx", sfx_volume)
	config.set_value("display", "fullscreen", fullscreen)
	config.set_value("display", "scale", window_scale)
	config.set_value("display", "vsync", vsync)
	config.set_value("display", "shake", screen_shake)
	config.set_value("game", "text_speed", text_speed)
	config.set_value("game", "hit_flash", hit_flash)
	config.set_value("game", "hit_stop", hit_stop)
	config.set_value("game", "rumble", rumble)
	config.set_value("audio", "blips", voice_blips)
	config.set_value("game", "prompts", prompt_layout)
	for action in keys:
		config.set_value("keys", action, keys[action])
	for action in pad_buttons:
		config.set_value("pad", action, pad_buttons[action])
	if config.save(PATH) != OK:
		push_warning("Could not save settings to %s" % PATH)


## Pushes every setting to the engine.
func apply() -> void:
	_set_bus_volume("Master", volume)
	_set_bus_volume("Music", music_volume)
	_set_bus_volume("SFX", sfx_volume)
	_apply_window()
	Prompts.set_layout(prompt_layout)
	for action in REBINDABLE_ACTIONS:
		_set_keyboard_keys(action, keys.get(action, _default_keys[action]))
		_set_joypad_buttons(action, pad_buttons.get(action, _default_pad_buttons[action]))


## Puts the volumes back to full and the blips on.
func reset_audio() -> void:
	volume = 1.0
	music_volume = 1.0
	sfx_volume = 1.0
	voice_blips = true
	apply()


## Puts the window and the screen shake back to the project's defaults.
func reset_display() -> void:
	fullscreen = false
	window_scale = DEFAULT_WINDOW_SCALE
	vsync = true
	screen_shake = 1.0
	apply()


## Puts the typing speed and the game feel back to the project's defaults.
func reset_game() -> void:
	text_speed = DEFAULT_TEXT_SPEED
	hit_flash = true
	hit_stop = true
	rumble = true
	prompt_layout = Prompts.Layout.AUTO
	apply()


## How many characters a second dialogue types, or 0 for a whole page at
## once.
func chars_per_second() -> float:
	return TEXT_SPEEDS[clampi(text_speed, 0, TEXT_SPEEDS.size() - 1)]


## The name of the typing speed: "Slow", "Normal", "Fast" or "Instant".
func text_speed_name() -> String:
	return TEXT_SPEED_NAMES[clampi(text_speed, 0, TEXT_SPEED_NAMES.size() - 1)]


## The window size for a scale, for the settings screen to show.
func window_size_for(scale: int) -> Vector2i:
	return BASE_SIZE * scale


## The name of a screen shake level: "Off", "Half" or "Full".
func shake_name(amount: float) -> String:
	if amount <= 0.0:
		return "Off"
	return "Full" if amount >= 1.0 else "Half"


func _set_bus_volume(bus_name: String, linear: float) -> void:
	var bus := AudioServer.get_bus_index(bus_name)
	if bus < 0:
		push_warning("No audio bus named %s" % bus_name)
		return
	AudioServer.set_bus_volume_db(bus, linear_to_db(maxf(linear, 0.0001)))
	AudioServer.set_bus_mute(bus, linear <= 0.0)


## Sets the window mode, size and vsync. Does nothing headless, where there
## is no window.
func _apply_window() -> void:
	if DisplayServer.get_name() == "headless":
		return
	var mode := (
		DisplayServer.WINDOW_MODE_FULLSCREEN if fullscreen else DisplayServer.WINDOW_MODE_WINDOWED
	)
	if DisplayServer.window_get_mode() != mode:
		DisplayServer.window_set_mode(mode)
	if not fullscreen:
		var size := window_size_for(window_scale)
		if DisplayServer.window_get_size() != size:
			DisplayServer.window_set_size(size)
			# Keep the window on the screen it was on, centred.
			var screen := DisplayServer.window_get_current_screen()
			var usable := DisplayServer.screen_get_usable_rect(screen)
			DisplayServer.window_set_position(usable.position + (usable.size - size) / 2)
	var vsync_mode := DisplayServer.VSYNC_ENABLED if vsync else DisplayServer.VSYNC_DISABLED
	if DisplayServer.window_get_vsync_mode() != vsync_mode:
		DisplayServer.window_set_vsync_mode(vsync_mode)


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
		names.append(Prompts.key_name(code))
	return " / ".join(names) if names.size() > 0 else "none"


## The gamepad buttons bound to an action, named in the layout of the
## pad the player last used (Xbox names for the keyboard).
func pad_button_names(action: StringName) -> String:
	var names: PackedStringArray = []
	for button in action_pad_buttons(action):
		names.append(Prompts.button_name(button))
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
