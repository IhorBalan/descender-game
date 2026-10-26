extends CanvasLayer

## The developer console: a panel over the top of the screen where commands
## are typed, for debugging and playtesting. The grave key (`) opens and
## closes it; Escape closes it too. The game pauses while it is open and
## resumes when it closes (a pause menu that was already open stays
## paused).
##
## Registered as the DevConsole autoload. Commands live in a registry so
## other scripts can add their own: `DevConsole.register("name", callable,
## "usage", "what it does")`, where the callable takes the arguments after
## the name as a PackedStringArray and returns the text to print. The
## built-ins are `help`, `clear` and `show_boxes on|off`, which draws every
## collision shape over the game (see CollisionBoxes). Arrow up and down
## walk the history of typed lines.

## Printed when a command's callable returns nothing.
const DONE_TEXT := "ok"
const PROMPT := "> "
const HISTORY_LIMIT := 50
## The panel's share of the screen height.
const HEIGHT_FRACTION := 0.55
const FONT_SIZE := 8
const BACKGROUND := Color(0.04, 0.03, 0.08, 0.92)
const BORDER := Color(0.35, 0.3, 0.55)
const TEXT_COLOR := Color(0.9, 0.9, 1.0)
const ECHO_COLOR := Color(0.7, 0.68, 0.85)
const ERROR_COLOR := Color(1.0, 0.45, 0.4)
const MUTED_COLOR := Color(0.55, 0.52, 0.7)

var panel: PanelContainer
var output: RichTextLabel
var input: LineEdit

## The registered commands by name, each {"callable", "usage", "help"}.
var _commands: Dictionary = {}
var _history: PackedStringArray = []
## Where the arrow keys are in the history; history.size() is the blank line.
var _history_index := 0
## The tree was paused before the console opened, so closing leaves it so.
var _was_paused := false
## The collision overlay while `show_boxes` is on.
var _boxes: CollisionBoxes


func _ready() -> void:
	layer = 100
	process_mode = Node.PROCESS_MODE_ALWAYS
	visible = false
	_build_ui()
	register("help", _cmd_help, "help [command]", "Lists the commands, or explains one.")
	register("clear", _cmd_clear, "clear", "Clears the output.")
	register(
		"show_boxes",
		_cmd_show_boxes,
		"show_boxes on|off",
		(
			"Draws the collision boxes: hero green, enemies red, items blue, "
			+ "walkable world black, hazards orange."
		)
	)


func _build_ui() -> void:
	panel = PanelContainer.new()
	panel.name = "Panel"
	panel.anchor_right = 1.0
	panel.anchor_bottom = HEIGHT_FRACTION
	var style := StyleBoxFlat.new()
	style.bg_color = BACKGROUND
	style.border_width_bottom = 1
	style.border_color = BORDER
	style.content_margin_left = 4.0
	style.content_margin_right = 4.0
	style.content_margin_top = 2.0
	style.content_margin_bottom = 2.0
	panel.add_theme_stylebox_override("panel", style)
	add_child(panel)

	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 1)
	panel.add_child(column)

	output = RichTextLabel.new()
	output.name = "Output"
	output.size_flags_vertical = Control.SIZE_EXPAND_FILL
	output.scroll_following = true
	output.scroll_active = false
	output.selection_enabled = false
	output.focus_mode = Control.FOCUS_NONE
	output.add_theme_font_size_override("normal_font_size", FONT_SIZE)
	output.add_theme_color_override("default_color", TEXT_COLOR)
	output.add_theme_stylebox_override("normal", StyleBoxEmpty.new())
	column.add_child(output)

	input = LineEdit.new()
	input.name = "Input"
	input.placeholder_text = "type a command, `help` lists them"
	input.caret_blink = true
	input.context_menu_enabled = false
	input.add_theme_font_size_override("font_size", FONT_SIZE)
	input.add_theme_color_override("font_color", TEXT_COLOR)
	input.add_theme_color_override("font_placeholder_color", MUTED_COLOR)
	input.add_theme_color_override("caret_color", TEXT_COLOR)
	var field := StyleBoxFlat.new()
	field.bg_color = Color(0.1, 0.08, 0.2)
	field.border_width_top = 1
	field.border_color = BORDER
	field.content_margin_left = 3.0
	field.content_margin_right = 3.0
	field.content_margin_top = 1.0
	field.content_margin_bottom = 1.0
	input.add_theme_stylebox_override("normal", field)
	input.add_theme_stylebox_override("focus", field)
	input.add_theme_constant_override("minimum_character_width", 1)
	input.text_submitted.connect(_on_submitted)
	input.gui_input.connect(_on_input_gui_input)
	column.add_child(input)


func _input(event: InputEvent) -> void:
	if event.is_action_pressed("dev_console"):
		toggle()
		get_viewport().set_input_as_handled()
	elif visible and event.is_action_pressed("ui_cancel"):
		close()
		get_viewport().set_input_as_handled()


func is_open() -> bool:
	return visible


func toggle() -> void:
	if visible:
		close()
	else:
		open()


## Shows the console over the game and pauses the tree under it.
func open() -> void:
	if visible:
		return
	_was_paused = get_tree().paused
	get_tree().paused = true
	visible = true
	input.clear()
	_history_index = _history.size()
	input.grab_focus()


func close() -> void:
	if not visible:
		return
	visible = false
	input.release_focus()
	get_tree().paused = _was_paused


# --- The registry -------------------------------------------------------


## Adds a command. `callable` takes the arguments typed after the name (a
## PackedStringArray) and returns the text to print, or nothing. Registering
## a name again replaces the earlier command.
func register(name: String, callable: Callable, usage := "", help := "") -> void:
	_commands[name] = {
		"callable": callable, "usage": usage if not usage.is_empty() else name, "help": help
	}


func unregister(name: String) -> void:
	_commands.erase(name)


func has_command(name: String) -> bool:
	return _commands.has(name)


## The registered command names, sorted.
func command_names() -> PackedStringArray:
	var names := PackedStringArray(_commands.keys())
	names.sort()
	return names


## Runs one typed line and returns what it printed: the command's answer,
## or an error for a blank line, an unknown name or a usage mistake.
func run(line: String) -> String:
	var words := line.strip_edges().split(" ", false)
	if words.is_empty():
		return ""
	var name := words[0]
	if not _commands.has(name):
		return "Unknown command '%s'. Type `help` for the list." % name
	var args := words.slice(1)
	var command: Dictionary = _commands[name]
	var callable: Callable = command["callable"]
	var result: Variant = callable.call(args)
	if result == null:
		return DONE_TEXT
	return str(result)


## Prints a line to the console's output, with its text escaped from BBCode.
func print_line(text: String, color := TEXT_COLOR) -> void:
	output.push_color(color)
	output.add_text(text)
	output.pop()
	output.newline()


func _on_submitted(line: String) -> void:
	input.clear()
	var trimmed := line.strip_edges()
	if trimmed.is_empty():
		return
	if _history.is_empty() or _history[_history.size() - 1] != trimmed:
		_history.append(trimmed)
		if _history.size() > HISTORY_LIMIT:
			_history.remove_at(0)
	_history_index = _history.size()
	print_line(PROMPT + trimmed, ECHO_COLOR)
	var answer := run(trimmed)
	var failed := answer.begins_with("Unknown command") or answer.begins_with("Usage:")
	for row in answer.split("\n"):
		print_line(row, ERROR_COLOR if failed else TEXT_COLOR)


## Arrow up and down walk the history into the input field.
func _on_input_gui_input(event: InputEvent) -> void:
	if not (event is InputEventKey and event.pressed):
		return
	var key := event as InputEventKey
	if key.keycode == KEY_UP:
		_recall(_history_index - 1)
	elif key.keycode == KEY_DOWN:
		_recall(_history_index + 1)
	else:
		return
	input.accept_event()


func _recall(index: int) -> void:
	_history_index = clampi(index, 0, _history.size())
	input.text = _history[_history_index] if _history_index < _history.size() else ""
	input.caret_column = input.text.length()


# --- Built-in commands --------------------------------------------------


func _cmd_help(args: PackedStringArray) -> String:
	if args.size() >= 1:
		var name := args[0]
		if not _commands.has(name):
			return "Unknown command '%s'. Type `help` for the list." % name
		var command: Dictionary = _commands[name]
		return "%s\n  %s" % [command["usage"], command["help"]]
	var rows := PackedStringArray()
	for name in command_names():
		var command: Dictionary = _commands[name]
		rows.append("%-22s %s" % [command["usage"], command["help"]])
	return "\n".join(rows)


func _cmd_clear(_args: PackedStringArray) -> String:
	output.clear()
	return ""


func _cmd_show_boxes(args: PackedStringArray) -> String:
	if args.is_empty():
		if boxes_shown() and _boxes.is_inside_tree():
			return "show_boxes is on: %d shapes tracked" % _boxes.shape_count()
		return "show_boxes is %s" % ("on" if boxes_shown() else "off")
	match args[0].to_lower():
		"on":
			set_boxes_shown(true)
			return "Collision boxes on"
		"off":
			set_boxes_shown(false)
			return "Collision boxes off"
	return "Usage: show_boxes on|off"


func boxes_shown() -> bool:
	return is_instance_valid(_boxes)


## Turns the collision overlay on or off (what `show_boxes` does). The
## overlay hangs under the root, so it outlives a level reload; it is added
## deferred, since a command can run while the root is still adding a scene.
func set_boxes_shown(shown: bool) -> void:
	if shown == boxes_shown():
		return
	if shown:
		_boxes = CollisionBoxes.new()
		get_tree().root.add_child.call_deferred(_boxes)
	else:
		_boxes.queue_free()
		_boxes = null
