class_name SettingsMenu
extends Control

## The settings screen, in four pages: Audio (master, music and sound
## effect volumes, the dialogue blips), Display (fullscreen, window size,
## v-sync, screen shake), Controls (keyboard and gamepad bindings) and
## Game (text speed, hit flashes, hit pause, gamepad rumble).
##
## Shared by the title screen and the pause menu, which show it and listen
## for `closed`. The page buttons along the top switch pages, as do Q/E and
## the shoulder buttons. The bindings grid shows one device at a time; the
## Device button flips between keyboard and gamepad. Pressing a binding
## button waits for the next key or gamepad button and binds it; Esc
## cancels. Every change is applied at once and written to the settings
## file when the control is let go of or the screen closes.

signal closed

enum Page { AUDIO, DISPLAY, CONTROLS, GAME }
enum Device { KEYBOARD, GAMEPAD }

const DEFAULT_HINT := "Arrows move   Space changes   Q/E or LB/RB switch pages   Esc goes back"

var page := Page.AUDIO
var device := Device.KEYBOARD

var _listening_action: StringName = &""
var _key_buttons: Dictionary = {}
## Page -> its tab button, its container and the control focused first.
var _tabs: Dictionary = {}
var _pages: Dictionary = {}
var _first_controls: Dictionary = {}

@onready var back_button: Button = $Tabs/Back
@onready var volume_slider: HSlider = $Pages/Audio/Master/Slider
@onready var music_slider: HSlider = $Pages/Audio/Music/Slider
@onready var sfx_slider: HSlider = $Pages/Audio/Sfx/Slider
@onready var volume_value: Label = $Pages/Audio/Master/Value
@onready var music_value: Label = $Pages/Audio/Music/Value
@onready var sfx_value: Label = $Pages/Audio/Sfx/Value
@onready var blips_toggle: CheckButton = $Pages/Audio/Blips/Toggle
@onready var audio_defaults_button: Button = $Pages/Audio/Buttons/Defaults
@onready var fullscreen_toggle: CheckButton = $Pages/Display/Fullscreen/Toggle
@onready var scale_button: Button = $Pages/Display/Scale/Cycle
@onready var vsync_toggle: CheckButton = $Pages/Display/Vsync/Toggle
@onready var shake_button: Button = $Pages/Display/Shake/Cycle
@onready var display_defaults_button: Button = $Pages/Display/Buttons/Defaults
@onready var device_button: Button = $Pages/Controls/KeysRow/Device
@onready var reset_button: Button = $Pages/Controls/KeysRow/ResetKeys
@onready var keys_grid: GridContainer = $Pages/Controls/Keys
@onready var text_speed_button: Button = $Pages/Game/TextSpeed/Cycle
@onready var hit_flash_toggle: CheckButton = $Pages/Game/HitFlash/Toggle
@onready var hit_stop_toggle: CheckButton = $Pages/Game/HitStop/Toggle
@onready var rumble_toggle: CheckButton = $Pages/Game/Rumble/Toggle
@onready var game_defaults_button: Button = $Pages/Game/Buttons/Defaults
@onready var hint: Label = $Hint


func _ready() -> void:
	_tabs = {
		Page.AUDIO: $Tabs/Audio,
		Page.DISPLAY: $Tabs/Display,
		Page.CONTROLS: $Tabs/Controls,
		Page.GAME: $Tabs/Game,
	}
	_pages = {
		Page.AUDIO: $Pages/Audio,
		Page.DISPLAY: $Pages/Display,
		Page.CONTROLS: $Pages/Controls,
		Page.GAME: $Pages/Game,
	}
	_first_controls = {
		Page.AUDIO: volume_slider,
		Page.DISPLAY: fullscreen_toggle,
		Page.CONTROLS: device_button,
		Page.GAME: text_speed_button,
	}
	for which in _tabs:
		var tab: Button = _tabs[which]
		tab.pressed.connect(_on_tab_pressed.bind(which))
	back_button.pressed.connect(close)
	_wire_volume(volume_slider, _on_volume_changed)
	_wire_volume(music_slider, _on_music_changed)
	_wire_volume(sfx_slider, _on_sfx_changed)
	blips_toggle.toggled.connect(_on_blips_toggled)
	audio_defaults_button.pressed.connect(_on_audio_defaults_pressed)
	fullscreen_toggle.toggled.connect(_on_fullscreen_toggled)
	vsync_toggle.toggled.connect(_on_vsync_toggled)
	_wire_cycle(scale_button, _on_scale_stepped)
	_wire_cycle(shake_button, _on_shake_stepped)
	display_defaults_button.pressed.connect(_on_display_defaults_pressed)
	_wire_cycle(text_speed_button, _on_text_speed_stepped)
	hit_flash_toggle.toggled.connect(_on_hit_flash_toggled)
	hit_stop_toggle.toggled.connect(_on_hit_stop_toggled)
	rumble_toggle.toggled.connect(_on_rumble_toggled)
	game_defaults_button.pressed.connect(_on_game_defaults_pressed)
	reset_button.pressed.connect(_on_reset_pressed)
	device_button.pressed.connect(_on_device_pressed)
	# Two actions per row: label, key, label, key.
	for action in Settings.REBINDABLE_ACTIONS:
		var label := Label.new()
		label.text = Settings.ACTION_LABELS[action]
		label.custom_minimum_size.x = 44
		keys_grid.add_child(label)
		var button := Button.new()
		button.custom_minimum_size.x = 70
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(_on_key_button_pressed.bind(action))
		keys_grid.add_child(button)
		_key_buttons[action] = button
	_show_page(page)
	_refresh()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if _listening_action != &"":
		if event is InputEventKey and event.pressed and not event.echo:
			if event.keycode == KEY_ESCAPE:
				_stop_listening()
			elif device == Device.KEYBOARD:
				Settings.bind_key(_listening_action, event.physical_keycode)
				Settings.save_settings()
				_stop_listening()
			get_viewport().set_input_as_handled()
		elif event is InputEventJoypadButton and event.pressed and device == Device.GAMEPAD:
			Settings.bind_pad_button(_listening_action, event.button_index)
			Settings.save_settings()
			_stop_listening()
			get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()
		return
	var step := _page_step(event)
	if step != 0:
		_show_page(wrapi(page + step, 0, Page.size()) as Page)
		_first_controls[page].grab_focus()
		get_viewport().set_input_as_handled()


func open() -> void:
	_listening_action = &""
	_show_page(page)
	_refresh()
	visible = true
	_first_controls[page].grab_focus()


func close() -> void:
	# Keyboard changes to the sliders are applied as they happen and written
	# here, in one go.
	_listening_action = &""
	Settings.save_settings()
	visible = false
	closed.emit()


## Q/E and the shoulder buttons turn pages: -1 back, 1 forward, 0 neither.
func _page_step(event: InputEvent) -> int:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.physical_keycode == KEY_Q:
			return -1
		if event.physical_keycode == KEY_E:
			return 1
	elif event is InputEventJoypadButton and event.pressed:
		if event.button_index == JOY_BUTTON_LEFT_SHOULDER:
			return -1
		if event.button_index == JOY_BUTTON_RIGHT_SHOULDER:
			return 1
	return 0


func _show_page(which: Page) -> void:
	page = which
	_listening_action = &""
	for other in _pages:
		_pages[other].visible = other == which
		_tabs[other].set_pressed_no_signal(other == which)
	_refresh()


func _wire_volume(slider: HSlider, on_changed: Callable) -> void:
	slider.value_changed.connect(on_changed)
	slider.drag_ended.connect(_on_slider_drag_ended)


## A cycle button steps forward when pressed and either way with the
## arrow keys or the stick, which it keeps from moving the focus.
func _wire_cycle(button: Button, on_stepped: Callable) -> void:
	button.pressed.connect(on_stepped.bind(1))
	button.gui_input.connect(_on_cycle_input.bind(button, on_stepped))


func _on_cycle_input(event: InputEvent, button: Button, on_stepped: Callable) -> void:
	var step := 0
	if event.is_action_pressed("ui_left"):
		step = -1
	elif event.is_action_pressed("ui_right"):
		step = 1
	if step == 0:
		return
	button.accept_event()
	on_stepped.call(step)
	Audio.play("menu_move")


func _refresh() -> void:
	volume_slider.set_value_no_signal(Settings.volume * 100.0)
	music_slider.set_value_no_signal(Settings.music_volume * 100.0)
	sfx_slider.set_value_no_signal(Settings.sfx_volume * 100.0)
	volume_value.text = _percent(Settings.volume)
	music_value.text = _percent(Settings.music_volume)
	sfx_value.text = _percent(Settings.sfx_volume)
	fullscreen_toggle.set_pressed_no_signal(Settings.fullscreen)
	fullscreen_toggle.text = "On" if Settings.fullscreen else "Off"
	vsync_toggle.set_pressed_no_signal(Settings.vsync)
	vsync_toggle.text = "On" if Settings.vsync else "Off"
	var size: Vector2i = Settings.window_size_for(Settings.window_scale)
	scale_button.text = "< %dx  %d x %d >" % [Settings.window_scale, size.x, size.y]
	shake_button.text = "< %s >" % Settings.shake_name(Settings.screen_shake)
	_refresh_toggle(blips_toggle, Settings.voice_blips)
	text_speed_button.text = "< %s >" % Settings.text_speed_name()
	_refresh_toggle(hit_flash_toggle, Settings.hit_flash)
	_refresh_toggle(hit_stop_toggle, Settings.hit_stop)
	_refresh_toggle(rumble_toggle, Settings.rumble)
	var gamepad := device == Device.GAMEPAD
	device_button.text = "Gamepad" if gamepad else "Keyboard"
	for action in _key_buttons:
		var button: Button = _key_buttons[action]
		if action == _listening_action:
			button.text = "Press a button..." if gamepad else "Press a key..."
		elif gamepad:
			button.text = Settings.pad_button_names(action)
		else:
			button.text = Settings.key_names(action)
	if _listening_action == &"":
		hint.text = DEFAULT_HINT
	elif gamepad:
		hint.text = "Press the new gamepad button, or Esc to keep the old one"
	else:
		hint.text = "Press the new key, or Esc to keep the old one"


func _refresh_toggle(toggle: CheckButton, on: bool) -> void:
	toggle.set_pressed_no_signal(on)
	toggle.text = "On" if on else "Off"


func _percent(linear: float) -> String:
	return "%d%%" % roundi(linear * 100.0)


func _stop_listening() -> void:
	_listening_action = &""
	_refresh()


func _on_tab_pressed(which: Page) -> void:
	_show_page(which)
	_tabs[which].grab_focus()


func _on_volume_changed(value: float) -> void:
	Settings.volume = value / 100.0
	Settings.apply()
	volume_value.text = _percent(Settings.volume)
	Audio.play("menu_move")


func _on_music_changed(value: float) -> void:
	Settings.music_volume = value / 100.0
	Settings.apply()
	music_value.text = _percent(Settings.music_volume)


func _on_sfx_changed(value: float) -> void:
	Settings.sfx_volume = value / 100.0
	Settings.apply()
	sfx_value.text = _percent(Settings.sfx_volume)
	Audio.play("menu_move")


func _on_slider_drag_ended(_changed: bool) -> void:
	Settings.save_settings()


func _on_blips_toggled(pressed: bool) -> void:
	Settings.voice_blips = pressed
	Settings.save_settings()
	_refresh()


func _on_audio_defaults_pressed() -> void:
	Settings.reset_audio()
	Settings.save_settings()
	_refresh()


func _on_fullscreen_toggled(pressed: bool) -> void:
	Settings.fullscreen = pressed
	Settings.apply()
	Settings.save_settings()
	_refresh()


func _on_vsync_toggled(pressed: bool) -> void:
	Settings.vsync = pressed
	Settings.apply()
	Settings.save_settings()
	_refresh()


func _on_scale_stepped(step: int) -> void:
	Settings.window_scale = wrapi(
		Settings.window_scale + step, Settings.MIN_WINDOW_SCALE, Settings.MAX_WINDOW_SCALE + 1
	)
	Settings.apply()
	Settings.save_settings()
	_refresh()


func _on_shake_stepped(step: int) -> void:
	var levels: Array[float] = Settings.SHAKE_LEVELS
	var index := levels.find(Settings.screen_shake)
	if index < 0:
		index = levels.size() - 1
	Settings.screen_shake = levels[wrapi(index + step, 0, levels.size())]
	Settings.apply()
	Settings.save_settings()
	_refresh()


func _on_display_defaults_pressed() -> void:
	Settings.reset_display()
	Settings.save_settings()
	_refresh()


func _on_text_speed_stepped(step: int) -> void:
	Settings.text_speed = wrapi(Settings.text_speed + step, 0, Settings.TEXT_SPEEDS.size())
	Settings.save_settings()
	_refresh()


func _on_hit_flash_toggled(pressed: bool) -> void:
	Settings.hit_flash = pressed
	Settings.save_settings()
	_refresh()


func _on_hit_stop_toggled(pressed: bool) -> void:
	Settings.hit_stop = pressed
	Settings.save_settings()
	_refresh()


func _on_rumble_toggled(pressed: bool) -> void:
	Settings.rumble = pressed
	Settings.save_settings()
	_refresh()


func _on_game_defaults_pressed() -> void:
	Settings.reset_game()
	Settings.save_settings()
	_refresh()


func _on_key_button_pressed(action: StringName) -> void:
	_listening_action = action
	_refresh()


func _on_device_pressed() -> void:
	device = Device.GAMEPAD if device == Device.KEYBOARD else Device.KEYBOARD
	_listening_action = &""
	_refresh()


func _on_reset_pressed() -> void:
	Settings.reset_keys()
	Settings.save_settings()
	_refresh()
