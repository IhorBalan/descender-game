class_name SettingsMenu
extends Control

## The settings screen: volume, fullscreen and keyboard bindings. Shared by
## the title screen and the pause menu, which show it and listen for
## `closed`. Pressing a key button waits for the next key and binds it;
## Esc cancels the wait.

signal closed

var _listening_action: StringName = &""
var _key_buttons: Dictionary = {}

@onready var volume_slider: HSlider = $Menu/Volume/Slider
@onready var fullscreen_toggle: CheckButton = $Menu/Fullscreen/Toggle
@onready var keys_grid: GridContainer = $Menu/Keys
@onready var reset_button: Button = $Menu/Buttons/ResetKeys
@onready var back_button: Button = $Menu/Buttons/Back
@onready var hint: Label = $Hint


func _ready() -> void:
	volume_slider.value_changed.connect(_on_volume_changed)
	fullscreen_toggle.toggled.connect(_on_fullscreen_toggled)
	reset_button.pressed.connect(_on_reset_pressed)
	back_button.pressed.connect(close)
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
	_refresh()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if _listening_action != &"":
		if event is InputEventKey and event.pressed and not event.echo:
			if event.keycode != KEY_ESCAPE:
				Settings.bind_key(_listening_action, event.physical_keycode)
				Settings.save_settings()
			_listening_action = &""
			_refresh()
			get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()


func open() -> void:
	_listening_action = &""
	_refresh()
	visible = true
	volume_slider.grab_focus()


func close() -> void:
	visible = false
	closed.emit()


func _refresh() -> void:
	volume_slider.set_value_no_signal(Settings.volume * 100.0)
	fullscreen_toggle.set_pressed_no_signal(Settings.fullscreen)
	fullscreen_toggle.text = "On" if Settings.fullscreen else "Off"
	for action in _key_buttons:
		var button: Button = _key_buttons[action]
		if action == _listening_action:
			button.text = "Press a key..."
		else:
			button.text = Settings.key_names(action)
	hint.text = (
		"Press the new key, or Esc to keep the old one"
		if _listening_action != &""
		else "Arrows move   Space changes   Esc goes back"
	)


func _on_volume_changed(value: float) -> void:
	Settings.volume = value / 100.0
	Settings.apply()
	Settings.save_settings()


func _on_fullscreen_toggled(pressed: bool) -> void:
	Settings.fullscreen = pressed
	Settings.apply()
	Settings.save_settings()


func _on_key_button_pressed(action: StringName) -> void:
	_listening_action = action
	_refresh()


func _on_reset_pressed() -> void:
	Settings.reset_keys()
	Settings.save_settings()
	_refresh()
