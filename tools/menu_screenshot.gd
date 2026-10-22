extends SceneTree

## Renders the title screen, the credits, the new game pickers, the four
## pages of the settings screen and the pause menu and saves screenshots.
## Needs a display (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/menu_screenshot.gd -- /tmp/shots
##
## The steps are ten ticks apart: a slow software renderer runs several
## physics ticks per frame, and a step has to be drawn before the next.

var _ticks := 0
var _out_dir := "/tmp"
var _title: Node
var _main: Node


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	DirAccess.make_dir_recursive_absolute(_out_dir)
	_title = load("res://scenes/title.tscn").instantiate()
	root.add_child(_title)


func _physics_process(_delta: float) -> bool:
	_ticks += 1
	match _ticks:
		10:
			_save("title.png")
		12:
			_title.get_node("Menu/Credits").pressed.emit()
		22:
			_save("credits.png")
		24:
			_title.get_node("Credits/Back").pressed.emit()
			_title.get_node("Menu/NewGame").pressed.emit()
		34:
			_save("new_game_slots.png")
		36:
			_title.get_node("Slots/Slot1").pressed.emit()
		46:
			_save("new_game_difficulty.png")
		48:
			_title.get_node("Difficulty/Back").pressed.emit()
			_title.get_node("Slots/Back").pressed.emit()
			_title.get_node("Menu/Settings").pressed.emit()
		58:
			_save("settings_audio.png")
		60:
			_settings_menu().get_node("Tabs/Display").pressed.emit()
		70:
			_save("settings_display.png")
		72:
			_settings_menu().get_node("Tabs/Controls").pressed.emit()
		82:
			_save("settings_controls.png")
		84:
			_settings_menu().get_node("Tabs/Game").pressed.emit()
		94:
			_save("settings_game.png")
		96:
			# The pause menu, over the start of the world.
			root.remove_child(_title)
			_title.free()
			_main = load("res://scenes/main.tscn").instantiate()
			root.add_child(_main)
		140:
			_main.pause_menu.open(_main.pause_status())
		150:
			_save("pause.png")
		156:
			quit()
			return true
	return false


## Typed as a Node: naming the SettingsMenu class here would compile the
## game scripts before the autoloads they use exist.
func _settings_menu() -> Node:
	return _title.get_node("SettingsMenu")


func _save(name: String) -> void:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
